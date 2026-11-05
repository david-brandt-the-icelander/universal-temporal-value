# Examples: traditional date/time tasks in UTV

[`traditional.json`](traditional.json) is a catalog of 37 everyday date and time tasks. Each entry has:

- the call you would usually write in a conventional library (`usual`);
- the same thing as a UTV 1.0 value;
- why the UTV form is different (`point`);
- the outcome every UTV library must report today (`expect`).

Every language library has a runner that walks this catalog, prints a tour and fails if any outcome differs from the catalog. The catalog is shared so the five languages can't drift apart.

| Section | What it shows |
|---|---|
| Dates | Calendar dates, leap years (including the 100 and 400 rules), 30 February, month 13, years beyond 9999 and before 1, year-month, year-only, a non-Gregorian calendar |
| Times | Time of day without a date, the 23:59:60 leap-second label |
| Date-times | Floating local time, fixed offset vs named zone, a future meeting, the DST gap and overlap, timestamps with exact fractions |
| Durations | Elapsed seconds vs a calendar day vs a month |
| Intervals | Half-open and closed ranges, open-ended vs unknown end, start + duration, disjoint ranges |
| Recurrence, uncertainty | A weekly meeting in Paris, "sometime between 1340 and 1355" |
| Refusals | Newer version, malformed version, duplicate keys, unknown kind |

## Running them

| Language | Command (from `implementations/<language>/`) |
|---|---|
| Rust | `cargo run --example traditional` |
| Python | `PYTHONPATH=src python3 examples/traditional.py` |
| TypeScript | `npm run example` |
| Java | `mvn -q compile exec:java` (see the Java README) |
| C# | `dotnet run --project examples/Traditional` |

Each library's normal test command also checks the catalog, so a library can't pass its tests while disagreeing with it.

## What the examples can't show yet

The libraries are at 0.1.0. They can tell whether a value is well-formed and whether a Gregorian date exists, but they don't yet perform the usual *operations*: adding a month, comparing, resolving a zoned time to an instant, expanding a recurrence. Entries that depend on one of those carry a `later` note naming the operation and its milestone from [`implementations/csharp/docs/engine-interface.md`](../implementations/csharp/docs/engine-interface.md). [`docs/operations.md`](../docs/operations.md) has the full list.

When an operation lands, its examples move from a `later` note into the `expect` block, for every language at once.
