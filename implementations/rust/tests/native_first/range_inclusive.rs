//! What `std::ops::RangeInclusive` (`a..=b`) does over chrono's `NaiveDate`, compared with UTV's
//! `Interval::closed`.

use std::error::Error;

use chrono::NaiveDate;
use utv::{CivilDate, Interval, Truth};

mod contains {
    use super::*;

    /// Verifies that RangeInclusive::contains is true when the day is the included end.
    /// Compared with: `Interval::closed(..).contains(..)` says the same, as `Truth::Definitely`.
    #[test]
    fn returns_true_when_the_day_is_the_included_end() -> Result<(), Box<dyn Error>> {
        // GIVEN: 30 November 2026, the last day of the range
        let given_day = NaiveDate::from_ymd_opt(2026, 11, 30).unwrap();

        // SYSTEM UNDER TEST: November 2026, from the 1st to the 30th, both included
        let sut = NaiveDate::from_ymd_opt(2026, 11, 1).unwrap()..=NaiveDate::from_ymd_opt(2026, 11, 30).unwrap();

        // WHEN
        let actual_contains = sut.contains(&given_day);

        // EXPECTATIONS: UTV's answer for the same interval and day
        let tmp_november = Interval::closed(CivilDate::of(2026, 11, 1)?, CivilDate::of(2026, 11, 30)?)?;
        let expected_contains = tmp_november.contains(&CivilDate::of(2026, 11, 30)?) == Truth::Definitely;

        // THEN
        assert_eq!(expected_contains, actual_contains);
        Ok(())
    }
}
