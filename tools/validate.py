#!/usr/bin/env python3
"""Structural checks for the UTV specification repository.

This checks *structure only*. It does not perform semantic validation, resolution
or any other part of a UTV engine; see spec/SPECIFICATION.md for why those are
different things.

Checks performed:
  1. Each schema in schema/ is a valid JSON Schema (Draft 2020-12).
  2. Every value in conformance/examples/valid.json passes the schema.
  3. Every value in conformance/examples/invalid-structural.json fails the schema.
  4. Every complete UTV JSON example embedded in spec/*.md passes the schema.
  5. Scenario files in conformance/scenarios/ have unique IDs and required fields.
  6. Every falsehood ID in spec/FALSEHOODS-MATRIX.md has a scenario, and vice versa.
  7. Every value in examples/traditional.json is accepted by the schema exactly when the
     catalog expects structural status `valid`.

Usage:  python3 tools/validate.py          (from the repository root)
Requires: jsonschema >= 4.18  (pip install -r tools/requirements.txt)
"""
from __future__ import annotations

import json
import re
import sys
from pathlib import Path

from jsonschema import Draft202012Validator

ROOT = Path(__file__).resolve().parent.parent
SPEC_VERSION = "1.0"
SCHEMA_PATH = ROOT / "schema" / f"utv-{SPEC_VERSION}.schema.json"
WIRE_ID = f"utv/{SPEC_VERSION}"

failures: list[str] = []


def fail(msg: str) -> None:
    failures.append(msg)
    print(f"  FAIL  {msg}")


def ok(msg: str) -> None:
    print(f"  ok    {msg}")


def load_json(path: Path):
    def no_duplicates(pairs):
        keys = [k for k, _ in pairs]
        dupes = {k for k in keys if keys.count(k) > 1}
        if dupes:
            raise ValueError(f"duplicate keys {sorted(dupes)} in {path}")
        return dict(pairs)

    return json.loads(path.read_text(encoding="utf-8"), object_pairs_hook=no_duplicates)


def load_json_text(text: str):
    def no_duplicates(pairs):
        keys = [k for k, _ in pairs]
        if len(keys) != len(set(keys)):
            raise ValueError("duplicate keys")
        return dict(pairs)

    return json.loads(text, object_pairs_hook=no_duplicates)


def check_schemas() -> Draft202012Validator:
    print("Schemas")
    for path in sorted((ROOT / "schema").glob("*.schema.json")):
        Draft202012Validator.check_schema(load_json(path))
        ok(f"{path.relative_to(ROOT)} is a valid Draft 2020-12 schema")
    return Draft202012Validator(load_json(SCHEMA_PATH))


def check_examples(validator: Draft202012Validator) -> None:
    print("Examples")
    valid = load_json(ROOT / "conformance" / "examples" / "valid.json")
    for name, value in valid.items():
        errors = list(validator.iter_errors(value))
        if errors:
            fail(f"valid example '{name}' rejected: {errors[0].message[:200]}")
        elif value.get("schema") != WIRE_ID:
            fail(f"valid example '{name}' does not use wire identifier {WIRE_ID}")
    ok(f"{len(valid)} valid examples checked")

    invalid = load_json(ROOT / "conformance" / "examples" / "invalid-structural.json")
    for name, value in invalid.items():
        if validator.is_valid(value):
            fail(f"invalid example '{name}' was accepted")
    ok(f"{len(invalid)} structurally invalid examples checked")


def check_spec_examples(validator: Draft202012Validator) -> None:
    print("Embedded spec examples")
    count = 0
    for md in sorted((ROOT / "spec").glob("*.md")):
        for block in re.findall(r"```json\n(.*?)```", md.read_text(encoding="utf-8"), re.S):
            try:
                value = json.loads(block)
            except json.JSONDecodeError:
                continue  # illustrative fragment with elisions such as {...}
            if not (isinstance(value, dict) and str(value.get("schema", "")).startswith("utv/")):
                continue  # e.g. a utv-result illustration
            count += 1
            for err in validator.iter_errors(value):
                fail(f"{md.relative_to(ROOT)}: embedded example rejected: {err.message[:200]}")
                break
    ok(f"{count} complete embedded UTV examples checked")


def check_scenarios() -> None:
    print("Conformance scenarios")
    required = {
        "falsehoods.json": {"id", "scenario", "designSafeguard", "specSections", "status"},
        "standards-convergence.json": {"id", "category", "scenario", "expectedDesignOutcome", "sourceStandard", "status"},
    }
    seen: set[str] = set()
    falsehood_ids: set[str] = set()
    for fname, fields in required.items():
        items = load_json(ROOT / "conformance" / "scenarios" / fname)
        for item in items:
            missing = fields - item.keys()
            if missing:
                fail(f"{fname}: {item.get('id', '?')} missing {sorted(missing)}")
            if item["id"] in seen:
                fail(f"{fname}: duplicate scenario id {item['id']}")
            seen.add(item["id"])
            if fname == "falsehoods.json":
                falsehood_ids.add(item["id"])
        ok(f"{fname}: {len(items)} scenarios")

    matrix = (ROOT / "spec" / "FALSEHOODS-MATRIX.md").read_text(encoding="utf-8")
    matrix_ids = set(re.findall(r"^\| ([A-Z]+-\d+) \|", matrix, re.M))
    for missing in sorted(matrix_ids - falsehood_ids):
        fail(f"FALSEHOODS-MATRIX.md row {missing} has no scenario in falsehoods.json")
    for missing in sorted(falsehood_ids - matrix_ids):
        fail(f"falsehoods.json scenario {missing} has no row in FALSEHOODS-MATRIX.md")
    ok(f"falsehoods matrix and scenarios agree ({len(matrix_ids)} IDs)")


def check_traditional_catalog(validator: Draft202012Validator) -> None:
    print("Traditional examples catalog")
    catalog = load_json(ROOT / "examples" / "traditional.json")
    ids: set[str] = set()
    for entry in catalog["entries"]:
        eid = entry["id"]
        if eid in ids:
            fail(f"traditional.json: duplicate id {eid}")
        ids.add(eid)
        if ("value" in entry) == ("text" in entry):
            fail(f"traditional.json: {eid} must have exactly one of `value` or `text`")
            continue
        expected = entry["expect"]["structural"]
        if "text" in entry:
            try:
                value = load_json_text(entry["text"])
            except ValueError:
                if expected == "valid":
                    fail(f"traditional.json: {eid} text does not parse but is expected valid")
                continue
        else:
            value = entry["value"]
        accepted = validator.is_valid(value)
        if accepted != (expected == "valid"):
            fail(f"traditional.json: {eid} schema {'accepts' if accepted else 'rejects'} it, catalog expects {expected}")
    ok(f"{len(catalog['entries'])} catalog entries agree with the schema")


def main() -> int:
    validator = check_schemas()
    check_examples(validator)
    check_spec_examples(validator)
    check_scenarios()
    check_traditional_catalog(validator)
    print()
    if failures:
        print(f"{len(failures)} failure(s)")
        return 1
    print("All structural checks passed.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
