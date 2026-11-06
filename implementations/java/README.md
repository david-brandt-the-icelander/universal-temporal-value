# utv-core (Java)

```java
LocalDate.of(2026, 1, 31).plusMonths(1);                    // 2026-02-28, clamped without a word

switch (CivilDate.of(2026, 1, 31).addMonths(1)) {           // UTV: an Outcome<CivilDate>
    case Unique<CivilDate> u -> use(u.value());
    case None<CivilDate> none -> report(none.why());       // 2026-02 has no day 31 (month-end)
    case Outcome<CivilDate> other -> report(other.why());
}
CivilDate.of(2026, 1, 31).addMonths(1, MonthEnd.CONSTRAIN); // 2026-02-28, because you asked
```

```
mvn test                                  # everything: fixtures, typed API, snapshot
mvn -q test-compile exec:java@everyday    # the everyday example, java.time and UTV side by side
mvn -q test-compile exec:java             # the traditional catalog tour
```

- **Library version:** 0.3.0 (`net.icelander.utv:utv-core:0.3.0`), **Java 21+** (it was 17 up to 0.2; 0.3 uses sealed interfaces, records and pattern `switch`).
- **Specification versions read:** `utv/1.0`. Results use `utv-result/1.0`. Text form: profile `utv-text/1` ([`docs/text-form.md`](../../docs/text-form.md)).
- **Conformance profiles claimed:** none yet. The library passes every fixture in `conformance/operations/everyday.json` (status, values and diagnostic codes) and `conformance/operations/text-form.json`.
- **Registries shipped:** none. Zone rules are always loaded explicitly.

The library never reads the system clock, time zone, locale or network.

## The typed API (0.3)

The contract is [`docs/library-api.md`](../../docs/library-api.md). Everything lives in package `net.icelander.utv`.

### Values

One immutable class per everyday value. Each is exactly one UTV JSON value (`toJson()`, `fromJson(json)`), and `toString()` is the [text form](../../docs/text-form.md).

| Type | Example | Built with |
|---|---|---|
| `CivilDate` | `2026-11-05` | `CivilDate.of(2026, 11, 5)` |
| `CivilYearMonth` | `2026-11` | `CivilYearMonth.of(2026, 11)` |
| `CivilTime` | `09:30`, `09:30:15.25` | `CivilTime.of(9, 30)` |
| `CivilDateTime` | `2026-11-05T14:30` | `CivilDateTime.of(2026, 11, 5, 14, 30)`, `date.at(time)` |
| `FixedOffsetDateTime` | `2026-11-05T14:30+01:00` | `dateTime.atOffset(3600)` |
| `ZonedIntent` | `2027-06-01T09:00[Europe/Paris]` | `dateTime.inZone("Europe/Paris")` |
| `Instant` | `2026-11-05T13:30:00Z` | `Instant.fromUnixSeconds(s, nanos)`, `Instant.fromUnixNanoseconds(n)` |
| `Period` | `P1M`, `P1Y2M10DT2H30M` | `Period.months(1)`, `Period.of(y, mo, w, d, h, mi, s)` |
| `PosixSeconds` | `3600.75 s (POSIX)` | `PosixSeconds.of(n)`, `PosixSeconds.of(num, den)` |
| `ElapsedSeconds` | `86400 s (SI)` | `ElapsedSeconds.of(n)` |
| `Interval<T>` (`T` is `CivilDate` or `Instant`) | `[2026-11-01, 2026-12-01)`, `[2026-11-01, ?)` | `Interval.halfOpen(a, b)`, `Interval.closed(a, b)`, `Interval.of(Bound.included(a), Bound.unknown())` |

