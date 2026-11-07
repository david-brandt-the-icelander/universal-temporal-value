//! How to use `Instant`, an exact moment on the POSIX timescale with no calendar and no zone,
//! compared with chrono's `DateTime<Utc>` and `TimeDelta`, and with `std::time::SystemTime`.

use std::error::Error;
use std::time::{Duration, SystemTime, UNIX_EPOCH};

use chrono::{DateTime, NaiveDateTime, TimeDelta, TimeZone, Utc};
use chrono_tz::America::New_York;
use utv::{CivilDateTime, ElapsedSeconds, Instant, PosixSeconds, Status, TemporalContext};

use crate::fixtures::demo_zone_rules;

mod from_system_time {
    use super::*;

    // BEGIN now
    /// Verifies that Instant::from_system_time takes a clock reading as an input when the program reads the clock.
    /// Compared with: `Utc::now()` and `SystemTime::now()` read the clock themselves; UTV never does, so a test can
    /// fix "now".
    #[test]
    fn returns_the_same_moment_when_given_a_clock_reading() -> Result<(), Box<dyn Error>> {
        // GIVEN: a clock reading, as SystemTime::now() would return it: 13:30:00.123 UTC on 5 November 2026
        let given_reading: SystemTime = UNIX_EPOCH + Duration::new(1_793_885_400, 123_000_000);

        // WHEN
        let actual_now = Instant::from_system_time(given_reading);

        // EXPECTATIONS: the same moment, as chrono reads it from the same SystemTime
        let expected_now = Instant::try_from(DateTime::<Utc>::from(given_reading))?;

        // THEN
        assert_eq!(expected_now, actual_now);
        Ok(())
    }
    // END now
}

mod from_unix_seconds {
    use super::*;

    /// Verifies that Instant::from_unix_seconds returns the Unix epoch when given zero seconds.
    /// Compared with: `DateTime::from_timestamp(0, 0)` is the same moment.
    #[test]
    fn returns_the_epoch_when_given_zero() -> Result<(), Box<dyn Error>> {
        // GIVEN: zero seconds since 1970-01-01T00:00:00Z
        let given_seconds = 0;

        // WHEN
        let actual_instant = Instant::from_unix_seconds(given_seconds);

        // EXPECTATIONS: the moment chrono returns for the same count, written in UTC
        let expected_instant = Instant::try_from(DateTime::from_timestamp(0, 0).unwrap())?;
        let expected_text = "1970-01-01T00:00:00Z";

        // THEN
        assert_eq!(expected_instant, actual_instant);
        assert_eq!(expected_text, actual_instant.to_string());
        Ok(())
    }
}

mod from_unix_seconds_nanos {
    use super::*;

    /// Verifies that Instant::from_unix_seconds_nanos keeps all nine fractional digits when given nanoseconds.
    /// Compared with: `DateTime::from_timestamp` keeps all nine too; chrono stops at nanoseconds, where UTV's
    /// fractions are exact rationals.
    #[test]
    fn keeps_every_nanosecond_when_given_nanoseconds() -> Result<(), Box<dyn Error>> {
        // GIVEN: a moment on 4 November 2026, to the nanosecond
        let given_seconds = 1_793_795_400;
        let given_nanoseconds = 123_456_789;

        // WHEN
        let actual_instant = Instant::from_unix_seconds_nanos(given_seconds, given_nanoseconds);

        // EXPECTATIONS: the moment chrono holds, with all nine digits written out
        let expected_instant = Instant::try_from(DateTime::from_timestamp(1_793_795_400, 123_456_789).unwrap())?;
        let expected_text = "2026-11-04T12:30:00.123456789Z";
        let expected_nanoseconds = 123_456_789;

        // THEN
        assert_eq!(expected_instant, actual_instant);
        assert_eq!(expected_text, actual_instant.to_string());
        assert_eq!(expected_nanoseconds, actual_instant.nanoseconds());
        Ok(())
    }

