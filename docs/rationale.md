# Why UTV exists

This document explains the problem UTV addresses, the principles behind its design, and the trade-offs that come with it. It is written for engineers evaluating whether UTV fits their system. The normative definitions are in [`spec/SPECIFICATION.md`](../spec/SPECIFICATION.md); this page gives the reasoning behind them.

## 1. The problem

Most software represents time with a small set of general-purpose types: a timestamp, a date, a date-time, a duration. These types are deliberately simple, and that simplicity serves most programs well. But each one rests on assumptions about time, and some of those assumptions don't always hold.

The best-known catalogue of these assumptions is the 2012 InfiniteUndo post ["Falsehoods programmers believe about time"](https://infiniteundo.com/post/25326999628/falsehoods-programmers-believe-about-time) and its [follow-up](https://infiniteundo.com/post/25509354022/more-falsehoods-programmers-believe-about-time). Some examples from that genre:

- **A day has 86,400 seconds.** A civil day can be 23 or 25 hours long across a daylight-saving transition. A UTC day containing a leap second has 86,401 seconds.
- **Every local time occurs exactly once.** When clocks move forward, some local times never occur. When they move back, some occur twice.
- **A time zone is an offset.** An offset such as `+01:00` describes one moment. A named zone such as `Europe/Paris` is a set of rules that changes over time.
- **Time-zone rules are fixed.** Governments change daylight-saving rules, sometimes with only weeks' or days' notice. The IANA time zone database is revised several times a year for this reason.
- **Month arithmetic is reversible.** 31 January plus one month has no exact answer. Whatever answer a library picks, subtracting a month again doesn't reliably return the starting date.
- **More digits mean more accuracy.** A timestamp with nanosecond digits is only as accurate as the clock that produced it.
- **Clocks move forward.** System clocks are stepped, slewed and corrected, and two machines rarely agree exactly.

These aren't hypothetical. Some documented cases:

- **Samoa** moved across the International Date Line at the end of 29 December 2011. The calendar went straight to 31 December, and [30 December 2011 never occurred there](https://en.wikipedia.org/wiki/Time_in_Samoa).
- **Sweden** had a [30 February in 1712](https://en.wikipedia.org/wiki/Swedish_calendar), the result of a gradual and later abandoned calendar transition.
- **Great Britain** moved to the Gregorian calendar in 1752. Wednesday 2 September was followed directly by Thursday 14 September.
- **A leap second** at the end of 31 December 2016 led to a negative time difference in a widely used DNS service. That caused partial resolution failures for several hours, as described in the operator's [public post-mortem](https://blog.cloudflare.com/how-and-why-the-leap-second-affected-cloudflare-dns/).

## 2. Where the assumptions live

These problems are usually discussed as programming mistakes. Many of them, though, follow from the shape of the types programs are given:

- **One value per date-time.** A date-time type that holds exactly one value has to do *something* with a local time that occurs twice, and with one that doesn't occur at all. Typically it picks one of the two, or shifts the time, and nothing tells the caller.
- **Digits imply accuracy.** A timestamp type has a fixed resolution, so every value looks equally precise, whatever its source.
- **Duration types make a choice.** A duration type either can't represent "one month", or represents it as a fixed number of days.
- **Comparison operators assume a total order.** For values in different time zones, or with uncertain endpoints, the order may depend on rules or may not be known.

Each of these is a reasonable design choice for a general-purpose library, and they are often the right defaults. The cost is that the choice is made once, inside the type, and is invisible at the point of use. UTV is for systems where that invisibility is the problem: scheduling across jurisdictions, auditing, historical and scientific data, and anything where a wrong answer is worse than no answer.

## 3. Design principles

UTV aims to be **correct where possible, and explicit where it can't be**. Its central rule is that an implementation never invents information it doesn't have.

1. **Questions about time can have zero, one or several answers.** Resolving a local time in a daylight-saving gap gives `none`; in an overlap it gives `multiple`, with both instants. Each of these is a correct result, not an error.
2. **Different kinds of "no answer" are kept apart.**
   - `indeterminate`: the information needed isn't available.
   - `unsupported`: this implementation lacks the rules or the capability.
   - `invalid`: the input contradicts itself.

   None of these is ever replaced with a guess.
3. **Different kinds of time are different types.** A civil date, an exact instant, a monotonic clock reading, a logical clock, an elapsed duration and a calendar period aren't interchangeable. UTV converts between them only when explicitly asked.
4. **Intent is preserved.** "09:00 every Tuesday in Paris" stays a wall-clock intention tied to a named zone. Turning it into instants is a separate step, so a change in the zone's rules doesn't corrupt the stored intention.
5. **Rules are explicit, versioned data.** Time zone, leap-second, calendar and holiday rules are dependencies, identified by version and content hash. Nothing is read from the host's clock, time zone or locale, so the same question with the same rules always gets the same answer.
6. **Conversions report what they lose.** For example, converting a zoned intention to a fixed instant loses the ability to follow future rule changes, and the result says so.

## 4. An example

On 8 March 2026, clocks in New York move from 02:00 directly to 03:00. A local time of 02:30 that night doesn't exist. In UTV, the intention is still a valid value:

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

Resolving it gives `none`. A caller who wants a time anyway asks for it with a named policy, such as `shift-forward-by-gap`, and the result records what changed.

The libraries expose the same idea as typed values. In C#:

```csharp
using InstantOutcome = Utv.Outcome<Utv.Instant>;

var ctx = TemporalContext.With(ZoneRules.Load("conformance/rules/demo-zones.json"));
var wall = CivilDateTime.Of(2026, 3, 8, 2, 30).InZone("America/New_York");

switch (wall.Resolve(ctx))
{
    case InstantOutcome.Unique(var instant): Schedule(instant); break;
    case InstantOutcome.None(var why):       AskTheUser(why); break;    // this case: a DST gap
    case InstantOutcome.Multiple(var both):  AskTheUser(both); break;   // a DST overlap
}
```

Operations that can't fail, such as adding days to a valid date, return plain values. Only questions that may have no answer, or several, return an `Outcome`. See [`library-api.md`](library-api.md).

## 5. How the design was developed

The specification was developed iteratively. Each draft was reviewed against two things: the published catalogues of incorrect time assumptions, and the relevant standards, listed in [`spec/STANDARDS-MATRIX.md`](../spec/STANDARDS-MATRIX.md). Every gap found was either addressed in the design or recorded as an open issue. [`spec/FALSEHOODS-MATRIX.md`](../spec/FALSEHOODS-MATRIX.md) traces each known false assumption to the design rule that addresses it. [`design-history.md`](design-history.md) records the drafts, and why each change was made.

The libraries were developed against shared, executable fixtures whose expected results were computed independently of the implementations, so all five libraries give the same answers.

## 6. Trade-offs

UTV asks more of its users than a conventional date-time type does. That cost is deliberate, but it is real.

- **More concepts.** Callers deal with kinds, intents, policies and outcomes. The typed libraries keep the common path short: operations that can't fail return ordinary values, and only partial operations need outcome handling.
- **Explicit rules.** Time-zone data has to be supplied and pinned rather than taken from the host. That makes results reproducible, but it adds setup.
- **Interoperability.** Most code already uses a platform's native types. Each library has explicit conversions to and from them, and they report what a conversion can't carry over.
- **The cost of being wrong.** Most of the failures above occur on a few days a year: daylight-saving transitions, month ends, leap seconds and rule changes. They are rare enough to be overlooked in testing, but their effects can be significant when they occur. Whether the extra explicitness is worth it depends on the system.

## 7. How native APIs compare

[`side-by-side.md`](side-by-side.md) shows everyday tasks in C#, Java, Python, Rust and TypeScript, with each language's standard date/time API next to UTV. The native APIs behave differently from one another, each according to its own documented design. Some examples, observed when the examples were run:

| Task | Native behaviour observed | UTV |
|---|---|---|
| 31 January + 1 month | Clamped to 28 February (.NET, `java.time`, chrono); rolled over to 3 March (JavaScript `Date`); no month arithmetic (Python standard library) | `none` by default; 28 February with an explicit `Constrain` policy |
| 02:30 on 8 March 2026, New York | Shifted to 03:30 (`java.time`); accepted as given (Python `zoneinfo`); exception on conversion (.NET); no result (chrono) | `none`, or a shifted time with an explicit policy |
| 01:30 on 1 November 2026, New York | One of the two times selected by default (most APIs); both reported (chrono) | `multiple`, with both instants |

These are documented behaviours, not defects: each API makes a reasonable choice for its typical users. UTV's choice is to make the decision visible and leave it to the caller.

## Further reading

- [`spec/SPECIFICATION.md`](../spec/SPECIFICATION.md): the normative specification.
- [`spec/FALSEHOODS-MATRIX.md`](../spec/FALSEHOODS-MATRIX.md): known false assumptions, traced to design rules.
- [`docs/operations.md`](operations.md): the questions programs ask of temporal values.
- [`docs/library-api.md`](library-api.md): the library API shared by all five languages.
- [`docs/side-by-side.md`](side-by-side.md): everyday tasks in five languages.
