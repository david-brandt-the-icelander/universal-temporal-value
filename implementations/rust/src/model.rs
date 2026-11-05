//! Reading UTV JSON values into checked Rust records, and writing canonical shapes back.
//! Internal: the public API stays JSON in, `UtvResult` out.

use serde_json::{json, Map, Value};

use crate::num::{civil_from_days, days_from_civil, days_in_month, Q};
use crate::{validate_value, Status, UtvResult};

pub(crate) type R<T> = Result<T, UtvResult>;

pub(crate) fn fail<T>(status: Status, code: &str, path: &str, message: impl Into<String>) -> R<T> {
    Err(UtvResult::fail(status, code, path, message))
}

pub(crate) fn range_err<T>(path: &str) -> R<T> {
    fail(Status::Unsupported, "implementation-limit", path, "value outside the range this library computes with (i128)")
}

/// Unwrap a computation result (or the `UtvResult` for whatever went wrong), named after the operation.
pub(crate) fn done(operation: &str, r: R<UtvResult>) -> UtvResult {
    r.unwrap_or_else(|e| e).op(operation)
}

pub(crate) fn calendar() -> Value {
    json!({"id": "gregory", "chronology": "proleptic"})
}

pub(crate) fn posix() -> Value {
    json!({"system": "utv.timescale", "id": "unix-posix", "version": "1"})
}

pub(crate) fn kind(v: &Value) -> &str {
    v.get("kind").and_then(Value::as_str).unwrap_or("")
}

/// Structural validation, as an early return.
pub(crate) fn structural(v: &Value) -> R<()> {
    let r = validate_value(v);
    if r.status == Status::Valid {
        Ok(())
    } else {
        Err(r)
    }
}

#[derive(Debug, Clone, Copy, PartialEq, Eq)]
pub(crate) enum Form {
    Date,
    YearMonth,
    Time,
    DateTime,
}

impl Form {
    pub fn as_str(self) -> &'static str {
        match self {
            Form::Date => "date",
            Form::YearMonth => "year-month",
            Form::Time => "time",
            Form::DateTime => "datetime",
        }
    }
    fn parse(s: &str) -> Option<Form> {
        Some(match s {
            "date" => Form::Date,
            "year-month" => Form::YearMonth,
            "time" => Form::Time,
            "datetime" => Form::DateTime,
            _ => return None,
        })
    }
    fn fields(self) -> &'static [&'static str] {
        match self {
            Form::Date => &["year", "month", "day"],
            Form::YearMonth => &["year", "month"],
            Form::Time => &["hour", "minute", "second", "nano"],
            Form::DateTime => &["year", "month", "day", "hour", "minute", "second", "nano"],
        }
    }
    fn required(self) -> &'static [&'static str] {
        match self {
            Form::Date => &["year", "month", "day"],
            Form::YearMonth => &["year", "month"],
            Form::Time => &["hour", "minute"],
            Form::DateTime => &["year", "month", "day", "hour", "minute"],
        }
    }
    pub fn has_date(self) -> bool {
        matches!(self, Form::Date | Form::DateTime)
    }
}

#[derive(Debug, Clone, PartialEq, Eq)]
pub(crate) enum Zone {
    Floating,
    Iana(String),
    Fixed(i64),
    Other(String),
}

/// `2026-02`: a year and month as text, for messages.
pub(crate) fn year_month_text(year: i128, month: u32) -> String {
    format!("{}-{month:02}", crate::iso::fmt_year(year))
}

/// Offsets beyond ±18:00 are invalid (RFC 3339 / ISO 8601 profile bound).
pub(crate) const MAX_OFFSET: i64 = 18 * 3600;

/// A checked proleptic Gregorian civil value.
#[derive(Debug, Clone, PartialEq, Eq)]
pub(crate) struct Civil {
    pub form: Form,
    pub year: i128,
    pub month: u32,
    pub day: u32,
    pub hour: u32,
    pub minute: u32,
    pub second: Option<u32>,
    pub nano: Option<u32>,
    pub zone: Zone,
    pub offset_assertion: Option<i64>,
}

impl Civil {
    pub fn new(form: Form) -> Civil {
        Civil { form, year: 0, month: 1, day: 1, hour: 0, minute: 0, second: None, nano: None, zone: Zone::Floating, offset_assertion: None }
    }

    /// Days from 1970-01-01 (only meaningful for forms with a date).
    pub fn day_number(&self) -> R<i128> {
        days_from_civil(self.year, self.month, self.day).map_or_else(|| range_err("/value/fields/year"), Ok)
    }

