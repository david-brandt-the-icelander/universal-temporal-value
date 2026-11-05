"""Zone rules and the civil ↔ instant operations ``resolve``, ``to_zone``
and ``to_offset`` (docs/everyday-api.md).

Rules are always an explicit input: a bundle in the format of
``conformance/rules/demo-zones.json``, loaded with :func:`load_rules` or
:func:`parse_rules`. Nothing here reads the host's tz database.
"""

from __future__ import annotations

import bisect
import json
import os
from dataclasses import dataclass, field
from fractions import Fraction
from typing import Any, Mapping, Optional, Union

from ._core import (
    MAX_OFFSET,
    civil,
    civil_value,
    datetime_fields,
    instant_seconds,
    instant_value,
    invalid,
    operation,
    unique,
    unsupported,
)
from .result import Diagnostic, Result, Status


@dataclass(frozen=True)
class Zone:
    initial_offset: int
    transitions: tuple[tuple[int, int], ...]  # (at, offset), ascending

    def offset_at(self, t: Fraction) -> int:
        """The UTC offset in force at instant ``t`` (a transition applies from its own instant)."""
        i = bisect.bisect_right([at for at, _ in self.transitions], t)
        return self.transitions[i - 1][1] if i else self.initial_offset

    def gap_offset_before(self, local: Fraction) -> Optional[int]:
        """The offset in force just before the forward transition whose gap holds wall time ``local``."""
        before = self.initial_offset
        for at, offset in self.transitions:
            if at + before <= local < at + offset:
                return before
            before = offset
        return None

    def offsets_near(self, lo: Fraction, hi: Fraction) -> list[int]:
        """Every offset in force at some instant of ``[lo, hi]``."""
        found = [self.offset_at(lo)]
        found += [off for at, off in self.transitions if lo < at <= hi]
        return sorted(set(found))


@dataclass(frozen=True)
class ZoneRules:
    """A parsed, immutable rule bundle."""

    rule_set: dict[str, Any]
    valid_from: int
    valid_until: int
    zones: Mapping[str, Zone] = field(default_factory=dict)

    def manifest(self) -> dict[str, Any]:
        return {"dependencies": [dict(self.rule_set)]}


def parse_rules(source: Union[str, bytes, Mapping[str, Any]]) -> ZoneRules:
    """Parse a rule bundle from JSON text or an already-parsed dict.

    Raises ``ValueError`` if the bundle is malformed.
    """
    doc = json.loads(source) if isinstance(source, (str, bytes)) else source
    try:
        zones = {}
        for zone_id, z in doc["zones"].items():
            transitions = tuple((int(t["at"]), int(t["offset"])) for t in z["transitions"])
            if list(transitions) != sorted(transitions) or any(abs(o) > MAX_OFFSET for _, o in transitions):
                raise ValueError(f"transitions of {zone_id} are out of order or out of range")
            zones[zone_id] = Zone(int(z["initialOffset"]), transitions)
        return ZoneRules(dict(doc["ruleSet"]), int(doc["validFrom"]), int(doc["validUntil"]), zones)
    except (KeyError, TypeError, AttributeError) as exc:
        raise ValueError(f"malformed rule bundle: {exc!r}") from None


def load_rules(path: Union[str, "os.PathLike[str]"]) -> ZoneRules:
    """Read and parse a rule bundle from a file the caller names."""
    with open(path, encoding="utf-8") as fh:
        return parse_rules(fh.read())


def _rules(rules: Any) -> ZoneRules:
    if isinstance(rules, ZoneRules):
        return rules
    try:
        return parse_rules(rules)
    except (ValueError, TypeError):
        raise invalid("malformed-rules", "/1", "the rules argument is not a valid rule bundle") from None


_GAP = ("reject", "preserve-unresolved", "shift-forward-by-gap")
_OVERLAP = ("reject", "all", "earliest", "latest")


def _policy(policy: Optional[Mapping[str, Any]], value: dict[str, Any]) -> tuple[str, str]:
    chosen = policy if policy is not None else value["value"].get("resolutionPolicy", {})
    if not isinstance(chosen, Mapping):
        raise unsupported("unsupported-policy", "/2", "the policy must be {'gap': …, 'overlap': …}")
    gap, overlap = chosen.get("gap", "reject"), chosen.get("overlap", "reject")
    if gap not in _GAP:
        raise unsupported("unsupported-policy", "/2/gap", f"gap policy {gap!r} is not implemented")
    if overlap not in _OVERLAP:
        raise unsupported("unsupported-policy", "/2/overlap", f"overlap policy {overlap!r} is not implemented")
    return gap, overlap


