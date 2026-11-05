"""``Outcome[T]``: the answer of a partial operation (docs/library-api.md §3).

An operation that can't fail for valid inputs returns a plain value. Only a
partial one returns an outcome, so every outcome in your code marks a question
time can refuse to answer. Handle it with ``match``::

    match date.add_months(1):
        case Unique(value=d):
            print(d)
        case NoneOf(diagnostics=why):
            print("no such date:", why[0].message)
        case other:
            print(other)            # multiple, unsupported or invalid

The helpers never pick an answer silently: :meth:`unique_or` needs a fallback,
and :meth:`single` raises :class:`OutcomeError` for anything but ``Unique``.
"""

from __future__ import annotations

import enum
from dataclasses import dataclass, field
from typing import Any, Callable, ClassVar, Generic, Iterable, Optional, TypeVar, Union

from .result import Diagnostic, Result, Status

T = TypeVar("T")
T_co = TypeVar("T_co", covariant=True)
U = TypeVar("U")


def _describe(status: str, diagnostics: tuple[Diagnostic, ...]) -> str:
    codes = ", ".join(dict.fromkeys(d.code for d in diagnostics))
    return f"{status} ({codes})" if codes else status


class OutcomeError(ValueError):
    """Raised by :meth:`single` on anything but ``Unique``, and by constructors given
    a value that can't exist (``CivilDate.of(2026, 2, 30)``). It is a ``ValueError``.

    ``status`` is the wire status (``"none"``, ``"invalid"``, …); ``diagnostics`` says why.
    """

    def __init__(self, status: str, diagnostics: Iterable[Diagnostic]) -> None:
        self.status = status
        self.diagnostics = tuple(diagnostics)
        reason = "; ".join(d.message for d in self.diagnostics if d.message)
        super().__init__(_describe(status, self.diagnostics) + (f": {reason}" if reason else ""))


class _Case(Generic[T_co]):
    """What every outcome case offers."""

    __slots__ = ()
    status: ClassVar[str]
    """The wire status: ``unique``, ``multiple``, ``none``, ``unsupported`` or ``invalid``."""

    @property
    def codes(self) -> tuple[str, ...]:
        """The diagnostic codes, in order (shared by every UTV library)."""
        return tuple(d.code for d in self._diagnostics())

    def _diagnostics(self) -> tuple[Diagnostic, ...]:
        diagnostics: tuple[Diagnostic, ...] = getattr(self, "diagnostics", ())
        return diagnostics

    def unique_or(self, fallback: U) -> Union[T_co, U]:
        """The value of a ``Unique`` outcome, otherwise ``fallback``."""
        return self.value if isinstance(self, Unique) else fallback

    def single(self) -> T_co:
        """The value of a ``Unique`` outcome; anything else raises :class:`OutcomeError`."""
        if isinstance(self, Unique):
            return self.value  # type: ignore[no-any-return]
        raise OutcomeError(self.status, self._diagnostics())

    def to_result(self) -> Result:
        """The ``utv-result/1.0`` envelope of the JSON layer, as a bridge to it."""
        kept = getattr(self, "_result", None)
        if kept is not None:
            return kept  # type: ignore[no-any-return]
        values = (self.value,) if isinstance(self, Unique) else self.values if isinstance(self, Multiple) else ()
        return Result(operation="", status=Status(self.status), diagnostics=list(self._diagnostics()),
                      candidates=[_wire(v) for v in values])

    def __str__(self) -> str:
        if isinstance(self, Unique):
            return str(self.value)
        return _describe(self.status, self._diagnostics())


def _wire(value: Any) -> Any:
    if hasattr(value, "to_json"):
        return value.to_json()
    if isinstance(value, enum.Enum):
        return value.value
    return value


@dataclass(frozen=True, slots=True)
class Unique(_Case[T_co]):
    """Exactly one answer."""

    value: T_co
    diagnostics: tuple[Diagnostic, ...] = ()
    _result: Optional[Result] = field(default=None, compare=False, repr=False)
    __match_args__ = ("value",)
    status: ClassVar[str] = "unique"


