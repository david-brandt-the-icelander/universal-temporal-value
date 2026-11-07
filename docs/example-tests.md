# Examples as unit tests

Every UTV library shows how it is used through **unit tests**, not example programs. The tests run with the
library's normal test command, so an example can't drift from what the library does. They are written for the
maintainer, as [`AGENTS.md`](../AGENTS.md) describes.

Each library has two example suites that cover the same scenarios from opposite ends:

- **UTV first.** Organized by UTV class and function. Each test does a task with UTV and compares the result with
  the language's standard date/time API. Where the two agree, the expected value is computed with the standard
  API. Where they differ, the expected value is a literal, and the doc comment says what the standard API does.
- **Native first.** Organized by the standard API's namespace, class and function. Each test does the task with
  the standard API and compares the result with UTV, the same way round.

Every test's doc comment has two sentences: "Verifies that …" for the behavior under test, and "Compared with: …"
for the other side.

| Language | UTV first | Native first | Standard API |
|---|---|---|---|
| C# | [`tests/Utv.Examples.Tests/UtvFirst`](../implementations/csharp/tests/Utv.Examples.Tests/UtvFirst) | [`tests/Utv.Examples.Tests/NativeFirst`](../implementations/csharp/tests/Utv.Examples.Tests/NativeFirst) | `DateOnly`, `TimeOnly`, `DateTime`, `DateTimeOffset`, `TimeSpan`, `TimeZoneInfo`, `ISOWeek`, `HebrewCalendar`, `JsonDocument` |
| Java | [`src/test/java/…/examples/utvfirst`](../implementations/java/src/test/java/net/icelander/utv/examples/utvfirst) | [`src/test/java/…/examples/nativefirst`](../implementations/java/src/test/java/net/icelander/utv/examples/nativefirst) | `java.time` |
| Python | [`tests/examples/utv_first`](../implementations/python/tests/examples/utv_first) | [`tests/examples/native_first`](../implementations/python/tests/examples/native_first) | `datetime`, `zoneinfo`, `json` |
| TypeScript | [`test/examples/utv-first`](../implementations/typescript/test/examples/utv-first) | [`test/examples/native-first`](../implementations/typescript/test/examples/native-first) | `Date`, `Intl.DateTimeFormat`, `JSON` |
| Rust | [`tests/utv_first`](../implementations/rust/tests/utv_first) | [`tests/native_first`](../implementations/rust/tests/native_first) | `chrono`, `chrono-tz`, `std::time`, `serde_json` (std has no calendar types) |

UTV's zone operations always use the pinned rules in
[`conformance/rules/demo-zones.json`](../conformance/rules/demo-zones.json) (New York, Paris, Reykjavik; 2026–2027
only), loaded through a shared fixture. The standard APIs necessarily read the host's tz database, so the
native-first zone tests need one that knows the 2026–2027 rules of New York and Paris.

## Scenario catalog

Every library covers every scenario below in its UTV-first suite. The native-first suite covers each scenario that
the language's standard API can express at all. Test names below are the C# names; other languages use the same
words in their own casing and grouping (for example `CivilDate` → `add_months` →
`returns_none_when_the_day_is_missing_from_the_target_month` in Rust).

The **task** column links a scenario to its section of [`side-by-side.md`](side-by-side.md). The UTV-first tests
for a task are wrapped in `BEGIN <task>` / `END <task>` comments, and `tools/side_by_side.py` lays them out
across the five languages.

### Dates — `CivilDate`

