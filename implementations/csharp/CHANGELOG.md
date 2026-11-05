# Changelog

## Unreleased

- Added `examples/Traditional`, a runnable tour of traditional date/time tasks
  (`dotnet run --project examples/Traditional`) that checks every entry of the shared catalog
  `examples/traditional.json`. `dotnet test` now checks the catalog as well. No library changes.

## 0.1.0

- First increment: strict JSON parsing with duplicate-key detection at every depth, `utv/1.0` envelope and
  full structural validation against the embedded `schema/utv-1.0.schema.json`, the `utv-result/1.0` result
  type (`UtvStatus`, `Diagnostic`, `UtvResult`), and proleptic Gregorian `civil` date validation.
