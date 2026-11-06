# @utv/core (TypeScript)

```ts
import { CivilDate, MonthEnd } from "@utv/core";

const d = new Date(2026, 0, 31);
d.setMonth(d.getMonth() + 1);                   // Date: Tue Mar 03 2026, rolled over silently

const jan31 = CivilDate.of(2026, 1, 31);
String(jan31.addMonths(1));                     // UTV: "none (month-end)": 2026-02 has no day 31
String(jan31.addMonths(1, MonthEnd.Constrain)); // "2026-02-28", because you asked
```

```sh
npm test   # build, then every test: fixtures, typed layer, and the examples, which are unit tests
```

- **Library version:** 0.3.0
- **Specification versions read:** `utv/1.0` (result contract `utv-result/1.0`, text form `utv-text/1`). Any other
  well-formed `utv/M.N` is `unsupported`; a missing, non-string or malformed identifier is `invalid`.
- **Contracts:** [`docs/library-api.md`](../../docs/library-api.md) (typed layer, 0.3),
  [`docs/everyday-api.md`](../../docs/everyday-api.md) (JSON layer, 0.2), [`docs/text-form.md`](../../docs/text-form.md).
- **Runtime dependencies:** none. Library code never reads the clock, the host's time zone, its tz database or its locale.

## Typed API (0.3)

One immutable class per everyday value. Each is exactly one UTV value: `toString()` is the
[text form](../../docs/text-form.md), `toJSON()` the UTV JSON (so `JSON.stringify(value)` just works), and
`equals()` compares by value. Node's `console.log` shows `CivilDate(2026-11-05)`.

| Type | Build it with | Text |
|---|---|---|
| `CivilDate` | `CivilDate.of(2026, 11, 5)` | `2026-11-05` |
| `CivilYearMonth` | `CivilYearMonth.of(2026, 11)` | `2026-11` |
| `CivilTime` | `CivilTime.of(9, 30)`, `CivilTime.of(9, 30, 15, 250_000_000)` | `09:30`, `09:30:15.25` |
| `CivilDateTime` | `CivilDateTime.of(2026, 11, 5, 14, 30)`, `date.at(time)` | `2026-11-05T14:30` |
| `FixedOffsetDateTime` | `dateTime.atOffset(3600)` | `2026-11-05T14:30+01:00` |
| `ZonedIntent` | `dateTime.inZone("Europe/Paris")` | `2027-06-01T09:00[Europe/Paris]` |
| `Instant` | `Instant.fromUnixSeconds(s, ns)`, `fromUnixNanoseconds(n)`, `fromUnixMilliseconds(Date.now())`, `fromDate(d)` | `2026-11-05T13:30:00Z` |
| `Period` | `Period.days(30)`, `Period.months(1)`, `Period.of({ years, months, weeks, days, hours, minutes, seconds })` | `P30D`, `P1M`, `-P1DT2H` |
| `PosixSeconds`, `ElapsedSeconds` | `PosixSeconds.of(86_400)`, `PosixSeconds.of(1, 3)`, `ElapsedSeconds.of(86_400)` | display only: `3600.75 s (POSIX)`, `1/3 s (POSIX)`, `86400 s (SI)` |
| `Interval<CivilDate \| Instant>` | `Interval.halfOpen(a, b)`, `Interval.closed(a, b)`, `Interval.of(Bound.included(a), Bound.unknown)` | `[2026-11-01, 2026-12-01)`, `[2026-11-01, ?)` |

- **Valid by construction.** `of` throws a `RangeError` for a value that can't exist (30 February, hour 25, an
  offset beyond ±18:00, a reversed interval). `tryOf` returns an `Outcome` instead. Years are `bigint`.
- **Input never throws.** `T.parse(text)`, `T.fromJson(json)`, and the untyped `parse(text)` / `fromJson(json)`
  return an `Outcome`. The wrong kind gives `invalid` (`kind-mismatch`).
- **Comparison** is `compare(other): -1 | 0 | 1`, `isBefore`, `isAfter`, only where the order is defined:
  civil dates, year-months, times and date-times, fixed-offset date-times (by instant) and instants.
  `ZonedIntent` has none; `ctx.compare(a, b)` resolves both first and answers with an `Outcome<Ordering>`.

### Total operations return values; partial ones return `Outcome`

