"""How to use ``Period``, an amount of calendar components such as days and months, compared with Python's
``datetime.timedelta``, which only counts days, seconds and microseconds and so cannot tell a calendar day from
86,400 seconds."""

import unittest
from datetime import timedelta

from utv import Intent, Period, from_native


class TestOfDays(unittest.TestCase):
    def test_is_not_equal_to_86400_seconds_when_given_one_day(self):
        """Verifies that Period.of_days creates a calendar day, which is not a number of seconds.

        Compared with: ``timedelta(days=1) == timedelta(seconds=86_400)``, so the difference is lost.
        """
        # GIVEN: one day
        given_days = 1

        # WHEN
        actual_period = Period.of_days(given_days)

        # EXPECTATIONS: a calendar component, written in ISO 8601, and a different value from 86,400 seconds
        expected_text = "P1D"
        expected_other_amount = from_native(timedelta(days=1), Intent.POSIX_SECONDS).single()

        # THEN
        self.assertEqual(expected_text, str(actual_period))
        self.assertNotEqual(expected_other_amount, actual_period)


class TestOfMonths(unittest.TestCase):
    def test_writes_p1m_when_given_one_month(self):
        """Verifies that Period.of_months creates a calendar month, distinct from a minute.

        Compared with: ``timedelta`` has no months at all; ISO 8601 writes P1M for a month and PT1M for a minute.
        """
        # GIVEN: one month
        given_months = 1

        # WHEN
        actual_period = Period.of_months(given_months)

        # EXPECTATIONS: P1M, never PT1M
        expected_text = "P1M"
        expected_minute_text = str(Period.of(minutes=1))

        # THEN
        self.assertEqual(expected_text, str(actual_period))
        self.assertNotEqual(expected_minute_text, str(actual_period))
