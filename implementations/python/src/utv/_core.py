"""Shared internals for the everyday API: constants, day numbers, exact
rationals and the checks every operation runs on its inputs.

Nothing here reads the clock, the host time zone or the locale.
"""

from __future__ import annotations

import functools
import math
import re
from dataclasses import dataclass
from fractions import Fraction
from typing import Any, Callable, Sequence

from .gregorian import days_in_month
from .result import Diagnostic, Result, Status
from .validate import validate_value

SCHEMA = "utv/1.0"
CAL = {"id": "gregory", "chronology": "proleptic"}
POSIX = {"system": "utv.timescale", "id": "unix-posix", "version": "1"}
NANOS = 1_000_000_000
MAX_OFFSET = 18 * 3600

# An IANA-style zone id: one or more slash-separated segments.
ZONE_ID = re.compile(r"[A-Za-z_][A-Za-z0-9_.+-]*(?:/[A-Za-z0-9_.+-]+)*")
_INTEGER = re.compile(r"(0|-?[1-9][0-9]*)")


class Outcome(Exception):
    """Raised inside an operation to stop with a non-``unique`` status."""

    def __init__(self, status: Status, code: str, path: str, message: str, severity: str = "error",
                 also: Sequence[Diagnostic] = ()) -> None:
        super().__init__(message)
        self.status = status
        self.diagnostic = Diagnostic(code=code, severity=severity, path=path, message=message)
        self.diagnostics = [self.diagnostic, *also]


def invalid(code: str, path: str, message: str) -> Outcome:
    return Outcome(Status.INVALID, code, path, message)


def unsupported(code: str, path: str, message: str) -> Outcome:
    return Outcome(Status.UNSUPPORTED, code, path, message)


def operation(name: str) -> Callable[[Callable[..., Result]], Callable[..., Result]]:
    """Turn an :class:`Outcome` raised by the wrapped function into a result."""

    def wrap(fn: Callable[..., Result]) -> Callable[..., Result]:
        @functools.wraps(fn)
        def run(*args: Any, **kwargs: Any) -> Result:
            try:
                res = fn(*args, **kwargs)
            except Outcome as out:
                return Result(operation=name, status=out.status, diagnostics=list(out.diagnostics))
            res.operation = name
            return res

        return run

    return wrap


def unique(candidate: Any, **extra: Any) -> Result:
    return Result(operation="", status=Status.UNIQUE, candidates=[candidate], **extra)


# --- calendar arithmetic (proleptic Gregorian, any year) -----------------

def days_from_civil(year: int, month: int, day: int) -> int:
    """Days since 1970-01-01 (H. Hinnant's algorithm; exact for any year)."""
    y = year - (1 if month <= 2 else 0)
    era = y // 400
    yoe = y - era * 400
    doy = (153 * ((month + 9) % 12) + 2) // 5 + day - 1
    doe = yoe * 365 + yoe // 4 - yoe // 100 + doy
    return era * 146097 + doe - 719468


