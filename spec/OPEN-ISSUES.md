# UTV 1.0 — Open issues

UTV 1.0 has known gaps and inconsistencies. Each one needs a decision before an implementation can claim the affected profile. They were found while turning the design package into this repository, or were already flagged in the design discussion (the design history in [`.dev/decisions.json`](../.dev/decisions.json)).

None of them changes the architecture. They are formalization work.

**Precedence while an issue is open:** the specification governs *semantics*. The JSON Schema governs *serialized field names and shapes* (specification header and DECISIONS item 18). When an implementation hits one of these gaps, it MUST return `unsupported` for the affected feature. It MUST NOT invent a serialization.

Resolving an issue that adds fields or kinds is a **minor** version change (1.1). Resolving one that renames or removes fields is a **major** change. Clarifying prose without changing any serialized form or result is a **patch**. See `VERSIONING.md`.

## Where the issues are recorded

Each issue is a record under its ID in the `.dev` folder it concerns: a question waiting on a decision is in a `decisions.json` with status `open`, and missing work is a `backlog.json` item. Add new issues the same way.

| IDs | Group | Recorded in |
|---|---|---|
| OI-01 … OI-06 | Prose and schema field mismatches | [`spec/.dev/decisions.json`](.dev/decisions.json) |
| OI-07 … OI-09 | Vocabulary inconsistencies | [`spec/.dev/decisions.json`](.dev/decisions.json) |
| OI-10, OI-11 | Missing schemas (result envelope, registry entries) | [`schema/.dev/backlog.json`](../schema/.dev/backlog.json) |
| OI-12 | Executable semantic fixtures | [`conformance/scenarios/.dev/backlog.json`](../conformance/scenarios/.dev/backlog.json) |
| OI-13, OI-17, OI-18 | Adapter mappings, unnamed operations, JSON authoring form | [`spec/.dev/backlog.json`](.dev/backlog.json) |
| OI-14, OI-15 | Design questions deferred to library APIs | [`spec/.dev/decisions.json`](.dev/decisions.json) |
| OI-16 | Examples for `constraint` and `calendar-rule` | [`conformance/examples/.dev/backlog.json`](../conformance/examples/.dev/backlog.json) |
