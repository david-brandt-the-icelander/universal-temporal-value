"""How to use ``Instant``, an exact moment on the POSIX timescale with no calendar and no zone, compared with Python's
aware ``datetime.datetime`` and ``datetime.timedelta``.

UTV's zone operations use the pinned demo rules. The Python side of ``TestInZone`` reads this machine's tz database,
so it needs one that knows the 2027 rules of New York.
"""

import unittest
from datetime import datetime, timedelta, timezone
from zoneinfo import ZoneInfo

from utv import CivilDateTime, ElapsedSeconds, Instant, Intent, PosixSeconds, TemporalContext, Unsupported, from_native

from ..fixtures import demo_zone_rules


class TestFromUnixNanoseconds(unittest.TestCase):
    # BEGIN now
    def test_returns_the_same_moment_when_given_a_clock_reading(self):
        """Verifies that Instant.from_unix_nanoseconds takes a clock reading as an input when the program reads the
        clock.

        Compared with: ``datetime.now(timezone.utc)`` reads the clock itself; UTV never does, so the caller passes
        ``time.time_ns()`` in and tests can fix "now".
        """
        # GIVEN: a clock reading, as time.time_ns() would return it
        given_reading = 1_793_885_400_123_000_000

        # WHEN
        actual_now = Instant.from_unix_nanoseconds(given_reading)

        # EXPECTATIONS: the same moment, counted in microseconds since 1970 as Python's datetime counts it
        tmp_native = datetime(1970, 1, 1, tzinfo=timezone.utc) + timedelta(microseconds=given_reading // 1_000)
        expected_now = from_native(tmp_native, Intent.INSTANT).single()

        # THEN
        self.assertEqual(expected_now, actual_now)
    # END now


class TestFromUnixSeconds(unittest.TestCase):
    def test_returns_the_epoch_when_given_zero(self):
        """Verifies that Instant.from_unix_seconds returns the Unix epoch when given zero seconds.

        Compared with: ``datetime.fromtimestamp(0, timezone.utc)`` is the same moment.
        """
        # GIVEN: zero seconds since 1970-01-01T00:00:00Z
        given_seconds = 0

        # WHEN
        actual_instant = Instant.from_unix_seconds(given_seconds)

        # EXPECTATIONS: the moment Python returns for the same count, written in UTC
        expected_instant = from_native(datetime.fromtimestamp(0, timezone.utc), Intent.INSTANT).single()
        expected_text = "1970-01-01T00:00:00Z"

        # THEN
        self.assertEqual(expected_instant, actual_instant)
        self.assertEqual(expected_text, str(actual_instant))

    def test_keeps_every_nanosecond_when_given_nanoseconds(self):
        """Verifies that Instant.from_unix_seconds keeps all nine fractional digits when given nanoseconds.

        Compared with: ``datetime`` counts microseconds, so it keeps only six of them.
        """
        # GIVEN: a moment on 4 November 2026, to the nanosecond
        given_seconds = 1_793_795_400
        given_nanoseconds = 123_456_789

        # WHEN
        actual_instant = Instant.from_unix_seconds(given_seconds, given_nanoseconds)

        # EXPECTATIONS: all nine digits, where Python's text stops at six
        expected_text = "2026-11-04T12:30:00.123456789Z"
        expected_nanoseconds = 123_456_789

        # THEN
        self.assertEqual(expected_text, str(actual_instant))
        self.assertEqual(expected_nanoseconds, actual_instant.nanoseconds)

    def test_keeps_the_fraction_when_given_a_quarter_second(self):
        """Verifies that Instant.from_unix_seconds keeps a fraction of a second when given one.

        Compared with: ``datetime`` holds the same moment, and ``timestamp()`` gives it back as a float.
        """
        # GIVEN: 13:30:00.25 on 5 November 2026
        given_seconds = 1_793_885_400
        given_nanoseconds = 250_000_000

        # WHEN
        actual_instant = Instant.from_unix_seconds(given_seconds, given_nanoseconds)

        # EXPECTATIONS: the moment Python holds, with the quarter second written out
        tmp_native = datetime(2026, 11, 5, 13, 30, 0, 250_000, tzinfo=timezone.utc)
        expected_instant = from_native(tmp_native, Intent.INSTANT).single()
        expected_text = "2026-11-05T13:30:00.25Z"

        # THEN
        self.assertEqual(expected_instant, actual_instant)
        self.assertEqual(expected_text, str(actual_instant))


class TestUntil(unittest.TestCase):
    # BEGIN timestamps
    def test_returns_an_exact_fraction_when_the_instants_are_a_fraction_of_a_second_apart(self):
        """Verifies that Instant.until returns the exact number of seconds, as a fraction, between two instants.

        Compared with: subtracting two ``datetime`` values gives a ``timedelta`` whose ``total_seconds()`` is a float.
        """
        # GIVEN: one hour and 0.75 seconds later
        given_later = Instant.from_unix_seconds(1_793_889_001)

        # SYSTEM UNDER TEST: 13:30:00.25 on 5 November 2026
        sut = Instant.from_unix_seconds(1_793_885_400, 250_000_000)

        # WHEN
        actual_seconds = sut.until(given_later)

        # EXPECTATIONS: exactly 14403/4 seconds, the number Python approximates as a float
        expected_seconds = PosixSeconds.of(14_403, 4)
        tmp_native = datetime(2026, 11, 5, 14, 30, 1, tzinfo=timezone.utc) - datetime(
            2026, 11, 5, 13, 30, 0, 250_000, tzinfo=timezone.utc
        )
        expected_approximation = tmp_native.total_seconds()

        # THEN
        self.assertEqual(expected_seconds, actual_seconds)
        self.assertEqual(expected_approximation, float(actual_seconds.seconds))
    # END timestamps

    def test_returns_23_hours_when_the_day_between_loses_an_hour_to_dst(self):
        """Verifies that Instant.until returns 23 hours when the instants are noon on both sides of a spring-forward
        night.

        Compared with: Python agrees when both noons carry their fixed offsets; two noons with the same ``ZoneInfo``
        would subtract as wall times and give a whole day.
        """
        # GIVEN: noon on 8 March 2026 in New York, daylight time
        given_next_noon = CivilDateTime.of(2026, 3, 8, 12, 0).at_offset(-4 * 3600).to_instant()

        # SYSTEM UNDER TEST: noon on 7 March 2026 in New York, standard time
        sut = CivilDateTime.of(2026, 3, 7, 12, 0).at_offset(-5 * 3600).to_instant()

        # WHEN
        actual_seconds = sut.until(given_next_noon)

        # EXPECTATIONS: 82,800 seconds, as Python's timedelta counts them
        tmp_native = datetime(2026, 3, 8, 12, tzinfo=timezone(timedelta(hours=-4))) - datetime(
            2026, 3, 7, 12, tzinfo=timezone(timedelta(hours=-5))
        )
        expected_seconds = from_native(tmp_native, Intent.POSIX_SECONDS).single()

        # THEN
        self.assertEqual(expected_seconds, actual_seconds)


class TestAdd(unittest.TestCase):
    def test_moves_by_exactly_those_seconds_when_given_posix_seconds(self):
        """Verifies that Instant.add with POSIX seconds moves the instant by exactly that many seconds.

        Compared with: adding ``timedelta(seconds=86_400)`` to the aware ``datetime`` lands on the same moment: 13:00
        in New York, not noon.
        """
        # GIVEN: 86,400 POSIX seconds, one nominal day
        given_seconds = PosixSeconds.of(86_400)

        # SYSTEM UNDER TEST: noon on 7 March 2026 in New York, the day before clocks spring forward
        sut = CivilDateTime.of(2026, 3, 7, 12, 0).at_offset(-5 * 3600).to_instant()

        # WHEN
        actual_instant = sut.add(given_seconds)

        # EXPECTATIONS: the moment Python computes for the same addition
        tmp_native = datetime(2026, 3, 7, 12, tzinfo=timezone(timedelta(hours=-5))) + timedelta(seconds=86_400)
        expected_instant = from_native(tmp_native, Intent.INSTANT).single()

        # THEN
        self.assertEqual(expected_instant, actual_instant)

    # BEGIN day-vs-24h
    def test_returns_unsupported_when_given_si_seconds_without_a_leap_second_table(self):
        """Verifies that Instant.add with SI seconds returns unsupported when no leap-second table is available.

        Compared with: ``timedelta`` has one kind of second, so Python cannot tell SI seconds from POSIX seconds.
        """
        # GIVEN: 86,400 SI seconds, as a physicist measures a day
        given_seconds = ElapsedSeconds.of(86_400)

        # SYSTEM UNDER TEST: noon on 7 March 2026 in New York
        sut = CivilDateTime.of(2026, 3, 7, 12, 0).at_offset(-5 * 3600).to_instant()

        # WHEN
        actual_outcome = sut.add(given_seconds)

        # EXPECTATIONS: unsupported, because SI seconds on a POSIX timescale need leap seconds
        expected_outcome_type = Unsupported
        expected_code = "needs-leap-seconds"

        # THEN
        self.assertIsInstance(actual_outcome, expected_outcome_type)
        self.assertIn(expected_code, actual_outcome.codes)
    # END day-vs-24h


class TestInZone(unittest.TestCase):
    # BEGIN meeting-in-paris
    def test_returns_the_local_wall_time_when_the_zone_has_rules(self):
        """Verifies that Instant.in_zone returns the wall time and offset in the zone when the zone has rules for it.

        Compared with: ``datetime.astimezone(ZoneInfo("America/New_York"))`` gives the same wall time and offset from
        this machine's tz database.
        """
        # GIVEN: New York
        given_zone_id = "America/New_York"

        # SETUP: the pinned demo zone rules, never this machine's tz database
        env_context = TemporalContext(demo_zone_rules())

        # SYSTEM UNDER TEST: 07:00 UTC on 1 June 2027, when the meeting starts in Paris
        sut = Instant.parse("2027-06-01T07:00:00Z").single()

        # WHEN
        actual_outcome = sut.in_zone(given_zone_id, env_context)

        # EXPECTATIONS: the wall time and offset Python computes
        tmp_native = datetime(2027, 6, 1, 7, 0, tzinfo=timezone.utc).astimezone(ZoneInfo("America/New_York"))
        expected_zoned = from_native(tmp_native).single()
        expected_text = "2027-06-01T03:00:00-04:00[America/New_York]"

        # THEN
        self.assertEqual(expected_zoned, actual_outcome.single())
        self.assertEqual(expected_text, str(actual_outcome.single()))
    # END meeting-in-paris
