"""A tour of traditional date/time tasks in UTV.

Run from ``implementations/python``::

    PYTHONPATH=src python3 examples/traditional.py

The first part builds two dates by hand. The second walks the shared catalog
``examples/traditional.json`` and exits with status 1 if any outcome differs
from what the catalog expects.
"""

from __future__ import annotations

import json
import sys
from pathlib import Path
from typing import Any, Callable

import utv
from utv import Result, Status
from utv.gregorian import validate_gregorian_date

CATALOG = Path(__file__).resolve().parent / ".." / ".." / ".." / "examples" / "traditional.json"

# The checks named in each entry's `expect` object, in print order.
CHECKS: dict[str, Callable[[str], Result]] = {
    "structural": utv.validate_structural,
    "gregorian": validate_gregorian_date,
}


# --- Part 1: by hand ---------------------------------------------------------

def gregorian_date(year: int, month: int, day: int) -> str:
    """A UTV date as JSON text. The year is a string because UTV years have no size limit."""
    return json.dumps({
        "schema": "utv/1.0",
        "kind": "civil",
        "value": {
            "form": "date",
            "fields": {"year": str(year), "month": month, "day": day},
            "calendar": {"id": "gregory", "chronology": "proleptic"},
        },
    })


def describe(text: str) -> str:
    """Say what a caller learns from the two checks."""
    structural = utv.validate_structural(text)
    gregorian = validate_gregorian_date(text)
    match structural.status, gregorian.status:
        case Status.VALID, Status.VALID:
            return "well-formed, and the date exists"
        case Status.VALID, Status.INVALID:
            why = "; ".join(f"{d.code} at {d.path}: {d.message}" for d in gregorian.diagnostics)
            return f"well-formed, but no such date ({why})"
        case Status.VALID, Status.UNSUPPORTED:
            return "well-formed, but this library can't judge the date"
        case _:
            why = "; ".join(f"{d.code} at {d.path}" for d in structural.diagnostics)
            return f"rejected: {structural.status.value} ({why})"


def by_hand() -> None:
    print("== By hand ==")
    for label, date in [("5 November 2026", (2026, 11, 5)), ("30 February 2026", (2026, 2, 30))]:
        print(f"\n{label}\n  {describe(gregorian_date(*date))}")


# --- Part 2: the catalog -----------------------------------------------------

def load_catalog(path: Path = CATALOG) -> list[dict[str, Any]]:
    with open(path, encoding="utf-8") as fh:
        return json.load(fh)["entries"]


def entry_text(entry: dict[str, Any]) -> str:
    """The JSON text to validate: raw `text` if given (e.g. duplicate keys), else the encoded `value`."""
    return entry["text"] if "text" in entry else json.dumps(entry["value"])


def check_entry(entry: dict[str, Any]) -> tuple[dict[str, str], list[tuple[str, str, str]]]:
    """Run the checks the entry expects. Returns (actual statuses, [(check, expected, got)] mismatches)."""
    text = entry_text(entry)
    actual: dict[str, str] = {}
    mismatches: list[tuple[str, str, str]] = []
    for check, run in CHECKS.items():
        if check not in entry["expect"]:
            continue
        actual[check] = run(text).status.value
        if actual[check] != entry["expect"][check]:
            mismatches.append((check, entry["expect"][check], actual[check]))
    return actual, mismatches


def label(name: str, value: str) -> str:
    return f"  {name + ':':<12}{value}"


def tour(entries: list[dict[str, Any]], out: Callable[[str], None] = print) -> int:
    """Print the tour of ``entries`` and return the number of mismatches."""
    section = None
    total = 0
    for entry in entries:
        if entry["section"] != section:
            section = entry["section"]
            out(f"\n== {section} ==")
        actual, mismatches = check_entry(entry)
        out(f"\n{entry['title']}")
        out(label("usual", entry["usual"]))
        for check, status in actual.items():
            out(label(check, status))
        out(label("why", entry["point"]))
        if "later" in entry:
            out(label("later", entry["later"]))
        for check, expected, got in mismatches:
            out(f"  MISMATCH: {check} expected {expected}, got {got}")
        total += len(mismatches)
    return total


def main() -> int:
    by_hand()
    entries = load_catalog()
    mismatches = tour(entries)
    print()
    if mismatches:
        print(f"{mismatches} mismatches.")
        return 1
    print(f"{len(entries)} examples, every outcome as expected.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
