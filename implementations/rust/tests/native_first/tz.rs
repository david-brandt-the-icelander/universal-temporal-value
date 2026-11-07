//! What chrono-tz's `Tz` does when it turns a wall time into an instant, compared with UTV's
//! `ZonedIntent::resolve`.
//!
//! chrono-tz compiles the IANA tz database into the binary (release 2025b for chrono-tz 0.10.4 in
//! `Cargo.lock`), so these tests read neither the host's tz database nor the pinned demo rules;
//! they need that copy to know the 2026–2027 rules of New York and Paris. UTV reads only the
//! pinned demo rules it is given.

use std::error::Error;

use chrono::{DateTime, LocalResult, NaiveDate, TimeZone, Utc};
use chrono_tz::America::New_York;
use chrono_tz::Europe::Paris;
use utv::{CivilDateTime, Gap, Overlap, ResolutionPolicy, Status, TemporalContext};

use crate::fixtures::demo_zone_rules;

mod from_local_datetime {
    use super::*;

    /// Verifies that Tz::from_local_datetime returns the one instant when the wall time happens exactly once.
    /// Compared with: `ZonedIntent::resolve` finds the same instant in the pinned rules.
    #[test]
    fn returns_the_instant_when_the_wall_time_happens_once() -> Result<(), Box<dyn Error>> {
        // GIVEN: a meeting at 09:00 on 1 June 2027
        let given_wall_time = NaiveDate::from_ymd_opt(2027, 6, 1).unwrap().and_hms_opt(9, 0, 0).unwrap();

        // SYSTEM UNDER TEST: Paris
        let sut = Paris;

        // WHEN
        let actual_result = sut.from_local_datetime(&given_wall_time);

        // EXPECTATIONS: the instant UTV resolves from the pinned rules
        let tmp_context = TemporalContext::with(demo_zone_rules());
        let tmp_instant = CivilDateTime::of(2027, 6, 1, 9, 0)?.in_zone("Europe/Paris").resolve(&tmp_context).single()?;
        let expected_instant = DateTime::<Utc>::try_from(tmp_instant)?;

        // THEN
        assert_eq!(expected_instant, actual_result.single().unwrap());
        Ok(())
    }

    /// Verifies that Tz::from_local_datetime returns `LocalResult::None` when the wall time falls in a daylight-saving gap.
    /// Compared with: `ZonedIntent::resolve` returns none too, and says why (gap); `Gap::ShiftForward` is there for
    /// callers who want 03:30.
    #[test]
    fn returns_none_when_the_wall_time_falls_in_a_gap() -> Result<(), Box<dyn Error>> {
        // GIVEN: 02:30 on 8 March 2026, when New York's clocks jump from 02:00 to 03:00
        let given_wall_time = NaiveDate::from_ymd_opt(2026, 3, 8).unwrap().and_hms_opt(2, 30, 0).unwrap();

        // SYSTEM UNDER TEST: New York
        let sut = New_York;

        // WHEN
        let actual_result = sut.from_local_datetime(&given_wall_time);

        // EXPECTATIONS: no instant, exactly when UTV finds none
        let tmp_context = TemporalContext::with(demo_zone_rules());
        let tmp_outcome = CivilDateTime::of(2026, 3, 8, 2, 30)?.in_zone("America/New_York").resolve(&tmp_context);
        let expected_is_none = tmp_outcome.status() == Status::None;

        // THEN
        assert_eq!(expected_is_none, matches!(actual_result, LocalResult::None));
        Ok(())
    }

    /// Verifies that Tz::from_local_datetime returns both instants when the wall time happens twice.
    /// Compared with: `ZonedIntent::resolve` returns the same two (multiple, ambiguous); chrono is the one standard
    /// API that reports both, and `LocalResult::earliest` and `latest` pick one as `Overlap::Earliest` and `Latest` do.
    #[test]
    fn returns_both_instants_when_the_wall_time_falls_in_an_overlap() -> Result<(), Box<dyn Error>> {
        // GIVEN: 01:30 on 1 November 2026, when New York's clocks go back from 02:00 to 01:00
        let given_wall_time = NaiveDate::from_ymd_opt(2026, 11, 1).unwrap().and_hms_opt(1, 30, 0).unwrap();

        // SYSTEM UNDER TEST: New York
        let sut = New_York;

        // WHEN
        let actual_result = sut.from_local_datetime(&given_wall_time);

        // EXPECTATIONS: the earlier and the later instant UTV finds in the pinned rules
        let tmp_context = TemporalContext::with(demo_zone_rules());
        let tmp_intent = CivilDateTime::of(2026, 11, 1, 1, 30)?.in_zone("America/New_York");
        let tmp_earliest = tmp_intent.resolve_with(&tmp_context, ResolutionPolicy::new(Gap::Reject, Overlap::Earliest)).single()?;
        let tmp_latest = tmp_intent.resolve_with(&tmp_context, ResolutionPolicy::new(Gap::Reject, Overlap::Latest)).single()?;
        let expected_earliest = DateTime::<Utc>::try_from(tmp_earliest)?;
        let expected_latest = DateTime::<Utc>::try_from(tmp_latest)?;

        // THEN
        match actual_result {
            LocalResult::Ambiguous(actual_earliest, actual_latest) => {
                assert_eq!(expected_earliest, actual_earliest);
                assert_eq!(expected_latest, actual_latest);
            }
            other => panic!("expected both instants, got {other:?}"),
        }
        Ok(())
    }
}
