"""How to use ``CivilDateTime``, a wall-clock date and time with no zone, compared with Python's naive
``datetime.datetime`` (which ``astimezone()`` and ``timestamp()`` quietly read as this machine's local time)."""

import unittest
from datetime import datetime, timedelta, timezone

from utv import CivilDateTime, Unsupported, to_native


class TestOf(unittest.TestCase):
    def test_creates_a_wall_clock_date_time_when_given_a_date_and_a_time(self):
        """Verifies that CivilDateTime.of creates a wall-clock date-time with no zone when given a date and a time.

        Compared with: ``datetime(2026, 11, 5, 14, 30)`` is the same wall time, naive (its ``tzinfo`` is None).
        """
        # GIVEN: 14:30 on 5 November 2026, in no particular zone
        given_year = 2026
        given_month = 11
        given_day = 5
        given_hour = 14
        given_minute = 30

        # WHEN
        actual_date_time = CivilDateTime.of(given_year, given_month, given_day, given_hour, given_minute)

        # EXPECTATIONS: the wall time Python holds, written without an offset or a zone
        expected_date_time = datetime(2026, 11, 5, 14, 30)
        expected_text = "2026-11-05T14:30"

        # THEN
        self.assertEqual(expected_date_time, to_native(actual_date_time).single())
        self.assertEqual(expected_text, str(actual_date_time))


class TestTryOf(unittest.TestCase):
    def test_returns_unsupported_when_the_second_is_a_leap_second(self):
        """Verifies that CivilDateTime.try_of returns unsupported when the second is a leap second, 60.

        Compared with: ``datetime(2016, 12, 31, 23, 59, 60)`` raises a ValueError, as if the second could never exist.
        """
        # GIVEN: 23:59:60 on 31 December 2016, a real leap second
        given_year = 2016
        given_month = 12
        given_day = 31
        given_hour = 23
        given_minute = 59
        given_second = 60

        # WHEN
        actual_outcome = CivilDateTime.try_of(given_year, given_month, given_day, given_hour, given_minute, given_second)

        # EXPECTATIONS: unsupported, not invalid: judging it needs a leap-second table
        expected_outcome_type = Unsupported
        expected_code = "leap-second"

        # THEN
        self.assertIsInstance(actual_outcome, expected_outcome_type)
        self.assertIn(expected_code, actual_outcome.codes)


class TestAddHours(unittest.TestCase):
    # BEGIN datetime-plus-hours
    def test_carries_into_the_next_day_when_the_hours_pass_midnight(self):
        """Verifies that CivilDateTime.add_hours carries into the next day when the hours pass midnight.

        Compared with: ``datetime + timedelta(hours=2)`` gives the same wall time; ``time`` has no arithmetic.
        """
        # GIVEN: two hours, past midnight
        given_hours = 2

        # SYSTEM UNDER TEST: 23:30 on 5 November 2026
        sut = CivilDateTime.of(2026, 11, 5, 23, 30)

        # WHEN
        actual_date_time = sut.add_hours(given_hours)

        # EXPECTATIONS: the wall time Python computes for the same addition
        expected_date_time = datetime(2026, 11, 5, 23, 30) + timedelta(hours=2)

        # THEN
        self.assertEqual(expected_date_time, to_native(actual_date_time).single())
    # END datetime-plus-hours


class TestInZone(unittest.TestCase):
    def test_keeps_the_wall_time_and_the_zone_when_given_a_zone_id(self):
        """Verifies that CivilDateTime.in_zone keeps the wall time and the zone name as an intention.

        Compared with: ``datetime(2027, 6, 1, 9, 0, tzinfo=ZoneInfo("Europe/Paris"))`` gets its offset from this
        machine's tz database the moment anything asks for it.
        """
        # GIVEN: the Paris time zone
        given_zone_id = "Europe/Paris"

        # SYSTEM UNDER TEST: 09:00 on 1 June 2027
        sut = CivilDateTime.of(2027, 6, 1, 9, 0)

        # WHEN
        actual_meeting = sut.in_zone(given_zone_id)

        # EXPECTATIONS: the wall time and the zone, with no offset decided yet
        expected_text = "2027-06-01T09:00[Europe/Paris]"

        # THEN
        self.assertEqual(expected_text, str(actual_meeting))


class TestAtOffset(unittest.TestCase):
    def test_pins_the_wall_time_to_the_offset_when_given_offset_seconds(self):
        """Verifies that CivilDateTime.at_offset pins the wall time to a fixed UTC offset.

        Compared with: ``datetime(2026, 11, 5, 14, 30, tzinfo=timezone(timedelta(hours=1)))`` is the same value.
        """
        # GIVEN: one hour east of UTC
        given_offset_seconds = 3600

        # SYSTEM UNDER TEST: 14:30 on 5 November 2026
        sut = CivilDateTime.of(2026, 11, 5, 14, 30)

        # WHEN
        actual_date_time = sut.at_offset(given_offset_seconds)

        # EXPECTATIONS: the value Python's aware datetime holds, offset included
        expected_date_time = datetime(2026, 11, 5, 14, 30, tzinfo=timezone(timedelta(hours=1)))

        # THEN
        self.assertEqual(expected_date_time, to_native(actual_date_time).single())
        self.assertEqual(expected_date_time.utcoffset(), to_native(actual_date_time).single().utcoffset())
