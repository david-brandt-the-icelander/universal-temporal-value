//! What chrono's `Utc` does when it reads the clock, compared with UTV's `Instant`, which never
//! reads the clock and takes the reading as an input.

use std::error::Error;

use chrono::{DateTime, Utc};
use utv::Instant;

mod now {
    use super::*;

    /// Verifies that Utc::now returns a reading that UTV takes as an input without changing it.
    /// Compared with: UTV never reads the clock; `Instant::try_from` is how the reading gets in.
    ///
    /// This test reads the real clock on purpose: the round trip does not depend on the time read, so the result is
    /// the same on every run. If it ever flakes, that is a finding about the conversion, not noise.
    #[test]
    fn survives_the_trip_through_utv_when_read_at_any_time() -> Result<(), Box<dyn Error>> {
        // WHEN
        let actual_reading = Utc::now();

        // EXPECTATIONS: the same reading after UTV has held it as an Instant
        let expected_reading = DateTime::<Utc>::try_from(Instant::try_from(actual_reading)?)?;

        // THEN
        assert_eq!(expected_reading, actual_reading);
        Ok(())
    }
}
