# utv (Rust) 0.3.0

```rust
// chrono: 31 January + 1 month is 28 February, and nothing tells you.
let jan31 = NaiveDate::from_ymd_opt(2026, 1, 31).unwrap();
println!("{}", jan31 + Months::new(1));                      // 2026-02-28

// UTV: there is no 31 February, so you get no answer, and you decide.
let jan31 = CivilDate::of(2026, 1, 31)?;
match jan31.add_months(1) {
    Outcome::Unique(date, _) => println!("{date}"),
    Outcome::None(why) => println!("no such date: {why}"),    // 2026-02 has no day 31 (month-end)
    other => println!("{other}"),
}
println!("{}", jan31.add_months_constrained(1));              // 2026-02-28, because you asked
```

```
cargo run --example everyday --features chrono    # 14 everyday tasks, chrono next to UTV
cargo run --example traditional                   # the shared catalog of traditional tasks
cargo test --all-features                         # everything, including the example's snapshot
```

Rust library for the Universal Temporal Value specification, wire version `utv/1.0`, results
`utv-result/1.0`, text form `utv-text/1`. It implements the library contract
[`docs/library-api.md`](../../docs/library-api.md) (0.3) on top of the JSON-level everyday API
of [`docs/everyday-api.md`](../../docs/everyday-api.md) (0.2). It never reads the system clock,
time zone or locale.

## Typed values

One immutable type per everyday value, each exactly one UTV value. All of them are at the
crate root.

| Type | Example (`Display`) | Constructed by |
|---|---|---|
| `CivilDate` | `2026-11-05` | `CivilDate::of(2026, 11, 5)?`, `"2026-11-05".parse()?` |
| `CivilYearMonth` | `2026-11` | `CivilYearMonth::of(2026, 11)?` |
| `CivilTime` | `09:30`, `09:30:15.25` | `CivilTime::of(9, 30)?`, `of_hms`, `of_hms_nano` |
| `CivilDateTime` | `2026-11-05T14:30` | `CivilDateTime::of(2026, 11, 5, 14, 30)?`, `date.at(time)` |
| `FixedOffsetDateTime` | `2026-11-05T14:30+01:00` | `date_time.at_offset(3600)?` |
| `ZonedIntent` | `2027-06-01T09:00[Europe/Paris]` | `date_time.in_zone("Europe/Paris")` |
| `Instant` | `2026-11-05T13:30:00Z` | `Instant::from_unix_seconds(s)`, `from_unix_seconds_nanos`, `from_unix_nanoseconds`, `from_system_time(reading)` |
| `Period` | `P1M`, `PT1M`, `-P1DT2H` | `Period::months(1)`, `Period { months: 1, days: 1, ..Period::default() }`, `Period::of(..)` |
| `PosixSeconds` | `3600.75 s (POSIX)`, `1/3 s (POSIX)` | `PosixSeconds::of(n)`, `of_ratio(n, d)?` |
| `ElapsedSeconds` | `86400 s (SI)` | `ElapsedSeconds::of(n)`, `of_ratio(n, d)?` |
| `Interval<CivilDate>`, `Interval<Instant>` | `[2026-11-01, 2026-12-01)`, `[2026-11-01, ?)` | `Interval::half_open(a, b)?`, `closed`, `Interval::of(Bound::Included(a), Bound::Unknown)?` |

- **Valid by construction.** `of` returns `Result<T, Diagnostics>`: `CivilDate::of(2026, 2, 30)`
  is `Err` with code `day-out-of-range`. `Diagnostics` implements `std::error::Error`, so `?`
  works in a `main` that returns `Result<(), Box<dyn Error>>`. Years are `i128`; values this
  library can't compute with are refused with `implementation-limit`. Intervals refuse a
  reversed order (`reversed-interval`) and having no point at all (`unsupported-form`).
- **Month-end policy.** The total forms are separate methods, `add_months_constrained(n)` and
  `add_years_constrained(n)`, so a `Reject` policy can't be passed where a plain value is
  returned; `add_months(n)` is the rejecting form and returns an `Outcome`.
- **Values.** `Clone`, `Eq`, `Hash` (equality is representation: `09:30` ≠ `09:30:00`). `Display`
  is the text form, `Debug` is `CivilDate(2026-11-05)`. `to_json()`, `T::from_json(&json)`,
  `T::parse(text)` and `FromStr` connect to JSON and text; `utv::parse(text)` reads any value
  into the `enum Value`. `from_json` of another kind or form is `invalid` (`kind-mismatch`); a
  valid UTV value that no typed type can hold (a zoned date, a `resolutionPolicy`, an interval
  of date-times) is `unsupported` (`unsupported-operand`), and the JSON layer still handles it.
- **Order.** `Ord` only where the order can't depend on zone rules: dates, year-months, times,
  date-times, fixed-offset date-times (by instant) and instants. A missing second counts as 0;
  values that denote the same time but are written differently (`09:30`, `09:30:00`) are then
  ordered by representation, so that `Ord` agrees with `Eq`. `ZonedIntent` has no `Ord`: use
  `ctx.compare(&a, &b)`, which returns `Outcome<Ordering>`.

