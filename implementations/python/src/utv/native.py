"""Explicit adapters between UTV values and :mod:`datetime` objects.

Rules (docs/everyday-api.md, "Native interop"):

- **The caller names the intent.** An aware ``datetime`` with a fixed
  ``timezone`` can be an *instant* or an *offset date-time*; pass ``intent=``.
- **No local zone.** A naive ``datetime`` is only ever a floating wall time.
  Asking for an instant or an offset from it would need the host's zone, so it
  is refused with ``unsupported``; so are ``ZoneInfo("localtime")`` and
  ``tzinfo`` classes the library can't inspect. ``astimezone()`` is never
  called without an argument.
- **Lossy conversions back say so.** Years outside 1–9999, nanoseconds that
  aren't whole microseconds, and a zoned value turned into a fixed offset give
  ``unsupported`` unless the caller passes ``allow_loss=True``; the result's
  ``loss`` then names what was dropped.

Every adapter returns a :class:`~utv.result.Result`. The ``to_*`` adapters put
native objects in ``candidates`` (``result.value`` for a unique answer).
Wrong Python types raise ``TypeError``.
"""

from __future__ import annotations

import datetime as _dt
import enum
from fractions import Fraction
from typing import Any, Optional, Union
from zoneinfo import ZoneInfo

from . import build
from ._core import ZONE_ID, Outcome, civil, instant_seconds, invalid, operation, structural, unique, unsupported
from .result import Diagnostic, Result, Status

_EPOCH = _dt.datetime(1970, 1, 1, tzinfo=_dt.timezone.utc)
_LOSSLESS = {"classification": "lossless", "fields": []}


class Intent(str, enum.Enum):
    """What a native value means, named by the caller."""

    FLOATING = "floating"  # wall time with no zone (a naive datetime)
    INSTANT = "instant"  # a point on the timeline
    OFFSET = "offset"  # wall time at a fixed UTC offset
    ZONED = "zoned"  # wall time in a named IANA zone (follows its rules)
    POSIX_SECONDS = "posix-seconds"  # timedelta as an exact number of seconds
    PERIOD = "period"  # timedelta as calendar days + wall-clock seconds


def _intent(intent: Any) -> Optional[Intent]:
    if intent is None:
        return None
    try:
        return Intent(intent)
    except ValueError:
        raise invalid("intent-required", "/intent", f"unknown intent {intent!r}") from None


def _lossy(fields: list[str]) -> dict[str, Any]:
    return {"classification": "information-losing", "fields": fields}


# --- native -> UTV -----------------------------------------------------------

@operation("convert")
def from_date(d: _dt.date) -> Result:
    """A ``datetime.date`` as a UTV ``date``."""
    if isinstance(d, _dt.datetime) or not isinstance(d, _dt.date):
        raise TypeError("from_date takes a datetime.date (use from_datetime for a datetime)")
    return unique(build.date(d.year, d.month, d.day))


@operation("convert")
def from_time(t: _dt.time) -> Result:
    """A naive ``datetime.time`` as a UTV ``time`` (always with ``second``)."""
    if not isinstance(t, _dt.time):
        raise TypeError("from_time takes a datetime.time")
    if t.tzinfo is not None:
        raise unsupported("unsupported-operand", "/tzinfo", "a time of day with a tzinfo has no UTV form; zones need a date")
    return unique(build.time(t.hour, t.minute, t.second, t.microsecond * 1000))


def _zone_key(tz: ZoneInfo) -> str:
    key = tz.key
    if key is None or key == "localtime" or key.startswith("/") or not ZONE_ID.fullmatch(key):
        raise unsupported("local-zone", "/tzinfo", f"ZoneInfo key {key!r} names the host's zone or a file, not an IANA zone id")
    return key


def _whole_seconds(delta: _dt.timedelta, path: str) -> int:
    if delta.microseconds:
        raise unsupported("unsupported-operand", path, "UTC offsets with a fraction of a second are not handled")
    return delta.days * 86400 + delta.seconds