    /// The wall time read as if it were UTC, in seconds, exactly (the contract's *L*).
    pub fn local_seconds(&self) -> R<Q> {
        let secs = self
            .day_number()?
            .checked_mul(86_400)
            .and_then(|s| s.checked_add(self.hour as i128 * 3600 + self.minute as i128 * 60 + self.second.unwrap_or(0) as i128))
            .and_then(|s| Q::from_secs_nanos(s, self.nano.unwrap_or(0)));
        secs.map_or_else(|| range_err("/value/fields/year"), Ok)
    }

    /// Set date and time fields from local seconds; `second` is always set, `nano` when non-zero.
    pub fn set_from_seconds(&mut self, q: Q) -> R<()> {
        let Some(nano) = q.frac_nanos() else {
            return fail(Status::Unsupported, "implementation-limit", "/value/subTick", "the fraction of a second is finer than a nanosecond, which civil fields can't hold");
        };
        let secs = q.floor();
        let (days, sod) = (secs.div_euclid(86_400), secs.rem_euclid(86_400) as u32);
        let Some((y, m, d)) = civil_from_days(days) else { return range_err("/value/ticks") };
        (self.year, self.month, self.day) = (y, m, d);
        (self.hour, self.minute, self.second) = (sod / 3600, sod / 60 % 60, Some(sod % 60));
        self.nano = (nano != 0).then_some(nano);
        Ok(())
    }

    /// Order of the fields, with absent `second` and `nano` as 0.
    pub fn key(&self) -> (i128, u32, u32, u32, u32, u32, u32) {
        (self.year, self.month, self.day, self.hour, self.minute, self.second.unwrap_or(0), self.nano.unwrap_or(0))
    }

    pub fn fields_json(&self) -> Value {
        let mut f = Map::new();
        if self.form != Form::Time {
            f.insert("year".into(), Value::String(self.year.to_string()));
            f.insert("month".into(), self.month.into());
        }
        if self.form.has_date() {
            f.insert("day".into(), self.day.into());
        }
        if matches!(self.form, Form::Time | Form::DateTime) {
            f.insert("hour".into(), self.hour.into());
            f.insert("minute".into(), self.minute.into());
            if let Some(s) = self.second {
                f.insert("second".into(), s.into());
            }
            if let Some(n) = self.nano.filter(|n| *n != 0) {
                f.insert("nano".into(), n.into());
            }
        }
        Value::Object(f)
    }

    /// The canonical value (builders' shape).
    pub fn to_json(&self) -> Value {
        let mut v = json!({"schema": crate::SPEC_VERSION, "kind": "civil", "value": {
            "form": self.form.as_str(), "fields": self.fields_json(), "calendar": calendar()
        }});
        let inner = v["value"].as_object_mut().expect("object");
        match &self.zone {
            Zone::Floating | Zone::Other(_) => {}
            Zone::Iana(id) => {
                inner.insert("zone".into(), json!({"type": "iana", "id": id}));
                inner.insert("zoneIntent".into(), "follow-zone-id".into());
            }
            Zone::Fixed(s) => {
                inner.insert("zone".into(), json!({"type": "fixed-offset", "seconds": s}));
                inner.insert("zoneIntent".into(), "fixed-offset".into());
            }
        }
        if let Some(a) = self.offset_assertion {
            inner.insert("offsetAssertion".into(), a.into());
        }
        v
    }
}

fn int_field(f: &Value, name: &str) -> R<Option<i64>> {
    match f.get(name) {
        None => Ok(None),
        Some(x) => match x.as_i64() {
            Some(n) => Ok(Some(n)),
            None => fail(Status::Invalid, "malformed-value", &format!("/value/fields/{name}"), format!("`{name}` must be an integer")),
        },
    }
}

