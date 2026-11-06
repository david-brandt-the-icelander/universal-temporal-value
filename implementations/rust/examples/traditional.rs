//! A tour of traditional date/time tasks in UTV.
//!
//! Run with `cargo run --example traditional`. It first builds two dates from Rust values, then
//! walks the shared catalog `examples/traditional.json` and fails if any outcome differs from it.

mod catalog;

use serde_json::json;
use utv::{validate_gregorian_civil, validate_structural, Status, SPEC_VERSION};

/// A proleptic Gregorian date. UTV years are strings, so they are never limited to 4 digits.
fn gregorian_date(year: i64, month: u32, day: u32) -> String {
    json!({
        "schema": SPEC_VERSION,
        "kind": "civil",
        "value": {
            "form": "date",
            "fields": { "year": year.to_string(), "month": month, "day": day },
            "calendar": { "id": "gregory", "chronology": "proleptic" }
        }
    })
    .to_string()
}

fn own_values() {
    println!("== Building dates from Rust values ==");
    for (label, text) in [("5 November 2026", gregorian_date(2026, 11, 5)), ("30 February 2026", gregorian_date(2026, 2, 30))] {
        println!("\n{label}");
        // Well-formed UTV is one question; whether the date exists in the calendar is another.
        println!("  structural: {}", validate_structural(&text).status.as_str());
        let result = validate_gregorian_civil(&text);
        match result.status {
            Status::Valid => println!("  gregorian:  valid, the date exists"),
            Status::Invalid => {
                for d in &result.diagnostics {
                    println!("  gregorian:  invalid, {} at {}: {}", d.code, d.path, d.message);
                }
            }
            Status::Unsupported => println!("  gregorian:  unsupported, this library can't judge it"),
            other => println!("  gregorian:  {}", other.as_str()),
        }
    }
}

fn main() {
    own_values();

    let entries = catalog::load();
    let mut mismatches = 0;
    let mut section = "";
    for e in &entries {
        if e.section != section {
            section = &e.section;
            println!("\n== {section} ==");
        }
        let checks = e.checks();
        println!("\n{}", e.title);
        println!("  usual:      {}", e.usual);
        for c in &checks {
            println!("  {:<11} {}", format!("{}:", c.name), c.got());
        }
        println!("  why:        {}", e.point);
        if let Some(later) = &e.later {
            println!("  later:      {later}");
        }
        for c in checks.iter().filter(|c| !c.matches()) {
            println!("  MISMATCH: {} expected {}, got {}", c.name, c.expected, c.got());
            mismatches += 1;
        }
    }

    println!();
    if mismatches == 0 {
        println!("{} examples, every outcome as expected.", entries.len());
    } else {
        println!("{mismatches} mismatches.");
        std::process::exit(1);
    }
}
