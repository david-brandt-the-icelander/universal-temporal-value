"""Everyday date/time tasks: Python's datetime, side by side with UTV.

Run from ``implementations/python``::

    PYTHONPATH=src python3 examples/everyday.py

Each section does an ordinary thing first with the standard library
(``datetime``, ``zoneinfo``), then with UTV. Native output can vary by host
(clock, tz database); UTV output can't, except the clock reading in section 1.
The script checks its own UTV outcomes and exits with status 1 if any differ.
"""

from __future__ import annotations

import calendar
import json
import time as clock
from datetime import date, datetime, time, timedelta, timezone
from pathlib import Path
from typing import Any, Optional
from zoneinfo import ZoneInfo, ZoneInfoNotFoundError

import utv
from utv import UNBOUNDED, UNKNOWN, Intent, Result, Status

RULES_PATH = Path(__file__).resolve().parents[3] / "conformance" / "rules" / "demo-zones.json"
mismatches: list[str] = []


# --- printing helpers ---------------------------------------------------------

def native(label: str, value: Any) -> None:
    print(f"  native  {label:<40} {value}")


def iso(value: Any) -> str:
    """format_iso for UTV values, plain text for strings, JSON for other answers."""
    if isinstance(value, str):
        return value
    if isinstance(value, dict) and "schema" in value:
        r = utv.format_iso(value)
        if r.status is Status.UNIQUE:
            return r.value
        if value.get("kind") == "amount":
            s = value["value"].get("seconds")
            if s:
                return f"{s['numerator']} s" if s["denominator"] == "1" else f"{s['numerator']}/{s['denominator']} s"
            return json.dumps(value["value"]["components"])
        return json.dumps(value)
    return json.dumps(value)


def show(label: str, r: Any, expect: Optional[str] = None, *, values: Optional[list[str]] = None) -> Result:
    """Print a UTV outcome, and record a mismatch if it isn't what this example expects.

    A built value (not a result) is shown through format_iso.
    """
    if not isinstance(r, Result):
        r = utv.format_iso(r)
    shown = ", ".join(iso(c) for c in r.candidates)
    why = "; ".join(f"{d.code}: {d.message}" for d in r.diagnostics if r.status is not Status.UNIQUE)
    print(f"  UTV     {label:<40} {r.status.value:<11} {shown}{'  (' + why + ')' if why else ''}")
    if expect is not None and r.status.value != expect:
        mismatches.append(f"{label}: expected {expect}, got {r.status.value}")
    if values is not None and [iso(c) for c in r.candidates] != values:
        mismatches.append(f"{label}: expected {values}, got {[iso(c) for c in r.candidates]}")
    return r


def note(text: str) -> None:
    print(f"  note    {text}")


def zone(key: str) -> Optional[ZoneInfo]:
    """A ZoneInfo from the host's tz database, or None (printed) if it isn't installed."""
    try:
        return ZoneInfo(key)
    except (ZoneInfoNotFoundError, ValueError, OSError):
        native(f"ZoneInfo({key!r})", "not available on this host; native side skipped")
        return None


def section(title: str) -> None:
    print(f"\n== {title} ==")


# --- the sections ---------------------------------------------------------------

def now() -> None:
    section("1. Now")
    reading = datetime.now(timezone.utc)  # the example reads the clock, not the library
    native("datetime.now(timezone.utc)", reading.isoformat())
    show("from_datetime(reading, INSTANT)", utv.from_datetime(reading, Intent.INSTANT), "unique")
    show("instant(*divmod(time_ns(), 10**9))", utv.instant(*divmod(clock.time_ns(), 10**9)), "unique")
    note("utv has no now(): the clock reading is an explicit input, so the dependency is visible (value varies per run)")


