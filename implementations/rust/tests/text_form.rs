//! Runs every fixture of the shared `conformance/operations/text-form.json` (`docs/text-form.md`)
//! through the JSON-level `parse_text` / `format_text`, read from the repository, not a copy.
//!
//! - `both`: `parse_text(text)` gives `value` (unique) and `format_text(value)` gives `text`;
//! - `parse` / `format`: one direction, checked against `expect` (status, the value or text for
//!   `unique`, and every listed diagnostic code).

use std::path::PathBuf;

use serde_json::Value;
use utv::{format_text, parse_text, UtvResult};

fn read_json(path: &str) -> Value {
    let p = PathBuf::from(env!("CARGO_MANIFEST_DIR")).join("../..").join(path);
    let text = std::fs::read_to_string(&p).unwrap_or_else(|e| panic!("{}: {e}", p.display()));
    serde_json::from_str(&text).unwrap_or_else(|e| panic!("{}: {e}", p.display()))
}

/// Compare a result with an expectation: `status`, then `want` as the unique candidate (if
/// given), then every code in `codes`. `None` when they agree, else what differs.
fn differs(r: &UtvResult, operation: &str, status: &str, want: Option<&Value>, codes: &[Value]) -> Option<String> {
    let got_codes: Vec<&str> = r.diagnostics.iter().map(|d| d.code.as_str()).collect();
    let summary = || format!("{} {} {got_codes:?}", r.status.as_str(), Value::Array(r.candidates.clone()));
    if r.operation != operation {
        return Some(format!("operation `{}`, expected `{operation}`", r.operation));
    }
    if r.status.as_str() != status {
        return Some(format!("expected {status}, got {}", summary()));
    }
    if let Some(want) = want {
        if r.value() != Some(want) {
            return Some(format!("expected {want}, got {}", summary()));
        }
    }
    codes.iter().filter_map(Value::as_str).find(|c| !got_codes.contains(c)).map(|c| format!("expected diagnostic `{c}`, got {}", summary()))
}

#[test]
fn every_text_form_fixture_passes() {
    let doc = read_json("conformance/operations/text-form.json");
    let fixtures = doc["fixtures"].as_array().expect("`fixtures` array");
    assert!(!fixtures.is_empty(), "no fixtures");

    let mut failures = vec![];
    for f in fixtures {
        let id = f["id"].as_str().expect("fixture id");
        let expect = &f["expect"];
        let codes = expect["diagnostics"].as_array().map(Vec::as_slice).unwrap_or_default();
        let text = f["text"].as_str();
        let mut problems = vec![];
        match f["direction"].as_str() {
            Some("both") => {
                let (text, value) = (text.expect("`both` needs text"), &f["value"]);
                problems.extend(differs(&parse_text(text), "parseText", "unique", Some(value), &[]).map(|p| format!("parse: {p}")));
                problems.extend(differs(&format_text(value), "formatText", "unique", Some(&Value::String(text.into())), &[]).map(|p| format!("format: {p}")));
            }
            Some("parse") => {
                let r = parse_text(text.expect("`parse` needs text"));
                problems.extend(differs(&r, "parseText", expect["status"].as_str().unwrap_or(""), expect.get("value"), codes));
            }
            Some("format") => {
                let r = format_text(&f["value"]);
                let want = expect.get("text").cloned();
                problems.extend(differs(&r, "formatText", expect["status"].as_str().unwrap_or(""), want.as_ref(), codes));
            }
            other => problems.push(format!("unknown direction {other:?}")),
        }
        failures.extend(problems.into_iter().map(|p| format!("{id}: {p}")));
    }
    println!("text-form fixtures: {}/{} passed", fixtures.len() - failures.len(), fixtures.len());
    assert!(failures.is_empty(), "{} text-form problems:\n{}", failures.len(), failures.join("\n"));
}
