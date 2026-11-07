//! How to use `TemporalContext`, which holds the zone rules that zoned operations need, compared
//! with chrono, where every zoned value is resolved with chrono-tz's compiled-in rules the moment
//! it is made.

use std::cmp::Ordering;
use std::error::Error;

use utv::{CivilDateTime, Outcome, TemporalContext};

use crate::fixtures::demo_zone_rules;

mod compare {
    use super::*;

    // BEGIN compare-dates
    /// Verifies that TemporalContext::compare returns both orders when both wall times fall in the same overlap.
    /// Compared with: two `NaiveDateTime`s compare by wall time and always say "earlier"; two chrono-tz
    /// `DateTime`s can't even be made until `LocalResult::earliest` or `latest` has picked an instant for each.
    #[test]
    fn returns_multiple_when_both_wall_times_fall_in_an_overlap() -> Result<(), Box<dyn Error>> {
        // GIVEN: a call at 01:30 and a stand-up at 01:45, on the night New York's clocks go back from 02:00 to 01:00
        let given_call = CivilDateTime::of(2026, 11, 1, 1, 30)?.in_zone("America/New_York");
        let given_standup = CivilDateTime::of(2026, 11, 1, 1, 45)?.in_zone("America/New_York");

        // SETUP: the pinned demo zone rules, never this machine's tz database
        let env_rules = demo_zone_rules();

        // SYSTEM UNDER TEST
        let sut = TemporalContext::with(env_rules);

        // WHEN
        let actual_outcome = sut.compare(&given_call, &given_standup);

        // EXPECTATIONS: either could come first (01:30 standard time is after 01:45 daylight time)
        let expected_orders = vec![Ordering::Less, Ordering::Greater];
        let expected_code = "ambiguous";

        // THEN
        match actual_outcome {
            Outcome::Multiple(actual_orders, actual_diagnostics) => {
                assert_eq!(expected_orders, actual_orders);
                assert!(actual_diagnostics.contains(expected_code));
            }
            other => panic!("expected both orders, got {other:?}"),
        }
        Ok(())
    }
    // END compare-dates
}
