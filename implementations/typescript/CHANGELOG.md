# Changelog

## 0.3.0 (unreleased)

Implements [`docs/library-api.md`](../../docs/library-api.md) (typed layer) and [`docs/text-form.md`](../../docs/text-form.md).

- **Typed values** (`src/values.ts`): `CivilDate`, `CivilYearMonth`, `CivilTime`, `CivilDateTime`,
  `FixedOffsetDateTime`, `ZonedIntent`, `Instant`, `Period`, `PosixSeconds`, `ElapsedSeconds`, `Interval<T>` with
  `Bound<T>`. Private constructors and static factories (`of` throws a `RangeError`, `tryOf` returns an `Outcome`),
  `toString()` in the text form, `toJSON()` in UTV JSON, `equals()`, `compare()` / `isBefore()` / `isAfter()` where
  the order is defined, `parse` and `fromJson` returning outcomes, and a `CivilDate(2026-11-05)` inspect view.
  Every operation calls the JSON layer underneath.
- **Total vs partial**: total operations return plain values; partial ones (`addMonths`, `resolve`, `inZone`,
  `add(ElapsedSeconds)`, parsing, JSON import) return `Outcome<T>`. Overloads make `addMonths(n, MonthEnd.Constrain)`
  return a plain value.
- **`Outcome<T>`** (`src/outcome.ts`): a union of `Unique`, `Multiple`, `None`, `Unsupported` and `Invalid`
  discriminated on `status`, with `assertNever` for exhaustive switches, `uniqueOr`, `single` (throws `OutcomeError`),
  `toString` and `toResult`. Also `Truth`, `Ordering` and the ISO `Weekday` enum.
- **Policies and context**: typed `MonthEnd`, `Gap`, `Overlap` and `ResolutionPolicy` (`Strict`, `AllCandidates`,
  `of`); `TemporalContext.with(rules)` with `resolve`, `inZone` and `compare`; `ZoneRules.load(path)` and
  `ZoneRules.parse(text)`.
- **Text form** (`src/text.ts`): `parseText` / `formatText` in the JSON layer: points, ISO 8601 periods, bracket
  intervals with `..` and `?`, and EDTF slash intervals on input. `test/text-form.test.ts` runs every fixture in
  `conformance/operations/text-form.json`.
- **Diagnostic codes** follow the shared table (library-api §5). Renamed, among others: `invalid-syntax` → `syntax`;
  `field-out-of-range` → `day-out-of-range` / `month-out-of-range` / `offset-out-of-range` where it applies;
  `resolve-first` → `needs-zone-rules`; `outside-rules-window` → `outside-rule-window`; `mixed-zones` →
  `different-zones`; `host-zone-refused` → `local-zone`; `out-of-range` → `out-of-native-range`; the `wrong-kind`,
  `unsupported-kind`, `incomparable`, `mixed-domains`, `time-wraps` and similar codes → `unsupported-operand` or
  `kind-mismatch`. `toDate` / `toEpochMilliseconds` now add `precision-loss` and `zone-dropped` warnings.
  The everyday fixture runner checks `expect.diagnostics`.
- **Example**: `examples/everyday.ts` rewritten as plain code (no harness): one aligned row per result, output as
  trailing comments, real `switch`es on outcomes, `‡` for host-dependent rows and `// BEGIN` / `// END` task
  markers. `test/everyday-example.test.ts` compares its output with `examples/everyday.expected.txt`
  (`npm run everyday:snapshot` records it).

## 0.2.0 (unreleased)

- Everyday API (`docs/everyday-api.md`): builders `date`, `yearMonth`, `time`, `dateTime`, `inZone`, `withOffset`,
  `instant`, `period`, `posixSeconds`, `interval`; operations `parseIso`, `formatIso`, `fields`, `add`, `compare`,
  `daysBetween`, `posixSecondsBetween`, `contains`, `overlaps`, `resolve`, `toZone`, `toOffset`; `loadRules` for
  rule bundles in the `conformance/rules/demo-zones.json` format.
- `UtvResult` gains `candidates`, a free `operation` name and manifest dependencies; `makeResult` takes options;
  `candidate(result)` helper.
- Native interop: `fromDate` (caller names the intent; `"local"` is refused), `toDate`, `fromEpochMilliseconds`,
  `toEpochMilliseconds`, `fromPlainDate`, `toPlainDate`, with loss reporting.
- `test/everyday.test.ts` runs every shared fixture in `conformance/operations/everyday.json`.
- `examples/everyday.ts` (`npm run everyday`): `Date`/`Intl` and UTV side by side, fourteen sections.

## 0.1.0 (unreleased)

- Strict duplicate-key-rejecting JSON parser.
- `utv/1.0` envelope and per-kind structural validation returning `utv-result/1.0` results.
- Proleptic Gregorian `civil` date semantic validation.
- `examples/traditional.ts` (`npm run example`): a tour of traditional date and time tasks from the shared
  `examples/traditional.json` catalog; `npm test` also checks every catalog outcome.