`date.addDays(30)` can't fail, so it returns a `CivilDate`. `date.addMonths(1)` can (31 January), so it returns an
`Outcome<CivilDate>`; `date.addMonths(1, MonthEnd.Constrain)` can't, so it returns a `CivilDate` again. Every
`Outcome` in your code marks a question time can refuse to answer.

| Type | Total | Partial (`Outcome`) |
|---|---|---|
| `CivilDate` | `addDays`, `addWeeks`, `addMonths(n, MonthEnd.Constrain)`, `addYears(n, MonthEnd.Constrain)`, `year`, `month`, `day`, `dayOfWeek` (`Weekday`, Monday = 1), `dayOfYear`, `isoWeek`, `isoWeekYear`, `daysInMonth`, `isLeapYear`, `daysUntil`, `at(time)` | `addMonths(n)`, `addYears(n)`, `add(period)` |
| `CivilYearMonth` | `addMonths`, `addYears` | |
| `CivilDateTime` | `addDays`/`Weeks`/`Hours`/`Minutes`/`Seconds` (wall clock), `addMonths(n, MonthEnd.Constrain)`, `date`, `time`, `inZone(id)`, `atOffset(s)` | `addMonths(n)`, `add(period)` |
| `FixedOffsetDateTime` | `toInstant()`, `dateTime`, `offsetSeconds` | |
| `ZonedIntent` | `addDays`/`Weeks`/`Hours`/`Minutes`/`Seconds` (wall clock; drops an asserted offset), `dateTime`, `zoneId`, `assertedOffsetSeconds` | `resolve(ctx, policy?)`, `addMonths(n)` |
| `Instant` | `add(PosixSeconds)`, `until(other)`, `atOffset(s)`, `unixSeconds`, `nanoseconds` | `inZone(id, ctx)`, `add(ElapsedSeconds)`, `toDate()` |
| `Period`, `PosixSeconds`, `ElapsedSeconds` | components, `negate()` | |
| `Interval` | `contains(point)`, `overlaps(other)` (both a `Truth`: `definitely`, `possibly`, `impossible`), `start`, `end` | |

### `Outcome<T>`

A discriminated union on `status`: `Unique` (`value`), `Multiple` (`values`, ascending), `None`, `Unsupported` and
`Invalid`. Every case carries `diagnostics` (`{ code, severity, path, message }`, with the shared codes of
library-api §5). Handle it with a `switch`; `assertNever` makes a missing case a type error:

```ts
const at: Outcome<Instant> = CivilDateTime.of(2026, 3, 8, 2, 30).inZone("America/New_York").resolve(ctx);
switch (at.status) {
  case "unique": use(at.value); break;
  case "multiple": chooseFrom(at.values); break;
  case "none": console.log(at.diagnostics[0]?.code); break; // "gap": clocks jump from 02:00 to 03:00
  case "unsupported": case "invalid": throw new Error(String(at));
  default: assertNever(at);
}
```

The helpers never pick an answer silently: `uniqueOr(fallback)`, `single()` (throws an `OutcomeError` carrying the
status and diagnostics unless the outcome is unique), `toString()` (`2026-02-28` or `none (month-end)`) and
`toResult()` (the `utv-result/1.0` envelope of the JSON layer).

### Policies and the context

Policies are typed constants, and their string literals type-check too: `MonthEnd.Reject | Constrain`,
`Gap.Reject | PreserveUnresolved | ShiftForward`, `Overlap.Reject | All | Earliest | Latest`, and
`ResolutionPolicy.Strict` (the default), `ResolutionPolicy.AllCandidates`, `ResolutionPolicy.of(gap, overlap)` or
`{ gap: Gap.ShiftForward }`.

Zone rules are an explicit input, held once by a `TemporalContext`. There is no default context.

```ts
const ctx = TemporalContext.with(ZoneRules.load("../../conformance/rules/demo-zones.json"));
meeting.resolve(ctx);                    // or ctx.resolve(meeting)
instant.inZone("America/New_York", ctx); // or ctx.inZone(instant, "America/New_York")
ctx.compare(zonedA, zonedB);             // Outcome<Ordering>: multiple if an overlap decides the order
```

`ZoneRules.load(path)` reads a rule bundle in the format of
[`conformance/rules/demo-zones.json`](../../conformance/rules/demo-zones.json); `ZoneRules.parse(text)` parses one.
The demo bundle covers three zones for 2026–2027 only; outside it, answers are `unsupported`, never extrapolated.

