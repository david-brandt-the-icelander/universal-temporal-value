"""What Python's ``datetime.datetime`` does, compared with UTV's ``CivilDateTime``, ``FixedOffsetDateTime``,
``ZonedIntent`` and ``Instant``. Where the two agree, the expected value is computed with UTV.

The tests that attach a ``zoneinfo.ZoneInfo`` read this machine's tz database, so they need one that knows the
2026–2027 rules of New York and Paris. UTV reads only the pinned demo rules it is given.
"""

import sys
import unittest
from datetime import datetime, timedelta, timezone
from zoneinfo import ZoneInfo

from utv import (
    CivilDateTime, Gap, Instant, Intent, Overlap, PosixSeconds, ResolutionPolicy, TemporalContext, from_native,
    to_native,
)

from ..fixtures import demo_zone_rules


class TestConstructor(unittest.TestCase):
    def test_creates_a_naive_wall_time_when_given_no_tzinfo(self):
        """Verifies that the datetime constructor creates a naive wall time when given no tzinfo.

        Compared with: ``CivilDateTime.of(2026, 11, 5, 14, 30)`` is the same wall time, and has no zone by design.
        """
        # GIVEN: 14:30 on 5 November 2026
        given_year = 2026
        given_month = 11
        given_day = 5
        given_hour = 14
        given_minute = 30

        # WHEN
        actual_date_time = datetime(given_year, given_month, given_day, given_hour, given_minute)

        # EXPECTATIONS: the wall time UTV holds, tied to no zone
        expected_date_time = to_native(CivilDateTime.of(2026, 11, 5, 14, 30)).single()
        expected_tzinfo = None

        # THEN
        self.assertEqual(expected_date_time, actual_date_time)
        self.assertIs(expected_tzinfo, actual_date_time.tzinfo)

    def test_raises_value_error_when_the_second_is_a_leap_second(self):
        """Verifies that the datetime constructor raises a ValueError when the second is a leap second, 60.

        Compared with: ``CivilDateTime.try_of(2016, 12, 31, 23, 59, 60)`` returns unsupported (leap-second): the
        second may well exist, and judging it needs a leap-second table.
        """
        # GIVEN: 23:59:60 on 31 December 2016, a real leap second
        given_second = 60

        # WHEN
        with self.assertRaises(ValueError) as actual_raised:
            datetime(2016, 12, 31, 23, 59, given_second)

        # EXPECTATIONS
        expected_exception_type = ValueError

        # THEN
        self.assertIs(expected_exception_type, type(actual_raised.exception))

    def test_pins_the_wall_time_to_the_offset_when_given_a_fixed_timezone(self):
        """Verifies that the datetime constructor pins the wall time to the offset when given a fixed timezone.

        Compared with: ``CivilDateTime.of(2026, 11, 5, 14, 30).at_offset(3600)`` is the same value, and its
        ``to_instant()`` is the same moment.
        """
        # GIVEN: one hour east of UTC
        given_tzinfo = timezone(timedelta(hours=1))

        # WHEN
        actual_date_time = datetime(2026, 11, 5, 14, 30, tzinfo=given_tzinfo)

        # EXPECTATIONS: the moment UTV computes for the same wall time and offset
        expected_moment = to_native(CivilDateTime.of(2026, 11, 5, 14, 30).at_offset(3600).to_instant()).single()

        # THEN
        self.assertEqual(expected_moment, actual_date_time)


class TestNow(unittest.TestCase):
    def test_returns_a_naive_wall_time_when_given_no_tz(self):
        """Verifies that datetime.now returns a naive wall time, of this machine's zone, when given no tz.

        Compared with: ``from_native(datetime.now(), Intent.INSTANT)`` is unsupported (local-zone), because its meaning
        depends on this machine's zone.

        This test reads the real clock on purpose: whether a tzinfo is attached does not depend on the time read, so
        the result is the same on every run. If it ever flakes, that is a finding about datetime.now, not noise.
        """
        # WHEN
        actual_now = datetime.now()

        # EXPECTATIONS: no zone attached
        expected_tzinfo = None

        # THEN
        self.assertIs(expected_tzinfo, actual_now.tzinfo)

    def test_survives_the_trip_through_utv_when_given_utc(self):
        """Verifies that datetime.now with timezone.utc returns a reading that UTV takes as an input without changing
        it.

        Compared with: UTV never reads the clock; ``from_native(reading, Intent.INSTANT)`` is how the reading gets in.

        This test reads the real clock on purpose: the round trip does not depend on the time read, so the result is
        the same on every run. If it ever flakes, that is a finding about the conversion, not noise.
        """
        # GIVEN: UTC
        given_tz = timezone.utc

        # WHEN
        actual_reading = datetime.now(given_tz)

        # EXPECTATIONS: the same reading after UTV has held it as an Instant
        expected_reading = to_native(from_native(actual_reading, Intent.INSTANT).single()).single()

        # THEN
        self.assertEqual(expected_reading, actual_reading)


