"""What Python's ``datetime.time`` does, compared with UTV's ``CivilTime``."""

import unittest
from datetime import time, timedelta

from utv import CivilTime


class TestIsoformat(unittest.TestCase):
    def test_writes_seconds_when_no_seconds_were_given(self):
        """Verifies that time.isoformat writes seconds even when none were given.

        Compared with: ``CivilTime.of(9, 30)`` remembers that no seconds were given, and writes "09:30".
        """
        # SYSTEM UNDER TEST: half past nine, to the minute
        sut = time(9, 30)

        # WHEN
        actual_text = sut.isoformat()

        # EXPECTATIONS: UTV's text for the same time, padded with zero seconds
        expected_text = str(CivilTime.of(9, 30)) + ":00"

        # THEN
        self.assertEqual(expected_text, actual_text)


class TestAdd(unittest.TestCase):
    def test_raises_type_error_when_given_a_timedelta(self):
        """Verifies that adding a timedelta to a time raises a TypeError, because time has no arithmetic.

        Compared with: ``CivilTime`` has no arithmetic either; ``CivilDateTime.add_hours`` carries 23:30 + 2 hours into
        the next day and keeps the date.
        """
        # GIVEN: two hours, past midnight
        given_hours = timedelta(hours=2)

        # SYSTEM UNDER TEST: 23:30
        sut = time(23, 30)

        # WHEN
        with self.assertRaises(TypeError) as actual_raised:
            sut + given_hours

        # EXPECTATIONS
        expected_exception_type = TypeError

        # THEN
        self.assertIs(expected_exception_type, type(actual_raised.exception))
