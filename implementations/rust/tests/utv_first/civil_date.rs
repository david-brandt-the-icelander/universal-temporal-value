//! How to use `CivilDate`, a day in the proleptic Gregorian calendar with no time and no zone,
//! compared with chrono's `NaiveDate`. Where the two agree, the expected value is computed with
//! `NaiveDate`.

use std::error::Error;

use chrono::{Datelike, Days, Months, NaiveDate};
use utv::{CivilDate, Diagnostics, Period, Status, Weekday};

mod of {
    use super::*;

    // BEGIN a-date
    /// Verifies that CivilDate::of creates the date when the fields name a real day.
    /// Compared with: `NaiveDate::from_ymd_opt(2026, 11, 5)` names the same day.
    #[test]
    fn creates_the_date_when_the_fields_name_a_real_day() -> Result<(), Box<dyn Error>> {
        // GIVEN: 5 November 2026
        let given_year = 2026;
        let given_month = 11;
        let given_day = 5;

        // WHEN
        let actual_date = CivilDate::of(given_year, given_month, given_day)?;

        // EXPECTATIONS: the same day as chrono's NaiveDate
        let expected_date = CivilDate::from(NaiveDate::from_ymd_opt(2026, 11, 5).unwrap());
        let expected_text = "2026-11-05";

        // THEN
        assert_eq!(expected_date, actual_date);
        assert_eq!(expected_text, actual_date.to_string());
        Ok(())
    }
    // END a-date

    /// Verifies that CivilDate::of accepts 29 February when the year is a leap year.
    /// Compared with: `NaiveDate::from_ymd_opt(2024, 2, 29)` accepts it too.
    #[test]
    fn accepts_the_leap_day_when_the_year_is_a_leap_year() -> Result<(), Box<dyn Error>> {
        // GIVEN: 29 February 2024, a year divisible by 4
        let given_year = 2024;
        let given_month = 2;
        let given_day = 29;

        // WHEN
        let actual_date = CivilDate::of(given_year, given_month, given_day)?;

        // EXPECTATIONS: the same day as chrono's NaiveDate
        let expected_date = CivilDate::from(NaiveDate::from_ymd_opt(2024, 2, 29).unwrap());

        // THEN
        assert_eq!(expected_date, actual_date);
        Ok(())
    }

    /// Verifies that CivilDate::of accepts 29 February when the year is a century divisible by 400.
    /// Compared with: `NaiveDate::from_ymd_opt(2000, 2, 29)` accepts it too.
    #[test]
    fn accepts_the_leap_day_when_the_century_is_divisible_by_400() -> Result<(), Box<dyn Error>> {
        // GIVEN: 29 February 2000, a century year divisible by 400
        let given_year = 2000;
        let given_month = 2;
        let given_day = 29;

        // WHEN
        let actual_date = CivilDate::of(given_year, given_month, given_day)?;

        // EXPECTATIONS: the same day as chrono's NaiveDate
        let expected_date = CivilDate::from(NaiveDate::from_ymd_opt(2000, 2, 29).unwrap());

        // THEN
        assert_eq!(expected_date, actual_date);
        Ok(())
    }

    // BEGIN feb-30
    /// Verifies that CivilDate::of returns an error, with the reason, when the day is 30 February.
    /// Compared with: `NaiveDate::from_ymd_opt(2026, 2, 30)` returns `None` and gives no reason.
    #[test]
    fn returns_day_out_of_range_when_the_day_is_30_february() {
        // GIVEN: 30 February 2026
        let given_year = 2026;
        let given_month = 2;
        let given_day = 30;

        // WHEN
        let actual_result = CivilDate::of(given_year, given_month, given_day);

        // EXPECTATIONS: no date, as chrono finds too, because February 2026 has 28 days
        let expected_is_err = NaiveDate::from_ymd_opt(2026, 2, 30).is_none();
        let expected_codes = vec!["day-out-of-range"];

        // THEN
        assert_eq!(expected_is_err, actual_result.is_err());
        assert_eq!(expected_codes, actual_result.unwrap_err().codes());
    }
    // END feb-30

