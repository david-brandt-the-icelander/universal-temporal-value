"""Typed values (docs/library-api.md §1–2), built on the JSON layer.

Each type is exactly one UTV value: ``to_json()`` gives it, ``T.from_json()``
reads it back. Constructors reject values that can't exist, the way
:class:`datetime.date` does, with :class:`~utv.outcome.OutcomeError` (a
``ValueError``); ``T.try_of(...)`` returns an outcome instead. ``str(v)`` is the
text form, ``repr(v)`` looks like ``CivilDate(2026-11-05)``.

Total operations return values. Partial ones return an
:data:`~utv.outcome.Outcome`. Every operation calls the JSON-level function
underneath, so the two layers can't disagree.
"""

from __future__ import annotations

import enum
import json
from dataclasses import dataclass
from fractions import Fraction
from typing import TYPE_CHECKING, Any, Callable, ClassVar, Generic, Literal, Optional, TypeVar, Union, cast, overload

from . import _core, build, ops
from ._core import MAX_OFFSET, NANOS, ZONE_ID
from .outcome import Outcome, OutcomeError, Truth, Unique, fail, failure, outcome_of
from .policy import MonthEnd, MonthEndLike, ResolutionPolicy
from .result import Diagnostic, Result, Status
from .text import format_text, parse_text, seconds_display
from .zones import resolve as _resolve
from .zones import to_offset as _to_offset

if TYPE_CHECKING:
    from .context import TemporalContext

V = TypeVar("V", bound="_Value")


# --- bridges to the JSON layer -------------------------------------------------------

def _check(fn: Callable[..., Any], *args: Any) -> Any:
    """Run a JSON-layer check; a value that can't exist raises OutcomeError (a ValueError)."""
    try:
        return fn(*args)
    except _core.Outcome as out:
        raise OutcomeError(out.status.value, out.diagnostics) from None


def _error(status: str, code: str, message: str) -> OutcomeError:
    return OutcomeError(status, [Diagnostic(code, "error", "", message)])


def _total(result: Result) -> Any:
    """The answer of an operation that can't fail for valid typed inputs."""
    if result.status is Status.UNIQUE:
        return result.value
    raise OutcomeError(result.status.value, result.diagnostics)


def _typed(value: Any) -> Any:
    """A JSON value the JSON layer just produced, as its typed value."""
    return from_json(value).single()


def _int(name: str, x: Any) -> int:
    if isinstance(x, bool) or not isinstance(x, int):
        raise TypeError(f"{name} must be an int, got {type(x).__name__}")
    return x


def _exact(name: str, x: Any) -> Fraction:
    if isinstance(x, bool) or not isinstance(x, (int, Fraction)):
        raise TypeError(f"{name} must be an int or a fractions.Fraction, got {type(x).__name__}")
    return Fraction(x)


def _text(value: dict[str, Any]) -> str:
    """The text form; the display string of a second count; failing both, compact JSON."""
    res = format_text(value)
    if res.status is Status.UNIQUE:
        return cast(str, res.value)
    return seconds_display(value) or json.dumps(value, separators=(",", ":"))


class _Value:
    """What every typed value offers: JSON, text, parsing and value semantics."""

    __slots__ = ()

    def to_json(self) -> dict[str, Any]:
        """The UTV JSON value."""
        raise NotImplementedError

    def __str__(self) -> str:
        return _text(self.to_json())

    def __repr__(self) -> str:
        return f"{type(self).__name__}({self})"

    @classmethod
    def from_json(cls: type[V], value: Any) -> Outcome[V]:
        """Read a UTV JSON value; a different kind or form is ``invalid`` (``kind-mismatch``)."""
        return _narrow(cls, from_json(value))

    @classmethod
    def parse(cls: type[V], text: str) -> Outcome[V]:
        """Parse the text form; bad text is an outcome, never an exception."""
        return _narrow(cls, parse(text))


def _narrow(cls: type[V], outcome: Outcome[Any]) -> Outcome[V]:
    if isinstance(outcome, Unique) and not isinstance(outcome.value, cls):
        return fail(Status.INVALID, "kind-mismatch", f"expected a {cls.__name__}, got a {type(outcome.value).__name__}")
    return outcome


def _try(make: Callable[[], V]) -> Outcome[V]:
    try:
        return Unique(make())
    except OutcomeError as err:
        return failure(err.status, err.diagnostics)


