"""How to use ``utv.from_native`` and ``utv.to_native``, which convert between UTV values and Python's ``date``,
``time``, ``datetime`` and ``timedelta``, compared with what those native values mean on their own. The caller names
the intent, a naive ``datetime`` is never read as local time, and a lossy conversion back says so."""

import unittest
from datetime import datetime, timedelta, timezone

from utv import CivilDate, CivilDateTime, Intent, Unsupported, from_native, to_native


class TestFromNative(unittest.TestCase):
    # BEGIN now
    def test_returns_unsupported_when_a_naive_datetime_is_asked_for_an_instant(self):
        """Verifies that from_native returns unsupported when a naive datetime is to be read as an instant.

        Compared with: ``datetime.now()`` returns a naive wall time that only means something on this machine, and
        ``astimezone()`` would read it in this machine's zone.
        """
        # GIVEN: a reading shaped like datetime.now()'s, with no zone
        given_local_time = datetime(2026, 11, 5, 14, 30)
        given_intent = Intent.INSTANT

        # WHEN
        actual_outcome = from_native(given_local_time, given_intent)

        # EXPECTATIONS: refused, because the host's zone is hidden input
        expected_outcome_type = Unsupported
        expected_code = "local-zone"

        # THEN
        self.assertIsInstance(actual_outcome, expected_outcome_type)
        self.assertIn(expected_code, actual_outcome.codes)
    # END now

    def test_keeps_the_wall_time_and_offset_when_given_a_fixed_offset_datetime(self):
        """Verifies that from_native with Intent.OFFSET keeps the wall time and the offset of the native value.

        Compared with: the ``datetime`` it was made from has the same wall time and ``utcoffset()``.
        """
        # GIVEN: lunch at 14:30 on 5 November 2026, one hour east of UTC
        given_lunch = datetime(2026, 11, 5, 14, 30, tzinfo=timezone(timedelta(hours=1)))
        given_intent = Intent.OFFSET

        # WHEN
        actual_outcome = from_native(given_lunch, given_intent)

        # EXPECTATIONS: the same wall time and offset as the native value
        expected_wall_time = CivilDateTime.of(2026, 11, 5, 14, 30, 0)
        expected_offset_seconds = 3600

        # THEN
        self.assertEqual(expected_wall_time, actual_outcome.single().date_time)
        self.assertEqual(expected_offset_seconds, actual_outcome.single().offset_seconds)


class TestToNative(unittest.TestCase):
    # BEGIN round-trip
    def test_returns_the_original_when_the_value_came_from_a_datetime(self):
        """Verifies that to_native gives back exactly the native value a FixedOffsetDateTime was made from.

        Compared with: ``datetime``'s ``==`` compares the moments, so the offset is checked as well.
        """
        # GIVEN: lunch at 14:30 on 5 November 2026, one hour east of UTC
        given_lunch = datetime(2026, 11, 5, 14, 30, tzinfo=timezone(timedelta(hours=1)))

        # SYSTEM UNDER TEST: the same lunch as a UTV value, with its intent named
        sut = from_native(given_lunch, Intent.OFFSET).single()

        # WHEN
        actual_outcome = to_native(sut)

        # EXPECTATIONS: the very same native value
        expected_lunch = datetime(2026, 11, 5, 14, 30, tzinfo=timezone(timedelta(hours=1)))

        # THEN
        self.assertEqual(expected_lunch, actual_outcome.single())
        self.assertEqual(expected_lunch.utcoffset(), actual_outcome.single().utcoffset())
    # END round-trip

    def test_returns_unsupported_when_the_year_is_beyond_9999(self):
        """Verifies that to_native returns unsupported when the date's year is beyond 9999.

        Compared with: ``date.max`` is 9999-12-31, so there is no ``date`` to return.
        """
        # GIVEN: 1 January 10000
        given_date = CivilDate.of(10_000, 1, 1)

        # WHEN
        actual_outcome = to_native(given_date)

        # EXPECTATIONS: unsupported, because the native type cannot hold the value
        expected_outcome_type = Unsupported
        expected_code = "out-of-native-range"

        # THEN
        self.assertIsInstance(actual_outcome, expected_outcome_type)
        self.assertIn(expected_code, actual_outcome.codes)
