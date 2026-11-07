# Library API 0.3: typed values, outcomes, one vocabulary

This is the contract for version 0.3 of every UTV library. It implements recommendations R1–R4 and R6–R9 of [`aesthetics-review.md`](aesthetics-review.md). [`text-form.md`](text-form.md) covers R5.

0.3 adds a **typed layer** on top of the JSON-level everyday API from 0.2 ([`everyday-api.md`](everyday-api.md)). The JSON layer stays: it's the wire boundary, and the shared fixtures keep testing it. The typed layer is what the README and the example tests show.

Where this document says "C#", the same rule applies in every language, in that language's idiom and casing. The [naming table](#naming) gives the exact spelling for each language.

## 1. Types

One immutable type per everyday value. Each one is exactly one UTV value, so you can always convert to and from JSON.

| Type | Wraps (UTV JSON) | Constructed by |
|---|---|---|
| `CivilDate` | civil `date`, Gregorian proleptic | `CivilDate.Of(year, month, day)` |
| `CivilYearMonth` | civil `year-month` | `CivilYearMonth.Of(year, month)` |
| `CivilTime` | civil `time` | `CivilTime.Of(hour, minute[, second[, nanosecond]])` |
| `CivilDateTime` | civil `datetime`, no zone | `CivilDateTime.Of(y, mo, d, h, mi[, s[, ns]])`, `date.At(time)` |
| `FixedOffsetDateTime` | civil `datetime` + `fixed-offset` zone | `dateTime.AtOffset(seconds)` |
| `ZonedIntent` | civil `datetime` + IANA zone + `follow-zone-id` (+ optional asserted offset) | `dateTime.InZone("Europe/Paris")` |
| `Instant` | POSIX instant | `Instant.FromUnixSeconds(s[, nanos])`, `Instant.FromUnixNanoseconds(n)` |
| `Period` | calendar amount | `Period.Days(30)`, `Period.Months(1)`, `Period.Of(years:, months:, weeks:, days:, hours:, minutes:, seconds:)` |
| `PosixSeconds` | `clock-ticks` POSIX amount | `PosixSeconds.Of(n)`, `PosixSeconds.Of(numerator, denominator)` |
| `ElapsedSeconds` | `elapsed` (SI) amount | `ElapsedSeconds.Of(n)` |
| `Interval<T>`, `T` ∈ {`CivilDate`, `Instant`} | `interval`, `start-end` | `Interval.HalfOpen(a, b)`, `Interval.Closed(a, b)`, `Interval.Of(Bound<T> start, Bound<T> end)` |
| `Bound<T>` | an endpoint | `Bound.Included(v)`, `Bound.Excluded(v)`, `Bound.Unbounded`, `Bound.Unknown` |

These rules apply to every type:

- **Valid by construction.** A constructor rejects a value that can't exist, such as 30 February or hour 25. It raises the language's ordinary argument error, the way the native types do (see the [naming table](#naming)).
  - `TryOf` returns an `Outcome` instead of raising.
  - Rust has no exceptions, so its `of` returns `Result<T, Diagnostics>`.
- **Years are arbitrary precision.** That's `BigInteger` in C# and Java, `int` in Python, `bigint` in TypeScript, and `i128` in Rust, where anything beyond the range gives `unsupported` with `implementation-limit`.
- **Seconds are optional.** `CivilTime` and `CivilDateTime` remember whether a second was given (`09:30` and `09:30:00` are different texts). Comparisons treat a missing second as 0. Where a language requires ordering to agree with equality (Rust's `Ord`, Java's `compareTo` contract), ties between equal times break by representation, so `09:30` comes before `09:30:00`.
- **Value semantics.** Equality is representation equality: same JSON, same value. Values are hashable and immutable.
- **Text output.** `ToString()` (`__str__`, `Display`, `toString`) returns the [text form](text-form.md):
  - `PosixSeconds` and `ElapsedSeconds` have no text form, so they return a display string instead: `3600.75 s (POSIX)`, `86400 s (SI)`. Terminating fractions print as decimals; others as a fraction, `1/3 s (POSIX)`.
  - The debugger view (`[DebuggerDisplay]`, `__repr__`, `Debug`) is `CivilDate(2026-11-05)`.
- **JSON at the edges.** `ToJson()` gives the UTV JSON. `T.FromJson(json)` returns `Outcome<T>`.
  - Wrong kinds or forms give `invalid` (`kind-mismatch`).
  - A valid UTV value that no typed type can hold gives `unsupported` (`unsupported-operand`). Examples are a zoned `date`, or a floating date-time carrying a `resolutionPolicy`. The JSON layer still handles such values.
- **Intervals are valid by construction too.** A reversed interval (end before start) is a constructor error with `reversed-interval`. So is an interval with no point at all, such as `Interval.Of(Bound.Unbounded, Bound.Unknown)`: there's nothing to fix its domain. Its code is `unsupported-form`, the same as when parsing `(.., ..)`.
- **`Instant.Nanoseconds`** is the floor of the sub-second part in nanoseconds. The exact rational stays in the JSON, and `Instant.SubSecond` returns it exactly as a rational.
- **Hashing.** Where a language has no hash protocol (JavaScript), value semantics means `equals()` plus immutability.
- **Text input.** `T.Parse(text)` returns `Outcome<T>`.
  - `Utv.Parse(text)` parses any text-form value. Its result is the language's union of the types above: in C# an `ITemporalValue`; in TypeScript a union type; in Python the union of the classes; in Rust an `enum Value`.
  - Parsing never raises for bad input; bad text is an `Outcome`.

## 2. Total operations return values; partial ones return `Outcome`

An operation returns a plain value when it can't fail for valid inputs. Only a partial operation returns an `Outcome`, so every `Outcome` in user code marks a question time can refuse to answer.

| Type | Total (plain result) | Partial (`Outcome<…>`) |
|---|---|---|
| `CivilDate` | `AddDays(n)`, `AddWeeks(n)`, `AddMonths(n, MonthEnd.Constrain)`, `AddYears(n, MonthEnd.Constrain)`; properties `Year`, `Month`, `Day`, `DayOfWeek` (an ISO `Weekday` enum, Monday first), `DayOfYear`, `IsoWeek`, `IsoWeekYear`, `DaysInMonth`, `IsLeapYear`; `DaysUntil(other)` (integer); `At(time)`; comparison operators / `Comparable` | `AddMonths(n)`, `AddYears(n)` (default policy `Reject`: `none`, `month-end`); `Add(Period)` (`none` on month-end; `invalid`/`kind-mismatch` if the period has time components) |
| `CivilYearMonth` | `AddMonths(n)`, `AddYears(n)`; comparison | — |
| `CivilTime` | comparison | — (no arithmetic: wrapping past midnight loses the day) |
| `CivilDateTime` | `AddDays/Weeks/Hours/Minutes/Seconds(n)` (wall clock, carrying into days); `AddMonths(n, MonthEnd.Constrain)`; `Date`, `Time`; `InZone(id)`; `AtOffset(seconds)`; comparison | `AddMonths(n)`, `Add(Period)` |
| `FixedOffsetDateTime` | `ToInstant()`; `DateTime`, `OffsetSeconds`; comparison (by instant) | — |
| `ZonedIntent` | `AddDays/Weeks/Hours/Minutes/Seconds(n)` (wall clock; drops an asserted offset); `DateTime`, `ZoneId`, `AssertedOffsetSeconds?` | `Resolve(ctx[, policy])` returns `Outcome<Instant>`; `AddMonths(n)`; **no comparison operators** (use `ctx.Compare(a, b)`, which returns `Outcome<Ordering>`) |
| `Instant` | `Add(PosixSeconds)`; `Until(other)` (returns `PosixSeconds`); `AtOffset(seconds)` (returns `FixedOffsetDateTime`); comparison; `UnixSeconds`, `Nanoseconds` | `InZone(id, ctx)` (returns `Outcome<ZonedIntent>` with the asserted offset; `unsupported` outside the rules); `Add(ElapsedSeconds)` (`unsupported`, `needs-leap-seconds`) |
| `Period`, `PosixSeconds`, `ElapsedSeconds` | components; `Negate()`; equality | — |
| `Interval<T>` | `Contains(T)` and `Overlaps(Interval<T>)` return `Truth` (`Definitely` / `Possibly` / `Impossible`); `Start`, `End` | — |

**Offsets as arguments.** An offset argument can be out of range (beyond ±18:00), so `AtOffset(seconds)` validates like a constructor: it raises an argument error, or returns `Result` in Rust (`offset-out-of-range`).

**Policy overloads.** A policy argument can't change the return type, so the total overload `AddMonths(n, monthEnd)` (and `AddYears`) accepts only `Constrain`. Passing `Reject` is a programming error (an argument exception, or `Err` in Rust). The rejecting form is the overload without a policy, which returns an `Outcome`. The same applies to any other policy overload documented as total.

Every operation must agree with the JSON-level everyday API, and so with the fixtures. Implementations should call the 0.2 functions underneath rather than re-implement them.

The type system removes some 0.2 outcomes entirely. For example, there's no `Resolve` on an unzoned `CivilDateTime`, so `symbolic` can't arise, and `Contains` can't be given an instant for a date interval.

## 3. `Outcome<T>`

These are the cases, and every case carries `Diagnostics`, possibly empty:

| Case | Carries | When |
|---|---|---|
| `Unique` | `Value: T` | exactly one answer |
| `Multiple` | `Values: list of T` (ascending) | several answers, e.g. both instants of a DST overlap |
| `None` | diagnostics | proven: no answer exists (month end, DST gap) |
| `Unsupported` | diagnostics | this library lacks the rules or the capability |
| `Invalid` | diagnostics | the input contradicts itself or the rules |

Each language uses its own exhaustive sum type:

| Language | Shape | Matching |
|---|---|---|
| C# | `abstract record Outcome<T>` with nested `sealed record`s `Unique(T Value)`, `Multiple(IReadOnlyList<T> Values)`, `None`, `Unsupported`, `Invalid` | `switch` expression with a type pattern per case |
| Java 21 | `sealed interface Outcome<T>` with `record`s `Unique<T>(T value, …)`, `Multiple<T>`, `None<T>`, `Unsupported<T>`, `Invalid<T>` | pattern `switch` (the library moves to Java 21) |
| Rust | `enum Outcome<T> { Unique(T, Diagnostics), Multiple(Vec<T>, Diagnostics), None(Diagnostics), Unsupported(Diagnostics), Invalid(Diagnostics) }`. Every case carries its diagnostics, and `.diagnostics()` works on all of them | `match`, e.g. `Outcome::Unique(date, _)` |
| TypeScript | `type Outcome<T> = { status: "unique"; value: T; diagnostics } \| { status: "multiple"; values: T[]; … } \| { status: "none"; … } \| …` | `switch (o.status)` with an exhaustive `never` check |
| Python | frozen dataclasses `Unique[T]`, `Multiple[T]`, `NoneOf[T]` (`None` is a keyword), `Unsupported[T]`, `Invalid[T]`, and `Outcome = Unique \| Multiple \| …` | `match` |

Helpers on every outcome (names per the naming table). None of them ever picks an answer silently:

- **`TryGetUnique(out value)`**: true only for `Unique`.
- **`UniqueOr(fallback)`**: the unique value, or the fallback.
- **`Single()`**: the value for `Unique`; otherwise raises `OutcomeException` (`OutcomeError` in Python/TypeScript). The error carries the status and diagnostics. Rust uses `.single() -> Result<T, OutcomeError>`.
- **`Status`**: the wire string (`unique`, `none`, …).
- **`ToString()`**: the value's text when `Unique`; otherwise the status plus the codes, e.g. `none (month-end)`.
- **`ToResult()`**: the `utv-result/1.0` envelope of 0.2, as the bridge to the JSON layer.

`Truth` is a three-valued enum: `Definitely`, `Possibly`, `Impossible`.

## 4. Policies, context, rules

**Policies** are typed in every language. No dictionaries or JSON:

- `MonthEnd { Reject, Constrain }`
- `Gap { Reject, PreserveUnresolved, ShiftForward }`
- `Overlap { Reject, All, Earliest, Latest }`
- `ResolutionPolicy(gap, overlap)`, with presets `ResolutionPolicy.Strict` (reject/reject, the default) and `ResolutionPolicy.AllCandidates`

**Python and TypeScript** may also accept the literal strings, through a typed parameter (`Literal[...]`, a string-union type).

**`TemporalContext`** holds the rules once:

```csharp
var ctx = TemporalContext.With(ZoneRules.Load("conformance/rules/demo-zones.json"));
```

- **Loading.** `ZoneRules.Load(path)` reads a file; `ZoneRules.Parse(text)` parses text.
- **No default context.** There is no global or ambient context. Nothing reads the host's time zone database.
- **Operations.** It exposes:
  - `ctx.Resolve(zoned[, policy])`
  - `ctx.InZone(instant, id)`
  - `ctx.Compare(a, b)`, for zoned values; it resolves both strictly, then compares, and gives `Multiple` if the order depends on the overlap choice
  - `ctx.Rules`

  The methods on values (`zoned.Resolve(ctx)`, `instant.InZone(id, ctx)`) are the same operations.

## 5. Diagnostic codes

Diagnostics are `{code, severity, path, message}`. The **codes** below are shared by every library. Messages are free text in each language. The fixtures check codes with `expect.diagnostics`, which lists the codes that must appear; others may also be present.

| Code | Status | Meaning |
|---|---|---|
| `syntax` | invalid | text doesn't match the grammar |
| `malformed-value` | invalid | JSON that isn't a well-formed value of the expected shape: wrong JSON types, missing or unexpected fields, a non-integer where an integer is required, a malformed rules bundle. Structural-validation codes from 0.1, such as `duplicate-key`, may appear alongside it |
| `day-out-of-range` | invalid | the day doesn't exist in that month |
| `month-out-of-range` | invalid | month outside 1–12 |
| `field-out-of-range` | invalid | hour, minute, second or nanosecond out of range |
| `offset-out-of-range` | invalid | offset beyond ±18:00 |
| `reversed-interval` | invalid | interval end before start |
| `kind-mismatch` | invalid | operand of a meaningless kind or form (hours added to a date, a calendar amount added to an instant, wrong JSON kind for a typed `FromJson`) |
| `offset-mismatch` | invalid | an asserted offset contradicts the zone rules |
| `month-end` | none | the day doesn't exist after month arithmetic |
| `gap` | none | the local time falls in a transition gap |
| `ambiguous` | multiple | the local time happens more than once |
| `leap-second` | unsupported | second 60 needs a leap-second table |
| `end-of-day` | unsupported | `24:00` needs an end-of-day policy |
| `needs-leap-seconds` | unsupported | SI seconds on a POSIX coordinate |
| `needs-zone-rules` | unsupported | order depends on zone rules that weren't supplied |
| `unsupported-form` | unsupported | valid notation outside this profile (week dates, ordinal dates, basic format, decimal period components, EDTF qualifiers) |
| `unsupported-annotation` | unsupported | RFC 9557 annotation other than one `[Area/Location]`, including any `[zone]` after `Z` or `-00:00` |
| `unsupported-operand` | unsupported | the operation doesn't handle this kind or form |
| `unsupported-calendar` | unsupported | non-Gregorian calendar |
| `unsupported-policy` | unsupported | policy value not implemented |
| `no-zone` | symbolic | unzoned value, nothing to resolve against |
| `unknown-zone` | unsupported | zone not in the supplied rules |
| `outside-rule-window` | unsupported | outside the rules' validity window |
| `different-zones` | unsupported | dates in different zones |
| `no-text-form` | unsupported | the value has no text form |
| `implementation-limit` | unsupported | beyond this library's numeric range |
| `local-zone` | unsupported | native value tied to the host's local zone (interop) |
| `intent-required` | invalid | native conversion needs an explicit intent (interop) |
| `out-of-native-range` | unsupported | the native type can't hold the value (interop) |
| `precision-loss` | warning | a conversion dropped precision (interop, lossy mode) |
| `zone-dropped` | warning | a conversion kept the offset and dropped the zone (interop, lossy mode) |
| `offset-assertion-dropped` | info | `add` removed an asserted offset |
| `offset-not-known` | info | an instant converted to a native type that needs an offset was given +00:00 (interop) |

## 6. Naming

| Concept | C# | Java | Python | TypeScript | Rust |
|---|---|---|---|---|---|
| constructor | `CivilDate.Of(…)` | `CivilDate.of(…)` | `CivilDate.of(…)` (or `CivilDate(…)`) | `CivilDate.of(…)` | `CivilDate::of(…) -> Result` |
| non-throwing constructor | `CivilDate.TryOf(…)` | `CivilDate.tryOf(…)` | `CivilDate.try_of(…)` | `CivilDate.tryOf(…)` | (same as `of`) |
| constructor error | `ArgumentOutOfRangeException` | `IllegalArgumentException` | `ValueError` | `RangeError` | `Err(Diagnostics)` |
| method | `date.AddMonths(1)` | `date.addMonths(1)` | `date.add_months(1)` | `date.addMonths(1)` | `date.add_months(1)` |
| property | `date.DayOfWeek` | `date.dayOfWeek()` | `date.day_of_week` | `date.dayOfWeek` | `date.day_of_week()` |
| parse | `CivilDate.Parse(text)`, `Utv.Parse(text)` | `CivilDate.parse`, `Utv.parse` | `CivilDate.parse`, `utv.parse` | `CivilDate.parse`, `parse` | `CivilDate::parse`, `utv::parse` |
| text | `ToString()` | `toString()` | `str(v)` | `toString()` / `String(v)` | `Display` / `to_string()` |
| JSON | `ToJson()` / `FromJson` | `toJson()` / `fromJson` | `to_json()` / `from_json` | `toJSON()` / `fromJson` | `to_json()` / `from_json` |
| outcome helpers | `TryGetUnique`, `UniqueOr`, `Single` | `tryGetUnique` (`Optional`), `uniqueOr`, `single` | `unique_or`, `single` | `uniqueOr`, `single` | `unique_or`, `single` |

Names never stutter: `Parse`/`Format`, not `ParseIso`/`FormatIso`. The 0.2 names stay available in the JSON layer.

Where a name collides with the language itself, each library records the deviation in its README:
- **Python:** `Period.of_days` / `of_months` / … because a `days` classmethod would clash with the `days` component; `TemporalContext(rules)` / `TemporalContext.with_rules(rules)` because `with` is a keyword.
- **Java:** `TemporalValue.parse(text)` because `Utv.parse(String)` is the 0.2 JSON parser.
- **C#:** `Temporal.Parse(text)` because `Utv` is the namespace; period components are read as `period.Components.Days`, because a static `Period.Days(30)` and an instance `Days` can't share a name.
- **Rust:** no overloads, so the total forms are separate methods, `add_months_constrained(n)` / `add_years_constrained(n)`, and `add_with(Period, MonthEnd)` takes a policy.

## 7. Examples (all five languages)

Each library's examples are **unit tests**, not programs, so they run with the library's normal test command and
can't drift from what the library does. [`example-tests.md`](example-tests.md) is the catalog of scenarios they
cover, and [`AGENTS.md`](../AGENTS.md) sets out how they are written.

- **Two suites.**
  - *UTV first*, organized by UTV class and function: each test does a task with UTV and compares the result with
    the language's standard date/time API.
  - *Native first*, organized by the standard API's namespace, class and function: each test does the same task
    with the standard API and compares the result with UTV.
- **Comparison.** Wherever the two sides agree, a test computes its expected value with the other side. Where they
  differ, the expected value is a literal, and the doc comment says what the other side does instead
  ("Compared with: …"). The opposite suite asserts that behavior.
- **Partial operations.** Tests of partial operations assert the outcome's status and its diagnostic code, which
  proves which branch the library took. At minimum this applies to tasks `month-end`, `dst-gap` and `dst-overlap`.
- **Zone rules.** UTV zone operations use `conformance/rules/demo-zones.json` through a shared fixture, never the
  host's tz database.
- **Task markers.** These feed the generated cross-language page `docs/side-by-side.md`. In the UTV-first suite,
  wrap the tests that illustrate a task in comment markers: `// BEGIN <task-id>` … `// END <task-id>`, or `#` in
  Python. A task may be marked more than once. Every library marks every task:

  | Section | Task ids (title) |
  |---|---|
  | 1. Now | `now` (Now) |
  | 2. A date | `a-date` (5 November 2026), `weekday-week` (Day of week and ISO week) |
  | 3. Invalid dates | `feb-30` (30 February 2026) |
  | 4. Adding | `add-days` (5 Nov + 30 days), `month-end` (31 Jan 2026 + 1 month) |
  | 5. Days between | `days-between` (5 Nov → 25 Dec), `compare-dates` (Which comes first) |
  | 6. Times and date-times | `time-of-day` (09:30), `datetime-plus-hours` (23:30 + 2 hours) |
  | 7. Parsing with offsets | `parse-offsets` (Z vs +00:00) |
  | 8. Time zones | `meeting-in-paris` (09:00 in Paris, seen from New York) |
  | 9. DST gap | `dst-gap` (02:30 on 8 March 2026 in New York) |
  | 10. DST overlap | `dst-overlap` (01:30 on 1 November 2026 in New York) |
  | 11. Timestamps | `timestamps` (Exact fractions and differences) |
  | 12. Durations vs periods | `day-vs-24h` (One day vs 86,400 seconds across DST) |
  | 13. Ranges | `ranges` (November, open-ended and unknown ends) |
  | 14. Round trip | `round-trip` (Native → UTV → native) |

  The code between markers is shown verbatim on the page, so it should read well on its own.
- **README.** Each library README opens with a before/after snippet of five to ten lines (task `month-end` is a good
  one), and describes its two example suites.

The 0.2 JSON-level API and its fixtures stay, unchanged.

## 8. Tests each library must have

1. The existing 0.2 tests, including all **130** everyday fixtures.
2. **Fixture codes.** The everyday fixtures now carry `expect.diagnostics`, so the runner must check codes as well as statuses.
3. **Text-form fixtures.** All of [`conformance/operations/text-form.json`](../conformance/operations/text-form.json) (44 entries), exercised through the JSON-level `parseText` / `formatText` functions.
4. **Typed-layer tests.** At least one test per row of the table in §2. Also:
   - `ToString()` round-trips through `Parse`;
   - `FromJson(ToJson(v)) == v`;
   - constructors reject invalid values.
5. **Example tests.** The two suites described in §7, covering every scenario of [`example-tests.md`](example-tests.md).
