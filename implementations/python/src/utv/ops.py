"""Calendar and timeline operations: ``fields``, ``add``, ``compare``,
``days_between``, ``posix_seconds_between``, ``contains``, ``overlaps``
(docs/everyday-api.md)."""

from __future__ import annotations

import copy
from typing import Any, Optional

from ._core import (
    Civil,
    Outcome,
    civil,
    civil_from_days,
    civil_value,
    day_of_week,
    days_from_civil,
    instant_seconds,
    instant_value,
    integer_string,
    invalid,
    is_posix,
    operation,
    parse_rational,
    posix_amount,
    structural,
    unique,
    unsupported,
)
from .gregorian import days_in_month, is_leap_year
from .result import Diagnostic, Result, Status

# --- fields ----------------------------------------------------------------


def _iso_weeks_in_year(year: int) -> int:
    jan1 = day_of_week(days_from_civil(year, 1, 1))
    return 53 if jan1 == 4 or (jan1 == 3 and is_leap_year(year)) else 52


@operation("fields")
def fields(value: Any) -> Result:
    """Derived fields of a Gregorian date or date-time: weekday, ISO week, …"""
    c = civil(value, forms=("date", "datetime"))
    y, m, d = c.year, c.month, c.day
    days = c.day_number
    dow = day_of_week(days)
    doy = days - days_from_civil(y, 1, 1) + 1  # type: ignore[arg-type]
    week_year, week = y, (doy - dow + 10) // 7
    if week < 1:
        week_year = y - 1  # type: ignore[operator]
        week = _iso_weeks_in_year(week_year)
    elif week > _iso_weeks_in_year(y):  # type: ignore[arg-type]
        week_year, week = y + 1, 1  # type: ignore[operator]
    return unique({
        "dayOfWeek": dow,
        "dayOfYear": doy,
        "isoWeekYear": str(week_year),
        "isoWeek": week,
        "daysInMonth": days_in_month(y, m),  # type: ignore[arg-type]
        "isLeapYear": is_leap_year(y),  # type: ignore[arg-type]
    })


# --- add -------------------------------------------------------------------

_COMPONENTS = ("years", "months", "weeks", "days", "hours", "minutes", "seconds")
_ALLOWED = {
    "date": {"years", "months", "weeks", "days"},
    "year-month": {"years", "months"},
    "datetime": set(_COMPONENTS),
}


def _components(amount: dict[str, Any]) -> dict[str, int]:
    comps = amount["value"].get("components", {})
    out = {}
    for name, text in comps.items():
        path = f"/1/value/components/{name}"
        if name not in _COMPONENTS:
            raise unsupported("unsupported-operand", path, f"component {name!r} is not handled by add")
        if isinstance(text, str) and "." in text:
            raise unsupported("unsupported-operand", path, "fractional components are not handled by add")
        out[name] = integer_string(text, path)
    return out


def _month_end(policy: Optional[dict[str, Any]]) -> str:
    if policy is None:
        return "reject"
    choice = policy.get("monthEnd", "reject") if isinstance(policy, dict) else None
    if choice not in ("reject", "constrain"):
        raise unsupported("unsupported-policy", "/2/monthEnd", f"monthEnd policy {choice!r} is not implemented")
    return str(choice)


