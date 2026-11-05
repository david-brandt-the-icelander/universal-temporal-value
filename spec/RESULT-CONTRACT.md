# UTV 1.0 — Semantic evaluation result contract

This is a normative API/JSON contract for semantic operations. It is deliberately independent of any programming language.

## Result envelope

```json
{
  "schema":"utv-result/1.0",
  "operation":"resolve",
  "status":"multiple",
  "inputDigest":"sha256:...",
  "candidates":[{"kind":"instant","value":{"system":"...","ticks":"..."}}],
  "cardinality":{"type":"finite","count":"2"},
  "manifest":{"dependencies":[{"system":"iana-tzdb","id":"tzdb","version":"2026x","sha256":"..."}]},
  "assumptions":[],
  "loss":{"classification":"lossless","fields":[]},
  "diagnostics":[]
}
```

The above is a **structural illustration**, with placeholders for referenced data and digest; it is not an executable fixture. `candidates` in real results MUST contain complete UTV envelopes with `schema:"utv/1.0"`. No actual version is asserted by `2026x`.

### Mandatory fields

- `schema`: `utv-result/1.0`. The result-contract version moves in lockstep with the specification version (see `VERSIONING.md`).
- `operation`: one of validate, resolve, compare, add, convert, expand, equivalent, explain, propagate-uncertainty.
- `status`: `invalid`, `unsupported`, `indeterminate`, `none`, `unique`, `multiple`, `range`, `symbolic`, `resource-limit`, `error`, or `valid` for validation.
- `manifest`: frozen rules, algorithm versions, reference datasets and effective evaluation context. Empty manifest is legal only if result is independent of external rules.
- `assumptions`: typed caller-approved and generated assumptions. Generated assumptions without caller approval must not be used for a definitive outcome.
- `loss`: `lossless`, `information-losing`, `assumption-dependent`, or `unrepresentable` with list of dropped/distorted semantic fields. Uncertainty truncation MUST be reported.
- `diagnostics`: array of `{code, severity, path, message, evidenceRefs?}`; errors that are inconsistent user data are distinct from missing runtime capabilities.

### Status invariants

| Status | Meaning | May contain candidates? |
|---|---|---|
| `invalid` | Contradiction, malformed input or failed semantic invariant | No authoritative candidates |
| `unsupported` | No installed registered algorithm or necessary immutable rules | No authoritative candidates |
| `indeterminate` | Semantics defined but evidence insufficient or contested | Optional possibilities, not asserted exhaustive |
| `none` | Complete valid query proven to have empty result | Empty only |
| `unique` | Exactly one proven result | Exactly one |
| `multiple` | More than one proven result | May be partially enumerated with cardinality completeness flag |
| `range` | Continuum/interval of possible answers | Symbolic bound representation |
| `symbolic` | Expression unevaluated or context not bound | Symbolic expression |
| `resource-limit` | Incomplete evaluation due to budget | Incomplete partial results flagged |
| `error` | Operational fault | No asserted semantic conclusion |
| `valid` | Validation successful, not necessarily resolvable | No need for candidates |

`indeterminate` is not `unsupported`; `unsupported` means the engine lacks capability, `indeterminate` means even a capable engine cannot derive the unique answer from information available. `none` is only allowed after a complete proof within the declared domain.

### Manifest and cache semantics

Each `manifest` has immutable dependency snapshots or explicit floating bindings and `asOf` evidence; a floating result MUST record actual resolved data release and complete dependency digests used in that evaluation. Context-dependent `now` is captured as an observation with its quality. A cache key covers input semantic digest, operation, normalized policy, rule manifest, algorithm versions, relevant location/clock model, and evaluation anchor. If any dependency changes, cached resolved answer is stale even when the source expression is unchanged.

### Conversion loss

- `civil zoned intent -> instant`: `information-losing` (future re-resolution intent and zone identity lost) even if unique now.
- `instant -> zoned display`: reversible only when retaining original instant+manifest, not when only formatted output survives.
- `uncertain -> single instant`: `assumption-dependent` and `information-losing`, never automatic.
- `IANA zone -> current offset`: `information-losing` because transition rules lost.
- `logical clock -> wall time`: `unrepresentable` without an explicitly calibrated protocol mapping, and mapping uncertainty retained.
- `UTC leap label -> POSIX coordinate`: potentially non-injective; output must preserve ambiguity or declare loss.

### Error and operation behavior

All operations are pure functions relative to explicit context and immutable registries. No fetching, shelling out or hidden system time during evaluation. `resource-limit` is a first-class bounded return, not a fabricated zero-answer. Infinite recurrences are lazily paginated; pagination tokens bind the same manifest and evaluation horizon. `compare` returns a set of possible relations, not a forced boolean. `equivalent` requires equality mode and may return `indeterminate`.


## Additional operations and result requirements (Part II kinds)

`operation` additionally admits `select`, `query-graph`, `check-consistency`, `match-event`, `expand-ordinal`, `sample`, `interpolate`, `aggregate`, `resample`, `stream-snapshot`, `map-standard`, and `round-trip`. `status` retains all the distinctions above. A graph may be `satisfiable` or `unsatisfiable` only as an operation-specific proof result in `result`, not silently mapped to `unique`/`none` without proof.

Every `series` query specifies sample-axis selection and whether bounds refer to time points or support intervals. Results report coverage, duplicates, missingness, ordering assumptions, interpolation/aggregation model, units and uncertainty. Every `selection` result provides a `sourceCompleteness` flag and a deterministic tie-breaking policy; an infinite source MAY return `resource-limit`.

Every `map-standard` operation returns `{sourceStandard,sourceEdition,targetFormat,fidelity,lexicalPreservation,semanticLoss,unmappedFields}`. A standards adapter cannot assert full fidelity without the applicable conformance profile and normative grammar tests.

Every result carries a `contextDigest` derived from explicit evaluation context, requested horizon and applicable rules. Canonical representation and semantics are versioned independently.