## JSON layer (0.2)

The typed layer is built on the JSON-level everyday API, which stays as it was. Values are plain UTV JSON, and every
operation returns a `utv-result/1.0` `UtvResult`, never throwing for a semantic outcome.

| Module | Exports |
|---|---|
| `src/builders.ts` | `date`, `yearMonth`, `time`, `dateTime`, `inZone`, `withOffset`, `instant`, `period`, `posixSeconds`, `interval` |
| `src/iso.ts`, `src/text.ts` | `parseIso`, `formatIso`; `parseText`, `formatText` (the text form, adding periods and intervals) |
| `src/ops.ts` | `fields`, `add`, `compare`, `daysBetween`, `posixSecondsBetween`, `contains`, `overlaps` |
| `src/zones.ts` | `loadRules` / `ZoneRules`, `resolve`, `toZone`, `toOffset` |
| `src/native.ts` | `fromDate` (the caller names the intent; `"local"` is refused with `local-zone`), `toDate`, `fromEpochMilliseconds`, `toEpochMilliseconds`, `fromPlainDate`, `toPlainDate`; loss is reported with `precision-loss` and `zone-dropped` warnings |
| `src/structural.ts`, `src/civil.ts` | `validateStructural`, `validateSemantic` (0.1) |

All arithmetic is exact: `bigint` years and days, rational seconds.

## Tests

```sh
npm test   # tsc -p . && node --test "dist/test/**/*.test.js"
```

`typescript` is the only devDependency. The tests read the shared files from the repository root:

| Test | Checks |
|---|---|
| `test/everyday.test.ts` | every fixture in `conformance/operations/everyday.json`: status, results and `expect.diagnostics` codes |
| `test/text-form.test.ts` | every fixture in `conformance/operations/text-form.json`, through `parseText` / `formatText` |
| `test/typed.test.ts` | the typed layer: each row of library-api §2, `parse(String(v))` and `fromJson(v.toJSON())` round trips, constructors, an exhaustiveness check that must fail to compile |
| `test/examples/utv-first/*.test.ts` | the UTV-first examples: each task done with UTV, compared with `Date`, `Intl.DateTimeFormat` and `JSON` |
| `test/examples/native-first/*.test.ts` | the native-first examples: each task done with `Date`, `Intl.DateTimeFormat` and `JSON`, compared with UTV |
| `test/examples/fixtures.test.ts` | the shared example fixture `demoZoneRules()` |
| `test/native.test.ts` | native interop, and that library sources never read the clock, host zone or locale |
| `test/conformance.test.ts`, `test/envelope.test.ts`, `test/civil.test.ts`, `test/api.test.ts` | validation and the 0.2 API |

## Examples

The examples are unit tests, so they run with `npm test` and cannot drift from what the library does. They cover the
scenario catalog in [`docs/example-tests.md`](../../docs/example-tests.md) and follow the test directives in
[`AGENTS.md`](../../AGENTS.md). There are two suites:

- **UTV first**, [`test/examples/utv-first/`](test/examples/utv-first): one file per UTV class or JSON-layer function
  (`civil-date.test.ts`, `zoned-intent.test.ts`, `validate-structural.test.ts`, …), with a `describe` per class and
  function. Each test does a task with UTV and compares the result with `Date`, `Intl.DateTimeFormat` or `JSON`. Where
  they agree, the expected value is computed with the standard API; where they differ, it is a literal and the doc
  comment says what the standard API does instead. Tests wrapped in `// BEGIN <task-id>` / `// END <task-id>` appear
  on [`docs/side-by-side.md`](../../docs/side-by-side.md).
- **Native first**, [`test/examples/native-first/`](test/examples/native-first): one file per standard object
  (`date.test.ts`, `intl-date-time-format.test.ts`, `json.test.ts`), with a `describe` per method. Each test does the
  task with the standard API and compares the result with UTV, which supplies the expected value where they agree.

UTV's zone operations use the pinned rules of
[`conformance/rules/demo-zones.json`](../../conformance/rules/demo-zones.json) through `demoZoneRules()` in
[`test/examples/fixtures.ts`](test/examples/fixtures.ts). The `Intl.DateTimeFormat` comparisons read the host's tz
database, which must know the 2026–2027 rules of New York and Paris.
