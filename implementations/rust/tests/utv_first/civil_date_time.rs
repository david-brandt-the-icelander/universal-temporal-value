//! How to use `CivilDateTime`, a wall-clock date and time with no zone, compared with chrono's
//! `NaiveDateTime`.

use std::error::Error;

use chrono::{DateTime, FixedOffset, NaiveDate, NaiveDateTime, TimeDelta, TimeZone};
use utv::CivilDateTime;

mod of {
    use super::*;

    /// Verifies that CivilDateTime::of creates a wall-clock date-time with no zone when given a date and a time.
    /// Compared with: `NaiveDate::and_hms_opt(14, 30, 0)` gives the same wall time as a `NaiveDateTime`, also with
    /// no zone.
    #[test]
    fn creates_a_wall_clock_date_time_when_given_a_date_and_a_time() -> Result<(), Box<dyn Error>> {
        // GIVEN: 14:30 on 5 November 2026, in no particular zone
        let given_year = 2026;
        let given_month = 11;
        let given_day = 5;
        let given_hour = 14;
        let given_minute = 30;

        // WHEN
        let actual_date_time = CivilDateTime::of(given_year, given_month, given_day, given_hour, given_minute)?;

        // EXPECTATIONS: the wall time chrono holds, written without an offset or a zone
        let expected_date_time = NaiveDate::from_ymd_opt(2026, 11, 5).unwrap().and_hms_opt(14, 30, 0).unwrap();
        let expected_text = "2026-11-05T14:30";

        // THEN
        assert_eq!(expected_date_time, NaiveDateTime::try_from(actual_date_time)?);
        assert_eq!(expected_text, actual_date_time.to_string());
        Ok(())
    }
}

mod of_hms {
    use super::*;

    /// Verifies that CivilDateTime::of_hms returns an error, leap-second, when the second is 60.
    /// Compared with: `NaiveDate::and_hms_opt(23, 59, 60)` returns `None`; chrono can only write a leap second as
    /// 23:59:59 plus 1,000 milliseconds, with `and_hms_milli_opt`.
    #[test]
    fn returns_leap_second_when_the_second_is_60() {
        // GIVEN: 23:59:60 on 31 December 2016, a real leap second
        let given_year = 2016;
        let given_month = 12;
        let given_day = 31;
        let given_hour = 23;
        let given_minute = 59;
        let given_second = 60;

        // WHEN
        let actual_result = CivilDateTime::of_hms(given_year, given_month, given_day, given_hour, given_minute, given_second);

        // EXPECTATIONS: refused as a leap second, not as an impossible time: judging it needs a leap-second table
        let expected_codes = vec!["leap-second"];

        // THEN
        assert_eq!(expected_codes, actual_result.unwrap_err().codes());
    }
}

mod add_hours {
    use super::*;

    // BEGIN datetime-plus-hours
    /// Verifies that CivilDateTime::add_hours carries into the next day when the hours pass midnight.
    /// Compared with: `NaiveDateTime + TimeDelta::hours(2)` gives the same wall time; `NaiveTime` alone would wrap
    /// and lose the day.
    #[test]
    fn carries_into_the_next_day_when_the_hours_pass_midnight() -> Result<(), Box<dyn Error>> {
        // GIVEN: two hours, past midnight
        let given_hours = 2;

        // SYSTEM UNDER TEST: 23:30 on 5 November 2026
        let sut = CivilDateTime::of(2026, 11, 5, 23, 30)?;

        // WHEN
        let actual_date_time = sut.add_hours(given_hours);

        // EXPECTATIONS: the wall time chrono computes for the same addition
        let expected_date_time = NaiveDate::from_ymd_opt(2026, 11, 5).unwrap().and_hms_opt(23, 30, 0).unwrap() + TimeDelta::hours(2);

        // THEN
        assert_eq!(expected_date_time, NaiveDateTime::try_from(actual_date_time)?);
        Ok(())
    }
    // END datetime-plus-hours
}

mod in_zone {
    use super::*;

    /// Verifies that CivilDateTime::in_zone keeps the wall time and the zone name as an intention.
    /// Compared with: chrono-tz's `Paris.with_ymd_and_hms(2027, 6, 1, 9, 0, 0)` resolves the wall time to an instant
    /// straight away, using the rules compiled into the binary.
    #[test]
    fn keeps_the_wall_time_and_the_zone_when_given_a_zone_id() -> Result<(), Box<dyn Error>> {
        // GIVEN: the Paris time zone
        let given_zone_id = "Europe/Paris";

        // SYSTEM UNDER TEST: 09:00 on 1 June 2027
        let sut = CivilDateTime::of(2027, 6, 1, 9, 0)?;

        // WHEN
        let actual_meeting = sut.in_zone(given_zone_id);

        // EXPECTATIONS: the wall time and the zone, with no offset decided yet
        let expected_text = "2027-06-01T09:00[Europe/Paris]";

        // THEN
        assert_eq!(expected_text, actual_meeting.to_string());
        Ok(())
    }
}

mod at_offset {
    use super::*;

    /// Verifies that CivilDateTime::at_offset pins the wall time to a fixed UTC offset.
    /// Compared with: `FixedOffset::east_opt(3600)` and `with_ymd_and_hms(2026, 11, 5, 14, 30, 0)` make the same
    /// `DateTime<FixedOffset>`.
    #[test]
    fn pins_the_wall_time_to_the_offset_when_given_offset_seconds() -> Result<(), Box<dyn Error>> {
        // GIVEN: one hour east of UTC
        let given_offset_seconds = 3600;

        // SYSTEM UNDER TEST: 14:30 on 5 November 2026
        let sut = CivilDateTime::of(2026, 11, 5, 14, 30)?;

        // WHEN
        let actual_date_time = sut.at_offset(given_offset_seconds)?;

        // EXPECTATIONS: the value chrono's DateTime<FixedOffset> holds; chrono's == compares only the instant, so the
        // offset is compared on its own
        let expected_date_time = FixedOffset::east_opt(3600).unwrap().with_ymd_and_hms(2026, 11, 5, 14, 30, 0).unwrap();
        let expected_offset = FixedOffset::east_opt(3600).unwrap();

        // THEN
        let actual_native = DateTime::<FixedOffset>::try_from(actual_date_time)?;
        assert_eq!(expected_date_time, actual_native);
        assert_eq!(expected_offset, *actual_native.offset());
        Ok(())
    }
}
