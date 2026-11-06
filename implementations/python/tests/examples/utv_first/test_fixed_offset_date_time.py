"""How to use ``FixedOffsetDateTime``, a wall time pinned to a UTC offset (but to no zone), compared with Python's
``datetime.datetime`` with a fixed ``datetime.timezone``. Conversions to and from ``datetime`` are in
``test_interop``."""

import unittest
from datetime import datetime, timedelta, timezone

from utv import CivilDateTime, Intent, from_native


class TestToInstant(unittest.TestCase):
    def test_returns_the_utc_moment_when_the_offset_is_known(self):
        """Verifies that FixedOffsetDateTime.to_instant returns the instant the wall time and offset describe.

        Compared with: ``datetime.astimezone(timezone.utc)`` names the same moment.
        """
        # SYSTEM UNDER TEST: 14:30 on 5 November 2026, one hour east of UTC
        sut = CivilDateTime.of(2026, 11, 5, 14, 30).at_offset(3600)

        # WHEN
        actual_instant = sut.to_instant()

        # EXPECTATIONS: the moment Python computes for the same value
        tmp_utc = datetime(2026, 11, 5, 14, 30, tzinfo=timezone(timedelta(hours=1))).astimezone(timezone.utc)
        expected_instant = from_native(tmp_utc, Intent.INSTANT).single()

        # THEN
        self.assertEqual(expected_instant, actual_instant)