@operation("convert")
def from_datetime(dt: _dt.datetime, intent: Union[Intent, str, None] = None) -> Result:
    """A ``datetime.datetime`` as a UTV value, with the meaning the caller names.

    ========================  ===========================================================
    ``dt``                    intents
    ========================  ===========================================================
    naive                     ``FLOATING`` (default). Anything else needs the host's
                              zone and is refused with ``unsupported``.
    fixed ``timezone``        ``INSTANT`` or ``OFFSET``; required (no default).
    ``ZoneInfo``              ``ZONED`` (default: ``in_zone`` with ``offsetAssertion``
                              from ``fold``), ``INSTANT`` or ``OFFSET``.
    ========================  ===========================================================

    ``FLOATING`` on an aware value keeps the wall time and reports the zone as lost.
    """
    if not isinstance(dt, _dt.datetime):
        raise TypeError("from_datetime takes a datetime.datetime")
    want = _intent(intent)
    wall = build.date_time(dt.year, dt.month, dt.day, dt.hour, dt.minute, dt.second, dt.microsecond * 1000)
    tz = dt.tzinfo
    if tz is None or dt.utcoffset() is None:
        if want in (None, Intent.FLOATING):
            return unique(wall)
        raise unsupported("local-zone", "/tzinfo",
                          "a naive datetime has no zone; reading it as an instant or offset would need the host's "
                          "local zone, which UTV never consults. Attach a ZoneInfo or timezone explicitly.")
    if want is Intent.FLOATING:
        return unique(wall, loss=_lossy(["zone"]))
    if isinstance(tz, _dt.timezone):
        if want is None:
            raise invalid("intent-required", "/intent", "a datetime with a fixed timezone can be an INSTANT or an OFFSET "
                                                        "date-time; pass intent=")
        if want is Intent.ZONED:
            raise unsupported("unsupported-operand", "/tzinfo", "a fixed timezone has no IANA zone id")
        key = None
    elif isinstance(tz, ZoneInfo):
        key = _zone_key(tz)
        want = want or Intent.ZONED
    else:
        raise unsupported("local-zone", "/tzinfo",
                          f"{type(tz).__name__} is not timezone or ZoneInfo; it may depend on the host's local zone")
    if want not in (Intent.INSTANT, Intent.OFFSET, Intent.ZONED):
        raise invalid("intent-required", "/intent", f"{want.value!r} is not an intent for a datetime")
    offset = _whole_seconds(dt.utcoffset(), "/tzinfo")  # type: ignore[arg-type]
    diagnostics: list[Diagnostic] = []
    if key is not None:
        # The ZoneInfo the caller passed (its tz data, not ours) decides gaps and folds.
        naive = dt.replace(tzinfo=None)
        if _native(dt.astimezone, _dt.timezone.utc).astimezone(tz).replace(tzinfo=None) != naive:
            if want is not Intent.ZONED:
                raise Outcome(Status.NONE, "gap", "/fold",
                              f"{naive.isoformat()} doesn't exist in {key} (a DST gap); Python would silently shift it", "info")
            diagnostics.append(Diagnostic("gap", "warning", "/fold",
                                          f"{naive.isoformat()} doesn't exist in {key}; no offset asserted"))
            return Result(operation="convert", status=Status.UNIQUE, candidates=[build.in_zone(wall, key)], diagnostics=diagnostics)
        if dt.replace(fold=1 - dt.fold).utcoffset() != dt.utcoffset():
            diagnostics.append(Diagnostic("ambiguous", "info", "/fold",
                                          f"{naive.isoformat()} happens twice in {key}; fold={dt.fold} chose offset {offset}s"))
    if want is Intent.INSTANT:
        delta = dt - _EPOCH
        whole = delta.days * 86400 + delta.seconds
        value = build.instant(whole, delta.microseconds * 1000)
    elif want is Intent.OFFSET:
        value = build.with_offset(wall, offset)
    else:
        value = build.in_zone(wall, key)  # type: ignore[arg-type]
        value["value"]["offsetAssertion"] = offset
    return Result(operation="convert", status=Status.UNIQUE, candidates=[value], diagnostics=diagnostics)


