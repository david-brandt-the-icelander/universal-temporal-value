# Changelog

## 0.2.0

- The everyday API of `docs/everyday-api.md`, as modules re-exported at the crate root:
  `build` (builders), `iso` (`parse_iso`, `format_iso`), `ops` (`fields`, `add`, `compare`,
  `days_between`, `posix_seconds_between`, `contains`, `overlaps`) and `zones` (`Rules`,
  `resolve`, `to_zone`, `to_offset`). Exact `i128` arithmetic; values beyond it are `unsupported`.
- `UtvResult` gained `candidates` and `operation`, and `to_json()` emits both. Helpers
  `UtvResult::new`, `unique`, `value`, `as_str` and a `Display` impl.
- Native interop in `utv::interop`: `SystemTime` ↔ instant always; behind the new optional
  feature `chrono`, adapters for `NaiveDate`, `NaiveTime`, `NaiveDateTime`,
  `DateTime<FixedOffset>` (caller picks instant or offset date-time) and `DateTime<Utc>`.
  `DateTime<Local>` is refused, and lossy conversions back to chrono return `unsupported`.
- `examples/everyday.rs`: 14 everyday tasks side by side, chrono/chrono-tz then UTV
  (`cargo run --example everyday --features chrono`). chrono and chrono-tz are dev-dependencies.
- Tests: `tests/everyday.rs` runs all of `conformance/operations/everyday.json`;
  `tests/everyday_example.rs` runs the example under `cargo test --all-features`;
  `tests/operations.rs` covers rules the fixtures don't.
- Schema patterns used by structural validation are compiled once and cached.

### Also in 0.2.0 (changes made after 0.1.0)

- Added `examples/traditional.rs`, a runnable tour of the shared
  `examples/traditional.json` catalog (`cargo run --example traditional`), and
  `tests/traditional.rs`, which checks every catalog outcome as part of `cargo test`.
- `validate_structural` no longer runs the Gregorian date check. That check is now
  the separate `validate_gregorian_civil`, which returns `unsupported` for other kinds
  and calendars. Structural and semantic validation are separate questions.
- Any well-formed `utv/M.N` other than `utv/1.0` (including other major versions)
  now returns `unsupported`, as `VERSIONING.md` requires.

## 0.1.0

- First increment: envelope and structural validation for `utv/1.0` against the
  embedded schema, the `utv-result/1.0` result type, and proleptic Gregorian date
  checks.
