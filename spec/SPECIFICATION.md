# Universal Temporal Value (UTV) — Specification

**Version:** 1.0 · **Wire identifier:** `utv/1.0` · **Result contract:** `utv-result/1.0` · **Date:** 2026-11-04

**Status:** design specification for independently implemented temporal engines. **Not** an ISO/IETF standard. This specification governs semantics; the accompanying JSON Schema (`schema/utv-1.0.schema.json`) only governs syntax and selected local invariants. An implementation MUST NOT claim UTV 1.0 conformance merely because objects pass JSON Schema. Versioning rules are in `VERSIONING.md` at the repository root.

The document has two parts. **Part I** (§1–§17) is the core model. **Part II** (§18–§27) covers standards convergence: ordinal reference systems, event-anchored time, selections, series and samples, interval construction and recurrence-standard fidelity. Both parts are normative. Where Part I and Part II conflict, Part II governs, and the conflict is a defect to be recorded in `spec/OPEN-ISSUES.md`.

**Normative terms:** MUST, MUST NOT, SHOULD, MAY have their usual RFC 2119 meanings within this project. A rule registry, semantic validator, resolver, and a conformance suite are necessary to claim the full profile. For registries not implemented, return `unsupported`, never silently substitute a different registry.

## 1. Objectives and anti-objectives

UTV is a **universal temporal expression and knowledge model**: temporal facts, intended local schedules, physical clock readings, logical causality, incomplete and approximate historical reports, calendar calculations, and mathematically describable temporal reference systems can be serialized in one versioned data envelope. The goal is no accidental lies, rather than every value yielding a unique instant.

UTV is **not** an assertion that UTC, POSIX time, IANA tzdb, the Gregorian calendar, or even globally synchronized clocks cover all timekeeping. It is not an event database, geometry database, astronomical ephemeris, holiday registry, or a physical synchronization system: it provides typed bindings to independently versioned definitions. Completeness means every meaningful temporal claim can be expressed **directly or through a registered extension**, without pretending unsupported claims are known. No finite schema can predefine every future calendar, physics theory, jurisdiction or temporal logic; the registry and extension contract are normative.

### Invariants
1. **No implied knowledge:** unknown, unbounded, approximate, alternative, unresolved and unsupported are different conditions.
2. **Types govern meaning:** civil dates, exact timeline coordinates, clock observations, calendar quantities, causal clocks and event assertions are never silently converted.
3. **Facts vs intentions:** historical measured instants and future locally scheduled obligations use distinct authority rules.
4. **Rules are data dependencies:** a version label without immutable content is insufficient for reproducibility.
5. **No hidden environment:** resolver defaults do not inherit host clock, OS timezone, locale, installed tzdb or network access.
6. **No fictitious accuracy:** number of displayed digits does not establish measurement accuracy; historical zone rules may be unknown.
7. **Conversions have declared loss and provenance:** no offset-to-zone guessing, no silently dropping calendar, zone, uncertainty or time-source identity.
8. **Invalid vs unresolved vs unsupported** are different outcomes.
9. **Multi-valued answers allowed:** zero, one, multiple, interval set, symbolic or indeterminate.
10. **Temporal assertions are not event IDs** and timestamps do not establish causality.

## 2. Serialization and profiles

Outer envelope:

```json
{"schema":"utv/1.0","kind":"civil","value":{...},"extensions":{},"provenance":{}}
```

`schema` and `kind` are mandatory. `schema` is the wire identifier `utv/<major>.<minor>` of the specification version the value conforms to (`utv/1.0` for this version); nested values carry the same identifier as their enclosing envelope. A reader MUST reject a value whose identifier it does not implement, reporting `unsupported` rather than `invalid` when the identifier is well-formed but newer than the reader (see `VERSIONING.md`). `value` is mandatory, type-specific; `extensions` and `provenance` optional. Nested temporal values use the same envelope. Unknown core properties and unknown `kind` values MUST be rejected. Extension names are reverse-DNS or URI-qualified and MUST have a registered type definition, semantic operations and criticality behavior; unknown *critical* extensions fail closed. Unknown elective extensions MAY be retained as opaque bytes, but MUST NOT affect interpretations or be discarded when performing claimed lossless round trips.

Core kinds (26): `ordinal-position`, `event-relative`, `selection`, `series`, `sample`, `temporal-graph`, `sequence`, `pattern`, `instant`, `civil`, `amount`, `interval`, `recurrence`, `uncertain`, `set`, `clock`, `observation`, `logical-clock`, `relative`, `relation`, `assertion`, `event-history`, `location-intent`, `constraint`, `calendar-rule`, `temporal-reference`.

Separate **conformance profiles** are defined: `core-civil`, `global-instant`, `zoned`, `recurrence`, `history`, `scientific`, `distributed`, `business`, `full`. Full conformance requires support for the core value vocabulary and explicit correct `unsupported` responses for unimplemented registry implementations; claiming **operational full conformance** additionally requires executing the registered profiles and their tests. A registration is not an implemented algorithm.

### Canonical JSON
All arbitrary-precision integer values are canonical decimal strings matching `0|-[1-9][0-9]*|[1-9][0-9]*` (never `-0`). Exact decimal fractions/rationals use signed arbitrary-precision numerator and strictly positive denominator, reduced to lowest terms; rational equality is mathematical, even if physical value cannot be mapped to an instant. `null` is forbidden as a placeholder for an unknown temporal value: use explicit typed `unknown`/`not-applicable` constructs. Strings use NFC normalization where semantically valid; zone identifiers and registry IDs are exact, case-sensitive data, not arbitrarily case-folded. The canonicalization profile defines deterministic UTF-8 JSON using RFC 8785 JCS for JSON-compatible values; since large integers are strings, it avoids numeric rounding. Reject duplicate object members before parsing and signing. Hash the canonical object **plus a declared signature scope**; digital signatures may include rule-bundle hashes and ontology version. A canonical UTV form has no implicit defaults: normalization of omitted semantic fields MUST make defaults explicit before signing. Structural representation can retain authoring form, distinct from resolved canonical form.

