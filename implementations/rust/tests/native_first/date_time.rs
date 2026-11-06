//! What chrono's `DateTime<Tz>` does, with `Utc`, `FixedOffset` and chrono-tz's `Tz` as its zone,
//! compared with UTV's `Instant`, `FixedOffsetDateTime` and `ZonedIntent`. Where the two agree, the
//! expected value is computed with UTV.
//!
//! The tests with a chrono-tz zone use the tz database compiled into chrono-tz, not the host's;
//! UTV uses the pinned demo rules.

use std::error::Error;

use chrono::{DateTime, Days, FixedOffset, NaiveDate, NaiveDateTime, Offset, TimeDelta, TimeZone, Utc};
use chrono_tz::America::New_York;
use utv::{CivilDateTime, FixedOffsetDateTime, Instant, PosixSeconds, TemporalContext};

use crate::fixtures::demo_zone_rules;

mod parse_from_rfc3339 {
    use super::*;

    /// Verifies that DateTime::parse_from_rfc3339 gives the same value for text ending in Z and text ending in +00:00.
    /// Compared with: `utv::parse` returns an `Instant` for Z and a `FixedOffsetDateTime` for +00:00; the two texts
    /// mean different things.
    #[test]
    fn cannot_tell_z_from_plus_zero_when_both_name_the_same_moment() -> Result<(), Box<dyn Error>> {
        // GIVEN: 14:30 UTC, written with Z
        let given_text = "2026-11-05T14:30:00Z";

        // WHEN
        let actual_value = DateTime::parse_from_rfc3339(given_text)?;

        // EXPECTATIONS: exactly the value chrono parses from "+00:00", which UTV keeps apart as a FixedOffsetDateTime
        let expected_value = DateTime::parse_from_rfc3339("2026-11-05T14:30:00+00:00")?;

        // THEN
        assert_eq!(expected_value, actual_value);
        assert_eq!(expected_value.offset(), actual_value.offset());
        Ok(())
    }
}

mod from_timestamp {
    use super::*;

    /// Verifies that DateTime::from_timestamp returns the Unix epoch when given zero seconds.
    /// Compared with: `Instant::from_unix_seconds(0)` is the same moment.
    #[test]
    fn returns_the_epoch_when_given_zero() -> Result<(), Box<dyn Error>> {
        // GIVEN: zero seconds and zero nanoseconds since 1970-01-01T00:00:00Z
        let given_seconds = 0;
        let given_nanoseconds = 0;

        // WHEN
        let actual_epoch = DateTime::from_timestamp(given_seconds, given_nanoseconds);

        // EXPECTATIONS: the moment UTV returns for the same count
        let expected_epoch = DateTime::<Utc>::try_from(Instant::from_unix_seconds(0))?;

        // THEN
        assert_eq!(Some(expected_epoch), actual_epoch);
        Ok(())
    }

    /// Verifies that DateTime::from_timestamp keeps all nine fractional digits when given nanoseconds.
    /// Compared with: `Instant::from_unix_seconds_nanos` keeps the same nine; below a nanosecond, UTV's fractions stay
    /// exact where chrono has no digits left.
    #[test]
    fn keeps_every_nanosecond_when_given_nanoseconds() -> Result<(), Box<dyn Error>> {
        // GIVEN: a moment on 4 November 2026, to the nanosecond
        let given_seconds = 1_793_795_400;
        let given_nanoseconds = 123_456_789;

        // WHEN
        let actual_moment = DateTime::from_timestamp(given_seconds, given_nanoseconds);

        // EXPECTATIONS: the moment UTV holds
        let expected_moment = DateTime::<Utc>::try_from(Instant::from_unix_seconds_nanos(1_793_795_400, 123_456_789))?;

        // THEN
        assert_eq!(Some(expected_moment), actual_moment);
        Ok(())
    }
}

mod timestamp {
    use super::*;

