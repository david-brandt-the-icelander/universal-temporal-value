# Changelog

## Unreleased

- **Examples are now unit tests**, in two suites that cover the scenarios of
  [`docs/example-tests.md`](../../docs/example-tests.md) and follow [`AGENTS.md`](../../AGENTS.md):
  - `tests/examples/utv_first/`: each task done with UTV, one module per UTV class and one
    `TestCase` per function, compared with `datetime`, `zoneinfo`, `calendar` and `json`.
    Tests of the side-by-side tasks sit between `# BEGIN <task-id>` / `# END <task-id>`.
  - `tests/examples/native_first/`: each task done with the standard library, one module per
    standard module and class, compared with UTV.
  - `tests/examples/fixtures.py` loads `conformance/rules/demo-zones.json` for both, and has a
    test of its own.
- Removed: the example programs `everyday.py` and `traditional.py`, the snapshot of the
  everyday tour's output, the tour of the shared traditional catalog and the test that ran them.
  The library is unchanged.

## 0.3.0

The typed layer of [`docs/library-api.md`](../../docs/library-api.md), on top of the
unchanged JSON layer.

- **Typed values**, frozen slotted dataclasses with value semantics: `CivilDate`,
  `CivilYearMonth`, `CivilTime`, `CivilDateTime`, `FixedOffsetDateTime`, `ZonedIntent`,
  `Instant`, `Period`, `PosixSeconds`, `ElapsedSeconds`, `Interval[T]` with `Bound[T]`
  (`T` is `CivilDate` or `Instant`), and the `Weekday` enum.
  - Constructors reject impossible values with `OutcomeError`, a `ValueError`
    (`CivilDate.of(2026, 2, 30)`); `try_of` returns an outcome instead.
  - `str(v)` is the text form, `repr(v)` is `CivilDate(2026-11-05)`. Second counts
    print as `3600.75 s (POSIX)`; a value with no text form prints as its JSON.
  - `to_json()` / `T.from_json()`, `T.parse()` and `utv.parse()` / `utv.from_json()`
    for any value.
- **Total operations return values; partial ones return `Outcome[T]`**: the frozen
  generic dataclasses `Unique`, `Multiple`, `NoneOf`, `Unsupported` and `Invalid`, with
  `__match_args__` for `match`, and the helpers `unique_or`, `single`, `status`, `codes`
  and `to_result`. `Truth` and `Ordering` are enums.
  - `add_months` / `add_years` return an outcome without a policy and a plain value with
    `MonthEnd.CONSTRAIN` (typed overloads, so a type checker knows which). Passing
    `MonthEnd.REJECT` to the policy form raises `ValueError`.
  - `Instant.nanoseconds` is the floor of the sub-second part; `Instant.sub_second` is exact.
  - Reversed intervals (`reversed-interval`) and intervals with no point (`unsupported-form`)
    are constructor errors.
  - `Instant.add(PosixSeconds)` returns an `Instant`; `Instant.add(ElapsedSeconds)` an
    outcome (`needs-leap-seconds`).
- **Typed policies**: `MonthEnd`, `Gap`, `Overlap` and `ResolutionPolicy` (presets
  `STRICT` and `ALL_CANDIDATES`). Each also accepts its wire string through a `Literal`
  type; dictionaries are no longer needed.
- **`TemporalContext`** holds the zone rules once: `resolve`, `in_zone`, `compare`
  (which answers `Multiple` when the order depends on a DST overlap). `ZoneRules.load`
  and `ZoneRules.parse` are new spellings of `load_rules` / `parse_rules`.
- **Typed native interop**: `utv.from_native(value, intent)` and
  `utv.to_native(value, tz=, allow_loss=)` over the `utv.native` adapters.
- **The text form** ([`docs/text-form.md`](../../docs/text-form.md)) in the JSON layer:
  `parse_text` / `format_text` (periods such as `P1M` and `-P1DT2H`, bracket intervals,
  EDTF slash intervals on input). New builder `elapsed_seconds`.
