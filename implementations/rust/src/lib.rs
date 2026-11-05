//! Universal Temporal Value (`utv/1.0`): validation and the everyday API.
//!
//! - Validation (0.1): [`validate_structural`] and [`validate_gregorian_civil`].
//! - Everyday API (0.2, `docs/everyday-api.md`): builders in [`build`], ISO 8601 text in
//!   [`iso`], calendar and instant arithmetic in [`ops`], zone rules and resolution in
//!   [`zones`], and explicit adapters to Rust types in [`interop`].
//!
//! Values are UTV JSON values held as [`serde_json::Value`]. Every operation returns a
//! [`UtvResult`]: a status, diagnostics and candidates. Semantic outcomes (`none`,
//! `multiple`, `unsupported`, `invalid`, ...) are results, never panics or `Err`s.
//!
//! This library never reads the system clock, time zone or locale.

pub mod build;
pub mod interop;
pub mod iso;
pub mod ops;
pub mod zones;

mod model;
mod num;

pub use build::{
    date, date_time, date_time_with_nanos, date_time_with_seconds, in_zone, instant, instant_with_nanos, interval, interval_with, period, posix_seconds,
    time, time_with_nanos, time_with_seconds, with_offset, year_month, Endpoint, Period,
};
pub use iso::{format_iso, parse_iso};
pub use ops::{add, compare, contains, days_between, fields, overlaps, posix_seconds_between};
pub use zones::{resolve, to_offset, to_zone, Rules};

use serde::de::{self, Deserializer, MapAccess, SeqAccess, Visitor};
use serde_json::{Map, Value};
use std::fmt;

pub const SPEC_VERSION: &str = "utv/1.0";
pub const RESULT_SCHEMA: &str = "utv-result/1.0";
const SCHEMA_TEXT: &str = include_str!("../../../schema/utv-1.0.schema.json");

/// The 26 core kinds of UTV 1.0.
pub const CORE_KINDS: [&str; 26] = [
    "instant", "civil", "amount", "interval", "recurrence", "uncertain", "set", "clock",
    "observation", "logical-clock", "relative", "relation", "assertion", "event-history",
    "location-intent", "constraint", "calendar-rule", "temporal-reference", "ordinal-position",
    "event-relative", "selection", "sample", "series", "temporal-graph", "sequence", "pattern",
];

#[derive(Debug, Clone, Copy, PartialEq, Eq)]
pub enum Status {
    Invalid, Unsupported, Indeterminate, None, Unique, Multiple, Range, Symbolic,
    ResourceLimit, Error, Valid,
}

impl Status {
    pub fn as_str(&self) -> &'static str {
        match self {
            Status::Invalid => "invalid",
            Status::Unsupported => "unsupported",
            Status::Indeterminate => "indeterminate",
            Status::None => "none",
            Status::Unique => "unique",
            Status::Multiple => "multiple",
            Status::Range => "range",
            Status::Symbolic => "symbolic",
            Status::ResourceLimit => "resource-limit",
            Status::Error => "error",
            Status::Valid => "valid",
        }
    }
}

#[derive(Debug, Clone, Copy, PartialEq, Eq)]
pub enum Severity { Error, Warning, Info }

impl Severity {
    pub fn as_str(&self) -> &'static str {
        match self { Severity::Error => "error", Severity::Warning => "warning", Severity::Info => "info" }
    }
}

#[derive(Debug, Clone, PartialEq)]
pub struct Diagnostic {
    pub code: String,
    pub severity: Severity,
    pub path: String,
    pub message: String,
}

/// A `utv-result/1.0` result: a status, diagnostics and candidates.
///
/// `unique` has exactly one candidate and `multiple` has all of them in ascending order.
/// `none`, `invalid`, `unsupported`, `symbolic` and validation results have none.
/// Candidates are UTV values, or plain JSON for operations that answer with a string,
/// number or object (`compare`, `fields`, `days_between`, `contains`, `overlaps`, `format_iso`).
#[derive(Debug, Clone, PartialEq)]
pub struct UtvResult {
    /// `validate` for validation, `convert` for the native adapters, otherwise the everyday
    /// operation's contract name (`parseIso`, `add`, `resolve`, ...).
    pub operation: String,
    pub status: Status,
    pub diagnostics: Vec<Diagnostic>,
    pub candidates: Vec<Value>,
}

