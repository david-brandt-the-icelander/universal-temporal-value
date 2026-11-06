# utv (Python)

```python
from datetime import date
from utv import CivilDate, MonthEnd, NoneOf, Unique

# Python's datetime: no month arithmetic, so people clamp by hand and never notice
date(2026, 1, 31).replace(month=2)                  # ValueError: day is out of range for month

# UTV: the question can have no answer, and the type says so
jan31 = CivilDate.of(2026, 1, 31)
match jan31.add_months(1):                          # Outcome[CivilDate]
    case Unique(value=d):
        print(d)
    case NoneOf(diagnostics=why):                   # this runs: 2026-02 has no day 31 (month-end)
        print("no such date:", why[0].message)
print(jan31.add_months(1, MonthEnd.CONSTRAIN))      # 2026-02-28, because you asked
```

Run it, the other everyday tasks, and the tests, from `implementations/python`:

```
PYTHONPATH=src python3 examples/everyday.py          # Python's datetime and UTV, task by task
PYTHONPATH=src python3 examples/traditional.py       # the shared catalog of traditional tasks
PYTHONPATH=src python3 -m unittest discover -s tests
```

- Library version: **0.3.0**. Specification read: **`utv/1.0`** (results use `utv-result/1.0`; the
  text form is profile `utv-text/1`).
- Python 3.10+, standard library only. Type-hinted throughout; passes `mypy --strict`.
- No clock, host time zone, locale, installed tzdb or network access in library code. Zone rules are
  an explicit input (a bundle in the format of `conformance/rules/demo-zones.json`).
- Conformance profiles claimed: none yet. Registries shipped: none.

## The typed API

The contract is [`docs/library-api.md`](../../docs/library-api.md). Everything is importable from `utv`.

### Values

| Type | Example (`str(v)`) | Built with |
|---|---|---|
| `CivilDate` | `2026-11-05` | `CivilDate.of(2026, 11, 5)` or `CivilDate(2026, 11, 5)` |
| `CivilYearMonth` | `2026-11` | `CivilYearMonth.of(2026, 11)` |
| `CivilTime` | `09:30`, `09:30:15.25` | `CivilTime.of(9, 30)`, `CivilTime.of(9, 30, 15, 250_000_000)` |
| `CivilDateTime` | `2026-11-05T14:30` | `CivilDateTime.of(2026, 11, 5, 14, 30)`, `date.at(time)` |
| `FixedOffsetDateTime` | `2026-11-05T14:30+01:00` | `date_time.at_offset(3600)` |
| `ZonedIntent` | `2027-06-01T09:00[Europe/Paris]` | `date_time.in_zone("Europe/Paris")` |
| `Instant` | `2026-11-05T13:30:00Z` | `Instant.from_unix_seconds(s, nanos)`, `Instant.from_unix_nanoseconds(time_ns())` |
| `Period` | `P1M`, `-P1DT2H` | `Period(months=1)`, `Period.of(days=1, hours=2)`, `Period.of_days(30)` |
| `PosixSeconds` | `3600.75 s (POSIX)` | `PosixSeconds.of(14403, 4)` |
| `ElapsedSeconds` | `86400 s (SI)` | `ElapsedSeconds.of(86400)` |
| `Interval[T]`, `T` = `CivilDate` or `Instant` | `[2026-11-01, 2026-12-01)`, `[2026-11-01, ..)`, `[2026-11-01, ?)` | `Interval.half_open(a, b)`, `Interval.closed(a, b)`, `Interval.of(Bound.included(a), Bound.UNKNOWN)` |

- **Valid by construction.** A constructor rejects a value that can't exist with `OutcomeError`, which
  is a `ValueError`, as `datetime.date` does; it carries the status and diagnostics. `T.try_of(...)`
  returns an outcome instead. Wrong Python types raise `TypeError`.
- **Intervals.** A reversed interval raises (`reversed-interval`), and so does one with no point at all,
  such as `Interval.of(Bound.UNBOUNDED, Bound.UNKNOWN)` (`unsupported-form`, as when parsing `(.., ..)`).
- **Values.** Frozen, slotted dataclasses: immutable, hashable, and equal when their UTV JSON is equal,
  so `CivilTime.of(9, 30) != CivilTime.of(9, 30, 0)`. Comparison operators compare the values
  (`09:30 <= 09:30:00` holds both ways). `ZonedIntent` has none: its order depends on zone rules.
