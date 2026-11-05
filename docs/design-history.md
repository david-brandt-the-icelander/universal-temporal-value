# Design history

UTV went through four internal drafts, developed through a series of design reviews, before being published as specification **1.0**. The drafts were never released. Their identifiers (`utv/2`, `utv/3`, `utv/4`) are not valid UTV 1.0 values; see `VERSIONING.md`.

This document records why the design has its present shape, so later revisions don't undo decisions without knowing the reasons for them.

## Draft v1: a single temporal type

The starting idea was one JSON-compatible type that could carry any date/time value. A review found it repeated many of the falsehoods it was meant to avoid. It assumed every value pointed to a unique instant. It mixed up durations and calendar periods. It had no account of leap seconds and treated precision as accuracy. It couldn't represent unknown or unbounded interval endpoints, or ambiguous and nonexistent local times.

## Draft v2: validate, resolve, compare as separate operations

The central change was that **a temporal value can be valid without identifying a unique instant**. Three operations were separated:

1. **Validate**: is the representation structurally and semantically meaningful?
2. **Resolve**: which instants, intervals or possibilities does it represent under specified rules?
3. **Compare or transform**: what can safely be concluded or calculated from those possibilities?

v2 added explicit resolution policies for DST gaps and overlaps, including `preserve-unresolved`. It also added floating versus pinned time-zone rules, unknown versus unbounded endpoints, explicit interval and comparison domains, elapsed versus calendar amounts, explicit timescales, independent measurement-quality metadata, physical, monotonic and logical clocks, uncertainty models, RFC 5545 recurrences, a calendar registry, and preservation of local-time intent for future appointments.

**Review of v2:** v2 covered most fundamental falsehoods at the design level, but five conceptual gaps remained:

- Time-zone identity versus geographical location (boundaries change; tzdb defines no geography).
- Retroactive correction of recorded facts (bitemporal history).
- Physically discontinuous clocks: suspend and resume, virtualization, resets and counter wrap.
- Mathematical semantics of uncertainty propagation, including correlated errors.
- Human conventions beyond calendars: business hours, jurisdictional holidays, cutoffs.

That review also recommended a traceability matrix: falsehood → representation → semantic rule → test. This became `spec/FALSEHOODS-MATRIX.md`.

## Draft v3: closing the conceptual gaps (18 kinds)

The goal was set explicitly as **a complete design, with implementation allowed to lag**. v3 added the following:

- Geographical and legal time separated from zone identity (`location-intent`, `zoneIntent: follow-location-law`).
- Append-only assertions with separate event time and record time (`assertion`, `event-history`).
- Clock generations, drift, resets, suspension and calibration.
- Correlated errors, statistical distributions, hard bounds versus confidence intervals.
- Stable recurrence occurrence identity and rule revisions.
- Historical calendar reforms, alternative eras, variable day boundaries, non-Earth calendars.
- Explicit epochs, reference frames and timescales.
- Partial ordering and uncertainty-aware comparison (sets of possible Allen relations).
- Versioned business and legal rules.
- Canonicalization, provenance and information-loss reporting.

One standards correction mattered here. RFC 5545's verified erratum 4271 treats nonexistent local times in a recurrence differently from invalid calendar dates. As a result, standards-compatible recurrence behaviour became a separate policy from UTV's stricter default.

A follow-up check confirmed that v3 covers the conventional vocabulary: date, time, date-time, offset and zoned date-time, instant, duration, period, interval and range (open, closed, half-open, unbounded), disjoint sets, recurrence, partial and approximate dates, relative expressions and business calendars. Whether `range` should be a language-level alias for `interval` was left as an API question. It is tracked as OI-14 in `spec/OPEN-ISSUES.md`.

## Draft v4: standards convergence (26 kinds)

A comparison against published standards found concepts that v3 could only approximate:

| Standard | Missing concept | v4 addition |
|---|---|---|
| ISO 19108, OWL-Time | Ordinal/nominal reference systems (geological eras, dynasties, archaeological phases) | `ordinal-position` |
| HL7 FHIR Timing | Event-anchored schedules ("30 min before a meal") and frequency obligations ("3× daily") | `event-relative`, `pattern` |
| ISO 8601-2 | First-class selections ("third Wednesday", "last eligible day"), which are not recurrences | `selection` |
| ISO 8601-1 | Intervals defined by start + duration or duration + end | `interval.construction` |
| RFC 7529, RFC 8984 | `SKIP` ordering, override identity | Edition-specific recurrence conformance (§23) |
| OGC TimeseriesML, SensorThings, ISO 19156 | Series, samples, phenomenon versus result time | `series`, `sample`, `sequence` |
| OWL-Time | Temporal topology graphs and partial orders | `temporal-graph` |

The stated goals were these. Every temporal concept in ISO 8601-1/-2, ISO 19108 and OWL-Time should have a native representation. Every construct in the relevant RFCs should have either a lossless mapping or a documented reason why one is impossible.

The design discussion first suggested keeping time series in a companion model. v4 put them in core; that choice is tracked as OI-15.

## Publication as 1.0

The v4 draft was published as specification 1.0 with the changes listed in `CHANGELOG.md`. Those are identifier renumbering, restructuring into Part I and Part II, and a few fixes that reconcile the examples and schema. Known remaining formalization work is in `spec/OPEN-ISSUES.md`.

The recommendation carried forward from the final review is this. Keep the architecture. Do a clause-by-clause completeness audit and formalize the adapter and registry contracts. Don't add more kinds without evidence of a missing concept.
