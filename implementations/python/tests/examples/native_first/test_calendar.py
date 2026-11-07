"""What Python's ``calendar`` module does, compared with UTV's ``CivilDate``. Where the two agree, the expected value
is computed with UTV."""

import calendar
import unittest

from utv import CivilDate


class TestIsleap(unittest.TestCase):
    def test_agrees_with_utv_when_given_any_kind_of_year(self):
        """Verifies that calendar.isleap follows the Gregorian leap-year rules for every kind of year.

        Compared with: ``CivilDate.is_leap_year`` gives the same answers.
        """
        # GIVEN: one year of each kind
        given_years = (
            2024,  # divisible by 4: leap
            2026,  # not divisible by 4: common
            1900,  # divisible by 100 but not 400: common
            2000,  # divisible by 400: leap
        )
        for given_year in given_years:
            with self.subTest(year=given_year):
                # WHEN
                actual_is_leap_year = calendar.isleap(given_year)

                # EXPECTATIONS: UTV's answer for the same year
                expected_is_leap_year = CivilDate.of(given_year, 1, 1).is_leap_year

                # THEN
                self.assertEqual(expected_is_leap_year, actual_is_leap_year)


class TestMonthrange(unittest.TestCase):
    def test_returns_28_days_when_the_month_is_february_of_a_common_year(self):
        """Verifies that calendar.monthrange returns the number of days in the month when given February 2026.

        Compared with: ``CivilDate.days_in_month`` gives the same count, and ``CivilDate.add_months(1,
        MonthEnd.CONSTRAIN)`` clamps to it, so nobody clamps 31 January by hand.
        """
        # GIVEN: February 2026
        given_year = 2026
        given_month = 2

        # WHEN
        actual_days = calendar.monthrange(given_year, given_month)[1]

        # EXPECTATIONS: the length UTV gives the same month
        expected_days = CivilDate.of(2026, 2, 1).days_in_month

        # THEN
        self.assertEqual(expected_days, actual_days)
