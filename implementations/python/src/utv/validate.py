"""Envelope and structural validation of UTV 1.0 JSON text.

Structural checks follow the bundled copy of ``schema/utv-1.0.schema.json``
using a small, standard-library-only interpreter for the JSON Schema keywords
that schema uses. Nothing here reads the clock, time zone or locale.
"""

from __future__ import annotations

import json
import re
from functools import lru_cache
from importlib import resources
from typing import Any

from .result import Diagnostic, Result, Status

SPEC_VERSION = "utv/1.0"
_WELL_FORMED = re.compile(r"utv/(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\Z")

CORE_KINDS = frozenset(
    {
        "instant", "civil", "amount", "interval", "recurrence", "uncertain", "set",
        "clock", "observation", "logical-clock", "relative", "relation", "assertion",
        "event-history", "location-intent", "constraint", "calendar-rule",
        "temporal-reference", "ordinal-position", "event-relative", "selection",
        "sample", "series", "temporal-graph", "sequence", "pattern",
    }
)


class _DuplicateKey(ValueError):
    pass


def _no_duplicates(pairs: list[tuple[str, Any]]) -> dict[str, Any]:
    out: dict[str, Any] = {}
    for key, value in pairs:
        if key in out:
            raise _DuplicateKey(key)
        out[key] = value
    return out


def _reject_constant(name: str) -> Any:
    raise ValueError(f"non-standard JSON constant {name}")


@lru_cache(maxsize=1)
def _schema() -> dict[str, Any]:
    text = resources.files("utv").joinpath("data/utv-1.0.schema.json").read_text(encoding="utf-8")
    return json.loads(text)


def _result(status: Status, diags: list[Diagnostic] | None = None) -> Result:
    return Result(operation="validate", status=status, diagnostics=diags or [])


def _err(code: str, path: str, message: str, severity: str = "error") -> Diagnostic:
    return Diagnostic(code=code, severity=severity, path=path, message=message)


def parse(text: str) -> Any:
    """Parse JSON text, rejecting duplicate keys and NaN/Infinity."""
    return json.loads(text, object_pairs_hook=_no_duplicates, parse_constant=_reject_constant)


def validate_structural(text: str) -> Result:
    """Structurally validate one UTV JSON document given as text."""
    try:
        doc = parse(text)
    except _DuplicateKey as exc:
        return _result(Status.INVALID, [_err("duplicate-key", "", f"duplicate JSON key {exc.args[0]!r}")])
    except (ValueError, RecursionError) as exc:
        return _result(Status.INVALID, [_err("malformed-json", "", str(exc))])
    return validate_value(doc)


def validate_value(doc: Any) -> Result:
    """Structurally validate an already-parsed UTV value (dict)."""
    if not isinstance(doc, dict):
        return _result(Status.INVALID, [_err("not-an-object", "", "a UTV value must be a JSON object")])
    schema_id = doc.get("schema")
    if schema_id != SPEC_VERSION:
        if isinstance(schema_id, str) and _WELL_FORMED.match(schema_id):
            return _result(
                Status.UNSUPPORTED,
                [_err("unsupported-version", "/schema", f"{schema_id} is not implemented; this reader implements {SPEC_VERSION}")],
            )
        return _result(Status.INVALID, [_err("unknown-schema", "/schema", f"unknown schema identifier {schema_id!r}")])
    diags: list[Diagnostic] = []
    _check(doc, {"$ref": "#/$defs/value"}, "", diags)
    return _result(Status.INVALID if diags else Status.VALID, diags)


# --- minimal JSON Schema interpreter -------------------------------------

_TYPES = {
    "object": lambda v: isinstance(v, dict),
    "array": lambda v: isinstance(v, list),
    "string": lambda v: isinstance(v, str),
    "boolean": lambda v: isinstance(v, bool),
    "null": lambda v: v is None,
    "integer": lambda v: (isinstance(v, int) and not isinstance(v, bool))
    or (isinstance(v, float) and v.is_integer()),
    "number": lambda v: isinstance(v, (int, float)) and not isinstance(v, bool),
}


