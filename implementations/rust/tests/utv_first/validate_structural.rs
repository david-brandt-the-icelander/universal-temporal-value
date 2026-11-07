//! How to use `utv::validate_structural`, which checks that JSON is well-formed UTV before anything
//! interprets it, compared with chrono, which has no temporal values beyond the everyday types:
//! the values here are written as UTV JSON because chrono has nothing to write them with.
//!
//! `validate_structural` is a free function at the crate root, so this module is the function:
//! its tests sit directly in it, with no inner module per function.

use utv::{validate_structural, Status};

/// Verifies that validate_structural accepts a value that is only a year.
/// Compared with: `NaiveDate::from_ymd_opt(1347, 1, 1)`, the usual stand-in, claims a day nobody knows.
#[test]
fn returns_valid_when_the_value_is_only_a_year() {
    // GIVEN: the year 1347, with no month or day
    let given_json = r#"
        { "schema": "utv/1.0", "kind": "civil",
          "value": { "form": "year", "fields": { "year": "1347" },
                     "calendar": { "id": "gregory", "chronology": "proleptic" } } }
        "#;

    // WHEN
    let actual_result = validate_structural(given_json);

    // EXPECTATIONS
    let expected_status = Status::Valid;

    // THEN
    assert_eq!(expected_status, actual_result.status);
}

/// Verifies that validate_structural accepts a leap-second label, leaving its meaning to a later check.
/// Compared with: `NaiveDate::and_hms_opt(23, 59, 60)` returns `None`; chrono can only write the second as 59 plus
/// 1,000 milliseconds.
#[test]
fn returns_valid_when_the_second_is_a_leap_second() {
    // GIVEN: 23:59:60 UTC on 31 December 2016, a real leap second
    let given_json = r#"
        { "schema": "utv/1.0", "kind": "civil",
          "value": { "form": "datetime",
                     "fields": { "year": "2016", "month": 12, "day": 31, "hour": 23, "minute": 59, "second": 60 },
                     "calendar": { "id": "gregory", "chronology": "proleptic" },
                     "zone": { "type": "fixed-offset", "seconds": 0 }, "zoneIntent": "fixed-offset" } }
        "#;

    // WHEN
    let actual_result = validate_structural(given_json);

    // EXPECTATIONS: well-formed; confirming the label needs a leap-second table
    let expected_status = Status::Valid;

    // THEN
    assert_eq!(expected_status, actual_result.status);
}

/// Verifies that validate_structural accepts an interval stored as a start plus an amount.
/// Compared with: a chrono `(start, start + TimeDelta)` pair stores a computed end, and forgets how it was written.
#[test]
fn returns_valid_when_the_interval_is_a_start_plus_an_amount() {
    // GIVEN: a two-hour meeting starting at 12:30 UTC on 4 November 2026
    let given_json = r#"
        { "schema": "utv/1.0", "kind": "interval",
          "value": { "domain": "physical", "construction": "start-amount",
                     "start": { "state": "included",
                                "point": { "schema": "utv/1.0", "kind": "instant",
                                           "value": { "system": { "system": "utv.timescale", "id": "unix-posix", "version": "1" },
                                                      "ticks": "1793795400" } } },
                     "amount": { "schema": "utv/1.0", "kind": "amount",
                                 "value": { "basis": "elapsed", "seconds": { "numerator": "7200", "denominator": "1" } } } } }
        "#;

    // WHEN
    let actual_result = validate_structural(given_json);

    // EXPECTATIONS
    let expected_status = Status::Valid;

    // THEN
    assert_eq!(expected_status, actual_result.status);
}