- **Valid by construction.** `of` throws `IllegalArgumentException` for a value that can't exist (30 February, hour 25); `tryOf` returns an `Outcome` instead. Years are `BigInteger`.
- **Value semantics.** `equals` is representation equality: `09:30` and `09:30:00` are different values (they compare as equal). `Comparable` only where order is always defined: `CivilDate`, `CivilYearMonth`, `CivilTime`, `CivilDateTime`, `FixedOffsetDateTime` (by instant) and `Instant`. `ZonedIntent` has no order of its own.
- **Parsing never throws.** `CivilDate.parse(text)`, `Instant.parse(text)` and so on return an `Outcome`; `TemporalValue.parse(text)` parses any text-form value into the sealed interface `TemporalValue`, which you take apart with a pattern `switch`.
- **Naming: `TemporalValue.parse`, not `Utv.parse`.** The contract's `Utv.parse(text)` is spelled `TemporalValue.parse(text)` in Java, because the 0.2 method `Utv.parse(String json)` (JSON with duplicate-key detection) already has that signature and keeps its behaviour.
- **`fromJson`.** Wrong kinds or forms give `invalid` (`kind-mismatch`); JSON of the wrong shape gives `invalid` (`malformed-value`, with the 0.1 structural code alongside); a valid UTV value that no typed type holds (a zoned `date`, a date-time carrying a `resolutionPolicy`) gives `unsupported` (`unsupported-operand`). The JSON layer still handles those.
- **Intervals are valid by construction.** A reversed interval is a constructor error (`reversed-interval`), and so is one with no point at all, `Interval.of(Bound.unbounded(), Bound.unknown())` (`unsupported-form`, as for the text `(.., ..)`).
- **No text form?** `PosixSeconds` and `ElapsedSeconds` print a display string (`1/3 s (POSIX)`); a value the text form can't express (a period with mixed signs) prints its JSON.

### Total operations return values; partial ones return `Outcome`

| | Always answers | Can refuse (`Outcome<…>`) |
|---|---|---|
| `CivilDate` | `addDays`, `addWeeks`, `addMonths(n, MonthEnd.CONSTRAIN)`, `addYears(n, MonthEnd.CONSTRAIN)`, `daysUntil`, `at(time)`, `year()`, `month()`, `day()`, `dayOfWeek()` (a `Weekday`), `dayOfYear()`, `isoWeek()`, `isoWeekYear()`, `daysInMonth()`, `isLeapYear()`, `compareTo` | `addMonths(n)`, `addYears(n)`, `add(Period)` |
| `CivilYearMonth` | `addMonths`, `addYears`, `compareTo` | |
| `CivilDateTime` | `addDays/Weeks/Hours/Minutes/Seconds`, `addMonths(n, MonthEnd.CONSTRAIN)`, `date()`, `time()`, `inZone(id)`, `atOffset(s)`, `compareTo` | `addMonths(n)`, `add(Period)` |
| `FixedOffsetDateTime` | `toInstant()`, `dateTime()`, `offsetSeconds()`, `compareTo` | |
| `ZonedIntent` | `addDays/Weeks/Hours/Minutes/Seconds` (wall clock; an asserted offset is dropped), `dateTime()`, `zoneId()`, `assertedOffsetSeconds()` | `resolve(ctx[, policy])`, `addMonths(n)` |
| `Instant` | `add(PosixSeconds)`, `until(other)`, `atOffset(s)`, `unixSeconds()`, `nanoseconds()` (the floor), `subSecond()` (exact, as `PosixSeconds` in [0, 1)), `compareTo` | `inZone(id, ctx)`, `add(ElapsedSeconds)` (`needs-leap-seconds`) |
| `Interval<T>` | `contains(T)` and `overlaps(Interval<T>)` give a `Truth` (`DEFINITELY`, `POSSIBLY`, `IMPOSSIBLE`); `start()`, `end()` | |

