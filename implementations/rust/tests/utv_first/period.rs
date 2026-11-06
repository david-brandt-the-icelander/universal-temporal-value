//! How to use `Period`, an amount of calendar components such as days and months, compared with
//! chrono's `TimeDelta` (alias `chrono::Duration`), which only counts seconds and nanoseconds and so
//! cannot tell a calendar day from 86,400 seconds.

use chrono::TimeDelta;
use utv::{Period, PosixSeconds, Value};

mod days {
    use super::*;

    /// Verifies that Period::days creates a calendar day, which is not a number of seconds.
    /// Compared with: `TimeDelta::days(1) == TimeDelta::seconds(86_400)` is true, so the difference is lost.
    #[test]
    fn is_not_equal_to_86400_seconds_when_given_one_day() {
        // GIVEN: one day
        let given_days = 1;

        // WHEN
        let actual_period = Period::days(given_days);

        // EXPECTATIONS: a calendar component, written in ISO 8601, and a different value from chrono's 86,400 seconds
        let expected_text = "P1D";
        let expected_other_amount = PosixSeconds::of(TimeDelta::days(1).num_seconds());

        // THEN
        assert_eq!(expected_text, actual_period.to_string());
        assert_ne!(Value::PosixSeconds(expected_other_amount), Value::Period(actual_period));
    }
}

mod months {
    use super::*;

    /// Verifies that Period::months creates a calendar month, distinct from a minute.
    /// Compared with: `TimeDelta` has no months at all, and chrono's `Months` is a separate count for date
    /// arithmetic; ISO 8601 writes P1M for a month and PT1M for a minute.
    #[test]
    fn writes_p1m_when_given_one_month() {
        // GIVEN: one month
        let given_months = 1;

        // WHEN
        let actual_period = Period::months(given_months);

        // EXPECTATIONS: P1M, never PT1M
        let expected_text = "P1M";
        let expected_minute_text = Period::minutes(1).to_string();

        // THEN
        assert_eq!(expected_text, actual_period.to_string());
        assert_ne!(expected_minute_text, actual_period.to_string());
    }
}
