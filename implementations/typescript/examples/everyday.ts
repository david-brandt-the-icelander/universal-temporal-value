// Everyday date and time tasks, done twice: with JavaScript's Date and Intl, then with UTV.
// Run it with `npm run everyday` from implementations/typescript/. Its output is everyday.expected.txt.
//
// Each print line shows its output as a trailing comment. Rows ending in ‡ depend on this host
// (its clock, time zone or tz database), so the snapshot test skips them. Temporal would fix many
// of the Date quirks below, but Node 22 doesn't ship it.
import {
  CivilDate, CivilDateTime, CivilTime, FixedOffsetDateTime, Instant, Interval, Bound, PosixSeconds, ElapsedSeconds,
  TemporalContext, ZoneRules, MonthEnd, Gap, Overlap, Weekday, assertNever, type Outcome,
} from "../src/index.js";

// The zone rules are an explicit input, loaded once. UTV never reads the host's tz database.
const ctx = TemporalContext.with(ZoneRules.load("../../conformance/rules/demo-zones.json"));
const NY = "America/New_York";

console.log("Everyday date and time tasks: Date and Intl, then UTV");

console.log("\n1. Now");

// BEGIN now
console.log("   Now");
// The program reads the clock. UTV never does: the reading is passed in.
const reading = Date.now();
row("Date", new Date(reading).toISOString(), "this host's clock ‡"); // 2026-11-05T13:30:00.123Z
row("UTV", Instant.fromUnixMilliseconds(reading), "the same reading ‡"); // 2026-11-05T13:30:00.123Z
// END now

console.log("\n2. A date");

// BEGIN a-date
console.log("   5 November 2026");
// Months count from 0, and the result is midnight in the host's zone, as an instant.
const jsNov5 = new Date(2026, 10, 5);
row("Date", jsNov5.toISOString(), "local midnight, in UTC ‡"); // 2026-11-05T00:00:00.000Z
row("Date", new Date("2026-11-05").toISOString(), "date-only text is UTC"); // 2026-11-05T00:00:00.000Z

const nov5 = CivilDate.of(2026, 11, 5);
row("UTV", nov5, "a date, not an instant"); // 2026-11-05
// END a-date

// BEGIN weekday-week
console.log("   Day of week and ISO week");
row("Date", jsNov5.getDay(), "Sunday is 0; no ISO week"); // 4

const weekday = Weekday[nov5.dayOfWeek];
row("UTV", `${weekday}, ISO week ${nov5.isoWeek}`, `day ${nov5.dayOfYear}`); // Thursday, ISO week 45  day 309
// END weekday-week

console.log("\n3. Invalid dates");

// BEGIN feb-30
console.log("   30 February 2026");
row("Date", new Date(2026, 1, 30).toDateString(), "rolled over silently"); // Mon Mar 02 2026
row("Date", new Date("2026-02-30").toISOString(), "parsed, rolled over"); // 2026-03-02T00:00:00.000Z

try {
  CivilDate.of(2026, 2, 30);
} catch (e) {
  row("UTV", String(e)); // RangeError: 2026-02 has no day 30 (day-out-of-range)
}
row("UTV", CivilDate.parse("2026-02-30"), "parsing returns an Outcome"); // invalid (day-out-of-range)
// END feb-30

console.log("\n4. Adding");

// BEGIN add-days
console.log("   5 Nov + 30 days");
const jsLater = new Date(2026, 10, 5);
jsLater.setDate(jsLater.getDate() + 30);
row("Date", jsLater.toDateString()); // Sat Dec 05 2026

row("UTV", nov5.addDays(30), "can't fail: a plain CivilDate"); // 2026-12-05
// END add-days

// BEGIN month-end
console.log("   31 Jan 2026 + 1 month");
const jsJan31 = new Date(2026, 0, 31);
jsJan31.setMonth(jsJan31.getMonth() + 1);
row("Date", jsJan31.toDateString(), "rolled over silently"); // Tue Mar 03 2026

