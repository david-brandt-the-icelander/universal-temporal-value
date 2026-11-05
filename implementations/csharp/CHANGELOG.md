# Changelog

## 0.3.0

Implements [`docs/library-api.md`](../../docs/library-api.md) (typed values, outcomes, one vocabulary) and
[`docs/text-form.md`](../../docs/text-form.md). The 0.2 JSON layer keeps working, apart from the diagnostic codes below.

- **Typed layer** (namespace `Utv`), built on the 0.2 functions:
  - `CivilDate`, `CivilYearMonth`, `CivilTime`, `CivilDateTime`, `FixedOffsetDateTime`, `ZonedIntent`, `Instant`,
    `Period`, `PosixSeconds`, `ElapsedSeconds` and `Interval<T>` (with `Bound<T>`, `Bound.Included/Excluded/Unbounded/Unknown`).
    They are `sealed record`s, valid by construction (`Of` throws `ArgumentOutOfRangeException`, `TryOf` returns an
    outcome), compare by representation, and carry `[DebuggerDisplay]`.
  - `ToString()` gives the text form. `Parse`, `FromJson` and `ToJson` are on every type; `Temporal.Parse` and
    `Temporal.FromJson` read any of them as an `ITemporalValue`.
  - Total operations return values (`date.AddDays(30)`); partial ones return `Outcome<T>`
    (`date.AddMonths(1)`, `zoned.Resolve(ctx)`). `IComparable<T>` and `<`/`>`/`<=`/`>=` exist only where the order
    needs no rules.
  - `Outcome<T>`: an `abstract record` with nested `Unique`, `Multiple`, `None`, `Unsupported` and `Invalid`, plus
    `TryGetUnique`, `UniqueOr`, `Single` (throws `OutcomeException`), `Status`, `Diagnostics` and `ToResult()`.
    `Diagnostics` is a list with `Codes`, `Has(code)` and readable text.
  - Policies `MonthEnd`, `Gap` and `Overlap`; `ResolutionPolicy(Gap, Overlap)`, `ResolutionPolicy.Strict` and
    `ResolutionPolicy.AllCandidates`. `AddMonths(n, MonthEnd)` and `AddYears(n, MonthEnd)` take only `Constrain`.
  - `TemporalContext.With(rules)` with `Resolve`, `InZone` and `Compare` (zoned values, `Outcome<Ordering>`);
    `ZoneRules.Load(path)`.
  - `Instant.FromUnixNanoseconds`, `Instant.SubSecond`, and typed .NET interop (`FromDateOnly`, `FromDateTimeOffset`,
    `ToDateTimeOffset`, …) where the target type names the intent.
- **Text form:** `TextForm.Parse` and `TextForm.Format` (operations `parseText` and `formatText`) in the JSON layer:
  ISO points, periods, bracket intervals, EDTF slash intervals as input. `formatText` refuses anything that would not
  read back as the same value.
- **Diagnostic codes** now follow the shared table (library-api §5). **This changes 0.2 codes**, for example
  `not-iso` → `syntax`, `minute-out-of-range` → `field-out-of-range`, `week-date`/`basic-format` → `unsupported-form`,
  `incomparable`/`time-wraps`/`mixed-domains` → `unsupported-operand`, `resolve-first` → `needs-zone-rules`,
  `mixed-zones` → `different-zones`, `outside-rules` → `outside-rule-window`, `wrong-component`/`calendar-on-instant`
  → `kind-mismatch`, and wrongly shaped JSON → `malformed-value` (alongside the 0.1 structural codes). Gap and overlap
  messages now name the transition (`clocks jump from 02:00 to 03:00 in America/New_York`).
- `Diagnostic.ToString()` gives `message (code)`.
- **Tests:** the fixture runner checks `expect.diagnostics`; all of `conformance/operations/text-form.json`; typed-layer
  tests; a snapshot test of the Everyday example.
- **Example:** `examples/Everyday` rewritten as plain top-level code with task markers, `switch`es on outcomes,
  aligned rows and output comments, checked against `examples/Everyday/everyday.expected.txt`.

## 0.2.0

- **Everyday API** (`docs/everyday-api.md`, including its Clarifications), in namespace `Utv`:
  - `Build`: builders for the canonical shapes, plus `IntervalBound`.
  - `Iso.ParseIso` and `Iso.FormatIso`.
  - `Calendar`: `Fields`, `Add`, `Compare`, `DaysBetween` and `PosixSecondsBetween`.
  - `Ranges`: `Contains` and `Overlaps`, with three-valued answers.
  - `Zones`: `Resolve`, `ToZone` and `ToOffset`, with caller-supplied `ZoneRules`.
  - Policies: `MonthEndPolicy`, `GapPolicy`, `OverlapPolicy` and `ResolutionPolicy`.
- **Native interop** (`Interop`, `NativeResult<T>`, `DateTimeOffsetIntent`) for `DateOnly`, `TimeOnly`, `DateTime`,
  `DateTimeOffset` and `TimeSpan`:
  - the caller names the intent;
  - `DateTimeKind.Local` is refused;
  - lossy or out-of-range conversions back to .NET say so.
- **`UtvResult`:** candidates may now be plain JSON answers (strings, numbers, objects). New accessors: `IsUnique`, `Value`, `TryGetValue`,
  `GetString` and `GetInt64`. Existing behaviour is unchanged.
- **Tests:**
  - every fixture of `conformance/operations/everyday.json` (read from the repository);
  - interop and edge-case unit tests;
  - a scan that the library never reads the clock, host zone or culture;
  - a run of the Everyday example.
- **Example:** added `examples/Everyday` (`dotnet run --project examples/Everyday`), a side-by-side tour of .NET and UTV.

## Unreleased (0.1.x)

- Added `examples/Traditional`, a runnable tour of traditional date/time tasks
  (`dotnet run --project examples/Traditional`) that checks every entry of the shared catalog
  `examples/traditional.json`. `dotnet test` now checks the catalog as well. No library changes.

## 0.1.0

- First increment: strict JSON parsing with duplicate-key detection at every depth, `utv/1.0` envelope and
  full structural validation against the embedded `schema/utv-1.0.schema.json`, the `utv-result/1.0` result
  type (`UtvStatus`, `Diagnostic`, `UtvResult`), and proleptic Gregorian `civil` date validation.
