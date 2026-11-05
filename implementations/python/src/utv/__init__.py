"""Universal Temporal Value (UTV) 1.0 for Python. Standard library only.

- 0.1: structural validation (:func:`validate_structural`) and Gregorian date
  validation (:func:`validate_gregorian_date`).
- 0.2: the everyday API (docs/everyday-api.md): builders, ISO 8601 parsing and
  formatting, calendar arithmetic, comparison, intervals, zone resolution with
  explicit rules, and adapters to and from :mod:`datetime` (:mod:`utv.native`).

Values are plain JSON dicts. Every operation returns a :class:`Result`. The
library never reads the clock, the host time zone or the locale.
"""

from .build import (
    UNBOUNDED,
    UNKNOWN,
    date,
    date_time,
    in_zone,
    instant,
    interval,
    period,
    posix_seconds,
    time,
    with_offset,
    year_month,
)
from .gregorian import days_in_month, is_leap_year, validate_gregorian_date
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
from .result import Diagnostic, Result, Status
from .validate import CORE_KINDS, validate_structural
from .zones import ZoneRules, load_rules, parse_rules, resolve, to_offset, to_zone

__version__ = "0.2.0"
SPEC_VERSION = "utv/1.0"

__all__ = [
    "CORE_KINDS", "Diagnostic", "Intent", "Result", "Status", "UNBOUNDED", "UNKNOWN", "ZoneRules",
    "add", "compare", "contains", "date", "date_time", "days_between", "days_in_month", "fields",
    "format_iso", "from_date", "from_datetime", "from_time", "from_timedelta", "in_zone", "instant",
    "interval", "is_leap_year", "load_rules", "overlaps", "parse_iso", "parse_rules", "period",
    "posix_seconds", "posix_seconds_between", "resolve", "time", "to_date", "to_datetime", "to_offset",
    "to_time", "to_timedelta", "to_zone", "validate_gregorian_date", "validate_structural", "with_offset",
    "year_month",
]
