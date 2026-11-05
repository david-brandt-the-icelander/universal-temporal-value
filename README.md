# Universal Temporal Value (UTV)

**A temporal data model that tells the truth about time, including when the truth is "it depends" or "nobody knows".**

| | |
|---|---|
| Specification | **1.0** ([`spec/SPECIFICATION.md`](spec/SPECIFICATION.md)) |
| Wire identifier | `utv/1.0` |
| Structural schema | [`schema/utv-1.0.schema.json`](schema/utv-1.0.schema.json) (JSON Schema 2020-12) |
| Implementations | C#, Java, Python, Rust and TypeScript libraries (0.3) under [`implementations/`](implementations/) |
| Side by side | [`docs/side-by-side.md`](docs/side-by-side.md): everyday tasks with each language's own date/time API next to UTV |
| License | [MPL-2.0](LICENSE) |

---

## Why this exists

Most date/time libraries are built around a few convenient simplifications. These sound reasonable, and every one of them is false:

- A day has 86,400 seconds.
- Every local date and time happens exactly once.
- A UTC offset identifies a time zone, and a time-zone abbreviation identifies an offset.
- Daylight-saving changes are always one hour, at 2 a.m.
- Adding a month and then subtracting a month gets you back where you started.
- Time-zone rules for next year are already known.
- A timestamp with nanosecond digits is accurate to the nanosecond.
- Clocks never go backwards, and two machines agree on the time.
- A larger timestamp means the event happened later, or caused the other one.
- Every date belongs to the Gregorian calendar, and every day starts at midnight.
- "Sometime in 1347" is the same as 1347-01-01T00:00:00Z.

Standard libraries usually choose one of these simplifications for you. They put it in a type such as `DateTime`, `Instant` or `timestamp`, and from then on the type quietly enforces it. The resulting bugs are well documented. A meeting moves by an hour after a government changes its DST rules. A billing job runs twice, or never, on a transition night. "Last day of the month" arithmetic drifts. Logs from two servers sort into an impossible order. A historical date gets silently converted to a calendar its source never used.

A standard library can't easily fix this: its value is in being simple, and the simplifications are the simple part. **UTV aims to be correct as far as possible, not merely convenient.** It is a single, serializable value model whose types encode what is actually known about a moment, a span or a schedule. When a question has no single answer, it says so.

## What "correct" means here

UTV doesn't claim to know everything about time. It claims to **never invent knowledge it doesn't have**. In practice:

- **Answers can be plural or absent.** A local time can resolve to zero instants (it falls in a DST gap), one, or several (it falls in a DST overlap). These outcomes are `none`, `unique` and `multiple`, and none of them is an error.
- **"Unknown" is not "unsupported" is not "invalid".** `indeterminate` means even a perfect engine couldn't decide from the available information. `unsupported` means this engine lacks the rules or algorithm. `invalid` means the input contradicts itself. These are all honest outcomes, and none of them is ever replaced by a guess.
- **Different kinds of time are different types.** A civil date, an exact physical instant, a monotonic clock reading, a Lamport clock, an elapsed duration and a calendar period aren't interchangeable, and no conversion between them happens silently.
- **Intent is preserved.** "09:00 every Tuesday in Paris" stays a wall-clock intention. It isn't flattened into a list of UTC instants that go stale when the law changes.
- **Rules are explicit data.** Time-zone databases, leap-second tables, calendars and holiday rules are versioned, content-addressed dependencies. Nothing comes from the host's clock, time zone, locale or network.
- **Precision is not accuracy.** Measurement quality, uncertainty and correlated clock errors are first-class.
- **Every conversion reports what it lost.**

### What it looks like in code

```csharp
using DateOutcome = Utv.Outcome<Utv.CivilDate>;

var jan31 = new DateOnly(2026, 1, 31);
jan31.AddMonths(1);                                  // 2026-02-28, clamped without a word

switch (CivilDate.Of(2026, 1, 31).AddMonths(1))      // an Outcome: the answer may not exist
{
    case DateOutcome.Unique(var d): Use(d); break;
    case DateOutcome.None(var why): Ask(why); break; // ← runs: 2026-02 has no day 31 (month-end)
}
CivilDate.Of(2026, 1, 31).AddMonths(1, MonthEnd.Constrain);   // 2026-02-28, because you asked
```

The same in Java, Python, Rust and TypeScript, task by task, is in [`docs/side-by-side.md`](docs/side-by-side.md).

### A small example

On 8 March 2026, clocks in New York jump from 01:59:59 to 03:00:00. Most libraries asked for 02:30 that night will quietly return 03:30, or 01:30. In UTV, the value is a valid statement of intent:

```json
{
  "schema": "utv/1.0",
  "kind": "civil",
  "value": {
    "form": "datetime",
    "fields": { "year": "2026", "month": 3, "day": 8, "hour": 2, "minute": 30 },
    "calendar": { "id": "gregory", "chronology": "proleptic" },
    "zone": { "type": "iana", "id": "America/New_York" },
    "zoneIntent": "follow-zone-id",
    "resolutionPolicy": { "gap": "preserve-unresolved", "overlap": "all" }
  }
}
```

Resolving it under a pinned tzdb release gives status `none`: proven to match zero instants. The original intent is kept, so a caller can choose an explicit policy instead (for example `shift-forward-by-gap`), and the result records what changed and what was lost.

## What UTV covers

One envelope, `{"schema": "utv/1.0", "kind": …, "value": …}`, with 26 composable kinds:

| You might call it… | UTV representation |
|---|---|
| Timestamp / instant | `instant` (coordinate in a named timescale: POSIX, TAI, UTC, GPS, TT, …) |
| Date, time, date-time, partial date, week date | `civil` with a `form` |
| Date-time with offset or named zone | `civil` with `zone`, `zoneIntent` and resolution policy |
| Duration vs. calendar period | `amount` (`elapsed` SI seconds vs. `calendar` components vs. `business`, …) |
| Interval / range (open, closed, half-open, unbounded, unknown endpoints) | `interval` (start–end, start+amount, amount+end) |
| Disjoint ranges, alternatives | `set` |
| Recurring events (RFC 5545 / 7529 / JSCalendar) | `recurrence` |
| "circa 1200", "between 3 and 5 pm", probability distributions | `uncertain` |
| Clock readings, monotonic clocks, observations | `clock`, `observation` |
| Lamport / vector / hybrid logical clocks | `logical-clock` |
| "next Friday", "end of fiscal quarter" | `relative` |
| "30 min before breakfast", "three times a day" | `event-relative`, `pattern` |
| "third Wednesday", "last business day" | `selection`, `calendar-rule` |
| Bitemporal facts and corrections | `assertion`, `event-history` |
| "the Bronze Age", geological stages | `ordinal-position`, `temporal-reference` |
| Allen interval relations, partial orders | `relation`, `temporal-graph` |
| Time series and sensor samples | `series`, `sample`, `sequence` |
| Location-dependent legal time | `location-intent` |
| Arbitrary temporal constraints | `constraint` |

`spec/STANDARDS-MATRIX.md` maps UTV to ISO 8601-1/-2, RFC 3339, RFC 9557, IANA tzdb, RFC 5545/7529/8984, ISO 19108, OWL-Time, HL7 FHIR Timing, OGC SensorThings/TimeseriesML, SQL:2011 and others. `spec/FALSEHOODS-MATRIX.md` traces 62 well-known falsehoods to the design rule that prevents each one.

## Repository layout

```
spec/                   Normative specification (the source of truth)
  SPECIFICATION.md        Core model (Part I) and standards convergence (Part II)
  DECISIONS.md            Binding architectural decisions
  RESULT-CONTRACT.md      Semantic operation results: statuses, manifests, loss reports
  STANDARDS-MATRIX.md     Coverage of published standards
  FALSEHOODS-MATRIX.md    Falsehood → safeguard → scenario traceability
  OPEN-ISSUES.md          Known gaps to resolve in later versions
schema/                 Structural JSON Schemas, one file per spec version
conformance/            Language-neutral test material shared by all implementations
  examples/               Structurally valid and invalid values
  scenarios/              Semantic scenarios every engine must eventually satisfy
implementations/        Language libraries (one directory per language)
tools/                  Repository tooling (structural validation)
docs/                   Background, including how the design evolved
```

Start with `spec/SPECIFICATION.md`, then `spec/DECISIONS.md` and `spec/RESULT-CONTRACT.md`. The JSON Schema checks shapes only. A value can pass it and still be semantically invalid or unresolvable.

## Status

UTV 1.0 is a **complete design specification**. Libraries in five languages (0.3) implement an everyday subset; see [`implementations/`](implementations/) and [`docs/library-api.md`](docs/library-api.md). Beyond that subset:

- The structural schema and examples are validated. Run `python3 tools/validate.py`; CI runs it on every push.
- The 94 semantic scenarios in `conformance/scenarios/` are **requirements**, not executable tests yet.
- `spec/OPEN-ISSUES.md` lists the known places where the prose and schema still need to be reconciled or formalized.

No finite specification can anticipate every future calendar, physical theory or legal convention. UTV handles this with a typed, versioned extension and registry contract, not with a claim of literal completeness.

## Versioning

The specification, the wire identifier and the schema share one version number. **This is version 1.0.** Language libraries are versioned independently and declare which specification versions they support. See [VERSIONING.md](VERSIONING.md) and [CHANGELOG.md](CHANGELOG.md).

## Contributing an implementation

See [`implementations/README.md`](implementations/README.md) for the layout and the minimum obligations every language library must meet.
