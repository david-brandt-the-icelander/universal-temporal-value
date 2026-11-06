"""How to use ``CivilYearMonth``, a month of a Gregorian year with no day, compared with Python, which has no
year-month type and invents day 1 of the month with ``datetime.date``."""

import unittest
from datetime import date

from utv import CivilYearMonth


class TestOf(unittest.TestCase):
    def test_creates_a_month_without_a_day_when_given_a_year_and_a_month(self):
        """Verifies that CivilYearMonth.of creates a month without inventing a day when given a year and a month.

        Compared with: ``date(2026, 11, 1)`` has the same year and month, plus a day nobody asked for.
        """
        # GIVEN: November 2026
        given_year = 2026
        given_month = 11

        # WHEN
        actual_month = CivilYearMonth.of(given_year, given_month)

        # EXPECTATIONS: the year and month of Python's stand-in, and text with no day in it
        expected_stand_in = date(2026, 11, 1)
        expected_text = "2026-11"

        # THEN
        self.assertEqual(expected_stand_in.year, actual_month.year)
        self.assertEqual(expected_stand_in.month, actual_month.month)
        self.assertEqual(expected_text, str(actual_month))


class TestAddMonths(unittest.TestCase):
    def test_returns_the_later_month_when_the_months_cross_a_year_end(self):
        """Verifies that CivilYearMonth.add_months returns the later month, carrying into the next year.

        Compared with: ``datetime`` has no month arithmetic at all, so the year and month of the stand-in
        ``date(2026, 11, 1)`` are counted by hand.
        """
        # GIVEN: three months, past December
        given_months = 3

        # SYSTEM UNDER TEST: November 2026
        sut = CivilYearMonth.of(2026, 11)

        # WHEN
        actual_month = sut.add_months(given_months)

        # EXPECTATIONS: February 2027, which Python has no function to compute
        expected_month = CivilYearMonth.of(2027, 2)

        # THEN
        self.assertEqual(expected_month, actual_month)
