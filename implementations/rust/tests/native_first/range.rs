//! What `std::ops::Range` (`a..b`) does over chrono's `NaiveDate`, compared with UTV's
//! `Interval::half_open`.

use std::error::Error;

use chrono::NaiveDate;
use utv::{CivilDate, Interval, Truth};

mod contains {
    use super::*;

    /// Verifies that Range::contains is true when the day is between the ends.
    /// Compared with: `Interval::half_open(..).contains(..)` says the same, as `Truth::Definitely`.
    #[test]
    fn returns_true_when_the_day_is_between_the_ends() -> Result<(), Box<dyn Error>> {
        // GIVEN: 15 November 2026
        let given_day = NaiveDate::from_ymd_opt(2026, 11, 15).unwrap();

        // SYSTEM UNDER TEST: November 2026, from the 1st up to but not including 1 December
        let sut = NaiveDate::from_ymd_opt(2026, 11, 1).unwrap()..NaiveDate::from_ymd_opt(2026, 12, 1).unwrap();

        // WHEN
        let actual_contains = sut.contains(&given_day);

        // EXPECTATIONS: UTV's answer for the same interval and day
        let tmp_november = Interval::half_open(CivilDate::of(2026, 11, 1)?, CivilDate::of(2026, 12, 1)?)?;
        let expected_contains = tmp_november.contains(&CivilDate::of(2026, 11, 15)?) == Truth::Definitely;

        // THEN
        assert_eq!(expected_contains, actual_contains);
        Ok(())
    }
}