### Interchange
ISO 8601-1/-2 (including ISO 8601-2:2019/Amd 1:2025), RFC 3339, RFC 9557, RFC 5545, RFC 8984/JSCalendar, EDTF, CLDR and POSIX/TZif have **explicit, versioned adapters**. Import must preserve input lexical representation when required. Not every UTV value can round-trip to those narrower formats. Export returns a typed loss report (`lossless`, `lossy`, `unrepresentable`) with lost fields and a required explicit caller policy. `Z`, `+00:00` and `-00:00` textual intent are distinct on input: RFC 9557 updates the treatment of `Z` and `-00:00` as UTC-known but local-offset-unknown; store that provenance if meaningful. UTV native `zoned` interpretation is strict even though RFC 9557 permits elective noncritical zone annotations with mismatched offsets. RFC 9557 parsing compatibility and UTV semantic validation are separate steps.

## 3. Temporal domains and reference systems

Every orderable value has a **temporal domain**. Built-ins:

- `physical`: location/coordinate-system-dependent physical time, with explicit timescale and reference epoch; historical UTC labels are not automatically globally precise physical measurements.
- `civil`: fielded calendar/time-of-day values under defined chronology, geography and law; not totally ordered without context.
- `monotonic-clock`: one named clock's ticks within a continuity generation; no guaranteed inter-host order.
- `logical`: causal/protocol-dependent order, no assumed physical duration.
- `relative`: expression anchored by explicit context.
- `geological`, `astronomical`, `proper-time`, `coordinate-time` are registered reference systems with domain-specific conversions, uncertainties and frame transformations, not aliases of Unix time.

A registered `temporal-reference` contains `id`, immutable version/content digest, domain, epoch event and reference frame, coordinate unit and rate, valid range, transformations (including calibration and uncertainty), and trusted references. Time scales are not interchangeable. Known timescales include POSIX (`unix-posix`), TAI, UTC (step), GPS, TT, UT1, Julian Date/MJD and smear profiles. Each registry entry defines reference epoch **including the timescale of the epoch label**, leap policy, coordinate-to-reference mapping, validity window, precision and input assumptions. TAI/GPS/TT require correct origins, offsets, and conversion data, not guessed arbitrary offsets; relativistic frames require reference worldlines and physical models. UTC before standardized adoption and ancient local solar times need explicit model/history caveats. Earth orientation (UT1−UTC) comes from a separately versioned IERS-like table. No unconditional `unix-posix` ↔ UTC leap-second bijection: POSIX folds/omits leap seconds; where two physical instants map to one coordinate, retain ambiguity or a higher-fidelity representation. Clock smear requires a specific named smear function, provider and valid interval; do not treat smear time as ordinary UTC.

`instant` is a coordinate: `{system: RuleRef, ticks: IntegerString, subTick?: Rational}`. `ticks` and `subTick` represent exact coordinates; `subTick` MUST be in [0,1) for normalized representation, even with negative ticks (floor-based convention). Alternatively `coordinate` is a reduced rational whose units are provided by the referenced system; the two encodings MUST NOT appear together. Bounds may be arbitrary precision. Coordinates outside registry model validity return `indeterminate` or `unsupported`. An instant is not evidence an event happened then. For simulation, a simulation clock gets its own registered domain, never a pretended physical UTC time.

## 4. Civil dates, calendar fields and time of day

`civil.value` includes `form`, `fields`, `calendar`, optional `zone`, `zoneIntent`, `resolutionPolicy`, `offsetAssertion`, `precision`, `interpretation`. The allowed `form` values include `date`, `time`, `datetime`, `year`, `year-month`, `month-day`, `week-date`, `ordinal-date`, `quarter`, `season`, `calendar-fields`, `era-year`, `partial-pattern`. A partial year denotes a granularity, not January 1; `month-day` and `partial-pattern` denote recurring/field patterns, not a single occurrence.

**Calendars are versioned algorithms.** Registry entries define months (including leap/intercalary months), days, eras and year numbering, week model, leap rules, epochs, reform gaps/duplicates, field grammar, date arithmetic, jurisdiction-specific adoptions and locale display rules. `calendar` includes `id`, `chronology` (`proleptic`, `historical-jurisdiction`, or registered model), and optional `rules` RuleRef. Year zero, negative years and BCE are explicitly defined per calendar. Historical legal dates may be unknown or contested; implementations do not rewrite them to modern Gregorian without labeling the interpretation. Calendars independent of timezone (and some calendars dependent on astronomical events or local sunsets) may require `location`, calculation method, ephemeris and observation provenance. The definition of a day boundary can be midnight, sunset, dawn, etc.; `civil` MUST NOT universally assume midnight-to-midnight. Locale-specific first day of week is not an inherent property of every calendar date.

**Time of day** is an ordered field system relative to a registered day boundary, not universally 24 equal hours; for ordinary clock profiles, hour `0..23`, minute `0..59`, seconds `0..59` (plus `60` only where a validated leap-second label is legal). `24:00` is normalized by an explicit policy, retaining source form if needed; not all calendar/day-boundary profiles admit that notation. Clock conventions with different divisions, variable hour lengths, local mean solar time, sunrise/sunset-dependent hours and non-Earth sols require registered clock rules. The semantics of time-only + zone cannot be reduced to one offset without a date. Distinguish missing field, unknown field, range field and wildcard field.

### Named zone, offset, geographical intent
`zone` may be `iana` (zone ID), `fixed-offset` (seconds east of UTC within profile bounds), `custom` (immutable rules bundle), `local-solar` (astronomical model), `unknown` or `floating` (no zone selected). A fixed offset is **not** a geographically named zone; historical UTC offsets can include seconds and non-hour changes. Abbreviations like CST are never accepted as unique zone IDs. An `offsetAssertion` can disambiguate candidates and MUST be checked against effective rules for strict native resolution; do not silently substitute it for a named zone. Offset-only date+time denotes a physical instant only if the civil calendar, reference UTC interpretation, leap policy and coordinate conversion are defined.