impl UtvResult {
    pub(crate) fn ok() -> Self { UtvResult::new(Status::Valid, vec![]) }
    pub(crate) fn fail(status: Status, code: &str, path: &str, message: impl Into<String>) -> Self {
        let mut r = UtvResult::new(status, vec![]);
        r.diagnostics.push(Diagnostic { code: code.into(), severity: Severity::Error, path: path.into(), message: message.into() });
        r
    }
    /// A result with this status and these candidates (operation `validate` until set).
    pub fn new(status: Status, candidates: Vec<Value>) -> Self {
        UtvResult { operation: "validate".into(), status, diagnostics: vec![], candidates }
    }
    /// A `unique` result with one candidate.
    pub fn unique(candidate: Value) -> Self {
        UtvResult::new(Status::Unique, vec![candidate])
    }
    pub(crate) fn op(mut self, operation: &str) -> Self {
        self.operation = operation.into();
        self
    }
    /// The single candidate of a `unique` result; `None` for every other status.
    pub fn value(&self) -> Option<&Value> {
        match (self.status, self.candidates.as_slice()) {
            (Status::Unique, [v]) => Some(v),
            _ => None,
        }
    }
    /// The single candidate of a `unique` result as a string (for `format_iso`, `compare`, ...).
    pub fn as_str(&self) -> Option<&str> {
        self.value().and_then(Value::as_str)
    }
    /// Serialize as a `utv-result/1.0` JSON object.
    pub fn to_json(&self) -> Value {
        let diags: Vec<Value> = self.diagnostics.iter().map(|d| serde_json::json!({
            "code": d.code, "severity": d.severity.as_str(), "path": d.path, "message": d.message
        })).collect();
        serde_json::json!({ "schema": RESULT_SCHEMA, "operation": self.operation, "status": self.status.as_str(), "diagnostics": diags, "candidates": self.candidates })
    }
}

impl fmt::Display for UtvResult {
    /// The status, then the candidate(s): ISO 8601 text where `format_iso` has one, JSON otherwise,
    /// then the first diagnostic for statuses without candidates.
    fn fmt(&self, f: &mut fmt::Formatter<'_>) -> fmt::Result {
        f.write_str(self.status.as_str())?;
        let shown: Vec<String> = self.candidates.iter().map(|c| match c {
            Value::String(s) => s.clone(),
            Value::Object(_) => iso::format_iso(c).as_str().map(str::to_owned).unwrap_or_else(|| c.to_string()),
            other => other.to_string(),
        }).collect();
        if !shown.is_empty() {
            write!(f, " {}", shown.join(", "))?;
        } else if let Some(d) = self.diagnostics.first() {
            write!(f, " ({}: {})", d.code, d.message)?;
        }
        Ok(())
    }
}

// ---------- strict JSON parsing (rejects duplicate keys) ----------

struct Strict(Value);

impl<'de> serde::Deserialize<'de> for Strict {
    fn deserialize<D: Deserializer<'de>>(d: D) -> Result<Self, D::Error> {
        d.deserialize_any(StrictVisitor).map(Strict)
    }
}

struct StrictVisitor;

impl<'de> Visitor<'de> for StrictVisitor {
    type Value = Value;
    fn expecting(&self, f: &mut fmt::Formatter) -> fmt::Result { f.write_str("JSON value") }
    fn visit_bool<E>(self, v: bool) -> Result<Value, E> { Ok(Value::Bool(v)) }
    fn visit_i64<E>(self, v: i64) -> Result<Value, E> { Ok(Value::from(v)) }
    fn visit_u64<E>(self, v: u64) -> Result<Value, E> { Ok(Value::from(v)) }
    fn visit_f64<E>(self, v: f64) -> Result<Value, E> { Ok(Value::from(v)) }
    fn visit_str<E>(self, v: &str) -> Result<Value, E> { Ok(Value::String(v.to_owned())) }
    fn visit_string<E>(self, v: String) -> Result<Value, E> { Ok(Value::String(v)) }
    fn visit_unit<E>(self) -> Result<Value, E> { Ok(Value::Null) }
    fn visit_seq<A: SeqAccess<'de>>(self, mut a: A) -> Result<Value, A::Error> {
        let mut v = Vec::new();
        while let Some(Strict(x)) = a.next_element()? { v.push(x); }
        Ok(Value::Array(v))
    }
    fn visit_map<A: MapAccess<'de>>(self, mut a: A) -> Result<Value, A::Error> {
        let mut m = Map::new();
        while let Some(k) = a.next_key::<String>()? {
            if m.contains_key(&k) {
                return Err(de::Error::custom(format!("duplicate key `{k}`")));
            }
            let Strict(v) = a.next_value()?;
            m.insert(k, v);
        }
        Ok(Value::Object(m))
    }
}

