"""The UTV text form, profile ``utv-text/1`` (docs/text-form.md): ``parse_text`` and
``format_text``, plus the display strings of exact second counts.

The text form is a lossless view over the JSON: points use the ISO 8601 / RFC 9557
forms of :func:`~utv.iso.parse_iso`, periods use ISO 8601 durations (``P1M``,
``-P1DT2H``), and intervals use brackets with explicit endpoint state
(``[2026-11-01, 2026-12-01)``, ``[2026-11-01, ..)``, ``[2026-11-01, ?)``).
EDTF slash intervals (``2026-11-01/2026-11-30``) are accepted on input.
"""

from __future__ import annotations

import re
from fractions import Fraction
from typing import Any, Optional

from ._core import Outcome, integer_string, invalid, operation, parse_rational, structural, unique, unsupported
from .build import UNBOUNDED, UNKNOWN, interval
from .iso import format_iso, parse_iso
from .ops import compare
from .result import Result, Status

# --- periods ---------------------------------------------------------------------

_PERIOD_UNITS = (("years", "Y"), ("months", "M"), ("weeks", "W"), ("days", "D"))
_PERIOD_TIME_UNITS = (("hours", "H"), ("minutes", "M"), ("seconds", "S"))
_NUMBER = r"[0-9]+(?:[.,][0-9]+)?"
_RE_PERIOD = re.compile(
    rf"(?P<sign>-)?P(?:(?P<years>{_NUMBER})Y)?(?:(?P<months>{_NUMBER})M)?(?:(?P<weeks>{_NUMBER})W)?"
    rf"(?:(?P<days>{_NUMBER})D)?(?:T(?:(?P<hours>{_NUMBER})H)?(?:(?P<minutes>{_NUMBER})M)?(?:(?P<seconds>{_NUMBER})S)?)?"
)
_PERIOD_NAMES = ("years", "months", "weeks", "days", "hours", "minutes", "seconds")


def _parse_period(text: str) -> Result:
    m = _RE_PERIOD.fullmatch(text)
    given = {name: m[name] for name in _PERIOD_NAMES if m and m[name] is not None} if m else {}
    if not m or not given or (text.find("T") >= 0 and not any(n in given for n in ("hours", "minutes", "seconds"))):
        raise invalid("syntax", "", f"{text!r} is not an ISO 8601 duration P[nY][nM][nW][nD][T[nH][nM][nS]]")
    if any("." in v or "," in v for v in given.values()):
        raise unsupported("unsupported-form", "", "decimal period components are not in the text form")
    sign = -1 if m["sign"] else 1
    components = {name: str(sign * int(v)) for name, v in given.items() if int(v) != 0}
    return unique({"schema": "utv/1.0", "kind": "amount", "value": {"basis": "calendar", "components": components}})


def _format_period(value: dict[str, Any]) -> str:
    v = value["value"]
    if set(v) != {"basis", "components"} or not isinstance(v["components"], dict):
        raise unsupported("no-text-form", "/value", "only plain calendar periods have a text form")
    comps: dict[str, int] = {}
    for name, text in v["components"].items():
        if name not in _PERIOD_NAMES or not isinstance(text, str) or not re.fullmatch(r"-?[0-9]+", text):
            raise unsupported("no-text-form", f"/value/components/{name}",
                              "only whole years, months, weeks, days, hours, minutes and seconds have a text form")
        n = integer_string(text, f"/value/components/{name}")
        if n:
            comps[name] = n
    if not comps:
        return "P0D"
    if any(n < 0 for n in comps.values()) and any(n > 0 for n in comps.values()):
        raise unsupported("no-text-form", "/value/components", "a period with mixed signs has no text form")
    text = "-P" if any(n < 0 for n in comps.values()) else "P"
    text += "".join(f"{abs(comps[n])}{u}" for n, u in _PERIOD_UNITS if n in comps)
    clock = "".join(f"{abs(comps[n])}{u}" for n, u in _PERIOD_TIME_UNITS if n in comps)
    return text + ("T" + clock if clock else "")


