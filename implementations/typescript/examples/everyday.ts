// Everyday date and time tasks, side by side: what you write today with JavaScript's `Date` and
// `Intl.DateTimeFormat`, and the same task with UTV.
// Run with `npm run everyday` from implementations/typescript/.
//
// `Temporal` (Temporal.PlainDate, Temporal.ZonedDateTime, …) would fix most of the native quirks shown
// here, but it is not available by default in Node 22, so "native" below means `Date` + `Intl`.
// Native lines print whatever this host gives (its clock, its ICU time zone data); only the UTV lines
// are checked, because they are deterministic.
import { readFileSync } from "node:fs";
import { pathToFileURL } from "node:url";
import {
  add, candidate, compare, contains, date, dateTime, daysBetween, fields, formatIso, fromDate, fromEpochMilliseconds,
  fromPlainDate, inZone, instant, interval, loadRules, overlaps, parseIso, period, posixSeconds, posixSecondsBetween,
  resolve, time, toDate, toZone, type Json, type UtvResult,
} from "../src/index.js";

// Compiled to dist/examples/; the repository root is four levels up.
export const RULES_URL = new URL("../../../../conformance/rules/demo-zones.json", import.meta.url);
const DAY_MS = 86_400_000;
const NY = "America/New_York", PARIS = "Europe/Paris";

// --- printing helpers --------------------------------------------------------------------------------

type Log = (line?: string) => void;

/** One line for a UTV result: the status, plus the value as ISO text where it has one. */
export function describe(r: UtvResult): string {
  const show = (c: Json | undefined): string => {
    if (typeof c !== "object" || c === null || Array.isArray(c)) return String(c);
    const text = formatIso(c);
    if (text.status === "unique") return String(text.candidates[0]);
    const v = c["value"] as { [k: string]: Json };
    if (c["kind"] === "amount" && v["basis"] === "clock-ticks") {
      const s = v["seconds"] as { numerator: string; denominator: string };
      return `${s.numerator}${s.denominator === "1" ? "" : `/${s.denominator}`} POSIX seconds`;
    }
    return JSON.stringify(c);
  };
  if (r.status === "unique") return `unique ${show(r.candidates[0])}`;
  if (r.status === "multiple") return `multiple [${r.candidates.map(show).join(", ")}]`;
  const code = r.diagnostics[0]?.code;
  return code === undefined ? r.status : `${r.status} (${code})`;
}

// --- native helpers: what people hand-write for time zones with Date + Intl ---------------------------

function zoneAvailable(zone: string): boolean {
  try { new Intl.DateTimeFormat("en-US", { timeZone: zone }); return true; } catch { return false; }
}

/** The zone's UTC offset at an instant, in ms, from the host's ICU time zone data. */
function offsetMs(zone: string, ms: number): number {
  const name = new Intl.DateTimeFormat("en-US", { timeZone: zone, timeZoneName: "longOffset" })
    .formatToParts(ms).find((p) => p.type === "timeZoneName")?.value ?? "GMT";
  const m = /GMT([+-])(\d{2}):(\d{2})/.exec(name);
  return m ? (m[1] === "-" ? -1 : 1) * (Number(m[2]) * 60 + Number(m[3])) * 60_000 : 0;
}

/** The usual hand-rolled "wall time in a zone → Date": guess, look up the offset, correct once. */
function zonedToDate(y: number, mo: number, d: number, h: number, mi: number, zone: string): Date {
  const guess = Date.UTC(y, mo - 1, d, h, mi);
  return new Date(guess - offsetMs(zone, guess - offsetMs(zone, guess)));
}

/** A Date's wall clock in a zone, as `YYYY-MM-DDThh:mm:ss±hh:mm`, via Intl. */
function wallClock(d: Date, zone: string): string {
  const parts = Object.fromEntries(new Intl.DateTimeFormat("en-US", {
    timeZone: zone, year: "numeric", month: "2-digit", day: "2-digit", hour: "2-digit", minute: "2-digit", second: "2-digit",
    hourCycle: "h23", timeZoneName: "longOffset",
  }).formatToParts(d).map((p) => [p.type, p.value]));
  const off = String(parts["timeZoneName"]).replace("GMT", "") || "+00:00";
  return `${parts["year"]}-${parts["month"]}-${parts["day"]}T${parts["hour"]}:${parts["minute"]}:${parts["second"]}${off}`;
}

