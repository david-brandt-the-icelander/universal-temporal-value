//! Calendar and instant operations: [`fields`], [`add`], [`compare`], [`days_between`],
//! [`posix_seconds_between`], [`contains`] and [`overlaps`].
//!
//! ```
//! use serde_json::json;
//! use utv::build::{date, period, Period};
//! use utv::{add, Status};
//! let jan31 = date(2026, 1, 31);
//! let month = period(Period::months(1));
//! assert_eq!(add(&jan31, &month, None).status, Status::None); // 31 Feb doesn't exist
//! let clamped = add(&jan31, &month, Some(&json!({"monthEnd": "constrain"})));
//! assert_eq!(clamped.value(), Some(&date(2026, 2, 28)));
//! ```

use std::cmp::Ordering;

use serde_json::{json, Value};

use crate::model::{
    done, fail, instant_json, kind, posix_seconds_json, range_err, read_amount, read_civil, read_instant, structural, Amount, Civil, Form, Zone, R,
};
use crate::num::{civil_from_days, days_from_civil, days_in_month, is_leap, Q};
use crate::{Diagnostic, Severity, Status, UtvResult};

fn checked<T>(v: Option<T>) -> R<T> {
    v.map_or_else(|| range_err(""), Ok)
}

// ---------- fields ----------

/// Derived fields of a proleptic Gregorian `date` or `datetime`:
/// `{"dayOfWeek" (Monday = 1), "dayOfYear", "isoWeekYear" (string), "isoWeek", "daysInMonth", "isLeapYear"}`.
pub fn fields(value: &Value) -> UtvResult {
    done("fields", (|| {
        let c = read_civil(value)?;
        if !c.form.has_date() {
            return fail(Status::Unsupported, "form", "/value/form", "fields applies to `date` and `datetime` values");
        }
        let days = c.day_number()?;
        let dow = (days + 3).rem_euclid(7) + 1;
        let jan1 = checked(days_from_civil(c.year, 1, 1))?;
        let thursday = days - (dow - 1) + 3;
        let (iso_year, _, _) = checked(civil_from_days(thursday))?;
        let iso_week = (thursday - checked(days_from_civil(iso_year, 1, 1))?) / 7 + 1;
        Ok(UtvResult::unique(json!({
            "dayOfWeek": dow as i64,
            "dayOfYear": (days - jan1 + 1) as i64,
            "isoWeekYear": iso_year.to_string(),
            "isoWeek": iso_week as i64,
            "daysInMonth": days_in_month(c.year, c.month),
            "isLeapYear": is_leap(c.year),
        })))
    })())
}

// ---------- add ----------

