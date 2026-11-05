import { test } from "node:test";
import assert from "node:assert/strict";
import {
  assertNever, Bound, CivilDate, CivilDateTime, CivilTime, CivilYearMonth, ElapsedSeconds, FixedOffsetDateTime, fromJson, Gap,
  Instant, Interval, Invalid, MonthEnd, Multiple, None, Ordering, OutcomeError, Overlap, parse, Period, PosixSeconds,
  ResolutionPolicy, TemporalContext, Truth, Unique, Weekday, ZonedIntent, ZoneRules, type Outcome, type UtvValue,
} from "../src/index.js";

// The typed layer (docs/library-api.md §1–§4). The shared fixtures run in everyday.test.ts and
// text-form.test.ts; here, at least one test per row of the §2 table, plus round trips and constructors.
const repo = new URL("../../../../", import.meta.url);
const ctx = TemporalContext.with(ZoneRules.load(new URL("conformance/rules/demo-zones.json", repo)));
const NY = "America/New_York";

const text = (o: Outcome<unknown>) => String(o);
const date = (y: number, m: number, d: number) => CivilDate.of(y, m, d);
const dt = (y: number, mo: number, d: number, h: number, mi: number) => CivilDateTime.of(y, mo, d, h, mi);
const codes = (o: Outcome<unknown>) => o.diagnostics.map((d) => d.code);

// --- Outcome ---------------------------------------------------------------------------------------

/** Exhaustive: removing a case makes `assertNever(o)` fail to type-check (see the next function). */
function describeOutcome(o: Outcome<CivilDate>): string {
  switch (o.status) {
    case "unique": return `one: ${o.value}`;
    case "multiple": return `${o.values.length} answers`;
    case "none": return "no answer";
    case "unsupported": return "can't tell";
    case "invalid": return "bad input";
    default: return assertNever(o);
  }
}
function missingCase(o: Outcome<CivilDate>): string {
  switch (o.status) {
    case "unique": return "one";
    case "multiple": return "many";
    case "none": return "none";
    case "unsupported": return "unsupported";
    // @ts-expect-error -- "invalid" is not handled, so `o` is not `never` here: this must not type-check.
    default: return assertNever(o);
  }
}

test("Outcome: exhaustive switch, helpers never pick silently", () => {
  assert.equal(describeOutcome(date(2026, 1, 31).addMonths(1)), "no answer");
  assert.equal(describeOutcome(date(2026, 1, 30).addMonths(2)), "one: 2026-03-30");
  assert.throws(() => missingCase(new Invalid([])), /unexpected case/);
  const none = date(2026, 1, 31).addMonths(1);
  assert.ok(none instanceof None);
  assert.equal(none.uniqueOr("fallback"), "fallback");
  assert.throws(() => none.single(), (e: unknown) => e instanceof OutcomeError && e.status === "none" && e.diagnostics[0]?.code === "month-end");
  assert.equal(String(none), "none (month-end)");
  assert.equal(none.toResult().status, "none");
  assert.equal(none.toResult().operation, "add");
  const one = date(2026, 1, 28).addMonths(1);
  assert.ok(one instanceof Unique);
  assert.equal(one.single().toString(), "2026-02-28");
  assert.equal(one.uniqueOr(undefined)?.day, 28);
  assert.equal(String(date(2026, 3, 30).addMonths(-1)), "none (month-end)");
  assert.equal(JSON.stringify(date(2026, 3, 1).addMonths(1).toResult().candidates[0]), JSON.stringify(date(2026, 4, 1)));
});

// --- §1 constructors --------------------------------------------------------------------------------

