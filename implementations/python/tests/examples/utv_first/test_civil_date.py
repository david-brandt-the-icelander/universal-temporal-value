"""How to use ``CivilDate``, a day in the Gregorian calendar with no time and no zone, compared with Python's
``datetime.date``. Where the two agree, the expected value is computed with ``datetime.date``."""

import calendar
import unittest
from datetime import date, timedelta

from utv import CivilDate, Invalid, MonthEnd, NoneOf, OutcomeError, Period, Weekday, from_native


class TestOf(unittest.TestCase):
    # BEGIN a-date
    def test_creates_the_date_when_the_fields_name_a_real_day(self):
        """Verifies that CivilDate.of creates the date when the fields name a real day.

        Compared with: ``date(2026, 11, 5)`` names the same day.
        """
        # GIVEN: 5 November 2026
        given_year = 2026
        given_month = 11
        given_day = 5

        # WHEN
        actual_date = CivilDate.of(given_year, given_month, given_day)

        # EXPECTATIONS: the same day as Python's date
        expected_date = from_native(date(2026, 11, 5)).single()
        expected_text = "2026-11-05"

        # THEN
        self.assertEqual(expected_date, actual_date)
        self.assertEqual(expected_text, str(actual_date))
    # END a-date

    def test_accepts_the_leap_day_when_the_year_is_a_leap_year(self):
        """Verifies that CivilDate.of accepts 29 February when the year is a leap year.

        Compared with: ``date(2024, 2, 29)`` accepts it too.
        """
        # GIVEN: 29 February 2024, a year divisible by 4
        given_year = 2024
        given_month = 2
        given_day = 29

        # WHEN
        actual_date = CivilDate.of(given_year, given_month, given_day)

        # EXPECTATIONS: the same day as Python's date
        expected_date = from_native(date(2024, 2, 29)).single()

        # THEN
        self.assertEqual(expected_date, actual_date)

    def test_accepts_the_leap_day_when_the_century_is_divisible_by_400(self):
        """Verifies that CivilDate.of accepts 29 February when the year is a century divisible by 400.

        Compared with: ``date(2000, 2, 29)`` accepts it too.
        """
        # GIVEN: 29 February 2000, a century year divisible by 400
        given_year = 2000
        given_month = 2
        given_day = 29

        # WHEN
        actual_date = CivilDate.of(given_year, given_month, given_day)

        # EXPECTATIONS: the same day as Python's date
        expected_date = from_native(date(2000, 2, 29)).single()

        # THEN
        self.assertEqual(expected_date, actual_date)

    def test_raises_value_error_when_the_day_does_not_exist(self):
        """Verifies that CivilDate.of raises a ValueError, with the reason, when the day does not exist in the month.

        Compared with: ``date(2026, 2, 30)`` raises a ValueError too; UTV's is an OutcomeError that carries the
        diagnostic.
        """
        # GIVEN: 30 February 2026
        given_year = 2026
        given_month = 2
        given_day = 30

        # WHEN
        with self.assertRaises(OutcomeError) as actual_raised:
            CivilDate.of(given_year, given_month, given_day)

        # EXPECTATIONS: the exception type Python's date raises for the same fields, and the reason
        expected_exception_type = ValueError
        expected_code = "day-out-of-range"

        # THEN
        self.assertIsInstance(actual_raised.exception, expected_exception_type)
        self.assertIn(expected_code, [d.code for d in actual_raised.exception.diagnostics])

    def test_creates_the_date_when_the_year_is_beyond_9999(self):
        """Verifies that CivilDate.of creates the date when the year is beyond 9999.

        Compared with: ``date`` ends at ``date.max``, 9999-12-31, and raises a ValueError for it.
        """
        # GIVEN: 13 September 275760, the last day a JavaScript Date can hold
        given_year = 275_760
        given_month = 9
        given_day = 13

        # WHEN
        actual_date = CivilDate.of(given_year, given_month, given_day)

        # EXPECTATIONS: an expanded year, written with a sign
        expected_text = "+275760-09-13"

        # THEN
        self.assertEqual(expected_text, str(actual_date))

    def test_creates_the_date_when_the_year_is_negative(self):
        """Verifies that CivilDate.of creates the date when the year is before year 1.

        Compared with: ``date`` starts at ``date.min``, 0001-01-01, and raises a ValueError for it.
        """
        # GIVEN: 1 March of year -500 in the proleptic Gregorian calendar
        given_year = -500
        given_month = 3
        given_day = 1

        # WHEN
        actual_date = CivilDate.of(given_year, given_month, given_day)

        # EXPECTATIONS: a signed, four-digit year
        expected_text = "-0500-03-01"

        # THEN
        self.assertEqual(expected_text, str(actual_date))


