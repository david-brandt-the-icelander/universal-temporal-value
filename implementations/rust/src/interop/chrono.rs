//! Adapters for the `chrono` crate (cargo feature `chrono`).
//!
//! | chrono | UTV |
//! |---|---|
//! | `NaiveDate` | `date` |
//! | `NaiveTime` | `time` (always with `second`) |
//! | `NaiveDateTime` | unzoned (floating) `datetime` |
//! | `DateTime<FixedOffset>` | `instant` **or** fixed-offset `datetime`: the caller picks with [`OffsetIntent`] |
//! | `DateTime<Utc>` | `instant` |
//! | `DateTime<Local>` | refused: `unsupported` |
//!
//! `to_*` functions return `Err(UtvResult)` with status `unsupported` when chrono can't hold
//! the value exactly: years outside chrono's range (about ±262 000), fractions finer than a
//! nanosecond, a zone or offset the target type would drop, or a named-zone intention.
//!
//! ```
//! use chrono::NaiveDate;
//! use utv::interop::chrono::{from_naive_date, to_naive_date};
//! let d = NaiveDate::from_ymd_opt(2026, 11, 5).unwrap();
//! let v = from_naive_date(d);
//! assert_eq!(utv::format_iso(v.value().unwrap()).as_str(), Some("2026-11-05"));
//! assert_eq!(to_naive_date(v.value().unwrap()), Ok(d));
//! assert!(to_naive_date(&utv::build::date(275760, 9, 13)).is_err()); // beyond chrono's range
//! ```

use ::chrono::{DateTime, Datelike, FixedOffset, Local, NaiveDate, NaiveDateTime, NaiveTime, TimeZone, Timelike, Utc};
use serde_json::Value;

use super::{convert, convert_back, lossy};
use crate::model::{fail, instant_json, kind, read_civil, read_instant, Civil, Form, Zone, R};
use crate::num::Q;
use crate::{Status, UtvResult};

/// What a `DateTime<FixedOffset>` means to the caller.
#[derive(Debug, Clone, Copy, PartialEq, Eq)]
pub enum OffsetIntent {
    /// A point on the timeline; the offset was only how it was written. Nothing civil is kept.
    Instant,
    /// A wall-clock date-time at that fixed offset (`with_offset`).
    OffsetDateTime,
}

fn leap_second<T>() -> R<T> {
    fail(
        Status::Unsupported,
        "leap-second",
        "",
        "chrono represents a leap second here; UTV needs a leap-second table for it",
    )
}

fn civil_date(c: &mut Civil, d: NaiveDate) {
    (c.year, c.month, c.day) = (d.year() as i128, d.month(), d.day());
}

fn civil_time(c: &mut Civil, t: NaiveTime) -> R<()> {
    if t.nanosecond() >= 1_000_000_000 {
        return leap_second();
    }
    (c.hour, c.minute, c.second) = (t.hour(), t.minute(), Some(t.second()));
    c.nano = Some(t.nanosecond()).filter(|n| *n != 0);
    Ok(())
}

fn unique(r: R<Value>) -> UtvResult {
    match r {
        Ok(v) => UtvResult::unique(v),
        Err(e) => e,
    }
}

/// A `NaiveDate` as a UTV `date`.
pub fn from_naive_date(d: NaiveDate) -> UtvResult {
    convert({
        let mut c = Civil::new(Form::Date);
        civil_date(&mut c, d);
        UtvResult::unique(c.to_json())
    })
}

/// A `NaiveTime` as a UTV `time`, always with `second`. A leap second is `unsupported`.
pub fn from_naive_time(t: NaiveTime) -> UtvResult {
    convert({
        let mut c = Civil::new(Form::Time);
        unique(civil_time(&mut c, t).map(|_| c.to_json()))
    })
}

/// A `NaiveDateTime` as an unzoned (floating) UTV `datetime`. chrono's naive values have no
/// zone, and UTV keeps it that way: it is not read as local time or as UTC.
pub fn from_naive_date_time(dt: NaiveDateTime) -> UtvResult {
    convert({
        let mut c = Civil::new(Form::DateTime);
        civil_date(&mut c, dt.date());
        unique(civil_time(&mut c, dt.time()).map(|_| c.to_json()))
    })
}

