// How to use ZonedIntent, a wall time in a named zone that stays an intention until it is resolved against explicit
// zone rules, compared with JavaScript, which has no zoned type: a Date is an instant, and Intl.DateTimeFormat with a
// timeZone reads the host's tz database to tell the offset in force at an instant. UTV reads only the pinned demo
// rules; the expected values computed with Intl need a host tz database that knows the 2026–2027 rules of Paris and
// New York.
import { describe, it } from "node:test";
import assert from "node:assert/strict";
import {
  CivilDateTime, Gap, Instant, Multiple, Overlap, TemporalContext, ZonedIntent, type Outcome, type ResolutionPolicy,
} from "../../../src/index.js";
import { demoZoneRules } from "../fixtures.js";

describe("ZonedIntent", () => {
  describe("resolve", () => {
    // BEGIN meeting-in-paris
    /**
     * Verifies that ZonedIntent.resolve returns the one instant when the wall time happens exactly once.
     * Compared with: JavaScript has no call for this; subtracting the offset `Intl.DateTimeFormat` reports for Paris
     * (from the host's tz database) gives the same instant.
     */
    it("returns the instant when the wall time happens once", () => {
      // SETUP: the pinned demo zone rules, never the host's tz database
      const envContext = TemporalContext.with(demoZoneRules());

      // SYSTEM UNDER TEST: a meeting at 09:00 on 1 June 2027 in Paris
      const sut: ZonedIntent = CivilDateTime.of(2027, 6, 1, 9, 0).inZone("Europe/Paris");

      // WHEN
      const actualOutcome: Outcome<Instant> = sut.resolve(envContext);

      // EXPECTATIONS: 09:00 minus the offset Intl reports for Paris that morning ("GMT+02:00")
      const tmpWallTimeAsUtc = Date.UTC(2027, 5, 1, 9, 0);
      const tmpOffsetName = new Intl.DateTimeFormat("en-US", { timeZone: "Europe/Paris", timeZoneName: "longOffset" })
        .formatToParts(tmpWallTimeAsUtc).find((part) => part.type === "timeZoneName")?.value;
      const expectedInstant = Instant.fromUnixMilliseconds(tmpWallTimeAsUtc - Number(tmpOffsetName?.slice(3, 6)) * 3_600_000);

      // THEN
      assert.equal(String(actualOutcome.single()), String(expectedInstant));
    });
    // END meeting-in-paris

    // BEGIN dst-gap
    /**
     * Verifies that ZonedIntent.resolve returns none when the wall time falls in a daylight-saving gap.
     * Compared with: JavaScript has no call for this; the by-hand conversion with `Intl.DateTimeFormat` offsets
     * always returns some instant, 01:30 EST or 03:30 EDT, without an error.
     */
    it("returns none when the wall time falls in a gap", () => {
      // SETUP: the pinned demo zone rules, never the host's tz database
      const envContext = TemporalContext.with(demoZoneRules());

      // SYSTEM UNDER TEST: 02:30 on 8 March 2026 in New York, when clocks jump from 02:00 to 03:00
      const sut = CivilDateTime.of(2026, 3, 8, 2, 30).inZone("America/New_York");

      // WHEN
      const actualOutcome = sut.resolve(envContext);

      // EXPECTATIONS: no such instant, explained by the gap diagnostic
      const expectedStatus = "none";
      const expectedCodes = ["gap"];

      // THEN
      assert.equal(actualOutcome.status, expectedStatus);
      assert.deepEqual(actualOutcome.diagnostics.map((diagnostic) => diagnostic.code), expectedCodes);
    });

    /**
     * Verifies that ZonedIntent.resolve with Gap.ShiftForward moves the wall time forward by the length of the gap.
     * Compared with: JavaScript has no such policy; which side of the gap the by-hand conversion lands on depends on
     * which offset it guesses first.
     */
    it("shifts forward by the gap when ShiftForward is requested", () => {
      // GIVEN: the policy that moves a skipped wall time forward by the gap
      const givenPolicy: ResolutionPolicy = { gap: Gap.ShiftForward };

      // SETUP: the pinned demo zone rules, never the host's tz database
      const envContext = TemporalContext.with(demoZoneRules());

      // SYSTEM UNDER TEST: 02:30 on 8 March 2026 in New York, when clocks jump from 02:00 to 03:00
      const sut = CivilDateTime.of(2026, 3, 8, 2, 30).inZone("America/New_York");

      // WHEN
      const actualOutcome = sut.resolve(envContext, givenPolicy);

      // EXPECTATIONS: 03:30 daylight time, four hours behind UTC
      const expectedInstant = Instant.fromDate(new Date("2026-03-08T03:30:00-04:00"));

      // THEN
      assert.equal(String(actualOutcome.single()), String(expectedInstant));
    });
    // END dst-gap

    // BEGIN dst-overlap
    /**
     * Verifies that ZonedIntent.resolve returns both instants when the wall time happens twice.
     * Compared with: the by-hand conversion with the offset `Intl.DateTimeFormat` reports silently picks one of them,
     * here the daylight-time one.
     */
    it("returns both instants when the wall time falls in an overlap", () => {
      // SETUP: the pinned demo zone rules, never the host's tz database
      const envContext = TemporalContext.with(demoZoneRules());

      // SYSTEM UNDER TEST: 01:30 on 1 November 2026 in New York, when clocks go back from 02:00 to 01:00
      const sut = CivilDateTime.of(2026, 11, 1, 1, 30).inZone("America/New_York");

      // WHEN
      const actualOutcome = sut.resolve(envContext);

      // EXPECTATIONS: 01:30 daylight time, then 01:30 standard time; JavaScript's single answer is one of them
      const expectedInstants = [
        Instant.fromDate(new Date("2026-11-01T01:30:00-04:00")),
        Instant.fromDate(new Date("2026-11-01T01:30:00-05:00")),
      ];
      const expectedCodes = ["ambiguous"];
      const tmpWallTimeAsUtc = Date.UTC(2026, 10, 1, 1, 30);
      const tmpOffsetName = new Intl.DateTimeFormat("en-US", { timeZone: "America/New_York", timeZoneName: "longOffset" })
        .formatToParts(tmpWallTimeAsUtc).find((part) => part.type === "timeZoneName")?.value;
      const expectedNativePick = Instant.fromUnixMilliseconds(tmpWallTimeAsUtc - Number(tmpOffsetName?.slice(3, 6)) * 3_600_000);

      // THEN
      assert.ok(actualOutcome instanceof Multiple);
      assert.deepEqual(actualOutcome.values.map(String), expectedInstants.map(String));
      assert.deepEqual(actualOutcome.diagnostics.map((diagnostic) => diagnostic.code), expectedCodes);
      assert.ok(actualOutcome.values.map(String).includes(String(expectedNativePick)));
    });

    /**
     * Verifies that ZonedIntent.resolve with Overlap.Earliest returns the first of the two instants.
     * Compared with: JavaScript has no such policy; the by-hand conversion picks whichever its offset guess leads to,
     * and does not say that there was a choice.
     */
    it("returns the earlier instant when Earliest is requested", () => {
      // GIVEN: the policy that picks the earlier of two instants
      const givenPolicy: ResolutionPolicy = { overlap: Overlap.Earliest };

      // SETUP: the pinned demo zone rules, never the host's tz database
      const envContext = TemporalContext.with(demoZoneRules());

      // SYSTEM UNDER TEST: 01:30 on 1 November 2026 in New York, when clocks go back from 02:00 to 01:00
      const sut = CivilDateTime.of(2026, 11, 1, 1, 30).inZone("America/New_York");

      // WHEN
      const actualOutcome = sut.resolve(envContext, givenPolicy);

      // EXPECTATIONS: 01:30 daylight time, the first time the clock shows it
      const expectedInstant = Instant.fromDate(new Date("2026-11-01T01:30:00-04:00"));

      // THEN
      assert.equal(String(actualOutcome.single()), String(expectedInstant));
    });
    // END dst-overlap
  });

  describe("addDays", () => {
    // BEGIN day-vs-24h
    /**
     * Verifies that ZonedIntent.addDays keeps the wall time when the day crosses a daylight-saving change.
     * Compared with: adding 86,400,000 ms to the Date for noon lands on 13:00 in New York, as Intl shows it.
     */
    it("keeps the wall time when the day crosses a DST change", () => {
      // GIVEN: one calendar day
      const givenDays = 1;

      // SYSTEM UNDER TEST: noon on 7 March 2026 in New York, the day before clocks spring forward
      const sut = CivilDateTime.of(2026, 3, 7, 12, 0).inZone("America/New_York");

      // WHEN
      const actualNextNoon: ZonedIntent = sut.addDays(givenDays);

      // EXPECTATIONS: noon again, on the next date
      const expectedNextNoon = CivilDateTime.of(2026, 3, 8, 12, 0).inZone("America/New_York");

      // THEN
      assert.equal(String(actualNextNoon), String(expectedNextNoon));
    });
    // END day-vs-24h
  });
});