test("constructors reject values that can't exist", () => {
  assert.throws(() => CivilDate.of(2026, 2, 30), (e: unknown) => e instanceof RangeError && /day-out-of-range/.test(e.message));
  assert.throws(() => CivilDate.of(2026, 13, 1), /month-out-of-range/);
  assert.throws(() => CivilDate.of(2026.5, 1, 1), RangeError);
  assert.throws(() => CivilYearMonth.of(2026, 0), RangeError);
  assert.throws(() => CivilTime.of(25, 0), /field-out-of-range/);
  assert.throws(() => CivilTime.of(9, 60), RangeError);
  assert.throws(() => CivilDateTime.of(2026, 2, 29, 9, 0), RangeError);
  assert.throws(() => dt(2026, 11, 5, 9, 0).atOffset(19 * 3600), /offset-out-of-range/);
  assert.throws(() => dt(2026, 11, 5, 9, 0).inZone("not a zone"), RangeError);
  assert.throws(() => PosixSeconds.of(1, 0), RangeError);
  assert.throws(() => Period.of({ days: 1.5 }), RangeError);
  assert.throws(() => Interval.halfOpen(date(2026, 12, 1), date(2026, 11, 1)), /reversed-interval/);
  assert.throws(() => Instant.fromDate(new Date(Number.NaN)), RangeError);
  assert.equal(text(CivilDate.tryOf(2026, 2, 30)), "invalid (day-out-of-range)");
  assert.equal(text(CivilTime.tryOf(9, 30, 60)), "unsupported (leap-second)");
  assert.equal(text(CivilDate.tryOf(2024, 2, 29)), "2024-02-29");
  assert.equal(CivilDate.of(10n ** 30n, 1, 1).year, 10n ** 30n);
});

// --- §1 text, JSON, equality -----------------------------------------------------------------------

const samples: UtvValue[] = [
  date(2026, 11, 5), CivilDate.of(-500, 3, 1), CivilDate.of(275760, 9, 13), CivilYearMonth.of(2026, 11),
  CivilTime.of(9, 30), CivilTime.of(9, 30, 0), CivilTime.of(9, 30, 15, 250_000_000),
  dt(2026, 11, 5, 14, 30), dt(2026, 11, 5, 14, 30).atOffset(3600), dt(2027, 6, 1, 9, 0).inZone("Europe/Paris"),
  ZonedIntent.parse("2027-06-01T09:00+02:00[Europe/Paris]").single(),
  Instant.fromUnixSeconds(1793885400), Instant.fromUnixSeconds(0, 500_000_000), Instant.fromUnixNanoseconds(-1n),
  Period.months(1), Period.minutes(1), Period.of({ years: 1, months: 2, days: 10, hours: 2, minutes: 30 }), Period.of({}), Period.months(-1),
  Interval.halfOpen(date(2026, 11, 1), date(2026, 12, 1)), Interval.closed(date(2026, 11, 1), date(2026, 11, 30)),
  Interval.of(Bound.included(date(2026, 11, 1)), Bound.unbounded), Interval.of(Bound.unknown, Bound.excluded(date(2026, 12, 1))),
  Interval.halfOpen(Instant.fromUnixSeconds(1793885400), Instant.fromUnixSeconds(1793892600)),
];

test("toString round-trips through parse", () => {
  for (const v of samples) {
    const back = parse(String(v)).single();
    assert.ok(back.equals(v), `${String(v)} -> ${String(back)}`);
    assert.equal(back.constructor, v.constructor);
  }
});

test("fromJson(toJSON(v)) equals v, and JSON.stringify uses toJSON", () => {
  for (const v of [...samples, PosixSeconds.of(14403, 4), ElapsedSeconds.of(86400), Period.of({ months: 1, days: -1 })]) {
    assert.ok(fromJson(v.toJSON()).single().equals(v), String(v));
    assert.ok(fromJson(JSON.stringify(v)).single().equals(v), String(v));
  }
  assert.ok(CivilDate.fromJson(JSON.stringify(date(2026, 11, 5))).single().equals(date(2026, 11, 5)));
});