@operation("add")
def add(value: Any, amount: Any, policy: Optional[dict[str, Any]] = None) -> Result:
    """Add an amount to a civil value or an instant.

    ``policy`` is ``{"monthEnd": "reject" | "constrain"}``; the default,
    ``reject``, answers ``none`` when the day doesn't exist in the target month.
    """
    structural(value, "/0")
    structural(amount, "/1")
    if amount["kind"] != "amount":
        raise invalid("kind-mismatch", "/1/kind", "the second argument must be an amount")
    basis = amount["value"]["basis"]
    if value["kind"] == "instant":
        t = instant_seconds(value, "/0")
        if basis == "elapsed":
            raise unsupported("needs-leap-seconds", "/1/value/basis",
                              "SI seconds on a POSIX instant need a leap-second table")
        if basis == "calendar":
            raise invalid("kind-mismatch", "/1/value/basis",
                          "an instant has no calendar; convert it to a civil value first")
        if basis != "clock-ticks" or not is_posix(amount["value"].get("rules")) or "seconds" not in amount["value"]:
            raise unsupported("unsupported-operand", "/1/value", "an instant accepts only a POSIX-seconds amount")
        return unique(instant_value(t + parse_rational(amount["value"]["seconds"], "/1/value/seconds")))
    if value["kind"] != "civil":
        raise unsupported("unsupported-operand", "/0/kind", f"add is not implemented for {value['kind']!r}")
    c = civil(value, "/0")
    if c.form == "time":
        raise unsupported("unsupported-operand", "/0/value/form", "adding to a time of day wraps past midnight and loses information")
    if c.form not in _ALLOWED:
        raise unsupported("unsupported-operand", "/0/value/form", f"add is not implemented for form {c.form!r}")
    if basis != "calendar":
        raise invalid("kind-mismatch", "/1/value/basis", "a civil value takes a calendar amount")
    comps = _components(amount)
    for name in comps:
        if name not in _ALLOWED[c.form]:
            raise invalid("kind-mismatch", f"/1/value/components/{name}", f"a {c.form} has no {name}")
    rule = _month_end(policy)

    # 1-2: years and months, then the month-end rule.
    months = c.year * 12 + (c.month - 1) + 12 * comps.get("years", 0) + comps.get("months", 0)  # type: ignore[operator]
    year, month = divmod(months, 12)
    month += 1
    fields: dict[str, Any] = dict(value["value"]["fields"], year=str(year), month=month)
    if c.form == "year-month":
        return unique(civil_value("year-month", fields))
    day = c.day
    limit = days_in_month(year, month)
    if day > limit:  # type: ignore[operator]
        if rule == "reject":
            raise Outcome(Status.NONE, "month-end", "/0/value/fields/day",
                          f"{year}-{month:02d} has no day {day}", "info")
        day = limit
    # 3-4: weeks and days, then wall-clock hours/minutes/seconds carrying into days.
    days = days_from_civil(year, month, day) + 7 * comps.get("weeks", 0) + comps.get("days", 0)  # type: ignore[arg-type]
    diagnostics: list[Diagnostic] = []
    if c.form == "datetime":
        clock = c.second_of_day + 3600 * comps.get("hours", 0) + 60 * comps.get("minutes", 0) + comps.get("seconds", 0)
        carry, sod = divmod(clock, 86400)
        days += carry
        fields.update(hour=sod // 3600, minute=sod // 60 % 60)
        if c.second is not None or comps.get("seconds", 0):
            fields["second"] = sod % 60
    y, mo, d = civil_from_days(days)
    fields.update(year=str(y), month=mo, day=d)
    out = copy.deepcopy(value)
    out["value"]["fields"] = fields
    if out["value"].pop("offsetAssertion", None) is not None:
        diagnostics.append(Diagnostic("offset-assertion-dropped", "info", "/0/value/offsetAssertion",
                                      "the asserted offset described the old wall time and was dropped"))
    return Result(operation="add", status=Status.UNIQUE, candidates=[out], diagnostics=diagnostics)


# --- compare ---------------------------------------------------------------

def _relation(a: Any, b: Any) -> str:
    return "before" if a < b else "after" if a > b else "equal"


@operation("compare")
def compare(a: Any, b: Any) -> Result:
    """``"before"``, ``"equal"`` or ``"after"``: where ``a`` is relative to ``b``."""
    structural(a, "/0")
    structural(b, "/1")
    if a["kind"] == "instant" and b["kind"] == "instant":
        return unique(_relation(instant_seconds(a, "/0"), instant_seconds(b, "/1")))
    if a["kind"] != "civil" or b["kind"] != "civil":
        raise unsupported("unsupported-operand", "", f"can't compare {a['kind']!r} with {b['kind']!r}")
    ca, cb = civil(a, "/0"), civil(b, "/1")
    if "iana" in (ca.zone, cb.zone):
        raise unsupported("needs-zone-rules", "", "ordering depends on zone rules: resolve both values to instants first")
    if ca.form != cb.form:
        raise unsupported("unsupported-operand", "", f"can't compare a {ca.form} with a {cb.form}")
    if ca.zone == cb.zone == "floating":
        return unique(_relation(ca.order_key(), cb.order_key()))
    if ca.zone == cb.zone == "fixed-offset" and ca.form == "datetime":
        return unique(_relation(ca.wall_seconds() - ca.offset, cb.wall_seconds() - cb.offset))  # type: ignore[operator]
    raise unsupported("unsupported-operand", "", "can't order a floating value against one with an offset")


# --- differences -----------------------------------------------------------

def _same_zone(dates: list[Civil]) -> None:
    """Dates relate only when all are unzoned or all share one zone (contract, Clarifications 4)."""
    if len({c.zone_key for c in dates}) > 1:
        raise unsupported("different-zones", "", "dates in different zones (or zoned and unzoned) can't be related")


@operation("daysBetween")
def days_between(a: Any, b: Any) -> Result:
    """Whole days from date ``a`` to date ``b`` (negative if ``b`` is earlier)."""
    ca, cb = civil(a, "/0", forms=("date",)), civil(b, "/1", forms=("date",))
    _same_zone([ca, cb])
    return unique(cb.day_number - ca.day_number)


@operation("posixSecondsBetween")
def posix_seconds_between(a: Any, b: Any) -> Result:
    """``b − a`` as an exact POSIX-seconds amount."""
    return unique(posix_amount(instant_seconds(b, "/1") - instant_seconds(a, "/0")))


# --- intervals -------------------------------------------------------------
# Three-valued logic: True, False, or None for unknown.

def _and(*conditions: Optional[bool]) -> str:
    if any(c is False for c in conditions):
        return "impossible"
    if all(c is True for c in conditions):
        return "definitely"
    return "possibly"


class _Bound:
    """One endpoint: ``state`` plus a comparable ``point`` (day number or exact seconds)."""

    def __init__(self, state: str, point: Any = None, date: Optional[Civil] = None) -> None:
        self.state, self.point, self.date = state, point, date


def _dates(*bounds: _Bound) -> None:
    _same_zone([b.date for b in bounds if b.date is not None])


def _point(value: Any, domain: str, path: str) -> _Bound:
    """An included bound at ``value``: exact seconds for instants, a day number for dates."""
    structural(value, path)
    if domain == "physical":
        if value["kind"] != "instant":
            raise unsupported("unsupported-operand", path, "an instant interval takes instant points")
        return _Bound("included", instant_seconds(value, path))
    if value["kind"] != "civil":
        raise unsupported("unsupported-operand", path, "a civil-date interval takes date points")
    c: Civil = civil(value, path)
    if c.form != "date":
        raise unsupported("unsupported-operand", path + "/value/form", "only civil-date intervals are implemented")
    return _Bound("included", c.day_number, c)


def _interval(value: Any, path: str) -> tuple[str, _Bound, _Bound]:
    structural(value, path)
    if value["kind"] != "interval":
        raise invalid("kind-mismatch", path + "/kind", "expected an interval")
    v = value["value"]
    domain = v["domain"]
    if domain not in ("physical", "civil/gregory"):
        raise unsupported("unsupported-operand", path + "/value/domain", f"interval domain {domain!r} is not implemented")
    if "start" not in v or "end" not in v:
        raise unsupported("unsupported-operand", path + "/value", "only start/end intervals are implemented")
    bounds = []
    for side in ("start", "end"):
        e = v[side]
        if "point" in e:
            at = _point(e["point"], domain, f"{path}/value/{side}/point")
            bounds.append(_Bound(e["state"], at.point, at.date))
        else:
            bounds.append(_Bound(e["state"]))
    return domain, bounds[0], bounds[1]


def _before(lo: _Bound, hi: _Bound, strict: bool) -> Optional[bool]:
    """Is ``lo`` before ``hi``? (``<`` if strict else ``≤``; unknown → None)."""
    if lo.state == "unbounded" or hi.state == "unbounded":
        return True
    if lo.state == "unknown" or hi.state == "unknown":
        return None
    return bool(lo.point < hi.point if strict else lo.point <= hi.point)


@operation("contains")
def contains(interval: Any, point: Any) -> Result:
    """``"definitely"``, ``"possibly"`` or ``"impossible"``."""
    domain, start, end = _interval(interval, "/0")
    p = _point(point, domain, "/1")
    _dates(start, end, p)
    return unique(_and(_before(start, p, start.state == "excluded"), _before(p, end, end.state == "excluded")))


def _half_open(start: _Bound, end: _Bound) -> tuple[_Bound, _Bound]:
    """Dates are discrete: rewrite as [included start, excluded end)."""
    if start.state == "excluded":
        start = _Bound("included", start.point + 1, start.date)
    if end.state == "included":
        end = _Bound("excluded", end.point + 1, end.date)
    return start, end


@operation("overlaps")
def overlaps(a: Any, b: Any) -> Result:
    """Whether intervals ``a`` and ``b`` share a point: three-valued."""
    da, sa, ea = _interval(a, "/0")
    db, sb, eb = _interval(b, "/1")
    if da != db:
        raise unsupported("unsupported-operand", "", "can't relate a civil-date interval to an instant interval")
    _dates(sa, ea, sb, eb)
    if da == "civil/gregory":
        (sa, ea), (sb, eb) = _half_open(sa, ea), _half_open(sb, eb)

    def lt(s: _Bound, e: _Bound) -> Optional[bool]:
        return _before(s, e, not (s.state == "included" and e.state == "included"))

    # Each start before the other's end, and neither interval empty.
    return unique(_and(lt(sa, eb), lt(sb, ea), lt(sa, ea), lt(sb, eb)))


__all__ = ["add", "compare", "contains", "days_between", "fields", "overlaps", "posix_seconds_between"]
