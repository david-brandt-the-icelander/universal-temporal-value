"""``TemporalContext``: the zone rules, held once (docs/library-api.md §4).

There is no default or global context, and nothing reads the host's tz database::

    ctx = TemporalContext(ZoneRules.load("conformance/rules/demo-zones.json"))
    meeting.resolve(ctx)                      # same as ctx.resolve(meeting)
"""

from __future__ import annotations

from dataclasses import dataclass
from typing import Any, Union, cast

from .outcome import Multiple, Ordering, Outcome, Unique, failure, outcome_of
from .policy import ResolutionPolicy
from .zones import ZoneRules, resolve, to_zone
from .values import FixedOffsetDateTime, Instant, ZonedIntent, _typed

Timed = Union[ZonedIntent, Instant, FixedOffsetDateTime]
_ORDER = (Ordering.BEFORE, Ordering.EQUAL, Ordering.AFTER)


@dataclass(frozen=True)
class TemporalContext:
    """The zone rules every zone operation uses, named once by the caller."""

    rules: ZoneRules

    def __post_init__(self) -> None:
        if not isinstance(self.rules, ZoneRules):
            raise TypeError("TemporalContext takes ZoneRules (ZoneRules.load(path) or ZoneRules.parse(text))")

    @classmethod
    def with_rules(cls, rules: ZoneRules) -> TemporalContext:
        return cls(rules)

    def resolve(self, zoned: ZonedIntent, policy: ResolutionPolicy = ResolutionPolicy.STRICT) -> Outcome[Instant]:
        """The instant(s) a zoned wall time names under these rules.

        ``NoneOf`` (``gap``) for a wall time that never happened, ``Multiple``
        (``ambiguous``) for one that happened twice, unless ``policy`` picks;
        ``Invalid`` (``offset-mismatch``) if an asserted offset contradicts the rules;
        ``Unsupported`` for a zone the rules don't know or a date they don't cover.
        """
        if not isinstance(zoned, ZonedIntent):
            raise TypeError("resolve takes a ZonedIntent (a CivilDateTime has no zone to resolve against)")
        return outcome_of(resolve(zoned.to_json(), self.rules, policy.to_json()), _typed)

    def in_zone(self, instant: Instant, zone_id: str) -> Outcome[ZonedIntent]:
        """The wall time in ``zone_id`` at ``instant``, with the offset in force asserted."""
        if not isinstance(instant, Instant):
            raise TypeError("in_zone takes an Instant")
        return outcome_of(to_zone(instant.to_json(), zone_id, self.rules), _typed)

    def _instants(self, value: Timed) -> Outcome[Instant]:
        if isinstance(value, Instant):
            return Unique(value)
        if isinstance(value, FixedOffsetDateTime):
            return Unique(value.to_instant())
        if isinstance(value, ZonedIntent):
            return self.resolve(value)
        raise TypeError("compare takes ZonedIntent, Instant or FixedOffsetDateTime values")

    def compare(self, a: Timed, b: Timed) -> Outcome[Ordering]:
        """Where ``a`` is relative to ``b``, resolving zoned values strictly first.

        ``Multiple`` lists every possible order when it depends on which side of a DST
        overlap is meant; a gap or an unknown zone passes through as ``NoneOf`` / ``Unsupported``.
        """
        oa, ob = self._instants(a), self._instants(b)
        diagnostics = oa.diagnostics + ob.diagnostics
        for o in (oa, ob):
            if not isinstance(o, (Unique, Multiple)):
                return failure(o.status, diagnostics)
        found = {_relation(x, y) for x in _values(oa) for y in _values(ob)}
        orders = tuple(o for o in _ORDER if o in found)
        if len(orders) == 1:
            return Unique(orders[0], diagnostics)
        return Multiple(orders, diagnostics)


def _values(outcome: Outcome[Instant]) -> tuple[Instant, ...]:
    if isinstance(outcome, Unique):
        return (outcome.value,)
    return cast(Any, outcome).values  # type: ignore[no-any-return]


def _relation(a: Instant, b: Instant) -> Ordering:
    return Ordering.BEFORE if a < b else Ordering.AFTER if a > b else Ordering.EQUAL


__all__ = ["TemporalContext"]
