//! Examples as unit tests, **UTV first**: how to do everyday date and time tasks with UTV, and how
//! each answer compares with Rust's de-facto standard date/time API.
//!
//! Rust's standard library has no calendar types, only `std::time::SystemTime`, so the other side
//! is the `chrono` crate, with `chrono-tz` for named zones and `serde_json` for JSON. Where UTV and
//! chrono agree, the expected value is computed with chrono and converted to the UTV type. Where
//! they differ, the expected value is a literal, and the test's doc comment says what chrono does
//! instead.
//!
//! The suite is organized like the library: one module per UTV type or free function, and inside
//! it one module per function, so a test's path reads
//! `civil_date::add_months::returns_none_when_the_day_is_missing_from_the_target_month`. Every
//! scenario of `docs/example-tests.md` has a test here; `tests/native_first` covers the same
//! scenarios from chrono's side. The tests follow `AGENTS.md`.
//!
//! UTV's zone operations use the pinned rules of `conformance/rules/demo-zones.json` through
//! [`fixtures::demo_zone_rules`]. chrono-tz compiles its own copy of the IANA tz database into the
//! binary (release 2025b for chrono-tz 0.10.4 in `Cargo.lock`), so neither side reads the host's.
//!
//! Tests wrapped in `// BEGIN <task>` and `// END <task>` illustrate a task of `docs/side-by-side.md`;
//! `tools/side_by_side.py` copies them onto that page.

#[path = "../fixtures/mod.rs"]
mod fixtures;

mod civil_date;
mod civil_date_time;
mod civil_time;
mod civil_year_month;
mod elapsed_seconds;
mod fixed_offset_date_time;
mod instant;
mod interop_chrono;
mod interval;
mod parse;
mod period;
mod temporal_context;
mod validate_gregorian_civil;
mod validate_structural;
mod zoned_intent;
