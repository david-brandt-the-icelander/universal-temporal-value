# Operations: the questions people actually ask of time

The specification lists operations (spec §13, `spec/RESULT-CONTRACT.md`, `spec/DECISIONS.md` "API design requirements"). That list grew out of the design. This document works in the other direction: it starts from the questions real programs ask about dates and times and checks that each one has an operation that can answer it honestly. It then lists the questions the specification doesn't cover yet.

It is language-neutral. The C# interface that implements it is in `implementations/csharp/docs/engine-interface.md`.

## Ground rules every operation follows

These come from the specification and apply to every row below.

1. **Pure and explicit.** An operation is `f(value(s), context, policy) → result`. The *context* holds everything the answer depends on: pinned rule data, the reference observation used as "now", location, budgets. Nothing comes from the host machine.
2. **The result is a `utv-result/1.0` envelope.** That means a status and candidates, plus cardinality, manifest, assumptions, a loss report and diagnostics. The status vocabulary is the same for every operation, so callers handle `none`, `multiple`, `indeterminate` and `unsupported` in one place.
3. **Questions with yes/no wording get three-valued answers.** "Is A before B?" answers `definitely`, `possibly` or `impossible`, or comes back with status `indeterminate` or `unsupported`. Never a bare boolean.
4. **Policies are arguments, not defaults buried in the library.** Month-end handling, DST gap and overlap, holiday roll: each is passed in, or taken from a named preset that the result records.

## The question catalog

Each row gives the question in plain words, why the usual answer is wrong, the operation that answers it, and the honest outcomes. *Spec* marks whether the specification already names the operation (✓), describes it only in prose (~), or doesn't have it yet (✗; see "Gaps" below).

### A. Is this value meaningful?

| # | Question | Why the convenient answer lies | Operation | Honest outcomes | Spec |
|---|---|---|---|---|---|
| A1 | Is this well-formed UTV? | n/a | `validate-structural` | `valid`, `invalid`, `unsupported` (newer version) | ✓ |
| A2 | Does it make sense (Feb 30? 25:00?) | Libraries either reject everything odd or normalize Feb 30 to Mar 2 | `validate` (semantic) | `valid`, `invalid`, `unsupported` (calendar not installed), `indeterminate` | ✓ |
| A3 | Can this engine answer question X about this value at all? | Callers find out by catching exceptions | `capabilities` | list of supported kinds × operations × profiles × registries | ✗ |

### B. When is it?

| # | Question | Why the convenient answer lies | Operation | Honest outcomes | Spec |
|---|---|---|---|---|---|
| B1 | Which instant(s) does this local date-time denote? | Exactly one is always returned | `resolve` | `none` (DST gap), `unique`, `multiple` (overlap), `indeterminate` (unknown historical rules) | ✓ |
| B2 | What does the wall clock read at instant *t* in zone/place Z? | Offset assumed fixed, or abbreviation used | `convert` (instant → civil) | `unique`, `indeterminate` (pre-tzdb history), `multiple` (place maps to several jurisdictions) | ✓ |
| B3 | What is this date in another calendar (Hebrew, Julian, ...)? | Gregorian assumed universal | `convert` (calendar) | `unique`, `multiple` (day-boundary differences), `unsupported` | ✓ |
| B4 | What day of the week / ISO week / quarter is it? | Fine until week rules or locale differ | `fields` (derived field query) | `unique`, `unsupported` (calendar has no weeks) | ✗ |
| B5 | When does this day start and end here? | Midnight to midnight, 24 h | `day-bounds` | interval or `none` (day skipped, e.g. Samoa 2011-12-30) | ✗ |

### C. Ordering and relations

