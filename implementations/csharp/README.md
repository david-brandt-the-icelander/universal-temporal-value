# Utv (C#)

- **Library version:** 0.2.0 (NuGet package `Utv`), .NET 8 (`net8.0`). No runtime dependencies beyond the BCL.
- **Specification versions read:** `utv/1.0`. Results use `utv-result/1.0`. Writing is not implemented yet.
- **Conformance profiles claimed:** none yet. This is a first increment.
- **Registries shipped:** none (no tzdb, leap-second table or non-Gregorian calendars). Zone rules are passed in by the
  caller as a `ZoneRules` bundle (for example `conformance/rules/demo-zones.json`).
- Library code never reads the system clock, host time zone, locale or network. All number and
  string formatting uses `CultureInfo.InvariantCulture`, enforced by analyzer rules (CA1304/1305/1307/1309/1310/1311)
  set to errors in `src/Utv/.editorconfig`.

## What is implemented

Everything is in namespace `Utv`.

- `UtvValidator.ValidateStructural(string json) -> UtvResult` (operation `validate`):
  - Strict JSON parsing on `System.Text.Json`'s `JsonDocument`: no comments, trailing commas, `NaN`/`Infinity`
    or trailing content; nesting deeper than 128 levels and strings with unpaired surrogates are rejected.
    Duplicate object keys are detected by the library itself at every depth (keys are compared after
    unescaping, so `"a"` and `"a"` are duplicates). Any of these gives `invalid`.
  - `schema` must be exactly `utv/1.0`. Any other well-formed identifier matching
    `^utv/(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)$` (for example `utv/1.3` or `utv/2.0`) gives `unsupported`;
    this check comes first, so a newer version's unknown kinds or properties are never called `invalid`.
    A missing, non-string or malformed identifier (for example `utv/4`, `utv/1`, `utv/1.01` or `"utv/1.0\n"`) gives `invalid`.
  - Unknown top-level properties and any `kind` outside the 26 core kinds give `invalid`.
  - Every value, top-level and nested, is checked against `schema/utv-1.0.schema.json`. The schema is
    embedded in the assembly as a resource linked from the repository file (not copied). A small interpreter
    implements exactly the keywords that schema uses (`$ref`, `type`, `const`, `enum`, `minLength`, `pattern`,
    `items`, `minItems`, `required`, `properties`, `additionalProperties`, `oneOf`, `anyOf`, `not`; `format`
    is an annotation, as in draft 2020-12). Loading fails if the schema ever uses any other keyword.
    JSON Schema semantics are kept where .NET differs: `pattern` anchors follow ECMA-262 (`$` does not match
    before a trailing newline), `minLength` counts code points, `integer` accepts `2.0`, and numbers are compared
    exactly (never via floating point).
  - `#/$defs/value` is evaluated by dispatching on `kind`, which is equivalent to the schema's `oneOf` (a test
    checks the schema keeps that shape) and gives diagnostics from the one applicable kind.
- `GregorianCivil.Validate(string json) -> UtvResult`: a separate semantic check. It runs structural validation
  first and returns that result unless it is `valid`. Then, for `civil` values with
  `calendar = {id: "gregory", chronology: "proleptic"}`, it checks `year`/`month`/`day`: month in 1..12, day
  within the month, Feb 29 only in leap years (Feb 30 never). Years are `BigInteger`s with astronomical
  numbering. Any form is accepted; missing fields are not required (`month-day` Feb 29 is valid).
  Time-of-day, zone and offset fields are not checked. Other kinds and other calendars or chronologies
  return `unsupported`. Also exposes `IsLeapYear(BigInteger)` and `DaysInMonth(BigInteger?, int)`.
- Result types:
  - `UtvStatus`: `Invalid`, `Unsupported`, `Indeterminate`, `None`, `Unique`, `Multiple`, `Range`, `Symbolic`,
    `ResourceLimit`, `Error`, `Valid`, with `ToWireString()` / `TryParseWireString()` (`resource-limit`, …).
  - `Diagnostic(string Code, DiagnosticSeverity Severity, string Path, string Message)`, where `Path` is an
    RFC 6901 JSON Pointer (`""` is the whole document).
  - `UtvResult`: `Operation`, `Status`, `Diagnostics`, `Candidates` (cloned `JsonElement` envelopes), and
    `ToJson()`, which emits a compact `utv-result/1.0` object with `schema`, `operation`, `status`, `candidates`,
    `manifest`, `assumptions`, `loss` and `diagnostics`. The constructor enforces that `none` has no candidates
    and `unique` exactly one.