class _Ordered(_Value):
    """Comparison operators, through the JSON layer's ``compare``.

    Equality is representation equality (``09:30`` != ``09:30:00``); order is the
    order of the values (``09:30 <= 09:30:00`` and ``09:30 >= 09:30:00``).
    """

    __slots__ = ()

    def _relation(self, other: object) -> Optional[str]:
        if type(other) is not type(self):
            return None
        return cast(str, _total(ops.compare(self.to_json(), cast(_Value, other).to_json())))

    def __lt__(self, other: object) -> bool:
        r = self._relation(other)
        return NotImplemented if r is None else r == "before"

    def __le__(self, other: object) -> bool:
        r = self._relation(other)
        return NotImplemented if r is None else r != "after"

    def __gt__(self, other: object) -> bool:
        r = self._relation(other)
        return NotImplemented if r is None else r == "after"

    def __ge__(self, other: object) -> bool:
        r = self._relation(other)
        return NotImplemented if r is None else r != "before"


def _policy(month_end: MonthEndLike) -> dict[str, str]:
    return {"monthEnd": MonthEnd(month_end).value}


Constrain = Literal[MonthEnd.CONSTRAIN, "constrain"]


def _month_step(value: _Value, period: dict[str, Any], month_end: Optional[MonthEndLike]) -> Any:
    """Month arithmetic in its two forms (library-api §2, "Policy overloads"): without a policy
    it returns an Outcome (a missing month end is ``none``); with ``MonthEnd.CONSTRAIN`` it returns
    the value. Any other policy can't change the return type, so it is a programming error."""
    if month_end is None:
        return outcome_of(ops.add(value.to_json(), period), _typed)
    if MonthEnd(month_end) is not MonthEnd.CONSTRAIN:
        raise ValueError(f"the policy form takes only MonthEnd.CONSTRAIN, not {month_end!r}; "
                         "call it without a policy to get an Outcome")
    return _typed(_total(ops.add(value.to_json(), period, _policy(month_end))))


class Weekday(enum.IntEnum):
    """ISO day of the week: Monday = 1 … Sunday = 7."""

    MONDAY = 1
    TUESDAY = 2
    WEDNESDAY = 3
    THURSDAY = 4
    FRIDAY = 5
    SATURDAY = 6
    SUNDAY = 7

    def __str__(self) -> str:
        return self.name.title()


# --- civil values -------------------------------------------------------------------------

@dataclass(frozen=True, slots=True, repr=False)
class CivilDate(_Ordered):
    """A calendar day, proleptic Gregorian, with no zone. Years have no size limit."""

    year: int
    month: int
    day: int

    def __post_init__(self) -> None:
        _check(_core.civil, self.to_json())

    @classmethod
    def of(cls, year: int, month: int, day: int) -> CivilDate:
        return cls(year, month, day)

    @classmethod
    def try_of(cls, year: int, month: int, day: int) -> Outcome[CivilDate]:
        return _try(lambda: cls(year, month, day))

    def to_json(self) -> dict[str, Any]:
        return build.date(self.year, self.month, self.day)

    def _fields(self) -> dict[str, Any]:
        return cast("dict[str, Any]", _total(ops.fields(self.to_json())))

    @property
    def day_of_week(self) -> Weekday:
        return Weekday(self._fields()["dayOfWeek"])

    @property
    def day_of_year(self) -> int:
        return cast(int, self._fields()["dayOfYear"])

    @property
    def iso_week(self) -> int:
        return cast(int, self._fields()["isoWeek"])

    @property
    def iso_week_year(self) -> int:
        return int(self._fields()["isoWeekYear"])

    @property
    def days_in_month(self) -> int:
        return cast(int, self._fields()["daysInMonth"])

    @property
    def is_leap_year(self) -> bool:
        return cast(bool, self._fields()["isLeapYear"])

    def add_days(self, n: int) -> CivilDate:
        return cast(CivilDate, _typed(_total(ops.add(self.to_json(), build.period(days=n)))))

    def add_weeks(self, n: int) -> CivilDate:
        return cast(CivilDate, _typed(_total(ops.add(self.to_json(), build.period(weeks=n)))))

    @overload
    def add_months(self, n: int) -> Outcome[CivilDate]: ...
    @overload
    def add_months(self, n: int, month_end: Constrain) -> CivilDate: ...

    def add_months(self, n: int, month_end: Optional[MonthEndLike] = None) -> Union[CivilDate, Outcome[CivilDate]]:
        """``n`` months later. Without a policy a missing day (31 Jan + 1 month) is ``NoneOf``
        (``month-end``); with ``MonthEnd.CONSTRAIN`` it clamps and returns the date itself."""
        return cast("Union[CivilDate, Outcome[CivilDate]]", _month_step(self, build.period(months=n), month_end))

    @overload
    def add_years(self, n: int) -> Outcome[CivilDate]: ...
    @overload
    def add_years(self, n: int, month_end: Constrain) -> CivilDate: ...

    def add_years(self, n: int, month_end: Optional[MonthEndLike] = None) -> Union[CivilDate, Outcome[CivilDate]]:
        """``n`` years later; 29 February is handled as in :meth:`add_months`."""
        return cast("Union[CivilDate, Outcome[CivilDate]]", _month_step(self, build.period(years=n), month_end))

    def add(self, period: Period, month_end: MonthEndLike = MonthEnd.REJECT) -> Outcome[CivilDate]:
        """Add a period: ``NoneOf`` on a missing month end; ``Invalid`` (``kind-mismatch``)
        if the period has hours, minutes or seconds."""
        return outcome_of(ops.add(self.to_json(), period.to_json(), _policy(month_end)), _typed)

    def days_until(self, other: CivilDate) -> int:
        """Whole days from this date to ``other`` (negative if ``other`` is earlier)."""
        return cast(int, _total(ops.days_between(self.to_json(), other.to_json())))

    def at(self, time: CivilTime) -> CivilDateTime:
        """This date at a time of day: an unzoned date-time."""
        return CivilDateTime(self, time)