test("fromJson and parse: wrong kinds, bad text and bad JSON are outcomes", () => {
  assert.deepEqual(codes(CivilDate.fromJson(Instant.fromUnixSeconds(0).toJSON())), ["kind-mismatch"]);
  assert.equal(CivilDate.fromJson("{not json").status, "invalid");
  assert.equal(Instant.parse("2026-11-05T14:30:00+00:00").status, "invalid");
  assert.equal(text(parse("next Tuesday")), "invalid (syntax)");
  assert.equal(text(CivilDate.parse("2026-02-30")), "invalid (day-out-of-range)");
  assert.equal(text(parse("1347~")), "unsupported (unsupported-form)");
});

test("value semantics: equals, immutability, display", () => {
  assert.ok(date(2026, 11, 5).equals(date(2026, 11, 5)));
  assert.ok(!date(2026, 11, 5).equals(date(2026, 11, 6)));
  assert.ok(!CivilTime.of(9, 30).equals(CivilTime.of(9, 30, 0)), "09:30 and 09:30:00 are different texts");
  assert.equal(CivilTime.of(9, 30).compare(CivilTime.of(9, 30, 0)), 0, "a missing second compares as 0");
  assert.ok(Object.isFrozen(date(2026, 11, 5).toJSON()));
  const inspect = Symbol.for("nodejs.util.inspect.custom");
  assert.equal((date(2026, 11, 5) as unknown as Record<symbol, () => string>)[inspect]!(), "CivilDate(2026-11-05)");
  assert.equal(String(PosixSeconds.of(14403, 4)), "3600.75 s (POSIX)");
  assert.equal(String(PosixSeconds.of(1, 3)), "1/3 s (POSIX)");
  assert.equal(String(PosixSeconds.of(-1, 8)), "-0.125 s (POSIX)");
  assert.equal(String(ElapsedSeconds.of(86400)), "86400 s (SI)");
  assert.equal(String(Period.of({ months: 1, days: -1 })), `{"schema":"utv/1.0","kind":"amount","value":{"basis":"calendar","components":{"months":"1","days":"-1"}}}`);
});

// --- §2 CivilDate ------------------------------------------------------------------------------------

test("CivilDate: total operations", () => {
  const d = date(2026, 11, 5);
  assert.equal(String(d.addDays(30)), "2026-12-05");
  assert.equal(String(d.addDays(-400)), "2025-10-01");
  assert.equal(String(d.addWeeks(2)), "2026-11-19");
  assert.equal(String(date(2026, 1, 31).addMonths(1, MonthEnd.Constrain)), "2026-02-28");
  assert.equal(String(date(2024, 2, 29).addYears(1, MonthEnd.Constrain)), "2025-02-28");
  assert.equal([d.year, d.month, d.day].join(), "2026,11,5");
  assert.equal(d.dayOfWeek, Weekday.Thursday);
  assert.equal(Weekday[d.dayOfWeek], "Thursday");
  assert.equal(d.dayOfYear, 309);
  assert.equal(d.isoWeek, 45);
  assert.equal(d.isoWeekYear, 2026n);
  assert.equal(date(2026, 12, 31).isoWeek, 53);
  assert.equal(date(2027, 1, 1).isoWeekYear, 2026n);
  assert.equal(d.daysInMonth, 30);
  assert.equal(d.isLeapYear, false);
  assert.equal(d.daysUntil(date(2026, 12, 25)), 50);
  assert.equal(date(2026, 12, 25).daysUntil(d), -50);
  assert.equal(String(d.at(CivilTime.of(9, 30))), "2026-11-05T09:30");
  assert.equal(String(d.at(CivilTime.of(9, 30, 15, 5))), "2026-11-05T09:30:15.000000005");
  assert.equal(d.compare(date(2026, 12, 25)), -1);
  assert.ok(d.isBefore(date(2026, 12, 25)) && date(2026, 12, 25).isAfter(d) && d.compare(date(2026, 11, 5)) === 0);
});

