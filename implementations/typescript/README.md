# @utv/core (TypeScript)

- **Library version:** 0.1.0
- **Specification versions read:** `utv/1.0` (result contract `utv-result/1.0`). Any other well-formed `utv/M.N` (newer minor or other major, e.g. `utv/1.1`, `utv/2.0`) is reported as `unsupported`; a missing, non-string or malformed identifier (e.g. `utv/4`, `utv/1`) is `invalid`.
- **Conformance profiles claimed:** none yet (this is a first increment, short of `core-civil`).
- **Registries shipped:** none (no tzdb, leap-second table or non-Gregorian calendars).
- **Runtime dependencies:** none. Library code never reads the system clock, host time zone, locale or network.

## Implemented

- `parseStrict(text)`: a hand-written JSON parser that rejects duplicate object keys (at any depth).
- `validateStructural(text)` → `UtvResult` (`utv-result/1.0`, operation `validate`):
  - envelope: only `schema`, `kind`, `value`, `provenance`, `extensions`; `schema`, `kind`, `value` required;
  - `schema` must be exactly `utv/1.0` (other well-formed `utv/M.N` → `unsupported`, malformed → `invalid`);
  - `kind` must be one of the 26 core kinds;
  - per-kind required and allowed `value` members, transcribed from `schema/utv-1.0.schema.json`;
  - `instant` ticks-xor-coordinate, `interval` construction-specific members, `series.samples` must be `sample` envelopes;
  - nested UTV envelopes are validated recursively.
  Deeper schema constraints (enums, nested field shapes, integer-string patterns, …) are not checked yet.
- `validateSemantic(text)`: structural check first, then proleptic Gregorian `civil` date validation
  (month 1–12, day within month, leap years via arbitrary-precision `bigint` years, e.g. Feb 30 is invalid,
  Feb 29 only in leap years).
- Types: `Status` (invalid, unsupported, indeterminate, none, unique, multiple, range, symbolic,
  resource-limit, error, valid), `Diagnostic` (`{code, severity, path, message, evidenceRefs?}`), `UtvResult`.

**Everything else returns `unsupported`**: semantic validation of other kinds, other calendars or
chronologies, and all other operations (resolve, compare, add, convert, expand, …) which are not implemented.

## Build and test

```sh
npm test   # tsc -p . && node --test "dist/test/**/*.test.js"
```

`typescript` is the only devDependency (a globally installed `tsc` also works). Tests read the shared
fixtures `conformance/examples/valid.json` and `conformance/examples/invalid-structural.json` from the
repository root, resolved relative to this package directory.
