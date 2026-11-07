//! What chrono's `Local` does when it reads the clock, compared with UTV, which refuses any value
//! that depends on the host's time zone.

use chrono::{DateTime, Local};
use utv::interop::chrono::from_local;
use utv::Status;

mod now {
    use super::*;

    /// Verifies that Local::now returns a reading tied to this machine's zone whatever the time is.
    /// Compared with: `utv::interop::chrono::from_local` refuses every `DateTime<Local>` as unsupported (local-zone),
    /// and UTV's typed values have no conversion from it at all.
    ///
    /// This test reads the real clock and this machine's zone on purpose: UTV's answer does not depend on either, so
    /// the result is the same on every run and on every machine.
    #[test]
    fn returns_a_reading_utv_refuses_when_read_at_any_time() {
        // WHEN
        let actual_reading: DateTime<Local> = Local::now();

        // EXPECTATIONS: the answer UTV gives for any DateTime<Local>
        let expected_status = Status::Unsupported;
        let expected_code = "local-zone";

        // THEN: what UTV says about this very reading. The reading is the actual value, so UTV's verdict on it is
        // taken here rather than in EXPECTATIONS, which cannot see it before WHEN.
        let actual_verdict = from_local(actual_reading);
        assert_eq!(expected_status, actual_verdict.status);
        assert!(actual_verdict.diagnostics.iter().any(|d| d.code == expected_code));
    }
}