def _resolve(ref: str) -> dict[str, Any]:
    if not ref.startswith("#/$defs/"):
        raise ValueError(f"unsupported $ref {ref}")
    return _schema()["$defs"][ref[len("#/$defs/"):]]


def _json_equal(a: Any, b: Any) -> bool:
    if isinstance(a, bool) or isinstance(b, bool):
        return type(a) is type(b) and a == b
    return a == b


def _valid(inst: Any, sch: Any) -> bool:
    tmp: list[Diagnostic] = []
    _check(inst, sch, "", tmp)
    return not tmp


def _check(inst: Any, sch: Any, path: str, diags: list[Diagnostic]) -> None:
    if sch is True:
        return
    if sch is False:
        diags.append(_err("schema-violation", path, "no value is allowed here"))
        return
    if "$ref" in sch:
        if sch["$ref"] == "#/$defs/value":
            _check_utv(inst, path, diags)
        else:
            _check(inst, _resolve(sch["$ref"]), path, diags)
    if "type" in sch:
        types = sch["type"] if isinstance(sch["type"], list) else [sch["type"]]
        if not any(_TYPES[t](inst) for t in types):
            diags.append(_err("wrong-type", path, f"expected {'/'.join(types)}"))
            return
    if "const" in sch and not _json_equal(inst, sch["const"]):
        diags.append(_err("const-mismatch", path, f"expected {sch['const']!r}"))
    if "enum" in sch and not any(_json_equal(inst, e) for e in sch["enum"]):
        diags.append(_err("enum-mismatch", path, f"expected one of {sch['enum']!r}"))
    if isinstance(inst, str):
        if "minLength" in sch and len(inst) < sch["minLength"]:
            diags.append(_err("too-short", path, f"string shorter than {sch['minLength']}"))
        if "pattern" in sch and not re.search(sch["pattern"], inst):
            diags.append(_err("pattern-mismatch", path, f"does not match {sch['pattern']}"))
    if isinstance(inst, list):
        if "minItems" in sch and len(inst) < sch["minItems"]:
            diags.append(_err("too-few-items", path, f"fewer than {sch['minItems']} items"))
        if "items" in sch:
            for i, item in enumerate(inst):
                _check(item, sch["items"], f"{path}/{i}", diags)
    if isinstance(inst, dict):
        props = sch.get("properties", {})
        for name in sch.get("required", []):
            if name not in inst:
                diags.append(_err("missing-property", f"{path}/{name}", f"required property {name!r} is missing"))
        addl = sch.get("additionalProperties", True)
        for name, val in inst.items():
            if name in props:
                _check(val, props[name], f"{path}/{name}", diags)
            elif addl is False:
                diags.append(_err("unknown-property", f"{path}/{name}", f"unknown property {name!r}"))
            elif isinstance(addl, dict):
                _check(val, addl, f"{path}/{name}", diags)
    if "anyOf" in sch and not any(_valid(inst, s) for s in sch["anyOf"]):
        diags.append(_err("schema-violation", path, "matches none of the allowed alternatives (anyOf)"))
    if "oneOf" in sch:
        n = sum(1 for s in sch["oneOf"] if _valid(inst, s))
        if n != 1:
            diags.append(_err("schema-violation", path, f"must match exactly one alternative (oneOf), matched {n}"))
    if "not" in sch and _valid(inst, sch["not"]):
        diags.append(_err("schema-violation", path, "matches a forbidden form (not)"))


def _check_utv(inst: Any, path: str, diags: list[Diagnostic]) -> None:
    """Check a (possibly nested) UTV envelope, dispatching on its kind."""
    if not isinstance(inst, dict):
        diags.append(_err("not-an-object", path, "a UTV value must be a JSON object"))
        return
    kind = inst.get("kind")
    if not isinstance(kind, str) or kind not in CORE_KINDS:
        diags.append(_err("unknown-kind", f"{path}/kind", f"{kind!r} is not a UTV 1.0 core kind"))
        return
    _check(inst, _resolve(f"#/$defs/{kind}Value"), path, diags)
