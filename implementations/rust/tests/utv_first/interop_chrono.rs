//! How to use `utv::interop::chrono`, the JSON-layer adapters behind the `chrono` cargo feature,
//! compared with chrono's own clock readings. The typed values convert with `From` and `TryFrom`
//! (see the tests of each type); this module holds the one conversion that has no typed form.

use chrono::{DateTime, Local};
use utv::interop::chrono::from_local;
use utv::Status;

mod from_local {
    use super::*;

    // BEGIN now
    /// Verifies that interop::chrono::from_local returns unsupported whatever the `DateTime<Local>` holds.
    /// Compared with: `Local::now()` returns a `DateTime<Local>`, a wall time that only means something on this
    /// machine, so UTV offers no typed conversion from it at all.
    ///
    /// Making the given value reads this machine's zone on purpose, because a `DateTime<Local>` cannot be made
    /// without it; the outcome is the same for every zone and every moment.
    #[test]
    fn returns_unsupported_when_given_a_local_date_time() {
        // GIVEN: a reading shaped like Local::now()'s: 13:30 UTC on 5 November 2026, in this machine's zone
        let given_reading: DateTime<Local> = DateTime::from_timestamp(1_793_885_400, 0).unwrap().with_timezone(&Local);

        // WHEN
        let actual_result = from_local(given_reading);

        // EXPECTATIONS: refused, because the host's zone is hidden input
        let expected_status = Status::Unsupported;
        let expected_code = "local-zone";

        // THEN
        assert_eq!(expected_status, actual_result.status);
        assert!(actual_result.diagnostics.iter().any(|d| d.code == expected_code));
    }
    // END now
}
