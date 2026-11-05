"""Typed policies (docs/library-api.md §4). Each also accepts its wire string,
through a ``Literal`` type: ``MonthEnd.CONSTRAIN`` or ``"constrain"``."""

from __future__ import annotations

import enum
from dataclasses import dataclass
from typing import ClassVar, Literal, Union


class MonthEnd(str, enum.Enum):
    """What month arithmetic does when the day doesn't exist in the target month."""

    REJECT = "reject"  # answer `none` (month-end)
    CONSTRAIN = "constrain"  # clamp to the last day of the month

    def __str__(self) -> str:
        return self.value


class Gap(str, enum.Enum):
    """What resolving does with a wall time in a DST gap (it never happened)."""

    REJECT = "reject"
    PRESERVE_UNRESOLVED = "preserve-unresolved"
    SHIFT_FORWARD = "shift-forward-by-gap"

    def __str__(self) -> str:
        return self.value


class Overlap(str, enum.Enum):
    """What resolving does with a wall time in a DST overlap (it happened twice)."""

    REJECT = "reject"
    ALL = "all"
    EARLIEST = "earliest"
    LATEST = "latest"

    def __str__(self) -> str:
        return self.value


MonthEndLike = Union[MonthEnd, Literal["reject", "constrain"]]
GapLike = Union[Gap, Literal["reject", "preserve-unresolved", "shift-forward-by-gap"]]
OverlapLike = Union[Overlap, Literal["reject", "all", "earliest", "latest"]]


@dataclass(frozen=True, init=False)
class ResolutionPolicy:
    """How to resolve a wall time in a gap or an overlap.

    ``ResolutionPolicy.STRICT`` (the default) rejects both; ``ALL_CANDIDATES`` keeps
    every candidate. ``ResolutionPolicy(gap=Gap.SHIFT_FORWARD)`` changes one side.
    """

    gap: Gap
    overlap: Overlap
    STRICT: ClassVar[ResolutionPolicy]
    ALL_CANDIDATES: ClassVar[ResolutionPolicy]

    def __init__(self, gap: GapLike = Gap.REJECT, overlap: OverlapLike = Overlap.REJECT) -> None:
        object.__setattr__(self, "gap", Gap(gap))
        object.__setattr__(self, "overlap", Overlap(overlap))

    def to_json(self) -> dict[str, str]:
        return {"gap": self.gap.value, "overlap": self.overlap.value}


ResolutionPolicy.STRICT = ResolutionPolicy(Gap.REJECT, Overlap.REJECT)
ResolutionPolicy.ALL_CANDIDATES = ResolutionPolicy(Gap.PRESERVE_UNRESOLVED, Overlap.ALL)

__all__ = ["Gap", "MonthEnd", "Overlap", "ResolutionPolicy"]
