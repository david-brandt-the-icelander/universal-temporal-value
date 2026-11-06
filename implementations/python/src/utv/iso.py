"""ISO 8601 extended / RFC 3339 / RFC 9557 parsing and formatting
(docs/everyday-api.md, ``parseIso`` and ``formatIso``)."""

from __future__ import annotations

import math
import re
from fractions import Fraction
from typing import Any

from ._core import (
    MAX_OFFSET,
    NANOS,
    ZONE_ID,
    civil,
    civil_value,
    datetime_fields,
    days_from_civil,
    instant_seconds,
    instant_value,
    invalid,
    operation,
    structural,
    unique,
    unsupported,
)
from .result import Result

_YEAR = r"(?P<year>[0-9]{4}|[+-][0-9]{4,})"
_DATE = _YEAR + r"-(?P<month>[0-9]{2})-(?P<day>[0-9]{2})"
_TIME = r"(?P<hour>[0-9]{2}):(?P<minute>[0-9]{2})(?::(?P<second>[0-9]{2})(?:\.(?P<frac>[0-9]{1,9}))?)?"
_OFFSET = r"(?P<offset>Z|[+-][0-9]{2}:[0-9]{2})"
_ANNOTATIONS = r"(?P<annotations>(?:\[[^\[\]]*\])*)"

_RE_DATE = re.compile(_DATE)
_RE_YEAR_MONTH = re.compile(_YEAR + r"-(?P<month>[0-9]{2})")
_RE_TIME = re.compile(_TIME)
_RE_DATETIME = re.compile(_DATE + "T" + _TIME + _OFFSET + "?" + _ANNOTATIONS)
_RE_ANNOTATION = re.compile(r"\[([^\[\]]*)\]")

# Valid ISO 8601 / RFC 9557 forms this profile recognises but doesn't handle.
_TIME_TAIL = r"(?:T[0-9:.]+(?:Z|[+-][0-9:]+)?)?"
_UNSUPPORTED_FORMS = [
    (re.compile(_YEAR + r"-W[0-9]{2}(?:-[1-7])?" + _TIME_TAIL), "unsupported-form", "week dates are not handled by this profile"),
    (re.compile(_YEAR + r"-[0-9]{3}" + _TIME_TAIL), "unsupported-form", "ordinal dates are not handled by this profile"),
    (re.compile(r"[+-]?[0-9]{4,}W[0-9]{2,3}"), "unsupported-form", "basic-format week dates are not handled by this profile"),
    (re.compile(r"[+-]?[0-9]{7,8}(?:T[0-9]{2,6}(?:[.,][0-9]+)?(?:Z|[+-][0-9]{2}(?:[0-9]{2})?)?)?"), "unsupported-form",
     "basic format (no separators) is not handled by this profile"),
]


def _year(text: str) -> int:
    year = int(text)
    if text.startswith("-") and year == 0:
        raise invalid("syntax", "", "-0000 is not a valid year")
    return year


def _time_fields(m: re.Match[str]) -> dict[str, Any]:
    fields: dict[str, Any] = {"hour": int(m["hour"]), "minute": int(m["minute"])}
    if m["second"] is not None:
        fields["second"] = int(m["second"])
        if m["frac"] is not None:
            nano = int(m["frac"].ljust(9, "0"))
            if nano:
                fields["nano"] = nano
    return fields


def _date_fields(m: re.Match[str]) -> dict[str, Any]:
    return {"year": str(_year(m["year"])), "month": int(m["month"]), "day": int(m["day"])}


def _offset_seconds(text: str) -> int:
    hh, mm = int(text[1:3]), int(text[4:6])
    if mm > 59 or hh * 3600 + mm * 60 > MAX_OFFSET:
        raise invalid("offset-out-of-range", "", f"offset {text} is beyond ±18:00")
    seconds = hh * 3600 + mm * 60
    return -seconds if text[0] == "-" else seconds


def _zone_annotation(annotations: str) -> str | None:
    """The single ``[Area/Location]`` annotation, if any.

    Anything else in brackets (critical ``[!…]``, ``[u-ca=…]``, ``[+01:00]``, a second
    annotation) is ``unsupported`` (contract, Clarifications 3).
    """
    found = _RE_ANNOTATION.findall(annotations)
    if not found:
        return None
    if any(content.startswith("!") for content in found):
        raise unsupported("unsupported-annotation", "", "critical annotations ([!…]) are not handled by this profile")
    if len(found) > 1 or not ZONE_ID.fullmatch(found[0]):
        shown = "".join(f"[{c}]" for c in found)
        raise unsupported("unsupported-annotation", "", f"only a single [Area/Location] annotation is handled, not {shown}")
    return str(found[0])


