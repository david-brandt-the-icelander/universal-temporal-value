//! What chrono's `FixedOffset` zone does, compared with UTV's `CivilDateTime::at_offset` and
//! `FixedOffsetDateTime`.

use std::error::Error;

use chrono::{DateTime, FixedOffset, TimeZone};
use utv::CivilDateTime;

mod with_ymd_and_hms {
    use super::*;

    /// Verifies that FixedOffset::with_ymd_and_hms pins the wall time to the offset when given one.
    /// Compared with: `CivilDateTime::of(2026, 11, 5, 14, 30)?.at_offset(3600)` is the same value, and its
    /// `to_instant()` is the same moment.
    #[test]
    fn pins_the_wall_time_to_the_offset_when_given_an_offset() -> Result<(), Box<dyn Error>> {
        // GIVEN: 14:30 on 5 November 2026
        let given_year = 2026;
        let given_month = 11;
        let given_day = 5;
        let given_hour = 14;
        let given_minute = 30;
        let given_second = 0;

        // SYSTEM UNDER TEST: one hour east of UTC
        let sut = FixedOffset::east_opt(3600).unwrap();

        // WHEN
        let actual_date_time = sut.with_ymd_and_hms(given_year, given_month, given_day, given_hour, given_minute, given_second).unwrap();

        // EXPECTATIONS: the value UTV holds for the same wall time and offset; chrono's == compares only the instant,
        // so the offset is compared on its own
        let expected_date_time = DateTime::<FixedOffset>::try_from(CivilDateTime::of(2026, 11, 5, 14, 30)?.at_offset(3600)?)?;

        // THEN
        assert_eq!(expected_date_time, actual_date_time);
        assert_eq!(expected_date_time.offset(), actual_date_time.offset());
        Ok(())
    }
}
