//! What chrono's `NaiveTime` does, compared with UTV's `CivilTime`.

use std::error::Error;

use chrono::{NaiveTime, TimeDelta};
use utv::{CivilDateTime, CivilTime};

mod to_string {
    use super::*;

    /// Verifies that NaiveTime's `to_string` writes seconds even when none were given.
    /// Compared with: `CivilTime::of(9, 30)` remembers that no seconds were given, and writes "09:30".
    #[test]
    fn writes_seconds_when_no_seconds_were_given() -> Result<(), Box<dyn Error>> {
        // SYSTEM UNDER TEST: half past nine, made with zero seconds because chrono needs some
        let sut = NaiveTime::from_hms_opt(9, 30, 0).unwrap();

        // WHEN
        let actual_text = sut.to_string();

        // EXPECTATIONS: UTV's text for the same time, padded with zero seconds
        let expected_text = format!("{}:00", CivilTime::of(9, 30)?);

        // THEN
        assert_eq!(expected_text, actual_text);
        Ok(())
    }
}

mod overflowing_add_signed {
    use super::*;

    /// Verifies that NaiveTime::overflowing_add_signed wraps around midnight and hands back the lost day as seconds
    /// when the hours pass midnight.
    /// Compared with: `CivilTime` has no arithmetic at all; `CivilDateTime::add_hours` carries into the next day and
    /// keeps the date.
    #[test]
    fn wraps_and_returns_the_lost_day_when_the_hours_pass_midnight() -> Result<(), Box<dyn Error>> {
        // GIVEN: two hours, past midnight
        let given_hours = TimeDelta::hours(2);

        // SYSTEM UNDER TEST: 23:30
        let sut = NaiveTime::from_hms_opt(23, 30, 0).unwrap();

        // WHEN
        let (actual_time, actual_overflow_seconds) = sut.overflowing_add_signed(given_hours);

        // EXPECTATIONS: the time of day UTV reaches, and the 86,400 seconds of the next date UTV also reaches
        let expected_time = NaiveTime::from(CivilDateTime::of(2026, 11, 5, 23, 30)?.add_hours(2).time());
        let expected_overflow_seconds = 86_400;

        // THEN
        assert_eq!(expected_time, actual_time);
        assert_eq!(expected_overflow_seconds, actual_overflow_seconds);
        Ok(())
    }
}
