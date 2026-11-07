"""How to use ``ElapsedSeconds``, SI seconds as a stopwatch measures them, compared with Python's
``datetime.timedelta``, which has one kind of second for every purpose."""

import unittest
from datetime import timedelta

from utv import ElapsedSeconds, Period, PosixSeconds


class TestOf(unittest.TestCase):
    def test_holds_the_seconds_when_given_ninety_minutes(self):
        """Verifies that ElapsedSeconds.of holds exactly the elapsed seconds it is given.

        Compared with: ``timedelta(minutes=90).total_seconds()`` is the same number, as a float.
        """
        # GIVEN: 90 minutes of elapsed time, in seconds
        given_seconds = 5_400

        # WHEN
        actual_amount = ElapsedSeconds.of(given_seconds)

        # EXPECTATIONS: the seconds Python counts in 90 minutes, labelled as SI seconds
        expected_seconds = timedelta(minutes=90).total_seconds()
        expected_text = "5400 s (SI)"

        # THEN
        self.assertEqual(expected_seconds, float(actual_amount.seconds))
        self.assertEqual(expected_text, str(actual_amount))

    def test_is_not_a_calendar_day_when_given_86400_seconds(self):
        """Verifies that ElapsedSeconds.of creates an amount that is neither a calendar day nor POSIX seconds when
        given 86,400 seconds.

        Compared with: ``timedelta(days=1)`` is all three at once.
        """
        # GIVEN: exactly 86,400 SI seconds
        given_seconds = 86_400

        # WHEN
        actual_amount = ElapsedSeconds.of(given_seconds)

        # EXPECTATIONS: different from a calendar day and from 86,400 POSIX seconds
        expected_calendar_day = Period.of_days(1)
        expected_posix_day = PosixSeconds.of(86_400)

        # THEN
        self.assertNotEqual(expected_calendar_day, actual_amount)
        self.assertNotEqual(expected_posix_day, actual_amount)
