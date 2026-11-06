"""Typed adapters to and from :mod:`datetime`, over the JSON-level ones in :mod:`utv.native`.

The rules are the same: the caller names the intent, a naive ``datetime`` is never
read as local time, and lossy conversions back say so::

    utv.from_native(datetime(2026, 11, 5, 14, 30, tzinfo=timezone.utc), Intent.INSTANT)
    # Unique(Instant(2026-11-05T14:30:00Z))
    utv.to_native(CivilDate.of(10000, 1, 1))   # Unsupported (out-of-native-range)
"""

from __future__ import annotations

import datetime as _dt
from typing import Any, Optional, Union

from . import native
from .native import Intent
from .outcome import Outcome, fail, outcome_of
from .result import Result, Status
from .values import (
    CivilDate,
    CivilDateTime,
    CivilTime,
    FixedOffsetDateTime,
    Instant,
    PosixSeconds,
    Value,
    ZonedIntent,
    _typed,
)

Native = Union[_dt.date, _dt.time, _dt.datetime, _dt.timedelta]


def from_native(value: Native, intent: Union[Intent, str, None] = None) -> Outcome[Value]:
    """A ``date``, ``time``, ``datetime`` or ``timedelta`` as a typed UTV value.

    ``intent`` says what a ``datetime`` or ``timedelta`` means (see
    :func:`utv.native.from_datetime`): an aware ``datetime`` with a fixed
    ``timezone`` needs ``Intent.INSTANT`` or ``Intent.OFFSET``; a naive one can
    only be a floating wall time.
    """
    res: Result
    if isinstance(value, _dt.datetime):
        res = native.from_datetime(value, intent)
    elif isinstance(value, _dt.date):
        res = native.from_date(value)
    elif isinstance(value, _dt.time):
        res = native.from_time(value)
    elif isinstance(value, _dt.timedelta):
        res = native.from_timedelta(value, intent)
    else:
        raise TypeError(f"from_native takes a date, time, datetime or timedelta, not {type(value).__name__}")
    return outcome_of(res, _typed)


def to_native(value: Value, *, tz: Optional[_dt.tzinfo] = None, allow_loss: bool = False) -> Outcome[Any]:
    """A typed value as a ``date``, ``time``, ``datetime`` or ``timedelta``.

    ``Unsupported`` (``out-of-native-range``) where the native type can't hold it,
    unless ``allow_loss=True`` (a ``precision-loss`` / ``zone-dropped`` warning then
    says what was dropped). A zoned value needs ``tz=ZoneInfo(<same id>)``.
    """
    if isinstance(value, CivilDate):
        res = native.to_date(value.to_json())
    elif isinstance(value, CivilTime):
        res = native.to_time(value.to_json(), allow_loss)
    elif isinstance(value, (CivilDateTime, FixedOffsetDateTime, ZonedIntent, Instant)):
        res = native.to_datetime(value.to_json(), tz, allow_loss)
    elif isinstance(value, PosixSeconds):
        res = native.to_timedelta(value.to_json(), allow_loss)
    else:
        return fail(Status.UNSUPPORTED, "unsupported-operand", f"a {type(value).__name__} has no datetime equivalent")
    return outcome_of(res, lambda x: x)


__all__ = ["from_native", "to_native"]