| # | Question | Why the convenient answer lies | Operation | Honest outcomes | Spec |
|---|---|---|---|---|---|
| C1 | Did A happen before B? | Numeric timestamp comparison across unsynchronized clocks | `compare` | set of possible relations (point or Allen) | ✓ |
| C2 | Do these intervals overlap / meet / contain? | Closed vs half-open confusion | `compare` (Allen) | set of the 13 relations, or `indeterminate` | ✓ |
| C3 | Is X inside interval/set S? | Endpoint inclusivity ignored | `contains` | definitely / possibly / impossible | ~ (via `compare`) |
| C4 | Has it happened yet? Is it overdue? | Reads the system clock | `compare` against an explicit `observation` of now | as C1, with the observation's uncertainty | ✓ |
| C5 | Are these the same? | `==` on timestamps conflates event identity, instant, wall time | `equivalent` with a mode (representation, semantic, timeline, schedule-intent, event identity) | definitely / possibly / impossible / `indeterminate` | ✓ |
| C6 | Could A have caused B? | Earlier timestamp taken as causation | `compare` on `logical-clock` values | happened-before / concurrent / `indeterminate` | ✓ |
| C7 | Is this set of "before/after" facts consistent? | Never checked | `check-consistency`, `query-graph` | satisfiable / unsatisfiable / `resource-limit` | ✓ |

### D. How long?

| # | Question | Why the convenient answer lies | Operation | Honest outcomes | Spec |
|---|---|---|---|---|---|
| D1 | How much time elapsed between A and B? | Subtract Unix seconds, ignoring leap seconds and clock domains | `difference` (basis `elapsed`) | exact rational seconds, `range` (uncertain endpoints), `unsupported` (no leap table) | ✗ |
| D2 | How many months/days between two dates? How old is she? | Elapsed seconds ÷ 86,400 | `difference` (basis `calendar`, with policy) | components, possibly `multiple` (month-end ambiguity) | ✗ |
| D3 | How many business days/hours between? | Weekends only | `difference` (basis `business`) | count, `unsupported` (no holiday rules) | ✗ |
| D4 | How long is this interval? | Assumes a single basis | `difference` on the interval's endpoints | as D1–D3 | ✗ |

### E. Calendar arithmetic