`zoneIntent` tells what controls future re-resolution:
- `follow-zone-id`: chosen zone identifier and future rules; may not follow physical boundaries.
- `follow-location-law`: location + jurisdictional/geographical time-zone mapping; treat region boundary changes and changes in legal applicability as meaningful.
- `fixed-offset`: offset is authoritative even when civil law changes.
- `fixed-instant`: instant authoritative; zone is display metadata.
- `floating-wall`: do not attach any zone until a later explicit operation.
- `historical-assertion`: preserve what a source claimed, whether or not rules can confirm it.

Geography is a reference, not an assumed perfect lookup. `location-intent` can carry WGS84 coordinates, a persistent place ID, jurisdiction, geofence/route, time-valid geometry and a rule binding for geocoding and time-zone boundaries. Boundary service resolution can return **multiple jurisdictions**, `none`, or `indeterminate`. Coordinates, map accuracy and movement matter; the IANA database explicitly does not define geographic boundaries. For travel/itineraries, `location` may be a time-varying trajectory; a single time zone need not apply for the whole recurrence. Legal time versus actual clock practice can differ; attach `observed-practice` separately from `legal-rule` when studying historical cases.

`zoneRuleBinding`: `latest-at-evaluation`, `pinned` immutable RuleRef, or `as-known-at` version-history query against retained records; these express different assertions. A `pinned` rule without accessible retained content yields `unsupported`, not an unverifiable reconstructed result. Historical coverage before tzdb's reliable period is never guaranteed; `rulesConfidence`/historical uncertainty must be propagated where required.

### Local-time resolution
Resolver computes **all** candidate instants consistent with supplied rules and assertions. There is no fixed maximum number of candidates. Exact results are `none`, `unique`, `multiple`, or `indeterminate`; the latter covers incomplete or competing rule evidence and is not the same as `none`. Policy:
- overlap: `reject`, `all`, `earliest`, `latest`, `offset-match` (requires an offset), `occurrence-index` (explicit index in defined ordering).
- gap: `reject`, `preserve-unresolved`, `shift-forward-by-gap`, `shift-backward-by-gap`, `next-valid-boundary`, `previous-valid-boundary` (each transforms civil intent and reports loss).
- unknown history: `indeterminate` unless an explicit assumption/model is supplied.

`reject` is the recommended default for operations requiring one exact instant, not an assertion that the civil input is invalid. Output includes original input, candidate set, interpretation, changed fields, loss report and rule manifest. Resolving a future event may be done repeatedly under different rule releases; never overwrite original intent with a cached instant.

## 5. Exact elapsed time, calendar period and non-standard duration

`amount` variants are `elapsed`, `calendar`, `business`, `clock-ticks`, `proper-time`, `mixed` (only as explicit ordered operation sequence). `elapsed` is a signed rational number of SI seconds in a specified physical-time metric/reference context, rather than a hard-coded 64-bit nanosecond count. `calendar` carries typed components (`years`, `months`, `weeks`, `days`, `timePart`) with a named calendar and a named day-length/day-boundary model. A calendar day is not necessarily 86,400 SI seconds. Business time uses a rule-bound service and a defined local schedule; business day/hour/cutoff/rollover rules must be explicit. Clock ticks require a clock calibration and domain; proper-time duration depends on a worldline/reference frame.

Calendar arithmetic is an **operation** with a policy, not an inherent property of `P1M` alone. An operation describes component application order, month-end handling (`reject`, `constrain`, `carry`, `preserve-end`), reform-gap handling, leap-month handling, DST overlap/gap policy, holiday roll (`following`, `preceding`, `modified-following`, `reject`), timezone rule version and elapsed-vs-wall subday policy. Its policy also defines subtract/negate behavior; `add(-x)` is not assumed inverse to `add(x)`. Days across DST cannot be equated to SI-day durations; month arithmetic is not necessarily associative or commutative. Time-zone transitions may be negative, fractional-hour or whole-day; no hour-count assumptions.

Duration grammar adapters distinguish ISO 8601 `P1M` (month) from `PT1M` (minute). A duration whose components cannot be meaningfully added without context is not silently converted to seconds. ISO repeating intervals are not automatically RFC 5545 recurrences.

## 6. Intervals, relations, sets and constraints

`interval` contains `domain`, `start`, `end`, `endpointSemantics` and optional `ruleBinding`. Boundary status is one of `included`, `excluded`, `unbounded`, `unknown`: an endpoint is present only for included/excluded. `unbounded` means known absence of boundary; `unknown` means boundary exists or may exist but is not known. Intervals of incompatible domains are semantically invalid unless explicitly expressed as a symbolic constraint whose mapping is deferred. At equal endpoints: `[x,x]` is singleton; `(x,x)`, `[x,x)`, `(x,x]` are empty; reversed endpoints are invalid unless modeled as a deliberately empty interval with an explicit empty flag. Canonical half-open `[start,end)` is preferred for bounded event spans, but closed/open exact semantics are preserved. Bounded civil intervals are not forced to physical instants. Set operations support union, intersection, difference, complement (requires domain/universe), alternatives and Cartesian constraints. Do not enumerate infinite results by default.

`relation` supports **Allen's 13 interval relations**, instant comparison, containment and metric constraints, with relation sets when uncertain. Determinations: `definitely`, `possibly`, `impossible`, `indeterminate`, plus evidence; mathematical relations do not imply a causal relationship. Constraint solver outcomes can be satisfiable/unsatisfiable/unknown/unsupported; do not treat timeout as contradiction. Structural equality, semantic equivalence under context, equal instant, same intended civil event, and equal event identifier are separate predicates.

## 7. Uncertainty, ignorance, precision and probabilities

Four orthogonal aspects of a temporal assertion:
1. **representation** (exact rational/field value and precision/granularity),
2. **epistemic uncertainty** (unknown, bounded support, disjoint alternatives, probability measure, fuzzy linguistic qualifier),
3. **instrument error** (bias, random error, drift, calibration, correlation),
4. **provenance** (witness, sensor, rule source and revision history).