const ymd = (d: Date) => (Number.isNaN(d.getTime()) ? "Invalid Date" : d.toISOString().slice(0, 10));

// --- the tour ----------------------------------------------------------------------------------------

/** Prints every section and returns the UTV lines that did not give the expected outcome. */
export function runEveryday(log: Log = console.log): string[] {
  const mismatches: string[] = [];
  const rules = loadRules(readFileSync(RULES_URL, "utf8")); // the demo rules, loaded explicitly

  const section = (title: string) => log(`\n== ${title} ==`);
  /** A native line. Never throws: a host without a time zone gets a note instead. */
  const nat = (label: string, f: () => unknown, zone?: string) => {
    if (zone !== undefined && !zoneAvailable(zone)) { log(`  Native  ${label}: (time zone ${zone} is not available on this host; skipped)`); return undefined; }
    try { const v = f(); log(`  Native  ${label}: ${String(v)}`); return v; } catch (e) { log(`  Native  ${label}: threw ${String(e)}`); return undefined; }
  };
  /** A UTV line, checked against the expected outcome. */
  const utv = (label: string, r: UtvResult, expected: string | RegExp) => {
    const got = describe(r);
    log(`  UTV     ${label}: ${got}`);
    if (typeof expected === "string" ? got !== expected : !expected.test(got)) mismatches.push(`${label}: expected ${String(expected)}, got ${got}`);
    return candidate(r)!;
  };
  const note = (text: string) => log(`  Note:   ${text}`);

  section("1. Now");
  nat("new Date().toISOString()", () => new Date().toISOString());
  // UTV never reads the clock. The example reads it, then converts that reading explicitly.
  const reading = Date.now();
  utv("fromEpochMilliseconds(Date.now())", fromEpochMilliseconds(reading), /^unique \d{4}-\d\d-\d\dT\d\d:\d\d:\d\d(\.\d+)?Z$/);
  note("both depend on this host's clock; with UTV the clock reading is a visible input, so the code below it is reproducible.");

  section("2. A date");
  const nov5 = new Date(Date.UTC(2026, 10, 5)); // month 10 is November: Date months are 0-based
  nat("new Date(Date.UTC(2026, 10, 5)).toISOString()", () => nov5.toISOString());
  nat('toLocaleDateString("en-GB", { timeZone: "UTC", dateStyle: "full" })', () => nov5.toLocaleDateString("en-GB", { timeZone: "UTC", dateStyle: "full" }));
  nat('new Date("2026-11-05").toISOString()', () => new Date("2026-11-05").toISOString());
  nat("getUTCDay()  (Sunday = 0)", () => nov5.getUTCDay());
  note('a Date is an instant, not a date. Date-only text parses as UTC midnight, but "2026-11-05T00:00" parses in the host zone. There is no ISO-week API.');
  const day = date(2026, 11, 5);
  utv("formatIso(date(2026, 11, 5))", formatIso(day), "unique 2026-11-05");
  utv('parseIso("2026-11-05")', parseIso("2026-11-05"), "unique 2026-11-05");
  utv("fields(day)", fields(day), 'unique {"dayOfWeek":4,"dayOfYear":309,"isoWeekYear":"2026","isoWeek":45,"daysInMonth":30,"isLeapYear":false}');

  section("3. Invalid dates");
  // new Date(y, m, d) builds local midnight in the host zone; read it back with the local getters.
  nat("new Date(2026, 1, 30)  (host-local fields)", () => { const d = new Date(2026, 1, 30); return `${d.getFullYear()}-${d.getMonth() + 1}-${d.getDate()}`; });
  nat("new Date(Date.UTC(2026, 1, 30))", () => ymd(new Date(Date.UTC(2026, 1, 30))));
  const parsed = nat('new Date("2026-02-30")', () => ymd(new Date("2026-02-30")));
  note(`the constructors roll 30 February over to 2 March without an error; parsing "2026-02-30" gave ${String(parsed)} on this engine.`);
  utv('parseIso("2026-02-30")', parseIso("2026-02-30"), "invalid (field-out-of-range)");
  utv("fields(date(2026, 2, 30))", fields(date(2026, 2, 30)), "invalid (field-out-of-range)");

  section("4. Adding");
  nat("new Date(nov5.getTime() + 30 * DAY_MS)", () => ymd(new Date(nov5.getTime() + 30 * DAY_MS)));
  nat("d.setUTCMonth(d.getUTCMonth() + 1)", () => { const d = new Date(nov5); d.setUTCMonth(d.getUTCMonth() + 1); return ymd(d); });
  const jan31 = nat("31 Jan 2026, setUTCMonth(+1)", () => { const d = new Date(Date.UTC(2026, 0, 31)); d.setUTCMonth(d.getUTCMonth() + 1); return ymd(d); });
  note(`Date rolled "31 February" over to ${String(jan31)} without saying so.`);
  utv("add(day, period({ days: 30 }))", add(day, period({ days: 30 })), "unique 2026-12-05");
  utv("add(day, period({ months: 1 }))", add(day, period({ months: 1 })), "unique 2026-12-05");
  utv("add(date(2026, 1, 31), period({ months: 1 }))", add(date(2026, 1, 31), period({ months: 1 })), "none (month-end)");
  utv('  same, { monthEnd: "constrain" }', add(date(2026, 1, 31), period({ months: 1 }), { monthEnd: "constrain" }), "unique 2026-02-28");
  note("UTV's default says the day does not exist (none); clamping is a policy you ask for.");

  section("5. Days between");
  nat("(Date.UTC(2026, 11, 25) - Date.UTC(2026, 10, 5)) / DAY_MS", () => (Date.UTC(2026, 11, 25) - Date.UTC(2026, 10, 5)) / DAY_MS);
  nat("nov5 < dec25", () => nov5 < new Date(Date.UTC(2026, 11, 25)));
  nat("nov5 === new Date(Date.UTC(2026, 10, 5))", () => nov5 === new Date(Date.UTC(2026, 10, 5)));
  note("=== compares object identity, so two equal Dates are not ===; compare getTime() instead.");
  const dec25 = date(2026, 12, 25);
  utv("daysBetween(day, dec25)", daysBetween(day, dec25), "unique 50");
  utv("compare(day, dec25)", compare(day, dec25), "unique before");
  utv("compare(day, date(2026, 11, 5))", compare(day, date(2026, 11, 5)), "unique equal");

  section("6. Times and date-times");
  nat("new Date(Date.UTC(1970, 0, 1, 9, 30)).toISOString().slice(11, 16)", () => new Date(Date.UTC(1970, 0, 1, 9, 30)).toISOString().slice(11, 16));
  nat("new Date(Date.UTC(2026, 10, 5, 23, 30) + 2 * 3_600_000)", () => new Date(Date.UTC(2026, 10, 5, 23, 30) + 2 * 3_600_000).toISOString());
  note("Date has no time-of-day or local date-time type: UTC stands in here, and the local constructor would follow the host's DST.");
  utv("formatIso(time(9, 30))", formatIso(time(9, 30)), "unique 09:30");
  utv("add(dateTime(2026, 11, 5, 23, 30), period({ hours: 2 }))", add(dateTime(2026, 11, 5, 23, 30), period({ hours: 2 })), "unique 2026-11-06T01:30");

  section("7. Parsing with offsets");
  for (const s of ["2026-11-05T14:30:00+01:00", "2026-11-05T14:30:00Z", "2026-11-05T14:30:00+00:00"]) nat(`new Date("${s}").toISOString()`, () => new Date(s).toISOString());
  note('Date keeps only the instant: "Z" and "+00:00" give identical Dates, and the +01:00 is gone.');
  utv('parseIso("…+01:00")', parseIso("2026-11-05T14:30:00+01:00"), "unique 2026-11-05T14:30:00+01:00");
  const z = utv('parseIso("…Z")', parseIso("2026-11-05T14:30:00Z"), "unique 2026-11-05T14:30:00Z");
  const zero = utv('parseIso("…+00:00")', parseIso("2026-11-05T14:30:00+00:00"), "unique 2026-11-05T14:30:00+00:00");
  utv("compare(z, zero)", compare(z, zero), "unsupported (incomparable)");
  utv("compare(z, resolve(zero, rules))", compare(z, candidate(resolve(zero, rules))!), "unique equal");
  note('"Z" is an instant (UTC known, local offset unknown); "+00:00" is a civil time at offset 0. Same moment, different facts.');

  section("8. Time zones");
  const meetingDate = nat("zonedToDate(2027, 6, 1, 9, 0, PARIS).toISOString()", () => zonedToDate(2027, 6, 1, 9, 0, PARIS).toISOString(), PARIS);
  if (meetingDate !== undefined) nat("wallClock(meeting, NY)", () => wallClock(new Date(String(meetingDate)), NY), NY);
  note("Date cannot be built in a zone: zonedToDate is hand-written offset lookup on Intl, using this host's tz database.");
  const meeting = inZone(dateTime(2027, 6, 1, 9, 0), PARIS);
  utv("formatIso(meeting)", formatIso(meeting), "unique 2027-06-01T09:00[Europe/Paris]");
  const at = utv("resolve(meeting, rules)", resolve(meeting, rules), "unique 2027-06-01T07:00:00Z");
  utv("toZone(at, NY, rules)", toZone(at, NY, rules), "unique 2027-06-01T03:00:00-04:00[America/New_York]");

  section("9. DST gap");
  const gapNative = nat("wallClock(zonedToDate(2026, 3, 8, 2, 30, NY), NY)", () => wallClock(zonedToDate(2026, 3, 8, 2, 30, NY), NY), NY);
  if (gapNative !== undefined) note(`02:30 does not exist in New York that day; with this host's tz data the native code returned ${String(gapNative)}, without an error.`);
  const gap = inZone(dateTime(2026, 3, 8, 2, 30), NY);
  utv("resolve(gap, rules)", resolve(gap, rules), "none (gap)");
  const shifted = utv('  same, { gap: "shift-forward-by-gap" }', resolve(gap, rules, { gap: "shift-forward-by-gap" }), "unique 2026-03-08T07:30:00Z");
  utv("toZone(shifted, NY, rules)", toZone(shifted, NY, rules), "unique 2026-03-08T03:30:00-04:00[America/New_York]");

  section("10. DST overlap");
  const overlapNative = nat("wallClock(zonedToDate(2026, 11, 1, 1, 30, NY), NY)", () => wallClock(zonedToDate(2026, 11, 1, 1, 30, NY), NY), NY);
  if (overlapNative !== undefined) note(`01:30 happens twice in New York that day; with this host's tz data the native code picked ${String(overlapNative)} and never mentioned the other.`);
  const twice = inZone(dateTime(2026, 11, 1, 1, 30), NY);
  utv("resolve(twice, rules)", resolve(twice, rules), "multiple [2026-11-01T05:30:00Z, 2026-11-01T06:30:00Z]");
  utv('  same, { overlap: "earliest" }', resolve(twice, rules, { overlap: "earliest" }), "unique 2026-11-01T05:30:00Z");

  section("11. Timestamps");
  nat("new Date(1793885400.123456 * 1000).toISOString()", () => new Date(1793885400.123456 * 1000).toISOString());
  nat("1793885400.3 - 1793885400.1", () => 1793885400.3 - 1793885400.1);
  note("epoch seconds as a float: Date keeps milliseconds only, and float subtraction is inexact.");
  utv("formatIso(instant(1793885400, 123_456_000))", formatIso(instant(1793885400, 123_456_000)), "unique 2026-11-05T13:30:00.123456Z");
  utv("posixSecondsBetween(instant(…, 100_000_000), instant(…, 300_000_000))", posixSecondsBetween(instant(1793885400, 100_000_000), instant(1793885400, 300_000_000)), "unique 1/5 POSIX seconds");

  section("12. Durations vs periods");
  const nextNoon = nat("noon NY 7 Mar + DAY_MS", () => wallClock(new Date(zonedToDate(2026, 3, 7, 12, 0, NY).getTime() + DAY_MS), NY), NY);
  if (nextNoon !== undefined) note(`a native "day" is 86,400,000 ms; across the DST change it landed on ${String(nextNoon).slice(11, 16)} with this host's tz data.`);
  const noon = inZone(dateTime(2026, 3, 7, 12, 0), NY);
  utv("add(noon, period({ days: 1 }))", add(noon, period({ days: 1 })), "unique 2026-03-08T12:00[America/New_York]");
  const noonAt = candidate(resolve(noon, rules))!;
  utv("toZone(add(noonAt, posixSeconds(86400)))", toZone(candidate(add(noonAt, posixSeconds(86400)))!, NY, rules), "unique 2026-03-08T13:00:00-04:00[America/New_York]");
  const si = { schema: "utv/1.0", kind: "amount", value: { basis: "elapsed", seconds: { numerator: "86400", denominator: "1" } } };
  utv("add(noonAt, 86400 SI seconds)", add(noonAt, si), "unsupported (needs-leap-seconds)");
  note("a period is a calendar intention; posixSeconds is exact clock time; SI seconds need a leap-second table.");

  section("13. Ranges");
  const novStart = Date.UTC(2026, 10, 1), decStart = Date.UTC(2026, 11, 1);
  nat("t >= novStart && t < decStart  (15 Nov)", () => { const t = Date.UTC(2026, 10, 15); return t >= novStart && t < decStart; });
  note("no range type: half-open checks are written by hand, and an unknown end has no representation.");
  const november = interval(date(2026, 11, 1), date(2026, 12, 1));
  const december = interval(date(2026, 12, 1), date(2027, 1, 1));
  utv("contains(november, date(2026, 11, 15))", contains(november, date(2026, 11, 15)), "unique definitely");
  utv("contains(november, date(2026, 12, 1))", contains(november, date(2026, 12, 1)), "unique impossible");
  utv("overlaps(november, december)", overlaps(november, december), "unique impossible");
  utv('contains(interval(nov1, "unbounded"), date(3000, 1, 1))', contains(interval(date(2026, 11, 1), "unbounded"), date(3000, 1, 1)), "unique definitely");
  utv('contains(interval(nov1, "unknown"), date(2027, 6, 1))', contains(interval(date(2026, 11, 1), "unknown"), date(2027, 6, 1)), "unique possibly");

  section("14. Round trip");
  const original = new Date(Date.UTC(2026, 10, 5, 13, 30, 0, 250));
  nat("original.toISOString()", () => original.toISOString());
  const asUtv = utv('fromDate(original, "instant")', fromDate(original, "instant"), "unique 2026-11-05T13:30:00.25Z");
  const back = toDate(asUtv);
  nat("toDate(asUtv).value.getTime() === original.getTime()", () => back.value?.getTime() === original.getTime());
  utv('fromDate(original, "local")', fromDate(original, "local"), "unsupported (host-zone-refused)");
  const fine = toDate(instant(1793885400, 123_456_789));
  log(`  UTV     toDate(instant with nanoseconds): ${fine.status}, ${fine.value?.toISOString()}, loss ${fine.loss.classification} [${fine.loss.fields.join(", ")}]`);
  if (fine.loss.classification !== "information-losing") mismatches.push("toDate did not report sub-millisecond loss");
  utv("fromPlainDate({ year: 2026, month: 2, day: 30 })", fromPlainDate({ year: 2026, month: 2, day: 30 }), "invalid (field-out-of-range)");
  note("each conversion names its intent; a host-local reading is refused and lost precision is reported.");

  log(mismatches.length === 0 ? "\nEvery UTV outcome as expected." : `\n${mismatches.length} UTV mismatches:\n  ${mismatches.join("\n  ")}`);
  return mismatches;
}

const isMain = process.argv[1] !== undefined && import.meta.url === pathToFileURL(process.argv[1]).href;
if (isMain && runEveryday().length > 0) process.exitCode = 1;
