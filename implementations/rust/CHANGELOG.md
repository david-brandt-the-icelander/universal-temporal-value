# Changelog

## Unreleased

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