| # | Question | Why the convenient answer lies | Operation | Honest outcomes | Spec |
|---|---|---|---|---|---|
| E1 | What is A plus one month / minus a day? | Silent clamping, assumed invertible | `add` with arithmetic policy | `unique`, `none` (`reject` month-end), `multiple`, plus loss report | ✓ |
| E2 | What is the next business day / end of month / start of quarter? | Hard-coded weekends, no cutoffs | `select` over a calendar-rule source, or `add` with a `business` amount | `unique`, `unsupported` (no rule set) | ✓ |
| E3 | Round/truncate to the hour, day or week? | Truncation in UTC, not in the local calendar | `truncate` (to a unit, in a calendar + zone) | `unique`, `none` (truncated value doesn't exist locally), `multiple` | ✗ |

### F. Repetition and selection

| # | Question | Why the convenient answer lies | Operation | Honest outcomes | Spec |
|---|---|---|---|---|---|
| F1 | When are the next N occurrences / those within a window? | Expanded in UTC, DST ignored, infinite loops | `expand` with window and budget, paged | page + continuation, `resource-limit` | ✓ |
| F2 | Is date X an occurrence of this schedule? | Re-expands from the start, or compares instants after a tzdb change | `contains` on a `recurrence` | definitely / impossible, with occurrence ID | ~ |
| F3 | Which original occurrence is this (after it was moved)? | Matched by instant | `occurrence-id` | stable ID | ~ (§23 prose) |
| F4 | Third Wednesday, last business day, first match in a window? | Implemented as a recurrence | `select` | `unique`, `none`, `resource-limit` on infinite sources | ✓ |
| F5 | Union/intersection/difference of availability windows | Overlaps double-counted, open ends lost | `set-op` | interval set, `indeterminate` endpoints preserved | ~ (§6 prose) |
| F6 | Does this "3 times a day" obligation hold for these observations? | Times invented | `match-event` / pattern evaluation | satisfied / violated / `indeterminate` | ✓ |

### G. What did we know, and when?

| # | Question | Why the convenient answer lies | Operation | Honest outcomes | Spec |
|---|---|---|---|---|---|
| G1 | When did event E happen, according to what we knew at time K? | Only the latest value is kept | `as-of` (valid time × known-at time) | assertion(s), `multiple` (competing claims) | ~ (§11 prose) |
| G2 | Record a correction without losing the original | Overwrite | `append-assertion` (returns a new history) | new `event-history` value | ~ |
| G3 | Is this cached resolved answer still valid? | Cache never invalidated after a tzdb release | `is-stale` (cache key vs current manifest) | stale / fresh, with changed dependencies | ✗ |

### H. How sure are we?

| # | Question | Why the convenient answer lies | Operation | Honest outcomes | Spec |
|---|---|---|---|---|---|
| H1 | What are the bounds or distribution of a derived value? | Uncertainty dropped at the first operation | `propagate-uncertainty` | bounds / distribution / `indeterminate` (correlation unknown) | ✓ |
| H2 | What is the probability that A precedes B? | n/a | `compare` with a probability model | probability per relation, or `indeterminate` | ~ |

### I. Getting values in and out

| # | Question | Why the convenient answer lies | Operation | Honest outcomes | Spec |
|---|---|---|---|---|---|
| I1 | Parse this ISO 8601 / RFC 3339 / RFC 9557 / iCalendar text | Guesses intent (`Z` vs `+00:00` vs `-00:00`) | `map-standard` (import) | value + fidelity report, `unrepresentable` | ✓ |
| I2 | Export to those formats | Silently drops zone, calendar, uncertainty | `map-standard` (export), `round-trip` | text + loss report | ✓ |
| I3 | Show this to a human in their language and locale | Host locale used | `format` with explicit locale, calendar, zone and pattern | text, `unsupported` (no CLDR data) | ✗ |
| I4 | Canonical bytes / hash / signature for storage and dedup | Field order, defaults and numbers differ | `canonicalize`, `digest` | canonical JSON (RFC 8785), digest | ~ (§2 prose) |
| I5 | Convert from/to a platform type (`DateTimeOffset`, `java.time`, ...) | Implicit conversions | language-adapter functions with a required intent argument | value + loss report | ✗ (library concern) |

### J. Series and scientific time

| # | Question | Operation | Spec |
|---|---|---|---|
| J1 | Values of a series within a window, by a chosen time axis | `sample` | ✓ |
| J2 | Value at a time between samples | `interpolate` (declared model) | ✓ |
| J3 | Daily/hourly aggregates in a civil zone | `aggregate`, `resample` | ✓ |
| J4 | Ordering of named periods (eras, dynasties) | `compare` on `ordinal-position`, `expand-ordinal` | ✓ |

## Gaps in the specification

These operations are needed to answer common questions, but the specification doesn't name them yet. Adding an operation is a **minor** change (1.1). They are tracked as OI-17 in [`spec/.dev/backlog.json`](../spec/.dev/backlog.json).

| Proposed operation | Answers | Notes |
|---|---|---|
| `difference` | D1–D4 | The specification has `add` but no inverse question. The result is an `amount` whose basis is explicit. Calendar difference needs the same policy object as `add`, and `add(a, difference(a, b)) = b` must not be assumed. |
| `contains` | C3, F2 | Could be a mode of `compare`, but it is so common that it deserves a name. Needs explicit endpoint semantics. |
| `fields` | B4 | Derived field queries (weekday, ISO week, quarter, day-of-year) for a calendar + week model. |
| `day-bounds` | B5 | Start and end of a civil day under a day-boundary rule. Can be `none`. |
| `truncate` | E3 | Rounding in a calendar and zone. Can be `none` or `multiple`. |
| `set-op` | F5 | Named form of the §6 set operations. |
| `as-of`, `append-assertion` | G1–G2 | Named forms of the §11 bitemporal prose. |
| `is-stale` | G3 | RESULT-CONTRACT already defines cache keys; this exposes the check. |
| `format` | I3 | A presentation operation with explicit locale (CLDR), never the host locale. |
| `canonicalize`, `digest` | I4 | Named forms of the §2 canonicalization rules. |
| `capabilities` | A3 | Lets callers discover `unsupported` before asking. |

## What is deliberately *not* an operation

- **"Now."** There's no `now()`. A caller who needs the current time takes an `observation` from a named clock and passes it in the context. This keeps every result reproducible, and the result says which clock and how good it was.
- **A total order / sort key.** Sorting arbitrary temporal values is not meaningful. A caller may sort by a *resolved* instant under an explicit policy, using a key returned by `resolve`. Values themselves define no ordering.
- **Implicit conversion** between kinds, or to platform date types. Every conversion goes through `convert` or a language adapter and reports what was lost.
