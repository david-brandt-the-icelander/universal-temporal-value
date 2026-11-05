# utv (Rust) 0.1.0

Rust reader for the Universal Temporal Value specification, wire version `utv/1.0`.
Results use `utv-result/1.0`.

## Implemented

- Strict JSON parsing with `serde_json`; duplicate object keys are rejected.
- Envelope check: `schema` must be exactly `utv/1.0`. Any other well-formed
  `utv/MAJOR.MINOR` (for example `utv/1.3` or `utv/2.0`) returns `unsupported`;
  a missing or malformed identifier (for example `utv/4`) returns `invalid`.
  Unknown top-level properties and kinds outside the 26 core kinds are `invalid`.
- `validate_structural(&str) -> UtvResult`: checks each kind against the structural
  schema `schema/utv-1.0.schema.json` (embedded at compile time), including required
  fields, nested values and unknown properties.
- Result type: `Status` (invalid, unsupported, indeterminate, none, unique, multiple,
  range, symbolic, resource-limit, error, valid) and `Diagnostic {code, severity, path, message}`;
  `UtvResult::to_json()` emits a `utv-result/1.0` object.
- `validate_gregorian_civil(&str)`: semantic check of proleptic Gregorian `civil`
  values (month range, day-of-month, Feb 29 only in leap years), run after and
  separately from structural validation. Other kinds and calendars return `unsupported`.

The library never reads the system clock, time zone or locale.

## Not implemented

Everything else (resolution, zone rules, registries, non-Gregorian calendars,
semantic validation beyond the above, all evaluation operations) returns `unsupported`
or is not offered yet.

## Examples

```
cargo run --example traditional
```

`examples/traditional.rs` first builds two dates from Rust values (5 November 2026,
which exists, and 30 February 2026, which is well-formed but doesn't) and shows how a
caller matches on the result status and reads the diagnostics. It then tours the shared
catalog [`examples/traditional.json`](../../examples/traditional.json) of 37 traditional
date/time tasks, printing each one's usual call, the outcome of each check and why the UTV
form differs. It exits non-zero if any outcome differs from the catalog.

## Tests

```
cargo test
```

Integration tests read `conformance/examples/valid.json` (every value must be `valid`)
and `conformance/examples/invalid-structural.json` (every value must be rejected),
resolved relative to `CARGO_MANIFEST_DIR`. `tests/traditional.rs` checks every expectation
in `examples/traditional.json`, using the same comparison code as the example
(`examples/catalog/mod.rs`), so the tests fail whenever the library disagrees with the catalog.
