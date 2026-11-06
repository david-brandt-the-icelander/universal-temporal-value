//! How to use `utv::parse`, which reads any value in the UTV text form and returns the type the
//! text describes, compared with chrono's `DateTime::parse_from_rfc3339`.
//!
//! `utv::parse` is a free function at the crate root, so this module is the function: its tests
//! sit directly in it, with no inner module per function.

use std::error::Error;

use chrono::DateTime;
use utv::{FixedOffsetDateTime, Instant, Value};

// BEGIN parse-offsets
/// Verifies that utv::parse returns an Instant when the text ends in Z.
/// Compared with: `DateTime::parse_from_rfc3339` gives a `DateTime<FixedOffset>` at +00:00, the same as for the
/// text "+00:00".
#[test]
fn returns_an_instant_when_the_text_ends_in_z() -> Result<(), Box<dyn Error>> {
    // GIVEN: 14:30 UTC, written with Z: a moment, with no local wall time implied
    let given_text = "2026-11-05T14:30:00Z";

    // WHEN
    let actual_outcome = utv::parse(given_text);

    // EXPECTATIONS: the moment chrono parses from the same text
    let expected_instant = Instant::try_from(DateTime::parse_from_rfc3339("2026-11-05T14:30:00Z")?)?;

    // THEN
    assert_eq!(Some(Value::Instant(expected_instant)), actual_outcome.unique());
    Ok(())
}

/// Verifies that utv::parse returns a FixedOffsetDateTime when the text ends in +00:00.
/// Compared with: `DateTime::parse_from_rfc3339` cannot tell this text from the one ending in Z.
#[test]
fn returns_a_fixed_offset_date_time_when_the_text_ends_in_plus_zero() -> Result<(), Box<dyn Error>> {
    // GIVEN: 14:30 at offset +00:00: a wall time somewhere that is at UTC+0 right now
    let given_text = "2026-11-05T14:30:00+00:00";

    // WHEN
    let actual_outcome = utv::parse(given_text);

    // EXPECTATIONS: the value chrono parses from the same text, and the text kept as written
    let expected_date_time = FixedOffsetDateTime::try_from(DateTime::parse_from_rfc3339("2026-11-05T14:30:00+00:00")?)?;
    let expected_text = "2026-11-05T14:30:00+00:00";

    // THEN
    let actual_value = actual_outcome.single()?;
    assert_eq!(Value::FixedOffset(expected_date_time), actual_value);
    assert_eq!(expected_text, actual_value.to_string());
    Ok(())
}
// END parse-offsets