@operation("convert")
def from_timedelta(td: _dt.timedelta, intent: Union[Intent, str, None] = None) -> Result:
    """A ``timedelta`` as ``POSIX_SECONDS`` (exact seconds) or ``PERIOD`` (days + seconds); intent is required."""
    if not isinstance(td, _dt.timedelta):
        raise TypeError("from_timedelta takes a datetime.timedelta")
    want = _intent(intent)
    if want is Intent.POSIX_SECONDS:
        return unique(build.posix_seconds(Fraction(td.days * 86400 + td.seconds) + Fraction(td.microseconds, 1_000_000)))
    if want is Intent.PERIOD:
        if td.microseconds:
            raise unsupported("unsupported-operand", "/microseconds", "calendar periods hold whole seconds")
        return unique(build.period(days=td.days, seconds=td.seconds))
    raise invalid("intent-required", "/intent", "a timedelta can be POSIX_SECONDS or a PERIOD; pass intent=")


# --- UTV -> native -----------------------------------------------------------

def _microseconds(nano: int, allow_loss: bool, loss: list[str]) -> int:
    if nano % 1000:
        if not allow_loss:
            raise unsupported("out-of-native-range", "/value/fields/nano",
                              "datetime holds microseconds; pass allow_loss=True to truncate")
        loss.append("nano")
    return nano // 1000


def _native(fn: Any, *args: Any, **kwargs: Any) -> Any:
    try:
        return fn(*args, **kwargs)
    except (ValueError, OverflowError) as exc:
        raise unsupported("out-of-native-range", "/value", f"datetime can't hold this value ({exc})") from None


def _done(candidates: list[Any], loss: list[str], status: Status = Status.UNIQUE) -> Result:
    """A conversion result; in lossy mode, a warning per kind of loss (``precision-loss``, ``zone-dropped``)."""
    diagnostics = []
    dropped = [f for f in loss if f != "zone"]
    if dropped:
        diagnostics.append(Diagnostic("precision-loss", "warning", "/value",
                                      f"truncated to microseconds; dropped {', '.join(dropped)}"))
    if "zone" in loss:
        diagnostics.append(Diagnostic("zone-dropped", "warning", "/value/zone",
                                      "kept the asserted offset and dropped the zone"))
    return Result(operation="convert", status=status, candidates=candidates, diagnostics=diagnostics,
                  loss=_lossy(loss) if loss else dict(_LOSSLESS))


@operation("convert")
def to_date(value: Any) -> Result:
    """A UTV ``date`` as a ``datetime.date`` (years 1–9999)."""
    c = civil(value, forms=("date",))
    return unique(_native(_dt.date, c.year, c.month, c.day))


@operation("convert")
def to_time(value: Any, allow_loss: bool = False) -> Result:
    """A UTV ``time`` as a naive ``datetime.time``."""
    c = civil(value, forms=("time",))
    loss: list[str] = []
    us = _microseconds(c.nano or 0, allow_loss, loss)
    return _done([_dt.time(c.hour, c.minute, c.second or 0, us)], loss)  # type: ignore[arg-type]