const jan31 = CivilDate.of(2026, 1, 31);
const nextMonth: Outcome<CivilDate> = jan31.addMonths(1);
switch (nextMonth.status) {
  case "unique":
    row("UTV", nextMonth.value);
    break;
  case "none": { // ← this one runs
    const [why] = nextMonth.diagnostics;
    row("UTV", "no such date", `${why?.message} (${why?.code})`); // 2026-02 has no day 31 (month-end)
    break;
  }
  case "multiple":
  case "unsupported":
  case "invalid":
    row("UTV", nextMonth);
    break;
  default:
    assertNever(nextMonth);
}
row("UTV", jan31.addMonths(1, MonthEnd.Constrain), "MonthEnd.Constrain clamps"); // 2026-02-28
// END month-end

console.log("\n5. Days between");

// BEGIN days-between
console.log("   5 Nov → 25 Dec");
const jsDec25 = new Date(2026, 11, 25);
const msPerDay = 24 * 60 * 60 * 1000;
row("Date", (jsDec25.getTime() - jsNov5.getTime()) / msPerDay, "milliseconds ÷ msPerDay"); // 50

const dec25 = CivilDate.of(2026, 12, 25);
row("UTV", nov5.daysUntil(dec25), "whole days"); // 50
// END days-between

// BEGIN compare-dates
console.log("   Which comes first");
row("Date", jsNov5 < jsDec25, "< compares time values"); // true
row("Date", jsNov5 === new Date(2026, 10, 5), "=== compares objects"); // false

row("UTV", nov5.isBefore(dec25), "isBefore"); // true
row("UTV", nov5.equals(CivilDate.of(2026, 11, 5)), "equals compares values"); // true
row("UTV", nov5.compare(dec25), "compare gives -1, 0 or 1"); // -1
// END compare-dates

console.log("\n6. Times and date-times");

// BEGIN time-of-day
console.log("   09:30");
// Date has no time of day, so pick a date. The time is read in the host's zone.
const jsNineThirty = new Date(1970, 0, 1, 9, 30);
row("Date", jsNineThirty.toISOString(), "a made-up date, in UTC ‡"); // 1970-01-01T09:30:00.000Z

row("UTV", CivilTime.of(9, 30), "a time of day"); // 09:30
row("UTV", CivilTime.of(9, 30, 0), "seconds you give are kept"); // 09:30:00
// END time-of-day

// BEGIN datetime-plus-hours
console.log("   23:30 + 2 hours");
const jsLate = new Date(2026, 10, 5, 23, 30);
jsLate.setHours(jsLate.getHours() + 2);
row("Date", jsLate.toLocaleString("en-GB"), "in the host's zone"); // 06/11/2026, 01:30:00

row("UTV", CivilDateTime.of(2026, 11, 5, 23, 30).addHours(2), "no zone, no DST"); // 2026-11-06T01:30
// END datetime-plus-hours

console.log("\n7. Parsing with offsets");

// BEGIN parse-offsets
console.log("   Z vs +00:00");
const jsZ = new Date("2026-11-05T14:30:00Z");
const jsZero = new Date("2026-11-05T14:30:00+00:00");
row("Date", jsZero.toISOString(), "the +00:00 is gone"); // 2026-11-05T14:30:00.000Z
row("Date", jsZ.getTime() === jsZero.getTime(), "the same Date"); // true

const z = Instant.parse("2026-11-05T14:30:00Z").single();
const zero = FixedOffsetDateTime.parse("2026-11-05T14:30:00+00:00").single();
row("UTV", z, "an Instant: offset unknown"); // 2026-11-05T14:30:00Z
row("UTV", zero, "a wall clock at +00:00"); // 2026-11-05T14:30:00+00:00
row("UTV", zero.toInstant().equals(z), "same moment, different facts"); // true
row("UTV", Instant.parse("2026-11-05T14:30:00+00:00"), "+00:00 is not Z"); // invalid (kind-mismatch)
// END parse-offsets

console.log("\n8. Time zones");

// BEGIN meeting-in-paris
console.log("   09:00 in Paris, seen from New York");
// zonedToDate is what people write by hand (at the end of this file). It asks the host's tz database.
const jsMeeting = zonedToDate(2027, 6, 1, 9, 0, "Europe/Paris");
row("Intl", jsMeeting.toLocaleString("en-GB", { timeZone: NY }), "in New York ‡"); // 01/06/2027, 03:00:00