@dataclass(frozen=True, slots=True, repr=False)
class CivilYearMonth(_Ordered):
    """A calendar month, such as 2026-11."""

    year: int
    month: int

    def __post_init__(self) -> None:
        _check(_core.civil, self.to_json())

    @classmethod
    def of(cls, year: int, month: int) -> CivilYearMonth:
        return cls(year, month)

    @classmethod
    def try_of(cls, year: int, month: int) -> Outcome[CivilYearMonth]:
        return _try(lambda: cls(year, month))

    def to_json(self) -> dict[str, Any]:
        return build.year_month(self.year, self.month)

    def add_months(self, n: int) -> CivilYearMonth:
        return cast(CivilYearMonth, _typed(_total(ops.add(self.to_json(), build.period(months=n)))))

    def add_years(self, n: int) -> CivilYearMonth:
        return cast(CivilYearMonth, _typed(_total(ops.add(self.to_json(), build.period(years=n)))))


@dataclass(frozen=True, slots=True, repr=False)
class CivilTime(_Ordered):
    """A time of day. It remembers whether a second was given: ``09:30`` and
    ``09:30:00`` are different values that compare as the same time."""

    hour: int
    minute: int
    second: Optional[int] = None
    nanosecond: int = 0

    def __post_init__(self) -> None:
        if self.nanosecond and self.second is None:
            object.__setattr__(self, "second", 0)  # a fraction of a second needs a second
        _check(_core.civil, self.to_json())

    @classmethod
    def of(cls, hour: int, minute: int, second: Optional[int] = None, nanosecond: int = 0) -> CivilTime:
        return cls(hour, minute, second, nanosecond)

    @classmethod
    def try_of(cls, hour: int, minute: int, second: Optional[int] = None, nanosecond: int = 0) -> Outcome[CivilTime]:
        return _try(lambda: cls(hour, minute, second, nanosecond))

    def to_json(self) -> dict[str, Any]:
        return build.time(self.hour, self.minute, self.second, self.nanosecond)


