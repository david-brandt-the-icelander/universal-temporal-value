# utv-core (Java)

- **Library version:** 0.2.0 (`net.icelander.utv:utv-core:0.2.0`), Java 17+
- **Specification versions read:** `utv/1.0`. Results use `utv-result/1.0`. Writing is not implemented yet.
- **Conformance profiles claimed:** none yet. The everyday API (`docs/everyday-api.md`) passes every fixture in `conformance/operations/everyday.json`.
- **Registries shipped:** none.

## What is implemented

- `Utv.validateStructural(String)` parses JSON with Jackson `STRICT_DUPLICATE_DETECTION`, so duplicate keys are rejected. It then checks the envelope:
  - `schema` must be exactly `utv/1.0`. Any other well-formed `utv/M.N` (matching `^utv/(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)$`, including newer minors and other majors such as `utv/2.0`) gives `unsupported`. A missing, non-string or malformed identifier (for example `utv/4` or `utv/1`) gives `invalid`.
  - Unknown top-level properties and any `kind` outside the 26 core kinds give `invalid`.
  - For each kind, it checks the value properties the schema requires and rejects unknown ones (generated from `schema/utv-1.0.schema.json`). It also checks the `interval` construction requirements and the kind of `series` samples.
- `GregorianCivil.validate(String)` runs structural validation and then checks proleptic Gregorian civil `year`, `year-month`, `date` and `datetime` values: month and day ranges, the leap-year rule (Feb 29 only in leap years), and time-of-day ranges. Years are arbitrary-precision integer strings.
- `UtvResult` holds a `utv-result/1.0` result with a `Status` (`invalid`, `unsupported`, `indeterminate`, `none`, `unique`, `multiple`, `range`, `symbolic`, `resource-limit`, `error`, `valid`) and `Diagnostic`s (`{code, severity, path, message}`).

Structural validation is **partial**. It does not yet enforce nested value types, enums, `$ref` sub-schemas or `oneOf` alternatives beyond the rules listed above.

### Everyday API (0.2)

The contract is [`docs/everyday-api.md`](../../docs/everyday-api.md). Values stay Jackson `JsonNode`s, and every operation returns a `UtvResult`. Results carry `candidates`: one for `unique`, all of them in ascending order for `multiple`, and none for any other status. `UtvResult.value()` returns the single candidate. Operations never throw for semantic outcomes.

| Class | What it holds |
|---|---|
| `Values` | Builders: `date`, `yearMonth`, `time`, `dateTime`, `inZone`, `withOffset`, `instant`, `period` (plus `days(n)`, `months(n)`, `hours(n)` and so on), `posixSeconds`, `interval`, and the `unbounded()` and `unknown()` endpoints. Builders don't validate: `date(2026, 2, 30)` builds, and the operations then say `invalid`. |
| `Iso` | `parseIso` and `formatIso`, plus `formatOrNull` for display |
| `Operations` | `fields`, `add` (with `MonthEnd.REJECT` by default, or `MonthEnd.CONSTRAIN`), `compare`, `daysBetween` and `posixSecondsBetween` |
| `Intervals` | `contains` and `overlaps`, with three-valued answers |
| `Zones` | `resolve` (taking a `ResolutionPolicy` with `Gap` and `Overlap` enums), `toZone` and `toOffset` |
| `RuleBundle` | Explicit zone rules in the format of `conformance/rules/demo-zones.json`. The library has no built-in rules and never reads the host's zone. |
| `JavaTime` | Native interop with `java.time`, with conversions back returning `NativeResult<T>` (`status`, `value`, `lossy`, `diagnostics`) |

`add`, `resolve` and the other operations also take their policy as JSON (`{"monthEnd": …}`, `{"gap": …, "overlap": …}`), as the fixtures do. An unknown policy value gives `unsupported`. The everyday contract's clarifications are implemented. `add` drops `offsetAssertion` and emits the `offset-assertion-dropped` diagnostic. `resolve` filters candidates by `offsetAssertion` and returns `invalid`/`offset-mismatch` when none matches. Every annotation other than a single `[Area/Location]` gives `unsupported`. `daysBetween`, `contains` and `overlaps` require dates that are all unzoned or all in one zone. `operation` holds the everyday operation's name.