    /// Verifies that CivilDate::of returns an error when 29 February falls in a common year.
    /// Compared with: `NaiveDate::from_ymd_opt(2026, 2, 29)` returns `None` too.
    #[test]
    fn returns_day_out_of_range_when_29_february_falls_in_a_common_year() {
        // GIVEN: 29 February 2026, a year not divisible by 4
        let given_year = 2026;
        let given_month = 2;
        let given_day = 29;

        // WHEN
        let actual_result = CivilDate::of(given_year, given_month, given_day);

        // EXPECTATIONS: no date, rather than one rolled over to 1 March
        let expected_is_err = NaiveDate::from_ymd_opt(2026, 2, 29).is_none();
        let expected_codes = vec!["day-out-of-range"];

        // THEN
        assert_eq!(expected_is_err, actual_result.is_err());
        assert_eq!(expected_codes, actual_result.unwrap_err().codes());
    }

    /// Verifies that CivilDate::of returns an error when 29 February falls in a century not divisible by 400.
    /// Compared with: `NaiveDate::from_ymd_opt(1900, 2, 29)` returns `None` too.
    #[test]
    fn returns_day_out_of_range_when_29_february_falls_in_a_century_not_divisible_by_400() {
        // GIVEN: 29 February 1900, divisible by 100 but not by 400
        let given_year = 1900;
        let given_month = 2;
        let given_day = 29;

        // WHEN
        let actual_result = CivilDate::of(given_year, given_month, given_day);

        // EXPECTATIONS: no date, because 1900 was not a leap year
        let expected_is_err = NaiveDate::from_ymd_opt(1900, 2, 29).is_none();
        let expected_codes = vec!["day-out-of-range"];

        // THEN
        assert_eq!(expected_is_err, actual_result.is_err());
        assert_eq!(expected_codes, actual_result.unwrap_err().codes());
    }

    /// Verifies that CivilDate::of returns an error when the month is 13.
    /// Compared with: `NaiveDate::from_ymd_opt(2026, 13, 1)` returns `None` too.
    #[test]
    fn returns_month_out_of_range_when_the_month_is_13() {
        // GIVEN: the first day of a thirteenth month
        let given_year = 2026;
        let given_month = 13;
        let given_day = 1;

        // WHEN
        let actual_result = CivilDate::of(given_year, given_month, given_day);

        // EXPECTATIONS: no date, because a Gregorian year has 12 months
        let expected_is_err = NaiveDate::from_ymd_opt(2026, 13, 1).is_none();
        let expected_codes = vec!["month-out-of-range"];

        // THEN
        assert_eq!(expected_is_err, actual_result.is_err());
        assert_eq!(expected_codes, actual_result.unwrap_err().codes());
    }

    /// Verifies that CivilDate::of creates the date when the year is beyond chrono's range.
    /// Compared with: `NaiveDate::from_ymd_opt(275760, 9, 13)` returns `None`; chrono's years end at +262142.
    #[test]
    fn creates_the_date_when_the_year_is_beyond_chronos_range() -> Result<(), Box<dyn Error>> {
        // GIVEN: 13 September 275760, the last day a JavaScript Date can hold
        let given_year = 275_760;
        let given_month = 9;
        let given_day = 13;

        // WHEN
        let actual_date = CivilDate::of(given_year, given_month, given_day)?;

        // EXPECTATIONS: an expanded year, written with a sign
        let expected_text = "+275760-09-13";

        // THEN
        assert_eq!(expected_text, actual_date.to_string());
        Ok(())
    }

    /// Verifies that CivilDate::of creates the date when the year is before year 1.
    /// Compared with: `NaiveDate::from_ymd_opt(-500, 3, 1)` holds the same proleptic Gregorian day.
    #[test]
    fn creates_the_date_when_the_year_is_negative() -> Result<(), Box<dyn Error>> {
        // GIVEN: 1 March of year -500 in the proleptic Gregorian calendar
        let given_year = -500;
        let given_month = 3;
        let given_day = 1;

        // WHEN
        let actual_date = CivilDate::of(given_year, given_month, given_day)?;

        // EXPECTATIONS: the same day as chrono's NaiveDate, written with a signed, four-digit year
        let expected_date = CivilDate::from(NaiveDate::from_ymd_opt(-500, 3, 1).unwrap());
        let expected_text = "-0500-03-01";

        // THEN
        assert_eq!(expected_date, actual_date);
        assert_eq!(expected_text, actual_date.to_string());
        Ok(())
    }
}

