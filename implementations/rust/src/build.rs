//! Builders for the canonical shapes of `docs/everyday-api.md`.
//!
//! Builders only assemble values; they don't judge them. `date(2026, 2, 30)` is a well-formed
//! value of a date that doesn't exist, and operations on it (`fields`, `add`, `format_iso`)
//! answer `invalid`. Years are `i64` here and stored as decimal strings, so UTV itself is not
//! limited to four-digit years.
//!
//! ```
//! use utv::build::{date, date_time, in_zone, period, Period};
//! let meeting = in_zone(date_time(2027, 6, 1, 9, 0), "Europe/Paris");
//! let later = utv::add(&date(2026, 11, 5), &period(Period::days(30)), None);
//! assert_eq!(later.value(), Some(&date(2026, 12, 5)));
//! # let _ = meeting;
//! ```

use serde_json::{json, Value};

use crate::model::{instant_json, posix_seconds_json, Civil, Form};
use crate::num::Q;

#[allow(clippy::too_many_arguments)]
fn civil(form: Form, year: i64, month: u32, day: u32, hour: u32, minute: u32, second: Option<u32>, nano: Option<u32>) -> Value {
    let mut c = Civil::new(form);
    (c.year, c.month, c.day, c.hour, c.minute, c.second, c.nano) = (year as i128, month, day, hour, minute, second, nano);
    c.to_json()
}

/// A calendar date, `{"form":"date"}`.
pub fn date(year: i64, month: u32, day: u32) -> Value {
    civil(Form::Date, year, month, day, 0, 0, None, None)
}

/// A year and month, `{"form":"year-month"}`.
pub fn year_month(year: i64, month: u32) -> Value {
    civil(Form::YearMonth, year, month, 1, 0, 0, None, None)
}

/// A time of day without seconds (`09:30`).
pub fn time(hour: u32, minute: u32) -> Value {
    civil(Form::Time, 0, 1, 1, hour, minute, None, None)
}

/// A time of day with seconds (`09:30:15`).
pub fn time_with_seconds(hour: u32, minute: u32, second: u32) -> Value {
    civil(Form::Time, 0, 1, 1, hour, minute, Some(second), None)
}

/// A time of day with seconds and nanoseconds; `nano` is stored only if non-zero.
pub fn time_with_nanos(hour: u32, minute: u32, second: u32, nano: u32) -> Value {
    civil(Form::Time, 0, 1, 1, hour, minute, Some(second), Some(nano))
}

/// An unzoned ("floating") local date-time without seconds. No `zone` property.
pub fn date_time(year: i64, month: u32, day: u32, hour: u32, minute: u32) -> Value {
    civil(Form::DateTime, year, month, day, hour, minute, None, None)
}

/// An unzoned local date-time with seconds.
pub fn date_time_with_seconds(year: i64, month: u32, day: u32, hour: u32, minute: u32, second: u32) -> Value {
    civil(Form::DateTime, year, month, day, hour, minute, Some(second), None)
}

/// An unzoned local date-time with seconds and nanoseconds; `nano` is stored only if non-zero.
pub fn date_time_with_nanos(year: i64, month: u32, day: u32, hour: u32, minute: u32, second: u32, nano: u32) -> Value {
    civil(Form::DateTime, year, month, day, hour, minute, Some(second), Some(nano))
}

fn with_zone(mut dt: Value, zone: Value, intent: &str) -> Value {
    if let Some(inner) = dt.get_mut("value").and_then(Value::as_object_mut) {
        inner.insert("zone".into(), zone);
        inner.insert("zoneIntent".into(), intent.into());
    }
    dt
}

/// The same civil value, meant to follow the named IANA zone's rules
/// (`"zone":{"type":"iana","id":…}`, `"zoneIntent":"follow-zone-id"`).
pub fn in_zone(dt: Value, zone_id: &str) -> Value {
    with_zone(dt, json!({"type": "iana", "id": zone_id}), "follow-zone-id")
}

/// The same civil value at a fixed UTC offset in seconds east of UTC
/// (`"zone":{"type":"fixed-offset","seconds":…}`, `"zoneIntent":"fixed-offset"`).
pub fn with_offset(dt: Value, seconds: i32) -> Value {
    with_zone(dt, json!({"type": "fixed-offset", "seconds": seconds}), "fixed-offset")
}

/// A POSIX instant, whole seconds since 1970-01-01T00:00:00Z.
pub fn instant(epoch_seconds: i64) -> Value {
    instant_json(Q::int(epoch_seconds as i128))
}

/// A POSIX instant with nanoseconds (`0..1_000_000_000`, added to `epoch_seconds`). The
/// fraction is stored as a `subTick` reduced to lowest terms, only if non-zero.
pub fn instant_with_nanos(epoch_seconds: i64, nano: u32) -> Value {
    instant_json(Q::from_secs_nanos(epoch_seconds as i128, nano).expect("i64 seconds and u32 nanos fit in i128"))
}

pub use crate::typed::amount::Period;

/// A calendar amount (`{"basis":"calendar","components":{…}}`): only non-zero components,
/// as decimal strings. Each component may be negative.
///
/// ```
/// use utv::build::{period, Period};
/// let p = period(Period { months: 1, days: 1, ..Period::default() });
/// assert_eq!(p["value"]["components"], serde_json::json!({"months": "1", "days": "1"}));
/// ```
pub fn period(p: Period) -> Value {
    p.to_json()
}

/// An amount of POSIX seconds (`basis: clock-ticks`). POSIX seconds are not SI seconds across a
/// leap second.
pub fn posix_seconds(n: i64) -> Value {
    posix_seconds_json(Q::int(n as i128))
}

/// An amount of SI seconds (`basis: elapsed`). Adding it to a POSIX instant needs a
/// leap-second table, so `add` answers `unsupported` (`needs-leap-seconds`).
pub fn elapsed_seconds(n: i64) -> Value {
    crate::typed::amount::ElapsedSeconds::of(n).to_json()
}

/// One end of an interval.
#[derive(Debug, Clone, PartialEq)]
pub enum Endpoint {
    Included(Value),
    Excluded(Value),
    /// No bound on this side: the interval is open-ended.
    Unbounded,
    /// There is a bound, but it isn't known (yet).
    Unknown,
}

impl Endpoint {
    fn to_json(&self) -> Value {
        match self {
            Endpoint::Included(p) => json!({"state": "included", "point": p}),
            Endpoint::Excluded(p) => json!({"state": "excluded", "point": p}),
            Endpoint::Unbounded => json!({"state": "unbounded"}),
            Endpoint::Unknown => json!({"state": "unknown"}),
        }
    }
    fn point(&self) -> Option<&Value> {
        match self {
            Endpoint::Included(p) | Endpoint::Excluded(p) => Some(p),
            _ => None,
        }
    }
}

/// The half-open interval `[start, end)`. The domain is `physical` for instants and
/// `civil/gregory` for civil points.
pub fn interval(start: Value, end: Value) -> Value {
    interval_with(Endpoint::Included(start), Endpoint::Excluded(end))
}

/// An interval with explicit endpoints, including unbounded and unknown ones. The domain comes
/// from the points: `physical` for instants, otherwise `civil/gregory`.
pub fn interval_with(start: Endpoint, end: Endpoint) -> Value {
    let physical = [&start, &end].iter().filter_map(|e| e.point()).any(|p| p["kind"] == "instant");
    let domain = if physical { "physical" } else { "civil/gregory" };
    json!({"schema": crate::SPEC_VERSION, "kind": "interval", "value": {
        "domain": domain, "start": start.to_json(), "end": end.to_json()
    }})
}

