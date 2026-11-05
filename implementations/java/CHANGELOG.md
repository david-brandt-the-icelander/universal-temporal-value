# Changelog

## 0.2.0

- **Everyday API** (`docs/everyday-api.md`). The library passes every fixture in `conformance/operations/everyday.json`, all 128.
  - **Builders:** new class `Values`.
  - **ISO text:** new class `Iso`, with `parseIso` and `formatIso`.
  - **Operations:** new class `Operations`, with `fields`, `add`, `compare`, `daysBetween` and `posixSecondsBetween`.
  - **Intervals:** new class `Intervals`, with `contains` and `overlaps`.
  - **Zones:** new class `Zones`, with `resolve`, `toZone` and `toOffset`, plus `RuleBundle` and `ResolutionPolicy`.
- The contract's clarifications are implemented:
  - `add` drops `offsetAssertion`, with the `offset-assertion-dropped` diagnostic;
  - `resolve` checks `offsetAssertion` and returns `invalid`/`offset-mismatch` when it fails;
  - annotations other than one `[Area/Location]` give `unsupported`;
  - zones must match in `daysBetween`, `contains` and `overlaps`.
- `UtvResult` gains `candidates` (and `value()` and `toJson()`). The three-argument constructor is kept.
- Native interop with `java.time` is in `JavaTime` and `NativeResult`.
  - **Intent:** the caller names it for `OffsetDateTime` and `java.util.Date`.
  - **Local zone:** civil readings of `java.util.Date`, which depend on `ZoneId.systemDefault()`, are refused.
  - **Lossy conversions back:** reported, or refused.
- New side-by-side example `net.icelander.utv.examples.Everyday` (`mvn -q test-compile exec:java@everyday`). `EverydayTest` runs it.
- `mvn test` now runs the shared everyday fixtures (`EverydayFixturesTest`, a JUnit 5 dynamic test) and the interop tests (`JavaTimeTest`).
- The traditional tour also runs as `exec:java@traditional`.

### Also in 0.2.0 (previously listed as Unreleased)

- Added the runnable example `net.icelander.utv.examples.Traditional` (`mvn -q test-compile exec:java`). It tours the shared catalog `examples/traditional.json`. The example lives in `src/examples/java` as a test source root, so it isn't in the library jar.
- `mvn test` now checks every expectation in `examples/traditional.json` (`TraditionalTest`).
- `GregorianCivil.validate` now also checks the `year` and `year-month` forms (year must be an integer string, month 1..12) instead of returning `unsupported`, matching the other libraries and the shared catalog.

## 0.1.0

- First increment: envelope and per-kind structural validation for `utv/1.0`, the `utv-result/1.0` status and diagnostic types, and proleptic Gregorian civil date validation.
