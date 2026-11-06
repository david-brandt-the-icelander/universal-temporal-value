"""What Python's ``datetime.date`` does, compared with UTV's ``CivilDate``. Where the two agree, the expected value is
computed with UTV."""

import unittest
from datetime import date, timedelta

from utv import CivilDate, to_native


class TestConstructor(unittest.TestCase):
    def test_creates_the_date_when_the_fields_name_a_real_day(self):
        """Verifies that the date constructor creates the date when the fields name a real day.

        Compared with: ``CivilDate.of(2026, 11, 5)`` names the same day.
        """
        # GIVEN: 5 November 2026
        given_year = 2026
        given_month = 11
        given_day = 5

        # WHEN
        actual_date = date(given_year, given_month, given_day)

        # EXPECTATIONS: the same day as UTV's CivilDate
        expected_date = to_native(CivilDate.of(2026, 11, 5)).single()

        # THEN
        self.assertEqual(expected_date, actual_date)

    def test_raises_value_error_when_the_day_does_not_exist(self):
        """Verifies that the date constructor raises a ValueError when the day does not exist in the month.

        Compared with: ``CivilDate.of`` raises a ValueError too (an OutcomeError, with the diagnostic);
        ``CivilDate.try_of`` returns invalid (day-out-of-range) instead.
        """
        # GIVEN: 30 February 2026
        given_year = 2026
        given_month = 2
        given_day = 30

        # WHEN
        with self.assertRaises(ValueError) as actual_raised:
            date(given_year, given_month, given_day)

        # EXPECTATIONS: a plain ValueError, whose only reason is its message
        expected_exception_type = ValueError

        # THEN
        self.assertIs(expected_exception_type, type(actual_raised.exception))

    def test_raises_value_error_when_the_month_is_13(self):
        """Verifies that the date constructor raises a ValueError when the month is 13.

        Compared with: ``CivilDate.try_of(2026, 13, 1)`` returns invalid with month-out-of-range.
        """
        # GIVEN: the first day of a thirteenth month
        given_year = 2026
        given_month = 13
        given_day = 1

        # WHEN
        with self.assertRaises(ValueError) as actual_raised:
            date(given_year, given_month, given_day)

        # EXPECTATIONS
        expected_exception_type = ValueError

        # THEN
        self.assertIs(expected_exception_type, type(actual_raised.exception))

    def test_raises_value_error_when_the_year_is_beyond_9999(self):
        """Verifies that the date constructor raises a ValueError when the year is beyond 9999.

        Compared with: ``CivilDate.of(275760, 9, 13)`` holds the date; UTV years have no size limit.
        """
        # GIVEN: 13 September 275760
        given_year = 275_760
        given_month = 9
        given_day = 13

        # WHEN
        with self.assertRaises(ValueError) as actual_raised:
            date(given_year, given_month, given_day)

        # EXPECTATIONS
        expected_exception_type = ValueError

        # THEN
        self.assertIs(expected_exception_type, type(actual_raised.exception))

    def test_raises_value_error_when_the_year_is_negative(self):
        """Verifies that the date constructor raises a ValueError when the year is before year 1.

        Compared with: ``CivilDate.of(-500, 3, 1)`` holds the date in the proleptic Gregorian calendar.
        """
        # GIVEN: 1 March of year -500
        given_year = -500
        given_month = 3
        given_day = 1

        # WHEN
        with self.assertRaises(ValueError) as actual_raised:
            date(given_year, given_month, given_day)

        # EXPECTATIONS
        expected_exception_type = ValueError

        # THEN
        self.assertIs(expected_exception_type, type(actual_raised.exception))


class TestMax(unittest.TestCase):
    def test_is_the_last_day_of_9999_when_read(self):
        """Verifies that date.max is the last day of year 9999.

        Compared with: ``to_native(CivilDate.of(10_000, 1, 1))`` is unsupported (out-of-native-range) for exactly this
        reason.
        """
        # WHEN
        actual_max = date.max

        # EXPECTATIONS: 31 December 9999, as UTV converts it
        expected_max = to_native(CivilDate.of(9999, 12, 31)).single()

        # THEN
        self.assertEqual(expected_max, actual_max)


class TestFromisoformat(unittest.TestCase):
    def test_reads_the_date_when_the_text_is_an_iso_date(self):
        """Verifies that date.fromisoformat reads the date when the text is an ISO 8601 calendar date.

        Compared with: ``CivilDate.parse("2026-11-05")`` reads the same day.
        """
        # GIVEN: the ISO 8601 text of 5 November 2026
        given_text = "2026-11-05"

        # WHEN
        actual_date = date.fromisoformat(given_text)

        # EXPECTATIONS: the day UTV parses from the same text
        expected_date = to_native(CivilDate.parse("2026-11-05").single()).single()

        # THEN
        self.assertEqual(expected_date, actual_date)

    def test_raises_value_error_when_the_text_names_a_day_that_does_not_exist(self):
        """Verifies that date.fromisoformat raises a ValueError when the text names a day that does not exist.

        Compared with: ``CivilDate.parse("2026-02-30")`` returns invalid with day-out-of-range, as a value.
        """
        # GIVEN: well-formed text for 30 February 2026
        given_text = "2026-02-30"

        # WHEN
        with self.assertRaises(ValueError) as actual_raised:
            date.fromisoformat(given_text)

        # EXPECTATIONS: a plain ValueError, whose only reason is its message
        expected_exception_type = ValueError

        # THEN
        self.assertIs(expected_exception_type, type(actual_raised.exception))


