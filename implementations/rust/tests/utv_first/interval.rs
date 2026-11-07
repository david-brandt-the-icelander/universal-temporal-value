//! How to use `Interval`, a range of dates or instants whose ends say whether they are included,
//! unbounded or unknown, compared with `std::ops`'s ranges over chrono's `NaiveDate`: `a..b`,
//! `a..=b` and `a..` cover the ends that are known, and an end nobody knows yet has no range type.

use std::error::Error;

use chrono::NaiveDate;
use utv::{Bound, CivilDate, Interval, Truth};

mod half_open {
    use super::*;

    // BEGIN ranges
    /// Verifies that a half-open Interval definitely contains a day between its ends.
    /// Compared with: `(nov1..dec1).contains(&day)` on `NaiveDate`s says the same.
    #[test]
    fn definitely_contains_the_day_when_the_day_is_between_the_ends() -> Result<(), Box<dyn Error>> {
        // GIVEN: 15 November 2026
        let given_day = CivilDate::of(2026, 11, 15)?;

        // SYSTEM UNDER TEST: November 2026, from the 1st up to but not including 1 December
        let sut = Interval::half_open(CivilDate::of(2026, 11, 1)?, CivilDate::of(2026, 12, 1)?)?;

        // WHEN
        let actual_contains = sut.contains(&given_day);

        // EXPECTATIONS: the answer std's half-open range gives
        let tmp_november = NaiveDate::from_ymd_opt(2026, 11, 1).unwrap()..NaiveDate::from_ymd_opt(2026, 12, 1).unwrap();
        let tmp_native_contains = tmp_november.contains(&NaiveDate::from_ymd_opt(2026, 11, 15).unwrap());
        let expected_contains = if tmp_native_contains { Truth::Definitely } else { Truth::Impossible };

        // THEN
        assert_eq!(expected_contains, actual_contains);
        Ok(())
    }

    /// Verifies that a half-open Interval cannot contain its excluded end.
    /// Compared with: `(nov1..dec1).contains(&dec1)` is false too.
    #[test]
    fn cannot_contain_the_day_when_the_day_is_the_excluded_end() -> Result<(), Box<dyn Error>> {
        // GIVEN: 1 December 2026, the end of the interval
        let given_day = CivilDate::of(2026, 12, 1)?;

        // SYSTEM UNDER TEST: November 2026, from the 1st up to but not including 1 December
        let sut = Interval::half_open(CivilDate::of(2026, 11, 1)?, CivilDate::of(2026, 12, 1)?)?;

        // WHEN
        let actual_contains = sut.contains(&given_day);

        // EXPECTATIONS: impossible, and written with the end's bracket open
        let expected_contains = Truth::Impossible;
        let expected_text = "[2026-11-01, 2026-12-01)";

        // THEN
        assert_eq!(expected_contains, actual_contains);
        assert_eq!(expected_text, sut.to_string());
        Ok(())
    }
    // END ranges
}

mod closed {
    use super::*;

    /// Verifies that a closed Interval definitely contains its last day.
    /// Compared with: `(nov1..=nov30).contains(&nov30)` on `NaiveDate`s says the same.
    #[test]
    fn definitely_contains_the_day_when_the_day_is_the_included_end() -> Result<(), Box<dyn Error>> {
        // GIVEN: 30 November 2026, the last day of the interval
        let given_day = CivilDate::of(2026, 11, 30)?;

        // SYSTEM UNDER TEST: November 2026, from the 1st to the 30th, both included
        let sut = Interval::closed(CivilDate::of(2026, 11, 1)?, CivilDate::of(2026, 11, 30)?)?;

        // WHEN
        let actual_contains = sut.contains(&given_day);

        // EXPECTATIONS: the answer std's inclusive range gives
        let tmp_november = NaiveDate::from_ymd_opt(2026, 11, 1).unwrap()..=NaiveDate::from_ymd_opt(2026, 11, 30).unwrap();
        let tmp_native_contains = tmp_november.contains(&NaiveDate::from_ymd_opt(2026, 11, 30).unwrap());
        let expected_contains = if tmp_native_contains { Truth::Definitely } else { Truth::Impossible };

        // THEN
        assert_eq!(expected_contains, actual_contains);
        Ok(())
    }
}

mod of {
    use super::*;

    /// Verifies that an Interval with an unbounded end definitely contains any later day.
    /// Compared with: `(nov1..).contains(&day)` on `NaiveDate`s says the same.
    #[test]
    fn definitely_contains_a_later_day_when_the_end_is_unbounded() -> Result<(), Box<dyn Error>> {
        // GIVEN: 1 January 3000
        let given_day = CivilDate::of(3000, 1, 1)?;

        // SYSTEM UNDER TEST: from 1 November 2026 on, with no end at all
        let sut = Interval::of(Bound::Included(CivilDate::of(2026, 11, 1)?), Bound::Unbounded)?;

        // WHEN
        let actual_contains = sut.contains(&given_day);

        // EXPECTATIONS: the answer std's open-ended range gives, and ".." written for the missing end
        let tmp_native_contains = (NaiveDate::from_ymd_opt(2026, 11, 1).unwrap()..).contains(&NaiveDate::from_ymd_opt(3000, 1, 1).unwrap());
        let expected_contains = if tmp_native_contains { Truth::Definitely } else { Truth::Impossible };
        let expected_text = "[2026-11-01, ..)";

        // THEN
        assert_eq!(expected_contains, actual_contains);
        assert_eq!(expected_text, sut.to_string());
        Ok(())
    }

    // BEGIN ranges
    /// Verifies that an Interval with an unknown end only possibly contains a later day.
    /// Compared with: std has no range with an unknown end; an `Option<NaiveDate>` end of `None` has to be read as
    /// `nov1..` (no end), and the answer would wrongly be "yes".
    #[test]
    fn possibly_contains_a_later_day_when_the_end_is_unknown() -> Result<(), Box<dyn Error>> {
        // GIVEN: 1 June 2027
        let given_day = CivilDate::of(2027, 6, 1)?;

        // SYSTEM UNDER TEST: from 1 November 2026, until an end nobody knows yet
        let sut = Interval::of(Bound::Included(CivilDate::of(2026, 11, 1)?), Bound::Unknown)?;

        // WHEN
        let actual_contains = sut.contains(&given_day);

        // EXPECTATIONS: possibly, and "?" written for the unknown end
        let expected_contains = Truth::Possibly;
        let expected_text = "[2026-11-01, ?)";

        // THEN
        assert_eq!(expected_contains, actual_contains);
        assert_eq!(expected_text, sut.to_string());
        Ok(())
    }
    // END ranges
}