/// Parse JSON text, rejecting duplicate object keys.
pub fn parse_strict(text: &str) -> Result<Value, String> {
    let mut de = serde_json::Deserializer::from_str(text);
    let Strict(v) = serde::Deserialize::deserialize(&mut de).map_err(|e| e.to_string())?;
    de.end().map_err(|e| e.to_string())?;
    Ok(v)
}

// ---------- envelope ----------

/// A well-formed wire identifier `utv/MAJOR.MINOR` (no leading zeros) other than `utv/1.0`.
/// Per VERSIONING.md, such versions are `unsupported`, not `invalid`.
fn other_wellformed_version(schema: &str) -> bool {
    fn num(s: &str) -> bool {
        !s.is_empty() && s.bytes().all(|b| b.is_ascii_digit()) && (s == "0" || !s.starts_with('0'))
    }
    match schema.strip_prefix("utv/").and_then(|r| r.split_once('.')) {
        Some((major, minor)) => num(major) && num(minor) && schema != SPEC_VERSION,
        None => false,
    }
}

/// Check the `{schema, kind, value}` envelope of an already parsed value.
pub fn check_envelope(v: &Value, path: &str) -> UtvResult {
    let Some(obj) = v.as_object() else {
        return UtvResult::fail(Status::Invalid, "envelope.not-object", path, "UTV value must be a JSON object");
    };
    match obj.get("schema").and_then(Value::as_str) {
        Some(SPEC_VERSION) => {}
        Some(s) if other_wellformed_version(s) => {
            return UtvResult::fail(Status::Unsupported, "envelope.unsupported-version", &format!("{path}/schema"), format!("specification version `{s}` is not implemented"));
        }
        _ => return UtvResult::fail(Status::Invalid, "envelope.schema", &format!("{path}/schema"), "schema must be exactly `utv/1.0`"),
    }
    for k in obj.keys() {
        if !matches!(k.as_str(), "schema" | "kind" | "value" | "provenance" | "extensions") {
            return UtvResult::fail(Status::Invalid, "envelope.unknown-property", &format!("{path}/{k}"), format!("unknown top-level property `{k}`"));
        }
    }
    match obj.get("kind").and_then(Value::as_str) {
        Some(k) if CORE_KINDS.contains(&k) => {}
        _ => return UtvResult::fail(Status::Invalid, "envelope.kind", &format!("{path}/kind"), "kind must be one of the 26 core kinds"),
    }
    if !obj.contains_key("value") {
        return UtvResult::fail(Status::Invalid, "envelope.value", path, "missing `value`");
    }
    UtvResult::ok()
}

// ---------- structural schema check ----------

struct SchemaCx { root: Value }