@dataclass(frozen=True, slots=True, repr=False)
class CivilDateTime(_Ordered):
    """A date and a time of day with no zone: a wall time not yet tied to the timeline."""

    date: CivilDate
    time: CivilTime

    def __post_init__(self) -> None:
        if not isinstance(self.date, CivilDate) or not isinstance(self.time, CivilTime):
            raise TypeError("CivilDateTime(date, time) takes a CivilDate and a CivilTime; use CivilDateTime.of(y, m, d, h, mi)")

    @classmethod
    def of(cls, year: int, month: int, day: int, hour: int, minute: int,
           second: Optional[int] = None, nanosecond: int = 0) -> CivilDateTime:
        return cls(CivilDate(year, month, day), CivilTime(hour, minute, second, nanosecond))

    @classmethod
    def try_of(cls, year: int, month: int, day: int, hour: int, minute: int,
               second: Optional[int] = None, nanosecond: int = 0) -> Outcome[CivilDateTime]:
        return _try(lambda: cls.of(year, month, day, hour, minute, second, nanosecond))

    def to_json(self) -> dict[str, Any]:
        d, t = self.date, self.time
        return build.date_time(d.year, d.month, d.day, t.hour, t.minute, t.second, t.nanosecond)

    @property
    def year(self) -> int:
        return self.date.year

    @property
    def month(self) -> int:
        return self.date.month

    @property
    def day(self) -> int:
        return self.date.day

    @property
    def hour(self) -> int:
        return self.time.hour

    @property
    def minute(self) -> int:
        return self.time.minute

    @property
    def second(self) -> Optional[int]:
        return self.time.second

    @property
    def nanosecond(self) -> int:
        return self.time.nanosecond

    def _step(self, **amount: int) -> CivilDateTime:
        return cast(CivilDateTime, _typed(_total(ops.add(self.to_json(), build.period(**amount)))))

    def add_days(self, n: int) -> CivilDateTime:
        return self._step(days=n)

    def add_weeks(self, n: int) -> CivilDateTime:
        return self._step(weeks=n)

    def add_hours(self, n: int) -> CivilDateTime:
        """Wall-clock hours, carrying into days (there is no zone, so no DST)."""
        return self._step(hours=n)

    def add_minutes(self, n: int) -> CivilDateTime:
        return self._step(minutes=n)

    def add_seconds(self, n: int) -> CivilDateTime:
        return self._step(seconds=n)

    @overload
    def add_months(self, n: int) -> Outcome[CivilDateTime]: ...
    @overload
    def add_months(self, n: int, month_end: Constrain) -> CivilDateTime: ...

    def add_months(self, n: int, month_end: Optional[MonthEndLike] = None) -> Union[CivilDateTime, Outcome[CivilDateTime]]:
        """``n`` months later: an Outcome without a policy, the value with ``MonthEnd.CONSTRAIN``."""
        return cast("Union[CivilDateTime, Outcome[CivilDateTime]]", _month_step(self, build.period(months=n), month_end))

    def add(self, period: Period, month_end: MonthEndLike = MonthEnd.REJECT) -> Outcome[CivilDateTime]:
        return outcome_of(ops.add(self.to_json(), period.to_json(), _policy(month_end)), _typed)

    def in_zone(self, zone_id: str) -> ZonedIntent:
        """This wall time, intended in an IANA zone. Turning it into an instant needs rules:
        :meth:`ZonedIntent.resolve`."""
        return ZonedIntent(self, zone_id)

    def at_offset(self, seconds: int) -> FixedOffsetDateTime:
        """This wall time at a fixed UTC offset, in seconds east of UTC."""
        return FixedOffsetDateTime(self, seconds)


def _offset(name: str, seconds: Any) -> int:
    if abs(_int(name, seconds)) > MAX_OFFSET:
        raise _error("invalid", "offset-out-of-range", f"offset {seconds}s is beyond ±18:00")
    return cast(int, seconds)


@dataclass(frozen=True, slots=True, repr=False)
class FixedOffsetDateTime(_Ordered):
    """A wall time at a fixed UTC offset, such as ``2026-11-05T14:30+01:00``. It names
    one instant without any zone rules. It orders by that instant."""

    date_time: CivilDateTime
    offset_seconds: int

    def __post_init__(self) -> None:
        if not isinstance(self.date_time, CivilDateTime):
            raise TypeError("date_time must be a CivilDateTime")
        _offset("offset_seconds", self.offset_seconds)

    def to_json(self) -> dict[str, Any]:
        return build.with_offset(self.date_time.to_json(), self.offset_seconds)

    def to_instant(self) -> Instant:
        return cast(Instant, _typed(_total(_resolve(self.to_json(), None))))