- **Diagnostic codes follow the shared table** (library-api §5). JSON of the wrong shape (missing
  or unexpected fields, malformed integers or rationals, a malformed rules bundle, a non-string
  to parse) is `malformed-value`, with any 0.1 structural codes after it. A `[zone]` after `Z`
  or `-00:00` is `unsupported-annotation`. In the text form, an interval with no point is
  `unsupported` (`unsupported-form`). Renamed, among others:
  `not-iso-8601` → `syntax`; `hour-`/`minute-`/`second-`/`nano-out-of-range` →
  `field-out-of-range`; `week-date`, `ordinal-date`, `basic-format` → `unsupported-form`;
  `critical-annotation`, `annotation` → `unsupported-annotation`; `resolve-first` →
  `needs-zone-rules`; `outside-rules` → `outside-rule-window`; `component-not-allowed`,
  `calendar-on-instant`, `wrong-amount-basis` → `kind-mismatch`; `time-wraps`,
  `mixed-domains`, `different-forms`, `not-a-datetime` and similar → `unsupported-operand`;
  in `utv.native`, `ambiguous-local-time` → `ambiguous`, `needs-zone-data` →
  `needs-zone-rules`, `sub-microsecond` → `out-of-native-range`. Statuses are unchanged.
  Lossy native conversions now also carry a `precision-loss` or `zone-dropped` warning.
- **`examples/everyday.py` rewritten** as plain, copyable code: one block per task,
  Python and UTV rows aligned (a long value puts its note on the next line), real `match` statements on outcomes, the output in
  trailing comments, and `# BEGIN <task>` / `# END <task>` markers. It no longer checks
  itself; `examples/everyday.expected.txt` is its snapshot.
- Tests: the fixture runner checks `expect.diagnostics`; new `tests/test_text_form.py`
  (all of `conformance/operations/text-form.json`), `tests/test_typed.py`, and a snapshot
  test of the example (also run with no tz database). The library and the example
  type-check with `mypy --strict`.
- Unchanged: the 0.1 and 0.2 functions and their outcomes, the traditional catalog tour.

## 0.2.0

- The everyday API (`docs/everyday-api.md`), re-exported from `utv`:
  - builders `date`, `year_month`, `time`, `date_time`, `in_zone`, `with_offset`,
    `instant`, `period`, `posix_seconds`, `interval` (with `UNBOUNDED` / `UNKNOWN`);
  - `parse_iso`, `format_iso`, `fields`, `add`, `compare`, `days_between`,
    `posix_seconds_between`, `contains`, `overlaps`;
  - `resolve`, `to_zone`, `to_offset`, with explicit rule bundles loaded by
    `load_rules` / `parse_rules` (`ZoneRules`).
- Follows the contract's "Clarifications": `add` drops `offsetAssertion`
  (`offset-assertion-dropped`); `resolve` filters by `offsetAssertion` before the
  gap/overlap policy (`invalid`/`offset-mismatch` if none matches); `parse_iso` treats
  any annotation but a single `[Area/Location]` as `unsupported`; `days_between`,
  `contains` and `overlaps` need all dates unzoned or in one zone; `operation` carries
  the everyday name.
- `Result` gains `candidates` (serialised by `to_dict()` when non-empty) and a
  `value` property for the single candidate of a `unique` result.
- Native interop (`utv.native`): `from_date`, `from_time`, `from_datetime`,
  `from_timedelta` with a caller-named `Intent`; `to_date`, `to_time`,
  `to_datetime`, `to_timedelta`, which report or refuse lossy conversions.
  Naive datetimes are never read as local time.
- `examples/everyday.py`: Python's `datetime` and UTV side by side, in 14 sections.
- Tests: `tests/test_everyday_fixtures.py` runs all of
  `conformance/operations/everyday.json`; new tests for the adapters, builders,
  purity and the example.
- 0.1 behaviour (`validate_structural`, `validate_gregorian_date`) is unchanged.
- Also in this release (previously unreleased): `examples/traditional.py`, a
  runnable tour of the shared `examples/traditional.json` catalog, and
  `tests/test_examples.py`, which checks every catalog outcome.

## 0.1.0

- First increment: envelope and structural validation for `utv/1.0`, the
  `utv-result/1.0` result type, and proleptic Gregorian date-field validation.