impl SchemaCx {
    fn resolve<'a>(&'a self, r: &str) -> Option<&'a Value> {
        let p = r.strip_prefix('#')?;
        self.root.pointer(p)
    }

    fn validate(&self, s: &Value, v: &Value, path: &str, errs: &mut Vec<(String, String)>) {
        let Some(s) = s.as_object() else {
            if s == &Value::Bool(false) { errs.push((path.into(), "value not allowed".into())); }
            return;
        };
        if let Some(r) = s.get("$ref").and_then(Value::as_str) {
            match self.resolve(r) {
                Some(t) => self.validate(t, v, path, errs),
                None => errs.push((path.into(), format!("unresolvable $ref {r}"))),
            }
        }
        if let Some(t) = s.get("type") {
            let ok = match t {
                Value::String(t) => type_ok(t, v),
                Value::Array(ts) => ts.iter().filter_map(Value::as_str).any(|t| type_ok(t, v)),
                _ => true,
            };
            if !ok { errs.push((path.into(), format!("expected type {t}"))); return; }
        }
        if let Some(c) = s.get("const") { if c != v { errs.push((path.into(), format!("expected const {c}"))); } }
        if let Some(Value::Array(e)) = s.get("enum") { if !e.contains(v) { errs.push((path.into(), "value not in enum".into())); } }
        if let (Some(p), Some(sv)) = (s.get("pattern").and_then(Value::as_str), v.as_str()) {
            if let Some(false) = pattern_matches(p, sv) {
                errs.push((path.into(), format!("does not match pattern {p}")));
            }
        }
        if let (Some(m), Some(sv)) = (s.get("minLength").and_then(Value::as_u64), v.as_str()) {
            if (sv.chars().count() as u64) < m { errs.push((path.into(), "string too short".into())); }
        }
        if let Some(n) = v.as_f64() {
            if let Some(m) = s.get("minimum").and_then(Value::as_f64) { if n < m { errs.push((path.into(), "below minimum".into())); } }
            if let Some(m) = s.get("maximum").and_then(Value::as_f64) { if n > m { errs.push((path.into(), "above maximum".into())); } }
        }
        if let Some(a) = v.as_array() {
            if let Some(m) = s.get("minItems").and_then(Value::as_u64) { if (a.len() as u64) < m { errs.push((path.into(), "too few items".into())); } }
            if let Some(it) = s.get("items") {
                for (i, x) in a.iter().enumerate() { self.validate(it, x, &format!("{path}/{i}"), errs); }
            }
        }
        if let Some(o) = v.as_object() {
            if let Some(Value::Array(req)) = s.get("required") {
                for r in req.iter().filter_map(Value::as_str) {
                    if !o.contains_key(r) { errs.push((path.into(), format!("missing required property `{r}`"))); }
                }
            }
            let props = s.get("properties").and_then(Value::as_object);
            for (k, x) in o {
                let p = format!("{path}/{k}");
                match props.and_then(|pr| pr.get(k)) {
                    Some(ps) => self.validate(ps, x, &p, errs),
                    None => match s.get("additionalProperties") {
                        Some(Value::Bool(false)) => errs.push((p, format!("unknown property `{k}`"))),
                        Some(ap @ Value::Object(_)) => self.validate(ap, x, &p, errs),
                        _ => {}
                    },
                }
            }
        }
        if let Some(Value::Array(all)) = s.get("allOf") {
            for sub in all { self.validate(sub, v, path, errs); }
        }
        if let Some(Value::Array(any)) = s.get("anyOf") {
            if !any.iter().any(|sub| self.ok(sub, v, path)) { errs.push((path.into(), "matches none of anyOf".into())); }
        }
        if let Some(Value::Array(one)) = s.get("oneOf") {
            let n = one.iter().filter(|sub| self.ok(sub, v, path)).count();
            if n != 1 { errs.push((path.into(), format!("matches {n} oneOf branches, expected 1"))); }
        }
        if let Some(not) = s.get("not") {
            if self.ok(not, v, path) { errs.push((path.into(), "matches forbidden `not` schema".into())); }
        }
    }

    fn ok(&self, s: &Value, v: &Value, path: &str) -> bool {
        let mut e = Vec::new();
        self.validate(s, v, path, &mut e);
        e.is_empty()
    }
}

/// Schema patterns, compiled once each. `None` if the pattern doesn't compile.
fn pattern_matches(pattern: &str, text: &str) -> Option<bool> {
    use std::collections::HashMap;
    use std::sync::{Mutex, OnceLock};
    static CACHE: OnceLock<Mutex<HashMap<String, Option<regex::Regex>>>> = OnceLock::new();
    let mut cache = CACHE.get_or_init(Default::default).lock().unwrap_or_else(|e| e.into_inner());
    let re = cache.entry(pattern.to_owned()).or_insert_with(|| regex::Regex::new(pattern).ok());
    re.as_ref().map(|re| re.is_match(text))
}

fn type_ok(t: &str, v: &Value) -> bool {
    match t {
        "object" => v.is_object(),
        "array" => v.is_array(),
        "string" => v.is_string(),
        "integer" => v.is_i64() || v.is_u64() || v.as_f64().is_some_and(|f| f.fract() == 0.0),
        "number" => v.is_number(),
        "boolean" => v.is_boolean(),
        "null" => v.is_null(),
        _ => true,
    }
}