class TestTryOf(unittest.TestCase):
    # BEGIN feb-30
    def test_returns_invalid_when_the_day_is_30_february(self):
        """Verifies that CivilDate.try_of returns invalid when the day is 30 February.

        Compared with: ``date(2026, 2, 30)`` raises a ValueError; try_of reports the problem as a value instead.
        """
        # GIVEN: 30 February 2026
        given_year = 2026
        given_month = 2
        given_day = 30

        # WHEN
        actual_outcome = CivilDate.try_of(given_year, given_month, given_day)

        # EXPECTATIONS: invalid, because February 2026 has 28 days
        expected_outcome_type = Invalid
        expected_code = "day-out-of-range"

        # THEN
        self.assertIsInstance(actual_outcome, expected_outcome_type)
        self.assertIn(expected_code, actual_outcome.codes)
    # END feb-30

    def test_returns_invalid_when_29_february_falls_in_a_common_year(self):
        """Verifies that CivilDate.try_of returns invalid when 29 February falls in a common year.

        Compared with: ``calendar.isleap(2026)`` is False and ``date(2026, 2, 29)`` raises a ValueError.
        """
        # GIVEN: 29 February 2026, a year not divisible by 4
        given_year = 2026
        given_month = 2
        given_day = 29

        # WHEN
        actual_outcome = CivilDate.try_of(given_year, given_month, given_day)

        # EXPECTATIONS: invalid, rather than rolled over to 1 March
        expected_outcome_type = Invalid
        expected_code = "day-out-of-range"

        # THEN
        self.assertIsInstance(actual_outcome, expected_outcome_type)
        self.assertIn(expected_code, actual_outcome.codes)

    def test_returns_invalid_when_29_february_falls_in_a_century_not_divisible_by_400(self):
        """Verifies that CivilDate.try_of returns invalid when 29 February falls in a century not divisible by 400.

        Compared with: ``calendar.isleap(1900)`` is False too.
        """
        # GIVEN: 29 February 1900, divisible by 100 but not by 400
        given_year = 1900
        given_month = 2
        given_day = 29

        # WHEN
        actual_outcome = CivilDate.try_of(given_year, given_month, given_day)

        # EXPECTATIONS: invalid, because 1900 was not a leap year
        expected_outcome_type = Invalid
        expected_code = "day-out-of-range"

        # THEN
        self.assertIsInstance(actual_outcome, expected_outcome_type)
        self.assertIn(expected_code, actual_outcome.codes)

    def test_returns_invalid_when_the_month_is_13(self):
        """Verifies that CivilDate.try_of returns invalid when the month is 13.

        Compared with: ``date(2026, 13, 1)`` raises a ValueError.
        """
        # GIVEN: the first day of a thirteenth month
        given_year = 2026
        given_month = 13
        given_day = 1

        # WHEN
        actual_outcome = CivilDate.try_of(given_year, given_month, given_day)

        # EXPECTATIONS: invalid, because a Gregorian year has 12 months
        expected_outcome_type = Invalid
        expected_code = "month-out-of-range"

        # THEN
        self.assertIsInstance(actual_outcome, expected_outcome_type)
        self.assertIn(expected_code, actual_outcome.codes)