const meeting = CivilDateTime.of(2027, 6, 1, 9, 0).inZone("Europe/Paris");
// single() gives the one answer, or throws an OutcomeError. It never picks silently.
const meetingAt = meeting.resolve(ctx).single();
row("UTV", meeting); // 2027-06-01T09:00[Europe/Paris]
row("UTV", meetingAt); // 2027-06-01T07:00:00Z
row("UTV", meetingAt.inZone(NY, ctx).single()); // 2027-06-01T03:00:00-04:00[America/New_York]
// END meeting-in-paris

console.log("\n9. DST gap");

// BEGIN dst-gap
console.log("   02:30 on 8 March 2026 in New York");
const jsGap = zonedToDate(2026, 3, 8, 2, 30, NY);
row("Intl", jsGap.toLocaleString("en-GB", { timeZone: NY }), "no error ‡"); // 08/03/2026, 01:30:00

const gapTime = CivilDateTime.of(2026, 3, 8, 2, 30).inZone(NY);
const gapAt: Outcome<Instant> = gapTime.resolve(ctx);
switch (gapAt.status) {
  case "unique":
    row("UTV", gapAt.value);
    break;
  case "multiple":
    for (const t of gapAt.values) row("UTV", t);
    break;
  case "none": { // ← this one runs: clocks jump from 02:00 to 03:00
    const [why] = gapAt.diagnostics;
    row("UTV", "no such time", `${why?.message} (${why?.code})`); // America/New_York skips this wall time (gap)
    break;
  }
  case "unsupported":
  case "invalid":
    row("UTV", gapAt);
    break;
  default:
    assertNever(gapAt);
}
const shifted = gapTime.resolve(ctx, { gap: Gap.ShiftForward }).single();
row("UTV", shifted.inZone(NY, ctx).single(), "Gap.ShiftForward"); // 2026-03-08T03:30:00-04:00[America/New_York]
// END dst-gap

console.log("\n10. DST overlap");

// BEGIN dst-overlap
console.log("   01:30 on 1 November 2026 in New York");
const jsTwice = zonedToDate(2026, 11, 1, 1, 30, NY);
const jsTwiceInNY = jsTwice.toLocaleString("en-GB", { timeZone: NY, timeZoneName: "short" });
row("Intl", jsTwiceInNY, "one of two, silently ‡"); // 01/11/2026, 1:30:00 GMT-4

const twice = CivilDateTime.of(2026, 11, 1, 1, 30).inZone(NY);
const twiceAt: Outcome<Instant> = twice.resolve(ctx);
switch (twiceAt.status) {
  case "unique":
    row("UTV", twiceAt.value);
    break;
  case "multiple": // ← this one runs: 01:30 EDT, then 01:30 EST
    for (const t of twiceAt.values) row("UTV", t.inZone(NY, ctx).single(), twiceAt.diagnostics[0]?.code);
    // 2026-11-01T01:30:00-04:00[America/New_York] ambiguous
    // 2026-11-01T01:30:00-05:00[America/New_York] ambiguous
    break;
  case "none":
  case "unsupported":
  case "invalid":
    row("UTV", twiceAt);
    break;
  default:
    assertNever(twiceAt);
}
const earliest = twice.resolve(ctx, { overlap: Overlap.Earliest }).single();
row("UTV", earliest, "Overlap.Earliest"); // 2026-11-01T05:30:00Z
// END dst-overlap

console.log("\n11. Timestamps");

// BEGIN timestamps
console.log("   Exact fractions and differences");
row("Date", new Date(1793885400.123456 * 1000).toISOString(), "milliseconds only"); // 2026-11-05T13:30:00.123Z
row("JS", 1793885400.3 - 1793885400.1, "float seconds"); // 0.20000004768371582

const t1 = Instant.fromUnixSeconds(1793885400, 100_000_000);
const t2 = Instant.fromUnixSeconds(1793885400, 300_000_000);
row("UTV", Instant.fromUnixSeconds(1793885400, 123_456_000), "microseconds kept"); // 2026-11-05T13:30:00.123456Z
row("UTV", t1.until(t2), "exact"); // 0.2 s (POSIX)
// END timestamps

console.log("\n12. Durations vs periods");

// BEGIN day-vs-24h
console.log("   One day vs 86,400 seconds across DST");
const jsNoon = zonedToDate(2026, 3, 7, 12, 0, NY);
const jsNextNoon = new Date(jsNoon.getTime() + 86_400_000);
row("Intl", jsNextNoon.toLocaleString("en-GB", { timeZone: NY }), "+ 86,400,000 ms ‡"); // 08/03/2026, 13:00:00