/// Read and check a proleptic Gregorian civil value of form date, year-month, time or datetime.
/// Structurally invalid input and non-existent dates are `invalid`; other kinds, calendars and
/// forms are `unsupported`, as is second 60 (it needs a leap-second table).
pub(crate) fn read_civil(v: &Value) -> R<Civil> {
    structural(v)?;
    if kind(v) != "civil" {
        return fail(Status::Unsupported, "unsupported-operand", "/kind", format!("expected a `civil` value, got `{}`", kind(v)));
    }
    let val = &v["value"];
    if val["calendar"]["id"] != "gregory" || val["calendar"]["chronology"] != "proleptic" {
        return fail(Status::Unsupported, "unsupported-calendar", "/value/calendar", "only the proleptic Gregorian calendar is implemented");
    }
    let form_s = val["form"].as_str().unwrap_or("");
    let Some(form) = Form::parse(form_s) else {
        return fail(Status::Unsupported, "unsupported-operand", "/value/form", format!("form `{form_s}` is not part of the everyday API"));
    };
    let f = &val["fields"];
    let names = f.as_object().map(|o| o.keys().cloned().collect::<Vec<_>>()).unwrap_or_default();
    if let Some(extra) = names.iter().find(|k| !form.fields().contains(&k.as_str())) {
        return fail(Status::Invalid, "malformed-value", &format!("/value/fields/{extra}"), format!("field `{extra}` doesn't belong to form `{form_s}`"));
    }
    if let Some(missing) = form.required().iter().find(|k| f.get(**k).is_none()) {
        return fail(Status::Invalid, "malformed-value", "/value/fields", format!("form `{form_s}` needs field `{missing}`"));
    }
    let mut c = Civil::new(form);
    if let Some(y) = f.get("year").and_then(Value::as_str) {
        c.year = match y.parse::<i128>() {
            Ok(y) => y,
            Err(_) => return range_err("/value/fields/year"),
        };
    }
    let check = |name: &str, lo: i64, hi: i64| -> R<Option<u32>> {
        match int_field(f, name)? {
            Some(n) if n < lo || n > hi => fail(Status::Invalid, "field-out-of-range", &format!("/value/fields/{name}"), format!("{name} {n} is out of range {lo}..={hi}")),
            n => Ok(n.map(|n| n as u32)),
        }
    };
    match int_field(f, "month")? {
        Some(m) if !(1..=12).contains(&m) => return fail(Status::Invalid, "month-out-of-range", "/value/fields/month", format!("there is no month {m}")),
        Some(m) => c.month = m as u32,
        None => {}
    }
    match int_field(f, "day")? {
        Some(d) if d < 1 || d > days_in_month(c.year, c.month) as i64 => {
            return fail(Status::Invalid, "day-out-of-range", "/value/fields/day", format!("{} has no day {d}", year_month_text(c.year, c.month)))
        }
        Some(d) => c.day = d as u32,
        None => {}
    }
    if let Some(h) = check("hour", 0, 23)? {
        c.hour = h;
    }
    if let Some(m) = check("minute", 0, 59)? {
        c.minute = m;
    }
    c.nano = check("nano", 0, 999_999_999)?;
    c.second = check("second", 0, 60)?;
    if c.second == Some(60) {
        return fail(Status::Unsupported, "leap-second", "/value/fields/second", "second 60 needs a leap-second table");
    }
    c.zone = match val.get("zone") {
        None => Zone::Floating,
        Some(z) => match z["type"].as_str().unwrap_or("") {
            "floating" => Zone::Floating,
            "iana" => Zone::Iana(z["id"].as_str().unwrap_or("").to_owned()),
            "fixed-offset" => {
                let s = z["seconds"].as_i64().unwrap_or(i64::MAX);
                if s.abs() > MAX_OFFSET {
                    return fail(Status::Invalid, "offset-out-of-range", "/value/zone/seconds", "offset beyond ±18:00");
                }
                Zone::Fixed(s)
            }
            other => Zone::Other(other.to_owned()),
        },
    };
    c.offset_assertion = val.get("offsetAssertion").and_then(Value::as_i64);
    Ok(c)
}

/// Read a POSIX instant as exact seconds since 1970-01-01T00:00:00Z.
pub(crate) fn read_instant(v: &Value) -> R<Q> {
    structural(v)?;
    if kind(v) != "instant" {
        return fail(Status::Unsupported, "unsupported-operand", "/kind", format!("expected an `instant`, got `{}`", kind(v)));
    }
    let val = &v["value"];
    if val["system"] != posix() {
        return fail(Status::Unsupported, "unsupported-operand", "/value/system", "only the unix-posix timescale is implemented");
    }
    let Some(ticks) = val["ticks"].as_str() else {
        return fail(Status::Unsupported, "unsupported-form", "/value", "instants given by `coordinate` are not implemented");
    };
    let Ok(ticks) = ticks.parse::<i128>() else { return range_err("/value/ticks") };
    let total = match val.get("subTick") {
        None => Some(Q::int(ticks)),
        Some(st) => read_rational(st).and_then(|f| Q::int(ticks).add(f)),
    };
    total.map_or_else(|| range_err("/value/subTick"), Ok)
}