/// A `DateTime<FixedOffset>` as the caller's choice of an instant or a fixed-offset date-time.
pub fn from_fixed_offset(dt: DateTime<FixedOffset>, intent: OffsetIntent) -> UtvResult {
    convert({
        match intent {
            OffsetIntent::Instant => instant_of(dt.timestamp(), dt.timestamp_subsec_nanos()),
            OffsetIntent::OffsetDateTime => {
                let mut c = Civil::new(Form::DateTime);
                civil_date(&mut c, dt.date_naive());
                c.zone = Zone::Fixed(dt.offset().local_minus_utc() as i64);
                unique(civil_time(&mut c, dt.time()).map(|_| c.to_json()))
            }
        }
    })
}

/// A `DateTime<Utc>` as a POSIX instant.
pub fn from_utc(dt: DateTime<Utc>) -> UtvResult {
    convert(instant_of(dt.timestamp(), dt.timestamp_subsec_nanos()))
}

/// Always `unsupported`: a `DateTime<Local>` depends on the host's time zone, and UTV never
/// interprets anything with the machine's zone. Convert it yourself with an explicit intent,
/// for example `from_fixed_offset(dt.fixed_offset(), OffsetIntent::Instant)`, or name the zone.
pub fn from_local(_dt: DateTime<Local>) -> UtvResult {
    convert({
        UtvResult::fail(
            Status::Unsupported,
            "local-zone",
            "",
            "chrono::Local depends on the host's time zone; convert with an explicit intent (from_fixed_offset or from_utc) instead",
        )
    })
}

fn instant_of(secs: i64, nanos: u32) -> UtvResult {
    if nanos >= 1_000_000_000 {
        return leap_second::<UtvResult>().unwrap_or_else(|e| e);
    }
    match Q::from_secs_nanos(secs as i128, nanos) {
        Some(q) => UtvResult::unique(instant_json(q)),
        None => UtvResult::fail(Status::Unsupported, "implementation-limit", "", "out of range"),
    }
}

// ---------- back to chrono ----------

fn read_form(v: &Value, form: Form, zone_ok: bool) -> R<Civil> {
    if kind(v) != "civil" {
        crate::model::structural(v)?;
        return lossy("unsupported-operand", format!("a `{}` value is not a chrono {}", kind(v), form.as_str()));
    }
    let c = read_civil(v)?;
    if c.form != form {
        return lossy("unsupported-operand", format!("expected a `{}`, got a `{}`", form.as_str(), c.form.as_str()));
    }
    if !zone_ok && c.zone != Zone::Floating {
        return lossy("zone-dropped", "this chrono type has no zone or offset; converting would drop it");
    }
    Ok(c)
}

fn naive_date(c: &Civil) -> R<NaiveDate> {
    i32::try_from(c.year)
        .ok()
        .and_then(|y| NaiveDate::from_ymd_opt(y, c.month, c.day))
        .map_or_else(|| lossy("out-of-native-range", format!("year {} is outside chrono's range", c.year)), Ok)
}

fn naive_time(c: &Civil) -> R<NaiveTime> {
    NaiveTime::from_hms_nano_opt(c.hour, c.minute, c.second.unwrap_or(0), c.nano.unwrap_or(0))
        .map_or_else(|| lossy("out-of-native-range", "time out of chrono's range"), Ok)
}

/// A UTV `date` (unzoned) as a `NaiveDate`.
pub fn to_naive_date(v: &Value) -> Result<NaiveDate, UtvResult> {
    convert_back(|| naive_date(&read_form(v, Form::Date, false)?))
}

/// A UTV `time` (unzoned) as a `NaiveTime`. An absent `second` becomes 0.
pub fn to_naive_time(v: &Value) -> Result<NaiveTime, UtvResult> {
    convert_back(|| naive_time(&read_form(v, Form::Time, false)?))
}

/// An unzoned UTV `datetime` as a `NaiveDateTime`. Zoned and fixed-offset values are refused:
/// `NaiveDateTime` would drop the zone.
pub fn to_naive_date_time(v: &Value) -> Result<NaiveDateTime, UtvResult> {
    convert_back(|| {
        let c = read_form(v, Form::DateTime, false)?;
        Ok(NaiveDateTime::new(naive_date(&c)?, naive_time(&c)?))
    })
}

