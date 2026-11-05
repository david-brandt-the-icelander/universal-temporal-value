# utv-core (Java)

- **Library version:** 0.1.0 (`net.icelander.utv:utv-core:0.1.0`), Java 17+
- **Specification versions read:** `utv/1.0`. Results use `utv-result/1.0`. Writing is not implemented yet.
- **Conformance profiles claimed:** none yet. This is a first increment.
- **Registries shipped:** none.

## What is implemented

- `Utv.validateStructural(String)` parses JSON with Jackson `STRICT_DUPLICATE_DETECTION`, so duplicate keys are rejected. It then checks the envelope:
  - `schema` must be exactly `utv/1.0`. Any other well-formed `utv/M.N` (matching `^utv/(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)$`, including newer minors and other majors such as `utv/2.0`) gives `unsupported`. A missing, non-string or malformed identifier (for example `utv/4` or `utv/1`) gives `invalid`.
  - Unknown top-level properties and any `kind` outside the 26 core kinds give `invalid`.
  - For each kind, it checks the value properties the schema requires and rejects unknown ones (generated from `schema/utv-1.0.schema.json`). It also checks the `interval` construction requirements and the kind of `series` samples.
- `GregorianCivil.validate(String)` runs structural validation and then checks proleptic Gregorian civil `date` and `datetime` values: month and day ranges, the leap-year rule (Feb 29 only in leap years), and time-of-day ranges. Years are arbitrary-precision integer strings.
- `UtvResult` holds a `utv-result/1.0` result with a `Status` (`invalid`, `unsupported`, `indeterminate`, `none`, `unique`, `multiple`, `range`, `symbolic`, `resource-limit`, `error`, `valid`) and `Diagnostic`s (`{code, severity, path, message}`).

Structural validation is **partial**. It does not yet enforce nested value types, enums, `$ref` sub-schemas or `oneOf` alternatives beyond the rules listed above.

Everything else (resolve, compare, add, convert, expand, other calendars, zones, recurrence and so on) is not implemented and returns `unsupported`. `GregorianCivil.validate` returns `unsupported` for any value that is not a proleptic Gregorian civil date or datetime.

The library never reads the system clock, time zone, locale or network.

## Testing

```
cd implementations/java && mvn test
```

The tests read `conformance/examples/valid.json` and `conformance/examples/invalid-structural.json` from the repository root, relative to this directory.
