# Everyday API (libraries 0.2)

This is the first set of *operations* every UTV library implements, beyond validation. It covers the everyday things people do with `DateTime`, `LocalDate`, `datetime`, `Date` and friends: build a value, parse and format ISO 8601, add days and months, compare, count days, test whether a range contains a date, and turn a local time in a named zone into an instant and back.

It is a subset of [`docs/operations.md`](operations.md). Where the specification is silent, this document is the contract. Every library must produce exactly the outcomes in [`conformance/operations/everyday.json`](../conformance/operations/everyday.json). That file is the arbiter: if this prose and the fixtures disagree, raise it rather than picking one.

Values are UTV JSON values, held in each language's ordinary JSON type, exactly as in 0.1. This is the **JSON layer**: from 0.3 on, the typed layer in [`library-api.md`](library-api.md) sits on top of it. The JSON layer also has `parseText` / `formatText` for the [text form](text-form.md). Diagnostic codes are shared by every library and listed in [`library-api.md` §5](library-api.md#5-diagnostic-codes); the fixtures check them through `expect.diagnostics`.

## Results

Every operation returns the library's existing `utv-result/1.0` result: a status, diagnostics and candidates. Candidates are UTV values, or plain JSON for operations that answer with a string, number or object (`compare`, `fields`, `daysBetween`, `contains`, `overlaps`). `unique` has exactly one candidate. `multiple` has all of them, in ascending order. `none`, `invalid`, `unsupported` and `symbolic` have none.

Operations never throw for semantic outcomes. Libraries never read the system clock, time zone or locale.

## Canonical shapes

Builders and operations produce exactly these shapes. Fixtures compare results as JSON (key order is irrelevant).

```text
CAL      = {"id":"gregory","chronology":"proleptic"}
POSIX    = {"system":"utv.timescale","id":"unix-posix","version":"1"}
civil    = {"schema":"utv/1.0","kind":"civil","value":{"form":F,"fields":{…},"calendar":CAL, …}}
```

| Builder | `form` | `fields` and extra properties |
|---|---|---|
| `date(y, m, d)` | `date` | `year` (decimal string), `month`, `day` |
| `yearMonth(y, m)` | `year-month` | `year`, `month` |
| `time(h, mi[, s[, nano]])` | `time` | `hour`, `minute`, plus `second` only if given, plus `nano` only if given and non-zero |
| `dateTime(y, m, d, h, mi[, s[, nano]])` | `datetime` | as `date` plus `time` fields. **No `zone` property**: an unzoned ("floating") local date-time |
| `inZone(dt, zoneId)` | (unchanged) | adds `"zone":{"type":"iana","id":zoneId}`, `"zoneIntent":"follow-zone-id"` |
| `withOffset(dt, seconds)` | (unchanged) | adds `"zone":{"type":"fixed-offset","seconds":s}`, `"zoneIntent":"fixed-offset"` |
| `instant(epochSeconds[, nano])` | n/a | `{"kind":"instant","value":{"system":POSIX,"ticks":"S"}}`, plus `"subTick":{"numerator":…,"denominator":…}` = nano/10⁹ **reduced to lowest terms**, only if nano ≠ 0 |
| `period(years, months, weeks, days, hours, minutes, seconds)` | n/a | `{"kind":"amount","value":{"basis":"calendar","components":{…}}}`: only non-zero components, values as decimal strings (may be negative) |
| `posixSeconds(n)` | n/a | `{"kind":"amount","value":{"basis":"clock-ticks","rules":POSIX,"seconds":{"numerator":"n","denominator":"1"}}}`. POSIX seconds, which are not SI seconds across a leap second |
| `interval(start, end[, startIncluded = true, endIncluded = false])` | n/a | `{"kind":"interval","value":{"domain":D,"start":{"state":"included"\|"excluded","point":start},"end":{…}}}`. `D` is `civil/gregory` for civil points and `physical` for instants. An endpoint may instead be `{"state":"unbounded"}` or `{"state":"unknown"}` |

