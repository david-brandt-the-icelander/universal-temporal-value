# @utv/core (TypeScript)

- **Library version:** 0.2.0
- **Specification versions read:** `utv/1.0` (result contract `utv-result/1.0`). Any other well-formed `utv/M.N` (newer minor or other major, e.g. `utv/1.1`, `utv/2.0`) is reported as `unsupported`; a missing, non-string or malformed identifier (e.g. `utv/4`, `utv/1`) is `invalid`.
- **Conformance profiles claimed:** none yet (short of `core-civil`). The everyday API passes every fixture in
  [`conformance/operations/everyday.json`](../../conformance/operations/everyday.json).
- **Registries shipped:** none (no tzdb, leap-second table or non-Gregorian calendars). `loadRules` parses rule bundles in
  the format of [`conformance/rules/demo-zones.json`](../../conformance/rules/demo-zones.json); the caller reads the file.
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
  resource-limit, error, valid), `Diagnostic` (`{code, severity, path, message, evidenceRefs?}`), `UtvResult`
  (now with `operation`, `candidates` and `manifest.dependencies`; `candidate(result)` returns the single
  candidate of a `unique` result).

### Everyday API (0.2)

The contract is [`docs/everyday-api.md`](../../docs/everyday-api.md). Values are plain JSON (`Json`), as in 0.1.
Every operation returns a `UtvResult` and never throws for a semantic outcome.

| Module | Exports |
|---|---|
| `src/builders.ts` | `date`, `yearMonth`, `time`, `dateTime`, `inZone`, `withOffset`, `instant`, `period` (positional or `period({ months: 1 })`), `posixSeconds`, `interval` (ends may be `"unbounded"` or `"unknown"`) |
| `src/iso.ts` | `parseIso`, `formatIso` |
| `src/ops.ts` | `fields`, `add` (`{ monthEnd: "reject" \| "constrain" }`), `compare`, `daysBetween`, `posixSecondsBetween`, `contains`, `overlaps` |
| `src/zones.ts` | `loadRules` → `ZoneRules`, `resolve` (`{ gap, overlap }` policy), `toZone`, `toOffset` |
| `src/native.ts` | native interop, below |

All arithmetic is exact (`bigint` years and days, rational seconds). Where the contract is silent this library:

- drops an `offsetAssertion` when `add` changes the wall time (diagnostic `offset-assertion-dropped`), since the old offset may no longer hold;
- in `resolve`, keeps only the candidate whose offset matches an `offsetAssertion`, and gives `invalid` (`offset-mismatch`) when none does;
- treats RFC 9557 annotations other than one `[Area/Location]` (e.g. `[u-ca=…]`, `[+01:00]`) as `unsupported`.

### Native interop

`Date` is an instant read in the host zone; this library never reads that zone. The caller names the intent:

| Function | Does |
|---|---|
| `fromDate(d, "instant")` | the instant a `Date` holds |
| `fromDate(d, { offsetSeconds })`, `fromDate(d, { zone, rules })` | the civil date-time at a fixed offset, or in a named zone with explicit rules |
| `fromDate(d, "local")` | refused: `unsupported` (`host-zone-refused`) |
| `toDate(v)` | instant or fixed-offset date-time → `Date`; sub-millisecond precision and a dropped offset are reported in `loss`; zoned or floating values are `unsupported` |
| `fromEpochMilliseconds(ms)`, `toEpochMilliseconds(v)` | epoch milliseconds ↔ instant (loss reported) |
| `fromPlainDate({ year, month, day })`, `toPlainDate(v)` | a plain object (1-based months) ↔ date; 30 February is `invalid` |

Conversions to native types return a `NativeResult<T>`: `{ status, value?, loss, diagnostics }`.

## Build and test

```sh
npm test   # tsc -p . && node --test "dist/test/**/*.test.js"
```

`typescript` is the only devDependency (a globally installed `tsc` also works). Tests read the shared
fixtures `conformance/examples/valid.json`, `conformance/examples/invalid-structural.json`,
`conformance/operations/everyday.json` (every fixture, `test/everyday.test.ts`) and
`conformance/rules/demo-zones.json` from the repository root, resolved relative to this package directory.

## Everyday example

```sh
npm run everyday   # tsc -p . && node dist/examples/everyday.js
```

[`examples/everyday.ts`](examples/everyday.ts) is the main example: fourteen everyday tasks (now, a date, invalid
dates, adding, days between, times, offsets, time zones, DST gap and overlap, timestamps, durations vs periods,
ranges, round trip), each done first with `Date` and `Intl.DateTimeFormat`, then with UTV. Native lines print what
this host gives (its clock and tz data); the UTV lines are deterministic and checked, so the run exits with code 1 on
any mismatch. `npm test` runs it too (`test/everyday-example.test.ts`).

## Traditional catalog tour

```sh
npm run example   # tsc -p . && node dist/examples/traditional.js
```

[`examples/traditional.ts`](examples/traditional.ts) builds two Gregorian dates from TypeScript values (5 November 2026
and 30 February 2026), validates them and switches on the result status. It then walks the shared catalog
[`examples/traditional.json`](../../examples/traditional.json) (37 everyday date and time tasks; see
[`examples/README.md`](../../examples/README.md)), printing each task's usual library call, this library's
structural and Gregorian statuses, and why the UTV form differs. Any outcome that differs from the catalog is
printed as `MISMATCH` and the process exits with code 1. `npm test` runs the same catalog check
(`test/traditional.test.ts`).