class TestParse(unittest.TestCase):
    def test_returns_the_date_when_the_text_is_an_iso_date(self):
        """Verifies that CivilDate.parse returns the date when the text is an ISO 8601 calendar date.

        Compared with: ``date.fromisoformat("2026-11-05")`` reads the same day.
        """
        # GIVEN: the ISO 8601 text of 5 November 2026
        given_text = "2026-11-05"

        # WHEN
        actual_outcome = CivilDate.parse(given_text)

        # EXPECTATIONS: the day Python parses from the same text
        expected_date = from_native(date.fromisoformat("2026-11-05")).single()

        # THEN
        self.assertEqual(expected_date, actual_outcome.single())

    def test_returns_invalid_when_the_text_names_a_day_that_does_not_exist(self):
        """Verifies that CivilDate.parse returns invalid, with the reason, when the text names a day that does not exist.

        Compared with: ``date.fromisoformat("2026-02-30")`` raises a ValueError, so bad input from outside needs a
        ``try`` around it.
        """
        # GIVEN: well-formed text for 30 February 2026
        given_text = "2026-02-30"

        # WHEN
        actual_outcome = CivilDate.parse(given_text)

        # EXPECTATIONS: invalid, and the diagnostic says which field is wrong
        expected_outcome_type = Invalid
        expected_code = "day-out-of-range"

        # THEN
        self.assertIsInstance(actual_outcome, expected_outcome_type)
        self.assertIn(expected_code, actual_outcome.codes)


class TestDayOfWeek(unittest.TestCase):
    # BEGIN weekday-week
    def test_returns_thursday_when_the_date_is_5_november_2026(self):
        """Verifies that CivilDate.day_of_week returns the weekday of the date, numbered as in ISO 8601.

        Compared with: ``date.isoweekday()`` numbers it the same, 4; ``date.weekday()`` would number it 3.
        """
        # SYSTEM UNDER TEST: 5 November 2026
        sut = CivilDate.of(2026, 11, 5)

        # WHEN
        actual_weekday = sut.day_of_week

        # EXPECTATIONS: the ISO weekday Python numbers for the same day
        expected_weekday = Weekday(date(2026, 11, 5).isoweekday())
        expected_name = "Thursday"

        # THEN
        self.assertEqual(expected_weekday, actual_weekday)
        self.assertEqual(expected_name, str(actual_weekday))
    # END weekday-week


class TestIsoWeek(unittest.TestCase):
    # BEGIN weekday-week
    def test_returns_45_when_the_date_is_5_november_2026(self):
        """Verifies that CivilDate.iso_week returns the ISO 8601 week number of the date.

        Compared with: ``date.isocalendar().week`` gives the same week.
        """
        # SYSTEM UNDER TEST: 5 November 2026
        sut = CivilDate.of(2026, 11, 5)

        # WHEN
        actual_week = sut.iso_week

        # EXPECTATIONS: the ISO week Python computes for the same day
        expected_week = date(2026, 11, 5).isocalendar().week

        # THEN
        self.assertEqual(expected_week, actual_week)
    # END weekday-week