    /// Verifies that Instant::from_unix_seconds_nanos keeps a fraction of a second when given one.
    /// Compared with: `DateTime::from_timestamp` keeps it too, but `DateTime::timestamp` drops it when counting back.
    #[test]
    fn keeps_the_fraction_when_given_a_quarter_second() -> Result<(), Box<dyn Error>> {
        // GIVEN: 13:30:00.25 on 5 November 2026
        let given_seconds = 1_793_885_400;
        let given_nanoseconds = 250_000_000;

        // WHEN
        let actual_instant = Instant::from_unix_seconds_nanos(given_seconds, given_nanoseconds);

        // EXPECTATIONS: the moment chrono holds, with the quarter second written out
        let expected_instant = Instant::try_from(DateTime::from_timestamp(1_793_885_400, 250_000_000).unwrap())?;
        let expected_text = "2026-11-05T13:30:00.25Z";

        // THEN
        assert_eq!(expected_instant, actual_instant);
        assert_eq!(expected_text, actual_instant.to_string());
        Ok(())
    }
}

mod until {
    use super::*;

    // BEGIN timestamps
    /// Verifies that Instant::until returns the exact number of seconds, as a fraction, between two instants.
    /// Compared with: subtracting two `DateTime<Utc>`s gives a `TimeDelta` of the same length, exact to the
    /// nanosecond; `TimeDelta::seconds(1) / 3` shows where that stops, while `PosixSeconds::of_ratio(1, 3)` is exact.
    #[test]
    fn returns_an_exact_fraction_when_the_instants_are_a_fraction_of_a_second_apart() -> Result<(), Box<dyn Error>> {
        // GIVEN: one hour and 0.75 seconds later
        let given_later = Instant::from_unix_seconds(1_793_889_001);

        // SYSTEM UNDER TEST: 13:30:00.25 on 5 November 2026
        let sut = Instant::from_unix_seconds_nanos(1_793_885_400, 250_000_000);

        // WHEN
        let actual_seconds = sut.until(given_later);

        // EXPECTATIONS: exactly 14403/4 seconds, the length chrono's TimeDelta gives in nanoseconds
        let tmp_delta = DateTime::from_timestamp(1_793_889_001, 0).unwrap() - DateTime::from_timestamp(1_793_885_400, 250_000_000).unwrap();
        let expected_seconds = PosixSeconds::of_ratio(tmp_delta.num_nanoseconds().unwrap().into(), 1_000_000_000)?;
        let expected_text = "3600.75 s (POSIX)";

        // THEN
        assert_eq!(expected_seconds, actual_seconds);
        assert_eq!(expected_text, actual_seconds.to_string());
        Ok(())
    }
    // END timestamps

    /// Verifies that Instant::until returns 23 hours when the instants are noon on both sides of a spring-forward night.
    /// Compared with: subtracting the two chrono-tz noons gives the same 82,800 seconds; the calendar day is 23 hours
    /// long.
    #[test]
    fn returns_23_hours_when_the_day_between_loses_an_hour_to_dst() -> Result<(), Box<dyn Error>> {
        // GIVEN: noon on 8 March 2026 in New York, daylight time
        let given_next_noon = CivilDateTime::of(2026, 3, 8, 12, 0)?.at_offset(-4 * 3600)?.to_instant();

        // SYSTEM UNDER TEST: noon on 7 March 2026 in New York, standard time
        let sut = CivilDateTime::of(2026, 3, 7, 12, 0)?.at_offset(-5 * 3600)?.to_instant();

        // WHEN
        let actual_seconds = sut.until(given_next_noon);

        // EXPECTATIONS: 82,800 seconds, as chrono's TimeDelta counts them
        let tmp_delta = New_York.with_ymd_and_hms(2026, 3, 8, 12, 0, 0).unwrap() - New_York.with_ymd_and_hms(2026, 3, 7, 12, 0, 0).unwrap();
        let expected_seconds = PosixSeconds::of(tmp_delta.num_seconds());

        // THEN
        assert_eq!(expected_seconds, actual_seconds);
        Ok(())
    }
}

mod add {
    use super::*;

