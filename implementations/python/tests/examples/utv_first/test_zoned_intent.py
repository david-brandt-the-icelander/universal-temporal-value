"""How to use ``ZonedIntent``, a wall time in a named zone that stays an intention until it is resolved against
explicit zone rules, compared with Python's ``datetime`` with a ``zoneinfo.ZoneInfo``.

UTV resolves against the pinned demo rules. The Python side reads this machine's tz database, so these tests need one
that knows the 2026–2027 rules of New York and Paris.
"""

import unittest
from datetime import datetime, timedelta, timezone
from zoneinfo import ZoneInfo

from utv import CivilDateTime, Gap, Intent, Multiple, NoneOf, Overlap, ResolutionPolicy, TemporalContext, from_native

from ..fixtures import demo_zone_rules


class TestResolve(unittest.TestCase):
    # BEGIN meeting-in-paris
    def test_returns_the_instant_when_the_wall_time_happens_once(self):
        """Verifies that ZonedIntent.resolve returns the one instant when the wall time happens exactly once.

        Compared with: ``datetime.astimezone(timezone.utc)`` on the same wall time with ``ZoneInfo("Europe/Paris")``
        finds the same instant in this machine's tz database.
        """
        # SETUP: the pinned demo zone rules, never this machine's tz database
        env_context = TemporalContext(demo_zone_rules())

        # SYSTEM UNDER TEST: a meeting at 09:00 on 1 June 2027 in Paris
        sut = CivilDateTime.of(2027, 6, 1, 9, 0).in_zone("Europe/Paris")

        # WHEN
        actual_outcome = sut.resolve(env_context)

        # EXPECTATIONS: the instant Python finds for the same wall time
        tmp_utc = datetime(2027, 6, 1, 9, 0, tzinfo=ZoneInfo("Europe/Paris")).astimezone(timezone.utc)
        expected_instant = from_native(tmp_utc, Intent.INSTANT).single()

        # THEN
        self.assertEqual(expected_instant, actual_outcome.single())
    # END meeting-in-paris

    # BEGIN dst-gap
    def test_returns_none_when_the_wall_time_falls_in_a_gap(self):
        """Verifies that ZonedIntent.resolve returns none when the wall time falls in a daylight-saving gap.

        Compared with: ``datetime(2026, 3, 8, 2, 30, tzinfo=ZoneInfo("America/New_York"))`` is accepted without a
        word, at offset -05:00, although that wall time never happened.
        """
        # SETUP: the pinned demo zone rules, never this machine's tz database
        env_context = TemporalContext(demo_zone_rules())

        # SYSTEM UNDER TEST: 02:30 on 8 March 2026 in New York, when clocks jump from 02:00 to 03:00
        sut = CivilDateTime.of(2026, 3, 8, 2, 30).in_zone("America/New_York")

        # WHEN
        actual_outcome = sut.resolve(env_context)

        # EXPECTATIONS: no such instant, explained by the gap diagnostic
        expected_outcome_type = NoneOf
        expected_code = "gap"

        # THEN
        self.assertIsInstance(actual_outcome, expected_outcome_type)
        self.assertIn(expected_code, actual_outcome.codes)

    def test_shifts_forward_by_the_gap_when_shift_forward_is_requested(self):
        """Verifies that ZonedIntent.resolve with Gap.SHIFT_FORWARD moves the wall time forward by the length of the
        gap.

        Compared with: Python lands on the same instant, 03:30 daylight time, when the wall time it accepted silently
        is converted to UTC; UTV gives it only when asked.
        """
        # GIVEN: the policy that moves a skipped wall time forward by the gap
        given_policy = ResolutionPolicy(gap=Gap.SHIFT_FORWARD)

        # SETUP: the pinned demo zone rules, never this machine's tz database
        env_context = TemporalContext(demo_zone_rules())

        # SYSTEM UNDER TEST: 02:30 on 8 March 2026 in New York, when clocks jump from 02:00 to 03:00
        sut = CivilDateTime.of(2026, 3, 8, 2, 30).in_zone("America/New_York")

        # WHEN
        actual_outcome = sut.resolve(env_context, given_policy)

        # EXPECTATIONS: the instant Python silently gives the same wall time
        tmp_utc = datetime(2026, 3, 8, 2, 30, tzinfo=ZoneInfo("America/New_York")).astimezone(timezone.utc)
        expected_instant = from_native(tmp_utc, Intent.INSTANT).single()

        # THEN
        self.assertEqual(expected_instant, actual_outcome.single())
    # END dst-gap

    # BEGIN dst-overlap
    def test_returns_both_instants_when_the_wall_time_falls_in_an_overlap(self):
        """Verifies that ZonedIntent.resolve returns both instants when the wall time happens twice.

        Compared with: ``datetime(2026, 11, 1, 1, 30, tzinfo=ZoneInfo("America/New_York"))`` silently picks one of
        them through ``fold=0``, the daylight-time one.
        """
        # SETUP: the pinned demo zone rules, never this machine's tz database
        env_context = TemporalContext(demo_zone_rules())

        # SYSTEM UNDER TEST: 01:30 on 1 November 2026 in New York, when clocks go back from 02:00 to 01:00
        sut = CivilDateTime.of(2026, 11, 1, 1, 30).in_zone("America/New_York")

        # WHEN
        actual_outcome = sut.resolve(env_context)

        # EXPECTATIONS: 01:30 daylight time, then 01:30 standard time; Python's single answer is one of them
        expected_outcome_type = Multiple
        expected_instants = (
            from_native(datetime(2026, 11, 1, 1, 30, tzinfo=timezone(timedelta(hours=-4))), Intent.INSTANT).single(),
            from_native(datetime(2026, 11, 1, 1, 30, tzinfo=timezone(timedelta(hours=-5))), Intent.INSTANT).single(),
        )
        expected_code = "ambiguous"
        tmp_utc = datetime(2026, 11, 1, 1, 30, tzinfo=ZoneInfo("America/New_York")).astimezone(timezone.utc)
        expected_native_pick = from_native(tmp_utc, Intent.INSTANT).single()

        # THEN
        self.assertIsInstance(actual_outcome, expected_outcome_type)
        self.assertEqual(expected_instants, actual_outcome.values)
        self.assertIn(expected_code, actual_outcome.codes)
        self.assertIn(expected_native_pick, actual_outcome.values)

    def test_returns_the_earlier_instant_when_earliest_is_requested(self):
        """Verifies that ZonedIntent.resolve with Overlap.EARLIEST returns the first of the two instants.

        Compared with: Python's default ``fold=0`` picks the same instant, and does not say there was a choice.
        """
        # GIVEN: the policy that picks the earlier of two instants
        given_policy = ResolutionPolicy(overlap=Overlap.EARLIEST)

        # SETUP: the pinned demo zone rules, never this machine's tz database
        env_context = TemporalContext(demo_zone_rules())

        # SYSTEM UNDER TEST: 01:30 on 1 November 2026 in New York, when clocks go back from 02:00 to 01:00
        sut = CivilDateTime.of(2026, 11, 1, 1, 30).in_zone("America/New_York")

        # WHEN
        actual_outcome = sut.resolve(env_context, given_policy)

        # EXPECTATIONS: 01:30 daylight time, the instant Python picks with fold=0
        tmp_utc = datetime(2026, 11, 1, 1, 30, fold=0, tzinfo=ZoneInfo("America/New_York")).astimezone(timezone.utc)
        expected_instant = from_native(tmp_utc, Intent.INSTANT).single()

        # THEN
        self.assertEqual(expected_instant, actual_outcome.single())
    # END dst-overlap


