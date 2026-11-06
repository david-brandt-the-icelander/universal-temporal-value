"""What Python's ``datetime.timedelta`` does, compared with UTV's three kinds of amount: ``Period``,
``PosixSeconds`` and ``ElapsedSeconds``."""

import unittest
from datetime import timedelta

from utv import ElapsedSeconds, PosixSeconds, to_native


class TestConstructor(unittest.TestCase):
    def test_equals_86400_seconds_when_given_one_day(self):
        """Verifies that timedelta(days=1) equals 86,400 seconds.

        Compared with: UTV keeps ``Period.of_days(1)``, ``PosixSeconds.of(86_400)`` and ``ElapsedSeconds.of(86_400)``
        apart, because a calendar day can be 23 or 25 hours long.
        """
        # GIVEN: one day
        given_days = 1

        # WHEN
        actual_day = timedelta(days=given_days)

        # EXPECTATIONS: the timedelta UTV converts 86,400 POSIX seconds into
        expected_day = to_native(PosixSeconds.of(86_400)).single()

        # THEN
        self.assertEqual(expected_day, actual_day)


class TestTotalSeconds(unittest.TestCase):
    def test_counts_sixty_seconds_a_minute_when_given_ninety_minutes(self):
        """Verifies that timedelta.total_seconds counts 60 seconds to the minute.

        Compared with: ``ElapsedSeconds.of(5_400)`` holds the same number of seconds as an exact SI amount.
        """
        # SYSTEM UNDER TEST: 90 minutes
        sut = timedelta(minutes=90)

        # WHEN
        actual_seconds = sut.total_seconds()

        # EXPECTATIONS: the seconds UTV holds for the same duration
        expected_seconds = float(ElapsedSeconds.of(5_400).seconds)

        # THEN
        self.assertEqual(expected_seconds, actual_seconds)
