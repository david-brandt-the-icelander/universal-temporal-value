//! What `serde_json` does with temporal JSON, compared with UTV's `validate_structural`.

use std::error::Error;

use serde_json::Value;

mod from_str {
    use super::*;

    /// Verifies that serde_json::from_str accepts duplicate keys and keeps the last value.
    /// Compared with: `utv::validate_structural` rejects the same text as invalid (`syntax`, naming the duplicate key):
    /// two months in one date is a contradiction, not something to settle by taking the last.
    #[test]
    fn keeps_the_last_value_when_a_key_appears_twice() -> Result<(), Box<dyn Error>> {
        // GIVEN: a UTV date with two different months
        let given_json = r#"{"schema":"utv/1.0","kind":"civil","value":{"form":"date","fields":{"year":"2026","month":11,"month":12,"day":5},"calendar":{"id":"gregory","chronology":"proleptic"}}}"#;

        // WHEN
        let actual_document: Value = serde_json::from_str(given_json)?;

        // EXPECTATIONS: the second month, December
        let expected_month = 12;

        // THEN
        assert_eq!(Some(expected_month), actual_document["value"]["fields"]["month"].as_i64());
        Ok(())
    }
}