class TestAddDays(unittest.TestCase):
    # BEGIN day-vs-24h
    def test_keeps_the_wall_time_when_the_day_crosses_a_dst_change(self):
        """Verifies that ZonedIntent.add_days keeps the wall time when the day crosses a daylight-saving change.

        Compared with: ``datetime + timedelta(days=1)`` with a ``ZoneInfo`` keeps noon too; with a fixed
        ``timezone`` it keeps the offset instead, and the wall time moves to 13:00.
        """
        # GIVEN: one calendar day
        given_days = 1

        # SYSTEM UNDER TEST: noon on 7 March 2026 in New York, the day before clocks spring forward
        sut = CivilDateTime.of(2026, 3, 7, 12, 0).in_zone("America/New_York")

        # WHEN
        actual_next_noon = sut.add_days(given_days)

        # EXPECTATIONS: the wall time and zone Python's zoned addition gives
        tmp_native = datetime(2026, 3, 7, 12, 0, tzinfo=ZoneInfo("America/New_York")) + timedelta(days=1)
        tmp_wall_time = CivilDateTime.of(
            tmp_native.year, tmp_native.month, tmp_native.day, tmp_native.hour, tmp_native.minute
        )
        expected_next_noon = tmp_wall_time.in_zone(tmp_native.tzinfo.key)

        # THEN
        self.assertEqual(expected_next_noon, actual_next_noon)
    # END day-vs-24h