### Total operations return values, partial ones an `Outcome`

| Type | Plain result | `Outcome<…>` |
|---|---|---|
| `CivilDate` | `add_days`, `add_weeks`, `add_months_constrained`, `add_years_constrained`; `year`, `month`, `day`, `day_of_week` (`Weekday`), `day_of_year`, `iso_week`, `iso_week_year`, `days_in_month`, `is_leap_year`; `days_until`; `at` | `add_months`, `add_years` (`month-end`), `add(Period)`, `add_with(Period, MonthEnd)` |
| `CivilYearMonth` | `add_months`, `add_years` | |
| `CivilDateTime` | `add_days/weeks/hours/minutes/seconds` (wall clock), `add_months_constrained`; `date`, `time`, `in_zone` | `add_months`, `add(Period)`, `add_with` |
| `FixedOffsetDateTime` | `to_instant`, `date_time`, `offset_seconds` | |
| `ZonedIntent` | `add_days/weeks/hours/minutes/seconds` (wall clock; drops an asserted offset); `date_time`, `zone_id`, `asserted_offset_seconds` | `resolve(&ctx)`, `resolve_with(&ctx, policy)`, `add_months` |
| `Instant` | `add(PosixSeconds)`, `until` (`PosixSeconds`), `unix_seconds`, `nanoseconds` (floor), `sub_second` (exact, as `PosixSeconds`) | `in_zone(id, &ctx)`, `add_elapsed(ElapsedSeconds)` (`needs-leap-seconds`) |
| `Interval<T>` | `contains`, `overlaps` (`Truth`: `Definitely`, `Possibly`, `Impossible`); `start`, `end` | |

`at_offset(seconds)` returns a `Result`, because the offset is an argument that can be out of
range (`offset-out-of-range`).

### `Outcome<T>`

```rust
pub enum Outcome<T> {
    Unique(T, Diagnostics),
    Multiple(Vec<T>, Diagnostics),   // ascending, e.g. both instants of a DST overlap
    None(Diagnostics),               // proven: no answer (month end, DST gap)
    Unsupported(Diagnostics),        // this library lacks the rules or the capability
    Invalid(Diagnostics),            // the input contradicts itself or the rules
}
```

Every case carries its `Diagnostics`, so `Unique` and `Multiple` keep their notes too (`ambiguous`
on an overlap, `offset-assertion-dropped`); `.diagnostics()` reads them from any case. Helpers
never pick an answer silently: `.unique()` (`Option<T>`), `.unique_or(fallback)`,
`.single()` (`Result<T, OutcomeError>`, for `?`), `.status()`, `.map(f)`, `.to_result()` (the
0.2 `UtvResult`). `Display` is the value for `Unique`, otherwise the status and codes:
`none (month-end)`. Don't `use Outcome::*`: its `None` would shadow `Option::None`.

### Policies and the context

Policies are enums: `MonthEnd::{Reject, Constrain}`, `Gap::{Reject, PreserveUnresolved,
ShiftForward}`, `Overlap::{Reject, All, Earliest, Latest}`, and `ResolutionPolicy::new(gap,
overlap)` with `ResolutionPolicy::STRICT` (the default) and `ResolutionPolicy::ALL_CANDIDATES`.

```rust
let ctx = TemporalContext::with(ZoneRules::load("conformance/rules/demo-zones.json")?);
let meeting = CivilDateTime::of(2027, 6, 1, 9, 0)?.in_zone("Europe/Paris");
let starts = meeting.resolve(&ctx).single()?;                 // 2027-06-01T07:00:00Z
let there = starts.in_zone("America/New_York", &ctx).single()?; // 2027-06-01T03:00:00-04:00[America/New_York]
```

