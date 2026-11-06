# Aesthetics review: the UTV libraries and their examples

> **Status: implemented in libraries 0.3** (all five languages).
> - **R1–R4, R6–R9:** see [`library-api.md`](library-api.md) and [`side-by-side.md`](side-by-side.md).
> - **R5, text form:** see [`text-form.md`](text-form.md). The canonical interval notation uses brackets instead of EDTF's slash, because EDTF's `a/b` can't say whether an end is excluded; EDTF slash forms are accepted as input.
> - **Deferred:** the shorter JSON *authoring* form, also proposed under R5, needs spec 1.1 (OI-18).
> - **Deferred:** EDTF qualifiers (`1347~`) need a mapping to the `uncertain` kind.
> - **Superseded:** R6's example programs and their snapshot tests, and the traditional catalog tour, were later
>   replaced by example unit tests in every library; see [`example-tests.md`](example-tests.md).
>
> The findings below describe the 0.2 libraries.

**Scope:** the five 0.2.0 libraries (`implementations/*`), their side-by-side `everyday` examples, the traditional catalog tour, and the UTV JSON format as programmers meet it.

**Question:** what would make UTV pleasant to read and write, and the examples persuasive, without giving up anything the specification guarantees?

**Short answer:** the semantics are right and consistent (all five libraries pass the same 130 fixtures), but almost nothing a programmer touches looks like something they'd want to write.

- **The examples read as test harnesses.** The UTV code in them is untyped JSON pushed through static helper functions.
- **Every call returns the same envelope.** That's true even when an operation can't fail, so the most important feature, honest outcomes, shows up as noise rather than as something you handle.

Most of the fixes are in the library API, not in the examples.

---

## Findings

### F1. The examples are test harnesses, not examples

Every UTV line in every example has the same shape:

```csharp
Check("Calendar.Add(jan31, Period(months: 1))", Calendar.Add(jan31, Build.Period(months: 1)), "none (month-end)");
```
```python
show("add(31 Jan, period(months=1))", utv.add(jan31, utv.period(months=1)), "none")
```

- **Each call is written twice.** Once it appears as a label string, once as the real call, and the two have to be kept in sync by hand. They already drift: the C# label above says `Period(months: 1)` where the call says `Build.Period(months: 1)`.
- **The expected answer is a third string.** It is there so the example can assert on itself.
- **The UTV call is buried.** It sits as the middle argument of a helper. A reader can't copy it, and can't see where the result goes or how it's handled.
- **Helper vocabulary differs by language.** It's `Check`/`Native`/`Note` in C#, `show`/`native`/`note` in Python, `t.utv`/`t.native` in Rust, and `utv`/`nat` in TypeScript.

The assertions are valuable, but they belong in tests. The libraries already run 130 fixtures plus the 37-entry catalog.

### F2. Values are raw JSON

Every UTV value is the language's generic JSON type: `JsonElement` in C#, `JsonNode` in Java, `dict` in Python, `serde_json::Value` in Rust, a plain object in TypeScript.

