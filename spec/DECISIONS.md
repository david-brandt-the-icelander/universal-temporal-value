# UTV 1.0 — Binding design decisions and implementation obligations

1. **One envelope, multiple semantic kinds.** 26 core kinds; recursively composable, single `schema: "utv/1.0"` envelope. No implicit conversion between domains.
2. **Nominal time is real data.** Named periods may be comparable without knowing Gregorian dates; all ordering can be partial.
3. **Temporal logic is not globally decidable.** A registry can declare algorithms and proof obligations; unknown/expensive predicates return `unsupported`, `indeterminate` or `resource-limit`, never false certainty.
4. **Separate future intentions from fixed instants.** Wall time and local legal rules are independently authoritative according to `zoneIntent`.
5. **Event-relative expressions do not create missing events.** Match identity and absence semantics are explicit, including open-world versus closed-world assumptions.
6. **Frequency constraints are not schedules.** "Three times daily" does not pick times. `pattern` describes obligations, `recurrence` describes generative occurrences; `observation`/`sample` record actual events.
7. **Selections are not recurrences.** Finite selection and tie-breaking have normative semantics; operations over unordered infinite domains may not terminate.
8. **Intervals preserve construction.** `start-end`, `start-amount` and `amount-end` are distinct; the last two require arithmetic policies; calendar add/subtract may be non-invertible.
9. **Uncertainty is structural.** Possible relation sets, error bounds and correlated clock errors must survive transformations.
10. **Sample support differs from sample timestamp.** `phenomenonTime`, `resultTime`, `validTime`, `samplingTime`, processing time are independent fields.
11. **Time series are UTV collections, not simply arrays of timestamped numbers.** Irregularity, duplicates, missingness, and multiple temporal axes are first-class. Interpolation and aggregation are explicit, separate, rule-defined interpretations.
12. **Recurrence fidelity is edition-specific.** RFC 5545 errata, RFC 7529 BYxxx/SKIP ordering, and RFC 8984 override identity are mandatory for the respective conformance claims.
13. **No universal exact duration of a civil day, month or year.** Day boundaries, calendars, zones and timescales are rule-based.
14. **No global physical clock.** Proper-time, geological, simulated, media and logical domains are registered reference systems, not disguised Unix timestamps.
15. **All mutable knowledge is versioned.** Rule bundles, geometry, ephemerides, clock calibration and historical assertions have independent provenance/version contracts.
16. **Signature/reproducibility require immutable content, not a version name.** Frozen sources are hashed and retained; live streams carry consistency and watermark semantics.
17. **Loss-aware adapters.** Structural expressibility does not imply complete round-trip compatibility.
18. **Explicit schema limitations.** Draft 2020-12 validates shapes only; calendar correctness, graph consistency, registration, cross-field arithmetic and standard adapters require semantic conformance tests.
19. **Partial or unknown fields aren't magic placeholders.** Unknown, unspecified, wildcard, approximate, invalid, unresolved and open-ended are different states.
20. **No one can promise literal temporal conceptual exhaustiveness.** The standard supplies a broad core and a typed, versioned extension/registry protocol capable of representing new domains without pretending their algorithms already exist.

## API design requirements

- Construction: parse/source-format, build native value, structural validate, semantic validate.
- Interpretation: resolve/context, list all candidates, explain rules/assumptions and uncertainty.
- Relations: `compare`, interval algebra, point/interval topology, graph consistency and entailment.
- Transformation: arithmetic with policies, calendar conversion, zone and physical-frame conversion, temporal selection and recurrence expansion.
- Series: ingest immutable sample snapshots, aggregate, resample, interpolate, query support intervals, and report coverage/error.
- Provenance: append/correct assertions and resolve valid-time versus known-at-time queries.
- Interchange: versioned adapters with explicit `lossless`, `lossy` or `unrepresentable` outcome, canonicalization and lexical retention.
- Execution: deterministic manifest, paging, cancellation/budgets and exhaustive-vs-partial cardinality in result.
