//! How to use `ZonedIntent`, a wall time in a named zone that stays an intention until it is
//! resolved against explicit zone rules, compared with chrono-tz's `Tz`, which resolves at once
//! with the tz database compiled into the binary.

use std::error::Error;

use chrono::{Days, NaiveDateTime, TimeZone, Utc};
use chrono_tz::America::New_York;
use chrono_tz::Europe::Paris;
use utv::{CivilDateTime, Gap, Instant, Outcome, Overlap, ResolutionPolicy, Status, TemporalContext};

use crate::fixtures::demo_zone_rules;

mod resolve {
    use super::*;

    // BEGIN meeting-in-paris
    /// Verifies that ZonedIntent::resolve returns the one instant when the wall time happens exactly once.
    /// Compared with: chrono-tz's `Paris.with_ymd_and_hms(2027, 6, 1, 9, 0, 0)` finds the same instant.
    #[test]
    fn returns_the_instant_when_the_wall_time_happens_once() -> Result<(), Box<dyn Error>> {
        // SETUP: the pinned demo zone rules, never this machine's tz database
        let env_context = TemporalContext::with(demo_zone_rules());

        // SYSTEM UNDER TEST: a meeting at 09:00 on 1 June 2027 in Paris
        let sut = CivilDateTime::of(2027, 6, 1, 9, 0)?.in_zone("Europe/Paris");

        // WHEN
        let actual_outcome = sut.resolve(&env_context);

        // EXPECTATIONS: the instant chrono-tz finds for the same wall time
        let tmp_native = Paris.with_ymd_and_hms(2027, 6, 1, 9, 0, 0).single().unwrap();
        let expected_instant = Instant::try_from(tmp_native.with_timezone(&Utc))?;

        // THEN
        assert_eq!(Some(expected_instant), actual_outcome.unique());
        Ok(())
    }
    // END meeting-in-paris

    // BEGIN dst-gap
    /// Verifies that ZonedIntent::resolve returns none when the wall time falls in a daylight-saving gap.
    /// Compared with: chrono-tz's `with_ymd_and_hms` returns `LocalResult::None` too, without saying why.
    #[test]
    fn returns_none_when_the_wall_time_falls_in_a_gap() -> Result<(), Box<dyn Error>> {
        // SETUP: the pinned demo zone rules, never this machine's tz database
        let env_context = TemporalContext::with(demo_zone_rules());

        // SYSTEM UNDER TEST: 02:30 on 8 March 2026 in New York, when clocks jump from 02:00 to 03:00
        let sut = CivilDateTime::of(2026, 3, 8, 2, 30)?.in_zone("America/New_York");

        // WHEN
        let actual_outcome = sut.resolve(&env_context);

        // EXPECTATIONS: no such instant, explained by the gap diagnostic
        let expected_status = Status::None;
        let expected_code = "gap";

        // THEN
        assert_eq!(expected_status, actual_outcome.status());
        assert!(actual_outcome.diagnostics().contains(expected_code));
        Ok(())
    }
    // END dst-gap

    // BEGIN dst-overlap
    /// Verifies that ZonedIntent::resolve returns both instants when the wall time happens twice.
    /// Compared with: chrono-tz's `with_ymd_and_hms` returns `LocalResult::Ambiguous` with the same two instants;
    /// chrono is the one standard API that reports both.
    #[test]
    fn returns_both_instants_when_the_wall_time_falls_in_an_overlap() -> Result<(), Box<dyn Error>> {
        // SETUP: the pinned demo zone rules, never this machine's tz database
        let env_context = TemporalContext::with(demo_zone_rules());

        // SYSTEM UNDER TEST: 01:30 on 1 November 2026 in New York, when clocks go back from 02:00 to 01:00
        let sut = CivilDateTime::of(2026, 11, 1, 1, 30)?.in_zone("America/New_York");

        // WHEN
        let actual_outcome = sut.resolve(&env_context);

        // EXPECTATIONS: 01:30 daylight time, then 01:30 standard time, as chrono-tz finds them
        let tmp_native = New_York.with_ymd_and_hms(2026, 11, 1, 1, 30, 0);
        let expected_instants = vec![
            Instant::try_from(tmp_native.earliest().unwrap().with_timezone(&Utc))?,
            Instant::try_from(tmp_native.latest().unwrap().with_timezone(&Utc))?,
        ];
        let expected_code = "ambiguous";

        // THEN
        match actual_outcome {
            Outcome::Multiple(actual_instants, actual_diagnostics) => {
                assert_eq!(expected_instants, actual_instants);
                assert!(actual_diagnostics.contains(expected_code));
            }
            other => panic!("expected both instants, got {other:?}"),
        }
        Ok(())
    }
    // END dst-overlap
}