class TestIsLeapYear(unittest.TestCase):
    def test_agrees_with_python_when_given_any_kind_of_year(self):
        """Verifies that CivilDate.is_leap_year follows the Gregorian leap-year rules for every kind of year.

        Compared with: ``calendar.isleap`` gives the same answers.
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
                # SYSTEM UNDER TEST: 1 January of the given year
                sut = CivilDate.of(given_year, 1, 1)

                # WHEN
                actual_is_leap_year = sut.is_leap_year

                # EXPECTATIONS: Python's answer for the same year
                expected_is_leap_year = calendar.isleap(given_year)

                # THEN
                self.assertEqual(expected_is_leap_year, actual_is_leap_year)


class TestAddDays(unittest.TestCase):
    # BEGIN add-days
    def test_returns_the_later_date_when_the_days_cross_a_month_end(self):
        """Verifies that CivilDate.add_days returns the date that many days later, carrying into the next month.

        Compared with: ``date + timedelta(days=30)`` gives the same date.
        """
        # GIVEN: 30 days, more than remain in November
        given_days = 30

        # SYSTEM UNDER TEST: 5 November 2026
        sut = CivilDate.of(2026, 11, 5)

        # WHEN
        actual_date = sut.add_days(given_days)

        # EXPECTATIONS: the date Python computes for the same addition
        expected_date = from_native(date(2026, 11, 5) + timedelta(days=30)).single()

        # THEN
        self.assertEqual(expected_date, actual_date)
    # END add-days


class TestAddMonths(unittest.TestCase):
    # BEGIN month-end
    def test_returns_none_when_the_day_is_missing_from_the_target_month(self):
        """Verifies that CivilDate.add_months returns none when the day is missing from the target month.

        Compared with: ``date`` has no month arithmetic; ``date(2026, 1, 31).replace(month=2)`` raises a ValueError.
        """
        # GIVEN: one month, into a February that has no day 31
        given_months = 1

        # SYSTEM UNDER TEST: 31 January 2026
        sut = CivilDate.of(2026, 1, 31)

        # WHEN
        actual_outcome = sut.add_months(given_months)

        # EXPECTATIONS: no such date, explained by the month-end diagnostic
        expected_outcome_type = NoneOf
        expected_code = "month-end"

        # THEN
        self.assertIsInstance(actual_outcome, expected_outcome_type)
        self.assertIn(expected_code, actual_outcome.codes)

    def test_returns_the_last_day_of_the_month_when_constrain_is_requested(self):
        """Verifies that CivilDate.add_months with MonthEnd.CONSTRAIN returns the last day of the target month when
        the day is missing from it.

        Compared with: Python code clamps by hand with ``calendar.monthrange``, and gets the same date.
        """
        # GIVEN: one month, constrained to the end of the target month
        given_months = 1
        given_policy = MonthEnd.CONSTRAIN

        # SYSTEM UNDER TEST: 31 January 2026
        sut = CivilDate.of(2026, 1, 31)

        # WHEN
        actual_date = sut.add_months(given_months, given_policy)

        # EXPECTATIONS: the date Python code clamps to by hand
        tmp_last_day = calendar.monthrange(2026, 2)[1]
        expected_date = from_native(date(2026, 2, min(31, tmp_last_day))).single()

        # THEN
        self.assertEqual(expected_date, actual_date)
    # END month-end


class TestAdd(unittest.TestCase):
    def test_returns_none_when_a_one_month_period_lands_on_a_missing_day(self):
        """Verifies that CivilDate.add returns none when a one-month period lands on a missing day.

        Compared with: Python has no calendar period type; ``timedelta`` cannot express a month.
        """
        # GIVEN: a calendar period of one month
        given_period = Period.of_months(1)

        # SYSTEM UNDER TEST: 31 January 2026
        sut = CivilDate.of(2026, 1, 31)

        # WHEN
        actual_outcome = sut.add(given_period)

        # EXPECTATIONS: no such date under the default, rejecting month-end policy
        expected_outcome_type = NoneOf
        expected_code = "month-end"

        # THEN
        self.assertIsInstance(actual_outcome, expected_outcome_type)
        self.assertIn(expected_code, actual_outcome.codes)


class TestDaysUntil(unittest.TestCase):
    # BEGIN days-between
    def test_returns_the_day_count_when_the_other_date_is_later(self):
        """Verifies that CivilDate.days_until returns the number of days to a later date.

        Compared with: subtracting two ``date`` values gives a ``timedelta`` whose ``days`` is the same.
        """
        # GIVEN: Christmas Day 2026
        given_christmas = CivilDate.of(2026, 12, 25)

        # SYSTEM UNDER TEST: 5 November 2026
        sut = CivilDate.of(2026, 11, 5)

        # WHEN
        actual_days = sut.days_until(given_christmas)

        # EXPECTATIONS: the difference Python computes
        expected_days = (date(2026, 12, 25) - date(2026, 11, 5)).days

        # THEN
        self.assertEqual(expected_days, actual_days)
    # END days-between


class TestLt(unittest.TestCase):
    # BEGIN compare-dates
    def test_returns_true_when_the_left_date_comes_first(self):
        """Verifies that CivilDate's less-than operator is true when the left date comes first.

        Compared with: ``date``'s operator gives the same answer. Plain dates always have an order.
        """
        # GIVEN: Christmas Day 2026
        given_christmas = CivilDate.of(2026, 12, 25)

        # SYSTEM UNDER TEST: 5 November 2026
        sut = CivilDate.of(2026, 11, 5)

        # WHEN
        actual_comes_first = sut < given_christmas

        # EXPECTATIONS: Python's answer for the same two days
        expected_comes_first = date(2026, 11, 5) < date(2026, 12, 25)

        # THEN
        self.assertEqual(expected_comes_first, actual_comes_first)
    # END compare-dates