test("CivilDate: partial operations", () => {
  const jan31 = date(2026, 1, 31);
  assert.deepEqual(codes(jan31.addMonths(1)), ["month-end"]);
  assert.equal(text(jan31.addMonths(2)), "2026-03-31");
  assert.equal(text(date(2024, 2, 29).addYears(1)), "none (month-end)");
  assert.equal(text(date(2024, 2, 29).addYears(4)), "2028-02-29");
  assert.equal(text(jan31.add(Period.of({ months: 1, days: 1 }))), "none (month-end)");
  assert.equal(text(jan31.add(Period.of({ months: 1, days: 1 }), MonthEnd.Constrain)), "2026-03-01");
  assert.equal(text(jan31.add(Period.hours(1))), "invalid (kind-mismatch)");
});

// --- §2 the other civil types -------------------------------------------------------------------------

test("CivilYearMonth: total operations and comparison", () => {
  const ym = CivilYearMonth.of(2026, 11);
  assert.equal(String(ym.addMonths(3)), "2027-02");
  assert.equal(String(ym.addYears(-1)), "2025-11");
  assert.ok(ym.isBefore(CivilYearMonth.of(2026, 12)));
  assert.equal([ym.year, ym.month].join(), "2026,11");
});

test("CivilTime: comparison only", () => {
  const t = CivilTime.of(9, 30, 15, 250_000_000);
  assert.equal([t.hour, t.minute, t.second, t.nanosecond].join(), "9,30,15,250000000");
  assert.ok(CivilTime.of(9, 30).isBefore(CivilTime.of(17, 0)));
  assert.equal((t as unknown as { addHours?: unknown }).addHours, undefined, "no arithmetic: it would lose the day");
});

test("CivilDateTime: wall-clock arithmetic, parts, zones", () => {
  const late = dt(2026, 11, 5, 23, 30);
  assert.equal(String(late.addHours(2)), "2026-11-06T01:30");
  assert.equal(String(late.addMinutes(-90)), "2026-11-05T22:00");
  assert.equal(String(late.addSeconds(61)), "2026-11-05T23:31:01");
  assert.equal(String(late.addDays(1)), "2026-11-06T23:30");
  assert.equal(String(late.addWeeks(1)), "2026-11-12T23:30");
  assert.equal(String(dt(2026, 1, 31, 9, 0).addMonths(1, MonthEnd.Constrain)), "2026-02-28T09:00");
  assert.equal(text(dt(2026, 1, 31, 9, 0).addMonths(1)), "none (month-end)");
  assert.equal(text(late.add(Period.of({ days: 1, hours: 1 }))), "2026-11-07T00:30");
  assert.equal(String(late.date), "2026-11-05");
  assert.equal(String(late.time), "23:30");
  assert.equal(String(late.inZone(NY)), "2026-11-05T23:30[America/New_York]");
  assert.equal(String(late.atOffset(-5 * 3600)), "2026-11-05T23:30-05:00");
  assert.ok(late.isAfter(dt(2026, 11, 5, 9, 0)));
});

test("FixedOffsetDateTime: instant, parts, comparison by instant", () => {
  const paris = dt(2026, 11, 5, 14, 30).atOffset(3600);
  assert.equal(String(paris.toInstant()), "2026-11-05T13:30:00Z");
  assert.equal(String(paris.dateTime), "2026-11-05T14:30");
  assert.equal(paris.offsetSeconds, 3600);
  const london = dt(2026, 11, 5, 13, 30).atOffset(0);
  assert.equal(paris.compare(london), 0);
  assert.ok(!paris.equals(london), "same instant, different values");
  assert.ok(FixedOffsetDateTime.parse("2026-11-05T14:31+01:00").single().isAfter(paris));
});