mod resolve_with {
    use super::*;

    // BEGIN dst-gap
    /// Verifies that ZonedIntent::resolve_with and Gap::ShiftForward move the wall time forward by the length of the gap.
    /// Compared with: chrono has no such policy; it returns `LocalResult::None` and leaves the choice to the caller.
    #[test]
    fn shifts_forward_by_the_gap_when_shift_forward_is_requested() -> Result<(), Box<dyn Error>> {
        // GIVEN: the policy that moves a skipped wall time forward by the gap
        let given_policy = ResolutionPolicy::new(Gap::ShiftForward, Overlap::Reject);

        // SETUP: the pinned demo zone rules, never this machine's tz database
        let env_context = TemporalContext::with(demo_zone_rules());

        // SYSTEM UNDER TEST: 02:30 on 8 March 2026 in New York, when clocks jump from 02:00 to 03:00
        let sut = CivilDateTime::of(2026, 3, 8, 2, 30)?.in_zone("America/New_York");

        // WHEN
        let actual_outcome = sut.resolve_with(&env_context, given_policy);

        // EXPECTATIONS: 03:30 daylight time, four hours behind UTC
        let expected_instant = CivilDateTime::of(2026, 3, 8, 3, 30)?.at_offset(-4 * 3600)?.to_instant();

        // THEN
        assert_eq!(Some(expected_instant), actual_outcome.unique());
        Ok(())
    }
    // END dst-gap

    // BEGIN dst-overlap
    /// Verifies that ZonedIntent::resolve_with and Overlap::Earliest return the first of the two instants.
    /// Compared with: chrono's `LocalResult::earliest` picks the same one.
    #[test]
    fn returns_the_earlier_instant_when_earliest_is_requested() -> Result<(), Box<dyn Error>> {
        // GIVEN: the policy that picks the earlier of two instants
        let given_policy = ResolutionPolicy::new(Gap::Reject, Overlap::Earliest);

        // SETUP: the pinned demo zone rules, never this machine's tz database
        let env_context = TemporalContext::with(demo_zone_rules());

        // SYSTEM UNDER TEST: 01:30 on 1 November 2026 in New York, when clocks go back from 02:00 to 01:00
        let sut = CivilDateTime::of(2026, 11, 1, 1, 30)?.in_zone("America/New_York");

        // WHEN
        let actual_outcome = sut.resolve_with(&env_context, given_policy);

        // EXPECTATIONS: 01:30 daylight time, the first time the clock shows it, as chrono picks it
        let tmp_native = New_York.with_ymd_and_hms(2026, 11, 1, 1, 30, 0).earliest().unwrap();
        let expected_instant = Instant::try_from(tmp_native.with_timezone(&Utc))?;

        // THEN
        assert_eq!(Some(expected_instant), actual_outcome.unique());
        Ok(())
    }
    // END dst-overlap
}

mod add_days {
    use super::*;

    // BEGIN day-vs-24h
    /// Verifies that ZonedIntent::add_days keeps the wall time when the day crosses a daylight-saving change.
    /// Compared with: `DateTime<Tz> + Days::new(1)` keeps noon too; `+ TimeDelta::days(1)` adds 86,400 seconds and
    /// lands on 13:00.
    #[test]
    fn keeps_the_wall_time_when_the_day_crosses_a_dst_change() -> Result<(), Box<dyn Error>> {
        // GIVEN: one calendar day
        let given_days = 1;

        // SYSTEM UNDER TEST: noon on 7 March 2026 in New York, the day before clocks spring forward
        let sut = CivilDateTime::of(2026, 3, 7, 12, 0)?.in_zone("America/New_York");

        // WHEN
        let actual_next_noon = sut.add_days(given_days);

        // EXPECTATIONS: noon again, on the next date, in the same zone, as chrono's calendar days give it
        let tmp_native = New_York.with_ymd_and_hms(2026, 3, 7, 12, 0, 0).unwrap() + Days::new(1);
        let expected_wall_time = tmp_native.naive_local();
        let expected_zone_id = "America/New_York";

        // THEN
        assert_eq!(expected_wall_time, NaiveDateTime::try_from(actual_next_noon.date_time())?);
        assert_eq!(expected_zone_id, actual_next_noon.zone_id());
        Ok(())
    }
    // END day-vs-24h
}
