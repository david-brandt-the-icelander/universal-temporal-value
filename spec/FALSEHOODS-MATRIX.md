# UTV 1.0 — Falsehoods coverage and design traceability

This matrix groups the recurring false assumptions from the classic lists, time-zone literature, and the design reviews recorded in the design history (`.dev/decisions.json`). It is **not** a claim that every conceivable future assumption has been enumerated. Every row specifies a semantics decision and a planned test case. A complete implementation MUST add executable tests for every ID.

| ID | False assumption | UTV safeguard | Conformance scenario | Variant(s) | Spec § |
|---|---|---|---|---|---|
| CAL-01 | Day always 24 hours / 86400 seconds | Civil/amount distinctions; named day boundary; DST | civil local day over spring transition | `civil, amount` | 4,5 |
| CAL-02 | All months and years have fixed lengths | Calendar registry; month-end arithmetic policy | Feb 29, leap and intercalary months | `civil, amount` | 4,5 |
| CAL-03 | Week/year/month boundaries align | Week system independent; fielded dates | ISO week 2020-W53 | `civil` | 4 |
| CAL-04 | The Gregorian calendar is universal | Registered calendar, chronology and eras | Historic Julian/Gregorian / Hebrew date | `civil` | 4 |
| CAL-05 | Every calendar day begins at midnight | Registered day boundary and location | Sunset-based date | `civil` | 4 |
| CAL-06 | Historical dates can always be converted precisely | History uncertainty, jurisdiction and source | 1582 reform, partial evidence | `civil, uncertain` | 4,7 |
| CAL-07 | There is no year zero or negative years | Explicit chronology and era semantics | 1 BCE vs astronomical year 0 | `civil` | 4 |
| CAL-08 | Every date has a clock time | Partial date is not midnight | 1347 year-only | `civil` | 4 |
| CAL-09 | Month/day arithmetic is reversible | Operation-specific clamp/carry and ordering | Jan 31 +1 month −1 month | `amount, civil` | 5 |
| CAL-10 | Every Earth day exists in a zone | Local-day resolution can have zero solutions | Pacific/Apia 2011-12-30 | `civil` | 4 |
| ZONE-01 | One offset identifies a time zone | Distinct named and fixed offset variants | +01:00 vs Europe/Paris | `civil` | 4 |
| ZONE-02 | Time-zone abbreviations unique | Abbreviation not valid zone identity | CST ambiguous | `civil` | 4 |
| ZONE-03 | DST always +/- 1 hour | Offset changes arbitrary seconds/days | Lord Howe, Apia | `civil` | 4 |
| ZONE-04 | DST always transitions at a certain time | Full tzdb transition rules | midnight transition | `civil` | 4 |
| ZONE-05 | Local datetime always identifies instant | 0/1/multiple/indeterminate resolution | NY DST overlap/gap | `civil` | 4 |
| ZONE-06 | Local transition has at most 2 candidates | General finite candidate set | synthetic multi-overlap rules | `civil` | 4 |
| ZONE-07 | Time-zone laws are fixed | Floating rules with re-resolution | future policy change | `civil, recurrence` | 4,10 |
| ZONE-08 | IANA zone always follows physical location | Geographical legal-time intent and mapping | zone boundary change | `location-intent` | 4 |
| ZONE-09 | IANA tzdb fully represents historical reality | Uncertainty/unknown/rule source | pre-1970 local times | `uncertain, civil` | 4,14 |
| ZONE-10 | UTC offset is always a whole minute/hour | Second-precision offsets and model registry | historic +00:09:21 | `civil` | 4 |
| ZONE-11 | Offset + zone mismatch can be safely ignored | Strict native consistency; RFC adapter difference | Paris July +01:00 | `civil` | 2,4 |
| ZONE-12 | Locations never change zone during event | Trajectory or per-occurrence location | travel crossing zone boundary | `location-intent, recurrence` | 4,10 |
| CLK-01 | Computer clock is correct | Quality/source/error bound separate | clock off by years | `observation` | 8 |
| CLK-02 | Clocks always move forward | Clock generation, reset, step and slew | NTP backward step | `clock` | 8 |
| CLK-03 | Clocks advance at constant speed | Drift/calibration/holdover model | temperature drift | `clock` | 8 |
| CLK-04 | NTP/PTP imply bounded accuracy | Calibration evidence required | NTP without bound | `observation` | 8 |
| CLK-05 | Two systems agree on now | Cross-clock correlation/uncertainty | cross-host event ordering | `observation, clock` | 8 |
| CLK-06 | Monotonic clocks have universally identical semantics | Clock registry: suspend/reboot/wrap | suspend-aware vs pause-on-suspend | `clock` | 8 |
| CLK-07 | Clock reading is unique / never repeats | Event IDs separate, logical ordering | same timestamp twice | `observation` | 8,9 |
| CLK-08 | Timestamp implies causal sequence | Protocol-specific causal metadata | Lamport counter mismatch | `logical-clock` | 9 |
| CLK-09 | Measuring short durations is exact | Clock-rate/quantization/quality | timer resolution and drift | `clock, uncertain` | 7,8 |
| CLK-10 | Latency equals receivedAt minus sentAt | Sync uncertainty and transport model | clock-skew negative latency | `observation` | 8 |
| CLK-11 | A timestamp means the event occurred then | Observation roles and assertion source | logged at vs happened at | `assertion, observation` | 8,11 |
| CLK-12 | UTC/POSIX/SI time all the same | Registered timescales/leap semantics | 2016 leap second | `instant` | 3 |
| CLK-13 | Leap seconds always positive or always exist | Versioned UTC/leap-rule model | negative leap model, changed UTC policy | `instant` | 3,14 |
| CLK-14 | All clocks smear leap seconds identically | Provider-specific smear model | Google vs other smear profile | `instant, observation` | 3,8 |
| CLK-15 | Leap-second timestamps roundtrip via POSIX | Non-bijective conversion result | 23:59:60 UTC label | `instant` | 3 |
| CLK-16 | Unix timestamp always unambiguous physical time | Registry epoch/leap/time uncertainty | POSIX leap folding | `instant` | 3 |
| HIST-01 | Historical calendar law equals actual practice | Legal vs observed practice separate | clocks deliberately stopped | `assertion, civil` | 4,11 |
| HIST-02 | Past records never change | Bitemporal revision chain | corrected date reported later | `event-history` | 11 |
| HIST-03 | Past tzdb always fixed | Immutable rule manifest and revisions | tzdb historical correction | `civil` | 4,16 |
| HIST-04 | A source-provided date is precisely known | Precision/epistemic uncertainty | approximately 1347 | `uncertain` | 7 |
| HIST-05 | BP/astronomical/geological dates same as Gregorian | Epoch/calibration registry | 12000 radiocarbon BP | `temporal-reference` | 14 |
| REC-01 | Weekly always means every 604800 seconds | Distinct elapsed vs local recurrence | weekly meeting across DST | `recurrence` | 10 |
| REC-02 | Recurring events can be specified by time alone | Rule+intent+zone+calendar+exceptions | first weekday of month | `recurrence` | 10 |
| REC-03 | Invalid dates and nonexistent times handled same | RFC5545 verified erratum and named policies | RRULE local 02:30 gap | `recurrence` | 10 |
| REC-04 | Recurrence occurrence identified by instant | Stable civil occurrence identity | tzdb changed scheduled instant | `recurrence` | 10 |
| REC-05 | All holidays/weekends universal | Versioned business/jurisdiction rules | market closed on local holiday | `calendar-rule` | 10 |
| REC-06 | Every recurrence can be fully expanded | Explicit finite budgets/continuations | daily recurrence forever | `recurrence` | 10,13 |
| REC-07 | Rules and jurisdiction never change after scheduling | Re-resolution and cache invalidation | country abolishes DST | `recurrence` | 10 |
| UNC-01 | Printed precision means accuracy | Precision, resolution, uncertainty separate | nine decimals ±1 s | `observation, uncertain` | 7 |
| UNC-02 | Uncertainty is necessarily a symmetric interval | Asymmetric, disjoint and distributions | asymmetric clock drift | `uncertain` | 7 |
| UNC-03 | Two errors are independent | Correlations, joint models and explicit unknown | two NTP-derived clocks | `uncertain` | 7 |
| UNC-04 | Unknown means uniformly distributed | Unknown != probability model | unobserved event | `uncertain` | 7 |
| UNC-05 | Any comparison yields true or false | Partial order and indeterminate | overlapping uncertain events | `relation, constraint` | 6,13 |
| UNC-06 | All timestamps refer to a single universal clock | Frame and proper-time reference systems | relativistic spacecraft | `temporal-reference` | 3,14 |
| SEC-01 | Valid JSON implies valid temporal semantics | 3-stage validation; unknown not coerced | February 30 | `any` | 13,15 |
| SEC-02 | Locale strings are portable dates | Explicit locale/calendar/parser policy | 03/04/05 | `relative` | 12 |
| SEC-03 | Time serializations are losslessly interchangeable | Adapter loss reports | ISO string lacking uncertainty | `any` | 2,13 |
| SEC-04 | Same timestamp provides event uniqueness | External IDs and event histories | two events same millisecond | `assertion` | 11 |
| SEC-05 | Rule data is safe/stable if version is named | Immutable digest and dependency closure | historical tzdb unavailable | `any` | 16 |
| SEC-06 | Unlimited time arithmetic is harmless | resource-limit status & budgets | unbounded recurrence explosion | `recurrence` | 15 |

## Test coverage status

62 distinct design cases defined. These are a **design coverage matrix**, not 62 executed semantic tests. The structural schema and example validation are separate.

Expected status vocabulary for tests: `invalid`, `none`, `unique`, `multiple`, `symbolic`, `indeterminate`, `unsupported`, `resource-limit`, `lossy`, or domain-specific property/invariant.