/// Add an amount to a value.
///
/// - Civil values take calendar amounts (`period`). Years and months are added first; if the
///   day doesn't exist in the resulting month, `policy` `{"monthEnd": "reject" | "constrain"}`
///   decides (default `reject`: status `none`, diagnostic `month-end`; `constrain` clamps to the
///   month's last day). Then weeks and days, then hours, minutes and seconds as wall-clock time.
///   A zone or offset is carried over unchanged: the result is still a civil intention. An
///   `offsetAssertion` is dropped, with an info diagnostic `offset-assertion-dropped`, because it
///   described the original wall time.
/// - Instants take `posix_seconds` amounts, exactly. An SI `elapsed` amount is `unsupported`
///   (it needs a leap-second table); a calendar amount is `invalid`.
/// - `time` values are `unsupported` (wrapping past midnight loses information).
pub fn add(value: &Value, amount: &Value, policy: Option<&Value>) -> UtvResult {
    done("add", (|| {
        let amt = read_amount(amount).map_err(|mut e| {
            for d in &mut e.diagnostics {
                d.path = format!("/amount{}", d.path);
            }
            e
        })?;
        match kind(value) {
            "instant" => {
                let t = read_instant(value)?;
                match amt {
                    Amount::PosixSeconds(s) => Ok(UtvResult::unique(instant_json(checked(t.add(s))?))),
                    Amount::Elapsed => fail(Status::Unsupported, "leap-seconds", "/amount", "adding SI (elapsed) seconds to a POSIX instant needs a leap-second table"),
                    Amount::Calendar(_) => fail(Status::Invalid, "add.calendar-on-instant", "/amount", "an instant has no calendar; resolve a civil value or use posix_seconds"),
                    Amount::Other(b) => fail(Status::Unsupported, "add.basis", "/amount", format!("amounts of basis `{b}` are not implemented")),
                }
            }
            _ => {
                let c = read_civil(value)?;
                let Amount::Calendar(p) = amt else {
                    return fail(Status::Unsupported, "add.basis", "/amount", "civil values take calendar amounts in this profile");
                };
                let constrain = match policy.map(|p| p.get("monthEnd").and_then(Value::as_str)) {
                    None | Some(None) | Some(Some("reject")) => false,
                    Some(Some("constrain")) => true,
                    Some(Some(other)) => return fail(Status::Unsupported, "policy", "/policy/monthEnd", format!("monthEnd policy `{other}` is not implemented")),
                };
                let has_time = p.hours != 0 || p.minutes != 0 || p.seconds != 0;
                match c.form {
                    Form::Time => return fail(Status::Unsupported, "add.time", "/value/form", "adding to a time of day wraps past midnight and loses information"),
                    Form::Date if has_time => return fail(Status::Invalid, "add.component", "/amount", "a date takes years, months, weeks and days only"),
                    Form::YearMonth if has_time || p.weeks != 0 || p.days != 0 => {
                        return fail(Status::Invalid, "add.component", "/amount", "a year-month takes years and months only")
                    }
                    _ => {}
                }
                let mut r = c.clone();
                // 1. years and months together.
                let months = checked(p.years.checked_mul(12).and_then(|y| y.checked_add(p.months)))?;
                let total = checked(c.year.checked_mul(12).and_then(|y| y.checked_add(c.month as i128 - 1 + months)))?;
                (r.year, r.month) = (total.div_euclid(12), (total.rem_euclid(12) + 1) as u32);
                if r.form == Form::YearMonth {
                    return Ok(UtvResult::unique(replace_fields(value, &r)));
                }
                // 2. month end.
                let dim = days_in_month(r.year, r.month);
                if r.day > dim {
                    if !constrain {
                        return fail(Status::None, "month-end", "/value/fields/day", format!("{}-{:02} has no day {}; pass {{\"monthEnd\":\"constrain\"}} to clamp", r.year, r.month, r.day));
                    }
                    r.day = dim;
                }
                // 3. weeks and days, 4. wall-clock time.
                let mut days = checked(r.day_number()?.checked_add(checked(p.weeks.checked_mul(7).and_then(|w| w.checked_add(p.days)))?))?;
                if r.form == Form::DateTime {
                    let secs = checked(
                        p.hours
                            .checked_mul(3600)
                            .and_then(|h| h.checked_add(p.minutes.checked_mul(60)?))
                            .and_then(|s| s.checked_add(p.seconds))
                            .and_then(|s| s.checked_add(c.hour as i128 * 3600 + c.minute as i128 * 60 + c.second.unwrap_or(0) as i128)),
                    )?;
                    days = checked(days.checked_add(secs.div_euclid(86_400)))?;
                    let sod = secs.rem_euclid(86_400) as u32;
                    (r.hour, r.minute) = (sod / 3600, sod / 60 % 60);
                    if c.second.is_some() || !sod.is_multiple_of(60) || p.seconds != 0 {
                        r.second = Some(sod % 60);
                    }
                }
                (r.year, r.month, r.day) = checked(civil_from_days(days))?;
                let mut out = UtvResult::unique(replace_fields(value, &r));
                if c.offset_assertion.is_some() {
                    let mut d = note("offset-assertion-dropped", "the asserted offset belonged to the old wall time; resolve the result to learn the new one");
                    d.path = "/value/offsetAssertion".into();
                    out.diagnostics.push(d);
                }
                Ok(out)
            }
        }
    })())
}

/// The input value with new fields; every other property (zone, intent, policy, provenance)
/// is kept, except `offsetAssertion`, which described the old wall time.
fn replace_fields(original: &Value, c: &Civil) -> Value {
    let mut v = original.clone();
    if let Some(inner) = v.get_mut("value").and_then(Value::as_object_mut) {
        inner.insert("fields".into(), c.fields_json());
        inner.remove("offsetAssertion");
    }
    v
}

// ---------- compare ----------

fn ordering(o: Ordering) -> Value {
    Value::String(match o {
        Ordering::Less => "before",
        Ordering::Equal => "equal",
        Ordering::Greater => "after",
    }
    .into())
}