test("ZonedIntent: wall-clock arithmetic, resolve, no comparison", () => {
  const asserted = ZonedIntent.parse("2026-03-28T09:00+01:00[Europe/Paris]").single();
  assert.equal(asserted.assertedOffsetSeconds, 3600);
  const next = asserted.addDays(1);
  assert.equal(String(next), "2026-03-29T09:00[Europe/Paris]");
  assert.equal(next.assertedOffsetSeconds, undefined, "the old offset may no longer hold");
  assert.equal(String(asserted.addHours(24)), "2026-03-29T09:00[Europe/Paris]");
  assert.equal(String(asserted.addWeeks(1).addMinutes(1).addSeconds(1)), "2026-04-04T09:01:01[Europe/Paris]");
  assert.equal(text(dt(2026, 1, 31, 9, 0).inZone(NY).addMonths(1)), "none (month-end)");
  assert.equal(String(dt(2026, 1, 31, 9, 0).inZone(NY).addMonths(1, MonthEnd.Constrain)), "2026-02-28T09:00[America/New_York]");
  assert.equal(asserted.zoneId, "Europe/Paris");
  assert.equal(String(asserted.dateTime), "2026-03-28T09:00");
  assert.equal((asserted as unknown as { compare?: unknown }).compare, undefined);

  const meeting = dt(2027, 6, 1, 9, 0).inZone("Europe/Paris");
  assert.equal(text(meeting.resolve(ctx)), "2027-06-01T07:00:00Z");
  const gap = dt(2026, 3, 8, 2, 30).inZone(NY);
  assert.deepEqual(codes(gap.resolve(ctx)), ["gap"]);
  assert.equal(text(gap.resolve(ctx, { gap: Gap.ShiftForward })), "2026-03-08T07:30:00Z");
  const twice = dt(2026, 11, 1, 1, 30).inZone(NY);
  const both = twice.resolve(ctx);
  assert.ok(both instanceof Multiple);
  assert.deepEqual(both.values.map(String), ["2026-11-01T05:30:00Z", "2026-11-01T06:30:00Z"]);
  assert.deepEqual(codes(both), ["ambiguous"]);
  assert.equal(codes(twice.resolve(ctx, ResolutionPolicy.AllCandidates)).length, 0);
  assert.equal(text(twice.resolve(ctx, { overlap: Overlap.Earliest })), "2026-11-01T05:30:00Z");
  assert.equal(text(twice.resolve(ctx, ResolutionPolicy.of(Gap.Reject, Overlap.Latest))), "2026-11-01T06:30:00Z");
  assert.equal(text(ctx.resolve(twice, ResolutionPolicy.Strict)), "multiple (ambiguous)");
  assert.equal(text(dt(2026, 7, 1, 9, 0).inZone("Asia/Tokyo").resolve(ctx)), "unsupported (unknown-zone)");
  assert.equal(text(dt(2030, 7, 1, 9, 0).inZone(NY).resolve(ctx)), "unsupported (outside-rule-window)");
  assert.equal(text(ZonedIntent.parse("2026-07-04T12:00+01:00[America/New_York]").single().resolve(ctx)), "invalid (offset-mismatch)");
});

test("TemporalContext.compare: zoned values resolve first; an overlap can make the order multiple", () => {
  const twice = dt(2026, 11, 1, 1, 30).inZone(NY);
  const between = Instant.fromUnixSeconds(1793514600 - 1800); // 06:00Z, between the two readings of 01:30
  const o = ctx.compare(twice, between);
  assert.ok(o instanceof Multiple);
  assert.deepEqual([...o.values], [Ordering.Before, Ordering.After]);
  assert.equal(text(ctx.compare(dt(2027, 6, 1, 9, 0).inZone("Europe/Paris"), dt(2027, 6, 1, 9, 0).inZone(NY))), "before");
  assert.equal(text(ctx.compare(dt(2026, 3, 8, 2, 30).inZone(NY), between)), "none (gap)");
  assert.equal(text(ctx.compare(Instant.fromUnixSeconds(1), Instant.fromUnixSeconds(0))), "after");
});