class TestFromisoformat(unittest.TestCase):
    # Python 3.10 cannot read Z at all, so on 3.10 there is nothing to compare.
    @unittest.skipIf(sys.version_info < (3, 11), "datetime.fromisoformat reads Z from Python 3.11")
    def test_cannot_tell_z_from_plus_zero_when_both_name_the_same_moment(self):
        """Verifies that datetime.fromisoformat gives the same value for text ending in Z and text ending in +00:00.

        Compared with: ``utv.parse`` returns an ``Instant`` for Z and a ``FixedOffsetDateTime`` for +00:00; the two
        texts mean different things.
        """
        # GIVEN: 14:30 UTC, written with Z
        given_text = "2026-11-05T14:30:00Z"

        # WHEN
        actual_value = datetime.fromisoformat(given_text)

        # EXPECTATIONS: exactly the value Python parses from "+00:00", which UTV keeps apart as a FixedOffsetDateTime
        expected_value = datetime.fromisoformat("2026-11-05T14:30:00+00:00")

        # THEN
        self.assertEqual(expected_value, actual_value)
        self.assertEqual(expected_value.tzinfo, actual_value.tzinfo)


class TestFromtimestamp(unittest.TestCase):
    def test_returns_the_epoch_when_given_zero(self):
        """Verifies that datetime.fromtimestamp returns the Unix epoch when given zero seconds and timezone.utc.

        Compared with: ``Instant.from_unix_seconds(0)`` is the same moment.
        """
        # GIVEN: zero seconds since 1970-01-01T00:00:00Z
        given_seconds = 0

        # WHEN
        actual_epoch = datetime.fromtimestamp(given_seconds, timezone.utc)

        # EXPECTATIONS: the moment UTV returns for the same count
        expected_epoch = to_native(Instant.from_unix_seconds(0)).single()

        # THEN
        self.assertEqual(expected_epoch, actual_epoch)

    def test_keeps_six_digits_when_given_nanoseconds(self):
        """Verifies that datetime.fromtimestamp keeps only six fractional digits, rounded, when given a timestamp with
        nine.

        Compared with: ``Instant.from_unix_seconds(1_793_795_400, 123_456_789)`` keeps all nine, and ``to_native`` of
        it is unsupported (out-of-native-range) rather than silently rounded.
        """
        # GIVEN: 12:30:00.123456789 UTC on 4 November 2026, as a float timestamp
        given_timestamp = 1_793_795_400.123456789

        # WHEN
        actual_moment = datetime.fromtimestamp(given_timestamp, timezone.utc)

        # EXPECTATIONS: the moment UTV holds once its nine digits are rounded to six
        expected_moment = to_native(Instant.from_unix_seconds(1_793_795_400, 123_457_000)).single()

        # THEN
        self.assertEqual(expected_moment, actual_moment)


class TestTimestamp(unittest.TestCase):
    def test_returns_a_float_when_the_moment_has_a_quarter_second(self):
        """Verifies that datetime.timestamp returns the seconds since the epoch as a float.

        Compared with: ``Instant.epoch_seconds`` is the exact fraction, and ``Instant.unix_seconds`` the whole seconds.
        """
        # SYSTEM UNDER TEST: 13:30:00.25 UTC on 5 November 2026
        sut = datetime(2026, 11, 5, 13, 30, 0, 250_000, tzinfo=timezone.utc)

        # WHEN
        actual_seconds = sut.timestamp()

        # EXPECTATIONS: UTV's exact count, as the nearest float
        expected_seconds = float(Instant.from_unix_seconds(1_793_885_400, 250_000_000).epoch_seconds)

        # THEN
        self.assertEqual(expected_seconds, actual_seconds)