/// A fixed-offset UTV `datetime` as a `DateTime<FixedOffset>`, exactly. A named-zone value is
/// refused even with an `offsetAssertion`: `FixedOffset` would keep the offset and drop the
/// zone's rules. Resolve it and use `to_offset` (or [`to_utc`]) to say which you mean.
pub fn to_fixed_offset(v: &Value) -> Result<DateTime<FixedOffset>, UtvResult> {
    convert_back(|| {
        let c = read_form(v, Form::DateTime, true)?;
        let offset = match c.zone {
            Zone::Fixed(s) => s,
            Zone::Iana(_) => {
                return lossy(
                    "zone-dropped",
                    "a named-zone intention can't become a FixedOffset without losing its rules; resolve it first",
                )
            }
            _ => return lossy("unsupported-operand", "an unzoned date-time has no offset; use to_naive_date_time"),
        };
        let off = i32::try_from(offset)
            .ok()
            .and_then(FixedOffset::east_opt)
            .map_or_else(|| lossy("out-of-native-range", "offset outside chrono's range"), Ok)?;
        let local = NaiveDateTime::new(naive_date(&c)?, naive_time(&c)?);
        off.from_local_datetime(&local)
            .single()
            .map_or_else(|| lossy("out-of-native-range", "outside chrono's range"), Ok)
    })
}

/// A POSIX instant as a `DateTime<Utc>`. Fractions finer than a nanosecond and instants outside
/// chrono's range are refused.
pub fn to_utc(v: &Value) -> Result<DateTime<Utc>, UtvResult> {
    convert_back(|| {
        let q = read_instant(v)?;
        let Some(nanos) = q.frac_nanos() else {
            return lossy("precision-loss", "chrono holds whole nanoseconds; this instant has a finer fraction");
        };
        i64::try_from(q.floor())
            .ok()
            .and_then(|s| DateTime::from_timestamp(s, nanos))
            .map_or_else(|| lossy("out-of-native-range", "outside chrono's range"), Ok)
    })
}

// ---------- typed values ----------
//
// The target type names the intent: `Instant::try_from(dt)` or `FixedOffsetDateTime::try_from(dt)`
// for a `DateTime<FixedOffset>`. There is deliberately no conversion from `DateTime<Local>`.

use crate::typed::{rejected, Typed};
use crate::{CivilDate, CivilDateTime, CivilTime, Diagnostics, FixedOffsetDateTime, Instant};

fn typed<T: Typed>(r: UtvResult) -> Result<T, Diagnostics> {
    match r.value() {
        Some(v) => T::read(v).map_err(rejected),
        None => Err(rejected(r)),
    }
}

fn native<T>(r: Result<T, UtvResult>) -> Result<T, Diagnostics> {
    r.map_err(rejected)
}

impl From<NaiveDate> for CivilDate {
    fn from(d: NaiveDate) -> CivilDate {
        typed(from_naive_date(d)).expect("every NaiveDate is a civil date")
    }
}

impl TryFrom<CivilDate> for NaiveDate {
    type Error = Diagnostics;
    /// `Err` (`out-of-native-range`) for years beyond chrono's range (about ±262 000).
    fn try_from(d: CivilDate) -> Result<NaiveDate, Diagnostics> {
        native(to_naive_date(&d.to_json()))
    }
}

impl TryFrom<NaiveTime> for CivilTime {
    type Error = Diagnostics;
    /// Always with a second. `Err` (`leap-second`) for chrono's leap-second representation.
    fn try_from(t: NaiveTime) -> Result<CivilTime, Diagnostics> {
        typed(from_naive_time(t))
    }
}

impl From<CivilTime> for NaiveTime {
    /// A missing second becomes 0.
    fn from(t: CivilTime) -> NaiveTime {
        to_naive_time(&t.to_json()).expect("every civil time fits a NaiveTime")
    }
}