**Native interop (`JavaTime`).**

- **Conversions to UTV.**
  - `LocalDate`, `LocalTime` and `Instant` convert directly.
  - `LocalDateTime` becomes a floating date-time.
  - `OffsetDateTime` converts with the caller naming the intent: `JavaTime.As.INSTANT`, `As.OFFSET_DATE_TIME` or `As.LOCAL_DATE_TIME`.
  - `ZonedDateTime` becomes a zoned intention (`inZone`) that keeps its offset as `offsetAssertion`. A zone that is only an offset becomes a fixed-offset date-time instead.
  - `Period` becomes a calendar amount, and `Duration` becomes `posixSeconds`.
- **Refused: anything that depends on `ZoneId.systemDefault()`.** `JavaTime.fromDate(java.util.Date, As)` accepts only `As.INSTANT`, because a `Date`'s date and time fields come from the host's default zone. Every civil reading gives `unsupported`.
- **Conversions back.**
  - **Methods:** `toLocalDate`, `toLocalTime`, `toLocalDateTime`, `toOffsetDateTime`, `toZonedDateTime`, `toInstant`, `toPeriod` and `toDuration`.
  - **Reported as lossy** (`lossy() == true`, with diagnostic `utv.loss`):
    - a zoned value converted to `OffsetDateTime` or `LocalDateTime`, which drops the zone;
    - precision finer than a nanosecond, which is truncated.
  - **Refused:**
    - years outside `java.time`'s range;
    - second 60;
    - a zoned intention with no `offsetAssertion`, which you should resolve first;
    - hours, minutes or seconds converted to a `Period`;
    - SI `elapsed` amounts.
  - **`toZonedDateTime`** is the only adapter that consults the JDK's bundled tz database. It checks the asserted offset with `ZonedDateTime.ofStrict`, and gives `invalid` if the offset disagrees.

Everything else (convert, expand, other calendars, recurrence and so on) is not implemented and returns `unsupported`. `GregorianCivil.validate` returns `unsupported` for any value that is not a proleptic Gregorian civil year, year-month, date or datetime.

The library never reads the system clock, time zone, locale or network.

## Everyday example

`net.icelander.utv.examples.Everyday` is the main example. It takes 14 everyday tasks (now, a date, invalid dates, adding, days between, times, offsets, time zones, the DST gap and overlap, timestamps, durations vs periods, ranges and the native round trip) and shows each one twice: first with `java.time`, then with UTV. It prints what each side gives, plus a note where they differ. Run it from this directory:

```
mvn -q test-compile exec:java@everyday
```

The native lines can depend on the host: the clock, the default zone and the JDK's tz database. Where they do, the output says so. The UTV lines are deterministic. They use the demo rules in [`conformance/rules/demo-zones.json`](../../conformance/rules/demo-zones.json), loaded explicitly, and the example exits with status 1 if any UTV outcome differs from what it expects. `EverydayTest` runs it under `mvn test` twice, the second time with an unusual default zone and locale.

## Examples

`net.icelander.utv.examples.Traditional` tours everyday date and time tasks. It builds two dates in code (5 November 2026 and 30 February 2026) and shows how a caller handles each status. Then it walks the shared catalog [`examples/traditional.json`](../../examples/traditional.json), validates every entry and checks the outcome against the catalog. It exits with status 1 if any outcome differs. Run it from this directory:

```
mvn -q test-compile exec:java            # or exec:java@traditional
```

The example source is in `src/examples/java`. `build-helper-maven-plugin` adds it as a *test* source root, so it never ships in the library jar, and the tests reuse it. That's why the command is `test-compile`, not `compile`.


## Testing

```
cd implementations/java && mvn test
```

`EverydayFixturesTest` is a JUnit 5 dynamic test. It reads `conformance/operations/everyday.json` and the rule bundles that file names in place in the repository, and runs every fixture as one test named by its id. Results are compared as JSON values. `JavaTimeTest` covers the native adapters.

The tests also read `conformance/examples/valid.json`, `conformance/examples/invalid-structural.json` and `examples/traditional.json` from the repository root, relative to this directory. `TraditionalTest` checks every catalog expectation with the example's own logic, so it fails on the same two entries as the example.