`uncertain` targets any compatible temporal value and has a `model`: `unknown`, `bounds`, `alternatives`, `distribution`, `fuzzy`, `censored` or `constraint`. A bounded interval can be a hard support limit or a confidence interval; they are **not synonymous**. Probabilistic models require named continuous/discrete distributions, parameters, domain, measure/unit, normalization and a confidence/credible interpretation. Unknown probability must not be represented as a uniform distribution. Probability mass at leap seconds or DST alternatives can be discrete, while continuous clock uncertainty might be a density. Model provenance and assumed priors are explicit.

Propagation uses registered operations: interval arithmetic with dependency tracking, covariance/joint distributions, Monte Carlo with specified PRNG/seed/algorithm where needed, exact symbolic constraints, or `indeterminate` if correlation cannot be established. Adding two ±5 ms observations of the same unsynchronized clock cannot presume independent errors. Unknown systematic bias cannot be erased by averaging. Report whether bounds are rigorous, statistical confidence, or estimates. Precision (reported digits), resolution (instrument step), repeatability, uncertainty, and accuracy are not interchangeable. Numerical rounding/quantization is explicit; storing the original assertion may be necessary.

Approximate historical dates, circa/season, BCE, archaeological BP, calibrated radiocarbon ages and geological Ma are separate provenance/calibration profiles; BP must define its epoch and whether calibrated or uncalibrated. Natural-language dates require locale, language, jurisdiction and reference context; a recognized text string does not intrinsically imply its intended calendar.

## 8. Observations, clocks, execution time and synchronization

`clock` describes a *reading* `{clockRef, generation, ticks, tickUnit}`. A registered clock description identifies hardware/software source, continuity guarantees, boot/reset/wrap epochs, clock rate and changes, suspend handling (`continues`, `pauses`, `unknown`), virtualization/migration, drift/adjustments, wrap width, faults, synchronization model and calibration. A clock's advertised name does not prove actual monotonicity. Clock readings across generations or machines cannot be ordered without calibration/evidence; even same generation can have wrap/reset/discontinuity requiring explicit handling.

`observation` must have `role` (`event-occurrence-estimate`, `sensor-captured-at`, `observed-at`, `received-at`, `processed-at`, `recorded-at`, `committed-at`, `published-at` or registered role), `reading`, `source`, and optional measured latency bounds, calibration/correlation model and quality. Network packet arrival time does not identify send time; transport time is not assumed zero or constant. Difference between UTC system clocks is not reliable latency without bounded synchronization uncertainty. `observation` may describe only measured time, not asserted occurrence time; provenance determines which.

Time sources (NTP/PTP/GNSS) are *methods*, not guarantees. Model possible clock step/slew, holdover, leap smear, oscillator frequency corrections, GPS rollover, localization bugs, timestamps that repeat, and abrupt administrative changes. Monotonic-vs-boottime semantics differ on sleep, so define the specific reference clock. Process CPU time, wall-clock time, simulation tick time and elapsed high-resolution timer time are different domains. Clock timestamps never serve as unique identifiers.

## 9. Distributed ordering and causality

`logical-clock` requires registered algorithm and version (Lamport, vector, dotted version vector, HLC, etc.), actor namespace, generation, participants/membership semantics and causal protocol. Lamport order is compatible with happens-before under appropriate protocol assumptions; a smaller Lamport count is not proof of causation. Vector clocks can establish happened-before vs concurrency only with correctly propagated causal metadata and actor identities. HLC physical component inherits clock calibration uncertainty and must not be claimed a perfectly accurate occurrence time. Node ID reuse, counter overflow, resets, membership pruning and Byzantine actors must be specified. For a causal history, temporal ordering and causal edges are explicit; timestamp equality/difference alone cannot prove a causal edge. Untrusted messages and dishonest clocks require trust policy and attestation, beyond format validation.

## 10. Recurrence, schedules, exceptions and civil obligations

`recurrence` has an **authoritative intent** (civil start+zone/location, floating rule, or absolute start), rule grammar/version, calendar system, expansion policies, durations, include/exclude/overrides, identity policy, rule-binding choice, and expansion bounds. RFC 5545, RFC 7529 and RFC 8984 adapters MUST honor their semantics, including verified errata. In particular, RFC 5545 invalid dates are ignored; nonexistent local times in recurrence are handled per RFC 5545's DATE-TIME rules, rather than universally ignored. UTV's strict recurrence algorithm is a **different** named policy, not described as RFC 5545-equivalent. Recurrences may mean fixed elapsed intervals (`every 604800 SI seconds`), fixed wall clock (`every Tuesday at 9`), business-pattern (`first working day`), or observational (`at sunset`); they are not interchangeable.

Generate recurrence candidates in calendar space, apply rule-specific invalid-date policy, resolve local candidates under zone rules, then apply exceptions/overrides and count/limit according to the named recurrence standard. Exact ordering is part of a registered algorithm. Occurrence IDs are based on stable original recurrence identity (calendar fields, calendar, rule and a sequence discriminator), **not only instant**; a new tzdb release must not make an exception silently target another occurrence. Overrides can amend time/location/duration or cancel; changed occurrence is still linked to original ID. Count/UNTIL and filters have version-dependent semantics; interoperability adapters MUST preserve meaning. Recurrence iterators require explicit query horizon, maximum occurrences, iteration/depth budget and cancellation; never recursively expand unbounded patterns without limits.

Scheduling policy for legislative change: `wall-law-following`, `wall-zone-following`, `instant-fixed`, `offset-fixed`, `rules-pinned`, `manual-review`. A derived alarm/instant has an evaluation manifest and invalidation key; recompute or flag when rules, geography, holiday data, ephemeris, reference time or policy change. A location-following event needs a versioned jurisdiction mapping, and the mapping may be uncertain or change over time. An event crossing a time-zone boundary during its duration needs a location trajectory or per-occurrence location; do not assume a single stable zone.

Business calendars have jurisdiction, organizational authority, week/work schedule, holidays, early closures, half-days, exceptions, business-day boundary/cutoff (possibly not midnight), observance policy, revisions, and precedence for overlapping rules. `next business day`, business-hour arithmetic, SLAs and clearing deadlines require explicit market/location and convention. Recurring historical business calendars should not be silently calculated from today's holiday dataset.

## 11. Assertions, corrections and bitemporal history