const noon = CivilDateTime.of(2026, 3, 7, 12, 0).inZone(NY);
const noonAt = noon.resolve(ctx).single();
const dayLater = noonAt.add(PosixSeconds.of(86_400));
row("UTV", noon.addDays(1), "+ 1 day"); // 2026-03-08T12:00[America/New_York]
row("UTV", dayLater.inZone(NY, ctx).single(), "+ 86,400 s"); // 2026-03-08T13:00:00-04:00[America/New_York]
row("UTV", noonAt.add(ElapsedSeconds.of(86_400)), "SI seconds"); // unsupported (needs-leap-seconds)
// END day-vs-24h

console.log("\n13. Ranges");

// BEGIN ranges
console.log("   November, open-ended and unknown ends");
const jsNov15 = new Date(2026, 10, 15);
row("Date", jsNov15 >= new Date(2026, 10, 1) && jsNov15 < new Date(2026, 11, 1), "by hand"); // true

const nov1 = CivilDate.of(2026, 11, 1);
const november = Interval.halfOpen(nov1, CivilDate.of(2026, 12, 1));
row("UTV", november); // [2026-11-01, 2026-12-01)
row("UTV", november.contains(CivilDate.of(2026, 11, 15)), "15 November"); // definitely
row("UTV", november.contains(CivilDate.of(2026, 12, 1)), "1 December"); // impossible

const fromNov = Interval.of(Bound.included(nov1), Bound.unbounded);
row("UTV", fromNov, "no end"); // [2026-11-01, ..)
row("UTV", fromNov.contains(CivilDate.of(3000, 1, 1)), "3000-01-01"); // definitely

const endUnknown = Interval.of(Bound.included(nov1), Bound.unknown);
row("UTV", endUnknown, "an end nobody knows yet"); // [2026-11-01, ?)
row("UTV", endUnknown.contains(CivilDate.of(2027, 6, 1)), "2027-06-01"); // possibly
// END ranges

console.log("\n14. Round trip");

// BEGIN round-trip
console.log("   Native → UTV → native");
const original = new Date(Date.UTC(2026, 10, 5, 13, 30, 0, 250));
const asInstant = Instant.fromDate(original);
const back = asInstant.toDate().single();
row("Date", original.toISOString()); // 2026-11-05T13:30:00.250Z
row("UTV", asInstant, "Instant.fromDate"); // 2026-11-05T13:30:00.25Z
row("Date", back.toISOString(), `same time value: ${back.getTime() === original.getTime()}`); // … true

const precise = Instant.fromUnixNanoseconds(1_793_885_400_123_456_789n);
const lossy: Outcome<Date> = precise.toDate();
const [loss] = lossy.diagnostics;
row("UTV", precise); // 2026-11-05T13:30:00.123456789Z
row("Date", lossy.single().toISOString(), `${loss?.severity}: ${loss?.code}`); // …123Z  warning: precision-loss
// END round-trip

// ---------------------------------------------------------------------------------------------------
// Printing: one aligned row per result. Rows whose note ends in ‡ depend on this host.

function row(who: string, value: unknown, note = ""): void {
  console.log(`     ${who.padEnd(5)} ${String(value).padEnd(32)} ${note}`.trimEnd());
}

// ---------------------------------------------------------------------------------------------------
// What people write by hand: a Date can't be built "at 09:00 in Paris", so guess the instant, ask Intl
// (the host's tz database) for the zone's offset there, and correct once.

function zonedToDate(y: number, month: number, d: number, h: number, min: number, zone: string): Date {
  const guess = Date.UTC(y, month - 1, d, h, min);
  return new Date(guess - offsetMs(zone, guess - offsetMs(zone, guess)));
}

function offsetMs(zone: string, at: number): number {
  const name = new Intl.DateTimeFormat("en-US", { timeZone: zone, timeZoneName: "longOffset" })
    .formatToParts(at).find((p) => p.type === "timeZoneName")?.value ?? "GMT";
  const m = /GMT([+-])(\d{2}):(\d{2})/.exec(name);
  return m === null ? 0 : (m[1] === "-" ? -1 : 1) * (Number(m[2]) * 60 + Number(m[3])) * 60_000;
}
