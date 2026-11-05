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

## Tests

```
cargo test
```

Integration tests read `conformance/examples/valid.json` (every value must be `valid`)
and `conformance/examples/invalid-structural.json` (every value must be rejected),
resolved relative to `CARGO_MANIFEST_DIR`.