    /// Verifies that DateTime::timestamp drops the fraction of a second when there is one.
    /// Compared with: `Instant::unix_seconds` is the same floor, and `Instant::sub_second` keeps the rest exactly.
    #[test]
    fn drops_the_fraction_when_the_moment_has_a_quarter_second() -> Result<(), Box<dyn Error>> {
        // SYSTEM UNDER TEST: 13:30:00.25 on 5 November 2026
        let sut = DateTime::from_timestamp(1_793_885_400, 250_000_000).unwrap();

        // WHEN
        let actual_seconds = sut.timestamp();

        // EXPECTATIONS: the whole seconds UTV reports, without its quarter second
        let expected_seconds = i64::try_from(Instant::from_unix_seconds_nanos(1_793_885_400, 250_000_000).unix_seconds())?;

        // THEN
        assert_eq!(expected_seconds, actual_seconds);
        Ok(())
    }
}

mod sub {
    use super::*;

    /// Verifies that subtracting two DateTimes gives a TimeDelta exact to the nanosecond.
    /// Compared with: `Instant::until` returns the exact fraction 14403/4 seconds; the two agree here, and part ways
    /// below a nanosecond.
    #[test]
    fn returns_the_exact_length_when_the_moments_are_a_fraction_of_a_second_apart() -> Result<(), Box<dyn Error>> {
        // GIVEN: one hour and 0.75 seconds later
        let given_later = DateTime::from_timestamp(1_793_889_001, 0).unwrap();

        // SYSTEM UNDER TEST: 13:30:00.25 on 5 November 2026
        let sut = DateTime::from_timestamp(1_793_885_400, 250_000_000).unwrap();

        // WHEN
        let actual_delta = given_later - sut;

        // EXPECTATIONS: UTV's exact answer, in nanoseconds
        let tmp_exact = Instant::from_unix_seconds_nanos(1_793_885_400, 250_000_000).until(Instant::from_unix_seconds(1_793_889_001));
        let expected_delta = TimeDelta::nanoseconds(i64::try_from(tmp_exact.numerator() * 1_000_000_000 / tmp_exact.denominator())?);

        // THEN
        assert_eq!(expected_delta, actual_delta);
        Ok(())
    }
}

mod checked_add_days {
    use super::*;

    /// Verifies that DateTime::checked_add_days keeps the wall time when the day crosses a daylight-saving change.
    /// Compared with: `ZonedIntent::add_days` keeps noon too, and lets the offset follow the zone.
    #[test]
    fn keeps_the_wall_time_when_the_day_crosses_a_dst_change() -> Result<(), Box<dyn Error>> {
        // GIVEN: one calendar day
        let given_days = Days::new(1);

        // SYSTEM UNDER TEST: noon on 7 March 2026 in New York, the day before clocks spring forward
        let sut = New_York.with_ymd_and_hms(2026, 3, 7, 12, 0, 0).unwrap();

        // WHEN
        let actual_next_noon = sut.checked_add_days(given_days).unwrap();

        // EXPECTATIONS: the wall time UTV keeps for the same addition
        let tmp_next_noon = CivilDateTime::of(2026, 3, 7, 12, 0)?.in_zone("America/New_York").add_days(1);
        let expected_wall_time = NaiveDateTime::try_from(tmp_next_noon.date_time())?;

        // THEN
        assert_eq!(expected_wall_time, actual_next_noon.naive_local());
        Ok(())
    }
}

mod add {
    use super::*;

    /// Verifies that adding `TimeDelta::days(1)` to a DateTime adds 86,400 seconds when the day crosses a
    /// daylight-saving change.
    /// Compared with: `Instant::add(PosixSeconds::of(86_400))` lands on the same moment; `ZonedIntent::add_days` keeps
    /// noon instead.
    #[test]
    fn moves_the_wall_time_to_13_00_when_the_day_crosses_a_dst_change() -> Result<(), Box<dyn Error>> {
        // GIVEN: one day, as a TimeDelta
        let given_delta = TimeDelta::days(1);

        // SYSTEM UNDER TEST: noon on 7 March 2026 in New York, standard time, the day before clocks spring forward
        let sut = New_York.with_ymd_and_hms(2026, 3, 7, 12, 0, 0).unwrap();

        // WHEN
        let actual_day_later = sut + given_delta;

        // EXPECTATIONS: exactly 86,400 seconds later, as UTV computes it, which is 13:00 daylight time, not noon
        let tmp_start = CivilDateTime::of(2026, 3, 7, 12, 0)?.at_offset(-5 * 3600)?.to_instant();
        let expected_moment = DateTime::<Utc>::try_from(tmp_start.add(PosixSeconds::of(86_400)))?;
        let expected_wall_time = NaiveDate::from_ymd_opt(2026, 3, 8).unwrap().and_hms_opt(13, 0, 0).unwrap();

        // THEN
        assert_eq!(expected_moment, actual_day_later);
        assert_eq!(expected_wall_time, actual_day_later.naive_local());
        Ok(())
    }
}