class TestSub(unittest.TestCase):
    def test_returns_float_seconds_when_the_moments_are_a_fraction_of_a_second_apart(self):
        """Verifies that subtracting two datetimes gives a timedelta whose total_seconds is a float.

        Compared with: ``Instant.until`` returns the exact fraction 14403/4.
        """
        # GIVEN: one hour and 0.75 seconds later
        given_later = datetime(2026, 11, 5, 14, 30, 1, tzinfo=timezone.utc)

        # SYSTEM UNDER TEST: 13:30:00.25 UTC on 5 November 2026
        sut = datetime(2026, 11, 5, 13, 30, 0, 250_000, tzinfo=timezone.utc)

        # WHEN
        actual_seconds = (given_later - sut).total_seconds()

        # EXPECTATIONS: UTV's exact answer, as the nearest float
        tmp_exact = Instant.from_unix_seconds(1_793_885_400, 250_000_000).until(Instant.from_unix_seconds(1_793_889_001))
        expected_seconds = float(tmp_exact.seconds)

        # THEN
        self.assertEqual(expected_seconds, actual_seconds)

    def test_returns_a_whole_day_when_both_share_a_zone_info_across_a_dst_change(self):
        """Verifies that subtracting two datetimes with the same ZoneInfo subtracts their wall times, ignoring a
        daylight-saving change between them.

        Compared with: ``Instant.until`` gives 82,800 seconds, 23 hours, for the same two noons.
        """
        # GIVEN: noon on 8 March 2026 in New York, the day clocks spring forward
        given_next_noon = datetime(2026, 3, 8, 12, 0, tzinfo=ZoneInfo("America/New_York"))

        # SYSTEM UNDER TEST: noon the day before
        sut = datetime(2026, 3, 7, 12, 0, tzinfo=ZoneInfo("America/New_York"))

        # WHEN
        actual_difference = given_next_noon - sut

        # EXPECTATIONS: 24 hours, where 23 have passed
        expected_difference = timedelta(hours=24)

        # THEN
        self.assertEqual(expected_difference, actual_difference)


class TestAdd(unittest.TestCase):
    def test_carries_into_the_next_day_when_the_hours_pass_midnight(self):
        """Verifies that adding a timedelta to a datetime carries into the next day when the hours pass midnight.

        Compared with: ``CivilDateTime.add_hours`` gives the same wall time.
        """
        # GIVEN: two hours, past midnight
        given_hours = timedelta(hours=2)

        # SYSTEM UNDER TEST: 23:30 on 5 November 2026
        sut = datetime(2026, 11, 5, 23, 30)

        # WHEN
        actual_date_time = sut + given_hours

        # EXPECTATIONS: the wall time UTV computes for the same addition
        expected_date_time = to_native(CivilDateTime.of(2026, 11, 5, 23, 30).add_hours(2)).single()

        # THEN
        self.assertEqual(expected_date_time, actual_date_time)

    def test_keeps_the_wall_time_when_the_day_crosses_a_dst_change_in_a_zone_info(self):
        """Verifies that adding a day to a datetime with a ZoneInfo keeps the wall time and takes the new offset when
        the day crosses a daylight-saving change.

        Compared with: ``ZonedIntent.add_days`` gives the same wall time, noon, in the same zone.
        """
        # GIVEN: one day
        given_days = timedelta(days=1)

        # SETUP: New York, from this machine's tz database
        env_new_york = ZoneInfo("America/New_York")

        # SYSTEM UNDER TEST: noon on 7 March 2026 in New York, the day before clocks spring forward
        sut = datetime(2026, 3, 7, 12, 0, tzinfo=env_new_york)

        # WHEN
        actual_next_noon = sut + given_days

        # EXPECTATIONS: UTV's next noon, in daylight time
        tmp_next_noon = CivilDateTime.of(2026, 3, 7, 12, 0).in_zone("America/New_York").add_days(1)
        expected_next_noon = to_native(tmp_next_noon, tz=env_new_york).single()

        # THEN
        self.assertEqual(expected_next_noon, actual_next_noon)
        self.assertEqual(expected_next_noon.utcoffset(), actual_next_noon.utcoffset())

    def test_keeps_the_offset_when_the_datetime_has_a_fixed_timezone(self):
        """Verifies that adding a day to a datetime with a fixed timezone keeps the offset, so across a
        daylight-saving change the wall time in New York becomes 13:00.

        Compared with: ``ZonedIntent.add_days`` keeps noon and lets the offset follow the zone; this moment is UTV's
        ``Instant.add(PosixSeconds.of(86_400))``.
        """
        # GIVEN: one day
        given_days = timedelta(days=1)

        # SYSTEM UNDER TEST: noon on 7 March 2026 in New York, standard time, the day before clocks spring forward
        sut = datetime(2026, 3, 7, 12, 0, tzinfo=timezone(timedelta(hours=-5)))

        # WHEN
        actual_day_later = sut + given_days

        # EXPECTATIONS: exactly 86,400 seconds later, which UTV places at 13:00 daylight time, not noon
        tmp_instant = CivilDateTime.of(2026, 3, 7, 12, 0).at_offset(-5 * 3600).to_instant()
        expected_day_later = to_native(tmp_instant.add(PosixSeconds.of(86_400))).single()
        expected_offset = timedelta(hours=-5)

        # THEN
        self.assertEqual(expected_day_later, actual_day_later)
        self.assertEqual(expected_offset, actual_day_later.utcoffset())