@dataclass(frozen=True, slots=True)
class Multiple(_Case[T_co]):
    """Several answers, in ascending order (both instants of a DST overlap)."""

    values: tuple[T_co, ...]
    diagnostics: tuple[Diagnostic, ...] = ()
    _result: Optional[Result] = field(default=None, compare=False, repr=False)
    __match_args__ = ("values",)
    status: ClassVar[str] = "multiple"


@dataclass(frozen=True, slots=True)
class NoneOf(_Case[T_co]):
    """Proven: no answer exists (no 31 February, a wall time in a DST gap)."""

    diagnostics: tuple[Diagnostic, ...] = ()
    _result: Optional[Result] = field(default=None, compare=False, repr=False)
    __match_args__ = ("diagnostics",)
    status: ClassVar[str] = "none"


@dataclass(frozen=True, slots=True)
class Unsupported(_Case[T_co]):
    """This library lacks the rules or the capability to answer."""

    diagnostics: tuple[Diagnostic, ...] = ()
    _result: Optional[Result] = field(default=None, compare=False, repr=False)
    __match_args__ = ("diagnostics",)
    status: ClassVar[str] = "unsupported"


@dataclass(frozen=True, slots=True)
class Invalid(_Case[T_co]):
    """The input contradicts itself or the rules."""

    diagnostics: tuple[Diagnostic, ...] = ()
    _result: Optional[Result] = field(default=None, compare=False, repr=False)
    __match_args__ = ("diagnostics",)
    status: ClassVar[str] = "invalid"


Outcome = Union[Unique[T], Multiple[T], NoneOf[T], Unsupported[T], Invalid[T]]
"""The answer of a partial operation: one of the five cases above."""


def failure(status: Union[Status, str], diagnostics: Iterable[Diagnostic], result: Optional[Result] = None) -> Outcome[Any]:
    """The case for a non-answer status. Statuses the typed layer has no case for
    (``symbolic``, ``resource-limit``, …) become ``Unsupported``."""
    diags = tuple(diagnostics)
    status = Status(status)
    if status is Status.NONE:
        return NoneOf(diags, result)
    if status is Status.INVALID:
        return Invalid(diags, result)
    return Unsupported(diags, result)


def fail(status: Union[Status, str], code: str, message: str, path: str = "") -> Outcome[Any]:
    """A non-answer with one diagnostic."""
    severity = "info" if Status(status) is Status.NONE else "error"
    return failure(status, [Diagnostic(code, severity, path, message)])


def outcome_of(result: Result, convert: Callable[[Any], T]) -> Outcome[T]:
    """Bridge from the JSON layer: a ``Result`` as an outcome, its candidates converted."""
    diags = tuple(result.diagnostics)
    try:
        if result.status is Status.UNIQUE:
            return Unique(convert(result.candidates[0]), diags, result)
        if result.status is Status.MULTIPLE:
            return Multiple(tuple(convert(c) for c in result.candidates), diags, result)
    except OutcomeError as err:
        return failure(err.status, diags + err.diagnostics)
    return failure(result.status, diags, result)


class Truth(str, enum.Enum):
    """A three-valued answer: ``contains`` and ``overlaps`` on intervals with unknown ends."""

    DEFINITELY = "definitely"
    POSSIBLY = "possibly"
    IMPOSSIBLE = "impossible"

    def __str__(self) -> str:
        return self.value


class Ordering(str, enum.Enum):
    """Where ``a`` is relative to ``b``."""

    BEFORE = "before"
    EQUAL = "equal"
    AFTER = "after"

    def __str__(self) -> str:
        return self.value


__all__ = [
    "Invalid", "Multiple", "NoneOf", "Ordering", "Outcome", "OutcomeError", "Truth", "Unique", "Unsupported",
]
