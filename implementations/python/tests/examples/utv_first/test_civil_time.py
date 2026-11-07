"""How to use ``CivilTime``, a wall-clock time of day with no date and no zone, compared with Python's
``datetime.time``."""

import unittest
from datetime import time

from utv import CivilTime, from_native


class TestOf(unittest.TestCase):
    # BEGIN time-of-day
    def test_writes_no_seconds_when_no_seconds_are_given(self):
        """Verifies that CivilTime.of keeps the time to the minute when no seconds are given.

        Compared with: ``time(9, 30)`` always has seconds, and ``isoformat()`` writes them: ``09:30:00``.
        """
        # GIVEN: half past nine, to the minute
        given_hour = 9
        given_minute = 30

        # WHEN
        actual_time = CivilTime.of(given_hour, given_minute)

        # EXPECTATIONS: the same hour and minute as Python, and no seconds in the text
        expected_time = time(9, 30)
        expected_text = "09:30"

        # THEN
        self.assertEqual(expected_time.hour, actual_time.hour)
        self.assertEqual(expected_time.minute, actual_time.minute)
        self.assertEqual(expected_text, str(actual_time))
    # END time-of-day

    def test_writes_the_seconds_when_seconds_are_given(self):
        """Verifies that CivilTime.of keeps the seconds when they are given.

        Compared with: ``time(9, 30, 0)`` is the same time; UTV also remembers that a second was given.
        """
        # GIVEN: half past nine, to the second
        given_hour = 9
        given_minute = 30
        given_second = 0

        # WHEN
        actual_time = CivilTime.of(given_hour, given_minute, given_second)

        # EXPECTATIONS: the time Python holds, with its seconds written out
        expected_time = from_native(time(9, 30, 0)).single()
        expected_text = "09:30:00"

        # THEN
        self.assertEqual(expected_time, actual_time)
        self.assertEqual(expected_text, str(actual_time))
