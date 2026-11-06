//! How to use `CivilYearMonth`, a month of a Gregorian year with no day, compared with chrono, which
//! has no year-month type and invents day 1 of the month with a `NaiveDate`.

use std::error::Error;

use chrono::{Datelike, Months, NaiveDate};
use utv::CivilYearMonth;

mod of {
    use super::*;

    /// Verifies that CivilYearMonth::of creates a month without inventing a day when given a year and a month.
    /// Compared with: `NaiveDate::from_ymd_opt(2026, 11, 1)` has the same year and month, plus a day nobody asked for.
    #[test]
    fn creates_a_month_without_a_day_when_given_a_year_and_a_month() -> Result<(), Box<dyn Error>> {
        // GIVEN: November 2026
        let given_year = 2026;
        let given_month = 11;

        // WHEN
        let actual_month = CivilYearMonth::of(given_year, given_month)?;

        // EXPECTATIONS: the year and month of chrono's stand-in, and text with no day in it
        let expected_stand_in = NaiveDate::from_ymd_opt(2026, 11, 1).unwrap();
        let expected_text = "2026-11";

        // THEN
        assert_eq!(i128::from(expected_stand_in.year()), actual_month.year());
        assert_eq!(expected_stand_in.month(), actual_month.month());
        assert_eq!(expected_text, actual_month.to_string());
        Ok(())
    }
}

mod add_months {
    use super::*;

    /// Verifies that CivilYearMonth::add_months returns the later month, carrying into the next year.
    /// Compared with: `NaiveDate::checked_add_months` on the stand-in day 1 lands in the same month.
    #[test]
    fn returns_the_later_month_when_the_months_cross_a_year_end() -> Result<(), Box<dyn Error>> {
        // GIVEN: three months, past December
        let given_months = 3;

        // SYSTEM UNDER TEST: November 2026
        let sut = CivilYearMonth::of(2026, 11)?;

        // WHEN
        let actual_month = sut.add_months(given_months);

        // EXPECTATIONS: the month chrono lands in from the stand-in day
        let tmp_stand_in = NaiveDate::from_ymd_opt(2026, 11, 1).unwrap().checked_add_months(Months::new(3)).unwrap();
        let expected_month = CivilYearMonth::of(i128::from(tmp_stand_in.year()), tmp_stand_in.month())?;

        // THEN
        assert_eq!(expected_month, actual_month);
        Ok(())
    }
}
