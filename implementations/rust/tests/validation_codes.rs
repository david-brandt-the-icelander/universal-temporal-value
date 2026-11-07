//! The diagnostic codes `validate_structural` and `validate_gregorian_civil` report for JSON text that can't be
//! read: `duplicate-key` for a repeated object key, `syntax` for anything else that isn't JSON
//! (docs/library-api.md §5).

use utv::{validate_gregorian_civil, validate_structural, Status};

mod validate_structural {
    use super::*;

    /// Verifies that validate_structural reports duplicate-key when an object repeats a key.
    #[test]
    fn reports_duplicate_key_when_an_object_repeats_a_key() {
        // GIVEN: a date with two different months
        let given_json = r#"{"schema":"utv/1.0","kind":"civil","value":{"form":"date","fields":{"year":"2026","month":11,"month":12,"day":5},"calendar":{"id":"gregory","chronology":"proleptic"}}}"#;

        // WHEN
        let actual_result = validate_structural(given_json);

        // EXPECTATIONS
        let expected_status = Status::Invalid;
        let expected_codes = vec!["duplicate-key".to_string()];

        // THEN
        assert_eq!(expected_status, actual_result.status);
        assert_eq!(expected_codes, actual_result.diagnostics.iter().map(|d| d.code.clone()).collect::<Vec<_>>());
    }

    /// Verifies that validate_structural reports syntax when the text is not JSON.
    #[test]
    fn reports_syntax_when_the_text_is_not_json() {
        // GIVEN: an object that is never closed
        let given_json = r#"{"schema":"utv/1.0","kind":"civil""#;

        // WHEN
        let actual_result = validate_structural(given_json);

        // EXPECTATIONS
        let expected_status = Status::Invalid;
        let expected_codes = vec!["syntax".to_string()];

        // THEN
        assert_eq!(expected_status, actual_result.status);
        assert_eq!(expected_codes, actual_result.diagnostics.iter().map(|d| d.code.clone()).collect::<Vec<_>>());
    }
}

mod validate_gregorian_civil {
    use super::*;

    /// Verifies that validate_gregorian_civil reports duplicate-key when an object repeats a key.
    #[test]
    fn reports_duplicate_key_when_an_object_repeats_a_key() {
        // GIVEN: a date with two different months
        let given_json = r#"{"schema":"utv/1.0","kind":"civil","value":{"form":"date","fields":{"year":"2026","month":11,"month":12,"day":5},"calendar":{"id":"gregory","chronology":"proleptic"}}}"#;

        // WHEN
        let actual_result = validate_gregorian_civil(given_json);

        // EXPECTATIONS
        let expected_status = Status::Invalid;
        let expected_codes = vec!["duplicate-key".to_string()];

        // THEN
        assert_eq!(expected_status, actual_result.status);
        assert_eq!(expected_codes, actual_result.diagnostics.iter().map(|d| d.code.clone()).collect::<Vec<_>>());
    }
}