| Scenario | Task | UTV first | Native first (C#) |
|---|---|---|---|
| 5 November 2026 | `a-date` | `CivilDate.Of` creates the date when the fields name a real day | `DateOnly` constructor creates the date |
| 29 February 2024 (leap year) | | `CivilDate.Of` accepts the leap day when the year is a leap year | `DateTime.IsLeapYear` agrees with UTV for any kind of year |
| 29 February 2000 (divisible by 400) | | `CivilDate.Of` accepts the leap day when the century is divisible by 400 | (same) |
| 29 February 2026 (common year) | | `CivilDate.TryOf` returns invalid (`day-out-of-range`) | (same) |
| 29 February 1900 (century rule) | | `CivilDate.TryOf` returns invalid (`day-out-of-range`) | (same) |
| Leap years, all four rules | | `CivilDate.IsLeapYear` agrees with the standard API (parameterized) | `DateTime.IsLeapYear` agrees with UTV (parameterized) |
| 30 February 2026 | `feb-30` | `CivilDate.Of` throws; `CivilDate.TryOf` returns invalid (`day-out-of-range`) | `DateOnly` constructor throws |
| Month 13 | | `CivilDate.TryOf` returns invalid (`month-out-of-range`) | `DateOnly` constructor throws |
| Year 275760 | | `CivilDate.Of` creates the date (`+275760-09-13`) | `DateOnly` constructor throws |
| Year −500 | | `CivilDate.Of` creates the date (`-0500-03-01`) | `DateOnly` constructor throws |
| Parsing `2026-11-05` | | `CivilDate.Parse` returns the date | `DateOnly.Parse` reads the date |
| Parsing `2026-02-30` | | `CivilDate.Parse` returns invalid (`day-out-of-range`) | `DateOnly.TryParse` returns false, with no reason |
| Weekday and ISO week | `weekday-week` | `CivilDate.DayOfWeek` is Thursday; `CivilDate.IsoWeek` is 45 | `DateOnly.DayOfWeek` numbers Sunday 0 (ISO: 7); `ISOWeek.GetWeekOfYear` is 45 |
| 5 Nov + 30 days | `add-days` | `CivilDate.AddDays` returns the later date | `DateOnly.AddDays` returns the later date |
| 31 Jan + 1 month | `month-end` | `CivilDate.AddMonths` returns none (`month-end`); with `MonthEnd.Constrain` returns 28 Feb | `DateOnly.AddMonths` clamps to 28 Feb silently |
| 31 Jan + `P1M` | | `CivilDate.Add(Period)` returns none (`month-end`) | — (no calendar period type) |
| 5 Nov → 25 Dec | `days-between` | `CivilDate.DaysUntil` returns 50 | difference of `DateOnly.DayNumber` is 50 |
| Which date comes first | `compare-dates` | `CivilDate` `<` is true when the left date comes first | `DateOnly` `<` is true when the left date comes first |
| Beyond the native range | | `CivilDate.ToDateOnly` returns unsupported (`out-of-native-range`) for year 10000 | `DateOnly.MaxValue` is 9999-12-31 |

### Months, times and date-times

| Scenario | Task | UTV first | Native first (C#) |
|---|---|---|---|
| November 2026 | | `CivilYearMonth.Of` creates a month without a day; `AddMonths` carries into the next year | — (no year-month type; the stand-in is day 1) |
| 09:30 | `time-of-day` | `CivilTime.Of` writes no seconds when none are given, and writes them when they are | `TimeOnly.ToString("O")` writes seconds and ticks |
| 23:30 + 2 hours | `datetime-plus-hours` | `CivilDateTime.AddHours` carries into the next day | `DateTime.AddHours` carries; `TimeOnly.AddHours` wraps and loses the day |
| 14:30, no zone | | `CivilDateTime.Of` creates a wall-clock date-time | `DateTime` constructor gives Kind Unspecified |
| 23:59:60 | | `CivilDateTime.TryOf` returns unsupported (`leap-second`) | `DateTime` constructor throws |
| A machine-local reading | `now` | `CivilDateTime.FromDateTime` returns unsupported (`local-zone`) for Kind Local | `DateTime.Now` returns Kind Local |
| 14:30+01:00 | | `CivilDateTime.AtOffset`, `FixedOffsetDateTime.FromDateTimeOffset`, `FixedOffsetDateTime.ToInstant` | `DateTimeOffset` constructor pins the wall time to the offset |
| Round trip | `round-trip` | `FixedOffsetDateTime.ToDateTimeOffset` returns the original | `DateTimeOffset.EqualsExact` is true after a trip through UTV |
| `Z` vs `+00:00` | `parse-offsets` | `Temporal.Parse` returns an `Instant` for `Z` and a `FixedOffsetDateTime` for `+00:00` | `DateTimeOffset.Parse` cannot tell them apart |

### Zones — `ZonedIntent`, `Instant`, `TemporalContext`

| Scenario | Task | UTV first | Native first (C#) |
|---|---|---|---|
| 09:00 in Paris | `meeting-in-paris` | `CivilDateTime.InZone` keeps wall time and zone; `ZonedIntent.Resolve` returns the instant; `Instant.InZone` gives 03:00 in New York | `TimeZoneInfo.ConvertTimeToUtc`, `TimeZoneInfo.ConvertTimeFromUtc` |
| 02:30 on 8 March 2026, New York | `dst-gap` | `ZonedIntent.Resolve` returns none (`gap`); with `Gap.ShiftForward` returns 03:30 EDT | `TimeZoneInfo.IsInvalidTime` is true; `ConvertTimeToUtc` throws |
| 01:30 on 1 November 2026, New York | `dst-overlap` | `ZonedIntent.Resolve` returns both instants (`ambiguous`), one of which the standard API picks; with `Overlap.Earliest` returns the first | `TimeZoneInfo.IsAmbiguousTime` is true; `ConvertTimeToUtc` picks the later instant (UTV's `Overlap.Latest`) |
| 01:30 vs 01:45 that night | `compare-dates` | `TemporalContext.Compare` returns multiple (`ambiguous`) | — |
| Noon + 1 day across DST | `day-vs-24h` | `ZonedIntent.AddDays` keeps noon; `Instant.Until` gives 82,800 s; `Instant.Add(PosixSeconds)` gives 13:00; `Instant.Add(ElapsedSeconds)` returns unsupported (`needs-leap-seconds`) | `DateTimeOffset.AddDays` keeps the offset, so the wall time is 13:00 |

### Timestamps and amounts — `Instant`, `Period`, `ElapsedSeconds`

| Scenario | Task | UTV first | Native first (C#) |
|---|---|---|---|
| Now | `now` | `Instant.FromDateTimeOffset` takes the clock reading as an input | `DateTimeOffset.UtcNow` survives the trip through UTV |
| The epoch | | `Instant.FromUnixSeconds(0)` | `DateTimeOffset.FromUnixTimeSeconds(0)` |
| Nanoseconds | | `Instant.FromUnixSeconds` keeps all nine digits | `DateTimeOffset.AddTicks` keeps seven |
| A quarter second | `timestamps` | `Instant.FromUnixSeconds` keeps the fraction; `Instant.Until` returns exactly 14403/4 s | `DateTimeOffset.ToUnixTimeSeconds` drops the fraction; subtraction gives a double |
| One calendar day | | `Period.Days(1)` is not 86,400 seconds | `TimeSpan.FromDays(1)` equals 86,400 seconds |
| One month | | `Period.Months(1)` is `P1M`, never `PT1M` | — (`TimeSpan` has no months) |
| 90 minutes | | `ElapsedSeconds.Of(5400)` | `TimeSpan.FromMinutes(90)` |
| 86,400 SI seconds | | `ElapsedSeconds.Of(86400)` is neither a calendar day nor POSIX seconds | (same as one calendar day) |

### Ranges — `Interval`

| Scenario | Task | UTV first | Native first (C#) |
|---|---|---|---|
| November, half-open | `ranges` | `Interval.HalfOpen` definitely contains 15 Nov and cannot contain 1 Dec | — (bounds are written by hand) |
| November, closed | | `Interval.Closed` definitely contains 30 Nov | — |
| Open-ended | | `Interval.Of(…, Bound.Unbounded)` definitely contains 1 Jan 3000 | — (a `null` end) |
| Unknown end | `ranges` | `Interval.Of(…, Bound.Unknown)` possibly contains 1 Jun 2027 | — (the same `null` end) |

### JSON layer — `UtvValidator`, `GregorianCivil`

Some values have no typed API yet and no standard counterpart at all. They are written as UTV JSON.

| Scenario | UTV first | Native first (C#) |
|---|---|---|
| A real date as JSON | `GregorianCivil.Validate` returns valid | — |
| 29 February 2026 as JSON | `GregorianCivil.Validate` returns invalid (`day-out-of-range`) | — |
| A Hebrew date | `GregorianCivil.Validate` returns unsupported | `HebrewCalendar.ToDateTime` converts it to a Gregorian date |
| The year 1347 | `UtvValidator.ValidateStructural` and `GregorianCivil.Validate` return valid | — |
| 23:59:60 UTC as JSON | `UtvValidator.ValidateStructural` returns valid | — |
| A two-hour meeting as start + amount | `UtvValidator.ValidateStructural` returns valid | — |
| Two separate ranges | `UtvValidator.ValidateStructural` returns valid | — |
| Every Tuesday at 09:00 in Paris | `UtvValidator.ValidateStructural` returns valid | — |
| About 1347 (1340–1355) | `UtvValidator.ValidateStructural` returns valid | — |
| `utv/2.0` | `UtvValidator.ValidateStructural` returns unsupported | — |
| `utv/4` | `UtvValidator.ValidateStructural` returns invalid | — |
| Duplicate keys | `UtvValidator.ValidateStructural` returns invalid (`duplicate-key`) | `JsonDocument.Parse` keeps the last value |
| Unknown kind | `UtvValidator.ValidateStructural` returns invalid | — |

### Shared fixture

| Fixture | Test |
|---|---|
| `Fixtures.DemoZoneRules()` loads `conformance/rules/demo-zones.json` | contains the three demo zones |
