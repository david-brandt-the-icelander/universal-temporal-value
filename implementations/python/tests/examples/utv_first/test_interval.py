"""How to use ``Interval``, a range of dates or instants whose ends say whether they are included, unbounded or
unknown, compared with Python, which has no range type: bounds are compared by hand and a missing end is ``None``,
whatever the reason."""

import unittest
from datetime import date

from utv import Bound, CivilDate, Interval, Truth


class TestHalfOpen(unittest.TestCase):
    # BEGIN ranges
    def test_definitely_contains_the_day_when_the_day_is_between_the_ends(self):
        """Verifies that a half-open Interval definitely contains a day between its ends.

        Compared with: ``first <= day < end`` on ``date`` values says the same.
        """
        # GIVEN: 15 November 2026
        given_day = CivilDate.of(2026, 11, 15)

        # SYSTEM UNDER TEST: November 2026, from the 1st up to but not including 1 December
        sut = Interval.half_open(CivilDate.of(2026, 11, 1), CivilDate.of(2026, 12, 1))

        # WHEN
        actual_contains = sut.contains(given_day)

        # EXPECTATIONS: the answer Python's hand-written bounds give
        tmp_native_contains = date(2026, 11, 1) <= date(2026, 11, 15) < date(2026, 12, 1)
        expected_contains = Truth.DEFINITELY if tmp_native_contains else Truth.IMPOSSIBLE

        # THEN
        self.assertEqual(expected_contains, actual_contains)
    # END ranges

    # BEGIN ranges
    def test_cannot_contain_the_day_when_the_day_is_the_excluded_end(self):
        """Verifies that a half-open Interval cannot contain its excluded end.

        Compared with: the hand-written ``day < end`` excludes it too, if nobody writes ``<=`` by mistake.
        """
        # GIVEN: 1 December 2026, the end of the interval
        given_day = CivilDate.of(2026, 12, 1)

        # SYSTEM UNDER TEST: November 2026, from the 1st up to but not including 1 December
        sut = Interval.half_open(CivilDate.of(2026, 11, 1), CivilDate.of(2026, 12, 1))

        # WHEN
        actual_contains = sut.contains(given_day)

        # EXPECTATIONS: impossible, and written with the end's bracket open
        expected_contains = Truth.IMPOSSIBLE
        expected_text = "[2026-11-01, 2026-12-01)"

        # THEN
        self.assertEqual(expected_contains, actual_contains)
        self.assertEqual(expected_text, str(sut))
    # END ranges


class TestClosed(unittest.TestCase):
    def test_definitely_contains_the_day_when_the_day_is_the_included_end(self):
        """Verifies that a closed Interval definitely contains its last day.

        Compared with: ``first <= day <= last`` on ``date`` values says the same.
        """
        # GIVEN: 30 November 2026, the last day of the interval
        given_day = CivilDate.of(2026, 11, 30)

        # SYSTEM UNDER TEST: November 2026, from the 1st to the 30th, both included
        sut = Interval.closed(CivilDate.of(2026, 11, 1), CivilDate.of(2026, 11, 30))

        # WHEN
        actual_contains = sut.contains(given_day)

        # EXPECTATIONS: the answer Python's hand-written bounds give
        tmp_native_contains = date(2026, 11, 1) <= date(2026, 11, 30) <= date(2026, 11, 30)
        expected_contains = Truth.DEFINITELY if tmp_native_contains else Truth.IMPOSSIBLE

        # THEN
        self.assertEqual(expected_contains, actual_contains)


class TestOf(unittest.TestCase):
    def test_definitely_contains_a_later_day_when_the_end_is_unbounded(self):
        """Verifies that an Interval with an unbounded end definitely contains any later day.

        Compared with: an end of ``None`` means "no end" here, by convention only.
        """
        # GIVEN: 1 January 3000
        given_day = CivilDate.of(3000, 1, 1)

        # SYSTEM UNDER TEST: from 1 November 2026 on, with no end at all
        sut = Interval.of(Bound.included(CivilDate.of(2026, 11, 1)), Bound.UNBOUNDED)

        # WHEN
        actual_contains = sut.contains(given_day)

        # EXPECTATIONS: definitely, and written with ".." for the missing end
        expected_contains = Truth.DEFINITELY
        expected_text = "[2026-11-01, ..)"

        # THEN
        self.assertEqual(expected_contains, actual_contains)
        self.assertEqual(expected_text, str(sut))

    # BEGIN ranges
    def test_possibly_contains_a_later_day_when_the_end_is_unknown(self):
        """Verifies that an Interval with an unknown end only possibly contains a later day.

        Compared with: the same ``None`` end would be read as "no end", and the answer would wrongly be "yes".
        """
        # GIVEN: 1 June 2027
        given_day = CivilDate.of(2027, 6, 1)

        # SYSTEM UNDER TEST: from 1 November 2026, until an end nobody knows yet
        sut = Interval.of(Bound.included(CivilDate.of(2026, 11, 1)), Bound.UNKNOWN)

        # WHEN
        actual_contains = sut.contains(given_day)

        # EXPECTATIONS: possibly, and written with "?" for the unknown end
        expected_contains = Truth.POSSIBLY
        expected_text = "[2026-11-01, ?)"

        # THEN
        self.assertEqual(expected_contains, actual_contains)
        self.assertEqual(expected_text, str(sut))
    # END ranges