An event's **asserted valid/occurrence time** differs from **when the database learned or accepted the assertion**. `assertion` wraps a temporal claim, subject/event ID, source/provenance, authority, confidence and optional evidence; it cannot masquerade as `instant` with provenance bolted on. `event-history` is an append-only series of assertion revisions. Every revision has `assertionId`, `subjectId`, `assertedTemporalValue`, `recordedAt`, `effectiveValidity?`, `supersedes?`, correction reason and source; revisions use immutable identifiers. `recordedAt` is an observation of the recording system (and may itself be uncertain), not proof of global physical time. A bitemporal query must explicitly choose *valid time* and *system/transaction time* semantics. Retroactive changes can correct an earlier accepted fact; preserving prior revisions is mandatory for audit profiles. Distinguish official correction of a legal rule, revised historical evidence, revised geographic mapping and operational data fixes.

For legal effectiveness, add a versioned authority/jurisdiction source and potentially multiple overlapping claims; conflicting laws or interpretations resolve to **competing assertions**, not a guessed winner. Database transaction ordering and timestamp observation are separate (e.g., commit sequence vs wall-clock); preserve both. Tamper-evident event history is an external trust/canonicalization problem; do not claim hashability proves truth.

## 12. Relative, natural-language and contextual expressions

`relative` carries a versioned expression language, named anchor (`instant`, `civil`, `eventId`, reference observation, or symbolic time variable), locale, calendar, zone/location, business rule set and evaluation policy. No implicit `now`, today, user's location or system locale is allowed in reproducible stored values; interactive import may capture those inputs then freeze them. `tomorrow` depends on a civil calendar and zone/day boundary; `next Friday` may be linguistically ambiguous and requires a declared convention or unresolved alternatives. `in two months`, `end of fiscal quarter`, `sunset`, `after the alarm` and `sometime before launch` need different registered interpretation functions. Nested expressions may yield sets/constraints. Competing event anchors or absent future data return symbolic/indeterminate results.

## 13. Resolution, operations and API result algebra

All functions take `(value, context, policy)`; context contains explicit rule manifest, as-of rule selection, reference observation/instant, calibration, geographical source and conversion profile. Required functions:

- `validateStructural(value) -> accepted | invalid`
- `validateSemantic(value, registries) -> valid | invalid | unsupported | indeterminate`
- `resolve(value, context, policy) -> ResolutionResult`
- `compare(a,b,context) -> relation-set | indeterminate | unsupported`
- `add(value,amount,policy,context) -> value + lossReport | unresolved/error`
- `convert(value,target,policy,context) -> converted + lossReport | unrepresentable`
- `expand(recurrence,window,budget,context) -> finite page + continuation + manifest`
- `equivalent(a,b,kind,context) -> true | false | indeterminate`
- `explain(result) -> source rules, interpretation, uncertainty, losses`

ResolutionResult status: `none` (proven no solutions), `unique`, `multiple` (finite candidate count known), `range` (possibly continuous), `symbolic` (expression not evaluated), `indeterminate` (insufficient/competing evidence), `unsupported` (implementation lacks capability), `invalid` (claim is contradictory/malformed), `resource-limit` (bounded evaluation incomplete), `error` (unexpected operational failure). `resource-limit` is not `none`. Every nontrivial result records rule dependency manifest, assumptions and provenance. `multiple` need not enumerate all possibilities; cardinality may be unknown. Deterministic APIs must expose explicit stable ordering if used for selected candidates. Transformations declare `lossless`, `information-losing` or `assumption-dependent` and preserve original source where reversible round trip is claimed.

Comparison is **partial**. `a < b` may be forbidden unless the relation is provably unique; for uncertainty, return possible Allen relations or interval conditions, never choose one by numeric text ordering. Equality has modes: representation, normalized semantic expression under manifest, timeline equivalence, schedule-intent equivalence, and event identity. Ordering need not be total. Derivation/cached interpretation always references original value plus rule manifest; cache invalidation includes all dependencies.

## 14. Historical and scientific references

`temporal-reference` accommodates astronomical Julian Date and modified Julian Date (explicit day origin/noon convention, scale), UT1 tables, geophysical timescales, geological time charts, astronomical dynamical coordinates, solar/local sidereal time, spacecraft mission clocks, time dilation and proper-time integration along worldlines, and non-Earth calendars. Their physical rules are external immutable datasets/algorithms with validity region, units, uncertainties, coordinates, motion/ref-frame details and provenance. These are separate profiles, not promised as pre-built by a core parser. Historic date ranges may predate modern standardized time; a mathematical label applied retrospectively is not evidence of actual historical clock readings. `unknown` should remain a first-class answer when historical practice cannot be reconstructed.

Leap-second policy must support positive and negative changes as domain models even if not observed historically. UTC's future leap-second regime is subject to standards updates; the model must allow rule changes rather than bake in an eternal one-second adjustment policy. Far-future instant↔civil resolution can legitimately be indeterminate because future rules are not known.

## 15. Security, privacy, resilience and trust

No remote rule code fetched or evaluated during parsing. Registry entries are signed or content-hashed and retrieved via trusted code paths. Custom calendars/recurrence policies execute in sandboxed, deterministic, resource-budgeted environments. Enforce limits on object depth, integer digit count, rational denominator, geographic polygons, rule graph breadth, recurrence expansion, probability integration, transformation chains and historical searches. Reject duplicate JSON keys, unknown critical extensions, conflicting value sources and invalid signatures. Prevent algorithmic DoS with cycles in rule references/relative expressions. Immutable document/event IDs are separate from timestamps. A `source`/`signature` says something about who asserted a time, not proof of temporal truth. Location and time histories can be sensitive; define redaction of provenance and location separately from lossless core representation. Transformation must never inadvertently claim a redacted value is the original complete assertion.

## 16. Registry contract

