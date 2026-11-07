"""How to use ``TemporalContext``, which holds the zone rules that zoned operations need, compared with Python, where
every ``ZoneInfo`` reads this machine's tz database."""

import unittest

from utv import CivilDateTime, Multiple, TemporalContext

from ..fixtures import demo_zone_rules


class TestCompare(unittest.TestCase):
    # BEGIN compare-dates
    def test_returns_multiple_when_both_wall_times_fall_in_an_overlap(self):
        """Verifies that TemporalContext.compare returns both orders when both wall times fall in the same overlap.

        Compared with: two ``datetime`` values with the same ``ZoneInfo`` compare by wall time, ignoring ``fold``, and
        always say 01:30 comes first.
        """
        # GIVEN: a call at 01:30 and a stand-up at 01:45, on the night New York's clocks go back from 02:00 to 01:00
        given_call = CivilDateTime.of(2026, 11, 1, 1, 30).in_zone("America/New_York")
        given_standup = CivilDateTime.of(2026, 11, 1, 1, 45).in_zone("America/New_York")

        # SETUP: the pinned demo zone rules, never this machine's tz database
        env_rules = demo_zone_rules()

        # SYSTEM UNDER TEST
        sut = TemporalContext(env_rules)

        # WHEN
        actual_outcome = sut.compare(given_call, given_standup)

        # EXPECTATIONS: either could come first (01:30 standard time is after 01:45 daylight time)
        expected_outcome_type = Multiple
        expected_code = "ambiguous"

        # THEN
        self.assertIsInstance(actual_outcome, expected_outcome_type)
        self.assertIn(expected_code, actual_outcome.codes)
    # END compare-dates