def a_date() -> None:
    section("2. A date")
    d = date(2026, 11, 5)
    native("date(2026, 11, 5).isoformat()", d.isoformat())
    native("date.fromisoformat('2026-11-05')", repr(date.fromisoformat("2026-11-05")))
    native("isoweekday(), isocalendar()", f"{d.isoweekday()}, {tuple(d.isocalendar())}")
    show("format_iso(date(2026, 11, 5))", utv.format_iso(utv.date(2026, 11, 5)), "unique")
    parsed = show("parse_iso('2026-11-05')", utv.parse_iso("2026-11-05"), "unique", values=["2026-11-05"])
    show("fields(...)", utv.fields(parsed.value), "unique",
         values=['{"dayOfWeek": 4, "dayOfYear": 309, "isoWeekYear": "2026", "isoWeek": 45, "daysInMonth": 30, "isLeapYear": false}'])


def invalid_dates() -> None:
    section("3. Invalid dates")
    try:
        native("date(2026, 2, 30)", date(2026, 2, 30))
    except ValueError as exc:
        native("date(2026, 2, 30)", f"raises ValueError: {exc}")
    feb30 = utv.date(2026, 2, 30)  # builders don't judge; operations do
    show("fields(date(2026, 2, 30))", utv.fields(feb30), "invalid")
    show("parse_iso('2026-02-30')", utv.parse_iso("2026-02-30"), "invalid")
    note("native refuses at construction with an exception; UTV builds the value and every operation answers 'invalid'")


def add_months(d: date, n: int) -> date:
    """The usual stdlib workaround for 'add n months' (dateutil isn't stdlib): clamp to month end."""
    y, m = divmod(d.month - 1 + n, 12)
    year, month = d.year + y, m + 1
    return d.replace(year=year, month=month, day=min(d.day, calendar.monthrange(year, month)[1]))


def adding() -> None:
    section("4. Adding")
    d = date(2026, 11, 5)
    native("d + timedelta(days=30)", d + timedelta(days=30))
    native("add_months(d, 1)  # workaround", add_months(d, 1))
    try:
        native("date(2026, 1, 31).replace(month=2)", date(2026, 1, 31).replace(month=2))
    except ValueError as exc:
        native("date(2026, 1, 31).replace(month=2)", f"raises ValueError: {exc}")
    native("add_months(date(2026, 1, 31), 1)", add_months(date(2026, 1, 31), 1))
    show("add(d, period(days=30))", utv.add(utv.date(2026, 11, 5), utv.period(days=30)), "unique", values=["2026-12-05"])
    show("add(d, period(months=1))", utv.add(utv.date(2026, 11, 5), utv.period(months=1)), "unique", values=["2026-12-05"])
    jan31 = utv.date(2026, 1, 31)
    show("add(31 Jan, period(months=1))", utv.add(jan31, utv.period(months=1)), "none")
    show("... {'monthEnd': 'constrain'}", utv.add(jan31, utv.period(months=1), {"monthEnd": "constrain"}),
         "unique", values=["2026-02-28"])
    note("stdlib has no month arithmetic: replace() raises, the usual workaround clamps silently; "
         "UTV answers 'none' until you choose 'constrain'")


def days_between() -> None:
    section("5. Days between")
    a, b = date(2026, 11, 5), date(2026, 12, 25)
    native("(b - a).days", (b - a).days)
    native("a < b", a < b)
    show("days_between(a, b)", utv.days_between(utv.date(2026, 11, 5), utv.date(2026, 12, 25)), "unique", values=["50"])
    show("compare(a, b)", utv.compare(utv.date(2026, 11, 5), utv.date(2026, 12, 25)), "unique", values=["before"])
    note("compare() returns before/equal/after, or 'unsupported' when the order depends on rules (e.g. two zones)")


def times() -> None:
    section("6. Times and date-times")
    native("time(9, 30).isoformat()", time(9, 30).isoformat())
    native("datetime(2026,11,5,23,30) + 2h", (datetime(2026, 11, 5, 23, 30) + timedelta(hours=2)).isoformat())
    show("time(9, 30)", utv.time(9, 30), "unique", values=["09:30"])
    show("add(date_time(...23:30), period(hours=2))",
         utv.add(utv.date_time(2026, 11, 5, 23, 30), utv.period(hours=2)), "unique", values=["2026-11-06T01:30"])
    note("native time() always has seconds (09:30:00); UTV keeps 09:30 as written")


