"""How to use ``utv.parse``, which reads any value in the UTV text form and returns the type the text describes,
compared with Python's ``datetime.fromisoformat``."""

import unittest
from datetime import datetime, timezone

from utv import FixedOffsetDateTime, Instant, Intent, from_native, parse


class TestParse(unittest.TestCase):
    # BEGIN parse-offsets
    def test_returns_an_instant_when_the_text_ends_in_z(self):
        """Verifies that utv.parse returns an Instant when the text ends in Z.

        Compared with: ``datetime.fromisoformat`` (Python 3.11 and later) reads it as an aware datetime with
        ``timezone.utc``, exactly what it reads from the text ending in +00:00.
        """
        # GIVEN: 14:30 UTC, written with Z: a moment, with no local wall time implied
        given_text = "2026-11-05T14:30:00Z"

        # WHEN
        actual_outcome = parse(given_text)

        # EXPECTATIONS: the moment Python holds for the same text
        expected_type = Instant
        expected_instant = from_native(datetime(2026, 11, 5, 14, 30, tzinfo=timezone.utc), Intent.INSTANT).single()

        # THEN
        self.assertIsInstance(actual_outcome.single(), expected_type)
        self.assertEqual(expected_instant, actual_outcome.single())

    def test_returns_a_fixed_offset_date_time_when_the_text_ends_in_plus_zero(self):
        """Verifies that utv.parse returns a FixedOffsetDateTime when the text ends in +00:00.

        Compared with: ``datetime.fromisoformat`` cannot tell this text from the one ending in Z.
        """
        # GIVEN: 14:30 at offset +00:00: a wall time somewhere that is at UTC+0 right now
        given_text = "2026-11-05T14:30:00+00:00"

        # WHEN
        actual_outcome = parse(given_text)

        # EXPECTATIONS: the value Python parses from the same text, and the text kept as written
        expected_type = FixedOffsetDateTime
        tmp_native = datetime.fromisoformat("2026-11-05T14:30:00+00:00")
        expected_date_time = from_native(tmp_native, Intent.OFFSET).single()
        expected_text = "2026-11-05T14:30:00+00:00"

        # THEN
        self.assertIsInstance(actual_outcome.single(), expected_type)
        self.assertEqual(expected_date_time, actual_outcome.single())
        self.assertEqual(expected_text, str(actual_outcome.single()))
    # END parse-offsets
