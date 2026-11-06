"""Native interop adapters (utv.native): intent, no local zone, lossy conversions back."""

import datetime as dt
import unittest
from fractions import Fraction
from zoneinfo import ZoneInfo, ZoneInfoNotFoundError

import utv
from utv import Intent, Status


def _zone(key):
    try:
        return ZoneInfo(key)
    except (ZoneInfoNotFoundError, ValueError, OSError):
        return None


NY = _zone("America/New_York")
needs_tzdb = unittest.skipIf(NY is None, "host has no tz database for America/New_York")


class FromNative(unittest.TestCase):
    def test_date_and_time_are_direct(self):
        self.assertEqual(utv.from_date(dt.date(2026, 11, 5)).value, utv.date(2026, 11, 5))
        self.assertEqual(utv.from_time(dt.time(9, 30)).value, utv.time(9, 30, 0))
        self.assertEqual(utv.from_time(dt.time(9, 30, 15, 250000)).value, utv.time(9, 30, 15, 250_000_000))

    def test_wrong_types_raise(self):
        with self.assertRaises(TypeError):
            utv.from_date(dt.datetime(2026, 11, 5))
        with self.assertRaises(TypeError):
            utv.from_datetime(dt.date(2026, 11, 5))

    def test_aware_time_is_unsupported(self):
        self.assertEqual(utv.from_time(dt.time(9, 30, tzinfo=dt.timezone.utc)).status, Status.UNSUPPORTED)

    def test_naive_datetime_is_floating(self):
        r = utv.from_datetime(dt.datetime(2026, 11, 5, 14, 30))
        self.assertEqual(r.value, utv.date_time(2026, 11, 5, 14, 30, 0))
        self.assertEqual(utv.from_datetime(dt.datetime(2026, 11, 5, 14, 30), Intent.FLOATING).value, r.value)

    def test_naive_datetime_is_never_read_as_local(self):
        naive = dt.datetime(2026, 11, 5, 14, 30)
        for intent in (Intent.INSTANT, Intent.OFFSET, Intent.ZONED):
            with self.subTest(intent=intent):
                r = utv.from_datetime(naive, intent)
                self.assertEqual(r.status, Status.UNSUPPORTED)
                self.assertEqual(r.diagnostics[0].code, "local-zone")

    def test_fixed_timezone_needs_an_intent(self):
        aware = dt.datetime(2026, 11, 5, 14, 30, tzinfo=dt.timezone(dt.timedelta(hours=1)))
        r = utv.from_datetime(aware)
        self.assertEqual(r.status, Status.INVALID)
        self.assertEqual(r.diagnostics[0].code, "intent-required")
        self.assertEqual(utv.format_iso(utv.from_datetime(aware, Intent.INSTANT).value).value, "2026-11-05T13:30:00Z")
        self.assertEqual(utv.format_iso(utv.from_datetime(aware, "offset").value).value, "2026-11-05T14:30:00+01:00")
        self.assertEqual(utv.from_datetime(aware, Intent.ZONED).status, Status.UNSUPPORTED)

    def test_utc_offset_zero_is_an_offset_not_unknown(self):
        aware = dt.datetime(2026, 11, 5, 14, 30, tzinfo=dt.timezone.utc)
        self.assertEqual(utv.from_datetime(aware, Intent.OFFSET).value["value"]["zone"], {"type": "fixed-offset", "seconds": 0})

    def test_instant_is_exact(self):
        aware = dt.datetime(1969, 12, 31, 23, 59, 59, 500000, tzinfo=dt.timezone.utc)
        self.assertEqual(utv.from_datetime(aware, Intent.INSTANT).value, utv.instant(-1, 500_000_000))

    def test_unknown_tzinfo_is_refused(self):
        class Mystery(dt.tzinfo):
            def utcoffset(self, d):
                return dt.timedelta(hours=2)

            def dst(self, d):
                return dt.timedelta(0)

        r = utv.from_datetime(dt.datetime(2026, 11, 5, tzinfo=Mystery()), Intent.INSTANT)
        self.assertEqual(r.status, Status.UNSUPPORTED)

    def test_unknown_intent(self):
        self.assertEqual(utv.from_datetime(dt.datetime(2026, 11, 5), "local").status, Status.INVALID)

    def test_timedelta_needs_an_intent(self):
        day = dt.timedelta(days=1, microseconds=1)
        self.assertEqual(utv.from_timedelta(day).status, Status.INVALID)
        self.assertEqual(utv.from_timedelta(day, Intent.POSIX_SECONDS).value,
                         utv.posix_seconds(Fraction(86400) + Fraction(1, 1_000_000)))
        self.assertEqual(utv.from_timedelta(day, Intent.PERIOD).status, Status.UNSUPPORTED)
        self.assertEqual(utv.from_timedelta(dt.timedelta(days=1), Intent.PERIOD).value, utv.period(days=1))


