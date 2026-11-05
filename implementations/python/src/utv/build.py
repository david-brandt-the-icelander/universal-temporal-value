"""Builders for the canonical everyday shapes (docs/everyday-api.md).

Builders only assemble UTV JSON values (plain dicts); they don't judge them.
``date(2026, 2, 30)`` builds a well-formed value naming a day that doesn't
exist, and the operations (``fields``, ``add``, ``format_iso``…) report it as
``invalid``. Wrong Python types raise ``TypeError``.
"""

from __future__ import annotations

import copy
from fractions import Fraction
from typing import Any

from ._core import NANOS, SCHEMA, civil_value, instant_value, posix_amount

UNBOUNDED: dict[str, str] = {"state": "unbounded"}
"""An interval endpoint with no bound (``interval(start, UNBOUNDED)``)."""

UNKNOWN: dict[str, str] = {"state": "unknown"}
"""An interval endpoint that exists but isn't known."""


def _int(name: str, x: Any) -> int:
    if isinstance(x, bool) or not isinstance(x, int):
        raise TypeError(f"{name} must be an int, got {type(x).__name__}")
    return x


def date(year: int, month: int, day: int) -> dict[str, Any]:
    return civil_value("date", {"year": str(_int("year", year)), "month": _int("month", month), "day": _int("day", day)})


def year_month(year: int, month: int) -> dict[str, Any]:
    return civil_value("year-month", {"year": str(_int("year", year)), "month": _int("month", month)})


def _time_fields(hour: int, minute: int, second: int | None, nano: int | None) -> dict[str, Any]:
    fields: dict[str, Any] = {"hour": _int("hour", hour), "minute": _int("minute", minute)}
    if second is not None:
        fields["second"] = _int("second", second)
    if nano is not None and _int("nano", nano) != 0:
        fields["nano"] = nano
    return fields


def time(hour: int, minute: int, second: int | None = None, nano: int | None = None) -> dict[str, Any]:
    return civil_value("time", _time_fields(hour, minute, second, nano))


def date_time(
    year: int, month: int, day: int, hour: int, minute: int, second: int | None = None, nano: int | None = None
) -> dict[str, Any]:
    """An unzoned ("floating") local date-time."""
    fields = {"year": str(_int("year", year)), "month": _int("month", month), "day": _int("day", day)}
    fields.update(_time_fields(hour, minute, second, nano))
    return civil_value("datetime", fields)


def _rezone(dt: dict[str, Any], zone: dict[str, Any], intent: str) -> dict[str, Any]:
    out = copy.deepcopy(dt)
    value = out["value"]
    value.pop("offsetAssertion", None)
    value["zone"] = zone
    value["zoneIntent"] = intent
    return out


def in_zone(dt: dict[str, Any], zone_id: str) -> dict[str, Any]:
    """The same wall time, intended in the named IANA zone (follows the zone's rules)."""
    if not isinstance(zone_id, str):
        raise TypeError("zone_id must be a str")
    return _rezone(dt, {"type": "iana", "id": zone_id}, "follow-zone-id")


def with_offset(dt: dict[str, Any], seconds: int) -> dict[str, Any]:
    """The same wall time at a fixed UTC offset, in seconds east of UTC."""
    return _rezone(dt, {"type": "fixed-offset", "seconds": _int("seconds", seconds)}, "fixed-offset")


def instant(epoch_seconds: int, nano: int = 0) -> dict[str, Any]:
    """A POSIX instant: ``epoch_seconds`` plus ``nano`` nanoseconds."""
    return instant_value(Fraction(_int("epoch_seconds", epoch_seconds)) + Fraction(_int("nano", nano), NANOS))


def period(
    years: int = 0, months: int = 0, weeks: int = 0, days: int = 0, hours: int = 0, minutes: int = 0, seconds: int = 0
) -> dict[str, Any]:
    """A calendar amount. Only non-zero components are kept."""
    parts = {"years": years, "months": months, "weeks": weeks, "days": days, "hours": hours, "minutes": minutes, "seconds": seconds}
    components = {k: str(_int(k, v)) for k, v in parts.items() if _int(k, v) != 0}
    return {"schema": SCHEMA, "kind": "amount", "value": {"basis": "calendar", "components": components}}


def posix_seconds(n: int | Fraction) -> dict[str, Any]:
    """An exact amount of POSIX seconds (not SI seconds across a leap second)."""
    if isinstance(n, bool) or not isinstance(n, (int, Fraction)):
        raise TypeError("n must be an int or a fractions.Fraction")
    return posix_amount(Fraction(n))


def _endpoint(point: Any, included: bool) -> dict[str, Any]:
    if isinstance(point, dict) and "state" in point and "kind" not in point:
        return dict(point)
    return {"state": "included" if included else "excluded", "point": copy.deepcopy(point)}


def _domain(point: Any) -> str | None:
    if isinstance(point, dict) and point.get("kind") == "instant":
        return "physical"
    if isinstance(point, dict) and point.get("kind") == "civil":
        return "civil/gregory"
    return None


def interval(
    start: Any, end: Any, start_included: bool = True, end_included: bool = False, *, domain: str | None = None
) -> dict[str, Any]:
    """An interval, half-open ``[start, end)`` by default.

    Either endpoint may be :data:`UNBOUNDED` or :data:`UNKNOWN`. The domain is
    ``civil/gregory`` for civil points and ``physical`` for instants; pass
    ``domain=`` when neither endpoint is a point.
    """
    dom = domain or _domain(start) or _domain(end)
    if dom is None:
        raise ValueError("cannot infer the interval domain; pass domain=")
    return {
        "schema": SCHEMA,
        "kind": "interval",
        "value": {"domain": dom, "start": _endpoint(start, start_included), "end": _endpoint(end, end_included)},
    }