- `UtvValidator.SpecVersion`, `UtvValidator.CoreKinds`, `UtvValidator.IsCoreKind()`, `UtvValidator.IsWellFormedVersion()`.

Diagnostic codes used so far: `malformed-json`, `duplicate-key`, `not-an-object`, `unknown-schema`,
`unsupported-version`, `unknown-kind`, `unknown-property`, `missing-property`, `wrong-type`, `const-mismatch`,
`enum-mismatch`, `too-short`, `pattern-mismatch`, `too-few-items`, `schema-violation`; and from
`GregorianCivil`: `unsupported-kind`, `unsupported-calendar`, `month-out-of-range`, `day-out-of-range`.

## Everyday API (0.2)

Implements [`docs/everyday-api.md`](../../docs/everyday-api.md), including its Clarifications. Every fixture in
`conformance/operations/everyday.json` passes. Values are UTV JSON held in `System.Text.Json.JsonElement`, as in 0.1.
Every operation returns a `UtvResult` and never throws for a semantic outcome.

| Class (namespace `Utv`) | Members |
|---|---|
| `Build` | `Date`, `YearMonth`, `Time`, `DateTime`, `InZone`, `WithOffset`, `Instant`, `Period` (named args: `Build.Period(months: 1)`), `PosixSeconds`, `Interval` (with `IntervalBound.Included/Excluded/Unbounded/Unknown`) |
| `Iso` | `ParseIso(string)`, `FormatIso(value)` |
| `Calendar` | `Fields`, `Add(value, amount, MonthEndPolicy = Reject)`, `Compare`, `DaysBetween`, `PosixSecondsBetween` |
| `Ranges` | `Contains(interval, point)`, `Overlaps(a, b)`: `"definitely"`, `"possibly"` or `"impossible"` |
| `Zones` | `Resolve(dateTime, rules, ResolutionPolicy? = null)`, `ToZone(instant, zoneId, rules)`, `ToOffset(instant, seconds)` |
| `ZoneRules` | `Parse(json)`: a rule bundle in the `demo-zones.json` format. Nothing outside `validFrom`..`validUntil` is extrapolated |
| `Interop` | see below |

- **Results.** `UtvResult.Candidates` holds UTV values, or plain JSON for operations that answer with a string, number
  or object (`formatIso`, `compare`, `fields`, `daysBetween`, `contains`, `overlaps`). New accessors: `IsUnique`,
  `Value` (the single candidate; it throws `InvalidOperationException` unless the status is `unique`), `TryGetValue`, `GetString()` and `GetInt64()`.
  `Operation` carries the everyday operation's name (`parseIso`, `add`, …).
- **Policies.** `MonthEndPolicy { Reject, Constrain }`. `ResolutionPolicy(GapPolicy Gap, OverlapPolicy Overlap)`
  with every schema value. Implemented: gap `reject`, `preserve-unresolved` and `shift-forward-by-gap`; overlap `reject`,
  `all`, `earliest` and `latest`. Any other value gives `unsupported`. `ResolutionPolicy.TryParse(JsonElement)` and
  `PolicyWire` convert to and from the wire strings.
- **Inputs are validated.** Every operation first runs the 0.1 structural validator on its UTV arguments. Then it checks the
  values semantically: dates must exist, and hour, minute, second and nano must be in range.
- **Choices where the contract is silent:**
  - `parseIso` gives `unsupported` (not `invalid`) for other valid ISO 8601 forms: a decimal comma, more than nine
    fraction digits, a year on its own, a time with `T` or an offset, and basic-format offsets.
  - An `offsetAssertion` that is not a whole number of minutes cannot be formatted, so `formatIso` gives `unsupported`.

### Native interop

`Interop` converts between UTV values and `DateOnly`, `TimeOnly`, `DateTime`, `DateTimeOffset` and `TimeSpan`:

- **Into UTV.** `FromDateOnly`, `FromTimeOnly`, `FromDateTime` and `FromDateTimeOffset(value, DateTimeOffsetIntent.Instant | OffsetDateTime)`
  (the caller names the intent), plus `PosixSecondsFromTimeSpan`.
  - `FromDateTime` maps `Kind.Unspecified` to a floating `datetime` and `Kind.Utc` to an `instant`.
  - `Kind.Local` is refused with `unsupported` (`local-zone`). The host zone is never read.
- **Back to .NET.** `ToDateOnly`, `ToTimeOnly`, `ToDateTime`, `ToDateTimeOffset` and `ToTimeSpan` return
  `NativeResult<T>`, which has `Status`, `Value`, `IsLossy` and `Diagnostics`.
  - **`unsupported`:** years outside 1..9999, nanoseconds that are not a multiple of 100, a zoned or offset value
    passed to `ToDateTime`, a zoned value without an asserted offset, and calendar periods passed to `TimeSpan`.
  - **Lossy:** a zoned value with an `offsetAssertion` converts to a `DateTimeOffset` with `IsLossy = true` and a
    `zone-dropped` warning.
  - **Instants:** an instant becomes a `DateTimeOffset` at +00:00, with an info diagnostic saying the local offset is not known.
- **No clock.** The library never reads the clock: pass `DateTimeOffset.UtcNow` in yourself. A test scans the library
  source for clock, host-zone and culture reads.

## What is not implemented

Everything outside the everyday API: convert, expand, equivalent and the other operations of `docs/operations.md`; canonical JSON
round-tripping and writing; other calendars, zones, recurrences; semantic validation of any kind other than
Gregorian `civil` dates; result `inputDigest`/`contextDigest`. The conformance scenarios
(`conformance/scenarios/*.json`) are not executable yet (OI-12) and are not run. When a `oneOf` fails (for example
an interval boundary holding a bad nested value), only a `schema-violation` at the `oneOf` location is reported,
not the failures inside each alternative.

## Build and test

```
cd implementations/csharp
dotnet test
```

The tests read `schema/utv-1.0.schema.json`, `conformance/examples/valid.json` (every value must be `valid`)
and `conformance/examples/invalid-structural.json` (every value must be `invalid`) from the repository root,
resolved relative to the test project at build time. They also check that the embedded schema is identical to
the repository file. The tests also check every expectation in the shared catalog `examples/traditional.json` (see Examples). They
run every fixture of `conformance/operations/everyday.json`, read from the repository (each fixture is its own test,
named by its id, and one more test reports all failing ids at once), and they run the Everyday example.

## Everyday example

```
cd implementations/csharp
dotnet run --project examples/Everyday
```

`examples/Everyday` is the main example. It does everyday date/time tasks side by side in 14 sections, from "Now" to
"Round trip": first with .NET's own types (`DateOnly`, `DateTime`, `DateTimeOffset`, `TimeSpan`, `TimeZoneInfo`), then
with UTV. A note says what differs. Native output depends on the host's clock and time zone database; a missing zone
is printed and skipped. Every UTV outcome is deterministic and checked. The run ends with
`N UTV outcomes checked, all as expected.` (exit code 0). The sections live in `examples/Everyday/EverydayTour.cs`,
which is also compiled into the test project, so `dotnet test` runs the example, including once without a host tz database.

## Examples

```
cd implementations/csharp
dotnet run --project examples/Traditional
```

`examples/Traditional` is a tour of traditional date/time tasks. It first builds two dates from C# values
with `System.Text.Json.Nodes.JsonObject` (5 November 2026, and 30 February 2026, which is well-formed but does
not exist) and shows how a caller handles each status with a `switch` and reads the diagnostics. It then walks
the shared catalog [`examples/traditional.json`](../../examples/traditional.json), located relative to the
project at build time, printing each entry with its structural and Gregorian outcomes. It ends with
`37 examples, every outcome as expected.`, or with the number of mismatches and exit code 1.
The catalog checker (`examples/Traditional/TraditionalCatalog.cs`) is compiled into the test project too,
so `dotnet test` fails if the library disagrees with the catalog.