@dataclass(frozen=True, slots=True, repr=False)
class ZonedIntent(_Value):
    """A wall time intended in an IANA zone, such as ``2027-06-01T09:00[Europe/Paris]``.

    It follows the zone's rules, so which instant it names depends on them:
    :meth:`resolve` takes a :class:`~utv.context.TemporalContext`. There are no
    comparison operators; use ``ctx.compare(a, b)``.
    """

    date_time: CivilDateTime
    zone_id: str
    asserted_offset_seconds: Optional[int] = None

    def __post_init__(self) -> None:
        if not isinstance(self.date_time, CivilDateTime):
            raise TypeError("date_time must be a CivilDateTime")
        if not isinstance(self.zone_id, str):
            raise TypeError("zone_id must be a str")
        if not ZONE_ID.fullmatch(self.zone_id):
            raise _error("invalid", "syntax", f"{self.zone_id!r} is not an IANA zone id like Europe/Paris")
        if self.asserted_offset_seconds is not None:
            _offset("asserted_offset_seconds", self.asserted_offset_seconds)

    def to_json(self) -> dict[str, Any]:
        value = build.in_zone(self.date_time.to_json(), self.zone_id)
        if self.asserted_offset_seconds is not None:
            value["value"]["offsetAssertion"] = self.asserted_offset_seconds
        return value

    def _step(self, **amount: int) -> ZonedIntent:
        return cast(ZonedIntent, _typed(_total(ops.add(self.to_json(), build.period(**amount)))))

    def add_days(self, n: int) -> ZonedIntent:
        """The same wall time ``n`` days later, in the same zone (drops an asserted offset)."""
        return self._step(days=n)

    def add_weeks(self, n: int) -> ZonedIntent:
        return self._step(weeks=n)

    def add_hours(self, n: int) -> ZonedIntent:
        """Wall-clock hours: 23:30 + 2 hours is 01:30, whatever DST does in between."""
        return self._step(hours=n)

    def add_minutes(self, n: int) -> ZonedIntent:
        return self._step(minutes=n)

    def add_seconds(self, n: int) -> ZonedIntent:
        return self._step(seconds=n)

    @overload
    def add_months(self, n: int) -> Outcome[ZonedIntent]: ...
    @overload
    def add_months(self, n: int, month_end: Constrain) -> ZonedIntent: ...

    def add_months(self, n: int, month_end: Optional[MonthEndLike] = None) -> Union[ZonedIntent, Outcome[ZonedIntent]]:
        """``n`` months later: an Outcome without a policy, the value with ``MonthEnd.CONSTRAIN``."""
        return cast("Union[ZonedIntent, Outcome[ZonedIntent]]", _month_step(self, build.period(months=n), month_end))

    def add(self, period: Period, month_end: MonthEndLike = MonthEnd.REJECT) -> Outcome[ZonedIntent]:
        return outcome_of(ops.add(self.to_json(), period.to_json(), _policy(month_end)), _typed)

    def resolve(self, ctx: TemporalContext, policy: ResolutionPolicy = ResolutionPolicy.STRICT) -> Outcome[Instant]:
        """The instant(s) this wall time names under ``ctx``'s rules: ``NoneOf`` in a gap
        (``gap``), ``Multiple`` in an overlap (``ambiguous``), unless ``policy`` says otherwise."""
        return ctx.resolve(self, policy)


# --- the timeline -----------------------------------------------------------------------

@dataclass(frozen=True, slots=True, repr=False)
class Instant(_Ordered):
    """A point on the POSIX timeline, exact: ``epoch_seconds`` is a rational number of
    seconds since 1970-01-01T00:00:00Z."""

    epoch_seconds: Fraction

    def __post_init__(self) -> None:
        object.__setattr__(self, "epoch_seconds", _exact("epoch_seconds", self.epoch_seconds))

    @classmethod
    def from_unix_seconds(cls, seconds: int, nanoseconds: int = 0) -> Instant:
        return cls(Fraction(_int("seconds", seconds)) + Fraction(_int("nanoseconds", nanoseconds), NANOS))

    @classmethod
    def from_unix_nanoseconds(cls, nanoseconds: int) -> Instant:
        """From a nanosecond count such as ``time.time_ns()``; the library never reads the clock."""
        return cls(Fraction(_int("nanoseconds", nanoseconds), NANOS))

    def to_json(self) -> dict[str, Any]:
        return _core.instant_value(self.epoch_seconds)

    @property
    def unix_seconds(self) -> int:
        """Whole seconds since the epoch (rounded down)."""
        return self.epoch_seconds.numerator // self.epoch_seconds.denominator

    @property
    def nanoseconds(self) -> int:
        """Nanoseconds past :attr:`unix_seconds`, rounded down (the floor of the sub-second part)."""
        return int((self.epoch_seconds - self.unix_seconds) * NANOS)

    @property
    def sub_second(self) -> Fraction:
        """The exact fraction of a second past :attr:`unix_seconds`, in [0, 1)."""
        return self.epoch_seconds - self.unix_seconds

    @overload
    def add(self, amount: PosixSeconds) -> Instant: ...
    @overload
    def add(self, amount: ElapsedSeconds) -> Outcome[Instant]: ...

    def add(self, amount: Union[PosixSeconds, ElapsedSeconds]) -> Union[Instant, Outcome[Instant]]:
        """POSIX seconds add exactly. SI seconds need a leap-second table: ``Unsupported``
        (``needs-leap-seconds``)."""
        if isinstance(amount, PosixSeconds):
            return cast(Instant, _typed(_total(ops.add(self.to_json(), amount.to_json()))))
        if isinstance(amount, ElapsedSeconds):
            return outcome_of(ops.add(self.to_json(), amount.to_json()), _typed)
        raise TypeError("Instant.add takes PosixSeconds or ElapsedSeconds")

    def until(self, other: Instant) -> PosixSeconds:
        """The exact POSIX seconds from this instant to ``other``."""
        return cast(PosixSeconds, _typed(_total(ops.posix_seconds_between(self.to_json(), other.to_json()))))

    def at_offset(self, seconds: int) -> FixedOffsetDateTime:
        """The wall time at a fixed UTC offset at this instant."""
        _offset("seconds", seconds)
        return cast(FixedOffsetDateTime, _typed(_total(_to_offset(self.to_json(), seconds))))

    def in_zone(self, zone_id: str, ctx: TemporalContext) -> Outcome[ZonedIntent]:
        """The wall time in a zone at this instant, with the offset asserted. ``Unsupported``
        if ``ctx``'s rules don't know the zone or don't cover the instant."""
        return ctx.in_zone(self, zone_id)


