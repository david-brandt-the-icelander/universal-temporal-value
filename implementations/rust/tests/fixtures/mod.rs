//! Shared test data for the two example suites, `tests/utv_first` and `tests/native_first`. Each
//! suite includes this file as its `fixtures` module, so the test of the fixture runs in both.

use utv::ZoneRules;

/// The pinned zone rules of `conformance/rules/demo-zones.json`: America/New_York, Europe/Paris
/// and Atlantic/Reykjavik, for 2026 and 2027 only. UTV never reads the host's tz database.
pub fn demo_zone_rules() -> ZoneRules {
    ZoneRules::load(concat!(env!("CARGO_MANIFEST_DIR"), "/../../conformance/rules/demo-zones.json")).expect("the demo zone rules load")
}

mod demo_zone_rules {
    use super::*;

    /// Verifies that fixtures::demo_zone_rules loads the three demo zones when called.
    #[test]
    fn contains_the_three_demo_zones_when_loaded() {
        // WHEN
        let actual_rules = demo_zone_rules();

        // EXPECTATIONS
        let expected_zone_ids = vec!["America/New_York", "Atlantic/Reykjavik", "Europe/Paris"];

        // THEN
        assert_eq!(expected_zone_ids, actual_rules.zone_ids().collect::<Vec<_>>());
    }
}