mod with_timezone {
    use super::*;

    /// Verifies that DateTime::with_timezone returns the local wall time and offset when given a zone.
    /// Compared with: `Instant::in_zone` gives the same wall time and offset from the pinned rules.
    #[test]
    fn returns_the_local_wall_time_when_given_a_zone() -> Result<(), Box<dyn Error>> {
        // GIVEN: New York, from the tz database compiled into chrono-tz
        let given_zone = New_York;

        // SYSTEM UNDER TEST: 07:00 UTC on 1 June 2027, when a 09:00 meeting starts in Paris
        let sut = Utc.with_ymd_and_hms(2027, 6, 1, 7, 0, 0).unwrap();

        // WHEN
        let actual_there = sut.with_timezone(&given_zone);

        // EXPECTATIONS: the wall time and offset UTV computes from the pinned rules
        let tmp_context = TemporalContext::with(demo_zone_rules());
        let tmp_there = Instant::parse("2027-06-01T07:00:00Z").single()?.in_zone("America/New_York", &tmp_context).single()?;
        let expected_wall_time = NaiveDateTime::try_from(tmp_there.date_time())?;
        let expected_offset_seconds = tmp_there.asserted_offset_seconds();

        // THEN
        assert_eq!(expected_wall_time, actual_there.naive_local());
        assert_eq!(expected_offset_seconds, Some(actual_there.offset().fix().local_minus_utc()));
        Ok(())
    }
}

mod eq {
    use super::*;

    /// Verifies that DateTime's `==` is true when the value made a round trip through UTV.
    /// Compared with: `FixedOffsetDateTime::try_from` and the conversion back lose nothing, neither the instant nor
    /// the offset.
    #[test]
    fn returns_true_when_the_value_made_a_round_trip_through_utv() -> Result<(), Box<dyn Error>> {
        // GIVEN: the same lunch, after a round trip through UTV's FixedOffsetDateTime
        let tmp_utv_lunch = FixedOffsetDateTime::try_from(DateTime::parse_from_rfc3339("2026-11-05T14:30:00+01:00")?)?;
        let given_round_trip = DateTime::<FixedOffset>::try_from(tmp_utv_lunch)?;

        // SYSTEM UNDER TEST: lunch at 14:30 on 5 November 2026, one hour east of UTC
        let sut = DateTime::parse_from_rfc3339("2026-11-05T14:30:00+01:00")?;

        // WHEN
        let actual_equal = sut == given_round_trip;

        // EXPECTATIONS
        let expected_equal = true;

        // THEN
        assert_eq!(expected_equal, actual_equal);
        Ok(())
    }

    /// Verifies that DateTime's `==` is true when only the offsets differ.
    /// Compared with: UTV's `FixedOffsetDateTime` equality includes the wall time and the offset, so 14:30+01:00 and
    /// 13:30+00:00 are different values; only their `to_instant()` results are equal.
    #[test]
    fn returns_true_when_only_the_offsets_differ() -> Result<(), Box<dyn Error>> {
        // GIVEN: the same moment, written at UTC
        let given_at_utc = DateTime::parse_from_rfc3339("2026-11-05T13:30:00+00:00")?;

        // SYSTEM UNDER TEST: 14:30 on 5 November 2026, one hour east of UTC
        let sut = DateTime::parse_from_rfc3339("2026-11-05T14:30:00+01:00")?;

        // WHEN
        let actual_equal = sut == given_at_utc;

        // EXPECTATIONS: equal, where UTV's values are not
        let expected_equal = true;

        // THEN
        assert_eq!(expected_equal, actual_equal);
        Ok(())
    }
}