There is no default or global context. `ZoneRules` is the JSON layer's `Rules`: `load(path)`
returns `Result<Rules, Diagnostics>` (`malformed-value` for a file that isn't a bundle),
`parse(text)` keeps its 0.2 `Result<Rules, String>`. Zones it doesn't list and times outside its
window are `unsupported`.

### Native types

With the cargo feature **`chrono`**, conversions use `From`/`TryFrom`, and the target type names
the intent:

| chrono | UTV |
|---|---|
| `NaiveDate` | `CivilDate::from(d)`; back with `NaiveDate::try_from(date)?` (`out-of-native-range` beyond about ±262 000) |
| `NaiveTime`, `NaiveDateTime` | `CivilTime::try_from`, `CivilDateTime::try_from` and back |
| `DateTime<FixedOffset>` | `Instant::try_from(dt)?` **or** `FixedOffsetDateTime::try_from(dt)?`; back from `FixedOffsetDateTime` |
| `DateTime<Utc>` | `Instant::try_from(dt)?` and back (`precision-loss` below a nanosecond) |
| `DateTime<Local>` | no conversion: it depends on the host's zone |

`SystemTime` is always available: `Instant::from_system_time(reading)` and
`SystemTime::try_from(instant)`.

### Diagnostic codes

Codes are the shared table of [`docs/library-api.md` §5](../../docs/library-api.md#5-diagnostic-codes):
`syntax`, `malformed-value`, `day-out-of-range`, `month-out-of-range`, `field-out-of-range`, `offset-out-of-range`,
`reversed-interval`, `kind-mismatch`, `offset-mismatch`, `month-end`, `gap`, `ambiguous`,
`leap-second`, `end-of-day`, `needs-leap-seconds`, `needs-zone-rules`, `unsupported-form`,
`unsupported-annotation`, `unsupported-operand`, `unsupported-calendar`, `unsupported-policy`,
`no-zone`, `unknown-zone`, `outside-rule-window`, `different-zones`, `no-text-form`,
`implementation-limit`, `local-zone`, `out-of-native-range`, `precision-loss`, `zone-dropped`,
`offset-assertion-dropped`. JSON of the wrong shape is `malformed-value`, followed by the
validator's specific `envelope.*` or `structural.schema` diagnostics.

## The JSON layer (0.1, 0.2)

Still here, unchanged in behaviour, and still what the shared fixtures test. All of it is
re-exported at the crate root.

| Module | Contents |
|---|---|
| `utv::build` | `date`, `year_month`, `time` (+ `_with_seconds`, `_with_nanos`), `date_time` (+ `_with_seconds`, `_with_nanos`), `in_zone`, `with_offset`, `instant` (+ `_with_nanos`), `period(Period)`, `posix_seconds`, `elapsed_seconds` (new), `interval`, `interval_with(Endpoint, Endpoint)` |
| `utv::iso` | `parse_iso`, `format_iso` |
| `utv::text` | `parse_text`, `format_text`: the text form, including periods and intervals (new in 0.3) |
| `utv::ops` | `fields`, `add`, `compare`, `days_between`, `posix_seconds_between`, `contains`, `overlaps` |
| `utv::zones` | `Rules` (`load`, `parse`, `from_json`), `resolve`, `to_zone`, `to_offset` |
| `utv::interop` | `from_system_time`, `to_system_time`; with feature `chrono`, `utv::interop::chrono` (`from_naive_date`, `to_utc`, ...) |
| validation | `validate_structural`, `validate_gregorian_civil` |

Values are `serde_json::Value`, policies are JSON (`Some(&json!({"monthEnd": "constrain"}))`),
and every call returns a `UtvResult` (status, diagnostics, candidates). Arithmetic is exact:
years and day counts are `i128`, fractions reduced `i128` rationals; anything beyond is
`unsupported` (`implementation-limit`), never a wrapped value.

## Examples

**Everyday** ([`examples/everyday.rs`](examples/everyday.rs)): the same 14 sections in every
language (now, a date, invalid dates, adding, days between, times, offsets, time zones, DST gap,
DST overlap, timestamps, durations vs periods, ranges, round trip). Each task is plain code:
chrono first, then UTV, with a real `match` on the outcome where the answer can be none or
many, and each printed line's output as a trailing comment. `// BEGIN <task-id>` / `// END
<task-id>` markers feed `docs/side-by-side.md`. One small `row(who, text, note)` helper aligns
the output; a note that doesn't fit after a long value goes on the next line, so every line
stays within 100 columns. chrono and chrono-tz are dev-dependencies.

The output is committed as [`examples/everyday.expected.txt`](examples/everyday.expected.txt), and
`tests/everyday_example.rs` compares every line with it, except lines ending in ` ‡`, which
depend on the host's clock or zone. After an intended change, record it again with
`cargo run --example everyday --features chrono > examples/everyday.expected.txt`.

**Traditional** ([`examples/traditional.rs`](examples/traditional.rs)): builds two dates and
matches on the result status, then tours the shared catalog
[`examples/traditional.json`](../../examples/traditional.json) of 37 traditional date/time tasks
with the JSON-layer validators. It exits non-zero if any outcome differs from the catalog.

## Tests

```
cargo test --all-features
cargo clippy --all-features --all-targets
```

`--all-features` also builds the `chrono` adapters and runs the example's snapshot test. Shared
files are read from the repository, relative to `CARGO_MANIFEST_DIR`.

| Test | Checks |
|---|---|
| `tests/everyday.rs` | every fixture of `conformance/operations/everyday.json`: status, candidates, operation and the `expect.diagnostics` codes |
| `tests/text_form.rs` | every fixture of `conformance/operations/text-form.json` through `parse_text` / `format_text` |
| `tests/typed.rs` | the typed layer: each row of library-api §2, `to_string` → `parse` and `to_json` → `from_json` round trips, constructor refusals, outcome helpers |
| `tests/everyday_example.rs` | the example's output against its snapshot (no libtest harness: it runs the example in a child process) |
| `tests/purity.rs` | library code never reads the clock, the host zone or locale, or environment variables |
| `tests/operations.rs` | JSON-layer rules the fixtures don't exercise |
| `tests/conformance.rs`, `tests/traditional.rs` | validation against `conformance/examples/` and the traditional catalog |
| unit tests in `src/` | exact arithmetic, ISO and text round trips, display strings, native adapters |

## Not implemented

Everything outside the everyday API and the validators (non-Gregorian calendars, leap-second
tables, recurrences, registries, result manifests, ...) returns `unsupported` or is not offered.
