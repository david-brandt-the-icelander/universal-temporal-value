# UTV 1.0 — Published-standard coverage matrix

**Scope:** concept-level architecture and required adapter semantics, **not a claim of certified, byte-for-byte, lossless support for every feature**. Standards may be copyrighted or may have amendments; an implementation agent MUST inspect the full normative publication before claiming full grammar or conformance. `Native` means a UTV structural category and normative model exist; `Profile/adaptor` means an adapter must be implemented and independently tested; `Registry` means a domain-specific rule algorithm is required.

| Published standard/specification | Distinctive concept | UTV representation | Coverage class | Implementation acceptance test |
|---|---|---|---|---|
| ISO 8601-1:2019 | Gregorian dates, wall times, offsets, intervals, repetitions | `civil`, `instant`, `interval`, `recurrence` | Native + adapter | Full lexical/semantic round-trip where representable |
| ISO 8601-2:2019, Amd 1:2025 | Approximate/unknown/unspecified, choices, sets, seasons, grouped units, selection, extended interval, arithmetic | `uncertain`, `set`, `civil`, `selection`, `interval`, `amount`, `constraint` | Native + adapter | Distinguish unknown/approximate/choice; test amendment constructs |
| RFC 3339 | Machine timestamps, offsets, `-00:00` | `instant`, `civil`, provenance | Adapter | Preserve offset-unknown intent and fractional precision |
| RFC 9557 | Named-zone annotations, criticality and offset semantics | `civil.zone`, `offsetAssertion`, extensions | Adapter | Native strict mismatch vs RFC noncritical annotation import |
| IANA tzdb / TZif | Zone rules including historical transitions | `civil.zone`, RuleRef and zone intent | Registry | Transition candidates, version pinning, historical uncertainty |
| POSIX / IEEE 1003.1 | Epoch seconds, discontinuous wall clock | `instant`, `clock`, `observation` | Registry | Leap treatment, discontinuity and monotonic separation |
| IERS/ITU UTC/TAI/UT1 | Timescales, leap behavior and EOP | `temporal-reference`, `instant` | Registry | Verify epochs, leap table and validity windows |
| ISO 19108 | Ordinal reference systems, chronology, temporal topology | `ordinal-position`, `temporal-reference`, `temporal-graph` | Native + registry | Named period order without invented dates |
| W3C/OGC OWL-Time | Nominal/numeric position, TRS, temporal entities, interval relations | `ordinal-position`, `instant`, `interval`, `relation`, `temporal-graph` | Native + ontology adapter | Allen 13 relations; partial-order uncertainty |
| RFC 5545 + verified errata | Recurrences, DTSTART/UNTIL, EXDATE/RDATE, DST, VTIMEZONE | `recurrence`, `civil`, `calendar-rule` | Adapter | Exact recurrence generation plus §3.3.5 DST behavior |
| RFC 7529 | RSCALE + SKIP and non-Gregorian recurrences | `recurrence`, calendar RuleRef | Adapter + registry | Exact BYxxx/SKIP ordering |
| RFC 8984 / JSCalendar | JSON events, recurrenceId, exceptions/overrides, rule arrays | `recurrence`, `assertion`, `sequence` | Adapter | Moving occurrences preserves original recurrenceId |
| HL7 FHIR R5 Timing | Meal/waking event codes, frequency and period ranges, bounds | `event-relative`, `pattern`, `constraint`, `interval` | Native + adapter + ontology | Do not invent concrete clock times from frequency rules |
| OGC TimeseriesML | Point/time window observation series, interpolation, metadata | `series`, `sample`, `interval` | Native + adapter | Duplicate/missing samples, interpolation policy |
| OGC SensorThings | PhenomenonTime, resultTime, observations and datastreams | `sample`, `series`, `observation` | Native + adapter | Preserve separate measurement and reporting times |
| ISO 19156 Observations & Measurements | Sampling, feature/phenomenon/result time | `sample`, `observation`, `series` | Native + adapter | Separate measured phenomenon support vs result time |
| W3C PROV-O | Source provenance and derivation history | `provenance`, `assertion`, `event-history` | Adapter | Trace revisions and source evidence immutably |
| SQL:2011 temporal tables | Application time, system time and periods | `assertion`, `event-history`, `interval` | Native + adapter | Bitemporal correction and overlap semantics |
| ISO 20022 financial scheduling conventions | Business calendars, settlement cutoffs | `calendar-rule`, `pattern`, `relative` | Registry + application profile | Holiday rolls and jurisdiction cutoffs, pinned rules |
| UCUM | Units for intervals and measures | `amount`, registered unit definitions | Adapter | Do not equate calendar month and exact seconds |
| CF climate calendar conventions | 360_day/noleap and non-Gregorian time axes | `calendar-rule`, `temporal-reference`, `series` | Registry + adapter | Climate calendar arithmetic without Gregorian assumptions |
| SMPTE timecode conventions | Frames, rate, drop-frame labels | `temporal-reference`, `clock`, `sequence` | Registry | Drop-frame labels do not imply dropped physical frames |
| Astronomy Julian Date / MJD and timescale standards | Astronomical coordinates, TT/UT1, ephemerides | `temporal-reference`, `instant` | Registry | Correct epoch and scale conversion with uncertainty |

## Interoperability fidelity levels

1. `native`: the full meaning is representable in a core UTV expression.
2. `composed-lossless`: preserved through multiple core expressions, with declared identity rules.
3. `adapter-lossless`: all standard semantics and selected lexical details are preserved by a complete tested adapter.
4. `adapter-lossy`: an explicit loss report identifies discarded or approximated semantics.
5. `unrepresentable`: no truthful mapping under the active vocabulary/profile; use extension or reject.
6. `unsupported`: meaning is defined but current engine lacks the necessary rule/algorithm.

Do not promote a native shape to adapter-lossless based solely on similarity of field names. Equivalence testing must cover canonicalization, date arithmetic, negative/out-of-domain inputs, recurrence expansion, exceptions and metadata round-trips.
