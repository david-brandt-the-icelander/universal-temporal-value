//! Explicit adapters between UTV values and Rust's own time types.
//!
//! The rules (`docs/everyday-api.md`, "Native interop"):
//!
//! - **The caller names the intent.** A function converts to exactly one kind of UTV value;
//!   where a native type could mean two things (chrono's `DateTime<FixedOffset>`), the caller
//!   passes the intent.
//! - **No local zone.** Native values that depend on the host's zone (`chrono::Local`) are
//!   refused with `unsupported`, never interpreted with the machine's zone.
//! - **Lossy conversions back to native say so.** `to_*` functions return
//!   `Err(UtvResult)` with status `unsupported` when the native type can't hold the value
//!   exactly (range, sub-native precision, or a zone intention the type can't carry).
//! - **"Now" is an explicit input.** The library never reads the clock. Read it yourself and
//!   pass the reading in: `from_system_time(SystemTime::now())`.
//!
//! `std` has no calendar types, only [`SystemTime`], which maps to an instant. Calendar types
//! come from the de-facto standard `chrono` crate, behind the optional cargo feature `chrono`
//! (module [`chrono`]).

use std::time::{Duration, SystemTime, UNIX_EPOCH};

use serde_json::Value;

use crate::model::{fail, instant_json, read_instant, R};
use crate::num::Q;
use crate::{Status, UtvResult};

#[cfg(feature = "chrono")]
pub mod chrono;

/// A `SystemTime` reading as a POSIX instant (`unique`). The library doesn't read the clock:
/// pass `SystemTime::now()` yourself, so the dependency on the host clock stays visible.
pub fn from_system_time(t: SystemTime) -> UtvResult {
    convert({
        let (sign, d) = match t.duration_since(UNIX_EPOCH) {
            Ok(d) => (1, d),
            Err(e) => (-1, e.duration()),
        };
        let q = Q::from_secs_nanos(d.as_secs() as i128, d.subsec_nanos()).and_then(|q| if sign < 0 { q.neg() } else { Some(q) });
        match q {
            Some(q) => UtvResult::unique(instant_json(q)),
            None => UtvResult::fail(Status::Unsupported, "implementation-limit", "", "SystemTime out of range"),
        }
    })
}

/// A POSIX instant as a `SystemTime`. `Err(unsupported)` if the platform's `SystemTime` can't
/// hold it exactly (out of range, or finer than its resolution, which is 1 ns on Unix and
/// 100 ns on Windows).
pub fn to_system_time(instant: &Value) -> Result<SystemTime, UtvResult> {
    convert_back(|| {
        let q = read_instant(instant)?;
        let t = system_time_of(q)?;
        // Round-trip check: catches platforms with coarser resolution than nanoseconds.
        match from_system_time(t).value() {
            Some(back) if read_instant(back).ok() == Some(q) => Ok(t),
            _ => lossy("precision-loss", "SystemTime on this platform can't hold this instant exactly"),
        }
    })
}

fn system_time_of(q: Q) -> R<SystemTime> {
    let Some(nanos) = q.frac_nanos() else {
        return lossy("precision-loss", "SystemTime holds whole nanoseconds; this instant has a finer fraction");
    };
    let secs = q.floor();
    let t = if secs >= 0 {
        u64::try_from(secs)
            .ok()
            .and_then(|s| UNIX_EPOCH.checked_add(Duration::new(s, nanos)))
    } else {
        // secs + nanos/1e9 with secs < 0: subtract (-secs) then add the nanos back.
        u64::try_from(-secs)
            .ok()
            .and_then(|s| UNIX_EPOCH.checked_sub(Duration::from_secs(s)))
            .and_then(|t| t.checked_add(Duration::from_nanos(nanos as u64)))
    };
    t.map_or_else(|| lossy("out-of-native-range", "outside the range of SystemTime on this platform"), Ok)
}

impl TryFrom<crate::Instant> for SystemTime {
    type Error = crate::Diagnostics;
    /// `Err` (`out-of-native-range`, `precision-loss`) when `SystemTime` can't hold the instant exactly.
    fn try_from(at: crate::Instant) -> Result<SystemTime, crate::Diagnostics> {
        to_system_time(&at.to_json()).map_err(crate::typed::rejected)
    }
}

/// Name a conversion's result after its operation, `convert`.
pub(crate) fn convert(r: UtvResult) -> UtvResult {
    r.op("convert")
}

/// Run a conversion back to a native type, naming any refusal after its operation, `convert`.
pub(crate) fn convert_back<T>(f: impl FnOnce() -> R<T>) -> Result<T, UtvResult> {
    f().map_err(convert)
}

pub(crate) fn lossy<T>(code: &str, message: impl Into<String>) -> R<T> {
    fail(Status::Unsupported, code, "", message)
}

#[cfg(test)]
mod tests {
    use super::*;
    use crate::build::{date, instant, instant_with_nanos};

    #[test]
    fn system_time_round_trip() {
        for v in [
            instant(1_793_885_400),
            instant(-1),
            instant_with_nanos(-2, 500_000_000),
            instant_with_nanos(0, 1),
        ] {
            let t = to_system_time(&v).expect("fits");
            assert_eq!(from_system_time(t).value(), Some(&v));
        }
        let before_epoch = UNIX_EPOCH - Duration::from_millis(1500);
        assert_eq!(from_system_time(before_epoch).value(), Some(&instant_with_nanos(-2, 500_000_000)));
        assert_eq!(from_system_time(before_epoch).operation, "convert");
    }

    #[test]
    fn lossy_back_to_system_time_is_refused() {
        let third = serde_json::json!({"schema":"utv/1.0","kind":"instant","value":{"system":crate::model::posix(),"ticks":"0","subTick":{"numerator":"1","denominator":"3"}}});
        assert_eq!(to_system_time(&third).unwrap_err().status, Status::Unsupported);
        let far = serde_json::json!({"schema":"utv/1.0","kind":"instant","value":{"system":crate::model::posix(),"ticks":"100000000000000000000000000"}});
        assert_eq!(to_system_time(&far).unwrap_err().status, Status::Unsupported);
        // A civil date is not an instant: the caller must resolve it first.
        assert_eq!(to_system_time(&date(2026, 11, 5)).unwrap_err().status, Status::Unsupported);
    }
}
