//! What chrono's `NaiveDate` does, compared with UTV's `CivilDate` and `CivilDateTime`. Where the
//! two agree, the expected value is computed with UTV.

use std::error::Error;
use std::str::FromStr;

use chrono::format::ParseErrorKind;
use chrono::{Datelike, Days, Months, NaiveDate, NaiveDateTime, Timelike};
use utv::{CivilDate, CivilDateTime, Status};

mod from_ymd_opt {
    use super::*;

    /// Verifies that NaiveDate::from_ymd_opt creates the date when the fields name a real day.
    /// Compared with: `CivilDate::of(2026, 11, 5)` names the same day.
    #[test]
    fn creates_the_date_when_the_fields_name_a_real_day() -> Result<(), Box<dyn Error>> {
        // GIVEN: 5 November 2026
        let given_year = 2026;
        let given_month = 11;
        let given_day = 5;

        // WHEN
        let actual_date = NaiveDate::from_ymd_opt(given_year, given_month, given_day);

        // EXPECTATIONS: the same day as UTV's CivilDate
        let expected_date = NaiveDate::try_from(CivilDate::of(2026, 11, 5)?)?;

        // THEN
        assert_eq!(Some(expected_date), actual_date);
        Ok(())
    }

    /// Verifies that NaiveDate::from_ymd_opt returns None when the day does not exist in the month.
    /// Compared with: `CivilDate::of(2026, 2, 30)` returns an error too, and says why (day-out-of-range).
    #[test]
    fn returns_none_when_the_day_does_not_exist() {
        // GIVEN: 30 February 2026
        let given_year = 2026;
        let given_month = 2;
        let given_day = 30;

        // WHEN
        let actual_date = NaiveDate::from_ymd_opt(given_year, given_month, given_day);

        // EXPECTATIONS: no date, as UTV finds too
        let expected_is_none = CivilDate::of(2026, 2, 30).is_err();

        // THEN
        assert_eq!(expected_is_none, actual_date.is_none());
    }

    /// Verifies that NaiveDate::from_ymd_opt returns None when the month is 13.
    /// Compared with: `CivilDate::of(2026, 13, 1)` returns an error too, and says why (month-out-of-range).
    #[test]
    fn returns_none_when_the_month_is_13() {
        // GIVEN: the first day of a thirteenth month
        let given_year = 2026;
        let given_month = 13;
        let given_day = 1;

        // WHEN
        let actual_date = NaiveDate::from_ymd_opt(given_year, given_month, given_day);

        // EXPECTATIONS: no date, as UTV finds too
        let expected_is_none = CivilDate::of(2026, 13, 1).is_err();

        // THEN
        assert_eq!(expected_is_none, actual_date.is_none());
    }

    /// Verifies that NaiveDate::from_ymd_opt returns None when the year is beyond chrono's range.
    /// Compared with: `CivilDate::of(275760, 9, 13)` holds the date; UTV's years are `i128`.
    #[test]
    fn returns_none_when_the_year_is_beyond_chronos_range() {
        // GIVEN: 13 September 275760
        let given_year = 275_760;
        let given_month = 9;
        let given_day = 13;

        // WHEN
        let actual_date = NaiveDate::from_ymd_opt(given_year, given_month, given_day);

        // EXPECTATIONS: no date, where UTV has one
        let expected_date: Option<NaiveDate> = None;

        // THEN
        assert_eq!(expected_date, actual_date);
    }

    /// Verifies that NaiveDate::from_ymd_opt creates the date when the year is before year 1.
    /// Compared with: `CivilDate::of(-500, 3, 1)` holds the same proleptic Gregorian day.
    #[test]
    fn creates_the_date_when_the_year_is_negative() -> Result<(), Box<dyn Error>> {
        // GIVEN: 1 March of year -500
        let given_year = -500;
        let given_month = 3;
        let given_day = 1;

        // WHEN
        let actual_date = NaiveDate::from_ymd_opt(given_year, given_month, given_day);

        // EXPECTATIONS: the same day as UTV's CivilDate
        let expected_date = NaiveDate::try_from(CivilDate::of(-500, 3, 1)?)?;

        // THEN
        assert_eq!(Some(expected_date), actual_date);
        Ok(())
    }
}

mod max {
    use super::*;

