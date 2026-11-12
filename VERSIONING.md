# Versioning

This repository versions two kinds of things separately:

1. **The specification**: the prose in `spec/`, the JSON Schema in `schema/` and the conformance material in `conformance/`. These move together under one version number.
2. **Language implementations** under `implementations/`. Each has its own version.

The current specification version is **1.0**.

## Specification versions

Specification versions are `MAJOR.MINOR.PATCH`. Only `MAJOR.MINOR` appears on the wire.

| Change | Example | Bump | Wire identifier changes? |
|---|---|---|---|
| Removes or renames a kind, property or enum value; changes the meaning of an existing serialized value or result status | renaming `claimedTime` | **MAJOR** (2.0) | yes: `utv/2.0` |
| Adds a kind, an optional property or an enum value; adds a result operation; resolves an open issue additively | adding `granularity` to `interval` | **MINOR** (1.1) | yes: `utv/1.1` |
| Editorial fixes, clarifications, new conformance scenarios or examples that don't change what any value means or what any operation returns | fixing a typo, adding a test vector | **PATCH** (1.0.1) | no |

### Wire identifiers

- Every UTV value carries `"schema": "utv/<MAJOR>.<MINOR>"`, for example `"utv/1.0"`.
- Every evaluation result carries `"schema": "utv-result/<MAJOR>.<MINOR>"`, for example `"utv-result/1.0"`. The result contract is versioned in lockstep with the specification.
- Nested values carry the same identifier as their outermost envelope. One value tree is never a mix of versions.

### Reader rules

UTV rejects unknown properties and kinds (fail-closed). That's why the minor version appears on the wire: a reader has to know exactly which vocabulary to validate against.

- A reader implementing `1.N` MUST accept values labelled `utv/1.M` for every `M ≤ N`, and MUST interpret them by the `1.M` rules.
- A reader implementing `1.N` that receives `utv/1.M` with `M > N` MUST return `unsupported` (an unimplemented version). It MUST NOT return `invalid`, and it MUST NOT guess at the unknown parts.
- A reader MUST return `unsupported` for any other major version it doesn't implement.
- A writer SHOULD label values with the version it implements. It MAY use a lower minor version, but only if the value uses nothing introduced after that version.

### Schema files

Each `MAJOR.MINOR` gets its own schema file, `schema/utv-MAJOR.MINOR.schema.json`, with `$id` `urn:utv:schema:MAJOR.MINOR`. Older schema files are kept unchanged when newer ones are added. PATCH releases may correct a schema file only to make it match the already-published prose. Any such correction is listed in `CHANGELOG.md`.

### Tags

Specification releases are tagged `spec/vMAJOR.MINOR.PATCH`, for example `spec/v1.0.0`. The `main` branch always holds the latest specification. Earlier versions are read from their tags.

## Implementation versions

Each library under `implementations/<language>/` follows [Semantic Versioning 2.0.0](https://semver.org/) for its own API, independently of the specification:

- Library tags are `<language>/vX.Y.Z`, for example `python/v0.1.0` or `rust/v1.2.0`.
- Each library MUST state, in its README and in its package metadata where the ecosystem allows, which specification versions (`utv/1.0`, …) and which conformance profiles it supports.
- A library MUST NOT reach `1.0.0` until it passes the structural conformance material and every semantic scenario for the profiles it claims, or returns the specified `unsupported` result for them.

## Pre-release design history

The design went through four internal drafts before publication. They were labelled v1–v4, and the later drafts used the identifiers `utv/2`, `utv/3` and `utv/4`. **None of them was released.** Specification 1.0 is the published form of the final draft (v4), with the reconciliation changes listed in `CHANGELOG.md`.

- Writers MUST NOT emit `utv/2`, `utv/3` or `utv/4`. A bare major identifier such as `utv/1` is also malformed: identifiers always carry `MAJOR.MINOR`.
- Readers MUST treat them as unknown identifiers. The structural schema rejects them.

The design history in [`.dev/decisions.json`](.dev/decisions.json) records what each draft changed and why.
