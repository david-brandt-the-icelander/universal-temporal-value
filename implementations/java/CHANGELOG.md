# Changelog

## Unreleased

- **Examples are unit tests** ([`docs/example-tests.md`](../../docs/example-tests.md), test style in [`AGENTS.md`](../../AGENTS.md)). Two suites run with `mvn test`:
  - **UTV first** (`src/test/java/net/icelander/utv/examples/utvfirst/`): one test class per UTV class, one `@Nested` class per method. Each test does a task with UTV and compares the result with `java.time`; tests for the tasks of `docs/side-by-side.md` sit between `// BEGIN` / `// END` markers.
  - **Native first** (`src/test/java/net/icelander/utv/examples/nativefirst/`): the same scenarios organized by `java.time` package, class and method (`time/LocalDateTest`, `time/temporal/IsoFieldsTest`, `time/chrono/HijrahDateTest`, `time/zone/ZoneRulesTest` …), compared with UTV.
  - A shared fixture, `Fixtures.demoZoneRules()`, loads `conformance/rules/demo-zones.json`, and has a test of its own.
- **Removed** the example programs `Everyday` and `Traditional` (`src/examples/`), the `everyday.expected.txt` snapshot and `EverydayTest`, the tour of `examples/traditional.json` and `TraditionalTest`, and the `build-helper-maven-plugin` and `exec-maven-plugin` configuration that served them. The library itself is unchanged.

## 0.3.0

- **Java 21.** The build moves from `maven.compiler.release` 17 to **21**, so `Outcome` can be a `sealed interface` with `record` cases taken apart with pattern `switch`. Java 21 or later is now required.
- **Typed layer** (`docs/library-api.md`), built on the 0.2 JSON functions:
  - **Values:** `CivilDate`, `CivilYearMonth`, `CivilTime`, `CivilDateTime`, `FixedOffsetDateTime`, `ZonedIntent`, `Instant`, `Period`, `PosixSeconds`, `ElapsedSeconds`, `Interval<T>` with `Bound<T>`, under the sealed interface `TemporalValue`. Valid by construction (`of` throws `IllegalArgumentException`, `tryOf` returns an `Outcome`), immutable, with value equality; `toString()` is the text form; `toJson()` / `fromJson(json)` and `parse(text)` at the edges.
  - **Total vs partial:** operations that always answer return plain values (`addDays`, `daysUntil`, `addMonths(n, MonthEnd.CONSTRAIN)`, `Instant.until` …); only partial ones return `Outcome<T>` (`addMonths(n)`, `resolve`, `inZone`, parsing).
  - **`Outcome<T>`:** `Unique`, `Multiple`, `None`, `Unsupported`, `Invalid`, each with diagnostics; helpers `single()` (throws `OutcomeException`), `tryGetUnique()`, `uniqueOr`, `status()`, `why()`, `map`, `toResult()`.
  - **Policies and context:** top-level `MonthEnd`, `Gap`, `Overlap` enums; `ResolutionPolicy.STRICT` and `ALL_CANDIDATES`; `ZoneRules.load` / `parse`; `TemporalContext.with(rules)` with `resolve`, `inZone` and `compare` (an `Outcome<Ordering>`).
  - `Weekday`, `Truth` and `Ordering` enums.
  - Typed `java.time` adapters: `CivilDate.from(LocalDate)` / `toLocalDate()`, `ZonedIntent.from(ZonedDateTime)` / `toZonedDateTime()`, `Instant.from(...)` / `toJavaInstant()` and so on.
- **Text form** (`docs/text-form.md`): new class `TextForm` with `parseText` and `formatText` in the JSON layer. Passes all of `conformance/operations/text-form.json`.
- **Settled after review** (library-api §1, §5):
  - new code `malformed-value` (invalid) for JSON of the wrong shape: wrong types, missing or unexpected fields, non-integers, a malformed rules bundle (`ZoneRules.tryParse`). 0.1 structural codes appear alongside it;
  - a `[zone]` after `Z` or `-00:00` gives `unsupported-annotation`;
  - `fromJson` of a valid value no typed type holds (a zoned `date`, a date-time with a `resolutionPolicy`) gives `unsupported` (`unsupported-operand`);
  - intervals with no point at all are constructor errors (`unsupported-form`, as for the text `(.., ..)`), like reversed ones (`reversed-interval`);
  - `Instant.subSecond()` gives the exact sub-second part; `nanoseconds()` is its floor;
  - the example's `row` helper puts the note on the next line when a value overflows its column.
- **Diagnostic codes** follow the shared table in library-api §5: for example `utv.civil.day` is now `day-out-of-range`, `utv.iso.syntax` is `syntax`, `utv.rules.zone` is `unknown-zone`, `utv.loss` is `zone-dropped` or `precision-loss`. Codes for structural validation keep their `utv.*` names. Some messages were reworded (`2026-02 has no day 31`).
- **Fixture runner** checks `expect.diagnostics` codes as well as statuses and values, for all 130 everyday fixtures.
- **Source changes to the 0.2 API** (behaviour is unchanged):
  - `ResolutionPolicy` now holds the top-level `Gap` and `Overlap` enums; `ResolutionPolicy.Gap.SHIFT_FORWARD_BY_GAP` is now `Gap.SHIFT_FORWARD` (still `"shift-forward-by-gap"` on the wire). `ResolutionPolicy.REJECT` is deprecated in favour of `STRICT`.
  - `Operations.MonthEnd` is deprecated in favour of the top-level `MonthEnd`; `Operations.add(…, Operations.MonthEnd)` still works.
- **Everyday example rewritten** as plain code (library-api §7): one method per task between `// BEGIN` / `// END` markers, `java.time` and UTV rows aligned by a single `row` helper, real pattern `switch`es on outcomes, output as trailing comments, and ` ‡` on host-dependent rows. Its output is committed as `src/examples/resources/everyday.expected.txt`; `EverydayTest` is now a snapshot test (also run under an unusual default zone and locale).
- The README opens with a before/after snippet and describes the typed API.

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
