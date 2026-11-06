# Utv (C#)

```csharp
// .NET: 31 January + 1 month is clamped, silently.
Console.WriteLine(new DateOnly(2026, 1, 31).AddMonths(1).ToString("O"));        // 2026-02-28

// UTV: the answer may not exist, and the return type says so.
switch (CivilDate.Of(2026, 1, 31).AddMonths(1))                                // Outcome<CivilDate>
{
    case Outcome<CivilDate>.Unique(var date): Console.WriteLine(date); break;
    case Outcome<CivilDate>.None(var why): Console.WriteLine($"no such date: {why}"); break;
}                                                                               // no such date: 2026-02 has no day 31 (month-end)
Console.WriteLine(CivilDate.Of(2026, 1, 31).AddMonths(1, MonthEnd.Constrain));  // 2026-02-28, because you asked
```

```
cd implementations/csharp
dotnet run --project examples/Everyday       # 14 everyday tasks, .NET next to UTV
dotnet run --project examples/Traditional    # the shared catalog of 37 traditional tasks
dotnet test                                  # fixtures, text form, typed layer, example snapshot
```

- **Library version:** 0.3.0 (NuGet package `Utv`), .NET 8 (`net8.0`). No runtime dependencies beyond the BCL.
- **Specification versions read:** `utv/1.0`. Results use `utv-result/1.0`. Text form: profile `utv-text/1`.
- **Conformance profiles claimed:** none yet.
- **Registries shipped:** none (no tzdb, leap-second table or non-Gregorian calendars). Zone rules are passed in by the
  caller as a `ZoneRules` bundle (for example `conformance/rules/demo-zones.json`).
- Library code never reads the system clock, host time zone, locale or network. All number and string formatting uses
  `CultureInfo.InvariantCulture`, enforced by analyzer rules (CA1304/1305/1307/1309/1310/1311) set to errors in
  `src/Utv/.editorconfig`; a test scans the source for clock, host-zone and culture reads.

## The typed API (0.3)

Implements [`docs/library-api.md`](../../docs/library-api.md). Everything is in namespace `Utv` and is built on the
JSON layer below: each typed operation calls the 0.2 function and converts its result.

### Values

| Type | Example text | Built with |
|---|---|---|
| `CivilDate` | `2026-11-05` | `CivilDate.Of(2026, 11, 5)` |
| `CivilYearMonth` | `2026-11` | `CivilYearMonth.Of(2026, 11)` |
| `CivilTime` | `09:30`, `09:30:15.25` | `CivilTime.Of(9, 30)`, `CivilTime.Of(9, 30, 15, 250_000_000)` |
| `CivilDateTime` | `2026-11-05T14:30` | `CivilDateTime.Of(2026, 11, 5, 14, 30)`, `date.At(time)` |
| `FixedOffsetDateTime` | `2026-11-05T14:30+01:00` | `dateTime.AtOffset(3600)` |
| `ZonedIntent` | `2027-06-01T09:00[Europe/Paris]` | `dateTime.InZone("Europe/Paris")` |
| `Instant` | `2026-11-05T13:30:00Z` | `Instant.FromUnixSeconds(s, nanos)`, `Instant.FromUnixNanoseconds(n)` |
| `Period` | `P1M`, `PT2H`, `-P1D` | `Period.Months(1)`, `Period.Days(30)`, `Period.Of(years:, months:, …)` |
| `PosixSeconds` | `3600.75 s (POSIX)` (display only) | `PosixSeconds.Of(n)`, `PosixSeconds.Of(num, den)` |
| `ElapsedSeconds` | `86400 s (SI)` (display only) | `ElapsedSeconds.Of(n)` |
| `Interval<T>` (`T` = `CivilDate` or `Instant`) | `[2026-11-01, 2026-12-01)`, `[2026-11-01, ..)`, `[2026-11-01, ?)` | `Interval.HalfOpen(a, b)`, `Interval.Closed(a, b)`, `Interval.Of(Bound.Included(a), Bound.Unknown)` |

- **Valid by construction.** `Of` throws `ArgumentOutOfRangeException` (naming the field) for 30 February, hour 25 and
  the like; `TryOf` returns an `Outcome` instead. `Interval.Of` throws `ArgumentException` for a reversed interval
  (`reversed-interval`) or one with no point (`unsupported-form`). Years are `BigInteger`.
- **Values.** Immutable `sealed record`s with representation equality (`09:30` ≠ `09:30:00`, though they compare
  equal) and `[DebuggerDisplay]` such as `CivilDate(2026-11-05)`.