class TestWeekday(unittest.TestCase):
    def test_numbers_sunday_6_when_the_date_is_a_sunday(self):
        """Verifies that date.weekday numbers Sunday 6, counting from Monday 0, when the date is a Sunday.

        Compared with: UTV's ``Weekday`` follows ISO 8601, Monday 1 to Sunday 7, so Sunday is 7.
        """
        # SYSTEM UNDER TEST: Sunday 8 November 2026
        sut = date(2026, 11, 8)

        # WHEN
        actual_weekday = sut.weekday()

        # EXPECTATIONS: 6, where UTV numbers the same day 7
        expected_weekday = 6

        # THEN
        self.assertEqual(expected_weekday, actual_weekday)


class TestIsoweekday(unittest.TestCase):
    def test_numbers_sunday_7_when_the_date_is_a_sunday(self):
        """Verifies that date.isoweekday numbers Sunday 7, as ISO 8601 does, when the date is a Sunday.

        Compared with: ``CivilDate.day_of_week`` numbers it the same.
        """
        # SYSTEM UNDER TEST: Sunday 8 November 2026
        sut = date(2026, 11, 8)

        # WHEN
        actual_weekday = sut.isoweekday()

        # EXPECTATIONS: the number UTV gives the same day
        expected_weekday = int(CivilDate.of(2026, 11, 8).day_of_week)

        # THEN
        self.assertEqual(expected_weekday, actual_weekday)


class TestIsocalendar(unittest.TestCase):
    def test_returns_week_45_when_the_date_is_5_november_2026(self):
        """Verifies that date.isocalendar returns the ISO 8601 week number of the date.

        Compared with: ``CivilDate.iso_week`` gives the same week.
        """
        # SYSTEM UNDER TEST: 5 November 2026
        sut = date(2026, 11, 5)

        # WHEN
        actual_week = sut.isocalendar().week

        # EXPECTATIONS: the week UTV computes for the same day
        expected_week = CivilDate.of(2026, 11, 5).iso_week

        # THEN
        self.assertEqual(expected_week, actual_week)


class TestAdd(unittest.TestCase):
    def test_returns_the_later_date_when_the_days_cross_a_month_end(self):
        """Verifies that adding a timedelta to a date returns the date that many days later, carrying into the next
        month.

        Compared with: ``CivilDate.add_days`` gives the same date.
        """
        # GIVEN: 30 days, more than remain in November
        given_days = timedelta(days=30)

        # SYSTEM UNDER TEST: 5 November 2026
        sut = date(2026, 11, 5)

        # WHEN
        actual_date = sut + given_days

        # EXPECTATIONS: the date UTV computes for the same addition
        expected_date = to_native(CivilDate.of(2026, 11, 5).add_days(30)).single()

        # THEN
        self.assertEqual(expected_date, actual_date)


class TestReplace(unittest.TestCase):
    def test_raises_value_error_when_the_day_is_missing_from_the_target_month(self):
        """Verifies that date.replace raises a ValueError when the day is missing from the target month.

        Compared with: ``date`` has no month arithmetic, so this is the nearest thing; UTV's
        ``CivilDate.add_months(1)`` returns none (month-end), and gives 28 February when asked with
        ``MonthEnd.CONSTRAIN``.
        """
        # GIVEN: February, which has no day 31
        given_month = 2

        # SYSTEM UNDER TEST: 31 January 2026
        sut = date(2026, 1, 31)

        # WHEN
        with self.assertRaises(ValueError) as actual_raised:
            sut.replace(month=given_month)

        # EXPECTATIONS
        expected_exception_type = ValueError

        # THEN
        self.assertIs(expected_exception_type, type(actual_raised.exception))


class TestSub(unittest.TestCase):
    def test_returns_the_day_count_when_subtracted_from_a_later_date(self):
        """Verifies that subtracting two dates gives a timedelta whose days are the number of days between them.

        Compared with: ``CivilDate.days_until`` returns the same count directly.
        """
        # GIVEN: Christmas Day 2026
        given_christmas = date(2026, 12, 25)

        # SYSTEM UNDER TEST: 5 November 2026
        sut = date(2026, 11, 5)

        # WHEN
        actual_days = (given_christmas - sut).days

        # EXPECTATIONS: the count UTV computes
        expected_days = CivilDate.of(2026, 11, 5).days_until(CivilDate.of(2026, 12, 25))

        # THEN
        self.assertEqual(expected_days, actual_days)


class TestLt(unittest.TestCase):
    def test_returns_true_when_the_left_date_comes_first(self):
        """Verifies that date's less-than operator is true when the left date comes first.

        Compared with: ``CivilDate``'s operator gives the same answer.
        """
        # GIVEN: Christmas Day 2026
        given_christmas = date(2026, 12, 25)

        # SYSTEM UNDER TEST: 5 November 2026
        sut = date(2026, 11, 5)

        # WHEN
        actual_comes_first = sut < given_christmas

        # EXPECTATIONS: UTV's answer for the same two days
        expected_comes_first = CivilDate.of(2026, 11, 5) < CivilDate.of(2026, 12, 25)

        # THEN
        self.assertEqual(expected_comes_first, actual_comes_first)