mod parse {
    use super::*;

    /// Verifies that CivilDate::parse returns the date when the text is an ISO 8601 calendar date.
    /// Compared with: `"2026-11-05".parse::<NaiveDate>()` reads the same day.
    #[test]
    fn returns_the_date_when_the_text_is_an_iso_date() -> Result<(), Box<dyn Error>> {
        // GIVEN: the ISO 8601 text of 5 November 2026
        let given_text = "2026-11-05";

        // WHEN
        let actual_outcome = CivilDate::parse(given_text);

        // EXPECTATIONS: the day chrono parses from the same text
        let expected_date = CivilDate::from("2026-11-05".parse::<NaiveDate>()?);

        // THEN
        assert_eq!(Some(expected_date), actual_outcome.unique());
        Ok(())
    }

    /// Verifies that CivilDate::parse returns invalid, with the reason, when the text names a day that does not exist.
    /// Compared with: `"2026-02-30".parse::<NaiveDate>()` returns an error of kind `OutOfRange`, which does not
    /// say which field is wrong.
    #[test]
    fn returns_invalid_when_the_text_names_a_day_that_does_not_exist() {
        // GIVEN: well-formed text for 30 February 2026
        let given_text = "2026-02-30";

        // WHEN
        let actual_outcome = CivilDate::parse(given_text);

        // EXPECTATIONS: invalid, and the diagnostic says which field is wrong
        let expected_status = Status::Invalid;
        let expected_code = "day-out-of-range";

        // THEN
        assert_eq!(expected_status, actual_outcome.status());
        assert!(actual_outcome.diagnostics().contains(expected_code));
    }
}

mod day_of_week {
    use super::*;

    // BEGIN weekday-week
    /// Verifies that CivilDate::day_of_week returns the weekday of the date, numbered the ISO way.
    /// Compared with: `NaiveDate::weekday` names the same day; its `number_from_monday` is the ISO number, while
    /// `num_days_from_monday` counts from 0.
    #[test]
    fn returns_thursday_when_the_date_is_5_november_2026() -> Result<(), Box<dyn Error>> {
        // SYSTEM UNDER TEST: 5 November 2026
        let sut = CivilDate::of(2026, 11, 5)?;

        // WHEN
        let actual_weekday = sut.day_of_week();

        // EXPECTATIONS: Thursday, the ISO number chrono gives for the same day
        let expected_weekday = Weekday::Thursday;
        let expected_number = NaiveDate::from_ymd_opt(2026, 11, 5).unwrap().weekday().number_from_monday();

        // THEN
        assert_eq!(expected_weekday, actual_weekday);
        assert_eq!(expected_number, actual_weekday.number());
        Ok(())
    }
    // END weekday-week
}

mod iso_week {
    use super::*;

    // BEGIN weekday-week
    /// Verifies that CivilDate::iso_week returns the ISO 8601 week number of the date.
    /// Compared with: `NaiveDate::iso_week().week()` gives the same week.
    #[test]
    fn returns_45_when_the_date_is_5_november_2026() -> Result<(), Box<dyn Error>> {
        // SYSTEM UNDER TEST: 5 November 2026
        let sut = CivilDate::of(2026, 11, 5)?;

        // WHEN
        let actual_week = sut.iso_week();

        // EXPECTATIONS: the ISO week chrono computes for the same day
        let expected_week = NaiveDate::from_ymd_opt(2026, 11, 5).unwrap().iso_week().week();

        // THEN
        assert_eq!(expected_week, actual_week);
        Ok(())
    }
    // END weekday-week
}

mod is_leap_year {
    use super::*;