# --- intervals --------------------------------------------------------------------

_RE_BRACKETS = re.compile(r"(?P<open>[\[(])(?P<start>[^,\s]+), ?(?P<end>[^,\s]+)(?P<close>[\])])")
_OPEN_ENDS = {"..": UNBOUNDED, "?": UNKNOWN}


def _point(text: str) -> dict[str, Any]:
    res = parse_iso(text)
    if res.status is not Status.UNIQUE:
        d = res.diagnostics[0]
        raise Outcome(res.status, d.code, d.path, d.message, d.severity, also=res.diagnostics[1:])
    return res.value  # type: ignore[no-any-return]


def _shape(point: dict[str, Any]) -> tuple[Any, ...]:
    """What must agree between two endpoints: kind, and for civil points the form and zone type."""
    if point["kind"] != "civil":
        return (point["kind"],)
    v = point["value"]
    return ("civil", v["form"], v.get("zone", {}).get("type", "floating"))


def _domain(point: dict[str, Any]) -> str:
    return "physical" if point["kind"] == "instant" else "civil/gregory"


def _endpoints(start: Any, end: Any, start_included: bool, end_included: bool) -> Result:
    points = [p for p in (start, end) if "kind" in p]
    if not points:
        raise unsupported("unsupported-form", "", "an interval with no point has no domain")
    if len(points) == 2:
        if _shape(start) != _shape(end):
            raise invalid("kind-mismatch", "", "both endpoints must be points of the same kind and form")
        order = compare(start, end)
        if order.status is Status.UNIQUE and order.value == "after":
            raise invalid("reversed-interval", "", "the interval ends before it starts")
    return unique(interval(start, end, start_included, end_included, domain=_domain(points[0])))


def _parse_brackets(text: str) -> Result:
    m = _RE_BRACKETS.fullmatch(text)
    if not m:
        raise invalid("syntax", "", f"{text!r} is not an interval like [2026-11-01, 2026-12-01)")
    if (m["start"] in _OPEN_ENDS and m["open"] != "(") or (m["end"] in _OPEN_ENDS and m["close"] != ")"):
        raise invalid("syntax", "", "'..' and '?' endpoints need an open bracket: ( at the start, ) at the end")
    start = _OPEN_ENDS.get(m["start"]) or _point(m["start"])
    end = _OPEN_ENDS.get(m["end"]) or _point(m["end"])
    return _endpoints(start, end, m["open"] == "[", m["close"] == "]")


def _parse_edtf(text: str) -> Result:
    """EDTF ``a/b``: closed at both points; ``..`` is unbounded, an empty side is unknown."""
    left, right = text.split("/")
    ends = {"..": UNBOUNDED, "": UNKNOWN}
    start = ends.get(left) or _point(left)
    end = ends.get(right) or _point(right)
    return _endpoints(start, end, True, True)


def _format_interval(value: dict[str, Any]) -> str:
    v = value["value"]
    if v.get("construction", "start-end") != "start-end" or set(v) - {"domain", "start", "end", "construction"}:
        raise unsupported("no-text-form", "/value", "only start-end intervals have a text form")
    texts, points = [], []
    for side, (open_, closed) in (("start", ("(", "[")), ("end", (")", "]"))):
        e = v[side]
        if e["state"] in ("unbounded", "unknown"):
            texts.append((".." if e["state"] == "unbounded" else "?", open_))
            continue
        point = e["point"]
        if point.get("kind") not in ("civil", "instant") or v["domain"] != _domain(point):
            raise unsupported("no-text-form", f"/value/{side}/point", "the endpoint has no text form in this domain")
        res = format_iso(point)
        if res.status is not Status.UNIQUE:
            d = res.diagnostics[0]
            raise unsupported("no-text-form", f"/value/{side}/point{d.path}", d.message)
        texts.append((res.value, closed if e["state"] == "included" else open_))
        points.append(point)
    if not points or (len(points) == 2 and _shape(points[0]) != _shape(points[1])):
        raise unsupported("no-text-form", "/value", "the endpoints can't be written as one interval")
    if len(points) == 2:
        order = compare(points[0], points[1])
        if order.status is Status.UNIQUE and order.value == "after":
            raise unsupported("no-text-form", "/value", "a reversed interval has no text form")
    (start, open_), (end, close) = texts
    return f"{open_}{start}, {end}{close}"


