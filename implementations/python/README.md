# utv (Python)

- Library version: **0.1.0**
- Specification version read: **`utv/1.0`** (results use `utv-result/1.0`)
- Python 3.10+, standard library only. No clock, time zone, locale, tzdb or network access.
- Conformance profiles claimed: none yet. Registries shipped: none.

## What is implemented

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
  `year`/`month`/`day` fields of a `civil` value in the proleptic Gregorian calendar
  (Feb 30 is invalid; Feb 29 exists only in leap years; arbitrary-size years).
  It does not check zones, offsets or time-of-day fields.
- `Result`, `Status` and `Diagnostic` (`{code, severity, path, message}`) model the
  `utv-result/1.0` envelope.

## What is not implemented

Everything else: resolve, compare, add, convert, expand, equivalent and all other
operations, other calendars, zones, recurrences and canonical round-tripping.
Such requests, and semantic validation of any kind other than Gregorian `civil` dates,
return `unsupported`.

## Tests

```
PYTHONPATH=src python3 -m unittest discover -s tests
```

The tests read `conformance/examples/valid.json` and
`conformance/examples/invalid-structural.json` from the repository root.
