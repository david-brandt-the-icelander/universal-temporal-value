//! How to use `ElapsedSeconds`, SI seconds as a stopwatch measures them, compared with chrono's
//! `TimeDelta`, which has one kind of second for every purpose.

use chrono::TimeDelta;
use utv::{ElapsedSeconds, Period, PosixSeconds, Value};

mod of {
    use super::*;

    /// Verifies that ElapsedSeconds::of holds exactly the elapsed seconds it is given.
    /// Compared with: `TimeDelta::minutes(90).num_seconds()` is the same number.
    #[test]
    fn holds_the_seconds_when_given_ninety_minutes() {
        // GIVEN: 90 minutes of elapsed time, in seconds
        let given_seconds = 5_400;

        // WHEN
        let actual_amount = ElapsedSeconds::of(given_seconds);

        // EXPECTATIONS: the seconds chrono counts in 90 minutes, labelled as SI seconds
        let expected_seconds = i128::from(TimeDelta::minutes(90).num_seconds());
        let expected_text = "5400 s (SI)";

        // THEN
        assert_eq!(expected_seconds, actual_amount.numerator());
        assert_eq!(expected_text, actual_amount.to_string());
    }

    /// Verifies that ElapsedSeconds::of creates an amount that is neither a calendar day nor POSIX seconds when
    /// given 86,400 seconds.
    /// Compared with: `TimeDelta::days(1)` is all three at once.
    #[test]
    fn is_not_a_calendar_day_when_given_86400_seconds() {
        // GIVEN: exactly 86,400 SI seconds
        let given_seconds = 86_400;

        // WHEN
        let actual_amount = ElapsedSeconds::of(given_seconds);

        // EXPECTATIONS: different from a calendar day and from 86,400 POSIX seconds
        let expected_calendar_day = Period::days(1);
        let expected_posix_day = PosixSeconds::of(86_400);

        // THEN
        assert_ne!(Value::Period(expected_calendar_day), Value::ElapsedSeconds(actual_amount));
        assert_ne!(Value::PosixSeconds(expected_posix_day), Value::ElapsedSeconds(actual_amount));
    }
}