    /// Verifies that NaiveDate::MAX is the last day of year 262142.
    /// Compared with: converting `CivilDate::of(275760, 9, 13)` into a `NaiveDate` is refused
    /// (out-of-native-range) for exactly this reason.
    #[test]
    fn is_the_last_day_of_262142_when_read() -> Result<(), Box<dyn Error>> {
        // WHEN
        let actual_max = NaiveDate::MAX;

        // EXPECTATIONS: 31 December 262142, as UTV converts it
        let expected_max = NaiveDate::try_from(CivilDate::of(262_142, 12, 31)?)?;

        // THEN
        assert_eq!(expected_max, actual_max);
        Ok(())
    }
}

mod from_str {
    use super::*;

    /// Verifies that NaiveDate::from_str reads the date when the text is an ISO 8601 calendar date.
    /// Compared with: `CivilDate::parse("2026-11-05")` reads the same day.
    #[test]
    fn reads_the_date_when_the_text_is_an_iso_date() -> Result<(), Box<dyn Error>> {
        // GIVEN: the ISO 8601 text of 5 November 2026
        let given_text = "2026-11-05";

        // WHEN
        let actual_date = NaiveDate::from_str(given_text)?;

        // EXPECTATIONS: the day UTV parses from the same text
        let expected_date = NaiveDate::try_from(CivilDate::parse("2026-11-05").single()?)?;

        // THEN
        assert_eq!(expected_date, actual_date);
        Ok(())
    }

    /// Verifies that NaiveDate::from_str returns an out-of-range error when the text names a day that does not exist.
    /// Compared with: `CivilDate::parse("2026-02-30")` returns invalid too, and names the field (day-out-of-range).
    #[test]
    fn returns_out_of_range_when_the_text_names_a_day_that_does_not_exist() {
        // GIVEN: well-formed text for 30 February 2026
        let given_text = "2026-02-30";

        // WHEN
        let actual_result = NaiveDate::from_str(given_text);

        // EXPECTATIONS: no date, as UTV finds too, with chrono's coarse reason
        let expected_is_err = CivilDate::parse("2026-02-30").status() != Status::Unique;
        let expected_kind = ParseErrorKind::OutOfRange;

        // THEN
        assert_eq!(expected_is_err, actual_result.is_err());
        assert_eq!(expected_kind, actual_result.unwrap_err().kind());
    }
}

mod weekday {
    use super::*;

    /// Verifies that NaiveDate::weekday names the day of the week, abbreviated, when given a date.
    /// Compared with: `CivilDate::day_of_week` names the same day, written in full: Thursday.
    #[test]
    fn returns_thursday_when_the_date_is_5_november_2026() -> Result<(), Box<dyn Error>> {
        // SYSTEM UNDER TEST: 5 November 2026
        let sut = NaiveDate::from_ymd_opt(2026, 11, 5).unwrap();

        // WHEN
        let actual_weekday = sut.weekday();

        // EXPECTATIONS: the ISO number UTV gives the same day, and chrono's short name
        let expected_number = CivilDate::of(2026, 11, 5)?.day_of_week().number();
        let expected_name = "Thu";

        // THEN
        assert_eq!(expected_number, actual_weekday.number_from_monday());
        assert_eq!(expected_name, actual_weekday.to_string());
        Ok(())
    }
}

mod iso_week {
    use super::*;

    /// Verifies that NaiveDate::iso_week returns the ISO 8601 week of the date.
    /// Compared with: `CivilDate::iso_week` gives the same week number.
    #[test]
    fn returns_week_45_when_the_date_is_5_november_2026() -> Result<(), Box<dyn Error>> {
        // SYSTEM UNDER TEST: 5 November 2026
        let sut = NaiveDate::from_ymd_opt(2026, 11, 5).unwrap();

        // WHEN
        let actual_week = sut.iso_week();

        // EXPECTATIONS: the week UTV computes for the same day
        let expected_week = CivilDate::of(2026, 11, 5)?.iso_week();

        // THEN
        assert_eq!(expected_week, actual_week.week());
        Ok(())
    }
}

mod leap_year {
    use super::*;