def parsing_offsets() -> None:
    section("7. Parsing with offsets")
    parsed: dict[str, Any] = {}
    for text in ("2026-11-05T14:30:00+01:00", "2026-11-05T14:30:00Z", "2026-11-05T14:30:00+00:00"):
        label = f"fromisoformat('...{text[19:]}')"
        try:
            parsed[text[19:]] = datetime.fromisoformat(text)
            native(label, f"{parsed[text[19:]].isoformat()}  tzinfo={parsed[text[19:]].tzinfo!r}")
        except ValueError as exc:  # Python 3.10 doesn't accept "Z"
            native(label, f"raises ValueError: {exc}")
    show("parse_iso('...+01:00')", utv.parse_iso("2026-11-05T14:30:00+01:00"), "unique", values=["2026-11-05T14:30:00+01:00"])
    z = show("parse_iso('...Z')", utv.parse_iso("2026-11-05T14:30:00Z"), "unique", values=["2026-11-05T14:30:00Z"])
    zero = show("parse_iso('...+00:00')", utv.parse_iso("2026-11-05T14:30:00+00:00"), "unique",
                values=["2026-11-05T14:30:00+00:00"])
    print(f"  UTV     {'kinds':<40} Z -> {z.value['kind']}, +00:00 -> {zero.value['kind']} at offset "
          f"{zero.value['value']['zone']['seconds']}")
    if "Z" in parsed and parsed["Z"].tzinfo == parsed["+00:00"].tzinfo:
        note("native gave Z and +00:00 the same tzinfo, so they can't be told apart afterwards")
    note("UTV keeps 'UTC, local offset unknown' (Z: an instant) apart from 'local offset is 0' (+00:00: an offset date-time)")


def time_zones(rules: utv.ZoneRules) -> None:
    section("8. Time zones")
    paris, new_york = zone("Europe/Paris"), zone("America/New_York")
    if paris and new_york:
        meeting = datetime(2027, 6, 1, 9, 0, tzinfo=paris)
        native("datetime(2027,6,1,9,0,tzinfo=Paris)", meeting.isoformat())
        native(".astimezone(New_York)", meeting.astimezone(new_york).isoformat())
    meeting_utv = utv.in_zone(utv.date_time(2027, 6, 1, 9, 0), "Europe/Paris")
    show("in_zone(date_time(...), 'Europe/Paris')", meeting_utv, "unique",
         values=["2027-06-01T09:00[Europe/Paris]"])
    at = show("resolve(meeting, rules)", utv.resolve(meeting_utv, rules), "unique", values=["2027-06-01T07:00:00Z"])
    show("to_zone(instant, 'America/New_York')", utv.to_zone(at.value, "America/New_York", rules), "unique",
         values=["2027-06-01T03:00:00-04:00[America/New_York]"])
    note(f"native uses the host's tz database; UTV uses only the rules passed in ({rules.rule_set['id']} "
         f"{rules.rule_set['version']}), recorded in the result manifest")


def dst_gap(rules: utv.ZoneRules) -> None:
    section("9. DST gap")
    new_york = zone("America/New_York")
    if new_york:
        wall = datetime(2026, 3, 8, 2, 30, tzinfo=new_york)
        back = wall.astimezone(timezone.utc).astimezone(new_york)
        native("datetime(2026,3,8,2,30,tzinfo=NY)", wall.isoformat())
        native(".astimezone(utc).astimezone(NY)", back.isoformat())
        if back.replace(tzinfo=None) != wall.replace(tzinfo=None):
            note("native accepted a wall time that this host's tzdb says never happened, with no error")
    gap = utv.in_zone(utv.date_time(2026, 3, 8, 2, 30), "America/New_York")
    show("resolve(02:30 New York)", utv.resolve(gap, rules), "none")
    shifted = show("... {'gap': 'shift-forward-by-gap'}",
                   utv.resolve(gap, rules, {"gap": "shift-forward-by-gap", "overlap": "reject"}),
                   "unique", values=["2026-03-08T07:30:00Z"])
    show("to_zone(that, 'America/New_York')", utv.to_zone(shifted.value, "America/New_York", rules), "unique",
         values=["2026-03-08T03:30:00-04:00[America/New_York]"])


