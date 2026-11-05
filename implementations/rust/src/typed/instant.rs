//! [`Instant`]: a point on the POSIX timeline.

use std::cmp::Ordering;
use std::time::SystemTime;

use serde_json::Value as Json;

use super::amount::{ElapsedSeconds, PosixSeconds};
use super::civil::{FixedOffsetDateTime, ZonedIntent};
use super::context::TemporalContext;
use super::outcome::{outcome, total, Diagnostics, Outcome};
use super::{kind_mismatch, rejected, typed_value, Typed};
use crate::model::{instant_json, kind, read_instant};
use crate::num::Q;
use crate::UtvResult;

/// A point on the timeline, as exact POSIX seconds since 1970-01-01T00:00:00Z:
/// `2026-11-05T13:30:00Z`. Fractions of a second are exact rationals, never floats.
///
/// ```
/// use utv::{Instant, PosixSeconds};
/// let a = Instant::from_unix_seconds_nanos(1_793_885_400, 250_000_000);
/// let b = a.add(PosixSeconds::of(3600));
/// assert_eq!(b.to_string(), "2026-11-05T14:30:00.25Z");
/// assert_eq!(a.until(b).to_string(), "3600 s (POSIX)");
/// assert!(a < b);
/// ```
#[derive(Clone, Copy, PartialEq, Eq, Hash)]
pub struct Instant {
    t: Q,
}

typed_value!(Instant);

impl Typed for Instant {
    fn read(json: &Json) -> Result<Self, UtvResult> {
        if kind(json) != "instant" {
            return kind_mismatch("an instant", json);
        }
        let t = read_instant(json)?;
        super::only_keys(json, &["schema", "kind", "value"])?;
        super::only_keys(&json["value"], &["system", "ticks", "subTick"])?;
        Ok(Instant { t })
    }
    fn json(&self) -> Json {
        instant_json(self.t)
    }
}

impl Instant {
    /// Whole seconds since the epoch.
    pub fn from_unix_seconds(seconds: i64) -> Instant {
        Instant { t: Q::int(seconds as i128) }
    }

    /// Seconds and nanoseconds since the epoch; nanoseconds of 10⁹ or more carry into seconds.
    pub fn from_unix_seconds_nanos(seconds: i64, nanos: u32) -> Instant {
        Instant::from_unix_nanoseconds(seconds as i128 * 1_000_000_000 + nanos as i128)
    }

    /// Nanoseconds since the epoch.
    pub fn from_unix_nanoseconds(nanos: i128) -> Instant {
        Instant { t: Q::new(nanos, 1_000_000_000).expect("a non-zero denominator") }
    }

    /// A clock reading you took yourself: the library never reads the clock.
    pub fn from_system_time(reading: SystemTime) -> Instant {
        total(crate::interop::from_system_time(reading), Instant::read)
    }

    /// Whole seconds since the epoch (rounded down).
    pub fn unix_seconds(self) -> i128 {
        self.t.floor()
    }

    /// The exact fraction of a second past [`Instant::unix_seconds`], in `[0, 1)`.
    pub fn sub_second(self) -> PosixSeconds {
        PosixSeconds::of_q(self.t.frac())
    }

    /// The nanoseconds within the second: the floor of the sub-second part (see
    /// [`Instant::sub_second`] for the exact fraction).
    pub fn nanoseconds(self) -> u32 {
        let f = self.t.frac();
        match f.n.checked_mul(1_000_000_000) {
            Some(scaled) => (scaled / f.d) as u32,
            None => (f.n / (f.d / 1_000_000_000).max(1)).min(999_999_999) as u32,
        }
    }

    /// Exactly `seconds` POSIX seconds later.
    pub fn add(&self, seconds: PosixSeconds) -> Instant {
        total(crate::ops::add(&self.json(), &seconds.json(), None), Instant::read)
    }

    /// SI seconds differ from POSIX seconds across a leap second, so this needs a leap-second
    /// table, which this library doesn't have: `unsupported` (`needs-leap-seconds`).
    pub fn add_elapsed(&self, seconds: ElapsedSeconds) -> Outcome<Instant> {
        outcome(crate::ops::add(&self.json(), &seconds.json(), None), Instant::read)
    }

    /// The POSIX seconds from `self` to `other`, exactly.
    pub fn until(self, other: Instant) -> PosixSeconds {
        total(crate::ops::posix_seconds_between(&self.json(), &other.json()), PosixSeconds::read)
    }

    /// The wall time at a fixed offset (seconds east of UTC). `Err` (`offset-out-of-range`)
    /// beyond ±18:00.
    pub fn at_offset(self, offset_seconds: i32) -> Result<FixedOffsetDateTime, Diagnostics> {
        let r = crate::zones::to_offset(&self.json(), offset_seconds as i64);
        match r.value() {
            Some(v) => FixedOffsetDateTime::read(v).map_err(rejected),
            None => Err(rejected(r)),
        }
    }

    /// The wall time in a named zone, with the offset in force asserted:
    /// `2027-06-01T03:00:00-04:00[America/New_York]`. `unsupported` outside the rules.
    pub fn in_zone(&self, zone_id: &str, ctx: &TemporalContext) -> Outcome<ZonedIntent> {
        ctx.in_zone(self, zone_id)
    }
}

impl Ord for Instant {
    fn cmp(&self, other: &Self) -> Ordering {
        self.t.cmp_exact(other.t)
    }
}

impl PartialOrd for Instant {
    fn partial_cmp(&self, other: &Self) -> Option<Ordering> {
        Some(self.cmp(other))
    }
}