Years are decimal strings with no leading zeros (`"0"`, `"-500"`, `"275760"`). An absent `zone` and `{"type":"floating"}` mean the same thing.

## Operations

| Operation | Arguments | Answer |
|---|---|---|
| `parseIso` | text | a UTV value |
| `formatIso` | value | a string |
| `fields` | civil `date` or `datetime` | `{"dayOfWeek":1–7 (Monday = 1), "dayOfYear", "isoWeekYear" (string), "isoWeek", "daysInMonth", "isLeapYear"}` |
| `add` | value, amount, policy (optional) | a UTV value |
| `compare` | a, b | `"before"`, `"equal"` or `"after"` (a relative to b) |
| `daysBetween` | date a, date b | integer number of days from a to b |
| `posixSecondsBetween` | instant a, instant b | a `posixSeconds` amount from a to b (a reduced rational) |
| `contains` | interval, point | `"definitely"`, `"possibly"` or `"impossible"` |
| `overlaps` | interval a, interval b | `"definitely"`, `"possibly"` or `"impossible"` |
| `resolve` | civil date-time, rules, policy (optional) | instant(s) |
| `toZone` | instant, zone id, rules | civil date-time in that zone |
| `toOffset` | instant, offset seconds | civil date-time with that fixed offset |

### `parseIso`

Only ISO 8601 **extended** format and RFC 3339 / RFC 9557 forms:

| Text | Result |
|---|---|
| `YYYY-MM-DD` | `date` |
| `YYYY-MM` | `yearMonth` |
| `hh:mm`, `hh:mm:ss`, `hh:mm:ss.fff…` | `time` |
| `YYYY-MM-DDThh:mm[:ss[.f…]]` | unzoned `dateTime` |
| … followed by `±hh:mm` | `withOffset` date-time (`+00:00` is an offset of 0, *not* UTC-with-unknown-offset) |
| … followed by `Z` or `-00:00` | `instant` (RFC 3339 / 9557: UTC is known and the local offset is not; nothing civil is kept) |
| … followed by `[Area/Location]` | `inZone` date-time |
| … followed by `±hh:mm[Area/Location]` | `inZone` date-time with `"offsetAssertion": seconds` |

- A year is four digits, or a sign followed by four or more digits (`+275760`, `-0500`). It is stored without leading zeros.
- A fraction of 1–9 digits becomes `nano` (omitted when the fraction is all zeros), and `second` is then present.
- Lower-case `t`/`z` and a space instead of `T` are `invalid`.

Outcomes:

- **`invalid`**: anything outside this grammar, an out-of-range field (month 13, 30 February, hour 24 with non-zero minutes, minute 60), or an offset beyond ±18:00.
- **`unsupported`**, for valid ISO/RFC forms this profile doesn't handle:
  - second `60` (needs a leap-second table);
  - `24:00` (needs an end-of-day policy);
  - week dates (`2026-W45-4`) and ordinal dates (`2026-309`);
  - basic format (`20261105`);
  - `Z` or `-00:00` followed by a `[zone]`;
  - critical annotations (`[!…]`).

### `formatIso`

Inverse of `parseIso` for the shapes above. Rules:

- **Years:** `0000`–`9999` print as four digits. Other years print with a sign and at least four digits (`+275760`, `-0500`).
- **Civil values:** seconds print only if the `second` field is present.
- **Fractions:** print with the fewest digits possible, without trailing zeros.
- **Offsets:** print as `±hh:mm`, so 0 is `+00:00`.
- **Zoned values:** print as `…[Zone]`, with the asserted offset before the bracket when `offsetAssertion` is present.
- **Instants:** print as UTC `YYYY-MM-DDThh:mm:ss[.f]Z`, always with seconds.
- **Anything else:** `unsupported`.

### `fields`

Applies to proleptic Gregorian `date` and `datetime` values. A non-existent date gives `invalid`; other calendars or forms give `unsupported`.

### `add`

`policy` is `{"monthEnd": "reject" | "constrain"}` and defaults to `reject`.

Steps:

1. Add years and months together, as `12·years + months`.
2. If the day doesn't exist in the resulting month:
   - `reject` → status **`none`**, diagnostic code `month-end`;
   - `constrain` → clamp to the last day of the month.
3. Add `7·weeks + days`.
4. Add hours, minutes and seconds as **wall-clock** arithmetic, carrying into days.

Every component may be negative. Type rules:

- **Forms:**
  - `date` accepts years, months, weeks and days; time components give `invalid`.
  - `year-month` accepts only years and months; anything else gives `invalid`.
  - `datetime` accepts every component.
  - `time` gives `unsupported` (wrapping past midnight loses information).
- **Zone:** a zone or offset on the input is carried over unchanged. The result is still a civil *intention*: adding a day to 09:00 in Paris gives 09:00 the next day in Paris, whatever the DST rules. Turning it into an instant is `resolve`'s job.
- **Instants:** an instant accepts only a `posixSeconds` amount, and the arithmetic is exact. An `elapsed` (SI) amount on a POSIX instant gives `unsupported`, because it needs a leap-second table. A `calendar` amount on an instant gives `invalid`.
- **Invalid input:** an input that isn't a valid date gives `invalid`.

### `compare`

| a, b | Rule |
|---|---|
| Both unzoned civil values of the same form (`date`, `year-month`, `time`, `datetime`) | civil order of the fields; an absent `second` or `nano` counts as 0 |
| Both `instant` in the same timescale | numeric order |
| Both fixed-offset date-times | converted to instants exactly (no rules needed), then numeric order |
| Anything involving a named zone | `unsupported`, with a diagnostic saying to `resolve` first: ordering depends on zone rules |
| Different kinds or forms | `unsupported` |

### `daysBetween`, `posixSecondsBetween`

`daysBetween` takes two Gregorian dates and returns b − a in days. `posixSecondsBetween` takes two POSIX instants and returns b − a as a `posixSeconds` amount, including fractions.

### `contains`, `overlaps`

Both work on civil-date intervals with date points, and on instant intervals with instant points. Mixing domains gives `unsupported`.

- **Three-valued logic.** Each endpoint condition is true, false or unknown:
  - `included`: ≤
  - `excluded`: <
  - `unbounded`: always true
  - `unknown`: unknown
- **Combining.** Conditions combine with three-valued AND. The result is `impossible` if any condition is false, `definitely` if all are true, and `possibly` otherwise.
- **`overlaps`.** A and B overlap when A's start is before B's end and B's start is before A's end.
  - **Dates:** dates are discrete, so first normalize both intervals to half-open: an included end *d* becomes an excluded *d + 1*, and an excluded start *d* becomes an included *d + 1*.
  - **Instants:** for each start/end pair, the condition is ≤ if both endpoints are included and < otherwise.
  - **Empty intervals:** an interval that is provably empty never overlaps anything.

### `resolve`

Turns a local date-time into instant(s). The rules are a parsed rule bundle in the format of [`conformance/rules/demo-zones.json`](../conformance/rules/demo-zones.json). The policy is `{"gap": …, "overlap": …}`. If it isn't passed, the value's own `resolutionPolicy` is used, and failing that `{"gap":"reject","overlap":"reject"}`.

Checks, in this order (the first that applies decides):

| # | Input | Outcome |
|---|---|---|
| 1 | form other than `datetime`, or `second` = 60 | `unsupported` |
| 2 | unzoned date-time | **`symbolic`**: no zone is bound, so there's nothing to resolve yet |
| 3 | fixed-offset date-time | `unique` (no rules needed) |
| 4 | named zone not in the bundle | `unsupported` |
| 5 | *L* − 86400 < `validFrom` or *L* + 86400 ≥ `validUntil` (with *L* as defined below) | `unsupported` (never extrapolate) |

Algorithm:

1. Let *L* be the wall time read as if it were UTC, in seconds; this keeps exact fractions from `nano`.
2. For each offset *o* in force in the zone within a day of *L*, *t = L − o* is a candidate when the zone's offset at *t* is exactly *o*.
3. Then apply the policy:

| Candidates | Policy | Status |
|---|---|---|
| 1 | any | `unique` |
| 0 (gap) | `reject` or `preserve-unresolved` | **`none`**, with diagnostic `gap` |
| 0 (gap) | `shift-forward-by-gap` | `unique`: *t = L − (offset before the gap)*, so 02:30 in a 1-hour spring-forward gap gives the instant labeled 03:30 |
| 2 (overlap) | `reject` or `all` | **`multiple`**, both instants ascending (`reject` adds a diagnostic `ambiguous`) |
| 2 (overlap) | `earliest` / `latest` | `unique` |

Any other policy value gives `unsupported`.

### `toZone`, `toOffset`

These convert an instant to a `datetime`, either in a named zone from the rules or at a fixed offset.

The output always includes `second`. It includes `nano` only when it's non-zero. It adds `inZone` (with `"offsetAssertion"` set to the offset in force) or `withOffset`.

If the zone isn't in the rules, or the instant is outside the bundle's validity window (`validFrom` ≤ t < `validUntil`), the result is `unsupported`.

## Clarifications

These were settled after the first implementation (TypeScript) found the contract silent. The fixtures cover each one.

1. **`add` and `offsetAssertion`.** `add` drops `offsetAssertion` from a zoned input, because the asserted offset can become false; for example, +01:00 in Paris stops being true after the March change. Emit an `info` diagnostic `offset-assertion-dropped`. The zone and `zoneIntent` are kept.
2. **`resolve` and `offsetAssertion`.** When the value has an `offsetAssertion`, `resolve` keeps only the candidate with that offset. If none matches, the status is **`invalid`**, with diagnostic `offset-mismatch`, because the value contradicts the rules (spec §4). This happens before the gap and overlap policy applies, so an assertion can pick one side of an overlap. A wall time in a gap has no candidates, so any asserted offset there is `invalid`, even with `shift-forward-by-gap`.
3. **`parseIso` annotations.** Anything other than a single `[Area/Location]` annotation, such as `[u-ca=…]` or `[+01:00]`, is `unsupported`.
4. **Dates with zones in other operations.** `daysBetween`, `contains` and `overlaps` accept civil dates only when every one of them is unzoned, or all have the same zone. Anything else is `unsupported`.
5. **The `operation` field in results.** It carries the everyday operation's name, such as `parseIso`. This extends the list in RESULT-CONTRACT; it is tracked under OI-17.

## Native interop (per language)

Each library also offers explicit adapters between UTV values and the language's own types. They are tested within each library, not through the shared fixtures. Rules:

- **The caller names the intent.** For example, a `DateTimeOffset` can become an *instant* or a *fixed-offset date-time*. Nothing is chosen silently.
- **No local zone.** A native value that depends on the host's local zone (C# `DateTimeKind.Local`, a naive Python `datetime` used "as local") is refused with `unsupported`. It is never interpreted using the machine's zone.
- **Lossy conversions back to native say so.** Converting a UTV value to a native type that can't hold it gives `unsupported` or reports loss. Examples: years outside the native range, sub-native precision, and a zoned intention converted to a type with only an offset.
- **"Now" is an explicit input.** The library never reads the clock. Examples read the platform clock themselves and pass the reading in, so the dependency is visible.

## Function names

| Neutral | Rust / Python | TypeScript / Java | C# |
|---|---|---|---|
| `parseIso` | `parse_iso` | `parseIso` | `ParseIso` |
| `formatIso` | `format_iso` | `formatIso` | `FormatIso` |
| `daysBetween` | `days_between` | `daysBetween` | `DaysBetween` |
| `posixSecondsBetween` | `posix_seconds_between` | `posixSecondsBetween` | `PosixSecondsBetween` |
| `toZone` | `to_zone` | `toZone` | `ToZone` |
| (others) | snake_case | camelCase | PascalCase |

Where the functions live (module, static class, namespace) is up to each library, following its language's conventions.
