//! How to use `FixedOffsetDateTime`, a wall time pinned to a UTC offset (but to no zone), compared
//! with chrono's `DateTime<FixedOffset>`.

use std::error::Error;

use chrono::{DateTime, FixedOffset, TimeZone, Utc};
use utv::{CivilDateTime, Diagnostics, FixedOffsetDateTime, Instant};

mod try_from {
    use super::*;

    /// Verifies that FixedOffsetDateTime::try_from keeps the wall time and the offset of a `DateTime<FixedOffset>`.
    /// Compared with: the `DateTime<FixedOffset>` it was made from has the same wall time and offset; converting it
    /// to an `Instant` instead would keep only the moment.
    #[test]
    fn keeps_the_wall_time_and_offset_when_given_a_native_value() -> Result<(), Box<dyn Error>> {
        // GIVEN: lunch at 14:30 on 5 November 2026, one hour east of UTC
        let given_lunch = FixedOffset::east_opt(3600).unwrap().with_ymd_and_hms(2026, 11, 5, 14, 30, 0).unwrap();

        // WHEN
        let actual_lunch = FixedOffsetDateTime::try_from(given_lunch)?;

        // EXPECTATIONS: the same wall time and offset as the native value
        let expected_wall_time = CivilDateTime::of_hms(2026, 11, 5, 14, 30, 0)?;
        let expected_offset_seconds = 3600;

        // THEN
        assert_eq!(expected_wall_time, actual_lunch.date_time());
        assert_eq!(expected_offset_seconds, actual_lunch.offset_seconds());
        Ok(())
    }
}

mod try_into {
    use super::*;

    // BEGIN round-trip
    /// Verifies that converting a FixedOffsetDateTime into a `DateTime<FixedOffset>` gives back exactly the native
    /// value it was made from.
    /// Compared with: chrono's `==` on `DateTime` compares only the instant, so the offset is compared on its own.
    #[test]
    fn returns_the_original_when_the_value_came_from_a_date_time() -> Result<(), Box<dyn Error>> {
        // GIVEN: lunch at 14:30 on 5 November 2026, one hour east of UTC
        let given_lunch = DateTime::parse_from_rfc3339("2026-11-05T14:30:00+01:00")?;

        // SYSTEM UNDER TEST: the same lunch as a UTV value
        let sut = FixedOffsetDateTime::try_from(given_lunch)?;

        // WHEN
        let actual_result: Result<DateTime<FixedOffset>, Diagnostics> = sut.try_into();

        // EXPECTATIONS: the very same native value, instant and offset
        let expected_lunch = DateTime::parse_from_rfc3339("2026-11-05T14:30:00+01:00")?;
        let expected_offset = FixedOffset::east_opt(3600).unwrap();

        // THEN
        let actual_lunch = actual_result?;
        assert_eq!(expected_lunch, actual_lunch);
        assert_eq!(expected_offset, *actual_lunch.offset());
        Ok(())
    }
    // END round-trip
}

mod to_instant {
    use super::*;

    /// Verifies that FixedOffsetDateTime::to_instant returns the instant the wall time and offset describe.
    /// Compared with: `DateTime<FixedOffset>::with_timezone(&Utc)` names the same moment.
    #[test]
    fn returns_the_utc_moment_when_the_offset_is_known() -> Result<(), Box<dyn Error>> {
        // SYSTEM UNDER TEST: 14:30 on 5 November 2026, one hour east of UTC
        let sut = CivilDateTime::of(2026, 11, 5, 14, 30)?.at_offset(3600)?;

        // WHEN
        let actual_instant = sut.to_instant();

        // EXPECTATIONS: the moment chrono computes for the same value
        let tmp_native = FixedOffset::east_opt(3600).unwrap().with_ymd_and_hms(2026, 11, 5, 14, 30, 0).unwrap();
        let expected_instant = Instant::try_from(tmp_native.with_timezone(&Utc))?;

        // THEN
        assert_eq!(expected_instant, actual_instant);
        Ok(())
    }
}
