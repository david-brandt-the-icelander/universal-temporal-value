//! What `std::time::SystemTime` does, compared with UTV's `Instant`. `SystemTime` is the only time
//! type in Rust's standard library: a clock reading, with no calendar.

use std::error::Error;
use std::time::SystemTime;

use utv::Instant;

mod now {
    use super::*;

    /// Verifies that SystemTime::now returns a reading that UTV takes as an input without changing it.
    /// Compared with: UTV never reads the clock; `Instant::from_system_time` is how the reading gets in.
    ///
    /// This test reads the real clock on purpose: the round trip does not depend on the time read, so the result is
    /// the same on every run. If it ever flakes, that is a finding about the conversion, not noise.
    #[test]
    fn survives_the_trip_through_utv_when_read_at_any_time() -> Result<(), Box<dyn Error>> {
        // WHEN
        let actual_reading = SystemTime::now();

        // EXPECTATIONS: the same reading after UTV has held it as an Instant
        let expected_reading = SystemTime::try_from(Instant::from_system_time(actual_reading))?;

        // THEN
        assert_eq!(expected_reading, actual_reading);
        Ok(())
    }
}
