# utv (Python)

- Library version: **0.2.0**
- Specification version read: **`utv/1.0`** (results use `utv-result/1.0`)
- Python 3.10+, standard library only. No clock, host time zone, locale, installed
  tzdb or network access in library code.
- Conformance profiles claimed: none yet. Registries shipped: none. Zone rules are
  an explicit input (a bundle in the format of `conformance/rules/demo-zones.json`).

## What is implemented

### Validation (0.1)

- `validate_structural(text) -> Result`: parses UTV JSON and checks it against
  `schema/utv-1.0.schema.json` (a bundled copy in `src/utv/data/`; a test keeps it
  identical to the repository file).
  - Duplicate JSON keys and `NaN`/`Infinity` are rejected (`invalid`).
  - `schema` must be exactly `utv/1.0`. Any other well-formed identifier matching
    `^utv/(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)$` (e.g. `utv/1.1`, `utv/2.0`) gives
    `unsupported`; a missing, non-string or malformed one (e.g. `utv/4`, `utv/1`) gives `invalid`.
  - Unknown kinds (outside the 26 core kinds), unknown properties and missing required
    fields, at the top level and in nested values, give `invalid`.
- `validate_gregorian_date(text) -> Result`: a separate semantic check of the
  `year`/`month`/`day` fields of a `civil` value in the proleptic Gregorian calendar.

### Everyday API (0.2)

The contract is [`docs/everyday-api.md`](../../docs/everyday-api.md). Everything is
re-exported from `utv`; values are plain JSON dicts, and every operation returns a
`Result` (`status`, `diagnostics`, `candidates`; `result.value` is the single
candidate of a `unique` result). Operations never raise for semantic outcomes;
wrong Python types raise `TypeError`.

| Module | Functions |
|---|---|
| `utv.build` | `date`, `year_month`, `time`, `date_time`, `in_zone`, `with_offset`, `instant`, `period`, `posix_seconds`, `interval`, `UNBOUNDED`, `UNKNOWN` |
| `utv.iso` | `parse_iso`, `format_iso` |
| `utv.ops` | `fields`, `add`, `compare`, `days_between`, `posix_seconds_between`, `contains`, `overlaps` |
| `utv.zones` | `load_rules`, `parse_rules`, `ZoneRules`, `resolve`, `to_zone`, `to_offset` |
| `utv.native` | `Intent`, `from_date`, `from_time`, `from_datetime`, `from_timedelta`, `to_date`, `to_time`, `to_datetime`, `to_timedelta` |

```python
import utv

jan31 = utv.date(2026, 1, 31)
utv.add(jan31, utv.period(months=1)).status  # Status.NONE: no 31 February
utv.format_iso(utv.add(jan31, utv.period(months=1), {"monthEnd": "constrain"}).value).value  # '2026-02-28'

rules = utv.load_rules("conformance/rules/demo-zones.json")  # path from the repository root
meeting = utv.in_zone(utv.date_time(2027, 6, 1, 9, 0), "Europe/Paris")
at = utv.resolve(meeting, rules).value
utv.format_iso(utv.to_zone(at, "America/New_York", rules).value).value
# '2027-06-01T03:00:00-04:00[America/New_York]'
```

Behaviour settled in the contract's "Clarifications" section, all covered by fixtures:

- `add` on a zoned value drops `offsetAssertion` (it can become false) and says so in
  an `info` diagnostic, `offset-assertion-dropped`.
- `resolve` on a value with `offsetAssertion` keeps only the candidate with that offset,
  before the gap/overlap policy; if none matches (including in a gap) the result is
  `invalid` with `offset-mismatch`.
- `parse_iso` reports any annotation other than a single `[Area/Location]` (`[u-ca=…]`,
  `[+01:00]`, `[!…]`, a second annotation) as `unsupported`.
- `days_between`, `contains` and `overlaps` relate civil dates only when all are
  unzoned or all have the same zone; otherwise `unsupported`.
