# Changelog

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