impl TryFrom<NaiveDateTime> for CivilDateTime {
    type Error = Diagnostics;
    fn try_from(dt: NaiveDateTime) -> Result<CivilDateTime, Diagnostics> {
        typed(from_naive_date_time(dt))
    }
}

impl TryFrom<CivilDateTime> for NaiveDateTime {
    type Error = Diagnostics;
    fn try_from(dt: CivilDateTime) -> Result<NaiveDateTime, Diagnostics> {
        native(to_naive_date_time(&dt.to_json()))
    }
}

impl TryFrom<DateTime<FixedOffset>> for Instant {
    type Error = Diagnostics;
    /// The point on the timeline; the offset was only how it was written.
    fn try_from(dt: DateTime<FixedOffset>) -> Result<Instant, Diagnostics> {
        typed(from_fixed_offset(dt, OffsetIntent::Instant))
    }
}

impl TryFrom<DateTime<FixedOffset>> for FixedOffsetDateTime {
    type Error = Diagnostics;
    /// The wall time at that offset.
    fn try_from(dt: DateTime<FixedOffset>) -> Result<FixedOffsetDateTime, Diagnostics> {
        typed(from_fixed_offset(dt, OffsetIntent::OffsetDateTime))
    }
}

impl TryFrom<FixedOffsetDateTime> for DateTime<FixedOffset> {
    type Error = Diagnostics;
    fn try_from(dt: FixedOffsetDateTime) -> Result<DateTime<FixedOffset>, Diagnostics> {
        native(to_fixed_offset(&dt.to_json()))
    }
}

impl TryFrom<DateTime<Utc>> for Instant {
    type Error = Diagnostics;
    fn try_from(dt: DateTime<Utc>) -> Result<Instant, Diagnostics> {
        typed(from_utc(dt))
    }
}

impl TryFrom<Instant> for DateTime<Utc> {
    type Error = Diagnostics;
    /// `Err` (`out-of-native-range`, `precision-loss`) when chrono can't hold the instant exactly.
    fn try_from(at: Instant) -> Result<DateTime<Utc>, Diagnostics> {
        native(to_utc(&at.to_json()))
    }
}

#[cfg(test)]
mod tests {
    use super::*;
    use crate::build::{date, date_time, date_time_with_seconds, in_zone, instant_with_nanos, time, with_offset};
    use crate::format_iso;

    fn iso(r: &UtvResult) -> String {
        format_iso(r.value().expect("unique")).as_str().unwrap().to_owned()
    }

    #[test]
    fn naive_round_trips() {
        let d = NaiveDate::from_ymd_opt(2026, 11, 5).unwrap();
        assert_eq!(from_naive_date(d).value(), Some(&date(2026, 11, 5)));
        assert_eq!(to_naive_date(&date(2026, 11, 5)), Ok(d));

        let t = NaiveTime::from_hms_nano_opt(9, 30, 15, 250_000_000).unwrap();
        assert_eq!(iso(&from_naive_time(t)), "09:30:15.25");
        assert_eq!(to_naive_time(from_naive_time(t).value().unwrap()), Ok(t));
        assert_eq!(to_naive_time(&time(9, 30)), Ok(NaiveTime::from_hms_opt(9, 30, 0).unwrap()));

        let dt = d.and_hms_opt(23, 30, 0).unwrap();
        assert_eq!(
            from_naive_date_time(dt).value(),
            Some(&date_time_with_seconds(2026, 11, 5, 23, 30, 0))
        );
        assert_eq!(to_naive_date_time(&date_time(2026, 11, 5, 23, 30)), Ok(dt));
    }

    #[test]
    fn fixed_offset_needs_an_intent() {
        let dt = DateTime::parse_from_rfc3339("2026-11-05T14:30:00+01:00").unwrap();
        assert_eq!(iso(&from_fixed_offset(dt, OffsetIntent::Instant)), "2026-11-05T13:30:00Z");
        let odt = from_fixed_offset(dt, OffsetIntent::OffsetDateTime);
        assert_eq!(iso(&odt), "2026-11-05T14:30:00+01:00");
        assert_eq!(to_fixed_offset(odt.value().unwrap()), Ok(dt));
    }

