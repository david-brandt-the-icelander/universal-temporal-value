# Changelog

All notable changes to the UTV specification are recorded here. Language implementations keep their own changelogs in `implementations/<language>/`. See `VERSIONING.md` for what each kind of version bump means.

## [1.0.0] — 2026-11-05

First published version of the specification. It is the published form of the final internal design draft (v4); see the design history in `.dev/decisions.json`.

### Contents

- `spec/SPECIFICATION.md`: the core model (Part I, §1–§17) and standards convergence (Part II, §18–§27), covering 26 core kinds.
- `spec/DECISIONS.md`, `spec/RESULT-CONTRACT.md`, `spec/STANDARDS-MATRIX.md`, `spec/FALSEHOODS-MATRIX.md`.
- `schema/utv-1.0.schema.json`: structural JSON Schema (Draft 2020-12).
- `conformance/examples/`: 31 structurally valid and 8 structurally invalid values.
- `conformance/scenarios/`: 62 falsehood scenarios and 32 standards-convergence scenarios (design requirements, not yet executable).
- `spec/OPEN-ISSUES.md`: known gaps to resolve in later versions.

### Changes from the internal v4 draft

- Version identifiers were renumbered: `utv/4` became `utv/1.0`, and `utv-result/2` became `utv-result/1.0`. The schema `$id` is now `urn:utv:schema:1.0`.
- The draft's "v3 baseline + v4 supplement" was restructured as Part I and Part II of one document. The precedence rule (Part II governs on conflict) is unchanged.
- Specified the wire-identifier format and reader behaviour for unknown versions (§2, `VERSIONING.md`).
- Scenario IDs `V4-001`…`V4-032` were renamed `STD-001`…`STD-032`.
- Fixed §19's `ordinal-position` example, which didn't conform to the schema's `ruleRef` shape.
- Fixed §22's boundary state names (`inclusive`/`exclusive`) to match §6 and the schema (`included`/`excluded`).
- Schema fix: an `interval` using `amount-end` construction must now state `"construction": "amount-end"`. Previously, a value with `start`, `end` and `amount` and no discriminator matched two `oneOf` branches and was rejected for the wrong reason.
- Added a structurally invalid example that uses the unreleased `utv/4` identifier.
