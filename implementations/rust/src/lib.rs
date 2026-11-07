//! Universal Temporal Value (`utv/1.0`): dates, times, instants and periods whose operations
//! say when time has no answer, or more than one.
//!
//! ```
//! use utv::{CivilDate, Outcome};
//! let jan31 = CivilDate::of(2026, 1, 31)?;
//! match jan31.add_months(1) {
//!     Outcome::Unique(date, _) => println!("{date}"),
//!     Outcome::None(why) => println!("no such date: {why}"), // 2026-02 has no day 31 (month-end)
//!     other => println!("{other}"),
//! }
//! assert_eq!(jan31.add_months_constrained(1).to_string(), "2026-02-28"); // because you asked
//! # Ok::<(), utv::Diagnostics>(())
//! ```
//!
//! Two layers, the same answers:
//!
//! - **Typed values (0.3, `docs/library-api.md`).** [`CivilDate`], [`CivilYearMonth`],
//!   [`CivilTime`], [`CivilDateTime`], [`FixedOffsetDateTime`], [`ZonedIntent`], [`Instant`],
//!   [`Period`], [`PosixSeconds`], [`ElapsedSeconds`] and [`Interval`]. Constructors validate
//!   (`CivilDate::of(2026, 2, 30)` is an `Err`); operations that can't fail return plain
//!   values; the ones that can return an [`Outcome`] to `match`. Zone rules live in a
//!   [`TemporalContext`]. `Display` is the text form (`docs/text-form.md`), [`parse`] reads it.
//! - **The JSON layer (0.1, 0.2).** UTV JSON values as [`serde_json::Value`], with every
//!   operation returning a [`UtvResult`]: validation ([`validate_structural`],
//!   [`validate_gregorian_civil`]), builders in [`build`], ISO 8601 in [`iso`], the text form in
//!   [`text`], arithmetic in [`ops`], zone rules in [`zones`], native adapters in [`interop`].
//!   The typed layer calls it underneath, and the shared fixtures test it.
//!
//! Diagnostic codes are the shared ones of `docs/library-api.md` §5. This library never reads the
//! system clock, time zone or locale.

pub mod build;
pub mod interop;
pub mod iso;
pub mod ops;
pub mod text;
pub mod zones;

mod model;
mod num;
mod typed;

pub use build::{
    date, date_time, date_time_with_nanos, date_time_with_seconds, elapsed_seconds, in_zone, instant, instant_with_nanos, interval, interval_with, period,
    posix_seconds, time, time_with_nanos, time_with_seconds, with_offset, year_month, Endpoint,
};
pub use iso::{format_iso, parse_iso};
pub use text::{format_text, parse_text};
pub use ops::{add, compare, contains, days_between, fields, overlaps, posix_seconds_between};
pub use zones::{resolve, to_offset, to_zone, Rules};

// The typed layer (0.3).
pub use typed::amount::{ElapsedSeconds, Period, PosixSeconds};
pub use typed::civil::{CivilDate, CivilDateTime, CivilTime, CivilYearMonth, FixedOffsetDateTime, Weekday, ZonedIntent};
pub use typed::context::{Gap, MonthEnd, Overlap, ResolutionPolicy, TemporalContext, ZoneRules};
pub use typed::instant::Instant;
pub use typed::interval::{Bound, Interval, IntervalPoint};
pub use typed::value::{parse, Value};
pub use typed::{Diagnostics, Outcome, OutcomeError, ToJson, Truth};

use serde::de::{self, Deserializer, MapAccess, SeqAccess, Visitor};
use serde_json::{Map, Value as Json};
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

#[derive(Debug, Clone, Copy, PartialEq, Eq, Hash)]
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

impl fmt::Display for Status {
    fn fmt(&self, f: &mut fmt::Formatter<'_>) -> fmt::Result {
        f.pad(self.as_str())
    }
}

#[derive(Debug, Clone, Copy, PartialEq, Eq, Hash)]
pub enum Severity { Error, Warning, Info }

impl Severity {
    pub fn as_str(&self) -> &'static str {
        match self { Severity::Error => "error", Severity::Warning => "warning", Severity::Info => "info" }
    }
}