@needs_tzdb
class FromZoneInfo(unittest.TestCase):
    def test_zoned_with_fold(self):
        first = utv.from_datetime(dt.datetime(2026, 11, 1, 1, 30, tzinfo=NY))
        second = utv.from_datetime(dt.datetime(2026, 11, 1, 1, 30, fold=1, tzinfo=NY))
        self.assertEqual(first.value["value"]["offsetAssertion"], -14400)
        self.assertEqual(second.value["value"]["offsetAssertion"], -18000)
        self.assertEqual(first.value["value"]["zone"], {"type": "iana", "id": "America/New_York"})
        self.assertEqual(first.diagnostics[0].code, "ambiguous")

    def test_gap_is_not_silently_shifted(self):
        gap = dt.datetime(2026, 3, 8, 2, 30, tzinfo=NY)
        self.assertEqual(utv.from_datetime(gap, Intent.INSTANT).status, Status.NONE)
        zoned = utv.from_datetime(gap)
        self.assertEqual(zoned.status, Status.UNIQUE)
        self.assertNotIn("offsetAssertion", zoned.value["value"])

    def test_localtime_key_is_refused(self):
        local = _zone("localtime")
        if local is None:
            self.skipTest("host has no /etc/localtime")
        self.assertEqual(utv.from_datetime(dt.datetime(2026, 11, 5, tzinfo=local)).status, Status.UNSUPPORTED)


class ToNative(unittest.TestCase):
    def test_round_trips(self):
        self.assertEqual(utv.to_date(utv.date(2026, 11, 5)).value, dt.date(2026, 11, 5))
        self.assertEqual(utv.to_time(utv.time(9, 30)).value, dt.time(9, 30))
        self.assertEqual(utv.to_datetime(utv.date_time(2026, 11, 5, 14, 30)).value, dt.datetime(2026, 11, 5, 14, 30))
        off = utv.to_datetime(utv.with_offset(utv.date_time(2026, 11, 5, 14, 30), -18000)).value
        self.assertEqual(off.utcoffset(), dt.timedelta(hours=-5))
        self.assertEqual(utv.to_datetime(utv.instant(1793885400)).value,
                         dt.datetime(2026, 11, 5, 13, 30, tzinfo=dt.timezone.utc))
        self.assertEqual(utv.to_timedelta(utv.posix_seconds(86400)).value, dt.timedelta(days=1))

    def test_years_outside_native_range(self):
        for value in (utv.date(10000, 1, 1), utv.date(0, 1, 1), utv.date(-500, 3, 1)):
            with self.subTest(value=value["value"]["fields"]["year"]):
                self.assertEqual(utv.to_date(value).status, Status.UNSUPPORTED)
        self.assertEqual(utv.to_datetime(utv.instant(-62135596801)).status, Status.UNSUPPORTED)

    def test_invalid_value(self):
        self.assertEqual(utv.to_date(utv.date(2026, 2, 30)).status, Status.INVALID)

    def test_sub_microsecond_is_reported(self):
        value = utv.date_time(2026, 11, 5, 14, 30, 0, 1)
        self.assertEqual(utv.to_datetime(value).status, Status.UNSUPPORTED)
        lossy = utv.to_datetime(value, allow_loss=True)
        self.assertEqual(lossy.value, dt.datetime(2026, 11, 5, 14, 30))
        self.assertEqual(lossy.loss, {"classification": "information-losing", "fields": ["nano"]})
        self.assertEqual([d.code for d in lossy.diagnostics], ["precision-loss"])
        self.assertEqual(utv.to_datetime(utv.instant(0, 1)).status, Status.UNSUPPORTED)
        self.assertEqual(utv.to_timedelta(utv.posix_seconds(Fraction(1, 3))).status, Status.UNSUPPORTED)

    def test_zoned_needs_zone_data_or_reports_loss(self):
        zoned = utv.parse_iso("2026-11-01T01:30-05:00[America/New_York]").value
        r = utv.to_datetime(zoned)
        self.assertEqual(r.status, Status.UNSUPPORTED)
        self.assertEqual(r.diagnostics[0].code, "needs-zone-rules")
        lossy = utv.to_datetime(zoned, allow_loss=True)
        self.assertEqual(lossy.value.utcoffset(), dt.timedelta(hours=-5))
        self.assertEqual(lossy.loss["fields"], ["zone"])
        self.assertEqual([d.code for d in lossy.diagnostics], ["zone-dropped"])

    def test_calendar_period_has_no_timedelta(self):
        self.assertEqual(utv.to_timedelta(utv.period(months=1)).status, Status.UNSUPPORTED)


@needs_tzdb
class ToZoneInfo(unittest.TestCase):
    def test_offset_assertion_picks_fold(self):
        zoned = utv.parse_iso("2026-11-01T01:30-05:00[America/New_York]").value
        native = utv.to_datetime(zoned, tz=NY).value
        self.assertEqual((native.fold, native.utcoffset()), (1, dt.timedelta(hours=-5)))

    def test_ambiguous_and_gap_without_assertion(self):
        overlap = utv.in_zone(utv.date_time(2026, 11, 1, 1, 30), "America/New_York")
        r = utv.to_datetime(overlap, tz=NY)
        self.assertEqual(r.status, Status.MULTIPLE)
        self.assertEqual([d.fold for d in r.candidates], [0, 1])
        gap = utv.in_zone(utv.date_time(2026, 3, 8, 2, 30), "America/New_York")
        self.assertEqual(utv.to_datetime(gap, tz=NY).status, Status.NONE)

    def test_zone_must_match(self):
        zoned = utv.in_zone(utv.date_time(2027, 6, 1, 9, 0), "Europe/Paris")
        self.assertEqual(utv.to_datetime(zoned, tz=NY).status, Status.UNSUPPORTED)


if __name__ == "__main__":
    unittest.main()
