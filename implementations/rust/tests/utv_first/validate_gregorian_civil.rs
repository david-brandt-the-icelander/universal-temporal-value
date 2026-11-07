//! How to use `utv::validate_gregorian_civil`, the semantic check of civil values in the proleptic
//! Gregorian calendar at the JSON layer, compared with chrono, where a `NaiveDate` that exists is
//! always valid and no calendar other than the Gregorian one exists.
//!
//! `validate_gregorian_civil` is a free function at the crate root, so this module is the
//! function: its tests sit directly in it, with no inner module per function.

use utv::{validate_gregorian_civil, Status};

/// Verifies that validate_gregorian_civil returns valid when the JSON names a real Gregorian date.
/// Compared with: `NaiveDate::from_ymd_opt(2026, 11, 5)` exists too.
#[test]
fn returns_valid_when_the_date_exists() {
    // GIVEN: 5 November 2026 as UTV JSON: fields in a named calendar, with no time and no zone
    let given_json = r#"
        { "schema": "utv/1.0", "kind": "civil",
          "value": { "form": "date", "fields": { "year": "2026", "month": 11, "day": 5 },
                     "calendar": { "id": "gregory", "chronology": "proleptic" } } }
        "#;

    // WHEN
    let actual_result = validate_gregorian_civil(given_json);

    // EXPECTATIONS
    let expected_status = Status::Valid;

    // THEN
    assert_eq!(expected_status, actual_result.status);
}

/// Verifies that validate_gregorian_civil returns invalid when well-formed JSON names a day that does not exist.
/// Compared with: chrono has one check, in `NaiveDate::from_ymd_opt`; UTV keeps structure and meaning as separate
/// checks.
#[test]
fn returns_invalid_when_well_formed_json_names_a_missing_day() {
    // GIVEN: 29 February 2026 as UTV JSON: structurally valid, but 2026 is a common year
    let given_json = r#"
        { "schema": "utv/1.0", "kind": "civil",
          "value": { "form": "date", "fields": { "year": "2026", "month": 2, "day": 29 },
                     "calendar": { "id": "gregory", "chronology": "proleptic" } } }
        "#;

    // WHEN
    let actual_result = validate_gregorian_civil(given_json);

    // EXPECTATIONS: invalid, with the reason, instead of rolling over to 1 March
    let expected_status = Status::Invalid;
    let expected_code = "day-out-of-range";

    // THEN
    assert_eq!(expected_status, actual_result.status);
    assert!(actual_result.diagnostics.iter().any(|d| d.code == expected_code));
}

/// Verifies that validate_gregorian_civil returns unsupported when the date is in another calendar.
/// Compared with: chrono has no Hebrew calendar at all, so the date can only be stored after someone converts it to
/// a Gregorian `NaiveDate` by hand, and the Hebrew fields are gone.
#[test]
fn returns_unsupported_when_the_calendar_is_not_gregorian() {
    // GIVEN: 14 Heshvan 5787 as UTV JSON, in the Hebrew calendar
    let given_json = r#"
        { "schema": "utv/1.0", "kind": "civil",
          "value": { "form": "date", "fields": { "year": "5787", "month": 2, "day": 14 },
                     "calendar": { "id": "hebrew", "chronology": "proleptic" } } }
        "#;

    // WHEN
    let actual_result = validate_gregorian_civil(given_json);

    // EXPECTATIONS: unsupported, kept as written, rather than guessed or converted
    let expected_status = Status::Unsupported;
    let expected_code = "unsupported-calendar";

    // THEN
    assert_eq!(expected_status, actual_result.status);
    assert!(actual_result.diagnostics.iter().any(|d| d.code == expected_code));
}

/// Verifies that validate_gregorian_civil returns valid when the value is a year with no month or day.
/// Compared with: `NaiveDate::from_ymd_opt(1347, 1, 1)` would claim it was 1 January.
#[test]
fn returns_valid_when_the_value_is_only_a_year() {
    // GIVEN: the year 1347 as UTV JSON
    let given_json = r#"
        { "schema": "utv/1.0", "kind": "civil",
          "value": { "form": "year", "fields": { "year": "1347" },
                     "calendar": { "id": "gregory", "chronology": "proleptic" } } }
        "#;

    // WHEN
    let actual_result = validate_gregorian_civil(given_json);

    // EXPECTATIONS
    let expected_status = Status::Valid;

    // THEN
    assert_eq!(expected_status, actual_result.status);
}