test("Instant: exact arithmetic, zones, offsets, native interop", () => {
  const t = Instant.fromUnixSeconds(1793885400, 100_000_000);
  assert.equal(String(t), "2026-11-05T13:30:00.1Z");
  assert.equal(t.unixSeconds, 1793885400n);
  assert.equal(t.nanoseconds, 100_000_000);
  assert.equal(String(t.until(Instant.fromUnixSeconds(1793885400, 300_000_000))), "0.2 s (POSIX)");
  assert.equal(String(t.add(PosixSeconds.of(1, 2))), "2026-11-05T13:30:00.6Z");
  assert.equal(text(t.add(ElapsedSeconds.of(1))), "unsupported (needs-leap-seconds)");
  assert.equal(String(t.atOffset(3600)), "2026-11-05T14:30:00.1+01:00");
  assert.equal(text(t.inZone(NY, ctx)), "2026-11-05T08:30:00.1-05:00[America/New_York]");
  assert.equal(text(ctx.inZone(t, "Asia/Tokyo")), "unsupported (unknown-zone)");
  assert.ok(t.isBefore(t.add(PosixSeconds.of(1, 1_000_000_000))));
  assert.equal(String(Instant.fromUnixNanoseconds(1_500_000_000n)), "1970-01-01T00:00:01.5Z");
  assert.equal(String(Instant.fromUnixMilliseconds(-1)), "1969-12-31T23:59:59.999Z");
  const original = new Date(Date.UTC(2026, 10, 5, 13, 30, 0, 250));
  assert.equal(Instant.fromDate(original).toDate().single().getTime(), original.getTime());
  const fine = Instant.fromUnixNanoseconds(1n).toDate();
  assert.equal(fine.status, "unique");
  assert.deepEqual(codes(fine), ["precision-loss"]);
  assert.equal(text(Instant.fromUnixSeconds(10n ** 20n).toDate()), "unsupported (out-of-native-range)");
});

test("Period, PosixSeconds, ElapsedSeconds: components, negate, equality", () => {
  const p = Period.of({ years: 1, months: 2, weeks: 3, days: 4, hours: 5, minutes: 6, seconds: 7 });
  assert.equal([p.years, p.months, p.weeks, p.days, p.hours, p.minutes, p.seconds].join(), "1,2,3,4,5,6,7");
  assert.equal(String(p), "P1Y2M3W4DT5H6M7S");
  assert.equal(String(p.negate()), "-P1Y2M3W4DT5H6M7S");
  assert.equal(String(Period.days(30)), "P30D");
  assert.ok(Period.weeks(1).equals(Period.parse("P1W").single()));
  assert.ok(!Period.days(7).equals(Period.weeks(1)));
  assert.equal(String(PosixSeconds.of(3, 2).negate()), "-1.5 s (POSIX)");
  assert.equal([PosixSeconds.of(6, 4).numerator, PosixSeconds.of(6, 4).denominator].join(), "3,2");
  assert.ok(PosixSeconds.of(2, 4).equals(PosixSeconds.of(1, 2)));
  assert.equal(String(ElapsedSeconds.of(86400).negate()), "-86400 s (SI)");
  assert.equal(String(Period.years(1)) + String(Period.hours(2)) + String(Period.seconds(3)), "P1YPT2HPT3S");
});

