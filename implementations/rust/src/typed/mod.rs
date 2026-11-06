//! The typed layer (library 0.3, `docs/library-api.md`): one immutable type per everyday value,
//! valid by construction, with total operations returning plain values and partial ones
//! returning an [`Outcome`].
//!
//! Every operation calls the 0.2 JSON layer underneath (`ops`, `zones`, `text`), so the typed
//! layer and the shared fixtures can't disagree.
//!
//! Total operations that would leave this library's `i128` range panic, the way integer
//! overflow does in Rust; constructors refuse such values with `implementation-limit` up front.

use std::fmt;

use serde_json::Value as Json;

use crate::model::{fail, kind};
use crate::{Status, UtvResult};

pub(crate) mod amount;
pub(crate) mod civil;
pub(crate) mod context;
pub(crate) mod instant;
pub(crate) mod interval;
pub(crate) mod outcome;
pub(crate) mod value;

pub use outcome::{Diagnostics, Outcome, OutcomeError, ToJson, Truth};

/// What every typed value can do internally: read itself from UTV JSON (via the JSON layer's
/// readers) and write its canonical JSON.
pub(crate) trait Typed: Sized {
    /// Read a value of exactly this type. Another kind or form is `invalid` (`kind-mismatch`);
    /// otherwise the JSON layer's own diagnostics apply.
    fn read(json: &Json) -> Result<Self, UtvResult>;
    fn json(&self) -> Json;
}

pub(crate) fn kind_mismatch<T>(expected: &str, json: &Json) -> Result<T, UtvResult> {
    let got = match (kind(json), json["value"]["form"].as_str()) {
        ("", _) => "something else".to_owned(),
        (k, Some(form)) => format!("a `{k}` value of form `{form}`"),
        (k, None) => format!("a `{k}` value"),
    };
    fail(Status::Invalid, "kind-mismatch", "", format!("expected {expected}, got {got}"))
}

/// A valid UTV value that no typed type can hold (a zoned date, extra properties such as a
/// `resolutionPolicy`): `unsupported` (`unsupported-operand`). The JSON layer still handles it.
pub(crate) fn no_typed_type<T>(why: impl Into<String>) -> Result<T, UtvResult> {
    fail(Status::Unsupported, "unsupported-operand", "", format!("no typed value can hold this: {}; use the JSON layer", why.into()))
}

/// `unsupported-operand` if the JSON object `v` has keys beyond `allowed`.
pub(crate) fn only_keys(v: &Json, allowed: &[&str]) -> Result<(), UtvResult> {
    match v.as_object().and_then(|o| o.keys().find(|k| !allowed.contains(&k.as_str()))) {
        Some(extra) => no_typed_type(format!("it has `{extra}`")),
        None => Ok(()),
    }
}

/// A constructor's refusal, as the `Err` of `of(...)`.
pub(crate) fn rejected(mut r: UtvResult) -> Diagnostics {
    if r.operation == "validate" || r.operation.is_empty() {
        r.operation = "construct".into();
    }
    Diagnostics::of(&r)
}

/// The text form of a value, or its JSON where the text form can't express it losslessly.
pub(crate) fn text_of(json: &Json) -> String {
    match crate::text::format_text(json).as_str() {
        Some(s) => s.to_owned(),
        None => json.to_string(),
    }
}

pub(crate) fn from_json<T: Typed>(json: &Json) -> Outcome<T> {
    match T::read(json) {
        Ok(v) => Outcome::Unique(v, Diagnostics::of(&UtvResult::new(Status::Unique, vec![]).op("convert"))),
        Err(e) => outcome::failure(e.op("convert")),
    }
}

pub(crate) fn parse_as<T: Typed>(text: &str) -> Outcome<T> {
    outcome::outcome(crate::text::parse_text(text), T::read)
}

pub(crate) fn debug(f: &mut fmt::Formatter<'_>, name: &str, text: &str) -> fmt::Result {
    write!(f, "{name}({text})")
}

/// `to_json`, `from_json`, `parse`, `Display` (the text form), `Debug` (`CivilDate(2026-11-05)`),
/// `FromStr` and [`ToJson`] for a typed value.
macro_rules! typed_value {
    ($t:ident) => {
        impl $t {
            /// The UTV JSON value.
            pub fn to_json(&self) -> serde_json::Value {
                $crate::typed::Typed::json(self)
            }
            /// Read a UTV JSON value: `invalid` (`kind-mismatch`) for another kind or form,
            /// otherwise the JSON layer's own verdict.
            pub fn from_json(json: &serde_json::Value) -> $crate::Outcome<Self> {
                $crate::typed::from_json(json)
            }
            /// Parse the text form. Bad text is an outcome, never a panic.
            pub fn parse(text: &str) -> $crate::Outcome<Self> {
                $crate::typed::parse_as(text)
            }
        }
        impl std::fmt::Display for $t {
            /// The text form (`docs/text-form.md`).
            fn fmt(&self, f: &mut std::fmt::Formatter<'_>) -> std::fmt::Result {
                f.pad(&$crate::typed::text_of(&$crate::typed::Typed::json(self)))
            }
        }
        impl std::fmt::Debug for $t {
            fn fmt(&self, f: &mut std::fmt::Formatter<'_>) -> std::fmt::Result {
                $crate::typed::debug(f, stringify!($t), &self.to_string())
            }
        }
        impl std::str::FromStr for $t {
            type Err = $crate::OutcomeError;
            /// `"2026-11-05".parse::<CivilDate>()`: [`Self::parse`], then [`Outcome::single`].
            fn from_str(text: &str) -> Result<Self, Self::Err> {
                Self::parse(text).single()
            }
        }
        impl $crate::ToJson for $t {
            fn to_json(&self) -> serde_json::Value {
                $crate::typed::Typed::json(self)
            }
        }
    };
}
pub(crate) use typed_value;
