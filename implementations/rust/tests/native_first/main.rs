//! Examples as unit tests, **native first**: what Rust's de-facto standard date/time API does for
//! everyday tasks, and how each answer compares with UTV.
//!
//! Rust's standard library has no calendar types, so "native" means the `chrono` crate, with
//! `chrono-tz` for named zones, plus `std::time::SystemTime`, `std::ops`'s ranges and `serde_json`.
//! Where the native API and UTV agree, the expected value is computed with UTV and converted to the
//! native type. Where they differ, the expected value is a literal, and the test's doc comment says
//! what UTV does instead.
//!
//! The suite is organized like the native API: one module per type (or free function), and inside
//! it one module per function, so a test's path reads
//! `naive_date::checked_add_months::clamps_to_the_last_day_when_the_day_is_missing_from_the_target_month`.
//! It covers every scenario of `docs/example-tests.md` that chrono, std or serde_json can express;
//! `tests/utv_first` covers them all from UTV's side. The tests follow `AGENTS.md`.
//!
//! chrono-tz compiles its own copy of the IANA tz database into the binary (release 2025b for
//! chrono-tz 0.10.4 in `Cargo.lock`), so the zone tests here don't read the host's tz database;
//! they need that copy to know the 2026–2027 rules of New York and Paris. UTV uses only the pinned
//! rules of `conformance/rules/demo-zones.json`, through [`fixtures::demo_zone_rules`].

#[path = "../fixtures/mod.rs"]
mod fixtures;

// chrono
mod date_time;
mod fixed_offset;
mod local;
mod naive_date;
mod naive_date_time;
mod naive_time;
mod time_delta;
mod utc;
mod weekday;

// chrono-tz
mod tz;

// std
mod range;
mod range_from;
mod range_inclusive;
mod system_time;

// serde_json
mod json;