def dst_overlap(rules: utv.ZoneRules) -> None:
    section("10. DST overlap")
    new_york = zone("America/New_York")
    if new_york:
        wall = datetime(2026, 11, 1, 1, 30, tzinfo=new_york)
        native("datetime(2026,11,1,1,30,tzinfo=NY)", f"{wall.isoformat()}  (fold={wall.fold})")
        native("same with fold=1", wall.replace(fold=1).isoformat())
        if wall.utcoffset() != wall.replace(fold=1).utcoffset():
            note(f"01:30 is ambiguous here; fold defaults to 0, which gave {wall.isoformat()[-6:]} with no warning")
    overlap = utv.in_zone(utv.date_time(2026, 11, 1, 1, 30), "America/New_York")
    show("resolve(01:30 New York)", utv.resolve(overlap, rules), "multiple",
         values=["2026-11-01T05:30:00Z", "2026-11-01T06:30:00Z"])
    show("... {'overlap': 'earliest'}", utv.resolve(overlap, rules, {"gap": "reject", "overlap": "earliest"}),
         "unique", values=["2026-11-01T05:30:00Z"])


def timestamps() -> None:
    section("11. Timestamps")
    stamp = datetime.fromtimestamp(1793885400.123456789, tz=timezone.utc)
    native("fromtimestamp(1793885400.123456789)", stamp.isoformat())
    a = datetime.fromtimestamp(1793885400, tz=timezone.utc)
    native("(stamp - a).total_seconds()", (stamp - a).total_seconds())
    precise = utv.instant(1793885400, 123_456_789)
    show("instant(1793885400, 123_456_789)", precise, "unique",
         values=["2026-11-05T13:30:00.123456789Z"])
    show("posix_seconds_between(a, precise)", utv.posix_seconds_between(utv.instant(1793885400), precise), "unique",
         values=["123456789/1000000000 s"])
    note("a float timestamp keeps microseconds at best; UTV ticks are integers and fractions are exact rationals")


def durations(rules: utv.ZoneRules) -> None:
    section("12. Durations vs periods")
    native("timedelta(days=1) == timedelta(hours=24)", timedelta(days=1) == timedelta(hours=24))
    new_york = zone("America/New_York")
    if new_york:
        start = datetime(2026, 10, 31, 12, 0, tzinfo=new_york)
        end = start + timedelta(days=1)
        native("NY 10-31 12:00 + timedelta(days=1)", end.isoformat())
        elapsed = end.astimezone(timezone.utc) - start.astimezone(timezone.utc)
        native("elapsed (via UTC)", elapsed)
        note(f"same-tzinfo datetime + timedelta is wall-clock arithmetic: 'one day' was {elapsed / timedelta(hours=1):g} hours here")
    noon = utv.in_zone(utv.date_time(2026, 10, 31, 12, 0), "America/New_York")
    next_day = show("add(noon NY, period(days=1))", utv.add(noon, utv.period(days=1)), "unique",
                    values=["2026-11-01T12:00[America/New_York]"])
    a, b = utv.resolve(noon, rules).value, utv.resolve(next_day.value, rules).value
    show("posix_seconds_between(resolved)", utv.posix_seconds_between(a, b), "unique", values=["90000 s"])
    show("add(instant, posix_seconds(86400))", utv.add(a, utv.posix_seconds(86400)), "unique",
         values=["2026-11-01T16:00:00Z"])
    si_day = {"schema": "utv/1.0", "kind": "amount",
              "value": {"basis": "elapsed", "seconds": {"numerator": "86400", "denominator": "1"}}}
    show("add(instant, 86400 SI seconds)", utv.add(a, si_day), "unsupported")
    note("period(days=1) is a calendar step, posix_seconds(86400) an exact count; SI seconds need a leap-second table")


