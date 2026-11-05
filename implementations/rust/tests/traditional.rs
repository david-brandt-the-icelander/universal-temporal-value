//! Every expectation in the shared catalog `examples/traditional.json` must hold, so
//! `cargo test` fails whenever this library disagrees with it. The comparison lives in
//! `examples/catalog/mod.rs`, shared with `cargo run --example traditional`.

#[path = "../examples/catalog/mod.rs"]
mod catalog;

#[test]
fn every_catalog_outcome_is_as_expected() {
    let entries = catalog::load();
    assert!(!entries.is_empty(), "{}: no entries", catalog::path().display());
    let mut bad = vec![];
    for e in &entries {
        for c in e.checks().iter().filter(|c| !c.matches()) {
            bad.push(format!("{}: {} expected {}, got {} {:?}", e.id, c.name, c.expected, c.got(), c.result.diagnostics));
        }
    }
    assert!(bad.is_empty(), "{} mismatches with the catalog:\n{}", bad.len(), bad.join("\n"));
}