#[derive(Debug, Clone, PartialEq, Eq, Hash)]
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
    pub candidates: Vec<Json>,
}

impl UtvResult {
    pub(crate) fn ok() -> Self { UtvResult::new(Status::Valid, vec![]) }
    pub(crate) fn fail(status: Status, code: &str, path: &str, message: impl Into<String>) -> Self {
        let mut r = UtvResult::new(status, vec![]);
        r.diagnostics.push(Diagnostic { code: code.into(), severity: Severity::Error, path: path.into(), message: message.into() });
        r
    }
    /// A result with this status and these candidates (operation `validate` until set).
    pub fn new(status: Status, candidates: Vec<Json>) -> Self {
        UtvResult { operation: "validate".into(), status, diagnostics: vec![], candidates }
    }
    /// A `unique` result with one candidate.
    pub fn unique(candidate: Json) -> Self {
        UtvResult::new(Status::Unique, vec![candidate])
    }
    pub(crate) fn op(mut self, operation: &str) -> Self {
        self.operation = operation.into();
        self
    }
    /// The single candidate of a `unique` result; `None` for every other status.
    pub fn value(&self) -> Option<&Json> {
        match (self.status, self.candidates.as_slice()) {
            (Status::Unique, [v]) => Some(v),
            _ => None,
        }
    }
    /// The single candidate of a `unique` result as a string (for `format_iso`, `compare`, ...).
    pub fn as_str(&self) -> Option<&str> {
        self.value().and_then(Json::as_str)
    }
    /// Serialize as a `utv-result/1.0` JSON object.
    pub fn to_json(&self) -> Json {
        let diags: Vec<Json> = self.diagnostics.iter().map(|d| serde_json::json!({
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
            Json::String(s) => s.clone(),
            Json::Object(_) => iso::format_iso(c).as_str().map(str::to_owned).unwrap_or_else(|| c.to_string()),
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

struct Strict(Json);

impl<'de> serde::Deserialize<'de> for Strict {
    fn deserialize<D: Deserializer<'de>>(d: D) -> Result<Self, D::Error> {
        d.deserialize_any(StrictVisitor).map(Strict)
    }
}

struct StrictVisitor;

impl<'de> Visitor<'de> for StrictVisitor {
    type Value = Json;
    fn expecting(&self, f: &mut fmt::Formatter) -> fmt::Result { f.write_str("JSON value") }
    fn visit_bool<E>(self, v: bool) -> Result<Json, E> { Ok(Json::Bool(v)) }
    fn visit_i64<E>(self, v: i64) -> Result<Json, E> { Ok(Json::from(v)) }
    fn visit_u64<E>(self, v: u64) -> Result<Json, E> { Ok(Json::from(v)) }
    fn visit_f64<E>(self, v: f64) -> Result<Json, E> { Ok(Json::from(v)) }
    fn visit_str<E>(self, v: &str) -> Result<Json, E> { Ok(Json::String(v.to_owned())) }
    fn visit_string<E>(self, v: String) -> Result<Json, E> { Ok(Json::String(v)) }
    fn visit_unit<E>(self) -> Result<Json, E> { Ok(Json::Null) }
    fn visit_seq<A: SeqAccess<'de>>(self, mut a: A) -> Result<Json, A::Error> {
        let mut v = Vec::new();
        while let Some(Strict(x)) = a.next_element()? { v.push(x); }
        Ok(Json::Array(v))
    }
    fn visit_map<A: MapAccess<'de>>(self, mut a: A) -> Result<Json, A::Error> {
        let mut m = Map::new();
        while let Some(k) = a.next_key::<String>()? {
            if m.contains_key(&k) {
                return Err(de::Error::custom(format!("duplicate key `{k}`")));
            }
            let Strict(v) = a.next_value()?;
            m.insert(k, v);
        }
        Ok(Json::Object(m))
    }
}

/// Parse JSON text, rejecting duplicate object keys.
pub fn parse_strict(text: &str) -> Result<Json, String> {
    deserialize_strict(text).map_err(|e| e.to_string())
}

fn deserialize_strict(text: &str) -> Result<Json, serde_json::Error> {
    let mut de = serde_json::Deserializer::from_str(text);
    let Strict(v) = serde::Deserialize::deserialize(&mut de)?;
    de.end()?;
    Ok(v)
}

/// Parse UTV JSON text for validation. A repeated object key is `duplicate-key`; text that isn't JSON is `syntax`.
fn parse_for_validation(text: &str) -> Result<Json, UtvResult> {
    deserialize_strict(text).map_err(|e| {
        // StrictVisitor accepts every JSON value, so the only data error it raises is a duplicate key.
        let code = if e.classify() == serde_json::error::Category::Data { "duplicate-key" } else { "syntax" };
        UtvResult::fail(Status::Invalid, code, "", e.to_string())
    })
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
pub fn check_envelope(v: &Json, path: &str) -> UtvResult {
    let Some(obj) = v.as_object() else {
        return UtvResult::fail(Status::Invalid, "envelope.not-object", path, "UTV value must be a JSON object");
    };
    match obj.get("schema").and_then(Json::as_str) {
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
    match obj.get("kind").and_then(Json::as_str) {
        Some(k) if CORE_KINDS.contains(&k) => {}
        _ => return UtvResult::fail(Status::Invalid, "envelope.kind", &format!("{path}/kind"), "kind must be one of the 26 core kinds"),
    }
    if !obj.contains_key("value") {
        return UtvResult::fail(Status::Invalid, "envelope.value", path, "missing `value`");
    }
    UtvResult::ok()
}

// ---------- structural schema check ----------

struct SchemaCx { root: Json }

impl SchemaCx {
    fn resolve<'a>(&'a self, r: &str) -> Option<&'a Json> {
        let p = r.strip_prefix('#')?;
        self.root.pointer(p)
    }

    fn validate(&self, s: &Json, v: &Json, path: &str, errs: &mut Vec<(String, String)>) {
        let Some(s) = s.as_object() else {
            if s == &Json::Bool(false) { errs.push((path.into(), "value not allowed".into())); }
            return;
        };
        if let Some(r) = s.get("$ref").and_then(Json::as_str) {
            match self.resolve(r) {
                Some(t) => self.validate(t, v, path, errs),
                None => errs.push((path.into(), format!("unresolvable $ref {r}"))),
            }
        }
        if let Some(t) = s.get("type") {
            let ok = match t {
                Json::String(t) => type_ok(t, v),
                Json::Array(ts) => ts.iter().filter_map(Json::as_str).any(|t| type_ok(t, v)),
                _ => true,
            };
            if !ok { errs.push((path.into(), format!("expected type {t}"))); return; }
        }
        if let Some(c) = s.get("const") { if c != v { errs.push((path.into(), format!("expected const {c}"))); } }
        if let Some(Json::Array(e)) = s.get("enum") { if !e.contains(v) { errs.push((path.into(), "value not in enum".into())); } }
        if let (Some(p), Some(sv)) = (s.get("pattern").and_then(Json::as_str), v.as_str()) {
            if let Some(false) = pattern_matches(p, sv) {
                errs.push((path.into(), format!("does not match pattern {p}")));
            }
        }
        if let (Some(m), Some(sv)) = (s.get("minLength").and_then(Json::as_u64), v.as_str()) {
            if (sv.chars().count() as u64) < m { errs.push((path.into(), "string too short".into())); }
        }
        if let Some(n) = v.as_f64() {
            if let Some(m) = s.get("minimum").and_then(Json::as_f64) { if n < m { errs.push((path.into(), "below minimum".into())); } }
            if let Some(m) = s.get("maximum").and_then(Json::as_f64) { if n > m { errs.push((path.into(), "above maximum".into())); } }
        }
        if let Some(a) = v.as_array() {
            if let Some(m) = s.get("minItems").and_then(Json::as_u64) { if (a.len() as u64) < m { errs.push((path.into(), "too few items".into())); } }
            if let Some(it) = s.get("items") {
                for (i, x) in a.iter().enumerate() { self.validate(it, x, &format!("{path}/{i}"), errs); }
            }
        }
        if let Some(o) = v.as_object() {
            if let Some(Json::Array(req)) = s.get("required") {
                for r in req.iter().filter_map(Json::as_str) {
                    if !o.contains_key(r) { errs.push((path.into(), format!("missing required property `{r}`"))); }
                }
            }
            let props = s.get("properties").and_then(Json::as_object);
            for (k, x) in o {
                let p = format!("{path}/{k}");
                match props.and_then(|pr| pr.get(k)) {
                    Some(ps) => self.validate(ps, x, &p, errs),
                    None => match s.get("additionalProperties") {
                        Some(Json::Bool(false)) => errs.push((p, format!("unknown property `{k}`"))),
                        Some(ap @ Json::Object(_)) => self.validate(ap, x, &p, errs),
                        _ => {}
                    },
                }
            }
        }
        if let Some(Json::Array(all)) = s.get("allOf") {
            for sub in all { self.validate(sub, v, path, errs); }
        }
        if let Some(Json::Array(any)) = s.get("anyOf") {
            if !any.iter().any(|sub| self.ok(sub, v, path)) { errs.push((path.into(), "matches none of anyOf".into())); }
        }
        if let Some(Json::Array(one)) = s.get("oneOf") {
            let n = one.iter().filter(|sub| self.ok(sub, v, path)).count();
            if n != 1 { errs.push((path.into(), format!("matches {n} oneOf branches, expected 1"))); }
        }
        if let Some(not) = s.get("not") {
            if self.ok(not, v, path) { errs.push((path.into(), "matches forbidden `not` schema".into())); }
        }
    }

    fn ok(&self, s: &Json, v: &Json, path: &str) -> bool {
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

fn type_ok(t: &str, v: &Json) -> bool {
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
/// A value of the wrong shape is `invalid` with the shared code `malformed-value`, followed by
/// the specific `envelope.*` or `structural.schema` diagnostics.
pub fn validate_value(v: &Json) -> UtvResult {
    let mut r = validate_shape(v);
    if r.status == Status::Invalid {
        let first = Diagnostic { code: "malformed-value".into(), severity: Severity::Error, path: String::new(), message: "not a well-formed UTV value".into() };
        r.diagnostics.insert(0, first);
    }
    r
}

fn validate_shape(v: &Json) -> UtvResult {
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
    match parse_for_validation(text) {
        Ok(v) => validate_value(&v),
        Err(failure) => failure,
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
    let v = match parse_for_validation(text) {
        Ok(v) => v,
        Err(failure) => return failure,
    };
    let structural = validate_value(&v);
    if structural.status != Status::Valid { return structural; }
    check_civil(&v)
}

fn check_civil(v: &Json) -> UtvResult {
    if v["kind"].as_str() != Some("civil") {
        return UtvResult::fail(Status::Unsupported, "unsupported-operand", "/kind", "only `civil` values are checked semantically");
    }
    let val = &v["value"];
    if val.pointer("/calendar/id").and_then(Json::as_str) != Some("gregory")
        || val.pointer("/calendar/chronology").and_then(Json::as_str) != Some("proleptic")
    {
        return UtvResult::fail(Status::Unsupported, "unsupported-calendar", "/value/calendar", "only the proleptic Gregorian calendar is implemented");
    }
    let f = &val["fields"];
    let Some(m) = f["month"].as_i64() else { return UtvResult::ok(); };
    let Some(y) = f["year"].as_str().and_then(|s| s.parse::<i128>().ok()) else {
        if f["year"].is_null() { return UtvResult::ok(); }
        return UtvResult::fail(Status::Unsupported, "implementation-limit", "/value/fields/year", "year outside the supported i128 range");
    };
    if !(1..=12).contains(&m) {
        return UtvResult::fail(Status::Invalid, "month-out-of-range", "/value/fields/month", format!("there is no month {m}"));
    }
    if let Some(d) = f["day"].as_i64() {
        let max = days_in_month(y, m as u32).unwrap_or(31) as i64;
        if d < 1 || d > max {
            return UtvResult::fail(Status::Invalid, "day-out-of-range", "/value/fields/day", format!("{} has no day {d}", model::year_month_text(y, m as u32)));
        }
    }
    UtvResult::ok()
}