fn schema_cx() -> &'static SchemaCx {
    use std::sync::OnceLock;
    static CX: OnceLock<SchemaCx> = OnceLock::new();
    CX.get_or_init(|| SchemaCx { root: serde_json::from_str(SCHEMA_TEXT).expect("embedded schema is valid JSON") })
}

/// Validate an already parsed value: envelope, then the structural schema for its kind.
pub fn validate_value(v: &Value) -> UtvResult {
    let env = check_envelope(v, "");
    if env.status != Status::Valid { return env; }
    let kind = v["kind"].as_str().unwrap_or_default();
    let cx = schema_cx();
    let Some(def) = cx.root.pointer(&format!("/$defs/{kind}Value")) else {
        return UtvResult::fail(Status::Unsupported, "structural.no-schema", "/kind", format!("no structural schema for kind `{kind}`"));
    };
    let mut errs = Vec::new();
    cx.validate(def, v, "", &mut errs);
    if errs.is_empty() {
        return UtvResult::ok();
    }
    let mut r = UtvResult::new(Status::Invalid, vec![]);
    r.diagnostics = errs.into_iter().map(|(path, message)| Diagnostic { code: "structural.schema".into(), severity: Severity::Error, path, message }).collect();
    r
}

/// Parse UTV JSON text and validate it structurally.
pub fn validate_structural(text: &str) -> UtvResult {
    match parse_strict(text) {
        Ok(v) => validate_value(&v),
        Err(e) => UtvResult::fail(Status::Invalid, "json.parse", "", e),
    }
}

// ---------- Gregorian civil date check ----------

/// Proleptic Gregorian leap year.
pub fn is_leap(year: i128) -> bool { year.rem_euclid(4) == 0 && (year.rem_euclid(100) != 0 || year.rem_euclid(400) == 0) }

pub fn days_in_month(year: i128, month: u32) -> Option<u32> {
    Some(match month {
        1 | 3 | 5 | 7 | 8 | 10 | 12 => 31,
        4 | 6 | 9 | 11 => 30,
        2 => if is_leap(year) { 29 } else { 28 },
        _ => return None,
    })
}

/// Semantic check of a proleptic Gregorian `civil` value. Runs structural validation first and
/// returns its result unless it is `valid`. Kept separate from `validate_structural` because
/// structural validity and semantic validity are different questions (implementations/README.md).
/// Other kinds, calendars and chronologies return `unsupported`.
pub fn validate_gregorian_civil(text: &str) -> UtvResult {
    let v = match parse_strict(text) {
        Ok(v) => v,
        Err(e) => return UtvResult::fail(Status::Invalid, "json.parse", "", e),
    };
    let structural = validate_value(&v);
    if structural.status != Status::Valid { return structural; }
    check_civil(&v)
}

fn check_civil(v: &Value) -> UtvResult {
    if v["kind"].as_str() != Some("civil") {
        return UtvResult::fail(Status::Unsupported, "semantic.kind", "/kind", "only `civil` values are checked semantically");
    }
    let val = &v["value"];
    if val.pointer("/calendar/id").and_then(Value::as_str) != Some("gregory")
        || val.pointer("/calendar/chronology").and_then(Value::as_str) != Some("proleptic")
    {
        return UtvResult::fail(Status::Unsupported, "semantic.calendar", "/value/calendar", "only the proleptic Gregorian calendar is implemented");
    }
    let f = &val["fields"];
    let Some(m) = f["month"].as_i64() else { return UtvResult::ok(); };
    let Some(y) = f["year"].as_str().and_then(|s| s.parse::<i128>().ok()) else {
        if f["year"].is_null() { return UtvResult::ok(); }
        return UtvResult::fail(Status::Unsupported, "semantic.year-range", "/value/fields/year", "year outside the supported i128 range");
    };
    if !(1..=12).contains(&m) {
        return UtvResult::fail(Status::Invalid, "civil.month", "/value/fields/month", "month out of range 1..12");
    }
    if let Some(d) = f["day"].as_i64() {
        let max = days_in_month(y, m as u32).unwrap_or(31) as i64;
        if d < 1 || d > max {
            return UtvResult::fail(Status::Invalid, "civil.day", "/value/fields/day", format!("day {d} does not exist in {y}-{m:02}"));
        }
    }
    UtvResult::ok()
}