Every registry entry MUST specify: stable namespace/name, schema and semantic version, canonical immutable content digest, maintained authority/provenance, domain, valid field grammar, evaluation/arith/comparison algorithms, epoch/reference/unit where relevant, validity interval, geographical scope where relevant, uncertainty limitations, migration/alias rules, licensing if applicable, test vectors, supported feature flags and trust model. Entry aliases are not silently rewritten: preserve asserted ID and record resolved canonical ID. Rule manifests list exact entry digests and dependency closure (tzdb data, leap tables, CLDR/ICU algorithm, jurisdiction geometry, business calendar, ephemeris, human authority sources), reference observation and evaluation date where a rule policy is `latest`/`as-known-at`. Rule bundles themselves can be versioned and corrected; reproducibility requires storage of previous bytes.

## 17. Conformance and specification completeness

A design risk is closed only when it has (a) a representable expression, (b) normative validation rule, (c) resolution outcome, (d) test vector and (e) named limitation or registry dependency. **`spec/FALSEHOODS-MATRIX.md`** enumerates the classic falsehood families, **`conformance/scenarios/falsehoods.json`** enumerates scenario vectors with expected semantic outcomes, and **`spec/DECISIONS.md`** records binding decisions and remaining non-semantic choices. No finite enumeration proves all possible future falsehoods covered; unrecognized temporal concepts must be registered or rejected explicitly rather than coerced.

**Reference sources:** ISO 8601-1/-2 (including ISO 8601-2:2019/Amd 1:2025), RFC 3339, RFC 9557, RFC 5545 with verified errata, RFC 7529, RFC 8984, RFC 8536, RFC 8785, W3C OWL-Time, IANA tzdb theory, POSIX, BIPM/CGPM UTC resolutions, Unicode CLDR, plus registered calendar/astronomical/civil-law references. Sources are normative only for specifically named adapters; UTV semantics supersede adapters where stricter, and differences MUST be disclosed.


---
# Part II — Standards convergence (NORMATIVE)

Part II takes precedence if it conflicts with Part I. The value vocabulary includes **26 kinds**: the 18 kinds introduced in Part I plus `ordinal-position`, `event-relative`, `selection`, `series`, `sample`, `temporal-graph`, `sequence`, and `pattern`. All core envelopes use `schema: "utv/1.0"`. Extension kinds are strictly versioned and registered; they are NOT confused with the standard core vocabulary.

## 18. Ontology, typed domains, and decidability

A temporal object describes one or more of: (a) a time coordinate or named position, (b) a period, (c) an assertion about an event, (d) a rule that computes or constrains times, (e) an observation/sample, (f) a collection or stream indexed by time, or (g) a relationship between temporal objects. These are distinct semantic categories, even if a `kind` may refer to another.

For every registered kind K the registry MUST specify a formal signature (`inputDomains`, `outputDomain`, `cardinality`), its validation predicate, equality mode(s), canonical construction, inference rules, supported operations, applicable resource bounds, information-loss behavior, and normative examples. **Extension presence is not universal support:** an implementation is only conformant for kinds it resolves or correctly returns `unsupported` for. Unknown critical extensions fail validation, optional opaque extensions must be preserved verbatim and cannot participate in authoritative reasoning.

There is no global total ordering of all UTV objects, and no requirement to decide arbitrary user-defined predicates. A `constraint` can remain symbolic or return `indeterminate` or `resource-limit`, distinguished from unsatisfiability. Expressions MUST be referentially transparent under a frozen evaluation manifest; no hidden dependence on current time, default time zone, locale or live network data.

## 19. Ordinal and nominal temporal reference systems (ISO 19108, OWL-Time)

`ordinal-position` has fields `system` (immutable RuleRef for an ordinal temporal reference system), `label` (exact registry member ID), and optionally `relationContext` and `sourceTerm`. Registry positions MAY designate an instant-like rank, epoch, named era, interval, or overlapping period. Ordinal systems define a **partial order** (not necessarily a total order), category/containment hierarchy, equivalence and explicitly asserted bounds; no implicit Gregorian anchor, numeric time scale, or duration. For two labels, comparison returns a *set of possible Allen/point relations*, or `indeterminate` with provenance. Equality of registry IDs is not equality of positions unless registry declarations provide an identity mapping. Cycles in strict-before assertions are invalid; equal/overlapping named periods are legal. A mapping into absolute dates MUST carry a separate `temporal-reference` or evidence binding and uncertainty.

```json
{"schema":"utv/1.0","kind":"ordinal-position","value":{"system":{"system":"example.ordinal","id":"dynasties","version":"1"},"label":"third-dynasty"}}
```

