"""Proleptic Gregorian calendar-field validation for ``civil`` values.

This is a semantic check, kept separate from structural validation. It only
judges the calendar fields (year, month, day); zone ids, offsets and time
fields are not checked here. Anything it cannot judge returns ``unsupported``.
"""

from __future__ import annotations

from .result import Diagnostic, Result, Status
from .validate import parse, validate_structural

_MONTH_DAYS = (31, 28, 31, 30, 31, 30, 31, 31, 30, 31, 30, 31)


def is_leap_year(year: int) -> bool:
    """Proleptic Gregorian leap rule (astronomical year numbering)."""
    return year % 4 == 0 and (year % 100 != 0 or year % 400 == 0)


def days_in_month(year: int | None, month: int) -> int:
    """Days in ``month``; with ``year`` unknown, February has at most 29."""
    if month == 2 and (year is None or is_leap_year(year)):
        return 29
    return _MONTH_DAYS[month - 1]


def _diag(code: str, path: str, message: str) -> Diagnostic:
    return Diagnostic(code=code, severity="error", path=path, message=message)


def validate_gregorian_date(text: str) -> Result:
    """Check the calendar fields of a proleptic-Gregorian ``civil`` value.

    Returns ``invalid`` for structural errors or impossible dates (e.g. Feb 30,
    Feb 29 in a common year), ``unsupported`` for other kinds or calendars,
    otherwise ``valid`` (the date fields name a real Gregorian date).
    """
    structural = validate_structural(text)
    if structural.status is not Status.VALID:
        return structural
    doc = parse(text)
    res = Result(operation="validate", status=Status.VALID)
    if doc["kind"] != "civil":
        res.status = Status.UNSUPPORTED
        res.diagnostics.append(_diag("unsupported-kind", "/kind", f"date validation is not implemented for {doc['kind']!r}"))
        return res
    value = doc["value"]
    cal = value.get("calendar")
    if not cal or cal.get("id") != "gregory" or cal.get("chronology") != "proleptic":
        res.status = Status.UNSUPPORTED
        res.diagnostics.append(_diag("unsupported-calendar", "/value/calendar", "only the proleptic Gregorian calendar is implemented"))
        return res
    fields = value.get("fields", {})
    year = int(fields["year"]) if "year" in fields else None
    month = fields.get("month")
    day = fields.get("day")
    if month is not None and not 1 <= month <= 12:
        res.diagnostics.append(_diag("month-out-of-range", "/value/fields/month", f"month {month} is not in 1..12"))
    elif day is not None:
        limit = 31 if month is None else days_in_month(year, int(month))
        if not 1 <= day <= limit:
            res.diagnostics.append(_diag("day-out-of-range", "/value/fields/day", f"day {day} is not in 1..{limit}"))
    if res.diagnostics:
        res.status = Status.INVALID
    return res