class TestAstimezone(unittest.TestCase):
    def test_returns_the_instant_when_the_wall_time_happens_once(self):
        """Verifies that datetime.astimezone(timezone.utc) returns the instant when the wall time happens exactly once.

        Compared with: ``ZonedIntent.resolve`` finds the same instant in the pinned rules.
        """
        # GIVEN: UTC
        given_tz = timezone.utc

        # SETUP: Paris, from this machine's tz database
        env_paris = ZoneInfo("Europe/Paris")

        # SYSTEM UNDER TEST: a meeting at 09:00 on 1 June 2027 in Paris
        sut = datetime(2027, 6, 1, 9, 0, tzinfo=env_paris)

        # WHEN
        actual_utc = sut.astimezone(given_tz)

        # EXPECTATIONS: the instant UTV resolves from the pinned rules
        tmp_context = TemporalContext(demo_zone_rules())
        tmp_instant = CivilDateTime.of(2027, 6, 1, 9, 0).in_zone("Europe/Paris").resolve(tmp_context).single()
        expected_utc = to_native(tmp_instant).single()

        # THEN
        self.assertEqual(expected_utc, actual_utc)

    def test_returns_the_local_wall_time_when_given_a_zone_info(self):
        """Verifies that datetime.astimezone returns the local wall time when given a ZoneInfo.

        Compared with: ``Instant.in_zone`` gives the same wall time from the pinned rules, with its offset.
        """
        # SETUP: New York, from this machine's tz database
        env_new_york = ZoneInfo("America/New_York")

        # SYSTEM UNDER TEST: 07:00 UTC on 1 June 2027
        sut = datetime(2027, 6, 1, 7, 0, tzinfo=timezone.utc)

        # WHEN
        actual_wall_time = sut.astimezone(env_new_york)

        # EXPECTATIONS: the wall time and offset UTV computes from the pinned rules
        tmp_context = TemporalContext(demo_zone_rules())
        tmp_zoned = Instant.parse("2027-06-01T07:00:00Z").single().in_zone("America/New_York", tmp_context).single()
        expected_wall_time = to_native(tmp_zoned, tz=env_new_york).single()

        # THEN
        self.assertEqual(expected_wall_time, actual_wall_time)
        self.assertEqual(expected_wall_time.utcoffset(), actual_wall_time.utcoffset())

    def test_shifts_the_wall_time_silently_when_it_falls_in_a_gap(self):
        """Verifies that datetime.astimezone converts a wall time that falls in a daylight-saving gap without a word,
        as if the clocks had not changed yet.

        Compared with: ``ZonedIntent.resolve`` returns none (gap); this instant, 03:30 daylight time, is what UTV gives
        only when asked with ``Gap.SHIFT_FORWARD``.
        """
        # GIVEN: UTC
        given_tz = timezone.utc

        # SETUP: New York, from this machine's tz database
        env_new_york = ZoneInfo("America/New_York")

        # SYSTEM UNDER TEST: 02:30 on 8 March 2026 in New York, when clocks jump from 02:00 to 03:00
        sut = datetime(2026, 3, 8, 2, 30, tzinfo=env_new_york)

        # WHEN
        actual_utc = sut.astimezone(given_tz)

        # EXPECTATIONS: the instant UTV picks only with the explicit shift-forward policy
        tmp_context = TemporalContext(demo_zone_rules())
        tmp_zoned = CivilDateTime.of(2026, 3, 8, 2, 30).in_zone("America/New_York")
        tmp_instant = tmp_zoned.resolve(tmp_context, ResolutionPolicy(gap=Gap.SHIFT_FORWARD)).single()
        expected_utc = to_native(tmp_instant).single()

        # THEN
        self.assertEqual(expected_utc, actual_utc)

    def test_picks_the_earlier_instant_silently_when_the_wall_time_falls_in_an_overlap(self):
        """Verifies that datetime.astimezone picks the earlier, daylight-time instant through fold=0, silently, when
        the wall time happens twice.

        Compared with: ``ZonedIntent.resolve`` returns both instants; this one is what ``Overlap.EARLIEST`` picks.
        """
        # GIVEN: UTC
        given_tz = timezone.utc

        # SETUP: New York, from this machine's tz database
        env_new_york = ZoneInfo("America/New_York")

        # SYSTEM UNDER TEST: 01:30 on 1 November 2026 in New York, when clocks go back from 02:00 to 01:00
        sut = datetime(2026, 11, 1, 1, 30, tzinfo=env_new_york)

        # WHEN
        actual_utc = sut.astimezone(given_tz)

        # EXPECTATIONS: the instant UTV picks only when asked for the earliest
        tmp_context = TemporalContext(demo_zone_rules())
        tmp_zoned = CivilDateTime.of(2026, 11, 1, 1, 30).in_zone("America/New_York")
        tmp_instant = tmp_zoned.resolve(tmp_context, ResolutionPolicy(overlap=Overlap.EARLIEST)).single()
        expected_utc = to_native(tmp_instant).single()

        # THEN
        self.assertEqual(expected_utc, actual_utc)