def civil_from_days(days: int) -> tuple[int, int, int]:
    z = days + 719468
    era = z // 146097
    doe = z - era * 146097
    yoe = (doe - doe // 1460 + doe // 36524 - doe // 146096) // 365
    doy = doe - (365 * yoe + yoe // 4 - yoe // 100)
    mp = (5 * doy + 2) // 153
    day = doy - (153 * mp + 2) // 5 + 1
    month = mp + 3 if mp < 10 else mp - 9
    return yoe + era * 400 + (1 if month <= 2 else 0), month, day


def day_of_week(days: int) -> int:
    """ISO day of week for a day number: Monday = 1 … Sunday = 7."""
    return (days + 3) % 7 + 1



# --- exact numbers ---------------------------------------------------------

def rational(f: Fraction) -> dict[str, str]:
    return {"numerator": str(f.numerator), "denominator": str(f.denominator)}


def parse_rational(r: dict[str, Any], path: str) -> Fraction:
    try:
        return Fraction(int(r["numerator"]), int(r["denominator"]))
    except (KeyError, TypeError, ValueError, ZeroDivisionError):
        raise invalid("malformed-value", path, "expected {numerator, denominator} decimal strings") from None


def integer_string(text: Any, path: str) -> int:
    if not isinstance(text, str) or not _INTEGER.fullmatch(text):
        raise invalid("malformed-value", path, f"{text!r} is not a decimal integer string")
    try:
        return int(text)
    except ValueError:  # beyond Python's int/str conversion limit
        raise Outcome(Status.RESOURCE_LIMIT, "implementation-limit", path, "integer string too long to convert") from None


def instant_value(t: Fraction) -> dict[str, Any]:
    ticks = math.floor(t)
    value: dict[str, Any] = {"system": dict(POSIX), "ticks": str(ticks)}
    if t != ticks:
        value["subTick"] = rational(t - ticks)
    return {"schema": SCHEMA, "kind": "instant", "value": value}


def posix_amount(seconds: Fraction) -> dict[str, Any]:
    return {
        "schema": SCHEMA,
        "kind": "amount",
        "value": {"basis": "clock-ticks", "rules": dict(POSIX), "seconds": rational(seconds)},
    }


# --- input checks ------------------------------------------------------------

def structural(value: Any, path: str) -> dict[str, Any]:
    """Structurally validate a UTV value; stop with its status if it fails."""
    res = validate_value(value)
    if res.status is not Status.VALID:
        found = [Diagnostic(d.code, d.severity, path + d.path, d.message) for d in res.diagnostics]
        if res.status is Status.INVALID:  # the shared code, with the 0.1 structural codes alongside
            d = found[0] if found else Diagnostic("malformed-value", "error", path, "not a UTV value")
            raise Outcome(Status.INVALID, "malformed-value", d.path, d.message, also=found)
        d = found[0] if found else Diagnostic("unsupported-operand", "error", path, "not a supported UTV value")
        raise Outcome(res.status, d.code, d.path, d.message, also=found[1:])
    checked: dict[str, Any] = value
    return checked


def is_posix(system: Any) -> bool:
    return (
        isinstance(system, dict)
        and system.get("system") == POSIX["system"]
        and system.get("id") == POSIX["id"]
        and system.get("version") == POSIX["version"]
    )


def instant_seconds(value: Any, path: str = "") -> Fraction:
    """Exact POSIX seconds of an instant value."""
    structural(value, path)
    if value["kind"] != "instant":
        raise unsupported("unsupported-operand", path + "/kind", f"expected an instant, got {value['kind']!r}")
    v = value["value"]
    if not is_posix(v["system"]):
        raise unsupported("unsupported-operand", path + "/value/system", "only the unix-posix timescale is implemented")
    if "ticks" not in v:
        raise unsupported("unsupported-operand", path + "/value", "only tick-based instants are implemented")
    t = Fraction(integer_string(v["ticks"], path + "/value/ticks"))
    if "subTick" in v:
        t += parse_rational(v["subTick"], path + "/value/subTick")
    return t


_FORM_FIELDS = {
    "date": ({"year", "month", "day"}, set()),
    "year-month": ({"year", "month"}, set()),
    "time": ({"hour", "minute"}, {"second", "nano"}),
    "datetime": ({"year", "month", "day", "hour", "minute"}, {"second", "nano"}),
}


@dataclass(frozen=True)
class Civil:
    """A checked Gregorian civil value."""

    form: str
    year: int | None
    month: int | None
    day: int | None
    hour: int | None
    minute: int | None
    second: int | None  # None when absent
    nano: int | None  # None when absent
    zone: str  # "floating", "fixed-offset" or "iana"
    offset: int | None  # fixed-offset seconds
    zone_id: str | None  # IANA id
    raw: dict[str, Any]

    @property
    def zone_key(self) -> tuple[Any, ...]:
        """What the zone means, for "same zone" checks: floating, a fixed offset, or an IANA id."""
        return (self.zone, self.offset, self.zone_id)

    @property
    def day_number(self) -> int:
        return days_from_civil(self.year, self.month, self.day)  # type: ignore[arg-type]

    @property
    def second_of_day(self) -> int:
        return (self.hour or 0) * 3600 + (self.minute or 0) * 60 + (self.second or 0)

    def wall_seconds(self) -> Fraction:
        """The wall time read as if it were UTC (spec: *L*), exact."""
        whole = self.day_number * 86400 + self.second_of_day if self.year is not None else self.second_of_day
        return Fraction(whole) + Fraction(self.nano or 0, NANOS)

    def order_key(self) -> tuple[int, ...]:
        return (
            self.year or 0, self.month or 0, self.day or 0,
            self.hour or 0, self.minute or 0, self.second or 0, self.nano or 0,
        )


def civil(value: Any, path: str = "", forms: tuple[str, ...] | None = None) -> Civil:
    """Check a civil Gregorian value: structure, calendar, form and field ranges.

    Field ranges are checked before the unsupported cases (second 60, 24:00),
    so a value that is both impossible and unusual is reported ``invalid``.
    """
    structural(value, path)
    if value["kind"] != "civil":
        raise unsupported("unsupported-operand", path + "/kind", f"expected a civil value, got {value['kind']!r}")
    v = value["value"]
    form = v["form"]
    cal = v["calendar"]
    if cal.get("id") != CAL["id"] or cal.get("chronology") != CAL["chronology"]:
        raise unsupported("unsupported-calendar", path + "/value/calendar", "only the proleptic Gregorian calendar is implemented")
    if form not in _FORM_FIELDS or (forms is not None and form not in forms):
        allowed = forms if forms is not None else tuple(_FORM_FIELDS)
        raise unsupported("unsupported-operand", path + "/value/form", f"form {form!r} is not handled here (expected {', '.join(allowed)})")
    required, optional = _FORM_FIELDS[form]
    f = v["fields"]
    fp = path + "/value/fields"
    for name in required:
        if name not in f:
            raise invalid("malformed-value", f"{fp}/{name}", f"a {form} needs {name!r}")
    for name in f:
        if name not in required and name not in optional:
            raise invalid("malformed-value", f"{fp}/{name}", f"a {form} has no {name!r} field")
    ints = {k: int(f[k]) for k in f if k != "year"}
    year = integer_string(f["year"], fp + "/year") if "year" in f else None
    month, day = ints.get("month"), ints.get("day")
    hour, minute, second, nano = ints.get("hour"), ints.get("minute"), ints.get("second"), ints.get("nano")
    if month is not None and not 1 <= month <= 12:
        raise invalid("month-out-of-range", fp + "/month", f"month {month} is not in 1..12")
    if day is not None and not 1 <= day <= days_in_month(year, month):  # type: ignore[arg-type]
        raise invalid("day-out-of-range", fp + "/day", f"day {day} is not in 1..{days_in_month(year, month)}")  # type: ignore[arg-type]
    if hour is not None and not 0 <= hour <= 24:
        raise invalid("field-out-of-range", fp + "/hour", f"hour {hour} is not in 0..23")
    if minute is not None and not 0 <= minute <= 59:
        raise invalid("field-out-of-range", fp + "/minute", f"minute {minute} is not in 0..59")
    if second is not None and not 0 <= second <= 60:
        raise invalid("field-out-of-range", fp + "/second", f"second {second} is not in 0..59")
    if nano is not None and not 0 <= nano < NANOS:
        raise invalid("field-out-of-range", fp + "/nano", f"nano {nano} is not in 0..999999999")
    if hour == 24:
        if minute or second or nano:
            raise invalid("field-out-of-range", fp + "/hour", "hour 24 is only meaningful as 24:00")
        raise unsupported("end-of-day", fp + "/hour", "24:00 needs an end-of-day policy")
    if second == 60:
        raise unsupported("leap-second", fp + "/second", "second 60 needs a leap-second table")
    zone, offset, zone_id = "floating", None, None
    z = v.get("zone")
    if z is not None and z["type"] != "floating":
        if form != "datetime" and form != "date":
            raise unsupported("unsupported-operand", path + "/value/zone", f"a zone on a {form} is not handled")
        if z["type"] == "fixed-offset":
            zone, offset = "fixed-offset", int(z["seconds"])
        elif z["type"] == "iana":
            zone, zone_id = "iana", z["id"]
        else:
            raise unsupported("unsupported-operand", path + "/value/zone", f"zone type {z['type']!r} is not implemented")
    return Civil(form, year, month, day, hour, minute, second, nano, zone, offset, zone_id, value)


def civil_value(form: str, fields: dict[str, Any], extra: dict[str, Any] | None = None) -> dict[str, Any]:
    value: dict[str, Any] = {"form": form, "fields": fields, "calendar": dict(CAL)}
    if extra:
        value.update(extra)
    return {"schema": SCHEMA, "kind": "civil", "value": value}


def datetime_fields(t: Fraction) -> dict[str, Any]:
    """Wall-clock fields (always with ``second``) for *L* seconds since 1970-01-01T00:00."""
    whole = math.floor(t)
    nano = (t - whole) * NANOS
    days, sod = divmod(whole, 86400)
    y, m, d = civil_from_days(days)
    fields: dict[str, Any] = {"year": str(y), "month": m, "day": d, "hour": sod // 3600, "minute": sod // 60 % 60, "second": sod % 60}
    if nano:
        if nano.denominator != 1:
            raise unsupported("implementation-limit", "/value/subTick", "civil values hold at most nanosecond precision")
        fields["nano"] = int(nano)
    return fields