/// Verifies that validate_structural accepts a union of two separate date ranges.
/// Compared with: std's ranges hold one span each, so two ranges are a `Vec<Range<NaiveDate>>` that means whatever
/// its code says.
#[test]
fn returns_valid_when_the_value_is_a_union_of_ranges() {
    // GIVEN: 1–5 November and 20–25 November 2026, as one value
    let given_json = r#"
        { "schema": "utv/1.0", "kind": "set",
          "value": { "operation": "union", "members": [
            { "schema": "utv/1.0", "kind": "interval",
              "value": { "domain": "civil/gregory",
                         "start": { "state": "included", "point": { "schema": "utv/1.0", "kind": "civil",
                                    "value": { "form": "date", "fields": { "year": "2026", "month": 11, "day": 1 },
                                               "calendar": { "id": "gregory", "chronology": "proleptic" } } } },
                         "end": { "state": "excluded", "point": { "schema": "utv/1.0", "kind": "civil",
                                  "value": { "form": "date", "fields": { "year": "2026", "month": 11, "day": 6 },
                                             "calendar": { "id": "gregory", "chronology": "proleptic" } } } } } },
            { "schema": "utv/1.0", "kind": "interval",
              "value": { "domain": "civil/gregory",
                         "start": { "state": "included", "point": { "schema": "utv/1.0", "kind": "civil",
                                    "value": { "form": "date", "fields": { "year": "2026", "month": 11, "day": 20 },
                                               "calendar": { "id": "gregory", "chronology": "proleptic" } } } },
                         "end": { "state": "excluded", "point": { "schema": "utv/1.0", "kind": "civil",
                                  "value": { "form": "date", "fields": { "year": "2026", "month": 11, "day": 26 },
                                             "calendar": { "id": "gregory", "chronology": "proleptic" } } } } } } ] } }
        "#;

    // WHEN
    let actual_result = validate_structural(given_json);

    // EXPECTATIONS
    let expected_status = Status::Valid;

    // THEN
    assert_eq!(expected_status, actual_result.status);
}

/// Verifies that validate_structural accepts a weekly recurrence anchored in a named zone.
/// Compared with: chrono has no recurrence type; a list of computed `DateTime`s goes stale when zone rules change.
#[test]
fn returns_valid_when_the_value_is_a_weekly_recurrence() {
    // GIVEN: every Tuesday at 09:00 in Paris, from 3 November 2026, as an RFC 5545 rule
    let given_json = r#"
        { "schema": "utv/1.0", "kind": "recurrence",
          "value": { "intent": { "schema": "utv/1.0", "kind": "civil",
                                 "value": { "form": "datetime",
                                            "fields": { "year": "2026", "month": 11, "day": 3, "hour": 9, "minute": 0 },
                                            "calendar": { "id": "gregory", "chronology": "proleptic" },
                                            "zone": { "type": "iana", "id": "Europe/Paris" }, "zoneIntent": "follow-zone-id" } },
                     "algorithm": { "system": "rfc", "id": "5545", "version": "1" },
                     "rule": "FREQ=WEEKLY;BYDAY=TU" } }
        "#;

    // WHEN
    let actual_result = validate_structural(given_json);

    // EXPECTATIONS
    let expected_status = Status::Valid;

    // THEN
    assert_eq!(expected_status, actual_result.status);
}

/// Verifies that validate_structural accepts a year known only to lie within bounds.
/// Compared with: chrono can only store one date, such as 1347-01-01, and hope nobody asks.
#[test]
fn returns_valid_when_the_year_is_uncertain() {
    // GIVEN: about 1347, somewhere between 1340 and 1355
    let given_json = r#"
        { "schema": "utv/1.0", "kind": "uncertain",
          "value": { "target": { "schema": "utv/1.0", "kind": "civil",
                                 "value": { "form": "year", "fields": { "year": "1347" },
                                            "calendar": { "id": "gregory", "chronology": "proleptic" } } },
                     "model": "bounds",
                     "parameters": { "earliest": { "year": "1340" }, "latest": { "year": "1355" },
                                     "interpretation": "possible range, not 95% confidence" } } }
        "#;

    // WHEN
    let actual_result = validate_structural(given_json);

    // EXPECTATIONS
    let expected_status = Status::Valid;

    // THEN
    assert_eq!(expected_status, actual_result.status);
}