@operation("resolve")
def resolve(value: Any, rules: Any, policy: Optional[Mapping[str, Any]] = None) -> Result:
    """Turn a local date-time into the instant(s) it names.

    ``policy`` is ``{"gap": …, "overlap": …}``; without it the value's own
    ``resolutionPolicy`` applies, then ``reject`` for both.
    """
    c = civil(value, "/0")
    if c.form != "datetime":
        raise unsupported("not-a-datetime", "/0/value/form", f"a {c.form} is not one instant")
    if c.zone == "floating":
        return Result(operation="resolve", status=Status.SYMBOLIC, diagnostics=[
            Diagnostic("no-zone", "info", "/0/value/zone", "no zone is bound, so there is nothing to resolve against yet")])
    local = c.wall_seconds()
    loss = {"classification": "information-losing", "fields": ["zone", "zoneIntent"]}
    if c.zone == "fixed-offset":
        return unique(instant_value(local - c.offset), loss=loss)  # type: ignore[operator]
    bundle = _rules(rules)
    zone = bundle.zones.get(c.zone_id)  # type: ignore[arg-type]
    if zone is None:
        raise unsupported("unknown-zone", "/0/value/zone/id", f"{c.zone_id} is not in rule set {bundle.rule_set.get('id')}")
    if local - 86400 < bundle.valid_from or local + 86400 >= bundle.valid_until:
        raise unsupported("outside-rules", "/0/value/fields", "the rules don't cover this date; never extrapolated")
    gap, overlap = _policy(policy, value)
    found = sorted(local - o for o in zone.offsets_near(local - 86400, local + 86400) if zone.offset_at(local - o) == o)
    manifest = bundle.manifest()
    asserted = value["value"].get("offsetAssertion")
    if asserted is not None:
        # The asserted offset selects among the candidates before any gap/overlap policy applies.
        found = [t for t in found if local - t == asserted]
        if not found:
            raise invalid("offset-mismatch", "/0/value/offsetAssertion",
                          f"{c.zone_id} is not at offset {asserted}s at this wall time under these rules")

    def answer(status: Status, candidates: list[Fraction], diags: Optional[list[Diagnostic]] = None) -> Result:
        return Result(operation="resolve", status=status, candidates=[instant_value(t) for t in candidates],
                      diagnostics=diags or [], manifest=manifest, loss=loss)

    if len(found) == 1:
        return answer(Status.UNIQUE, found)
    if not found:
        if gap == "shift-forward-by-gap":
            before = zone.gap_offset_before(local)
            if before is not None:
                return answer(Status.UNIQUE, [local - before])
        return Result(operation="resolve", status=Status.NONE, manifest=manifest, diagnostics=[
            Diagnostic("gap", "info", "/0/value/fields", f"this wall time doesn't exist in {c.zone_id} (a DST gap)")])
    if overlap == "earliest":
        return answer(Status.UNIQUE, found[:1])
    if overlap == "latest":
        return answer(Status.UNIQUE, found[-1:])
    diags = [Diagnostic("ambiguous", "info", "/0/value/fields", f"this wall time happens {len(found)} times in {c.zone_id}")]
    return answer(Status.MULTIPLE, found, diags if overlap == "reject" else [])


@operation("toZone")
def to_zone(instant: Any, zone_id: str, rules: Any) -> Result:
    """The wall time in a named zone at ``instant``, with its offset asserted."""
    t = instant_seconds(instant, "/0")
    bundle = _rules(rules)
    zone = bundle.zones.get(zone_id)
    if zone is None:
        raise unsupported("unknown-zone", "/1", f"{zone_id} is not in rule set {bundle.rule_set.get('id')}")
    if not bundle.valid_from <= t < bundle.valid_until:
        raise unsupported("outside-rules", "/0", "the rules don't cover this instant; never extrapolated")
    offset = zone.offset_at(t)
    extra = {"zone": {"type": "iana", "id": zone_id}, "zoneIntent": "follow-zone-id", "offsetAssertion": offset}
    return unique(civil_value("datetime", datetime_fields(t + offset), extra), manifest=bundle.manifest())


@operation("toOffset")
def to_offset(instant: Any, seconds: int) -> Result:
    """The wall time at a fixed UTC offset (seconds east of UTC) at ``instant``."""
    t = instant_seconds(instant, "/0")
    if isinstance(seconds, bool) or not isinstance(seconds, int):
        raise invalid("not-an-offset", "/1", "the offset must be an integer number of seconds")
    if abs(seconds) > MAX_OFFSET:
        raise invalid("offset-out-of-range", "/1", "offsets are limited to ±18:00")
    extra = {"zone": {"type": "fixed-offset", "seconds": seconds}, "zoneIntent": "fixed-offset"}
    return unique(civil_value("datetime", datetime_fields(t + seconds), extra))


__all__ = ["Zone", "ZoneRules", "load_rules", "parse_rules", "resolve", "to_offset", "to_zone"]
