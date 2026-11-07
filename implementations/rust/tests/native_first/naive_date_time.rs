//! What chrono's `NaiveDateTime` does, compared with UTV's `CivilDateTime`.

use std::error::Error;

use chrono::{NaiveDate, NaiveDateTime, TimeDelta};
use utv::CivilDateTime;

mod checked_add_signed {
    use super::*;

    /// Verifies that NaiveDateTime::checked_add_signed carries into the next day when the hours pass midnight.
    /// Compared with: `CivilDateTime::add_hours` gives the same wall time.
    #[test]
    fn carries_into_the_next_day_when_the_hours_pass_midnight() -> Result<(), Box<dyn Error>> {
        // GIVEN: two hours, past midnight
        let given_hours = TimeDelta::hours(2);

        // SYSTEM UNDER TEST: 23:30 on 5 November 2026
        let sut = NaiveDate::from_ymd_opt(2026, 11, 5).unwrap().and_hms_opt(23, 30, 0).unwrap();

        // WHEN
        let actual_date_time = sut.checked_add_signed(given_hours);

        // EXPECTATIONS: the wall time UTV computes for the same addition
        let expected_date_time = NaiveDateTime::try_from(CivilDateTime::of(2026, 11, 5, 23, 30)?.add_hours(2))?;

        // THEN
        assert_eq!(Some(expected_date_time), actual_date_time);
        Ok(())
    }
}
