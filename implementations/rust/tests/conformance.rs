use std::path::PathBuf;
use utv::{validate_structural, Status};

fn load(name: &str) -> serde_json::Map<String, serde_json::Value> {
    let p = PathBuf::from(env!("CARGO_MANIFEST_DIR")).join("../../conformance/examples").join(name);
    let text = std::fs::read_to_string(&p).unwrap_or_else(|e| panic!("{}: {e}", p.display()));
    serde_json::from_str::<serde_json::Value>(&text).unwrap().as_object().unwrap().clone()
}

#[test]
fn valid_examples_are_valid() {
    let mut bad = vec![];
    for (name, v) in load("valid.json") {
        let r = validate_structural(&v.to_string());
        if r.status != Status::Valid { bad.push(format!("{name}: {:?}", r.diagnostics)); }
    }
    assert!(bad.is_empty(), "{bad:#?}");
}

#[test]
fn invalid_structural_examples_are_rejected() {
    let mut bad = vec![];
    for (name, v) in load("invalid-structural.json") {
        let r = validate_structural(&v.to_string());
        if r.status == Status::Valid { bad.push(name); }
    }
    assert!(bad.is_empty(), "{bad:#?}");
}

#[test]
fn envelope_rules() {
    let base = r#"{"schema":"utv/1.0","kind":"instant","value":{"system":{"system":"utv.timescale","id":"unix-posix","version":"1"},"ticks":"0"}}"#;
    assert_eq!(validate_structural(base).status, Status::Valid);
    assert_eq!(validate_structural(&base.replace("utv/1.0", "utv/1.3")).status, Status::Unsupported);
    assert_eq!(validate_structural(&base.replace("utv/1.0", "utv/1.03")).status, Status::Invalid);
    assert_eq!(validate_structural(&base.replace("utv/1.0", "utv/2.0")).status, Status::Unsupported);
    assert_eq!(validate_structural(&base.replace("utv/1.0", "utv/4")).status, Status::Invalid);
    assert_eq!(validate_structural(&base.replace("utv/1.0", "utv/01.0")).status, Status::Invalid);
    assert_eq!(validate_structural(&base.replace("\"instant\"", "\"bogus\"")).status, Status::Invalid);
    assert_eq!(validate_structural(&base.replace("\"value\":", "\"zzz\":1,\"value\":")).status, Status::Invalid);
    assert_eq!(validate_structural(&base.replace("\"kind\":\"instant\"", "\"kind\":\"instant\",\"kind\":\"instant\"")).status, Status::Invalid);
    assert_eq!(validate_structural(base).to_json()["schema"], "utv-result/1.0");
}

#[test]
fn gregorian_dates() {
    let d = |y: &str, m: u32, day: u32| format!(r#"{{"schema":"utv/1.0","kind":"civil","value":{{"form":"date","fields":{{"year":"{y}","month":{m},"day":{day}}},"calendar":{{"id":"gregory","chronology":"proleptic"}}}}}}"#);
    assert_eq!(validate_structural(&d("2026", 2, 30)).status, Status::Invalid);
    assert_eq!(validate_structural(&d("2024", 2, 29)).status, Status::Valid);
    assert_eq!(validate_structural(&d("2026", 2, 29)).status, Status::Invalid);
    assert_eq!(validate_structural(&d("1900", 2, 29)).status, Status::Invalid);
    assert_eq!(validate_structural(&d("2000", 2, 29)).status, Status::Valid);
}