    /// Verifies that NaiveDate::leap_year follows the Gregorian leap-year rules for every kind of year.
    /// Compared with: `CivilDate::is_leap_year` gives the same answers.
    #[test]
    fn agrees_with_utv_when_given_any_kind_of_year() -> Result<(), Box<dyn Error>> {
        // GIVEN: one year of each kind. Rust's test harness has no parameterized tests, so the loop runs the same
        // test body once per year, and each assertion names its year.
        // 2024 is divisible by 4: leap; 2026 is not: common; 1900 is divisible by 100 but not 400: common;
        // 2000 is divisible by 400: leap.
        for given_year in [2024, 2026, 1900, 2000] {
            // SYSTEM UNDER TEST: 1 January of the given year
            let sut = NaiveDate::from_ymd_opt(given_year, 1, 1).unwrap();

            // WHEN
            let actual_is_leap_year = sut.leap_year();

            // EXPECTATIONS: UTV's answer for the same year
            let expected_is_leap_year = CivilDate::of(i128::from(given_year), 1, 1)?.is_leap_year();

            // THEN
            assert_eq!(expected_is_leap_year, actual_is_leap_year, "year {given_year}");
        }
        Ok(())
    }
}

mod checked_add_days {
    use super::*;

    /// Verifies that NaiveDate::checked_add_days returns the date that many days later, carrying into the next month.
    /// Compared with: `CivilDate::add_days` gives the same date.
    #[test]
    fn returns_the_later_date_when_the_days_cross_a_month_end() -> Result<(), Box<dyn Error>> {
        // GIVEN: 30 days, more than remain in November
        let given_days = Days::new(30);

        // SYSTEM UNDER TEST: 5 November 2026
        let sut = NaiveDate::from_ymd_opt(2026, 11, 5).unwrap();

        // WHEN
        let actual_date = sut.checked_add_days(given_days);

        // EXPECTATIONS: the date UTV computes for the same addition
        let expected_date = NaiveDate::try_from(CivilDate::of(2026, 11, 5)?.add_days(30))?;

        // THEN
        assert_eq!(Some(expected_date), actual_date);
        Ok(())
    }
}

mod checked_add_months {
    use super::*;

    /// Verifies that NaiveDate::checked_add_months clamps to the last day of the month when the day is missing from
    /// the target month.
    /// Compared with: `CivilDate::add_months(1)` returns none (month-end), and `add_months_constrained` gives this date
    /// only when asked.
    #[test]
    fn clamps_to_the_last_day_when_the_day_is_missing_from_the_target_month() -> Result<(), Box<dyn Error>> {
        // GIVEN: one month, into a February that has no day 31
        let given_months = Months::new(1);

        // SYSTEM UNDER TEST: 31 January 2026
        let sut = NaiveDate::from_ymd_opt(2026, 1, 31).unwrap();

        // WHEN
        let actual_date = sut.checked_add_months(given_months);

        // EXPECTATIONS: the date UTV gives only with the explicit constrained form
        let expected_date = NaiveDate::try_from(CivilDate::of(2026, 1, 31)?.add_months_constrained(1))?;

        // THEN
        assert_eq!(Some(expected_date), actual_date);
        Ok(())
    }
}

mod with_month {
    use super::*;

    /// Verifies that NaiveDate::with_month returns None when the day is missing from the new month.
    /// Compared with: `CivilDate::add_months(1)` returns none too, and says why (month-end).
    #[test]
    fn returns_none_when_the_day_is_missing_from_the_new_month() -> Result<(), Box<dyn Error>> {
        // GIVEN: February, which has no day 31
        let given_month = 2;

        // SYSTEM UNDER TEST: 31 January 2026
        let sut = NaiveDate::from_ymd_opt(2026, 1, 31).unwrap();

        // WHEN
        let actual_date = sut.with_month(given_month);

        // EXPECTATIONS: no date, as UTV finds when moving 31 January on by one month
        let expected_is_none = CivilDate::of(2026, 1, 31)?.add_months(1).status() == Status::None;

        // THEN
        assert_eq!(expected_is_none, actual_date.is_none());
        Ok(())
    }
}

mod signed_duration_since {
    use super::*;

    /// Verifies that NaiveDate::signed_duration_since counts the days between two dates.
    /// Compared with: `CivilDate::days_until` returns the same count directly.
    #[test]
    fn counts_the_days_when_the_other_date_is_earlier() -> Result<(), Box<dyn Error>> {
        // GIVEN: 5 November 2026
        let given_earlier = NaiveDate::from_ymd_opt(2026, 11, 5).unwrap();

        // SYSTEM UNDER TEST: Christmas Day 2026
        let sut = NaiveDate::from_ymd_opt(2026, 12, 25).unwrap();

        // WHEN
        let actual_days = sut.signed_duration_since(given_earlier).num_days();

        // EXPECTATIONS: the count UTV computes
        let expected_days = CivilDate::of(2026, 11, 5)?.days_until(CivilDate::of(2026, 12, 25)?);

        // THEN
        assert_eq!(expected_days, actual_days);
        Ok(())
    }
}