    /// Verifies that Instant::add with POSIX seconds moves the instant by exactly that many seconds.
    /// Compared with: `DateTime + TimeDelta::seconds(86_400)` lands on the same moment: 13:00 in New York, not noon.
    #[test]
    fn moves_by_exactly_those_seconds_when_given_posix_seconds() -> Result<(), Box<dyn Error>> {
        // GIVEN: 86,400 POSIX seconds, one nominal day
        let given_seconds = PosixSeconds::of(86_400);

        // SYSTEM UNDER TEST: noon on 7 March 2026 in New York, the day before clocks spring forward
        let sut = CivilDateTime::of(2026, 3, 7, 12, 0)?.at_offset(-5 * 3600)?.to_instant();

        // WHEN
        let actual_instant = sut.add(given_seconds);

        // EXPECTATIONS: the moment chrono computes for the same addition
        let tmp_native = New_York.with_ymd_and_hms(2026, 3, 7, 12, 0, 0).unwrap() + TimeDelta::seconds(86_400);
        let expected_instant = Instant::try_from(tmp_native.with_timezone(&Utc))?;

        // THEN
        assert_eq!(expected_instant, actual_instant);
        Ok(())
    }
}

mod add_elapsed {
    use super::*;

    // BEGIN day-vs-24h
    /// Verifies that Instant::add_elapsed returns unsupported when no leap-second table is available.
    /// Compared with: `TimeDelta` has one kind of second, so chrono cannot tell SI seconds from POSIX seconds.
    #[test]
    fn returns_unsupported_when_given_si_seconds_without_a_leap_second_table() -> Result<(), Box<dyn Error>> {
        // GIVEN: 86,400 SI seconds, as a physicist measures a day
        let given_seconds = ElapsedSeconds::of(86_400);

        // SYSTEM UNDER TEST: noon on 7 March 2026 in New York
        let sut = CivilDateTime::of(2026, 3, 7, 12, 0)?.at_offset(-5 * 3600)?.to_instant();

        // WHEN
        let actual_outcome = sut.add_elapsed(given_seconds);

        // EXPECTATIONS: unsupported, because SI seconds on a POSIX timescale need leap seconds
        let expected_status = Status::Unsupported;
        let expected_code = "needs-leap-seconds";

        // THEN
        assert_eq!(expected_status, actual_outcome.status());
        assert!(actual_outcome.diagnostics().contains(expected_code));
        Ok(())
    }
    // END day-vs-24h
}

mod in_zone {
    use super::*;

    // BEGIN meeting-in-paris
    /// Verifies that Instant::in_zone returns the wall time and offset in the zone when the zone has rules for it.
    /// Compared with: `DateTime::with_timezone(&New_York)` gives the same wall time from chrono-tz's rules.
    #[test]
    fn returns_the_local_wall_time_when_the_zone_has_rules() -> Result<(), Box<dyn Error>> {
        // GIVEN: New York
        let given_zone_id = "America/New_York";

        // SETUP: the pinned demo zone rules, never this machine's tz database
        let env_context = TemporalContext::with(demo_zone_rules());

        // SYSTEM UNDER TEST: 07:00 UTC on 1 June 2027, when the meeting starts in Paris
        let sut = Instant::try_from(Utc.with_ymd_and_hms(2027, 6, 1, 7, 0, 0).unwrap())?;

        // WHEN
        let actual_outcome = sut.in_zone(given_zone_id, &env_context);

        // EXPECTATIONS: the wall time chrono-tz computes, with the offset UTV asserts alongside it
        let expected_wall_time = Utc.with_ymd_and_hms(2027, 6, 1, 7, 0, 0).unwrap().with_timezone(&New_York).naive_local();
        let expected_text = "2027-06-01T03:00:00-04:00[America/New_York]";

        // THEN
        let actual_there = actual_outcome.single()?;
        assert_eq!(expected_wall_time, NaiveDateTime::try_from(actual_there.date_time())?);
        assert_eq!(expected_text, actual_there.to_string());
        Ok(())
    }
    // END meeting-in-paris
}
