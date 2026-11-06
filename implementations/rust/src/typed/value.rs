//! [`Value`]: any typed value, and [`parse`] for any text-form value.

use std::fmt;

use serde_json::Value as Json;

use super::amount::{ElapsedSeconds, Period, PosixSeconds};
use super::civil::{CivilDate, CivilDateTime, CivilTime, CivilYearMonth, FixedOffsetDateTime, ZonedIntent};
use super::instant::Instant;
use super::interval::Interval;
use super::outcome::{outcome, Outcome};
use super::{kind_mismatch, Typed};
use crate::model::kind;
use crate::UtvResult;

/// Any value of the typed layer: what [`parse`] and [`Value::from_json`] answer when you don't
/// know the kind in advance.
///
/// ```
/// use utv::Value;
/// match utv::parse("2026-11-05T14:30:00Z").single()? {
///     Value::Instant(at) => assert_eq!(at.unix_seconds(), 1_793_889_000),
///     other => panic!("not an instant: {other}"),
/// }
/// # Ok::<(), utv::OutcomeError>(())
/// ```
#[derive(Clone, PartialEq, Eq, Hash)]
pub enum Value {
    Date(CivilDate),
    YearMonth(CivilYearMonth),
    Time(CivilTime),
    DateTime(CivilDateTime),
    FixedOffset(FixedOffsetDateTime),
    Zoned(ZonedIntent),
    Instant(Instant),
    Period(Period),
    PosixSeconds(PosixSeconds),
    ElapsedSeconds(ElapsedSeconds),
    DateInterval(Interval<CivilDate>),
    InstantInterval(Interval<Instant>),
}

/// Parse any text-form value: a date, time, date-time, instant, period or interval.
/// Bad text is an [`Outcome`], never a panic.
pub fn parse(text: &str) -> Outcome<Value> {
    outcome(crate::text::parse_text(text), Value::read)
}

/// A valid UTV value outside the typed layer (a recurrence, a business-day amount, ...):
/// `unsupported` (`unsupported-operand`); anything else is `invalid` (`kind-mismatch`).
fn no_value<T>(json: &Json) -> Result<T, UtvResult> {
    if crate::validate_value(json).status == crate::Status::Valid {
        super::no_typed_type(format!("a `{}` value", kind(json)))
    } else {
        kind_mismatch("an everyday value", json)
    }
}

impl Value {
    /// Read any UTV JSON value the typed layer has a type for. Other kinds and forms are
    /// `invalid` (`kind-mismatch`).
    pub fn from_json(json: &Json) -> Outcome<Value> {
        super::from_json(json)
    }

    pub fn to_json(&self) -> Json {
        self.json()
    }
}

impl Typed for Value {
    fn read(json: &Json) -> Result<Self, UtvResult> {
        let v = &json["value"];
        Ok(match (kind(json), v["form"].as_str(), v["zone"]["type"].as_str()) {
            ("civil", Some("date"), _) => Value::Date(CivilDate::read(json)?),
            ("civil", Some("year-month"), _) => Value::YearMonth(CivilYearMonth::read(json)?),
            ("civil", Some("time"), _) => Value::Time(CivilTime::read(json)?),
            ("civil", Some("datetime"), Some("iana")) => Value::Zoned(ZonedIntent::read(json)?),
            ("civil", Some("datetime"), Some("fixed-offset")) => Value::FixedOffset(FixedOffsetDateTime::read(json)?),
            ("civil", Some("datetime"), _) => Value::DateTime(CivilDateTime::read(json)?),
            ("instant", _, _) => Value::Instant(Instant::read(json)?),
            ("amount", _, _) => match v["basis"].as_str() {
                Some("calendar") => Value::Period(Period::read(json)?),
                Some("clock-ticks") => Value::PosixSeconds(PosixSeconds::read(json)?),
                Some("elapsed") => Value::ElapsedSeconds(ElapsedSeconds::read(json)?),
                _ => return no_value(json),
            },
            ("interval", _, _) => match v["domain"].as_str() {
                Some("physical") => Value::InstantInterval(Interval::read(json)?),
                _ => Value::DateInterval(Interval::read(json)?),
            },
            _ => return no_value(json),
        })
    }

    fn json(&self) -> Json {
        match self {
            Value::Date(v) => v.json(),
            Value::YearMonth(v) => v.json(),
            Value::Time(v) => v.json(),
            Value::DateTime(v) => v.json(),
            Value::FixedOffset(v) => v.json(),
            Value::Zoned(v) => v.json(),
            Value::Instant(v) => v.json(),
            Value::Period(v) => v.json(),
            Value::PosixSeconds(v) => v.json(),
            Value::ElapsedSeconds(v) => v.json(),
            Value::DateInterval(v) => v.json(),
            Value::InstantInterval(v) => v.json(),
        }
    }
}

macro_rules! each {
    ($self:expr, $v:ident => $e:expr) => {
        match $self {
            Value::Date($v) => $e,
            Value::YearMonth($v) => $e,
            Value::Time($v) => $e,
            Value::DateTime($v) => $e,
            Value::FixedOffset($v) => $e,
            Value::Zoned($v) => $e,
            Value::Instant($v) => $e,
            Value::Period($v) => $e,
            Value::PosixSeconds($v) => $e,
            Value::ElapsedSeconds($v) => $e,
            Value::DateInterval($v) => $e,
            Value::InstantInterval($v) => $e,
        }
    };
}

impl fmt::Display for Value {
    fn fmt(&self, f: &mut fmt::Formatter<'_>) -> fmt::Result {
        each!(self, v => fmt::Display::fmt(v, f))
    }
}

impl fmt::Debug for Value {
    fn fmt(&self, f: &mut fmt::Formatter<'_>) -> fmt::Result {
        each!(self, v => fmt::Debug::fmt(v, f))
    }
}

impl crate::ToJson for Value {
    fn to_json(&self) -> Json {
        self.json()
    }
}

impl std::str::FromStr for Value {
    type Err = crate::OutcomeError;
    fn from_str(text: &str) -> Result<Self, Self::Err> {
        parse(text).single()
    }
}