`temporal-graph` is an assertion graph of nodes (temporal entities, events or opaque subject IDs) and edges (Allen's thirteen base relations for proper intervals, point ordering, containment, synchrony and domain-specific relations). Edges specify relation alternatives, proof/evidence and validity. Graph consistency is a **constraint satisfaction problem**; solver results must distinguish satisfiable, unsatisfiable, indeterminate and resource-limited. No asserted relation is silently inferred from timestamps with unknown accuracy. Graph nodes need not have absolute timestamps.

## 20. Event-anchored time and temporal obligations (FHIR Timing)

`event-relative` has an explicit `anchor` (stable event class/identity or referenced event occurrence), `matching` (`each`, `same-episode`, `nearest-before`, `nearest-after`, `specified`, or a registered pairing rule), `relation` (`before`, `after`, `during`, `starts-with`, `ends-with`, `overlaps`, `offset-from-start`, `offset-from-end`), and optional `offset`, `window`, `fallback`, `cardinality` and `referenceContext`. The anchor can be absent, repeated, uncertain or not yet observed. An absent anchor is NOT midnight, an empty event set, nor a scheduling failure by itself: evaluate as `symbolic`, `indeterminate`, or `none` according to explicit open-/closed-world policy. A future event ID is not an instant.

`pattern` describes *constraints on occurrences* rather than a fully determined sequence: `frequency` minimum/maximum, `per` amount or interval, `spacing` minimum/maximum, `window`, optional day/time selections, `onEvent`, and `completionCriterion`. FHIR-style "three times each day" is not silently transformed into 08:00/12:00/18:00; a scheduler MAY generate candidate schedules given a declared selection policy, keeping the distinction between a prescription/requirement and an actual observation. Named event-relative codes such as meals, waking, bedtime, sunrise and sunset require versioned ontology/rule binding; localization and culturally dependent event meanings are not implicit.

The system MUST separate `obligation` or requested schedule, `planned occurrence`, `observed occurrence`, and `completion assertion`. Missed occurrences are not silently deleted. An event-relative schedule may be scoped to subjects and trajectories, including different time zones across events.

## 21. First-class temporal selection and ISO 8601-2 extensions

`selection` holds `source` (finite/infinite interval, range, set, recurrence or registered sequence), an explicit versioned `predicate` DSL/registry reference, `orderBy` with domain-aware stable tie-breaker, `take` (`first`, `last`, `nth`, `all`, `sample`, `top-k`) and `cardinality` policy. `nth` uses explicit zero/one-based index and negative index rules; first/last on an unbounded unordered source is `indeterminate`/`resource-limit`, never guessed. Predicates cannot silently execute arbitrary code, use system time, or fetch network data. Calendar predicates (`weekday`, `nth-weekday`, `last-business-day`, `season`, `holiday`, `astronomical-event`) bind immutable rule dependencies. Selection and recurrence are distinct: selecting the third Wednesday in one month does not assert a perpetual schedule.

ISO 8601-2:2019 + Amendment 1:2025 adapters cover: uncertain/approximate/unspecified components, masked digits, season/division-of-year, sets vs choices, grouped time-scale units, extended intervals, repeating intervals, selections, canonical expressions and date-time arithmetic. **The ISO text is a notation, not UTV's semantic truth model.** Adapters MUST document loss or ambiguity when converting lexical forms into a structured expression, including differences between approximate and unknown values. Special textual source forms are preserved in `provenance.lexical` for lossless round trips. Do not claim exact ISO grammar coverage without test vectors licensed/obtained from the normative publication.

`sequence` is an *ordered* finite or lazily generated collection, with `ordering`/ordinal index, optional membership and pagination context, distinct from an unordered `set` or periodic `recurrence`. Sequences may contain repeated equal dates/instants: deduplication requires an explicit policy. Count may be finite, infinite, unknown, or computationally undefined. Indexing into an infinite sequence requires a generating algorithm and resource policy.

## 22. Interval construction, topology, extents, and boundaries

`interval` now has a `construction` discriminator: `start-end` (two boundaries), `start-amount`, or `amount-end` (amount plus one boundary). All three are **authoring constructs** preserved for lossless intent. A derived opposite endpoint is a *resolution result* and can change with calendar, zone or legislation; it is not an authority replacing the original duration. For `start-amount`, the amount is applied with explicit arithmetic policy; for `amount-end`, inversion MUST be separately defined and may have zero/multiple solutions, especially month-end and reform handling. Unbounded/unknown endpoints may not be combined with nonsensical amount modes. `end-start` is not silently interchangeable with `start-end`.

Intervals carry boundary state (`included`, `excluded`, `unbounded`, `unknown`), endpoint domain/reference system, support for empty intervals, and optional `granularity` and `construction`. A closed discrete date interval and a half-open day interval can denote equivalent date sets, but conversion MUST declare domain and successor operation; there is no universal "next representable instant" for dense physical time. Interval sets implement union, intersection, difference, complement relative to an explicitly declared universe, and closure/interior where topology defines them. The chosen equivalence relation and adjacency semantics must be explicit.

Interval relations include Allen's 13 mutually exclusive relations for precisely known proper intervals: before, meets, overlaps, starts, during, finishes, equals, and six converses. Under uncertainty they return a *nonempty set of possible relations*; contradiction returns `none` only if evidence proves no valid valuation. Degenerate empty intervals need a separate defined algebra rather than pretending Allen base relations apply unchanged. The system supports ordinal and event-indexed intervals, not only numeric UTC.

## 23. Recurrence conformance (RFC 5545, 7529, 8984, ISO 8601)

A recurrence stores: authored local `intent`, named recurrence algorithm and edition, base occurrence identity, inclusion rules, exclusion rules, explicit inclusion dates, explicit exclusion dates, recurrence instance overrides, zone/resolution policy, and duration. A **stable recurrence ID** refers to the rule-generated original local occurrence, not necessarily the moved result. Two occurrences may resolve to the same instant; their IDs stay distinct unless the chosen adapter specifies deduplication. Moving an occurrence must not implicitly change series membership, exception identity, or historical completion records.

RFC 5545 generation MUST follow the normative rule evaluation order and verified erratum 4271: invalid calendar dates are discarded and do not count; a nonexistent local time is instead handled according to the DATE-TIME rule in RFC 5545 §3.3.5, not universally discarded. For a repeated time, the RFC's preferred interpretation is encoded by the RFC profile, distinct from UTV's default `reject` on ambiguous resolution. RFC 7529 `RSCALE`/`SKIP` operates only in the specified positions relative to BYMONTH/BYWEEKNO/BYYEARDAY/BYMONTHDAY/BYDAY/BYHOUR/BYMINUTE/BYSECOND/BYSETPOS; `SKIP=OMIT|BACKWARD|FORWARD` must preserve prescribed ordering. `UNTIL` typing and inclusivity and `COUNT` semantics follow the chosen standard version, not an intuitive simplification.

RFC 8984 imports/exports use recurrenceRules, excludedRecurrenceRules, recurrenceOverrides, recurrenceId and recurrenceIdTimeZone as separately defined. Patches are applied to the **identified occurrence** with normative override ordering, not by matching the resulting instant after movement. Unknown patch fields, UID changes, cancellation and exception-only instances require explicit handling. Embedded VTIMEZONE and IANA rule references are *not equivalent* without comparison; do not discard embedded rule definitions. ISO 8601 repeating intervals remain a distinct adapter. Every adapter has canonical field mapping, supported edition, lexical-preservation mode, and a mismatch/loss report. If full standard fidelity cannot be shown, return `unrepresentable` or `lossy` as appropriate.

## 24. Temporal observations, samples, series, and support intervals

`sample` has `subject`, `phenomenonTime` (point or **support interval** over which the measured phenomenon applies), `resultTime` (availability of result), optional `validTime`, `samplingTime`, `acquisitionTime`, `processingTime`, `publicationTime`, `timeCoordinates` and `payload` (typed non-temporal value or external immutable content ref). This recognizes event-time, observation-time, processing-time and publication-time as different. `sample` may have multiple clock sources and uncertainty dependencies, not merely a single UTC scalar.

`series` has a stable series identity, subject and measurement definition, temporal axis (`event`, `observation`, `result`, `valid`, `logical`, `ordinal`, or registered), samples (inline bounded array or immutable chunk manifest), declared sequence semantics, optional temporal support per sample, missing-data state, quality flags, and rendering/aggregation metadata. Values may be numeric, categorical, vector, spatial, events or arbitrary typed immutable references; time series are not restricted to one numeric column. Explicit **duplicate timestamps, out-of-order arrivals, multiple samples per time, clock resets, sparse/irregular grids, changing units**, and time-zone changes are valid source states. Neither sampling interval nor sample ordering is inferred from row position unless explicitly declared.

Interpolation is a *declared interpretation*, not part of raw evidence: `none` (discrete), `step-previous`, `step-next`, `linear`, `spline`, `constant-interval`, `continuous-model`, `categorical`, and registered models, each with domain, parameters, extrapolation prohibition/default, behavior across missing/censored values, discontinuities, and uncertainty propagation. Do not imply linear interpolation for categorical observations. Sampling model distinguishes point sample, aggregated window, instantaneous state change, continuous support, irregular event, cumulative counter, and rate. Aggregation declares bin alignment, calendar/zone, interval inclusivity, weighting, downsampling, support coverage, counter-reset policy, and treatment of missing values. A one-day bin in a civil zone need not contain 86,400 SI seconds.

`series` may be large and/or unbounded. A serialized UTV value can reference immutable chunks with digest, size, schema and ordered index. Streaming/pagination are transport protocols rather than changing value identity. Exact reproducibility requires chunk digest, data dictionary, ordering and processing algorithm manifest. Data privacy, access, retention and redaction policies apply to series and precise event trajectories. An external stream without a frozen snapshot is **not** an immutable canonical value; reference it as a live source with consistency semantics and a watermark, not as a signed immutable UTV sample set.

## 25. Physical, astronomical, geological and specialized temporal concepts

UTV core has reference-system and extension paths for non-Earth calendars, astronomical events and ephemerides, proper time along worldlines, relativistic coordinate time, time dilation, geological ages (including calibrated and uncalibrated BP), radiocarbon years and calibration curves, stochastic event times, simulation timesteps, musical metrical time, media timelines, frame/timecode coordinates (including drop-frame conventions), hardware counters, particle-detector time, orbital/rotational phases, legal time and social/historical chronologies. These are NOT assumed interchangeable with UTC; each is a registered reference domain defining ordering, rate, origin, uncertainty, transformation and validity. Named temporal entities can be connected symbolically without numerical mapping.

UTV does not pretend that a generic `temporal-reference` plus arbitrary tags automatically implements each domain. The registry MUST provide an explicit ontology and algorithm (or `unsupported`). A potentially unbounded list of human/instrument/fictional future time conventions is accommodated by this **typed, declarative extension contract**, not by an unprovable assertion of literal universal completeness.

## 26. Normalization, compositional operations, and loss

For any constructor, normalizer, resolver, comparison, arithmetic operation, graph query, selection, recurrence expansion, and series transform, an evaluator MUST return the result-contract envelope (`utv-result/1.0`, described in `spec/RESULT-CONTRACT.md`), with rule digests, complete/partial cardinality, diagnostic evidence, assumptions and conversion-loss report. Operations yielding `none` require proof of emptiness under pinned rules. Queries over uncertain and set-valued times MUST propagate uncertainty; sampling or picking a representative requires explicit policy. Inference must not create false precision or choose among calendar reform interpretations.

All serialization has stable schema identity, canonical JSON byte rules, arbitrary-precision integer/rational numbers, no untyped `null` as missing data, no implicit field defaults in signed canonical form, no registry algorithm execution from arbitrary untrusted content, bounded parser depth and evaluation budgets, and optional detached provenance/signatures. Lexical and semantic equivalence are distinct. Each standard adapter records edition, algorithm version and any unavailable source features, not just its standards organization's name.

## 27. Completeness gates

A UTV 1.0 design claim is satisfied only for an explicitly enumerated **standards coverage baseline**, never for unknown future standards. Before implementing a named profile, the agent must complete:

1. A standards concept-to-UTV field mapping, marking `native`, `composed-lossless`, `adapter-lossless`, `lossy`, `not-representable`, or `unsupported`.
2. Structural schema validation AND semantic validation, including negative fixtures and under-/over-specified inputs.
3. Evaluation test vectors for numerical time reference, calendars, zone rules, astronomical/geological references if claimed, topology, event anchors, recurrence and series processing.
4. Round-trip equivalence tests against official/reference-format examples, preserving lexical form where required.
5. Deterministic dependency manifests, limits and explicit `indeterminate`/`unsupported`/`resource-limit` outcomes.
6. Separate profiles for temporal **data expression**, temporal **reasoning**, **storage**, **interoperability**, and **scientific** model support. The ability to serialize a construct never proves an algorithm can solve it.

### Additional normative references
- ISO 8601-2:2019 plus Amendment 1:2025 (published; grammar must be verified against licensed normative text).
- ISO 19108: Geographic information — Temporal schema; ordinal and temporal reference systems.
- W3C/OGC Time Ontology in OWL (OWL-Time); named positions, relationships, temporal duration and reference systems.
- RFC 5545 with verified erratum 4271; RFC 7529; RFC 8984 (JSCalendar).
- HL7 FHIR R5 Timing repeat, event-based timing, frequency ranges and period ranges.
- OGC TimeseriesML and OGC SensorThings API, observation/result times and sampled series.
- W3C PROV-O for derivation/provenance; relevant parts of ISO 19156 observations and measurements.

### Explicit non-claims
This project cannot prove that it enumerates every conceivable temporal concept. It can define a **closed, exhaustive core for the specified published-standard baseline**, and an open extension ontology for other or future temporal systems. Its built-in concepts are intentionally broad, but custom semantics are not automatically understood. A schema-only implementation is not a conformant temporal engine.
