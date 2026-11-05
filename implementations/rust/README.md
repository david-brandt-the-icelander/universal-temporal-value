# utv (Rust) 0.2.0

Rust library for the Universal Temporal Value specification, wire version `utv/1.0`.
Results use `utv-result/1.0`.

Values are UTV JSON values held as `serde_json::Value`, as in 0.1. Every operation returns a
`UtvResult` with a status, diagnostics and candidates; semantic outcomes (`none`, `multiple`,
`invalid`, `unsupported`, ...) are results, never panics or `Err`s. The library never reads the
system clock, time zone or locale.

## Implemented

### Validation (0.1)

- Strict JSON parsing with `serde_json`; duplicate object keys are rejected.
- Envelope check: `schema` must be exactly `utv/1.0`. Any other well-formed
  `utv/MAJOR.MINOR` (for example `utv/1.3` or `utv/2.0`) returns `unsupported`;
  a missing or malformed identifier (for example `utv/4`) returns `invalid`.
  Unknown top-level properties and kinds outside the 26 core kinds are `invalid`.
- `validate_structural(&str) -> UtvResult`: checks each kind against the structural
  schema `schema/utv-1.0.schema.json` (embedded at compile time), including required
  fields, nested values and unknown properties.
- `validate_gregorian_civil(&str)`: semantic check of proleptic Gregorian `civil`
  values (month range, day-of-month, Feb 29 only in leap years), run after and
  separately from structural validation. Other kinds and calendars return `unsupported`.

### Everyday API (0.2, [`docs/everyday-api.md`](../../docs/everyday-api.md))

All of it is re-exported at the crate root.

| Module | Contents |
|---|---|
| `utv::build` | `date`, `year_month`, `time` (+ `time_with_seconds`, `time_with_nanos`), `date_time` (+ `_with_seconds`, `_with_nanos`), `in_zone`, `with_offset`, `instant` (+ `instant_with_nanos`), `period(Period { .. })` with `Period::days(n)` etc., `posix_seconds`, `interval` (half-open) and `interval_with(Endpoint, Endpoint)` for included, excluded, unbounded and unknown ends |
| `utv::iso` | `parse_iso`, `format_iso` |
| `utv::ops` | `fields`, `add`, `compare`, `days_between`, `posix_seconds_between`, `contains`, `overlaps` |
| `utv::zones` | `Rules` (loads a bundle in the format of `conformance/rules/demo-zones.json`), `resolve`, `to_zone`, `to_offset` |
| `utv::interop` | adapters to Rust types (below) |

- `UtvResult` gained `candidates: Vec<serde_json::Value>` and `operation` (`validate`, `convert`,
  or the everyday op name such as `parseIso`), plus `value()` (the candidate of a `unique` result),
  `as_str()` and a `Display` that prints the status and the candidates as ISO 8601 text.
- Policies are JSON, as in the contract: `add(&v, &amount, Some(&json!({"monthEnd": "constrain"})))`,
  `resolve(&v, &rules, Some(&json!({"gap": "shift-forward-by-gap", "overlap": "earliest"})))`.
- Arithmetic is exact: years and day counts are `i128`, fractions are reduced `i128` rationals.
  Anything beyond `i128` gives `unsupported` (diagnostic `range`), never a wrapped value.
- Zone rules are always passed in. Zones missing from the bundle and times outside its
  validity window are `unsupported`; nothing is extrapolated.

### Native interop (`utv::interop`)

`std` has no calendar types, only `SystemTime`:

- `from_system_time(SystemTime) -> UtvResult` (an instant) and
  `to_system_time(&Value) -> Result<SystemTime, UtvResult>`, always available.

With the optional cargo feature **`chrono`**, `utv::interop::chrono` adds:

| chrono | UTV | functions |
|---|---|---|
| `NaiveDate` | `date` | `from_naive_date`, `to_naive_date` |
| `NaiveTime` | `time` | `from_naive_time`, `to_naive_time` |
| `NaiveDateTime` | unzoned `datetime` | `from_naive_date_time`, `to_naive_date_time` |
| `DateTime<FixedOffset>` | instant **or** fixed-offset `datetime`, chosen by `OffsetIntent` | `from_fixed_offset(dt, intent)`, `to_fixed_offset` |
| `DateTime<Utc>` | instant | `from_utc`, `to_utc` |
| `DateTime<Local>` | refused (`unsupported`) | `from_local` |

Conversions back to Rust types return `Err` with status `unsupported` when the target can't hold
the value exactly: years outside chrono's range, fractions finer than a nanosecond, a zone or
offset the type would drop, or a named-zone intention converted to `FixedOffset`. Without the
feature, the library depends only on `serde_json`, `serde` and `regex`.

## Not implemented

Everything outside the everyday API and the validators (non-Gregorian calendars, leap-second
tables, recurrences, registries, result manifests, ...) returns `unsupported` or is not offered yet.

## Everyday example

```
cargo run --example everyday --features chrono
```

[`examples/everyday.rs`](examples/everyday.rs) is the main example. In 14 sections (now, a date,
invalid dates, adding, days between, times, offsets, time zones, DST gap, DST overlap, timestamps,
durations vs periods, ranges, round trip) it shows the usual Rust code with chrono and chrono-tz,
then the same task with UTV, and a note where they differ. chrono and chrono-tz are
dev-dependencies, used only by the example and tests. Native outputs depend on the host (clock,
local zone, and the tzdb compiled into chrono-tz) and are only printed; UTV outputs are checked,
and the example exits non-zero if one differs.

## Traditional example

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
cargo test --all-features
```

`--all-features` also builds and tests the `chrono` adapters and runs the everyday example
(`tests/everyday_example.rs`). Plain `cargo test` runs everything else.

- `tests/everyday.rs` runs every fixture in `conformance/operations/everyday.json`, read from the
  repository (rules from `conformance/rules/demo-zones.json`), and fails with the id of each
  fixture whose status, candidates or operation name differ.
- `tests/operations.rs` covers contract rules the fixtures don't exercise.
- `tests/conformance.rs` reads `conformance/examples/valid.json` (every value must be `valid`)
  and `conformance/examples/invalid-structural.json` (every value must be rejected).
- `tests/traditional.rs` checks every expectation in `examples/traditional.json`, using the same
  comparison code as the traditional example (`examples/catalog/mod.rs`).
- Unit tests in `src/` cover exact arithmetic, ISO round trips and the native adapters.

All shared files are resolved relative to `CARGO_MANIFEST_DIR`.
