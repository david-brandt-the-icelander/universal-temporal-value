"""What Python's ``zoneinfo.ZoneInfo`` does at a daylight-saving change, compared with UTV's ``ZonedIntent.resolve``.

``ZoneInfo`` reads this machine's tz database, so these tests need one that knows the 2026 rules of New York. UTV
reads only the pinned demo rules it is given.
"""

import unittest
from datetime import datetime, timedelta
from zoneinfo import ZoneInfo

from utv import CivilDateTime, Overlap, ResolutionPolicy, TemporalContext

from ..fixtures import demo_zone_rules


class TestUtcoffset(unittest.TestCase):
    def test_returns_the_standard_offset_silently_when_the_wall_time_falls_in_a_gap(self):
        """Verifies that ZoneInfo.utcoffset returns the offset in force before the change, without a word, when the
        wall time falls in a daylight-saving gap.

        Compared with: ``ZonedIntent.resolve`` returns none (gap), because 02:30 never happened that night.
        """
        # GIVEN: 02:30 on 8 March 2026, when New York's clocks jump from 02:00 to 03:00
        given_wall_time = datetime(2026, 3, 8, 2, 30)

        # SYSTEM UNDER TEST: New York, from this machine's tz database
        sut = ZoneInfo("America/New_York")

        # WHEN
        actual_offset = sut.utcoffset(given_wall_time)

        # EXPECTATIONS: standard time, five hours behind UTC, for a wall time UTV says does not exist
        expected_offset = timedelta(hours=-5)

        # THEN
        self.assertEqual(expected_offset, actual_offset)

    def test_returns_the_daylight_offset_when_fold_is_0_in_an_overlap(self):
        """Verifies that ZoneInfo.utcoffset returns the daylight-time offset when the wall time happens twice and fold
        is 0, the default.

        Compared with: ``ZonedIntent.resolve`` returns both instants; this offset is the one of
        ``Overlap.EARLIEST``.
        """
        # GIVEN: 01:30 on 1 November 2026, when New York's clocks go back from 02:00 to 01:00, the first time round
        given_wall_time = datetime(2026, 11, 1, 1, 30, fold=0)

        # SYSTEM UNDER TEST: New York, from this machine's tz database
        sut = ZoneInfo("America/New_York")

        # WHEN
        actual_offset = sut.utcoffset(given_wall_time)

        # EXPECTATIONS: the offset of the instant UTV picks when asked for the earliest
        tmp_context = TemporalContext(demo_zone_rules())
        tmp_zoned = CivilDateTime.of(2026, 11, 1, 1, 30).in_zone("America/New_York")
        tmp_instant = tmp_zoned.resolve(tmp_context, ResolutionPolicy(overlap=Overlap.EARLIEST)).single()
        tmp_offset_seconds = tmp_instant.in_zone("America/New_York", tmp_context).single().asserted_offset_seconds
        expected_offset = timedelta(seconds=tmp_offset_seconds)

        # THEN
        self.assertEqual(expected_offset, actual_offset)

    def test_returns_the_standard_offset_when_fold_is_1_in_an_overlap(self):
        """Verifies that ZoneInfo.utcoffset returns the standard-time offset when the wall time happens twice and fold
        is 1.

        Compared with: ``ZonedIntent.resolve`` returns both instants; this offset is the one of ``Overlap.LATEST``.
        """
        # GIVEN: 01:30 on 1 November 2026, when New York's clocks go back from 02:00 to 01:00, the second time round
        given_wall_time = datetime(2026, 11, 1, 1, 30, fold=1)

        # SYSTEM UNDER TEST: New York, from this machine's tz database
        sut = ZoneInfo("America/New_York")

        # WHEN
        actual_offset = sut.utcoffset(given_wall_time)

        # EXPECTATIONS: the offset of the instant UTV picks when asked for the latest
        tmp_context = TemporalContext(demo_zone_rules())
        tmp_zoned = CivilDateTime.of(2026, 11, 1, 1, 30).in_zone("America/New_York")
        tmp_instant = tmp_zoned.resolve(tmp_context, ResolutionPolicy(overlap=Overlap.LATEST)).single()
        tmp_offset_seconds = tmp_instant.in_zone("America/New_York", tmp_context).single().asserted_offset_seconds
        expected_offset = timedelta(seconds=tmp_offset_seconds)

        # THEN
        self.assertEqual(expected_offset, actual_offset)
