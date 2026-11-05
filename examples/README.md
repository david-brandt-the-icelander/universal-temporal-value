# Examples

Two kinds of examples, both runnable in every language library.

## 1. Side by side: what you write today, and what you'd write with UTV

Each library has an **everyday** example with 14 short sections. In each section, the language's own date/time types do an ordinary task (`DateTime`/`DateOnly` in C#, `java.time`, Python `datetime`, JavaScript `Date`, Rust `chrono`), and UTV does the same task right next to it. Both are run for real, and the output says what the native API actually did when the two differ.

1. Now
2. A date
3. Invalid dates
4. Adding days and months
5. Days between and comparing
6. Times and date-times
7. Parsing with offsets (`Z` vs `+00:00`)
8. Time zones
9. DST gap
10. DST overlap
11. Timestamps
12. Durations vs periods
13. Ranges
14. Round trips to native types

| Language | Command (from `implementations/<language>/`) | Native types compared |
|---|---|---|
| C# | `dotnet run --project examples/Everyday` | `DateTime`, `DateOnly`, `TimeOnly`, `DateTimeOffset`, `TimeZoneInfo` |
| Java | `mvn -q test-compile exec:java@everyday` | `java.time` |
| Python | `PYTHONPATH=src python3 examples/everyday.py` | `datetime`, `zoneinfo` |
| TypeScript | `npm run everyday` | `Date`, `Intl.DateTimeFormat` |
| Rust | `cargo run --example everyday --features chrono` | `chrono`, `chrono-tz` (std has no calendar types) |

Some things the examples show, as observed on the build machine:

| Task | Typical native result | UTV |
|---|---|---|
| 31 January 2026 + 1 month | C#, Java and chrono clamp to 28 February silently. JavaScript rolls over to 3 March. Python's stdlib has no month arithmetic. | `none` (there is no 31 February) until you pass `monthEnd: constrain` |
| 02:30 on 8 March 2026 in New York (doesn't exist) | Java moves it to 03:30, Python accepts it, .NET throws on conversion, chrono returns `None` | `none`, or 03:30 EDT if you ask for `shift-forward-by-gap` |
| 01:30 on 1 November 2026 in New York (happens twice) | Most pick one offset without saying so; chrono reports both | `multiple`, with both instants, until you pick a policy |

UTV operations use the small pinned rule bundle in [`conformance/rules/demo-zones.json`](../conformance/rules/demo-zones.json) (New York, Paris, Reykjavik; 2026–2027 only), never the machine's time zone database. Native output can vary by host; UTV output doesn't. The operations are specified in [`docs/everyday-api.md`](../docs/everyday-api.md), and all five libraries pass the same 130 fixtures in [`conformance/operations/everyday.json`](../conformance/operations/everyday.json).

## 2. A tour of 37 traditional tasks as UTV values

[`traditional.json`](traditional.json) is a catalog of 37 everyday tasks. For each one it gives the conventional call (`usual`), the same thing as a UTV 1.0 value, why the UTV form differs (`point`), and the outcome every library must report (`expect`). It covers dates, leap years, times, offsets and zones, the DST gap and overlap, timestamps, durations vs periods, ranges, recurrence, uncertainty and refusals. Each library walks it, and its normal test command checks every entry.

| Language | Command (from `implementations/<language>/`) |
|---|---|
| C# | `dotnet run --project examples/Traditional` |
| Java | `mvn -q test-compile exec:java@traditional` |
| Python | `PYTHONPATH=src python3 examples/traditional.py` |
| TypeScript | `npm run example` |
| Rust | `cargo run --example traditional` |

Entries with a `later` note name operations that haven't been built yet (recurrence expansion, leap-second tables, and so on). [`docs/operations.md`](../docs/operations.md) has the full list.
