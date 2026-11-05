//! The shared catalog of traditional date/time tasks (`examples/traditional.json` at the
//! repository root) and the check that compares this library against it.
//!
//! Used by `examples/traditional.rs` (the printed tour) and `tests/traditional.rs` (part of
//! `cargo test`), so the tour and the tests can't disagree about what counts as a match.
#![allow(dead_code)] // each user needs a different subset

use std::path::PathBuf;

use serde_json::Value;
use utv::{validate_gregorian_civil, validate_structural, UtvResult};

pub struct Entry {
    pub id: String,
    pub section: String,
    pub title: String,
    pub usual: String,
    pub point: String,
    pub later: Option<String>,
    /// The UTV text to validate: `text` if present, otherwise the serialized `value`.
    pub text: String,
    pub expect_structural: String,
    pub expect_gregorian: Option<String>,
}

/// One check of one entry: which validator, what the catalog expects and what the library said.
pub struct Check {
    pub name: &'static str,
    pub expected: String,
    pub result: UtvResult,
}

impl Check {
    pub fn got(&self) -> &'static str {
        self.result.status.as_str()
    }

    pub fn matches(&self) -> bool {
        self.got() == self.expected
    }
}

impl Entry {
    /// The structural check always; the Gregorian check only when the catalog expects one.
    pub fn checks(&self) -> Vec<Check> {
        let mut out = vec![Check {
            name: "structural",
            expected: self.expect_structural.clone(),
            result: validate_structural(&self.text),
        }];
        if let Some(expected) = &self.expect_gregorian {
            out.push(Check {
                name: "gregorian",
                expected: expected.clone(),
                result: validate_gregorian_civil(&self.text),
            });
        }
        out
    }
}

pub fn path() -> PathBuf {
    PathBuf::from(env!("CARGO_MANIFEST_DIR")).join("../../examples/traditional.json")
}

/// Load every entry, panicking with the entry id if one is missing a required field.
pub fn load() -> Vec<Entry> {
    let p = path();
    let text = std::fs::read_to_string(&p).unwrap_or_else(|e| panic!("{}: {e}", p.display()));
    let doc: Value = serde_json::from_str(&text).unwrap_or_else(|e| panic!("{}: {e}", p.display()));
    let entries = doc["entries"].as_array().unwrap_or_else(|| panic!("{}: no `entries` array", p.display()));
    entries.iter().map(entry).collect()
}

fn entry(e: &Value) -> Entry {
    let id = e["id"].as_str().expect("catalog entry without `id`").to_owned();
    let opt = |v: &Value| v.as_str().map(str::to_owned);
    let req = |field: &str, v: &Value| opt(v).unwrap_or_else(|| panic!("entry `{id}`: missing `{field}`"));
    let text = match (&e["text"], &e["value"]) {
        (Value::String(t), _) => t.clone(),
        (Value::Null, Value::Null) => panic!("entry `{id}`: needs `value` or `text`"),
        (Value::Null, v) => v.to_string(),
        _ => panic!("entry `{id}`: `text` must be a string"),
    };
    Entry {
        section: req("section", &e["section"]),
        title: req("title", &e["title"]),
        usual: req("usual", &e["usual"]),
        point: req("point", &e["point"]),
        later: opt(&e["later"]),
        text,
        expect_structural: req("expect.structural", &e["expect"]["structural"]),
        expect_gregorian: opt(&e["expect"]["gregorian"]),
        id,
    }
}