- `Result.operation` is the everyday operation's name (`parseIso`, `daysBetween`, …);
  the native adapters use `convert`.

`resolve` also records the rule set in `manifest.dependencies` and reports the
civil → instant conversion as `information-losing`.

### Native interop (`utv.native`)

- **The caller names the intent.** `from_datetime(dt, intent)`:
  - naive → floating date-time (`Intent.FLOATING`, the default);
  - fixed `timezone` → `Intent.INSTANT` or `Intent.OFFSET`, required;
  - `ZoneInfo` → `Intent.ZONED` (default: `in_zone` with `offsetAssertion` taken from
    `fold`), or `INSTANT` / `OFFSET`. A wall time in a DST gap gives `none` for
    `INSTANT`/`OFFSET` instead of Python's silent shift.
  - `from_timedelta(td, intent)` needs `Intent.POSIX_SECONDS` or `Intent.PERIOD`.
- **No local zone.** A naive `datetime` is never read as local time: asking for an
  instant or offset from one is `unsupported`. So are `ZoneInfo("localtime")` and
  `tzinfo` classes other than `timezone` and `ZoneInfo`. `astimezone()` is never
  called without an argument.
- **Lossy conversions back say so.** `to_date`, `to_time`, `to_datetime` and
  `to_timedelta` give `unsupported` for years outside 1–9999 and for nanoseconds that
  aren't whole microseconds, unless `allow_loss=True`, in which case `result.loss`
  names the dropped fields. A zoned value needs `tz=ZoneInfo(<same id>)` (the library
  doesn't open the tz database itself); with `allow_loss=True` and no `tz`, only the
  asserted offset is kept and `zone` is reported lost. Without an asserted offset, a
  repeated wall time gives `multiple` (fold 0, fold 1) and a skipped one `none`.

## What is not implemented

Everything else: expand, equivalent and the other operations of
`docs/operations.md`, other calendars, `elapsed` (SI) arithmetic, leap seconds,
recurrences and canonical round-tripping. Such requests return `unsupported`.

## Everyday example

```
PYTHONPATH=src python3 examples/everyday.py
```

[`examples/everyday.py`](examples/everyday.py) is the main example: 14 sections
(now, a date, invalid dates, adding, days between, times, parsing with offsets, time
zones, DST gap, DST overlap, timestamps, durations vs periods, ranges, round trip).
Each one does the ordinary thing with `datetime`/`zoneinfo` first, then with UTV, and
notes what differs. The native side uses the host's tz database and skips itself
if a zone isn't installed; the UTV side uses `conformance/rules/demo-zones.json`. The
script checks its own UTV outcomes and exits with status 1 if any differ.

## Traditional catalog example

```
PYTHONPATH=src python3 examples/traditional.py
```

[`examples/traditional.py`](examples/traditional.py) first builds two dates by hand
(5 November 2026, and 30 February 2026, which is well-formed but doesn't exist) and
shows how a caller matches on the status and reads the diagnostics. It then walks the
shared catalog [`examples/traditional.json`](../../examples/traditional.json) at the
repository root: 37 traditional date/time tasks, each with the outcome every UTV
library must report. It exits with status 1 if any outcome differs.

## Tests

```
PYTHONPATH=src python3 -m unittest discover -s tests
```

The tests read these files from the repository root (never copies):

- `conformance/examples/valid.json`, `conformance/examples/invalid-structural.json`;
- `conformance/operations/everyday.json`: every fixture, one subtest each
  (`tests/test_everyday_fixtures.py`), with `conformance/rules/demo-zones.json`;
- `examples/traditional.json`.

They also run both examples (`tests/test_examples.py`, including a run with no tz
database), test the native adapters (`tests/test_native.py`; the `ZoneInfo` cases skip
if the host has no tz database), and check that library code never reads the clock,
time zone or locale (`tests/test_everyday_api.py`).