/// Order `a` relative to `b`: `"before"`, `"equal"` or `"after"`.
///
/// Unzoned civil values of the same form compare by their fields; instants numerically;
/// fixed-offset date-times as the instants they denote. Anything with a named zone is
/// `unsupported` (ordering depends on zone rules: `resolve` first), as are different kinds or forms.
pub fn compare(a: &Value, b: &Value) -> UtvResult {
    done("compare", (|| match (kind(a), kind(b)) {
        ("instant", "instant") => {
            let (x, y) = (read_instant(a)?, read_instant(b)?);
            Ok(UtvResult::unique(ordering(checked(x.cmp(y))?)))
        }
        ("civil", "civil") => {
            let (x, y) = (read_civil(a)?, read_civil(b)?);
            if [&x.zone, &y.zone].iter().any(|z| matches!(z, Zone::Iana(_))) {
                return fail(Status::Unsupported, "compare.zoned", "", "ordering of zoned values depends on zone rules: resolve them to instants first");
            }
            if x.form != y.form {
                return fail(Status::Unsupported, "compare.forms", "", format!("can't order a `{}` against a `{}`", x.form.as_str(), y.form.as_str()));
            }
            match (&x.zone, &y.zone) {
                (Zone::Floating, Zone::Floating) => Ok(UtvResult::unique(ordering(x.key().cmp(&y.key())))),
                (Zone::Fixed(ox), Zone::Fixed(oy)) if x.form == Form::DateTime => {
                    let tx = checked(x.local_seconds()?.sub(Q::int(*ox as i128)))?;
                    let ty = checked(y.local_seconds()?.sub(Q::int(*oy as i128)))?;
                    Ok(UtvResult::unique(ordering(checked(tx.cmp(ty))?)))
                }
                _ => fail(Status::Unsupported, "compare.zones", "", "these values have different or unsupported zone kinds"),
            }
        }
        _ => {
            structural(a)?;
            structural(b)?;
            fail(Status::Unsupported, "compare.kinds", "", format!("can't order `{}` against `{}`", kind(a), kind(b)))
        }
    })())
}

// ---------- differences ----------

fn read_date(v: &Value) -> R<(i128, Zone)> {
    let c = read_civil(v)?;
    if c.form != Form::Date {
        return fail(Status::Unsupported, "form", "/value/form", "expected a `date` value");
    }
    Ok((c.day_number()?, c.zone))
}

/// Dates in one operation must be all unzoned or all in the same zone; a date in Paris and a
/// date in New York don't share day boundaries.
fn same_zone<'a>(zones: impl IntoIterator<Item = &'a Zone>) -> R<()> {
    let mut zones = zones.into_iter();
    let first = zones.next();
    if zones.any(|z| Some(z) != first) {
        return fail(Status::Unsupported, "zone.mixed", "", "dates in different zones (or zoned and unzoned) can't be related without zone rules");
    }
    Ok(())
}

/// The number of days from date `a` to date `b` (b − a), as a JSON integer. Both dates must be
/// unzoned or in the same zone; otherwise `unsupported`.
pub fn days_between(a: &Value, b: &Value) -> UtvResult {
    done("daysBetween", (|| {
        let ((da, za), (db, zb)) = (read_date(a)?, read_date(b)?);
        same_zone([&za, &zb])?;
        let n = checked(db.checked_sub(da))?;
        match i64::try_from(n) {
            Ok(n) => Ok(UtvResult::unique(n.into())),
            Err(_) => range_err(""),
        }
    })())
}

/// The POSIX seconds from instant `a` to instant `b` (b − a), as a `posix_seconds` amount
/// with an exact, reduced rational.
pub fn posix_seconds_between(a: &Value, b: &Value) -> UtvResult {
    done("posixSecondsBetween", (|| {
        let d = checked(read_instant(b)?.sub(read_instant(a)?))?;
        Ok(UtvResult::unique(posix_seconds_json(d)))
    })())
}

// ---------- intervals ----------

#[derive(Debug, Clone, Copy, PartialEq, Eq)]
enum Domain {
    Dates,
    Instants,
}

#[derive(Debug, Clone, Copy)]
enum Bound {
    Point { at: Q, included: bool },
    Unbounded,
    Unknown,
}

struct Interval {
    domain: Domain,
    /// Zones of the date points (empty for instants).
    zones: Vec<Zone>,
    start: Bound,
    end: Bound,
    empty_flag: bool,
}

/// Three-valued truth.
#[derive(Debug, Clone, Copy, PartialEq, Eq)]
enum T3 {
    True,
    False,
    Unknown,
}

fn all(conds: &[T3]) -> T3 {
    if conds.contains(&T3::False) {
        T3::False
    } else if conds.contains(&T3::Unknown) {
        T3::Unknown
    } else {
        T3::True
    }
}

fn answer(t: T3) -> UtvResult {
    UtvResult::unique(Value::String(
        match t {
            T3::True => "definitely",
            T3::False => "impossible",
            T3::Unknown => "possibly",
        }
        .into(),
    ))
}

fn read_point(v: &Value, domain: Domain, zones: &mut Vec<Zone>) -> R<Q> {
    match (domain, kind(v)) {
        (Domain::Dates, "civil") => read_date(v).map(|(d, z)| {
            zones.push(z);
            Q::int(d)
        }),
        (Domain::Instants, "instant") => read_instant(v),
        _ => fail(Status::Unsupported, "interval.domain", "", "points must be dates in a civil/gregory interval and instants in a physical one"),
    }
}