class TestLt(unittest.TestCase):
    def test_compares_wall_times_ignoring_fold_when_both_share_a_zone_info(self):
        """Verifies that datetime's less-than operator compares wall times, ignoring fold, when both datetimes share
        a ZoneInfo.

        Compared with: ``TemporalContext.compare`` returns multiple (ambiguous) for 01:30 and 01:45 that night, because
        01:30 standard time comes after 01:45 daylight time.
        """
        # GIVEN: a stand-up at 01:45 on 1 November 2026 in New York, the first time round (daylight time)
        given_standup = datetime(2026, 11, 1, 1, 45, fold=0, tzinfo=ZoneInfo("America/New_York"))

        # SYSTEM UNDER TEST: a call at 01:30 the same night, the second time round (standard time, the later instant)
        sut = datetime(2026, 11, 1, 1, 30, fold=1, tzinfo=ZoneInfo("America/New_York"))

        # WHEN
        actual_comes_first = sut < given_standup

        # EXPECTATIONS: "earlier", by wall time, although the call happens 45 minutes after the stand-up
        expected_comes_first = True

        # THEN
        self.assertEqual(expected_comes_first, actual_comes_first)


class TestEq(unittest.TestCase):
    def test_returns_true_when_the_value_made_a_round_trip_through_utv(self):
        """Verifies that datetime's == is true when the value made a round trip through UTV.

        Compared with: ``from_native(…, Intent.OFFSET)`` and ``to_native`` lose nothing, offset included.
        """
        # GIVEN: the same lunch, after a round trip through UTV's FixedOffsetDateTime
        given_round_trip = to_native(
            from_native(datetime(2026, 11, 5, 14, 30, tzinfo=timezone(timedelta(hours=1))), Intent.OFFSET).single()
        ).single()

        # SYSTEM UNDER TEST: lunch at 14:30 on 5 November 2026, one hour east of UTC
        sut = datetime(2026, 11, 5, 14, 30, tzinfo=timezone(timedelta(hours=1)))

        # WHEN
        actual_equal = sut == given_round_trip

        # EXPECTATIONS
        expected_equal = True

        # THEN
        self.assertEqual(expected_equal, actual_equal)