    #[test]
    fn utc_round_trip() {
        let v = instant_with_nanos(1_793_885_400, 5);
        let u = to_utc(&v).unwrap();
        assert_eq!(from_utc(u).value(), Some(&v));
    }

    #[test]
    fn local_is_refused() {
        // Built from a fixed UTC reading, so the test doesn't read the clock.
        let local = Local.from_utc_datetime(&NaiveDate::from_ymd_opt(2026, 11, 5).unwrap().and_hms_opt(12, 0, 0).unwrap());
        let r = from_local(local);
        assert_eq!(r.status, Status::Unsupported);
        assert!(r.candidates.is_empty());
    }

    #[test]
    fn lossy_conversions_are_refused() {
        let unsupported = |r: Result<_, UtvResult>| r.map(|_: ()| ()).unwrap_err().status == Status::Unsupported;
        // Years beyond chrono's range.
        assert!(unsupported(to_naive_date(&date(275760, 9, 13)).map(drop)));
        // A zone the native type would drop.
        let paris = in_zone(date_time(2027, 6, 1, 9, 0), "Europe/Paris");
        assert!(unsupported(to_naive_date_time(&paris).map(drop)));
        assert!(unsupported(to_fixed_offset(&paris).map(drop)));
        assert!(unsupported(
            to_naive_date_time(&with_offset(date_time(2026, 11, 5, 9, 0), 3600)).map(drop)
        ));
        // Sub-nanosecond precision.
        let third = serde_json::json!({"schema":"utv/1.0","kind":"instant","value":{"system":crate::model::posix(),"ticks":"0","subTick":{"numerator":"1","denominator":"3"}}});
        assert!(unsupported(to_utc(&third).map(drop)));
        // Wrong kind or form.
        assert!(unsupported(to_naive_date(&time(9, 30)).map(drop)));
        assert!(unsupported(to_utc(&date(2026, 11, 5)).map(drop)));
        // Invalid input stays invalid.
        assert_eq!(to_naive_date(&date(2026, 2, 30)).unwrap_err().status, Status::Invalid);
    }

    #[test]
    fn typed_conversions() {
        let d = NaiveDate::from_ymd_opt(2026, 11, 5).unwrap();
        let civil = CivilDate::from(d);
        assert_eq!(civil.to_string(), "2026-11-05");
        assert_eq!(NaiveDate::try_from(civil.add_days(30)), Ok(NaiveDate::from_ymd_opt(2026, 12, 5).unwrap()));
        let far = CivilDate::of(275_760, 9, 13).unwrap();
        assert_eq!(NaiveDate::try_from(far).unwrap_err().codes(), ["out-of-native-range"]);
        let odt = DateTime::parse_from_rfc3339("2026-11-05T14:30:00+01:00").unwrap();
        assert_eq!(Instant::try_from(odt).unwrap().to_string(), "2026-11-05T13:30:00Z");
        let wall = FixedOffsetDateTime::try_from(odt).unwrap();
        assert_eq!(wall.to_string(), "2026-11-05T14:30:00+01:00");
        assert_eq!(DateTime::<FixedOffset>::try_from(wall), Ok(odt));
        let t = NaiveTime::from_hms_opt(9, 30, 0).unwrap();
        assert_eq!(NaiveTime::from(CivilTime::try_from(t).unwrap()), t);
        let leap = NaiveTime::from_hms_nano_opt(23, 59, 59, 1_500_000_000).unwrap();
        assert_eq!(CivilTime::try_from(leap).unwrap_err().codes(), ["leap-second"]);
        let utc = DateTime::from_timestamp(1_793_885_400, 5).unwrap();
        assert_eq!(DateTime::<Utc>::try_from(Instant::try_from(utc).unwrap()), Ok(utc));
        let naive = d.and_hms_opt(23, 30, 0).unwrap();
        assert_eq!(NaiveDateTime::try_from(CivilDateTime::try_from(naive).unwrap()), Ok(naive));
    }

    #[test]
    fn leap_second_is_unsupported() {
        let leap = NaiveTime::from_hms_nano_opt(23, 59, 59, 1_500_000_000).unwrap();
        assert_eq!(from_naive_time(leap).status, Status::Unsupported);
    }
}