test("Interval: contains and overlaps give Truth; start and end are Bounds", () => {
  const november = Interval.halfOpen(date(2026, 11, 1), date(2026, 12, 1));
  assert.equal(november.contains(date(2026, 11, 15)), Truth.Definitely);
  assert.equal(november.contains(date(2026, 12, 1)), Truth.Impossible);
  assert.equal(november.overlaps(Interval.halfOpen(date(2026, 12, 1), date(2027, 1, 1))), Truth.Impossible);
  assert.equal(november.overlaps(Interval.closed(date(2026, 11, 30), date(2026, 12, 1))), Truth.Definitely);
  const open = Interval.of(Bound.included(date(2026, 11, 1)), Bound.unknown);
  assert.equal(open.contains(date(2027, 6, 1)), Truth.Possibly);
  assert.equal(open.contains(date(2026, 10, 1)), Truth.Impossible);
  assert.equal(november.start.state, "included");
  assert.ok(november.start.state === "included" && november.start.value.equals(date(2026, 11, 1)));
  assert.equal(november.end.state, "excluded");
  const hours = Interval.halfOpen(Instant.fromUnixSeconds(0), Instant.fromUnixSeconds(3600));
  assert.equal(hours.contains(Instant.fromUnixSeconds(3599, 999_999_999)), Truth.Definitely);
  // @ts-expect-error -- a date interval can't be asked about an instant.
  assert.throws(() => november.contains(Instant.fromUnixSeconds(0)));
  assert.throws(() => Interval.of(Bound.unbounded, Bound.unknown), RangeError);
  assert.equal(String(Interval.parse("2026-11-01/..").single()), "[2026-11-01, ..)");
});

test("policy overloads: the total addMonths / addYears take only Constrain (library-api §2)", () => {
  // @ts-expect-error -- Reject is the overload without a policy; passing it is a programming error.
  assert.throws(() => date(2026, 1, 31).addMonths(1, MonthEnd.Reject), RangeError);
  // @ts-expect-error -- the string literal is guarded too.
  assert.throws(() => date(2024, 2, 29).addYears(1, "reject"), RangeError);
  // @ts-expect-error -- and on date-times and zoned values.
  assert.throws(() => dt(2026, 1, 31, 9, 0).addMonths(1, "reject"), RangeError);
  // @ts-expect-error -- ditto.
  assert.throws(() => dt(2026, 1, 31, 9, 0).inZone(NY).addMonths(1, MonthEnd.Reject), RangeError);
  // @ts-expect-error -- not a policy at all.
  assert.throws(() => date(2026, 1, 31).addMonths(1, "nearest"), RangeError);
  assert.equal(String(date(2024, 2, 29).addYears(1, MonthEnd.Constrain)), "2025-02-28");
  assert.equal(text(date(2024, 2, 29).addYears(1)), "none (month-end)");
});

test("Instant.subSecond is exact; nanoseconds floors it (library-api §1)", () => {
  const third = Instant.fromUnixSeconds(10).add(PosixSeconds.of(1, 3));
  assert.deepEqual({ ...third.subSecond }, { numerator: 1n, denominator: 3n });
  assert.equal(third.nanoseconds, 333_333_333);
  assert.equal(third.unixSeconds, 10n);
  const before = Instant.fromUnixSeconds(0).add(PosixSeconds.of(-1, 3)); // -1/3 s: second -1, fraction 2/3
  assert.deepEqual({ ...before.subSecond }, { numerator: 2n, denominator: 3n });
  assert.equal(before.unixSeconds, -1n);
  assert.equal(before.nanoseconds, 666_666_666);
  const half = Instant.fromUnixSeconds(0, 500_000_000);
  assert.deepEqual({ ...half.subSecond }, { numerator: 1n, denominator: 2n });
  assert.equal(half.nanoseconds, 500_000_000);
  assert.deepEqual({ ...Instant.fromUnixSeconds(5).subSecond }, { numerator: 0n, denominator: 1n });
});

test("text form: an interval with no point has no domain", () => {
  assert.equal(text(parse("(.., ..)")), "unsupported (unsupported-form)");
  assert.equal(text(parse("(?, ?)")), "unsupported (unsupported-form)");
});

test("policies accept the typed constants and the literal strings", () => {
  assert.equal(String(date(2026, 1, 31).addMonths(1, "constrain")), "2026-02-28");
  assert.equal(text(dt(2026, 3, 8, 2, 30).inZone(NY).resolve(ctx, { gap: "shift-forward-by-gap" })), "2026-03-08T07:30:00Z");
  assert.equal(ResolutionPolicy.Strict.gap, Gap.Reject);
});