mod lt {
    use super::*;

    /// Verifies that NaiveDate's less-than operator is true when the left date comes first.
    /// Compared with: `CivilDate`'s operator gives the same answer.
    #[test]
    fn returns_true_when_the_left_date_comes_first() -> Result<(), Box<dyn Error>> {
        // GIVEN: Christmas Day 2026
        let given_christmas = NaiveDate::from_ymd_opt(2026, 12, 25).unwrap();

        // SYSTEM UNDER TEST: 5 November 2026
        let sut = NaiveDate::from_ymd_opt(2026, 11, 5).unwrap();

        // WHEN
        let actual_comes_first = sut < given_christmas;

        // EXPECTATIONS: UTV's answer for the same two days
        let expected_comes_first = CivilDate::of(2026, 11, 5)? < CivilDate::of(2026, 12, 25)?;

        // THEN
        assert_eq!(expected_comes_first, actual_comes_first);
        Ok(())
    }
}

mod and_hms_opt {
    use super::*;

    /// Verifies that NaiveDate::and_hms_opt creates a wall-clock date-time with no zone when given a time.
    /// Compared with: `CivilDateTime::of(2026, 11, 5, 14, 30)` is the same wall time, and has no zone by design.
    #[test]
    fn creates_a_wall_clock_date_time_when_given_a_time() -> Result<(), Box<dyn Error>> {
        // GIVEN: 14:30
        let given_hour = 14;
        let given_minute = 30;
        let given_second = 0;

        // SYSTEM UNDER TEST: 5 November 2026
        let sut = NaiveDate::from_ymd_opt(2026, 11, 5).unwrap();

        // WHEN
        let actual_date_time = sut.and_hms_opt(given_hour, given_minute, given_second);

        // EXPECTATIONS: the wall time UTV holds, tied to no zone
        let expected_date_time = NaiveDateTime::try_from(CivilDateTime::of(2026, 11, 5, 14, 30)?)?;

        // THEN
        assert_eq!(Some(expected_date_time), actual_date_time);
        Ok(())
    }

    /// Verifies that NaiveDate::and_hms_opt returns None when the second is a leap second, 60.
    /// Compared with: `CivilDateTime::of_hms(2016, 12, 31, 23, 59, 60)` refuses it too, but as leap-second: the second
    /// may well exist, and judging it needs a leap-second table.
    #[test]
    fn returns_none_when_the_second_is_60() {
        // GIVEN: 23:59:60, a real leap second on this date
        let given_hour = 23;
        let given_minute = 59;
        let given_second = 60;

        // SYSTEM UNDER TEST: 31 December 2016
        let sut = NaiveDate::from_ymd_opt(2016, 12, 31).unwrap();

        // WHEN
        let actual_date_time = sut.and_hms_opt(given_hour, given_minute, given_second);

        // EXPECTATIONS: no value, as UTV gives none
        let expected_is_none = CivilDateTime::of_hms(2016, 12, 31, 23, 59, 60).is_err();

        // THEN
        assert_eq!(expected_is_none, actual_date_time.is_none());
    }
}

mod and_hms_milli_opt {
    use super::*;

    /// Verifies that NaiveDate::and_hms_milli_opt represents a leap second as second 59 plus 1,000 milliseconds.
    /// Compared with: UTV writes the second as 60, and both `CivilDateTime::of_hms` and `CivilDateTime::try_from` this
    /// value return an error (leap-second).
    #[test]
    fn writes_the_leap_second_as_59_plus_a_second_when_given_1000_milliseconds() {
        // GIVEN: 23:59:59 plus 1,000 milliseconds, chrono's spelling of 23:59:60
        let given_hour = 23;
        let given_minute = 59;
        let given_second = 59;
        let given_milliseconds = 1_000;

        // SYSTEM UNDER TEST: 31 December 2016
        let sut = NaiveDate::from_ymd_opt(2016, 12, 31).unwrap();

        // WHEN
        let actual_date_time = sut.and_hms_milli_opt(given_hour, given_minute, given_second, given_milliseconds).unwrap();

        // EXPECTATIONS: second 59, with a nanosecond field past one second
        let expected_second = 59;
        let expected_nanosecond = 1_000_000_000;

        // THEN
        assert_eq!(expected_second, actual_date_time.second());
        assert_eq!(expected_nanosecond, actual_date_time.nanosecond());
    }
}