- **No types.** A date and an instant have the same type, so passing one where the other belongs compiles, and only fails at run time with a status.
- **No properties.** There is no `.Year` and no `.Month`. Reading a field means `result.Value.GetProperty("kind")` (C# section 7) or indexing into dicts.
- **No printing.** Values don't render. Every example routes values through `FormatIso`, and then has to unwrap that result too.
- **Gaps get filled with hand-written JSON.** C# section 12 contains a raw JSON literal, because there is no builder for SI seconds:
  ```csharp
  using JsonDocument si = JsonDocument.Parse("""{"schema":"utv/1.0","kind":"amount","value":{"basis":"elapsed",...}}""");
  ```

This was a deliberate 0.1/0.2 choice (milestone M2 in `implementations/csharp/docs/engine-interface.md` adds typed records). The examples show that it is now the main thing that makes UTV look worse than the native APIs it's compared with.

### F3. Every operation returns a result envelope, even when it can't fail

`Calendar.Add(date, Period(days: 30))` returns a `UtvResult`, the same as `Zones.Resolve(...)`. Once a date is known to be valid, adding days always succeeds. But the reader can't tell that from the signature, so every call needs unwrapping (`.Value`, `.value()`, `candidate(r)`).

When every call can "fail", the calls that genuinely can (month arithmetic, DST resolution, comparing zoned values) no longer stand out. **The honest outcomes should look special, and right now they look like everything else.**

### F4. Outcome handling is never shown

UTV's central idea is that a time question can have none, one or many answers, and that the caller decides. Yet no example contains a `switch`/`match` on an outcome. The `Check` helper hides the only place where a reader would see how to handle `none` or `multiple`.

### F5. Policies are strings and dictionaries in some languages

| Language | Month-end policy | Notes |
|---|---|---|
| C# | `MonthEndPolicy.Constrain` | typed |
| Java | `MonthEnd.CONSTRAIN` | typed |
| Python | `{"monthEnd": "constrain"}` | typo-prone |
| TypeScript | `{ monthEnd: "constrain" }` | checked only if the type is declared |
| Rust | `json!({"monthEnd": "constrain"})` | passed as `Some(&constrain)` |

### F6. Names and module layout differ across languages for no reason

| Concept | C# | Java | Python | TypeScript | Rust |
|---|---|---|---|---|---|
| builders | `Build.Date` | `Values.date` | `utv.date` | `date` | `date` |
| add | `Calendar.Add` | `Operations.add` | `utv.add` | `add` | `add(&a, &b, None)` |
| ranges | `Ranges.Contains` | `Intervals.contains` | `utv.contains` | `contains` | `contains` |
| parse | `Iso.ParseIso` | `Iso.parseIso` | `utv.parse_iso` | `parseIso` | `parse_iso` |

- **Different module names.** The table shows a different module for the same concept in each language.
- **Stutter.** `Iso.ParseIso` repeats itself.
- **Undocumented divergence.** Each agent chose a sensible layout, but nobody chose one layout.

### F7. Diagnostic codes are not standardized

The same input, `parseIso("2026-02-30")`, gives a different code in each language:

| Language | Code |
|---|---|
| Python, C# | `day-out-of-range` |
| TypeScript | `field-out-of-range` |
| Java | `utv.civil.day` |
| Rust | its own message-style code |

The contract fixed only a handful of codes (`month-end`, `gap`, `ambiguous`, `offset-*`). Codes are what programs branch on, so this is an interoperability gap as well as an aesthetic one.

### F8. Output is hard to scan

- **Long lines:** 11 of the 119 lines of Python output are longer than 120 characters.
- **Raw JSON objects:** the `fields` result prints as a raw JSON object.
- **Noise status:** `unique` precedes almost every value, so it carries no signal.
- **Exact rationals print as fractions:** an elapsed time prints as `14403/4 POSIX s`, which is exact but hard to read; `3600.75 s` is how a person reads it.
- **No visual pairing:** native and UTV results come in two separate blocks per section. The reader has to pair up "31 Jan + 1 month, native" with "31 Jan + 1 month, UTV" by hand.

### F9. The JSON format is verbose for simple values

A date is 140 characters:

```json
{"schema":"utv/1.0","kind":"civil","value":{"form":"date","fields":{"year":"2026","month":11,"day":5},"calendar":{"id":"gregory","chronology":"proleptic"}}}
```

Every part of that is justified for interchange: an explicit calendar, a version, and a year as a string because years are unbounded. But it shouldn't be the face of UTV in code, logs or documentation. There is no compact *text* form. ISO 8601 / RFC 9557 cover part of the vocabulary. EDTF / ISO 8601-2 cover more of it: intervals `2026-11-01/2026-12-01`, open ends `2026-11-01/..`, unknown ends `2026-11-01/`, uncertainty `1347?` and `1347~`. Nothing in UTV uses those forms for display.

### F10. Rules are threaded through every call

Every zone operation takes `rules`:

```csharp
Zones.ToZone(instant.Value, "America/New_York", _rules)
```

Passing rules explicitly is the right principle: no hidden tzdb. But repeating them in every call is noise. The engine design (`ITemporalEngine` + `EvaluationContext` in `implementations/csharp/docs/engine-interface.md`) already holds rules once, in a context object; 0.2 doesn't have one yet.

---

## Recommendations

The recommendations are ordered by impact. R1–R4 change the library API in all five languages. They are what makes the examples readable, and should land together as library version 0.3.

### R1. Typed values that are valid by construction (most impact)

Introduce one immutable type per everyday kind, with the same names in every language (adapted to each language's casing):

| UTV type | Holds | Avoids clashing with |
|---|---|---|
| `CivilDate` | year, month, day, calendar | `DateOnly`, `LocalDate`, `date`, `Date` |
| `CivilTime` | hour … nanosecond | `TimeOnly`, `LocalTime` |
| `CivilDateTime` | date + time, no zone | `DateTime`, `LocalDateTime`, naive `datetime` |
| `ZonedIntent` | civil date-time + zone + `zoneIntent` (+ optional asserted offset) | `ZonedDateTime` (a different idea: it fixes the instant) |
| `Instant` | timescale + exact coordinate | (keep; `java.time.Instant` has the same meaning) |
| `Period` | calendar components | `Period` (Java) |
| `PosixSeconds`, `ElapsedSeconds` | exact rationals with explicit basis | `TimeSpan`, `Duration`, `timedelta` |
| `Interval<T>` | two `Bound<T>`s: `Included`, `Excluded`, `Unbounded`, `Unknown` | |

Rules:

- **Constructors validate.** `CivilDate.Of(2026, 2, 30)` can't produce a value. In C#/Java it throws `ArgumentException`, like `DateOnly`, and there is `TryOf` returning an outcome. Python, TypeScript and Rust use their idiomatic equivalents (`ValueError`, a thrown `RangeError` or a result, `Result`). Parsing and JSON import always return outcomes, because external data can be invalid.
- **Value semantics everywhere.** Each type has a `ToString()`/`__str__`/`Display`/`toString` that returns the text form (R5). Equality is representation equality. There are no `<` operators on types whose order can be undefined.
- **JSON at the edges only.** `ToJson()` and `FromJson()` connect to the wire format. Raw JSON stays available, but is no longer the everyday API.

The specification is unaffected: these are library types over the same UTV values, and the fixtures keep testing the JSON boundary.

### R2. Return plain values from total operations, outcomes only from partial ones

When values are valid by construction (R1), many operations can't fail and should return the value itself:

| Operation | Total? | Returns |
|---|---|---|
| `date.AddDays(n)`, `date.AddWeeks(n)`, `dateTime.AddHours(n)` | yes | `CivilDate` / `CivilDateTime` |
| `date.DayOfWeek`, `IsoWeek`, `DaysInMonth` | yes | plain values (properties) |
| `a.DaysUntil(b)` (same calendar) | yes | integer |
| `instant + PosixSeconds` | yes | `Instant` |
| `date.AddMonths(n)` with the default `Reject` policy | **no** | `Outcome<CivilDate>` |
| `date.AddMonths(n, MonthEnd.Constrain)` | yes | `CivilDate` (a separate overload or method) |
| `zoned.Resolve()` | **no** | `Outcome<Instant>`: none / unique / multiple / unsupported |
| `a.CompareTo(b)` for zoned or mixed kinds | **no** | `Outcome<Order>` |
| parsing, JSON import | **no** | `Outcome<T>` |

Then **every `Outcome` in the code marks a question time can genuinely refuse to answer**, which is UTV's point, now visible in the types.

### R3. Outcomes as each language's own sum type, with exhaustive matching

| Language | Shape |
|---|---|
| C# | `abstract record Outcome<T>` with `Unique(T Value)`, `Multiple(IReadOnlyList<T> Values)`, `None(Diagnostic Why)`, `Unsupported(...)`, `Invalid(...)`; handled with `switch` expressions |
| Java (21) | `sealed interface Outcome<T> permits Unique, Multiple, None, Unsupported, Invalid` with records, handled with pattern `switch`. This needs Java 21 (LTS); the library targets 17 today |
| Rust | `enum Outcome<T> { Unique(T), Multiple(Vec<T>), None(Reason), Unsupported(Reason), Invalid(Reason) }`, used with `match` and `?` through helper adapters |
| TypeScript | discriminated union `{ status: "unique", value } \| { status: "multiple", values } \| …`, narrowed with `switch (r.status)` |
| Python | `@dataclass(frozen=True)` classes `Unique`, `Multiple`, `NoneOf`, `Unsupported`, `Invalid`, handled with `match` (3.10+) |

Each also gets small, explicit helpers that never pick an answer silently, such as `TryGetUnique` / `unique_or(default)` / `Single()` (throws on anything but `Unique`). Exhaustive matching makes the compiler or type checker require the caller to handle `None` and `Multiple`, which is exactly the discipline UTV exists to encourage.

### R4. Typed policies, one name per concept, methods on values

- **Typed policies.** Make policies enums or records in every language: `MonthEnd.Constrain`, `Gap.ShiftForward`, `Overlap.Earliest`. Python and TypeScript also accept the string literal through a typed parameter (`Literal["reject", "constrain"]`), but never an untyped dictionary.
- **Operations as methods.** Make operations methods on the values: `date.AddMonths(1)`, `meeting.Resolve(ctx)`, `instant.InZone("America/New_York", ctx)`, `november.Contains(day)`. Keep free functions only where they're idiomatic, such as Python's module functions and Rust traits, and with the same names.
- **One shared naming table.** Settle names once in `docs/everyday-api.md`, then apply them in every language: `Parse`/`Format` (not `ParseIso`), `DaysUntil`, `Contains`, `Overlaps`, `Resolve`, `InZone`, `AtOffset`.
- **A context object for rules.** Holding the rules in one object (`TemporalContext ctx = TemporalContext.With(rules)`, or the planned engine) removes rules from every call. Its constructor makes the dependency explicit once. There is still no default context, so nothing reads the host zone.

### R5. A compact text form, used for display and accepted for input

Define a UTV text notation:

- **Plain values** use ISO 8601 / RFC 9557: `2026-11-05`, `2027-06-01T09:00[Europe/Paris]`.
- **Intervals and uncertainty** use EDTF / ISO 8601-2: `2026-11-01/2026-12-01`, `2026-11-01/..`, `2026-11-01/`, `1347~`.
- **What it's for.** `ToString()` uses this text, and so do logs and the examples. It is also an accepted input to `Parse`.
- **Fallback.** Anything the notation can't express prints as its JSON. That boundary is honest, and the spec already has the vocabulary for it (§2 "Interchange").

This is a minor spec addition (1.1). It makes UTV look like ISO dates in the common case, and keeps the JSON for interchange.

Separately, consider an **authoring form** for JSON that may omit the default calendar (`gregory`/`proleptic`) and the `schema` of nested values. Canonicalization makes them explicit again. Spec §2 already separates authoring form from canonical form; this would let hand-written JSON be about 40% shorter.

### R6. Examples as plain, copyable code, verified by snapshot

Rewrite the examples so that each task is ordinary code a reader would write, with the output shown as a comment next to it:

```csharp
// 31 January + 1 month
var jan31 = new DateOnly(2026, 1, 31);
Console.WriteLine(jan31.AddMonths(1));                 // 2026-02-28   ← clamped, silently

var utvJan31 = CivilDate.Of(2026, 1, 31);
switch (utvJan31.AddMonths(1))                          // Outcome<CivilDate>
{
    case Unique<CivilDate>(var d): Console.WriteLine(d); break;
    case None<CivilDate>(var why): Console.WriteLine($"no such date: {why}"); break;   // ← this runs: 2026-02 has no day 31
}
Console.WriteLine(utvJan31.AddMonths(1, MonthEnd.Constrain));   // 2026-02-28   ← because you asked
```

- **Assertions move out.** They go into a **snapshot test**, which runs the example and compares its output with a committed `everyday.expected.txt`. Lines from the native side that depend on the host (the clock, the tz database) are masked. The output comments stay honest because the snapshot fails when they drift.
- **Outcome handling.** At least sections 4 (month end), 9 (DST gap) and 10 (DST overlap) show a real `switch`/`match`.
- **Same tasks in every language.** Use the same section titles and task names in all five languages, so the five examples can be shown as tabs of one page. Generate a `docs/side-by-side.md`, or a published page, from them.
- **Helpers out of sight.** No harness vocabulary in the visible code. Native-side helpers that hide native code (TypeScript's `zonedToDate`, Python's `add_months`) are marked clearly as "what people write by hand".

### R7. Output that pairs native and UTV per task

When the examples run, print one block per task, with native and UTV aligned. Print the status word only when it isn't `unique`.

```
4. Adding
   31 Jan 2026 + 1 month
     .NET   2026-02-28    clamped silently
     UTV    no such date  2026-02 has no day 31
     UTV    2026-02-28    with MonthEnd.Constrain
```

Further rules:

- Keep lines within 100 columns.
- Print fields as `Thursday, ISO week 45` rather than as JSON.
- Print exact rationals as decimals where they terminate (`3600.75 s`), keeping the fraction only when they don't.

### R8. A shared diagnostic code registry

- **The registry.** Add a table of diagnostic codes to `docs/everyday-api.md` (and later to the spec), for example `day-out-of-range`, `month-out-of-range`, `month-end`, `gap`, `ambiguous`, `offset-mismatch`, `local-zone`, `outside-rule-window`, `unknown-zone`, `needs-leap-seconds`.
- **Fixtures check them.** Extend the fixtures with `expect.diagnostics` for the cases where a code matters.
- **Messages stay free-form.** Each language words its messages its own way; only the codes are shared.

### R9. Smaller items

- **Interop for "now".** Add `Instant.FromUnixNanoseconds(long)` / `from_unix_ns(time_ns())`, so section 1 doesn't need `instant(*divmod(time_ns(), 10**9))`.
- **Missing builders.** Add `ElapsedSeconds` (SI), so no example has to write raw JSON.
- **Debugger display.** Add a `[DebuggerDisplay]` (C#), `__repr__` (Python) and `Debug` (Rust) that show the text form.
- **Lead with a snippet.** The README of each library should open with a five-line before/after snippet, not with the validation API.

---

## Suggested sequence

1. **Contract first (0.3).** Write R1–R4 and R8 into `docs/everyday-api.md`: the type names, which operations are total and which partial, the outcome shapes, the naming table and the diagnostic codes. Add `expect.diagnostics` to the fixtures. Agree it once, as with 0.2, so the five libraries can't diverge.
2. **Text form (spec 1.1).** Specify R5 and add fixtures for `toString`/`parse` of the text form.
3. **Libraries 0.3.** Implement in all five languages in parallel against the contract. The existing 130 fixtures keep passing at the JSON boundary.
4. **Examples.** Rewrite them per R6/R7, with snapshot tests, and generate the side-by-side page.

Steps 1–3 matter more than step 4. Better examples written against the current API would still show untyped JSON and envelopes everywhere, so they would only be marginally better.
