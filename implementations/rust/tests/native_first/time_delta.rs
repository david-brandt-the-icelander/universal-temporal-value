//! What chrono's `TimeDelta` (alias `chrono::Duration`) does, compared with UTV's three kinds of
//! amount: `Period`, `PosixSeconds` and `ElapsedSeconds`.

use std::error::Error;

use chrono::TimeDelta;
use utv::{ElapsedSeconds, PosixSeconds};

mod days {
    use super::*;

    /// Verifies that TimeDelta::days(1) equals 86,400 seconds.
    /// Compared with: UTV keeps `Period::days(1)`, `PosixSeconds::of(86_400)` and `ElapsedSeconds::of(86_400)` apart,
    /// because a calendar day can be 23 or 25 hours long.
    #[test]
    fn equals_86400_seconds_when_given_one_day() -> Result<(), Box<dyn Error>> {
        // GIVEN: one day
        let given_days = 1;

        // WHEN
        let actual_day = TimeDelta::days(given_days);

        // EXPECTATIONS: the POSIX seconds UTV counts in a nominal day
        let expected_day = TimeDelta::seconds(i64::try_from(PosixSeconds::of(86_400).numerator())?);

        // THEN
        assert_eq!(expected_day, actual_day);
        Ok(())
    }
}

mod minutes {
    use super::*;

    /// Verifies that TimeDelta::minutes counts 60 seconds to the minute.
    /// Compared with: `ElapsedSeconds::of(5_400)` holds the same number of seconds as an exact SI amount.
    #[test]
    fn counts_sixty_seconds_a_minute_when_given_ninety_minutes() {
        // GIVEN: 90 minutes
        let given_minutes = 90;

        // WHEN
        let actual_duration = TimeDelta::minutes(given_minutes);

        // EXPECTATIONS: the seconds UTV holds for the same duration
        let expected_seconds = ElapsedSeconds::of(5_400).numerator();

        // THEN
        assert_eq!(expected_seconds, i128::from(actual_duration.num_seconds()));
    }
}

mod div {
    use super::*;

    /// Verifies that dividing a TimeDelta rounds to whole nanoseconds when the quotient has no finite decimal.
    /// Compared with: `PosixSeconds::of_ratio(1, 3)` is exactly one third of a second.
    #[test]
    fn rounds_to_nanoseconds_when_dividing_a_second_by_three() {
        // GIVEN: three parts
        let given_divisor = 3;

        // SYSTEM UNDER TEST: one second
        let sut = TimeDelta::seconds(1);

        // WHEN
        let actual_third = sut / given_divisor;

        // EXPECTATIONS: 333,333,333 nanoseconds, where UTV keeps 1/3
        let expected_third = TimeDelta::nanoseconds(333_333_333);

        // THEN
        assert_eq!(expected_third, actual_third);
    }
}