def ranges() -> None:
    section("13. Ranges")
    start, end = date(2026, 11, 1), date(2026, 12, 1)
    native("start <= date(2026,11,15) < end", start <= date(2026, 11, 15) < end)
    native("start <= date(2026,12,1) < end", start <= date(2026, 12, 1) < end)
    note("the stdlib has no date-range type; you write the comparisons yourself")
    november = utv.interval(utv.date(2026, 11, 1), utv.date(2026, 12, 1))
    december = utv.interval(utv.date(2026, 12, 1), utv.date(2027, 1, 1))
    show("contains(november, 15 Nov)", utv.contains(november, utv.date(2026, 11, 15)), "unique", values=["definitely"])
    show("contains(november, 1 Dec)", utv.contains(november, utv.date(2026, 12, 1)), "unique", values=["impossible"])
    show("overlaps(november, december)", utv.overlaps(november, december), "unique", values=["impossible"])
    open_ended = utv.interval(utv.date(2026, 11, 1), UNBOUNDED)
    show("contains([1 Nov, ...), 1 Jan 3000)", utv.contains(open_ended, utv.date(3000, 1, 1)), "unique",
         values=["definitely"])
    unknown_end = utv.interval(utv.date(2026, 11, 1), UNKNOWN)
    show("contains([1 Nov, ?), 1 Jun 2027)", utv.contains(unknown_end, utv.date(2027, 6, 1)), "unique",
         values=["possibly"])


def round_trip() -> None:
    section("14. Round trip")
    original = datetime(2026, 11, 5, 14, 30, tzinfo=timezone(timedelta(hours=1)))
    as_utv = show("from_datetime(+01:00, OFFSET)", utv.from_datetime(original, Intent.OFFSET), "unique",
                  values=["2026-11-05T14:30:00+01:00"])
    back = utv.to_datetime(as_utv.value).value
    native("to_datetime(...)", f"{back.isoformat()}  equal to original: {back == original}")
    paris = zone("Europe/Paris")
    if paris:
        zoned = show("from_datetime(Paris ZoneInfo)", utv.from_datetime(datetime(2027, 6, 1, 9, 0, tzinfo=paris)), "unique")
        native("to_datetime(zoned, tz=Paris)", utv.to_datetime(zoned.value, tz=paris).value.isoformat())
    show("to_datetime(zoned) without tz", utv.to_datetime(utv.parse_iso("2027-06-01T09:00+02:00[Europe/Paris]").value),
         "unsupported")
    naive = datetime(2026, 11, 5, 14, 30)  # what datetime.now() returns: host-local wall time, no zone
    show("from_datetime(naive, INSTANT)", utv.from_datetime(naive, Intent.INSTANT), "unsupported")
    show("from_datetime(naive)", utv.from_datetime(naive), "unique", values=["2026-11-05T14:30:00"])
    note("a naive datetime is only ever a floating wall time; UTV never reads it 'as local'")


def main() -> int:
    mismatches.clear()
    rules = utv.load_rules(RULES_PATH)
    now()
    a_date()
    invalid_dates()
    adding()
    days_between()
    times()
    parsing_offsets()
    time_zones(rules)
    dst_gap(rules)
    dst_overlap(rules)
    timestamps()
    durations(rules)
    ranges()
    round_trip()
    print()
    if mismatches:
        for m in mismatches:
            print(f"MISMATCH: {m}")
        return 1
    print("Every UTV outcome as expected.")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