- **Text.** `str(v)` is the [text form](../../docs/text-form.md); `repr(v)` is `CivilDate(2026-11-05)`.
  `T.parse(text)` and `utv.parse(text)` return an outcome and never raise for bad text. Values with no
  text form (a period with mixed signs, an instant at a third of a second) print as their JSON.
- **JSON.** `v.to_json()` gives the UTV value; `T.from_json(json)` and `utv.from_json(json)` read one
  back as an outcome. A different kind or form is `invalid` (`kind-mismatch`), JSON of the wrong shape is
  `invalid` (`malformed-value`), and JSON that no typed value
  holds exactly (a zoned date, a value with `provenance`) is `unsupported` (`unsupported-operand`), and
  stays usable in the JSON layer.

### Total operations return values, partial ones return outcomes

| Type | Total: returns the value | Partial: returns `Outcome[...]` |
|---|---|---|
| `CivilDate` | `add_days`, `add_weeks`, `add_months(n, MonthEnd.CONSTRAIN)`, `add_years(n, MonthEnd.CONSTRAIN)`, `days_until`, `at`; `year`, `month`, `day`, `day_of_week` (`Weekday`), `day_of_year`, `iso_week`, `iso_week_year`, `days_in_month`, `is_leap_year`; `<` | `add_months(n)`, `add_years(n)` (`none`, `month-end`), `add(period)` |
| `CivilYearMonth` | `add_months`, `add_years`; `<` | |
| `CivilTime` | `<` | |
| `CivilDateTime` | `add_days/weeks/hours/minutes/seconds`, `add_months(n, MonthEnd.CONSTRAIN)`, `date`, `time`, `in_zone`, `at_offset`; `<` | `add_months(n)`, `add(period)` |
| `FixedOffsetDateTime` | `to_instant`, `date_time`, `offset_seconds`; `<` (by instant) | |
| `ZonedIntent` | `add_days/weeks/hours/minutes/seconds` (wall clock; drops an asserted offset), `date_time`, `zone_id`, `asserted_offset_seconds` | `resolve(ctx[, policy])`, `add_months(n)`, `add(period)`; order with `ctx.compare(a, b)` |
| `Instant` | `add(PosixSeconds)`, `until`, `at_offset`, `unix_seconds`, `nanoseconds` (floor), `sub_second` (exact `Fraction`); `<` | `in_zone(id, ctx)`, `add(ElapsedSeconds)` (`needs-leap-seconds`) |
| `Period`, `PosixSeconds`, `ElapsedSeconds` | components, `negate` | |
| `Interval[T]` | `contains(point)`, `overlaps(other)` → `Truth.DEFINITELY / POSSIBLY / IMPOSSIBLE`; `start`, `end` | |

The overloads are typed, so a type checker knows that `date.add_months(1)` is an `Outcome[CivilDate]`
and `date.add_months(1, MonthEnd.CONSTRAIN)` a `CivilDate`. The policy form accepts only `CONSTRAIN`:
passing `MonthEnd.REJECT` raises `ValueError`, because rejecting is what the call without a policy does.
`add(period, month_end)` always returns an outcome and takes either policy.

### Outcomes

`Outcome[T]` is `Unique[T] | Multiple[T] | NoneOf[T] | Unsupported[T] | Invalid[T]` (`None` is a
keyword, hence `NoneOf`). Each is a frozen dataclass with `diagnostics` and `__match_args__`:

```python
match meeting.resolve(ctx):
    case Unique(value=instant): ...           # exactly one answer
    case Multiple(values=instants): ...       # a DST overlap: both, ascending (ambiguous)
    case NoneOf(diagnostics=why): ...         # a DST gap: it never happened (gap)
    case Unsupported() | Invalid(): ...       # no rules for it, or it contradicts them
```

With `match` and `typing.assert_never`, a type checker makes you handle every case. The helpers never
pick an answer silently: `unique_or(fallback)`; `single()`, which raises `OutcomeError` for anything
but `Unique`; `status` (the wire string); `codes`; `to_result()` (the 0.2 `Result` envelope); and
`str(outcome)`, which is the value's text or, for example, `none (month-end)`.

