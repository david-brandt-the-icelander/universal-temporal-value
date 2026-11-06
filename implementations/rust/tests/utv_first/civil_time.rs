//! How to use `CivilTime`, a wall-clock time of day with no date and no zone, compared with
//! chrono's `NaiveTime`.

use std::error::Error;

use chrono::{NaiveTime, Timelike};
use utv::CivilTime;

mod of {
    use super::*;

    // BEGIN time-of-day
    /// Verifies that CivilTime::of keeps the time to the minute when no seconds are given.
    /// Compared with: `NaiveTime::from_hms_opt(9, 30, 0)` always has seconds, and prints them: `09:30:00`.
    #[test]
    fn writes_no_seconds_when_no_seconds_are_given() -> Result<(), Box<dyn Error>> {
        // GIVEN: half past nine, to the minute
        let given_hour = 9;
        let given_minute = 30;

        // WHEN
        let actual_time = CivilTime::of(given_hour, given_minute)?;

        // EXPECTATIONS: the same hour and minute as chrono, and no seconds in the text
        let expected_time = NaiveTime::from_hms_opt(9, 30, 0).unwrap();
        let expected_text = "09:30";

        // THEN
        assert_eq!(expected_time.hour(), actual_time.hour());
        assert_eq!(expected_time.minute(), actual_time.minute());
        assert_eq!(expected_text, actual_time.to_string());
        Ok(())
    }
    // END time-of-day
}

mod of_hms {
    use super::*;

    /// Verifies that CivilTime::of_hms keeps the seconds when they are given.
    /// Compared with: `NaiveTime::from_hms_opt(9, 30, 0)` is the same time; UTV also remembers that a second was given.
    #[test]
    fn writes_the_seconds_when_seconds_are_given() -> Result<(), Box<dyn Error>> {
        // GIVEN: half past nine, to the second
        let given_hour = 9;
        let given_minute = 30;
        let given_second = 0;

        // WHEN
        let actual_time = CivilTime::of_hms(given_hour, given_minute, given_second)?;

        // EXPECTATIONS: the time chrono holds, with its seconds written out
        let expected_time = CivilTime::try_from(NaiveTime::from_hms_opt(9, 30, 0).unwrap())?;
        let expected_text = "09:30:00";

        // THEN
        assert_eq!(expected_time, actual_time);
        assert_eq!(expected_text, actual_time.to_string());
        Ok(())
    }
}