@operation("convert")
def to_datetime(value: Any, tz: Optional[_dt.tzinfo] = None, allow_loss: bool = False) -> Result:
    """A UTV date-time or instant as a ``datetime.datetime``.

    - instant: aware, in UTC, or converted to ``tz`` when one is passed;
    - floating date-time: naive (``tz`` must be ``None``);
    - offset date-time: aware, with a fixed ``timezone``;
    - zoned date-time: needs ``tz``, a ``ZoneInfo`` for the same zone id (the library
      doesn't read the host's tz database). ``offsetAssertion`` picks ``fold``; without
      one, a repeated wall time gives ``multiple`` (fold 0, fold 1) and a skipped one
      gives ``none``. With ``tz=None`` and ``allow_loss=True`` the asserted offset is
      kept as a fixed ``timezone`` and the zone id is reported lost.
    """
    loss: list[str] = []
    structural(value, "")
    if value["kind"] == "instant":
        t = instant_seconds(value)
        micros = t * 1_000_000
        if micros.denominator != 1:
            if not allow_loss:
                raise unsupported("out-of-native-range", "/value/subTick", "datetime holds microseconds; pass allow_loss=True to truncate")
            loss.append("subTick")
        out = _native(lambda: _EPOCH + _dt.timedelta(microseconds=micros.numerator // micros.denominator))
        return _done([out if tz is None else _native(out.astimezone, tz)], loss)
    c = civil(value, forms=("datetime",))
    us = _microseconds(c.nano or 0, allow_loss, loss)
    naive = _native(_dt.datetime, c.year, c.month, c.day, c.hour, c.minute, c.second or 0, us)
    if c.zone == "floating":
        if tz is not None:
            raise unsupported("unsupported-operand", "/tz", "a floating date-time has no zone; use in_zone() first")
        return _done([naive], loss)
    if c.zone == "fixed-offset":
        if tz is not None:
            raise unsupported("unsupported-operand", "/tz", "an offset date-time already has its offset; don't pass tz")
        return _done([naive.replace(tzinfo=_native(_dt.timezone, _dt.timedelta(seconds=c.offset)))], loss)  # type: ignore[arg-type]
    asserted = value["value"].get("offsetAssertion")
    if tz is None:
        if allow_loss and asserted is not None:
            fixed = _native(_dt.timezone, _dt.timedelta(seconds=asserted))
            return _done([naive.replace(tzinfo=fixed)], loss + ["zone"])
        raise unsupported("needs-zone-rules", "/tz",
                          f"pass tz=ZoneInfo({c.zone_id!r}); or allow_loss=True to keep only the asserted offset")
    if not isinstance(tz, ZoneInfo) or tz.key != c.zone_id:
        raise unsupported("unsupported-operand", "/tz", f"tz must be a ZoneInfo for {c.zone_id}")
    folds = [naive.replace(tzinfo=tz, fold=f) for f in (0, 1)]
    if _native(folds[0].astimezone, _dt.timezone.utc).astimezone(tz).replace(tzinfo=None) != naive:
        return Result(operation="convert", status=Status.NONE, diagnostics=[
            Diagnostic("gap", "info", "/value/fields", f"this wall time doesn't exist in {c.zone_id} per the tz passed")])
    if asserted is not None:
        matches = [d for d in folds if d.utcoffset() == _dt.timedelta(seconds=asserted)]
        if not matches:
            raise invalid("offset-mismatch", "/value/offsetAssertion",
                          f"{c.zone_id} has no offset {asserted}s at this wall time per the tz passed")
        return _done(matches[:1], loss)
    if folds[0].utcoffset() != folds[1].utcoffset():
        return _done(folds, loss, Status.MULTIPLE)
    return _done(folds[:1], loss)


@operation("convert")
def to_timedelta(amount: Any, allow_loss: bool = False) -> Result:
    """A POSIX-seconds amount as a ``timedelta``. Calendar periods have no fixed length: ``unsupported``."""
    structural(amount, "")
    v = amount["value"]
    if amount["kind"] != "amount" or v["basis"] != "clock-ticks" or "seconds" not in v:
        raise unsupported("unsupported-operand", "/value/basis", "only POSIX-seconds amounts have a timedelta form")
    seconds = Fraction(int(v["seconds"]["numerator"]), int(v["seconds"]["denominator"]))
    us = seconds * 1_000_000
    loss: list[str] = []
    if us.denominator != 1:
        if not allow_loss:
            raise unsupported("out-of-native-range", "/value/seconds", "timedelta holds microseconds; pass allow_loss=True")
        loss.append("seconds")
    return _done([_native(_dt.timedelta, microseconds=us.numerator // us.denominator)], loss)


__all__ = [
    "Intent", "from_date", "from_datetime", "from_time", "from_timedelta",
    "to_date", "to_datetime", "to_time", "to_timedelta",
]