# --- amounts ---------------------------------------------------------------------------

_PERIOD_FIELDS = ("years", "months", "weeks", "days", "hours", "minutes", "seconds")


@dataclass(frozen=True, slots=True, repr=False)
class Period(_Value):
    """A calendar amount, such as ``P1M`` or ``P1DT2H``: steps on the calendar and the
    wall clock, not a fixed number of seconds. ``Period(days=30)``."""

    years: int = 0
    months: int = 0
    weeks: int = 0
    days: int = 0
    hours: int = 0
    minutes: int = 0
    seconds: int = 0

    def __post_init__(self) -> None:
        self.to_json()  # TypeError for anything but ints

    @classmethod
    def of(cls, years: int = 0, months: int = 0, weeks: int = 0, days: int = 0,
           hours: int = 0, minutes: int = 0, seconds: int = 0) -> Period:
        return cls(years, months, weeks, days, hours, minutes, seconds)

    @classmethod
    def of_years(cls, n: int) -> Period:
        return cls(years=n)

    @classmethod
    def of_months(cls, n: int) -> Period:
        return cls(months=n)

    @classmethod
    def of_weeks(cls, n: int) -> Period:
        return cls(weeks=n)

    @classmethod
    def of_days(cls, n: int) -> Period:
        return cls(days=n)

    def to_json(self) -> dict[str, Any]:
        return build.period(**{name: getattr(self, name) for name in _PERIOD_FIELDS})

    def negate(self) -> Period:
        return Period(*(-getattr(self, name) for name in _PERIOD_FIELDS))


@dataclass(frozen=True, slots=True, repr=False)
class PosixSeconds(_Value):
    """An exact number of POSIX seconds (a day is always 86,400 of them; not SI seconds
    across a leap second). Prints as ``3600.75 s (POSIX)``, which is not parseable."""

    seconds: Fraction

    def __post_init__(self) -> None:
        object.__setattr__(self, "seconds", _exact("seconds", self.seconds))

    @classmethod
    def of(cls, numerator: int, denominator: int = 1) -> PosixSeconds:
        return cls(Fraction(_int("numerator", numerator), _int("denominator", denominator)))

    def to_json(self) -> dict[str, Any]:
        return build.posix_seconds(self.seconds)

    def negate(self) -> PosixSeconds:
        return PosixSeconds(-self.seconds)


@dataclass(frozen=True, slots=True, repr=False)
class ElapsedSeconds(_Value):
    """An exact number of SI seconds. Prints as ``86400 s (SI)``."""

    seconds: Fraction

    def __post_init__(self) -> None:
        object.__setattr__(self, "seconds", _exact("seconds", self.seconds))

    @classmethod
    def of(cls, numerator: int, denominator: int = 1) -> ElapsedSeconds:
        return cls(Fraction(_int("numerator", numerator), _int("denominator", denominator)))

    def to_json(self) -> dict[str, Any]:
        return build.elapsed_seconds(self.seconds)

    def negate(self) -> ElapsedSeconds:
        return ElapsedSeconds(-self.seconds)


# --- intervals ------------------------------------------------------------------------------

P = TypeVar("P", CivilDate, Instant)
BoundState = Literal["included", "excluded", "unbounded", "unknown"]