# --- the operations ----------------------------------------------------------------

# EDTF qualifiers and unspecified digits: 1347~, 1347?, 2004-06~-11, 134X, 19XX-XX.
_RE_EDTF_QUALIFIED = re.compile(r"[+-]?[0-9X]{4,}(?:-[0-9X]{2}){0,2}")


@operation("parseText")
def parse_text(text: str) -> Result:
    """Parse the UTV text form (docs/text-form.md) into a UTV JSON value."""
    if not isinstance(text, str):
        raise invalid("malformed-value", "", "parse_text takes a str")
    if text[:1] in ("[", "("):
        return _parse_brackets(text)
    if text.startswith(("P", "-P")):
        return _parse_period(text)
    if "/" in text and "[" not in text:  # a slash outside a [Zone/Id] annotation
        if text.count("/") != 1:
            raise invalid("syntax", "", f"{text!r} has more than one '/'")
        return _parse_edtf(text)
    if any(q in text for q in "~%?X") and _RE_EDTF_QUALIFIED.fullmatch(re.sub(r"[~%?]", "", text)):
        raise unsupported("unsupported-form", "", "EDTF qualifiers and unspecified digits are not in the text form yet")
    res = parse_iso(text)
    res.operation = "parseText"
    return res


@operation("formatText")
def format_text(value: Any) -> Result:
    """The text form of a UTV JSON value; ``unsupported`` (``no-text-form``) where it has none."""
    structural(value, "")
    kind = value["kind"]
    if kind in ("civil", "instant"):
        res = format_iso(value)
        res.operation = "formatText"
        return res
    if kind == "amount" and value["value"]["basis"] == "calendar":
        return unique(_format_period(value))
    if kind == "interval":
        return unique(_format_interval(value))
    raise unsupported("no-text-form", "/kind", f"this {kind} has no text form; it is written as JSON")


# --- display strings (not parseable) -----------------------------------------------

def decimal_text(x: Fraction) -> str:
    """``x`` as a decimal when it terminates (``3600.75``), otherwise as a fraction (``1/3``)."""
    den, places = x.denominator, 0
    while den % 10 == 0 or den % 2 == 0 or den % 5 == 0:
        if den % 10 == 0:
            den //= 10
        elif den % 2 == 0:
            den //= 2
        else:
            den //= 5
        places += 1
    if den != 1:
        return f"{x.numerator}/{x.denominator}"
    if x.denominator == 1:
        return str(x.numerator)
    digits = abs(x * 10**places)
    whole, frac = divmod(int(digits), 10**places)
    text = f"{whole}.{frac:0{places}d}".rstrip("0")
    return ("-" if x < 0 else "") + text


def seconds_display(value: Any) -> Optional[str]:
    """A display string for an exact second count: ``3600.75 s (POSIX)``, ``86400 s (SI)``.

    It is for people, not for parsing. ``None`` if ``value`` is not a POSIX-seconds or
    SI-seconds amount.
    """
    if not isinstance(value, dict) or value.get("kind") != "amount":
        return None
    v = value.get("value", {})
    basis = {"clock-ticks": "POSIX", "elapsed": "SI"}.get(v.get("basis"))
    if basis is None or "seconds" not in v:
        return None
    return f"{decimal_text(parse_rational(v['seconds'], '/value/seconds'))} s ({basis})"


__all__ = ["decimal_text", "format_text", "parse_text", "seconds_display"]