/// A structurally valid rational (`{"numerator","denominator"}` decimal strings).
pub(crate) fn read_rational(v: &Value) -> Option<Q> {
    let n = v["numerator"].as_str()?.parse::<i128>().ok()?;
    let d = v["denominator"].as_str()?.parse::<i128>().ok()?;
    Q::new(n, d)
}

pub(crate) fn rational_json(q: Q) -> Value {
    json!({"numerator": q.n.to_string(), "denominator": q.d.to_string()})
}

/// The canonical instant: integer `ticks` (floor) plus a reduced `subTick` only when non-zero.
pub(crate) fn instant_json(q: Q) -> Value {
    let mut value = json!({"system": posix(), "ticks": q.floor().to_string()});
    let frac = q.frac();
    if frac.n != 0 {
        value["subTick"] = rational_json(frac);
    }
    json!({"schema": crate::SPEC_VERSION, "kind": "instant", "value": value})
}

pub(crate) fn posix_seconds_json(q: Q) -> Value {
    json!({"schema": crate::SPEC_VERSION, "kind": "amount", "value": {
        "basis": "clock-ticks", "rules": posix(), "seconds": rational_json(q)
    }})
}

/// Calendar amount components (each may be negative).
#[derive(Debug, Clone, Copy, Default, PartialEq, Eq)]
pub(crate) struct Components {
    pub years: i128,
    pub months: i128,
    pub weeks: i128,
    pub days: i128,
    pub hours: i128,
    pub minutes: i128,
    pub seconds: i128,
}

pub(crate) enum Amount {
    Calendar(Components),
    PosixSeconds(Q),
    /// SI seconds, when given as an exact rational.
    Elapsed(Option<Q>),
    Other(String),
}

pub(crate) fn read_amount(v: &Value) -> R<Amount> {
    structural(v)?;
    if kind(v) != "amount" {
        return fail(Status::Invalid, "kind-mismatch", "/kind", format!("expected an `amount`, got `{}`", kind(v)));
    }
    let val = &v["value"];
    match val["basis"].as_str().unwrap_or("") {
        "calendar" => {
            let Some(comps) = val.get("components").and_then(Value::as_object) else {
                return fail(Status::Invalid, "malformed-value", "/value", "a calendar amount needs `components`");
            };
            let mut c = Components::default();
            for (k, x) in comps {
                let path = format!("/value/components/{k}");
                let slot = match k.as_str() {
                    "years" => &mut c.years,
                    "months" => &mut c.months,
                    "weeks" => &mut c.weeks,
                    "days" => &mut c.days,
                    "hours" => &mut c.hours,
                    "minutes" => &mut c.minutes,
                    "seconds" => &mut c.seconds,
                    _ => return fail(Status::Unsupported, "unsupported-operand", &path, format!("component `{k}` is not part of the everyday API")),
                };
                let s = x.as_str().unwrap_or("");
                *slot = match s.parse::<i128>() {
                    Ok(n) => n,
                    Err(_) if is_decimal(s) => return fail(Status::Unsupported, "unsupported-form", &path, "fractional or out-of-range components are not implemented"),
                    Err(_) => return fail(Status::Invalid, "malformed-value", &path, "components are decimal strings"),
                };
            }
            Ok(Amount::Calendar(c))
        }
        "clock-ticks" => {
            if val["rules"] != posix() {
                return fail(Status::Unsupported, "unsupported-operand", "/value/rules", "only POSIX clock-tick amounts are implemented");
            }
            match val.get("seconds").and_then(read_rational) {
                Some(q) => Ok(Amount::PosixSeconds(q)),
                None => fail(Status::Invalid, "malformed-value", "/value/seconds", "a clock-ticks amount needs `seconds`"),
            }
        }
        "elapsed" => Ok(Amount::Elapsed(val.get("seconds").and_then(read_rational))),
        other => Ok(Amount::Other(other.to_owned())),
    }
}

fn is_decimal(s: &str) -> bool {
    let s = s.strip_prefix('-').unwrap_or(s);
    let (i, f) = s.split_once('.').unwrap_or((s, ""));
    !i.is_empty() && i.bytes().all(|b| b.is_ascii_digit()) && f.bytes().all(|b| b.is_ascii_digit())
}
