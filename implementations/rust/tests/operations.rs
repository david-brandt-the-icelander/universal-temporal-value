//! Behaviour the contract specifies that the shared fixtures don't exercise.

use serde_json::json;
use utv::build::{date, date_time, in_zone, instant, interval_with, period, with_offset, Endpoint, Period};
use utv::{add, compare, contains, format_iso, overlaps, parse_iso, resolve, to_offset, Rules, Status};

fn rules() -> Rules {
    let p = concat!(env!("CARGO_MANIFEST_DIR"), "/../../conformance/rules/demo-zones.json");
    Rules::parse(&std::fs::read_to_string(p).unwrap()).unwrap()
}

#[test]
fn result_json_carries_candidates() {
    let r = parse_iso("2026-11-05");
    let j = r.to_json();
    assert_eq!(j["schema"], "utv-result/1.0");
    assert_eq!(j["candidates"], json!([date(2026, 11, 5)]));
    assert_eq!(parse_iso("nope").to_json()["candidates"], json!([]));
    assert_eq!(r.to_string(), "unique 2026-11-05");
}

#[test]
fn resolve_rejects_unknown_policies_and_uses_the_default() {
    let overlap = in_zone(date_time(2026, 11, 1, 1, 30), "America/New_York");
    let r = resolve(&overlap, &rules(), Some(&json!({"gap": "reject", "overlap": "offset-match"})));
    assert_eq!(r.status, Status::Unsupported);
    let r = resolve(&overlap, &rules(), None);
    assert_eq!((r.status, r.candidates.len()), (Status::Multiple, 2));
    assert_eq!(r.diagnostics[0].code, "ambiguous");
    let all = resolve(&overlap, &rules(), Some(&json!({"overlap": "all"})));
    assert_eq!(all.status, Status::Multiple);
    assert!(all.diagnostics.is_empty());
}

#[test]
fn add_keeps_the_zone_and_drops_a_stale_offset_assertion() {
    let v = parse_iso("2026-03-28T09:00+01:00[Europe/Paris]").value().unwrap().clone();
    let r = add(&v, &period(Period::days(1)), None);
    assert_eq!(r.as_str(), None);
    assert_eq!(format_iso(r.value().unwrap()).as_str(), Some("2026-03-29T09:00[Europe/Paris]"));
    let offset = with_offset(date_time(2026, 11, 5, 23, 30), -18000);
    let r = add(&offset, &period(Period { hours: 1, seconds: 30, ..Period::default() }), None);
    assert_eq!(format_iso(r.value().unwrap()).as_str(), Some("2026-11-06T00:30:30-05:00"));
}

#[test]
fn compare_refuses_mixed_zone_kinds() {
    let floating = date_time(2026, 11, 5, 9, 0);
    let fixed = with_offset(date_time(2026, 11, 5, 9, 0), 0);
    assert_eq!(compare(&floating, &fixed).status, Status::Unsupported);
    assert_eq!(compare(&instant(0), &fixed).status, Status::Unsupported);
    assert_eq!(compare(&date(2026, 2, 30), &date(2026, 3, 1)).status, Status::Invalid);
}

#[test]
fn empty_intervals_overlap_nothing() {
    let empty = interval_with(Endpoint::Included(instant(5)), Endpoint::Excluded(instant(5)));
    let wide = interval_with(Endpoint::Included(instant(0)), Endpoint::Unbounded);
    assert_eq!(overlaps(&empty, &wide).as_str(), Some("impossible"));
    let day = interval_with(Endpoint::Included(date(2026, 11, 5)), Endpoint::Included(date(2026, 11, 5)));
    assert_eq!(contains(&day, &date(2026, 11, 5)).as_str(), Some("definitely"));
    let none = interval_with(Endpoint::Excluded(date(2026, 11, 5)), Endpoint::Excluded(date(2026, 11, 6)));
    assert_eq!(overlaps(&none, &day).as_str(), Some("impossible"));
}

#[test]
fn offsets_beyond_18_hours_are_invalid() {
    assert_eq!(to_offset(&instant(0), 18 * 3600 + 60).status, Status::Invalid);
    assert_eq!(to_offset(&instant(0), -18 * 3600).status, Status::Unique);
}

#[test]
fn huge_years_work_or_are_unsupported_never_wrong() {
    let far = parse_iso("+100000000000000000000-01-01");
    assert_eq!(format_iso(far.value().unwrap()).as_str(), Some("+100000000000000000000-01-01"));
    let next = add(far.value().unwrap(), &period(Period::days(1)), None);
    assert_eq!(format_iso(next.value().unwrap()).as_str(), Some("+100000000000000000000-01-02"));
    let beyond = format!("+{}-01-01", "9".repeat(40));
    assert_eq!(parse_iso(&beyond).status, Status::Unsupported);
}

#[test]
fn clarifications() {
    // add drops a stale offsetAssertion and says so.
    let v = parse_iso("2026-03-28T09:00+01:00[Europe/Paris]").value().unwrap().clone();
    let r = add(&v, &period(Period::days(1)), None);
    assert_eq!(r.diagnostics[0].code, "offset-assertion-dropped");
    // An asserted offset that matches nothing (here: a wall time in a gap) contradicts the rules.
    let gap = parse_iso("2026-03-08T02:30-05:00[America/New_York]").value().unwrap().clone();
    let r = resolve(&gap, &rules(), Some(&json!({"gap": "shift-forward-by-gap"})));
    assert_eq!((r.status, r.diagnostics[0].code.as_str()), (Status::Invalid, "offset-mismatch"));
    // Dates must share a zone (or all be unzoned).
    let paris = in_zone(date(2026, 11, 5), "Europe/Paris");
    let nov = utv::interval(date(2026, 11, 1), date(2026, 12, 1));
    assert_eq!(contains(&nov, &paris).status, Status::Unsupported);
    let nov_paris = utv::interval(in_zone(date(2026, 11, 1), "Europe/Paris"), in_zone(date(2026, 12, 1), "Europe/Paris"));
    assert_eq!(contains(&nov_paris, &paris).as_str(), Some("definitely"));
    assert_eq!(overlaps(&nov, &nov_paris).status, Status::Unsupported);
    // Results name their operation.
    assert_eq!(parse_iso("2026-11-05").to_json()["operation"], "parseIso");
    assert_eq!(utv::validate_structural("{}").operation, "validate");
}
