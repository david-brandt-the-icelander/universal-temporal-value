//! Runs every fixture of the shared `conformance/operations/everyday.json` (the arbiter of
//! `docs/everyday-api.md`), read from the repository, not a copy. Fails with the id of every
//! fixture whose status or candidates differ. Results are compared as JSON values; object key
//! order is irrelevant (`serde_json::Map` equality ignores it).

use std::collections::HashMap;
use std::path::PathBuf;

use serde_json::Value;
use utv::{Rules, UtvResult};

fn repo(path: &str) -> PathBuf {
    PathBuf::from(env!("CARGO_MANIFEST_DIR")).join("../..").join(path)
}

fn read_json(path: &str) -> Value {
    let p = repo(path);
    let text = std::fs::read_to_string(&p).unwrap_or_else(|e| panic!("{}: {e}", p.display()));
    serde_json::from_str(&text).unwrap_or_else(|e| panic!("{}: {e}", p.display()))
}

fn run(op: &str, args: &[Value], rules: &HashMap<String, Rules>) -> Result<UtvResult, String> {
    let rules_arg = |i: usize| -> Result<&Rules, String> {
        let name = args.get(i).and_then(Value::as_str).ok_or("rules argument must be a name")?;
        rules.get(name).ok_or_else(|| format!("unknown rules `{name}`"))
    };
    let arg = |i: usize| args.get(i).ok_or_else(|| format!("missing argument {i}"));
    Ok(match op {
        "parseIso" => utv::parse_iso(arg(0)?.as_str().ok_or("parseIso takes text")?),
        "formatIso" => utv::format_iso(arg(0)?),
        "fields" => utv::fields(arg(0)?),
        "add" => utv::add(arg(0)?, arg(1)?, args.get(2)),
        "compare" => utv::compare(arg(0)?, arg(1)?),
        "daysBetween" => utv::days_between(arg(0)?, arg(1)?),
        "posixSecondsBetween" => utv::posix_seconds_between(arg(0)?, arg(1)?),
        "contains" => utv::contains(arg(0)?, arg(1)?),
        "overlaps" => utv::overlaps(arg(0)?, arg(1)?),
        "resolve" => utv::resolve(arg(0)?, rules_arg(1)?, args.get(2)),
        "toZone" => utv::to_zone(arg(0)?, arg(1)?.as_str().ok_or("toZone takes a zone id")?, rules_arg(2)?),
        "toOffset" => utv::to_offset(arg(0)?, arg(1)?.as_i64().ok_or("toOffset takes seconds")?),
        other => return Err(format!("unknown op `{other}`")),
    })
}

#[test]
fn every_everyday_fixture_passes() {
    let doc = read_json("conformance/operations/everyday.json");
    let rules: HashMap<String, Rules> = doc["rules"]
        .as_object()
        .expect("`rules` map")
        .iter()
        .map(|(name, path)| {
            let p = path.as_str().expect("rules path");
            (name.clone(), Rules::from_json(&read_json(p)).unwrap_or_else(|e| panic!("{p}: {e}")))
        })
        .collect();
    let fixtures = doc["fixtures"].as_array().expect("`fixtures` array");
    assert!(!fixtures.is_empty(), "no fixtures");

    let mut failures = vec![];
    for f in fixtures {
        let id = f["id"].as_str().expect("fixture id");
        let args = f["args"].as_array().map(Vec::as_slice).unwrap_or_default();
        let expect = &f["expect"];
        let op = f["op"].as_str().unwrap_or("");
        let r = match run(op, args, &rules) {
            Ok(r) if r.operation != op => {
                failures.push(format!("{id}: result operation is `{}`, expected `{op}`", r.operation));
                continue;
            }
            Ok(r) => r,
            Err(e) => {
                failures.push(format!("{id}: {e}"));
                continue;
            }
        };
        let want_status = expect["status"].as_str().unwrap_or("");
        let want_candidates: Vec<Value> = match (expect.get("result"), expect.get("results")) {
            (Some(one), _) => vec![one.clone()],
            (None, Some(Value::Array(many))) => many.clone(),
            _ => vec![],
        };
        if r.status.as_str() != want_status || r.candidates != want_candidates {
            failures.push(format!(
                "{id}: expected {want_status} {}, got {} {} {:?}",
                Value::Array(want_candidates),
                r.status.as_str(),
                Value::Array(r.candidates.clone()),
                r.diagnostics.iter().map(|d| format!("{}: {}", d.code, d.message)).collect::<Vec<_>>()
            ));
        }
    }
    println!("everyday fixtures: {}/{} passed", fixtures.len() - failures.len(), fixtures.len());
    assert!(failures.is_empty(), "{} of {} fixtures failed:\n{}", failures.len(), fixtures.len(), failures.join("\n"));
}