@dataclass(frozen=True, slots=True, repr=False)
class Bound(Generic[P]):
    """One end of an interval: ``Bound.included(v)``, ``Bound.excluded(v)``,
    ``Bound.UNBOUNDED`` (there is no end) or ``Bound.UNKNOWN`` (there is one; nobody knows it)."""

    state: BoundState
    value: Optional[P] = None
    UNBOUNDED: ClassVar[Bound[Any]]
    UNKNOWN: ClassVar[Bound[Any]]

    def __post_init__(self) -> None:
        if self.state in ("included", "excluded"):
            if not isinstance(self.value, (CivilDate, Instant)):
                raise TypeError("an included or excluded bound takes a CivilDate or an Instant")
        elif self.state in ("unbounded", "unknown"):
            if self.value is not None:
                raise ValueError(f"an {self.state} bound has no value")
        else:
            raise ValueError(f"unknown bound state {self.state!r}")

    @classmethod
    def included(cls, value: P) -> Bound[P]:
        return cls("included", value)

    @classmethod
    def excluded(cls, value: P) -> Bound[P]:
        return cls("excluded", value)

    def _json(self) -> dict[str, Any]:
        if self.value is None:
            return {"state": self.state}
        return {"state": self.state, "point": self.value.to_json()}

    def __repr__(self) -> str:
        if self.value is None:
            return f"Bound.{self.state.upper()}"
        return f"Bound.{self.state}({self.value!r})"


Bound.UNBOUNDED = Bound("unbounded")
Bound.UNKNOWN = Bound("unknown")


@dataclass(frozen=True, slots=True, repr=False)
class Interval(_Value, Generic[P]):
    """A range of dates or of instants, with each end's state explicit:
    ``[2026-11-01, 2026-12-01)``, ``[2026-11-01, ..)``, ``[2026-11-01, ?)``."""

    start: Bound[P]
    end: Bound[P]

    def __post_init__(self) -> None:
        if not isinstance(self.start, Bound) or not isinstance(self.end, Bound):
            raise TypeError("Interval(start, end) takes two Bounds; use Interval.half_open(a, b)")
        points = [b.value for b in (self.start, self.end) if b.value is not None]
        if not points:
            raise _error("unsupported", "unsupported-form", "an interval with no point has no domain")
        if len({type(p) for p in points}) > 1:
            raise _error("invalid", "kind-mismatch", "both endpoints must be dates, or both instants")
        if len(points) == 2 and ops.compare(points[0].to_json(), points[1].to_json()).value == "after":
            raise _error("invalid", "reversed-interval", f"the interval ends ({points[1]}) before it starts ({points[0]})")

    @classmethod
    def of(cls, start: Bound[P], end: Bound[P]) -> Interval[P]:
        return cls(start, end)

    @classmethod
    def try_of(cls, start: Bound[P], end: Bound[P]) -> Outcome[Interval[P]]:
        return _try(lambda: cls(start, end))

    @classmethod
    def half_open(cls, start: P, end: P) -> Interval[P]:
        """``[start, end)``: includes ``start``, excludes ``end``."""
        return cls(Bound.included(start), Bound.excluded(end))

    @classmethod
    def closed(cls, start: P, end: P) -> Interval[P]:
        """``[start, end]``: includes both."""
        return cls(Bound.included(start), Bound.included(end))

    def to_json(self) -> dict[str, Any]:
        point = self.start.value if self.start.value is not None else self.end.value
        domain = "physical" if isinstance(point, Instant) else "civil/gregory"
        return {"schema": _core.SCHEMA, "kind": "interval",
                "value": {"domain": domain, "start": self.start._json(), "end": self.end._json()}}

    def contains(self, point: P) -> Truth:
        """``DEFINITELY``, ``POSSIBLY`` (an unknown end decides) or ``IMPOSSIBLE``."""
        return Truth(_total(ops.contains(self.to_json(), point.to_json())))

    def overlaps(self, other: Interval[P]) -> Truth:
        return Truth(_total(ops.overlaps(self.to_json(), other.to_json())))


# --- any value -------------------------------------------------------------------------------

Value = Union[CivilDate, CivilYearMonth, CivilTime, CivilDateTime, FixedOffsetDateTime, ZonedIntent,
              Instant, Period, PosixSeconds, ElapsedSeconds, Interval[Any]]
"""Any typed value: what :func:`parse` and :func:`from_json` return."""


def _civil_time(c: _core.Civil) -> CivilTime:
    return CivilTime(cast(int, c.hour), cast(int, c.minute), c.second, c.nano or 0)