**Policy overloads** (library-api §2): `addMonths(n, policy)` and `addYears(n, policy)` are the total overloads, so only `MonthEnd.CONSTRAIN` makes sense there; passing `REJECT` is a programming error and throws `IllegalArgumentException`. The rejecting form is `addMonths(n)`, which returns an `Outcome`. `Instant.atOffset` throws `ArithmeticException` for an instant with a fraction finer than a nanosecond (the JSON layer's `implementation-limit`).

### `Outcome<T>`

A `sealed interface` with five `record` cases, each carrying `diagnostics()`:

| Case | When |
|---|---|
| `Unique<T>(T value, …)` | exactly one answer |
| `Multiple<T>(List<T> values, …)` | several, ascending (both instants of a DST overlap) |
| `None<T>` | provably no answer (31 February, a DST gap) |
| `Unsupported<T>` | this library lacks the rules or the capability |
| `Invalid<T>` | the input contradicts itself or the rules |

Helpers never pick an answer silently: `single()` (throws `OutcomeException` unless `Unique`), `tryGetUnique()` (`Optional`), `uniqueOr(fallback)`, `status()`, `why()` (`"message (code)"`), `map(f)`, `toResult()` (the 0.2 `UtvResult`). `toString()` is the value's text, or the status and codes: `none (month-end)`.

### Policies and context

- `MonthEnd { REJECT, CONSTRAIN }`, `Gap { REJECT, PRESERVE_UNRESOLVED, SHIFT_FORWARD }`, `Overlap { REJECT, ALL, EARLIEST, LATEST }`, `ResolutionPolicy(gap, overlap)` with `STRICT` (the default) and `ALL_CANDIDATES`. `zoned.resolve(ctx, Gap.SHIFT_FORWARD)` and `zoned.resolve(ctx, Overlap.EARLIEST)` are shorthands.
- `TemporalContext ctx = TemporalContext.with(ZoneRules.load(path))` holds the rules once. `ZoneRules.load` / `parse` throw `IllegalArgumentException` for a malformed bundle; `ZoneRules.tryParse` returns `invalid` (`malformed-value`) instead. It offers `resolve`, `inZone`, `compare(a, b)` (an `Outcome<Ordering>`; `Multiple` when the order depends on which side of an overlap is meant) and `rules()`. There is no default context.

### Native interop on the types

`CivilDate.from(LocalDate)` / `toLocalDate()`, `CivilTime.from(LocalTime)`, `CivilDateTime.from(LocalDateTime)`, `FixedOffsetDateTime.from(OffsetDateTime)` (the wall time at its offset) and `Instant.from(OffsetDateTime)` (the instant: the target type names the intent), `ZonedIntent.from(ZonedDateTime)` / `toZonedDateTime()`, `Instant.from(java.time.Instant)` / `toJavaInstant()`, `Period.from(java.time.Period)`, `PosixSeconds.from(Duration)` / `toDuration()`. Conversions back return an `Outcome`: `unsupported` (`out-of-native-range`) when `java.time` can't hold the value, `Unique` with a `precision-loss` or `zone-dropped` warning when it holds it lossily.

### Diagnostic codes

The codes are the shared table in library-api §5 (`syntax`, `malformed-value`, `day-out-of-range`, `month-end`, `gap`, `ambiguous`, `needs-leap-seconds`, `unknown-zone`, …). The 0.1 validation API (`Utv.validateStructural`, `GregorianCivil.validate`) keeps its structural `utv.*` codes; operations report those beside `malformed-value`.

## The everyday example

`net.icelander.utv.examples.Everyday` takes 14 sections of everyday tasks (now, a date, invalid dates, adding, days between, times, offsets, time zones, the DST gap and overlap, timestamps, durations vs periods, ranges, the native round trip). Each task is plain code: the `java.time` way, then the UTV way, with a pattern `switch` wherever UTV returns an `Outcome`. Each printed row has its output as a trailing comment, and each task sits between `// BEGIN <task-id>` and `// END <task-id>` markers.

```
mvn -q test-compile exec:java@everyday
```

- **Snapshot.** The output of one run is committed as [`src/examples/resources/everyday.expected.txt`](src/examples/resources/everyday.expected.txt). `EverydayTest` runs the example and compares every line with it, also under an unusual default zone and locale, and checks the task markers and the 100-column limit.
- **Layout.** The `row` helper aligns values in a 30-column field; a longer value (a zoned date-time) gets its note on the next line, so rows stay aligned and within 100 columns.
- **Host-dependent rows** end in ` ‡` (every line of them, when the note wraps) and are skipped by the comparison: they read the clock, the default zone or the JDK's own tz database.
- **Re-recording** after a deliberate change: `mvn -q test-compile exec:java@everyday > src/examples/resources/everyday.expected.txt`.

The UTV rows use the demo rules in [`conformance/rules/demo-zones.json`](../../conformance/rules/demo-zones.json), loaded explicitly.

## The JSON layer (0.2, unchanged)

The 0.2 API stays: it's the wire boundary, the shared fixtures test it, and the typed layer calls it underneath. Values are Jackson `JsonNode`s and every operation returns a `UtvResult` (`utv-result/1.0`: status, diagnostics, candidates). Operations never throw for semantic outcomes.

| Class | What it holds |
|---|---|
| `Values` | Builders: `date`, `yearMonth`, `time`, `dateTime`, `inZone`, `withOffset`, `instant`, `period` (plus `days(n)`, `months(n)` …), `posixSeconds`, `interval`, `unbounded()`, `unknown()`. Builders don't validate. |
| `Iso` | `parseIso`, `formatIso`, `formatOrNull` |
| `TextForm` | `parseText`, `formatText` (new in 0.3): points, periods (`P1M`, `-P1DT2H`), bracket intervals (`[a, b)`, `..`, `?`), and the EDTF slash forms on input |
| `Operations` | `fields`, `add` (with a `MonthEnd` or a JSON policy), `compare`, `daysBetween`, `posixSecondsBetween` |
| `Intervals` | `contains`, `overlaps` (three-valued) |
| `Zones` | `resolve` (with a `ResolutionPolicy` or a JSON policy), `toZone`, `toOffset` |
| `RuleBundle` | Explicit zone rules in the format of `conformance/rules/demo-zones.json` |
| `JavaTime` | Native interop with `java.time` on JSON values, returning `NativeResult<T>` |
| `Utv`, `GregorianCivil` | Structural validation (duplicate keys rejected, fail-closed on versions, kinds and properties) and proleptic Gregorian civil validation |

`JavaTime` follows the interop rules: the caller names the intent for `OffsetDateTime` (`JavaTime.As`); civil readings of `java.util.Date`, which depend on `ZoneId.systemDefault()`, are refused (`local-zone`); lossy conversions back say so; `toZonedDateTime` is the only adapter that consults the JDK's tz database, and it checks the asserted offset against it.

Structural validation is **partial**: nested value types, enums, `$ref` sub-schemas and `oneOf` alternatives are not fully enforced. Everything outside the everyday API (convert, expand, other calendars, recurrence …) returns `unsupported`.

## The traditional catalog tour

`net.icelander.utv.examples.Traditional` builds two dates in code (5 November 2026 and 30 February 2026), shows how a caller handles each validation status, then walks the shared catalog [`examples/traditional.json`](../../examples/traditional.json) and checks every outcome. It exits with status 1 if any differs.

```
mvn -q test-compile exec:java            # or exec:java@traditional
```

The example sources are in `src/examples/java`, added as a *test* source root by `build-helper-maven-plugin`, so they never ship in the library jar. That's why the commands say `test-compile`.

## Testing

```
cd implementations/java && mvn test
```

| Test | What it checks |
|---|---|
| `EverydayFixturesTest` | every fixture in `conformance/operations/everyday.json`: status, candidates and the codes in `expect.diagnostics` |
| `TextFormFixturesTest` | every fixture in `conformance/operations/text-form.json`, through `TextForm.parseText` / `formatText` |
| `TypedApiTest` | the typed layer: each row of library-api §2, outcome helpers, `toString`/`parse` and `toJson`/`fromJson` round trips, constructors rejecting invalid values, interop |
| `EverydayTest` | the example snapshot (above) |
| `ConformanceTest`, `GregorianCivilTest`, `TraditionalTest` | structural and Gregorian validation against `conformance/examples/*.json` and `examples/traditional.json` |
| `JavaTimeTest` | the JSON-layer `java.time` adapters |

All shared files are read in place from the repository root, relative to this directory.
