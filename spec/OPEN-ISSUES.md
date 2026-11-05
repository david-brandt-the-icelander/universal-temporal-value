# UTV 1.0 — Open issues

This file lists known gaps and inconsistencies in UTV 1.0. Each one needs a decision before an implementation can claim the affected profile. They were found while turning the design package into this repository, or were already flagged in the design discussion (`docs/design-history.md`).

None of them changes the architecture. They are formalization work.

**Precedence while an issue is open:** the specification governs *semantics*. The JSON Schema governs *serialized field names and shapes* (specification header and DECISIONS item 18). When an implementation hits one of these gaps, it MUST return `unsupported` for the affected feature. It MUST NOT invent a serialization.

Resolving an issue that adds fields or kinds is a **minor** version change (1.1). Resolving one that renames or removes fields is a **major** change. Clarifying prose without changing any serialized form or result is a **patch**. See `VERSIONING.md`.

## A. Prose and schema field mismatches

| ID | Area | Specification says | Schema 1.0 has | Notes |
|---|---|---|---|---|
| OI-01 | `civil` (§4) | `precision`, `interpretation` | `quality`, `location` | Decide whether `precision`/`interpretation` belong inside `quality`, or are missing properties. |
| OI-02 | `civil` (§4) | `zoneRuleBinding` (`latest-at-evaluation`, `pinned`, `as-known-at`) | Only on `recurrence`, and without an enum | Needed on single civil values for future appointments. |
| OI-03 | `interval` (§6, §22) | `endpointSemantics`, `ruleBinding`, `granularity` | Per-boundary `state`, plus `empty`, `construction`, `amount`, `arithmeticPolicy` | Per-boundary `state` covers `endpointSemantics`. `ruleBinding` and `granularity` are missing. |
| OI-04 | `assertion` (§11) | `assertedTemporalValue`, `recordedAt`, `effectiveValidity`, `supersedes`, correction reason, authority, confidence, evidence | `claimedTime`, `recordedAt`, `source` (string) | Bitemporal correction cannot be fully serialized yet. `claimedTime` is the 1.0 name for `assertedTemporalValue`. |
| OI-05 | `event-history` (§11) | Revisions are assertions with immutable IDs | `revisions` is an array of any UTV value | Should be constrained to `assertion` values. |
| OI-06 | Many kinds | Rule-bound objects (policies, predicates, arithmetic policies) | Open `{"type":"object"}` | Dependent field constraints are not yet formalized (design-history: v3 qualification). |

## B. Vocabulary inconsistencies

| ID | Issue |
|---|---|
| OI-07 | Loss classification uses three vocabularies. Spec §2 uses `lossless`/`lossy`/`unrepresentable`. RESULT-CONTRACT uses `lossless`/`information-losing`/`assumption-dependent`/`unrepresentable`. STANDARDS-MATRIX uses six fidelity levels. These should become one vocabulary for `loss.classification` plus a separate one for adapter fidelity. |
| OI-08 | FALSEHOODS-MATRIX lists `lossy` as an expected test status. `lossy` is a loss classification, not a result `status`. |
| OI-09 | Conformance-profile names in §2 (`core-civil`, `zoned`, …) and the profile families in §27 item 6 (data expression, reasoning, storage, interoperability, scientific) are not yet one taxonomy. |

## C. Missing machine-readable artifacts

| ID | Missing artifact |
|---|---|
| OI-10 | A JSON Schema for the `utv-result/1.0` result envelope (RESULT-CONTRACT). |
| OI-11 | A JSON Schema for registry entries and rule manifests (§16). |
| OI-12 | Executable semantic fixtures. Each scenario in `conformance/scenarios/` needs concrete input values, the evaluation context and manifest, and an expected result envelope. |
| OI-13 | Field-by-field adapter mappings for RFC 5545 (+ erratum 4271), RFC 7529, RFC 8984, RFC 9557 and ISO 8601-1/-2. ISO 8601-2 grammar must be checked against the licensed normative text. |
| OI-16 | Example values for the `constraint` and `calendar-rule` kinds in `conformance/examples/valid.json`. These are the only core kinds without one. |
| OI-17 | Operations needed to answer common questions but not named in RESULT-CONTRACT: `difference`, `contains`, `fields`, `day-bounds`, `truncate`, `set-op`, `as-of`, `append-assertion`, `is-stale`, `format`, `canonicalize`/`digest`, `capabilities`. See `docs/operations.md`. Adding them is a minor change (1.1). |

## D. Design questions deferred to library APIs

| ID | Question | Current position |
|---|---|---|
| OI-14 | Should `range` be a first-class alias for `interval`, e.g. `date1..date2` syntax? | Not a wire kind: `interval` already carries the semantics. Libraries MAY offer a `range` name or constructor that produces `interval` values. |
| OI-15 | Should time series stay in core, or move to a companion model? | In core for 1.0 (`series`, `sample`). The design discussion originally preferred a companion model. Revisit if implementations show the core is too heavy. |