def _from_json(value: Any, path: str = "") -> Value:
    """The typed value for a JSON value; raises the JSON layer's outcome if there is none."""
    _core.structural(value, path)
    kind, v = value["kind"], value["value"]
    if kind == "civil":
        c = _core.civil(value, path)
        if c.form == "date" and c.zone == "floating":
            return CivilDate(cast(int, c.year), cast(int, c.month), cast(int, c.day))
        if c.form == "year-month":
            return CivilYearMonth(cast(int, c.year), cast(int, c.month))
        if c.form == "time":
            return _civil_time(c)
        if c.form == "datetime":
            dt = CivilDateTime(CivilDate(cast(int, c.year), cast(int, c.month), cast(int, c.day)), _civil_time(c))
            if c.zone == "floating":
                return dt
            if c.zone == "fixed-offset":
                return FixedOffsetDateTime(dt, cast(int, c.offset))
            return ZonedIntent(dt, cast(str, c.zone_id), v.get("offsetAssertion"))
    elif kind == "instant":
        return Instant(_core.instant_seconds(value, path))
    elif kind == "amount":
        basis = v["basis"]
        if basis == "calendar" and isinstance(v.get("components"), dict):
            comps = v["components"]
            if all(name in _PERIOD_FIELDS for name in comps):
                return Period(**{name: _core.integer_string(text, f"{path}/value/components/{name}")
                                 for name, text in comps.items()})
        if basis == "clock-ticks" and _core.is_posix(v.get("rules")) and "seconds" in v:
            return PosixSeconds(_core.parse_rational(v["seconds"], path + "/value/seconds"))
        if basis == "elapsed" and "seconds" in v:
            return ElapsedSeconds(_core.parse_rational(v["seconds"], path + "/value/seconds"))
    elif kind == "interval" and "start" in v and "end" in v:
        bounds: list[Bound[Any]] = []
        for side in ("start", "end"):
            e = v[side]
            if "point" not in e:
                bounds.append(Bound(e["state"]))
                continue
            point = _from_json(e["point"], f"{path}/value/{side}/point")
            if not isinstance(point, (CivilDate, Instant)):
                raise _core.unsupported("unsupported-operand", f"{path}/value/{side}/point",
                                        "typed intervals hold dates or instants")
            bounds.append(Bound(e["state"], point))
        return Interval(bounds[0], bounds[1])
    raise _core.unsupported("unsupported-operand", path, f"the typed layer has no type for this {kind}; use the JSON layer")


def _normalized(value: Any) -> Any:
    """The JSON a typed value would write for ``value``: an explicit floating zone is dropped
    (it means the same as no zone)."""
    if not isinstance(value, dict):
        return value
    out = {k: _normalized(x) for k, x in value.items()}
    if out.get("kind") == "civil" and isinstance(out.get("value"), dict) and out["value"].get("zone") == {"type": "floating"}:
        out["value"] = {k: x for k, x in out["value"].items() if k != "zone"}
    return out


def _canonical(value: Any) -> str:
    return json.dumps(value, sort_keys=True, separators=(",", ":"))


def from_json(value: Any) -> Outcome[Value]:
    """The typed value of a UTV JSON value (a dict or JSON text).

    ``Unsupported`` (``unsupported-operand``) if no typed value holds it exactly, for
    example a zoned date or a value with ``provenance``; it stays usable in the JSON layer.
    """
    if isinstance(value, (str, bytes)):
        try:
            value = json.loads(value)
        except ValueError as exc:
            return fail(Status.INVALID, "malformed-value", f"not JSON: {exc}")
    try:
        typed = _from_json(value)
    except _core.Outcome as out:
        return failure(out.status, out.diagnostics)
    except OutcomeError as err:
        return failure(err.status, err.diagnostics)
    if _canonical(typed.to_json()) != _canonical(_normalized(value)):
        return fail(Status.UNSUPPORTED, "unsupported-operand",
                    f"a {type(typed).__name__} can't hold this value exactly; use the JSON layer")
    return Unique(typed)


def parse(text: str) -> Outcome[Value]:
    """Parse any text-form value: ``2026-11-05``, ``2027-06-01T09:00[Europe/Paris]``, ``P1M``,
    ``[2026-11-01, ..)``, … Bad text is an outcome, never an exception."""
    res = parse_text(text)
    if res.status is not Status.UNIQUE:
        return failure(res.status, res.diagnostics, res)
    return from_json(res.value)


__all__ = [
    "Bound", "CivilDate", "CivilDateTime", "CivilTime", "CivilYearMonth", "ElapsedSeconds", "FixedOffsetDateTime",
    "Instant", "Interval", "Period", "PosixSeconds", "Value", "Weekday", "ZonedIntent", "from_json", "parse",
]