    /// Verifies that CivilDate::is_leap_year follows the Gregorian leap-year rules for every kind of year.
    /// Compared with: `NaiveDate::leap_year` gives the same answers.
    #[test]
    fn agrees_with_chrono_when_given_any_kind_of_year() -> Result<(), Box<dyn Error>> {
        // GIVEN: one year of each kind. Rust's test harness has no parameterized tests, so the loop runs the same
        // test body once per year, and each assertion names its year.
        // 2024 is divisible by 4: leap; 2026 is not: common; 1900 is divisible by 100 but not 400: common;
        // 2000 is divisible by 400: leap.
        for given_year in [2024, 2026, 1900, 2000] {
            // SYSTEM UNDER TEST: 1 January of the given year
            let sut = CivilDate::of(i128::from(given_year), 1, 1)?;

            // WHEN
            let actual_is_leap_year = sut.is_leap_year();

            // EXPECTATIONS: chrono's answer for the same year
            let expected_is_leap_year = NaiveDate::from_ymd_opt(given_year, 1, 1).unwrap().leap_year();

            // THEN
            assert_eq!(expected_is_leap_year, actual_is_leap_year, "year {given_year}");
        }
        Ok(())
    }
}

mod add_days {
    use super::*;

    // BEGIN add-days
    /// Verifies that CivilDate::add_days returns the date that many days later, carrying into the next month.
    /// Compared with: `NaiveDate::checked_add_days(Days::new(30))` gives the same date.
    #[test]
    fn returns_the_later_date_when_the_days_cross_a_month_end() -> Result<(), Box<dyn Error>> {
        // GIVEN: 30 days, more than remain in November
        let given_days = 30;

        // SYSTEM UNDER TEST: 5 November 2026
        let sut = CivilDate::of(2026, 11, 5)?;

        // WHEN
        let actual_date = sut.add_days(given_days);

        // EXPECTATIONS: the date chrono computes for the same addition
        let tmp_native = NaiveDate::from_ymd_opt(2026, 11, 5).unwrap().checked_add_days(Days::new(30)).unwrap();
        let expected_date = CivilDate::from(tmp_native);

        // THEN
        assert_eq!(expected_date, actual_date);
        Ok(())
    }
    // END add-days
}

mod add_months {
    use super::*;

    // BEGIN month-end
    /// Verifies that CivilDate::add_months returns none when the day is missing from the target month.
    /// Compared with: `NaiveDate::checked_add_months` clamps 31 January to 28 February without saying so.
    #[test]
    fn returns_none_when_the_day_is_missing_from_the_target_month() -> Result<(), Box<dyn Error>> {
        // GIVEN: one month, into a February that has no day 31
        let given_months = 1;

        // SYSTEM UNDER TEST: 31 January 2026
        let sut = CivilDate::of(2026, 1, 31)?;

        // WHEN
        let actual_outcome = sut.add_months(given_months);

        // EXPECTATIONS: no such date, explained by the month-end diagnostic
        let expected_status = Status::None;
        let expected_code = "month-end";

        // THEN
        assert_eq!(expected_status, actual_outcome.status());
        assert!(actual_outcome.diagnostics().contains(expected_code));
        Ok(())
    }
    // END month-end
}

mod add_months_constrained {
    use super::*;

    // BEGIN month-end
    /// Verifies that CivilDate::add_months_constrained returns the last day of the target month when the day is
    /// missing from it.
    /// Compared with: `NaiveDate::checked_add_months` gives the same date, but without being asked.
    #[test]
    fn returns_the_last_day_of_the_month_when_the_day_is_missing_from_it() -> Result<(), Box<dyn Error>> {
        // GIVEN: one month, constrained to the end of the target month
        let given_months = 1;

        // SYSTEM UNDER TEST: 31 January 2026
        let sut = CivilDate::of(2026, 1, 31)?;

        // WHEN
        let actual_date = sut.add_months_constrained(given_months);

        // EXPECTATIONS: the date chrono clamps to
        let tmp_native = NaiveDate::from_ymd_opt(2026, 1, 31).unwrap().checked_add_months(Months::new(1)).unwrap();
        let expected_date = CivilDate::from(tmp_native);

        // THEN
        assert_eq!(expected_date, actual_date);
        Ok(())
    }
    // END month-end
}