### Policies and the context

```python
ctx = TemporalContext(ZoneRules.load("conformance/rules/demo-zones.json"))   # from the repository root
meeting = CivilDateTime.of(2027, 6, 1, 9, 0).in_zone("Europe/Paris")
starts = meeting.resolve(ctx).single()                                   # same as ctx.resolve(meeting)
starts.in_zone("America/New_York", ctx).single()   # 2027-06-01T03:00:00-04:00[America/New_York]

gap = CivilDateTime.of(2026, 3, 8, 2, 30).in_zone("America/New_York")
gap.resolve(ctx)                                             # NoneOf (gap)
gap.resolve(ctx, ResolutionPolicy(gap=Gap.SHIFT_FORWARD))    # Unique: the instant labelled 03:30
```

- `MonthEnd.REJECT / CONSTRAIN`, `Gap.REJECT / PRESERVE_UNRESOLVED / SHIFT_FORWARD`,
  `Overlap.REJECT / ALL / EARLIEST / LATEST`, and `ResolutionPolicy(gap, overlap)` with
  `ResolutionPolicy.STRICT` (the default) and `ResolutionPolicy.ALL_CANDIDATES`. Each parameter also
  accepts the wire string (`"constrain"`, `"shift-forward-by-gap"`), typed as a `Literal`.
- `TemporalContext(rules)` (or `TemporalContext.with_rules(rules)`) offers `resolve`, `in_zone`,
  `compare` (an `Outcome[Ordering]`; `Multiple` when the order depends on which side of an overlap is
  meant) and `rules`. `ZoneRules.load(path)` reads a file, `ZoneRules.parse(text)` parses text.

### Native interop

`utv.from_native(value, intent)` turns a `date`, `time`, `datetime` or `timedelta` into a typed value,
and `utv.to_native(value, tz=..., allow_loss=...)` goes back. Both return outcomes and follow the
rules of `utv.native` below: the caller names the intent, a naive `datetime` is never read as local
time, and lossy conversions are refused or reported (`precision-loss`, `zone-dropped`).

### Diagnostic codes

Codes are shared by every UTV library (library-api §5): `syntax`, `malformed-value`, `day-out-of-range`,
`month-out-of-range`, `field-out-of-range`, `offset-out-of-range`, `reversed-interval`,
`kind-mismatch`, `offset-mismatch`, `month-end`, `gap`, `ambiguous`, `leap-second`, `end-of-day`,
`needs-leap-seconds`, `needs-zone-rules`, `unsupported-form`, `unsupported-annotation`,
`unsupported-operand`, `unsupported-calendar`, `unsupported-policy`, `no-zone`, `unknown-zone`,
`outside-rule-window`, `different-zones`, `no-text-form`, `implementation-limit`, `local-zone`,
`intent-required`, `out-of-native-range`, `precision-loss`, `zone-dropped`,
`offset-assertion-dropped`. Messages are free text. JSON of the wrong shape is `malformed-value`;
when structural validation (0.1) found it, its own codes (`duplicate-key`, `unknown-kind`,
`unknown-property`, …) follow. `validate_structural` itself still reports only the 0.1 codes.

## The JSON layer

The typed layer calls these functions underneath; they remain the wire boundary and what the shared
fixtures test. Values are plain JSON dicts and every function returns a `Result` (`status`,
`diagnostics`, `candidates`; `result.value` is the single candidate of a `unique` result). They never
raise for semantic outcomes; wrong Python types raise `TypeError`.

| Module | Functions |
|---|---|
| `utv.validate`, `utv.gregorian` (0.1) | `validate_structural`, `validate_gregorian_date` |
| `utv.build` | `date`, `year_month`, `time`, `date_time`, `in_zone`, `with_offset`, `instant`, `period`, `posix_seconds`, `elapsed_seconds`, `interval`, `UNBOUNDED`, `UNKNOWN` |
| `utv.iso` | `parse_iso`, `format_iso` |
| `utv.text` | `parse_text`, `format_text` (the text form), `seconds_display` |
| `utv.ops` | `fields`, `add`, `compare`, `days_between`, `posix_seconds_between`, `contains`, `overlaps` |
| `utv.zones` | `load_rules`, `parse_rules`, `ZoneRules`, `resolve`, `to_zone`, `to_offset` |
| `utv.native` | `Intent`, `from_date`, `from_time`, `from_datetime`, `from_timedelta`, `to_date`, `to_time`, `to_datetime`, `to_timedelta` |

