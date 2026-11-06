//! What `std::ops::RangeFrom` (`a..`) does over chrono's `NaiveDate`, compared with UTV's
//! `Interval::of` with an unbounded end. std has no range whose end is unknown, which UTV writes
//! as `Bound::Unknown`; there is no native-first test for it.

use std::error::Error;

use chrono::NaiveDate;
use utv::{Bound, CivilDate, Interval, Truth};

mod contains {
    use super::*;

    /// Verifies that RangeFrom::contains is true when the day is any later day.
    /// Compared with: `Interval::of(Bound::Included(..), Bound::Unbounded)` says the same, as `Truth::Definitely`;
    /// with `Bound::Unknown` it says `Truth::Possibly`, which `a..` cannot express.
    #[test]
    fn returns_true_when_the_day_is_any_later_day() -> Result<(), Box<dyn Error>> {
        // GIVEN: 1 January 3000
        let given_day = NaiveDate::from_ymd_opt(3000, 1, 1).unwrap();

        // SYSTEM UNDER TEST: from 1 November 2026 on, with no end at all
        let sut = NaiveDate::from_ymd_opt(2026, 11, 1).unwrap()..;

        // WHEN
        let actual_contains = sut.contains(&given_day);

        // EXPECTATIONS: UTV's answer for the same interval and day
        let tmp_open_ended = Interval::of(Bound::Included(CivilDate::of(2026, 11, 1)?), Bound::Unbounded)?;
        let expected_contains = tmp_open_ended.contains(&CivilDate::of(3000, 1, 1)?) == Truth::Definitely;

        // THEN
        assert_eq!(expected_contains, actual_contains);
        Ok(())
    }
}