mod add {
    use super::*;

    /// Verifies that CivilDate::add returns none when a one-month period lands on a missing day.
    /// Compared with: chrono has no calendar period type; `Months` is a separate count that only clamps.
    #[test]
    fn returns_none_when_a_one_month_period_lands_on_a_missing_day() -> Result<(), Box<dyn Error>> {
        // GIVEN: a calendar period of one month
        let given_period = Period::months(1);

        // SYSTEM UNDER TEST: 31 January 2026
        let sut = CivilDate::of(2026, 1, 31)?;

        // WHEN
        let actual_outcome = sut.add(given_period);

        // EXPECTATIONS: no such date under the default, rejecting month-end policy
        let expected_status = Status::None;
        let expected_code = "month-end";

        // THEN
        assert_eq!(expected_status, actual_outcome.status());
        assert!(actual_outcome.diagnostics().contains(expected_code));
        Ok(())
    }
}

mod days_until {
    use super::*;

    // BEGIN days-between
    /// Verifies that CivilDate::days_until returns the number of days to a later date.
    /// Compared with: `NaiveDate::signed_duration_since(..).num_days()` gives the same count.
    #[test]
    fn returns_the_day_count_when_the_other_date_is_later() -> Result<(), Box<dyn Error>> {
        // GIVEN: Christmas Day 2026
        let given_christmas = CivilDate::of(2026, 12, 25)?;

        // SYSTEM UNDER TEST: 5 November 2026
        let sut = CivilDate::of(2026, 11, 5)?;

        // WHEN
        let actual_days = sut.days_until(given_christmas);

        // EXPECTATIONS: the days chrono counts between the same two dates
        let tmp_christmas = NaiveDate::from_ymd_opt(2026, 12, 25).unwrap();
        let expected_days = tmp_christmas.signed_duration_since(NaiveDate::from_ymd_opt(2026, 11, 5).unwrap()).num_days();

        // THEN
        assert_eq!(expected_days, actual_days);
        Ok(())
    }
    // END days-between
}

mod lt {
    use super::*;

    // BEGIN compare-dates
    /// Verifies that CivilDate's less-than operator is true when the left date comes first.
    /// Compared with: `NaiveDate`'s operator gives the same answer. Plain dates always have an order.
    #[test]
    fn returns_true_when_the_left_date_comes_first() -> Result<(), Box<dyn Error>> {
        // GIVEN: Christmas Day 2026
        let given_christmas = CivilDate::of(2026, 12, 25)?;

        // SYSTEM UNDER TEST: 5 November 2026
        let sut = CivilDate::of(2026, 11, 5)?;

        // WHEN
        let actual_comes_first = sut < given_christmas;

        // EXPECTATIONS: chrono's answer for the same two days
        let expected_comes_first = NaiveDate::from_ymd_opt(2026, 11, 5).unwrap() < NaiveDate::from_ymd_opt(2026, 12, 25).unwrap();

        // THEN
        assert_eq!(expected_comes_first, actual_comes_first);
        Ok(())
    }
    // END compare-dates
}

mod try_into {
    use super::*;

    /// Verifies that converting a CivilDate into a NaiveDate returns out-of-native-range when the year is beyond
    /// chrono's range.
    /// Compared with: `NaiveDate::MAX` is +262142-12-31, so there is no NaiveDate to return.
    #[test]
    fn returns_out_of_native_range_when_the_year_is_beyond_chronos_range() -> Result<(), Box<dyn Error>> {
        // SYSTEM UNDER TEST: 13 September 275760
        let sut = CivilDate::of(275_760, 9, 13)?;

        // WHEN
        let actual_result: Result<NaiveDate, Diagnostics> = sut.try_into();

        // EXPECTATIONS: refused, as chrono refuses the same fields, because the native type cannot hold the value
        let expected_is_err = NaiveDate::from_ymd_opt(275_760, 9, 13).is_none();
        let expected_codes = vec!["out-of-native-range"];

        // THEN
        assert_eq!(expected_is_err, actual_result.is_err());
        assert_eq!(expected_codes, actual_result.unwrap_err().codes());
        Ok(())
    }
}
