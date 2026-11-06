"""Universal Temporal Value (UTV) 1.0 for Python. Standard library only.

The typed API (0.3, docs/library-api.md)::

    from utv import CivilDate, MonthEnd, NoneOf, Unique

    jan31 = CivilDate.of(2026, 1, 31)
    match jan31.add_months(1):                     # Outcome[CivilDate]
        case Unique(value=d):
            print(d)
        case NoneOf(diagnostics=why):
            print("no such date:", why[0].message)  # this runs: 2026-02 has no day 31
    print(jan31.add_months(1, MonthEnd.CONSTRAIN))  # 2026-02-28, because you asked

- Typed values: :class:`CivilDate`, :class:`CivilYearMonth`, :class:`CivilTime`,
  :class:`CivilDateTime`, :class:`FixedOffsetDateTime`, :class:`ZonedIntent`,
  :class:`Instant`, :class:`Period`, :class:`PosixSeconds`, :class:`ElapsedSeconds`,
  :class:`Interval` / :class:`Bound`. ``str(v)`` is the text form; :func:`parse` reads it.
- Total operations return values; partial ones return an :data:`Outcome`:
  :class:`Unique`, :class:`Multiple`, :class:`NoneOf`, :class:`Unsupported`, :class:`Invalid`.
- Zone rules are held by a :class:`TemporalContext`; there is no default one.

The JSON layer (0.1, 0.2) is unchanged underneath: values are plain JSON dicts and
every function returns a :class:`Result` (``validate_structural``, ``parse_iso``,
``add``, ``resolve``, …, plus ``parse_text`` / ``format_text`` for the text form).

The library never reads the clock, the host time zone or the locale.
"""

from .build import (
    UNBOUNDED,
    UNKNOWN,
    date,
    date_time,
    elapsed_seconds,
    in_zone,
    instant,
    interval,
    period,
    posix_seconds,
    time,
    with_offset,
    year_month,
)
from .context import TemporalContext
from .gregorian import days_in_month, is_leap_year, validate_gregorian_date
from .interop import from_native, to_native
from .iso import format_iso, parse_iso
from .native import (
    Intent,
    from_date,
    from_datetime,
    from_time,
    from_timedelta,
    to_date,
    to_datetime,
    to_time,
    to_timedelta,
)
from .ops import add, compare, contains, days_between, fields, overlaps, posix_seconds_between
from .outcome import Invalid, Multiple, NoneOf, Ordering, Outcome, OutcomeError, Truth, Unique, Unsupported
from .policy import Gap, MonthEnd, Overlap, ResolutionPolicy
from .result import Diagnostic, Result, Status
from .text import format_text, parse_text
from .validate import CORE_KINDS, validate_structural
from .values import (
    Bound,
    CivilDate,
    CivilDateTime,
    CivilTime,
    CivilYearMonth,
    ElapsedSeconds,
    FixedOffsetDateTime,
    Instant,
    Interval,
    Period,
    PosixSeconds,
    Value,
    Weekday,
    ZonedIntent,
    from_json,
    parse,
)
from .zones import ZoneRules, load_rules, parse_rules, resolve, to_offset, to_zone

__version__ = "0.3.0"
SPEC_VERSION = "utv/1.0"

__all__ = [
    # typed layer (0.3)
    "Bound", "CivilDate", "CivilDateTime", "CivilTime", "CivilYearMonth", "ElapsedSeconds", "FixedOffsetDateTime",
    "Gap", "Instant", "Interval", "Invalid", "MonthEnd", "Multiple", "NoneOf", "Ordering", "Outcome",
    "OutcomeError", "Overlap", "Period", "PosixSeconds", "ResolutionPolicy", "TemporalContext", "Truth", "Unique",
    "Unsupported", "Value", "Weekday", "ZonedIntent", "from_json", "from_native", "parse", "to_native",
    # JSON layer (0.1, 0.2, and the text form)
    "CORE_KINDS", "Diagnostic", "Intent", "Result", "Status", "UNBOUNDED", "UNKNOWN", "ZoneRules",
    "add", "compare", "contains", "date", "date_time", "days_between", "days_in_month", "elapsed_seconds", "fields",
    "format_iso", "format_text", "from_date", "from_datetime", "from_time", "from_timedelta", "in_zone", "instant",
    "interval", "is_leap_year", "load_rules", "overlaps", "parse_iso", "parse_rules", "parse_text", "period",
    "posix_seconds", "posix_seconds_between", "resolve", "time", "to_date", "to_datetime", "to_offset",
    "to_time", "to_timedelta", "to_zone", "validate_gregorian_date", "validate_structural", "with_offset",
    "year_month",
]
