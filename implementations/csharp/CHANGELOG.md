# Changelog

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