- **Text and JSON.** `ToString()` is the text form; `T.Parse(text)`, `T.FromJson(json)` and `ToJson()` return or take
  outcomes and `JsonElement`s. `Temporal.Parse(text)` and `Temporal.FromJson(json)` read any of them as an
  `ITemporalValue` to `switch` on (the contract's `Utv.Parse`: `Utv` is the namespace in C#). Wrong kinds are
  `invalid` (`kind-mismatch`); valid values no type holds (a zoned date, extra properties) are `unsupported`
  (`unsupported-operand`).
- **Interop**, where the target type names the intent: `CivilDate.FromDateOnly`, `FixedOffsetDateTime.FromDateTimeOffset`,
  `Instant.FromDateTimeOffset`, `CivilDateTime.FromDateTime` / `Instant.FromDateTime` (a `Kind.Local` value is
  `unsupported`, `local-zone`), and `ToDateOnly()`, `ToDateTimeOffset()`, … returning outcomes.

### Total operations return values; partial ones return `Outcome<T>`

| Type | Total | Partial (`Outcome<…>`) |
|---|---|---|
| `CivilDate` | `AddDays`, `AddWeeks`, `AddMonths(n, MonthEnd.Constrain)`, `AddYears(n, MonthEnd.Constrain)`, `DaysUntil`, `At`, `DayOfWeek` (`Weekday`, Monday = 1), `DayOfYear`, `IsoWeek`, `IsoWeekYear`, `DaysInMonth`, `IsLeapYear`, `<` … | `AddMonths(n)`, `AddYears(n)`, `Add(Period)` |
| `CivilYearMonth` | `AddMonths`, `AddYears`, `<` … | |
| `CivilTime` | `<` … (no arithmetic: wrapping past midnight loses the day) | |
| `CivilDateTime` | `AddDays/Weeks/Hours/Minutes/Seconds`, `AddMonths(n, MonthEnd.Constrain)`, `Date`, `Time`, `InZone`, `AtOffset`, `<` … | `AddMonths(n)`, `Add(Period)` |
| `FixedOffsetDateTime` | `ToInstant()`, `DateTime`, `OffsetSeconds`, `<` … (by instant) | |
| `ZonedIntent` | `AddDays/Weeks/Hours/Minutes/Seconds` (wall clock), `DateTime`, `ZoneId`, `AssertedOffsetSeconds` | `Resolve(ctx[, policy])`, `AddMonths(n)`; no `<`: use `ctx.Compare` |
| `Instant` | `Add(PosixSeconds)`, `+`, `Until`, `AtOffset`, `UnixSeconds`, `Nanoseconds`, `SubSecond`, `<` … | `InZone(id, ctx)`, `Add(ElapsedSeconds)` |
| `Period`, `PosixSeconds`, `ElapsedSeconds` | components, `Negate()`, equality | |
| `Interval<T>` | `Contains`, `Overlaps` (a `Truth`: `Definitely`, `Possibly`, `Impossible`), `Start`, `End` | |

The overloads that take a `MonthEnd` always answer, so they accept only `MonthEnd.Constrain`; `MonthEnd.Reject` throws
`ArgumentException` (the rejecting form is the overload without a policy).

### Outcomes

`Outcome<T>` is an `abstract record` with nested `sealed record`s `Unique(T Value)`, `Multiple(IReadOnlyList<T> Values)`
(ascending), `None`, `Unsupported` and `Invalid`. Every case has `Diagnostics` (codes from library-api §5, with
`Codes`, `Has(code)` and readable text) and `Status`. Helpers never pick an answer silently: `TryGetUnique(out v)`,
`UniqueOr(fallback)`, `Single()` (throws `OutcomeException` for anything but `Unique`), and `ToResult()` back to the
`utv-result/1.0` envelope.

The case names are nested, so `Outcome<CivilDate>.None(var why)` works anywhere. To keep call sites short:

```csharp
using DateOutcome = Utv.Outcome<Utv.CivilDate>;   // an alias per result type: DateOutcome.None(var why)
using static Utv.Outcome<Utv.Instant>;            // or, in a file handling one result type: None(var why)
```

The example uses aliases. C# doesn't know the five cases are all there is, so a `switch` expression needs a final
`_ =>` arm.

### Policies and context

```csharp
var ctx = TemporalContext.With(ZoneRules.Load("conformance/rules/demo-zones.json"));
Outcome<Instant> start = meeting.Resolve(ctx);                    // ResolutionPolicy.Strict: gap → None, overlap → Multiple
Instant shifted = springForward.Resolve(ctx, Gap.ShiftForward).Single();
Instant first = fallBack.Resolve(ctx, Overlap.Earliest).Single();
Outcome<Ordering> order = ctx.Compare(a, b);                      // zoned values: Multiple if the overlap decides
```

Policies are enums: `MonthEnd { Reject, Constrain }`, `Gap { Reject, PreserveUnresolved, ShiftForward }`,
`Overlap { Reject, All, Earliest, Latest }`, combined as `new ResolutionPolicy(Gap.ShiftForward, Overlap.Earliest)`
or the presets `ResolutionPolicy.Strict` and `ResolutionPolicy.AllCandidates`. There is no default context.

## The JSON layer (0.1 and 0.2)

### Validation (0.1)

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

### Everyday API (0.2) and text form

Implements [`docs/everyday-api.md`](../../docs/everyday-api.md), including its Clarifications, and the text form of
[`docs/text-form.md`](../../docs/text-form.md). Values are UTV JSON held in `System.Text.Json.JsonElement`. Every
operation returns a `UtvResult` and never throws for a semantic outcome. Every fixture in
`conformance/operations/everyday.json` and `conformance/operations/text-form.json` passes, diagnostic codes included.

| Class (namespace `Utv`) | Members |
|---|---|
| `Build` | `Date`, `YearMonth`, `Time`, `DateTime`, `InZone`, `WithOffset`, `Instant`, `Period` (named args: `Build.Period(months: 1)`), `PosixSeconds`, `Interval` (with `IntervalBound.Included/Excluded/Unbounded/Unknown`) |
| `Iso` | `ParseIso(string)`, `FormatIso(value)` |
| `TextForm` | `Parse(string)`, `Format(value)`: the [text form](../../docs/text-form.md) (operations `parseText`, `formatText`) |
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
  - `parseIso` gives `unsupported` for other valid ISO 8601 forms: a decimal comma, a year on its own, a time with `T`
    or an offset, and basic-format offsets (`unsupported-form`), and more than nine fraction digits (`implementation-limit`).
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
- **Text form.** `TextForm.Format` refuses (`unsupported`, `no-text-form`) any value whose text would not read back as
  the same JSON, such as a value with extra properties. An interval with no point (`(.., ..)`) parses as
  `unsupported` (`unsupported-form`).
- **Diagnostic codes** follow the shared table in library-api §5. Wrongly shaped JSON gives `malformed-value`, with the
  0.1 structural codes (`missing-property`, `wrong-type`, `duplicate-key`, …) alongside. Codes outside the table:
  the 0.1 validation codes, and the `offset-not-known` note on an instant converted to `DateTimeOffset`.

## What is not implemented

Everything outside the everyday API: convert, expand, equivalent and the other operations of `docs/operations.md`; canonical JSON
round-tripping and writing; other calendars, zones, recurrences; semantic validation of any kind other than
Gregorian `civil` dates; result `inputDigest`/`contextDigest`. The conformance scenarios
(`conformance/scenarios/*.json`) are not executable yet (OI-12) and are not run. When a `oneOf` fails (for example
an interval boundary holding a bad nested value), only a `schema-violation` at the `oneOf` location is reported,
not the failures inside each alternative.

## Tests

`dotnet test` reads the shared material from the repository root (resolved relative to the test project at build time):

- the schema (the embedded copy must equal it), `conformance/examples/*.json`, and the catalog `examples/traditional.json`;
- every fixture of `conformance/operations/everyday.json`, checking status, candidates and `expect.diagnostics`
  (each fixture is its own test, named by its id, and one more test reports all failures at once);
- every fixture of `conformance/operations/text-form.json` through `TextForm.Parse` / `TextForm.Format`;
- the typed layer (`TypedApiTests`: each row of library-api §2, `ToString`/`Parse` and `ToJson`/`FromJson` round
  trips, constructors, outcomes, policies, interop);
- the **snapshot** of the Everyday example (`EverydayExampleTests`): it runs the example in-process and compares every
  line with `examples/Everyday/everyday.expected.txt`, skipping lines that end in ` ‡` (they depend on this machine's
  clock or time zone database). It also checks that lines stay within 100 columns and that every task is marked.
  To record a new snapshot: `dotnet run --project examples/Everyday > examples/Everyday/everyday.expected.txt`.

## Examples

`examples/Everyday` (`Program.cs`) is the main example: 14 sections of everyday tasks, each written twice, first with
.NET's own types (`DateOnly`, `DateTime`, `DateTimeOffset`, `TimeOnly`, `TimeZoneInfo`), then with UTV. It is plain
top-level code: each task sits between `// BEGIN <task-id>` and `// END <task-id>` markers (they feed
`docs/side-by-side.md`), the month-end and DST tasks `switch` on the outcome, and every printed row has its output as
a trailing comment. The demo zone rules are linked from `conformance/rules/demo-zones.json` and copied next to the
program at build time.

`examples/Traditional` is a tour of the shared catalog [`examples/traditional.json`](../../examples/traditional.json)
at the JSON layer. It ends with `37 examples, every outcome as expected.` (exit code 0), and `dotnet test` checks the
catalog too.