- **Validation (0.1).** `validate_structural(text)` checks UTV JSON against
  `schema/utv-1.0.schema.json` (a bundled copy in `src/utv/data/`, kept identical by a test): duplicate
  keys and `NaN` are `invalid`, `schema` must be exactly `utv/1.0` (another well-formed version is
  `unsupported`), and unknown kinds and properties are `invalid`. `validate_gregorian_date(text)` checks
  the `year`/`month`/`day` of a civil value.
- **Everyday API (0.2).** As in [`docs/everyday-api.md`](../../docs/everyday-api.md), including its
  clarifications: `add` drops `offsetAssertion` (`offset-assertion-dropped`); `resolve` filters by
  `offsetAssertion` before the gap/overlap policy; `parse_iso` treats any annotation but one
  `[Area/Location]` as `unsupported`; dates relate only when all are unzoned or share a zone.
  `resolve` records the rule set in `manifest.dependencies`.
- **Text form.** `parse_text` / `format_text` implement [`docs/text-form.md`](../../docs/text-form.md):
  ISO 8601 / RFC 9557 points, ISO durations, bracket intervals with explicit endpoint state, and EDTF
  slash intervals on input. A value with no text form gives `unsupported` (`no-text-form`).
- **Native interop.** The caller names the intent (`from_datetime(dt, Intent.INSTANT)`); a naive
  `datetime` is only ever a floating wall time; `ZoneInfo("localtime")` and unknown `tzinfo` classes are
  refused (`local-zone`); conversions back give `unsupported` for years outside 1–9999 or
  sub-microsecond values unless `allow_loss=True`. A zoned value needs `tz=ZoneInfo(<same id>)`.

## Examples

[`examples/everyday.py`](examples/everyday.py) is the main example: 14 sections and 18 tasks (now, a
date, invalid dates, adding, days between, times, parsing with offsets, time zones, DST gap, DST
overlap, timestamps, durations vs periods, ranges, round trip). Each task does the ordinary thing with
`datetime`/`zoneinfo`, then with UTV, as plain code you can copy; the comments show what it prints.
A value too long for its column puts its note on the next line, so rows stay aligned within 100
columns. Rows marked `‡` depend on the machine (the clock, its tz database or the Python version). Without a tz
database the Python rows that need one are skipped; the UTV rows never change, because UTV uses only
`conformance/rules/demo-zones.json`. Each task sits between `# BEGIN <task-id>` and `# END <task-id>`.

[`examples/traditional.py`](examples/traditional.py) builds two dates by hand and walks the shared
catalog [`examples/traditional.json`](../../examples/traditional.json): 37 traditional tasks, each with
the outcome every UTV library must report. It exits with status 1 if any outcome differs.

## Tests

```
PYTHONPATH=src python3 -m unittest discover -s tests
```

They read the shared files from the repository root (never copies):

- `conformance/operations/everyday.json`: every fixture, checking status, candidates and the codes in
  `expect.diagnostics` (`tests/test_everyday_fixtures.py`);
- `conformance/operations/text-form.json`: every fixture, through `parse_text` / `format_text`
  (`tests/test_text_form.py`);
- `conformance/examples/*.json` and `examples/traditional.json`.

`tests/test_typed.py` covers each row of the table above, outcomes, policies, the context, round trips
(`parse(str(v))` and `from_json(to_json(v))`) and constructor checks. `tests/test_examples.py` runs
`examples/everyday.py` and compares its output with
[`examples/everyday.expected.txt`](examples/everyday.expected.txt), skipping rows that end in `‡`;
it also runs it with no tz database. After an intended change to the example, record the snapshot
again with `PYTHONPATH=src python3 examples/everyday.py > examples/everyday.expected.txt`. Other tests
cover the native adapters and check that library code never reads the clock, time zone or locale.

## What is not implemented

Everything else: expand, equivalent and the other operations of `docs/operations.md`, other
calendars, `elapsed` (SI) arithmetic, leap seconds, recurrences, EDTF qualifiers and canonical
round-tripping. Such requests return `unsupported`.