fn read_interval(v: &Value) -> R<Interval> {
    structural(v)?;
    if kind(v) != "interval" {
        return fail(Status::Unsupported, "kind", "/kind", format!("expected an `interval`, got `{}`", kind(v)));
    }
    let val = &v["value"];
    let domain = match val["domain"].as_str() {
        Some("civil/gregory") => Domain::Dates,
        Some("physical") => Domain::Instants,
        _ => return fail(Status::Unsupported, "interval.domain", "/value/domain", "only civil/gregory and physical intervals are implemented"),
    };
    if val.get("construction").is_some_and(|c| c != "start-end") {
        return fail(Status::Unsupported, "interval.construction", "/value/construction", "only start-end intervals are implemented");
    }
    let mut zones = Vec::new();
    let mut bound = |b: &Value| -> R<Bound> {
        Ok(match b["state"].as_str().unwrap_or("") {
            "unbounded" => Bound::Unbounded,
            "unknown" => Bound::Unknown,
            s => Bound::Point { at: read_point(&b["point"], domain, &mut zones)?, included: s == "included" },
        })
    };
    let (start, end) = (bound(&val["start"])?, bound(&val["end"])?);
    Ok(Interval { domain, zones, start, end, empty_flag: val["empty"] == true })
}

impl Interval {
    /// Dates are discrete: an excluded start d is an included d + 1, an included end d an
    /// excluded d + 1. Afterwards starts are included and ends excluded.
    fn half_open(mut self) -> R<Interval> {
        if self.domain == Domain::Dates {
            if let Bound::Point { at, included: false } = self.start {
                self.start = Bound::Point { at: checked(at.add(Q::int(1)))?, included: true };
            }
            if let Bound::Point { at, included: true } = self.end {
                self.end = Bound::Point { at: checked(at.add(Q::int(1)))?, included: false };
            }
        }
        Ok(self)
    }

    fn provably_empty(&self) -> R<bool> {
        if self.empty_flag {
            return Ok(true);
        }
        Ok(match (self.start, self.end) {
            (Bound::Point { at: s, included: si }, Bound::Point { at: e, included: ei }) => match checked(s.cmp(e))? {
                Ordering::Greater => true,
                Ordering::Equal => !(si && ei),
                Ordering::Less => false,
            },
            _ => false,
        })
    }
}

/// `lo` before `hi`: ≤ when both are included, < otherwise. Unbounded is true, unknown unknown.
fn before(lo: Bound, hi: Bound) -> R<T3> {
    Ok(match (lo, hi) {
        (Bound::Unbounded, _) | (_, Bound::Unbounded) => T3::True,
        (Bound::Unknown, _) | (_, Bound::Unknown) => T3::Unknown,
        (Bound::Point { at: a, included: ai }, Bound::Point { at: b, included: bi }) => {
            let o = checked(a.cmp(b))?;
            if o == Ordering::Less || (o == Ordering::Equal && ai && bi) {
                T3::True
            } else {
                T3::False
            }
        }
    })
}

/// Whether `interval` contains `point`: `"definitely"`, `"possibly"` (an unknown endpoint
/// decides) or `"impossible"`. Civil intervals take dates; physical intervals take instants.
/// All dates involved must be unzoned or in the same zone; otherwise `unsupported`.
pub fn contains(interval: &Value, point: &Value) -> UtvResult {
    done("contains", (|| {
        let mut i = read_interval(interval)?;
        let p = Bound::Point { at: read_point(point, i.domain, &mut i.zones)?, included: true };
        same_zone(&i.zones)?;
        if i.empty_flag {
            return Ok(answer(T3::False));
        }
        Ok(answer(all(&[before(i.start, p)?, before(p, i.end)?])))
    })())
}

/// Whether two intervals share at least one point: `"definitely"`, `"possibly"` or
/// `"impossible"`. Date intervals are normalized to half-open first; a provably empty interval
/// overlaps nothing. All dates involved must be unzoned or in the same zone.
pub fn overlaps(a: &Value, b: &Value) -> UtvResult {
    done("overlaps", (|| {
        let (a, b) = (read_interval(a)?, read_interval(b)?);
        if a.domain != b.domain {
            return fail(Status::Unsupported, "interval.domain", "", "can't relate intervals of different domains");
        }
        same_zone(a.zones.iter().chain(&b.zones))?;
        let (a, b) = (a.half_open()?, b.half_open()?);
        if a.provably_empty()? || b.provably_empty()? {
            return Ok(answer(T3::False));
        }
        Ok(answer(all(&[before(a.start, b.end)?, before(b.start, a.end)?])))
    })())
}

/// A diagnostic for successful results that still deserve a note.
pub(crate) fn note(code: &str, message: impl Into<String>) -> Diagnostic {
    Diagnostic { code: code.into(), severity: Severity::Info, path: String::new(), message: message.into() }
}
