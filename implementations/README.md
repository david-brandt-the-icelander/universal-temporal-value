# Implementations

Language libraries for UTV live here, one directory per language:

```
implementations/
  python/
  typescript/
  rust/
  …
```

No implementation exists yet. This file sets out the conventions every library must follow, so that they can grow independently and still agree with each other.

## Layout of a language directory

Each `implementations/<language>/` directory is a self-contained project in that ecosystem's normal layout. It also contains:

- `README.md`, which states:
  - the library version,
  - the **specification versions** it reads and writes (for example `utv/1.0`),
  - the **conformance profiles** it claims (`core-civil`, `global-instant`, `zoned`, `recurrence`, `history`, `scientific`, `distributed`, `business`, `full`; spec §2),
  - which registries (tzdb, leap-second table, calendars, …) it ships or can load.
- `CHANGELOG.md` for the library's own versions.
- A test target that runs the shared conformance material (below).
- Two example test suites, *UTV first* and *native first*, covering the scenarios of
  [`docs/example-tests.md`](../docs/example-tests.md). Examples are unit tests, never separate programs, and every
  test follows [`AGENTS.md`](../AGENTS.md).

Libraries are versioned independently with SemVer and tagged `<language>/vX.Y.Z`; see `VERSIONING.md`.

## Shared conformance material

Don't copy the fixtures into a library. Read them from the repository root so every implementation tests against the same files:

| Path | What to check |
|---|---|
| `schema/utv-1.0.schema.json` | Structural validation should agree with this schema. |
| `conformance/examples/valid.json` | Every value parses, passes structural validation and round-trips to canonical JSON without loss. |
| `conformance/examples/invalid-structural.json` | Every value is rejected as `invalid`. A newer or unknown version identifier is rejected as `unsupported`. |
| `conformance/scenarios/*.json` | Semantic scenarios. Each one needs a test asserting the specified outcome. A test that returns `unsupported` for an unimplemented feature passes only if that feature is outside the library's claimed profiles. |

When an implementation finds a scenario too vague to test, the fix belongs in `conformance/` (and possibly `spec/OPEN-ISSUES.md`), not in a library-local reinterpretation.

## Non-negotiable obligations

These come from `spec/DECISIONS.md` and `spec/RESULT-CONTRACT.md`. They apply to every language:

1. **Keep structural validity, semantic validity and resolvability separate.** They are three separate checks with three separate results.
2. **No hidden environment.** Never read the system clock, host time zone, locale, installed tzdb or the network during evaluation. "Now" is an explicit input, captured as an `observation`.
3. **Rules are explicit, versioned, content-addressed dependencies.** Every nontrivial result carries a manifest of the exact rule data used.
4. **Report the truth, including truthful non-answers.** `none`, `multiple`, `indeterminate`, `unsupported` and `resource-limit` are successful outcomes. Never replace them with a guess or a default.
5. **No silent conversions** between civil, physical, clock, logical and calendar domains. Every conversion returns a loss report.
6. **Arbitrary precision.** Integers are decimal strings and fractions are exact rationals. Don't round-trip through floating point or fixed-width integers.
7. **Fail closed** on unknown kinds, unknown core properties, unknown critical extensions, duplicate JSON keys and unknown specification versions.
8. **Bounded evaluation.** Recurrence expansion, constraint solving and series processing take explicit budgets and return `resource-limit` when they run out.

## Idiomatic APIs

Libraries should feel native to their language: builders, operator overloads where they are safe, and the language's usual error handling. They must not expose a convenience that silently breaks an obligation above. For example, a `<` operator on two UTV values must not exist unless it can return "unknown". Prefer an explicit `compare()` that returns a set of possible relations.
