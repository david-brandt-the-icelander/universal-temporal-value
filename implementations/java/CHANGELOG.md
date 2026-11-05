# Changelog

## Unreleased

- Added the runnable example `net.icelander.utv.examples.Traditional` (`mvn -q test-compile exec:java`). It tours the shared catalog `examples/traditional.json`. The example lives in `src/examples/java` as a test source root, so it isn't in the library jar.
- `mvn test` now checks every expectation in `examples/traditional.json` (`TraditionalTest`).
- `GregorianCivil.validate` now also checks the `year` and `year-month` forms (year must be an integer string, month 1..12) instead of returning `unsupported`, matching the other libraries and the shared catalog.

## 0.1.0

- First increment: envelope and per-kind structural validation for `utv/1.0`, the `utv-result/1.0` status and diagnostic types, and proleptic Gregorian civil date validation.
