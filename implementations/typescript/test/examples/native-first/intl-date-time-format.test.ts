// What JavaScript's Intl.DateTimeFormat does with time zones and calendars, compared with UTV's Instant.inZone,
// ZonedIntent and validateSemantic. Where the two agree, the expected value is computed with UTV.
//
// Intl.DateTimeFormat reads the host's tz database (through ICU), so these tests need one that knows the 2026–2027
// rules of New York. UTV reads only the pinned demo rules it is given. Intl only goes one way, from an instant to a
// wall time; JavaScript has no call that turns a wall time in a zone into an instant.
import { describe, it } from "node:test";
import assert from "node:assert/strict";
import { CivilDateTime, Instant, PosixSeconds, TemporalContext } from "../../../src/index.js";
import { demoZoneRules } from "../fixtures.js";

describe("Intl.DateTimeFormat", () => {
  describe("formatToParts", () => {
    /**
     * Verifies that Intl.DateTimeFormat.formatToParts shows the local wall time when given an instant and a zone.
     * Compared with: `Instant.inZone` gives the same wall time from the pinned rules, with its offset, as a value
     * instead of text.
     */
    it("shows 03:00 in New York when given 07:00 UTC on 1 June 2027", () => {
      // GIVEN: 07:00 UTC on 1 June 2027, when a 09:00 meeting starts in Paris
      const givenMoment = new Date(Date.UTC(2027, 5, 1, 7, 0));

      // SYSTEM UNDER TEST: New York's wall time, from the host's tz database
      const sut = new Intl.DateTimeFormat("en-US", {
        timeZone: "America/New_York", hourCycle: "h23",
        year: "numeric", month: "numeric", day: "numeric", hour: "numeric", minute: "numeric",
      });

      // WHEN
      const actualFields = Object.fromEntries(sut.formatToParts(givenMoment).map((part) => [part.type, part.value]));

      // EXPECTATIONS: the wall time UTV computes from the pinned rules
      const tmpContext = TemporalContext.with(demoZoneRules());
      const expectedWallTime = Instant.fromUnixSeconds(1_811_833_200).inZone("America/New_York", tmpContext).single().dateTime;

      // THEN
      assert.equal(Number(actualFields["year"]), Number(expectedWallTime.date.year));
      assert.equal(Number(actualFields["month"]), expectedWallTime.date.month);
      assert.equal(Number(actualFields["day"]), expectedWallTime.date.day);
      assert.equal(Number(actualFields["hour"]), expectedWallTime.time.hour);
      assert.equal(Number(actualFields["minute"]), expectedWallTime.time.minute);
    });

    /**
     * Verifies that Intl.DateTimeFormat.formatToParts jumps from 01:59 to 03:00 when the clocks spring forward, so no
     * instant shows a wall time between 02:00 and 03:00.
     * Compared with: `Instant.inZone` gives the same 03:00; `ZonedIntent.resolve` returns none (gap) for 02:30, where
     * JavaScript has nothing to call.
     */
    it("shows 03:00 when given the minute after 01:59 on the spring-forward night", () => {
      // GIVEN: 07:00 UTC on 8 March 2026, one minute after 01:59 standard time in New York
      const givenMoment = new Date(Date.UTC(2026, 2, 8, 7, 0));

      // SYSTEM UNDER TEST: New York's wall time, from the host's tz database
      const sut = new Intl.DateTimeFormat("en-US", {
        timeZone: "America/New_York", hourCycle: "h23",
        year: "numeric", month: "numeric", day: "numeric", hour: "numeric", minute: "numeric",
      });

      // WHEN
      const actualFields = Object.fromEntries(sut.formatToParts(givenMoment).map((part) => [part.type, part.value]));

      // EXPECTATIONS: the wall time UTV computes from the pinned rules, daylight time already
      const tmpContext = TemporalContext.with(demoZoneRules());
      const expectedWallTime = Instant.fromUnixSeconds(1_772_953_200).inZone("America/New_York", tmpContext).single().dateTime;

      // THEN
      assert.equal(Number(actualFields["year"]), Number(expectedWallTime.date.year));
      assert.equal(Number(actualFields["month"]), expectedWallTime.date.month);
      assert.equal(Number(actualFields["day"]), expectedWallTime.date.day);
      assert.equal(Number(actualFields["hour"]), expectedWallTime.time.hour);
      assert.equal(Number(actualFields["minute"]), expectedWallTime.time.minute);
    });

    /**
     * Verifies that Intl.DateTimeFormat.formatToParts shows 01:30 for the second of two instants an hour apart when
     * the clocks go back.
     * Compared with: `ZonedIntent.resolve` for 01:30 returns this instant and the one an hour earlier (multiple,
     * ambiguous); with Intl, only the instant knows which 01:30 it is.
     */
    it("shows 01:30 when given the later of the two instants that wall time names", () => {
      // GIVEN: 06:30 UTC on 1 November 2026, 01:30 standard time in New York
      const givenMoment = new Date(Date.UTC(2026, 10, 1, 6, 30));

      // SYSTEM UNDER TEST: New York's wall time, from the host's tz database
      const sut = new Intl.DateTimeFormat("en-US", {
        timeZone: "America/New_York", hourCycle: "h23",
        year: "numeric", month: "numeric", day: "numeric", hour: "numeric", minute: "numeric",
      });

      // WHEN
      const actualFields = Object.fromEntries(sut.formatToParts(givenMoment).map((part) => [part.type, part.value]));

      // EXPECTATIONS: the wall time UTV computes from the pinned rules
      const tmpContext = TemporalContext.with(demoZoneRules());
      const expectedWallTime = Instant.fromUnixSeconds(1_793_514_600).inZone("America/New_York", tmpContext).single().dateTime;

      // THEN
      assert.equal(Number(actualFields["year"]), Number(expectedWallTime.date.year));
      assert.equal(Number(actualFields["month"]), expectedWallTime.date.month);
      assert.equal(Number(actualFields["day"]), expectedWallTime.date.day);
      assert.equal(Number(actualFields["hour"]), expectedWallTime.time.hour);
      assert.equal(Number(actualFields["minute"]), expectedWallTime.time.minute);
    });

    /**
     * Verifies that Intl.DateTimeFormat.formatToParts shows 13:00 when given noon plus 86,400,000 ms across the
     * spring-forward night.
     * Compared with: `Instant.add(PosixSeconds.of(86_400))` reaches the same 13:00; `ZonedIntent.addDays(1)` keeps
     * noon.
     */
    it("shows 13:00 when the instant is 86,400,000 ms after noon on the day before clocks spring forward", () => {
      // GIVEN: 24 hours of milliseconds after noon on 7 March 2026 in New York, standard time
      const givenMoment = new Date(Date.parse("2026-03-07T12:00:00-05:00") + 86_400_000);

      // SYSTEM UNDER TEST: New York's wall time, from the host's tz database
      const sut = new Intl.DateTimeFormat("en-US", {
        timeZone: "America/New_York", hourCycle: "h23",
        year: "numeric", month: "numeric", day: "numeric", hour: "numeric", minute: "numeric",
      });

      // WHEN
      const actualFields = Object.fromEntries(sut.formatToParts(givenMoment).map((part) => [part.type, part.value]));

      // EXPECTATIONS: the wall time UTV reaches with 86,400 POSIX seconds, from the pinned rules
      const tmpContext = TemporalContext.with(demoZoneRules());
      const tmpNoon = CivilDateTime.of(2026, 3, 7, 12, 0).inZone("America/New_York").resolve(tmpContext).single();
      const expectedWallTime = tmpNoon.add(PosixSeconds.of(86_400)).inZone("America/New_York", tmpContext).single().dateTime;

      // THEN
      assert.equal(Number(actualFields["year"]), Number(expectedWallTime.date.year));
      assert.equal(Number(actualFields["month"]), expectedWallTime.date.month);
      assert.equal(Number(actualFields["day"]), expectedWallTime.date.day);
      assert.equal(Number(actualFields["hour"]), expectedWallTime.time.hour);
      assert.equal(Number(actualFields["minute"]), expectedWallTime.time.minute);
    });
  });

  describe("format", () => {
    /**
     * Verifies that Intl.DateTimeFormat with the Hebrew calendar writes a Date as a Hebrew date.
     * Compared with: UTV keeps a Hebrew date's own fields as written, and `validateSemantic` returns unsupported
     * (unsupported-calendar) rather than converting; JavaScript can write Hebrew dates but not read or compute them.
     */
    it("writes 14 Heshvan 5787 when given 25 October 2026 and the Hebrew calendar", () => {
      // GIVEN: midnight UTC at the start of 25 October 2026
      const givenDate = new Date(Date.UTC(2026, 9, 25));

      // SYSTEM UNDER TEST: a formatter for the Hebrew calendar, read at UTC
      const sut = new Intl.DateTimeFormat("en-US", {
        calendar: "hebrew", timeZone: "UTC", year: "numeric", month: "long", day: "numeric",
      });

      // WHEN
      const actualText = sut.format(givenDate);

      // EXPECTATIONS: the Hebrew date, as text only
      const expectedText = "14 Heshvan 5787";

      // THEN
      assert.equal(actualText, expectedText);
    });
  });
});
