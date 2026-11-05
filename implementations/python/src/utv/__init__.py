"""UTV 1.0 structural validation (first increment). Standard library only."""

from .gregorian import days_in_month, is_leap_year, validate_gregorian_date
from .result import Diagnostic, Result, Status
from .validate import CORE_KINDS, validate_structural

__version__ = "0.1.0"
SPEC_VERSION = "utv/1.0"

__all__ = ["CORE_KINDS", "Diagnostic", "Result", "Status", "days_in_month", "is_leap_year", "validate_gregorian_date", "validate_structural"]