/// Verifies that validate_structural returns unsupported when the value is from a newer version.
/// Compared with: a serde deserializer would fail with a parse error, or read it as if nothing had changed.
#[test]
fn returns_unsupported_when_the_version_is_newer() {
    // GIVEN: a well-formed date labelled with a future specification version
    let given_json = r#"
        { "schema": "utv/2.0", "kind": "civil",
          "value": { "form": "date", "fields": { "year": "2026", "month": 11, "day": 5 },
                     "calendar": { "id": "gregory", "chronology": "proleptic" } } }
        "#;

    // WHEN
    let actual_result = validate_structural(given_json);

    // EXPECTATIONS: unsupported, not invalid: the value may be fine, this library just doesn't read it
    let expected_status = Status::Unsupported;
    let expected_code = "envelope.unsupported-version";

    // THEN
    assert_eq!(expected_status, actual_result.status);
    assert!(actual_result.diagnostics.iter().any(|d| d.code == expected_code));
}

/// Verifies that validate_structural returns invalid when the version label is malformed.
/// Compared with: chrono has no versioned format to check.
#[test]
fn returns_invalid_when_the_version_label_is_malformed() {
    // GIVEN: a date labelled "utv/4", an unreleased draft label
    let given_json = r#"
        { "schema": "utv/4", "kind": "civil",
          "value": { "form": "date", "fields": { "year": "2026", "month": 11, "day": 5 },
                     "calendar": { "id": "gregory", "chronology": "proleptic" } } }
        "#;

    // WHEN
    let actual_result = validate_structural(given_json);

    // EXPECTATIONS: invalid, with the envelope's schema label as the reason
    let expected_status = Status::Invalid;
    let expected_code = "envelope.schema";

    // THEN
    assert_eq!(expected_status, actual_result.status);
    assert!(actual_result.diagnostics.iter().any(|d| d.code == expected_code));
}

/// Verifies that validate_structural returns invalid when a key appears twice.
/// Compared with: `serde_json::from_str` accepts the text and keeps the last month.
///
/// This library reports the duplicate as `syntax` (invalid JSON text), with the key in the message; the C#, Java,
/// Python and TypeScript libraries report `duplicate-key`.
#[test]
fn returns_invalid_when_a_key_appears_twice() {
    // GIVEN: a date with two different months
    let given_json = r#"{"schema":"utv/1.0","kind":"civil","value":{"form":"date","fields":{"year":"2026","month":11,"month":12,"day":5},"calendar":{"id":"gregory","chronology":"proleptic"}}}"#;

    // WHEN
    let actual_result = validate_structural(given_json);

    // EXPECTATIONS: invalid, rather than whichever month a parser happens to keep
    let expected_status = Status::Invalid;
    let expected_code = "syntax";

    // THEN
    assert_eq!(expected_status, actual_result.status);
    assert!(actual_result.diagnostics.iter().any(|d| d.code == expected_code));
}

/// Verifies that validate_structural returns invalid when the kind is unknown.
/// Compared with: chrono has no kinds; a serde enum would reject an unknown variant the same way.
#[test]
fn returns_invalid_when_the_kind_is_unknown() {
    // GIVEN: a value of a kind the specification does not define
    let given_json = r#"
        { "schema": "utv/1.0", "kind": "moment", "value": {} }
        "#;

    // WHEN
    let actual_result = validate_structural(given_json);

    // EXPECTATIONS: fail closed: an unknown kind is rejected, not ignored
    let expected_status = Status::Invalid;
    let expected_code = "envelope.kind";

    // THEN
    assert_eq!(expected_status, actual_result.status);
    assert!(actual_result.diagnostics.iter().any(|d| d.code == expected_code));
}