@operation("parseIso")
def parse_iso(text: str) -> Result:
    """Parse ISO 8601 extended / RFC 3339 / RFC 9557 text into a UTV value."""
    if not isinstance(text, str):
        raise invalid("malformed-value", "", "parse_iso takes a str")
    if m := _RE_DATE.fullmatch(text):
        value = civil_value("date", _date_fields(m))
    elif m := _RE_YEAR_MONTH.fullmatch(text):
        value = civil_value("year-month", {"year": str(_year(m["year"])), "month": int(m["month"])})
    elif m := _RE_TIME.fullmatch(text):
        value = civil_value("time", _time_fields(m))
    elif m := _RE_DATETIME.fullmatch(text):
        return _parse_datetime(m)
    else:
        for pattern, code, message in _UNSUPPORTED_FORMS:
            if pattern.fullmatch(text):
                raise unsupported(code, "", message)
        raise invalid("syntax", "", f"{text!r} is not in the supported ISO 8601 / RFC 3339 / RFC 9557 grammar")
    civil(value)  # field ranges: invalid / unsupported
    return unique(value)


def _parse_datetime(m: re.Match[str]) -> Result:
    fields = _date_fields(m)
    fields.update(_time_fields(m))
    offset = m["offset"]
    seconds = None if offset in (None, "Z", "-00:00") else _offset_seconds(offset)
    zone = _zone_annotation(m["annotations"])
    local = civil_value("datetime", fields)
    checked = civil(local)
    if offset in ("Z", "-00:00"):
        if zone is not None:
            raise unsupported("unsupported-annotation", "", "a [zone] after Z or -00:00 is not handled by this profile")
        return unique(instant_value(checked.wall_seconds()))
    if zone is not None:
        extra: dict[str, Any] = {"zone": {"type": "iana", "id": zone}, "zoneIntent": "follow-zone-id"}
        if seconds is not None:
            extra["offsetAssertion"] = seconds
        return unique(civil_value("datetime", fields, extra))
    if seconds is not None:
        return unique(civil_value("datetime", fields, {"zone": {"type": "fixed-offset", "seconds": seconds}, "zoneIntent": "fixed-offset"}))
    return unique(local)


# --- formatting ------------------------------------------------------------

def _fmt_year(year: int) -> str:
    if 0 <= year <= 9999:
        return f"{year:04d}"
    return f"{'+' if year > 0 else '-'}{abs(year):04d}"


def _fmt_fraction(nano: int) -> str:
    return "." + f"{nano:09d}".rstrip("0") if nano else ""


def _fmt_offset(seconds: int) -> str:
    if seconds % 60 or abs(seconds) > 99 * 3600:
        raise unsupported("no-text-form", "/value/zone/seconds", "the offset can't be printed as ±hh:mm")
    sign = "-" if seconds < 0 else "+"
    hh, mm = divmod(abs(seconds) // 60, 60)
    return f"{sign}{hh:02d}:{mm:02d}"


def _fmt_time(c: Any) -> str:
    out = f"{c.hour:02d}:{c.minute:02d}"
    if c.second is not None or c.nano:
        out += f":{c.second or 0:02d}" + _fmt_fraction(c.nano or 0)
    return out


@operation("formatIso")
def format_iso(value: Any) -> Result:
    """Format a UTV value as ISO 8601 / RFC 3339 / RFC 9557 text."""
    structural(value, "")
    if value["kind"] == "instant":
        t = instant_seconds(value)
        whole = math.floor(t)
        frac = (t - whole) * NANOS
        if frac.denominator != 1:
            raise unsupported("no-text-form", "/value/subTick", "the fraction needs more than 9 decimal digits")
        f = datetime_fields(Fraction(whole))
        text = f"{_fmt_year(int(f['year']))}-{f['month']:02d}-{f['day']:02d}T{f['hour']:02d}:{f['minute']:02d}:{f['second']:02d}"
        return unique(text + _fmt_fraction(int(frac)) + "Z")
    if value["kind"] != "civil":
        raise unsupported("unsupported-operand", "/kind", f"{value['kind']!r} values have no ISO 8601 form in this profile")
    c = civil(value)
    if c.form == "time":
        return unique(_fmt_time(c))
    text = f"{_fmt_year(c.year)}-{c.month:02d}"  # type: ignore[arg-type]
    if c.form == "year-month":
        return unique(text)
    text += f"-{c.day:02d}"
    if c.form == "date":
        if c.zone != "floating":
            raise unsupported("no-text-form", "/value/zone", "a zoned date has no ISO 8601 form")
        return unique(text)
    text += "T" + _fmt_time(c)
    if c.zone == "fixed-offset":
        text += _fmt_offset(c.offset)  # type: ignore[arg-type]
    elif c.zone == "iana":
        asserted = value["value"].get("offsetAssertion")
        text += (_fmt_offset(asserted) if asserted is not None else "") + f"[{c.zone_id}]"
    return unique(text)
