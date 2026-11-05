# Changelog

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
