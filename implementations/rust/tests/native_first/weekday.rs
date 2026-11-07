//! What chrono's `Weekday` does, compared with UTV's `Weekday`, which numbers the days the ISO 8601
//! way, Monday 1 to Sunday 7.

use std::error::Error;

use chrono::{Datelike, NaiveDate};
use utv::CivilDate;

mod num_days_from_monday {
    use super::*;

    /// Verifies that Weekday::num_days_from_monday numbers Sunday 6 when the day is a Sunday.
    /// Compared with: UTV's `Weekday::number` follows ISO 8601 and numbers Sunday 7.
    #[test]
    fn numbers_sunday_6_when_the_day_is_a_sunday() {
        // SYSTEM UNDER TEST: the weekday of Sunday 8 November 2026
        let sut = NaiveDate::from_ymd_opt(2026, 11, 8).unwrap().weekday();

        // WHEN
        let actual_number = sut.num_days_from_monday();

        // EXPECTATIONS: 6, counting from Monday 0, where UTV says 7
        let expected_number = 6;

        // THEN
        assert_eq!(expected_number, actual_number);
    }
}

mod number_from_monday {
    use super::*;

    /// Verifies that Weekday::number_from_monday numbers Sunday 7 when the day is a Sunday.
    /// Compared with: UTV's `Weekday::number` gives the same ISO 8601 number.
    #[test]
    fn numbers_sunday_7_when_the_day_is_a_sunday() -> Result<(), Box<dyn Error>> {
        // SYSTEM UNDER TEST: the weekday of Sunday 8 November 2026
        let sut = NaiveDate::from_ymd_opt(2026, 11, 8).unwrap().weekday();

        // WHEN
        let actual_number = sut.number_from_monday();

        // EXPECTATIONS: the number UTV gives the same day
        let expected_number = CivilDate::of(2026, 11, 8)?.day_of_week().number();

        // THEN
        assert_eq!(expected_number, actual_number);
        Ok(())
    }
}
