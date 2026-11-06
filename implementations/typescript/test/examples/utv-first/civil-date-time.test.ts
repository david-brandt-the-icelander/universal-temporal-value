// How to use CivilDateTime, a wall-clock date and time with no zone, compared with JavaScript's Date, which always
// holds an instant: its local constructor reads the fields in the host's time zone. A Date built with Date.UTC and
// read with the getUTC… methods is the usual zone-free stand-in, and the host's time zone cannot change it.
import { describe, it } from "node:test";
import assert from "node:assert/strict";
import { CivilDateTime, FixedOffsetDateTime, Instant, ZonedIntent, type Outcome } from "../../../src/index.js";

describe("CivilDateTime", () => {
  describe("of", () => {
    /**
     * Verifies that CivilDateTime.of creates a wall-clock date-time with no zone when given a date and a time.
     * Compared with: `new Date(2026, 10, 5, 14, 30)` reads the fields in the host's zone and holds an instant; only
     * the UTC stand-in `Date.UTC(2026, 10, 5, 14, 30)` keeps the wall time as given.
     */
    it("creates a wall-clock date-time when given a date and a time", () => {
      // GIVEN: 14:30 on 5 November 2026, in no particular zone
      const givenYear = 2026;
      const givenMonth = 11;
      const givenDay = 5;
      const givenHour = 14;
      const givenMinute = 30;

      // WHEN
      const actualDateTime: CivilDateTime = CivilDateTime.of(givenYear, givenMonth, givenDay, givenHour, givenMinute);

      // EXPECTATIONS: the wall time of Date's UTC stand-in, without its seconds, milliseconds and Z
      const expectedText = new Date(Date.UTC(2026, 10, 5, 14, 30)).toISOString().slice(0, 16);

      // THEN
      assert.equal(actualDateTime.toString(), expectedText);
    });
  });

  describe("tryOf", () => {
    /**
     * Verifies that CivilDateTime.tryOf returns unsupported when the second is a leap second, 60.
     * Compared with: `Date.UTC(2016, 11, 31, 23, 59, 60)` rolls over to 00:00 on 1 January 2017, and
     * `Date.parse("2016-12-31T23:59:60Z")` is NaN, as if the second could never exist.
     */
    it("returns unsupported when the second is a leap second", () => {
      // GIVEN: 23:59:60 on 31 December 2016, a real leap second
      const givenYear = 2016;
      const givenMonth = 12;
      const givenDay = 31;
      const givenHour = 23;
      const givenMinute = 59;
      const givenSecond = 60;

      // WHEN
      const actualOutcome: Outcome<CivilDateTime> =
        CivilDateTime.tryOf(givenYear, givenMonth, givenDay, givenHour, givenMinute, givenSecond);

      // EXPECTATIONS: unsupported, not invalid: judging it needs a leap-second table
      const expectedStatus = "unsupported";
      const expectedCodes = ["leap-second"];

      // THEN
      assert.equal(actualOutcome.status, expectedStatus);
      assert.deepEqual(actualOutcome.diagnostics.map((diagnostic) => diagnostic.code), expectedCodes);
    });
  });

  describe("addHours", () => {
    // BEGIN datetime-plus-hours
    /**
     * Verifies that CivilDateTime.addHours carries into the next day when the hours pass midnight.
     * Compared with: `setUTCHours(getUTCHours() + 2)` on the UTC stand-in gives the same wall time; the local
     * `setHours` would add the hours in the host's zone, DST changes included.
     */
    it("carries into the next day when the hours pass midnight", () => {
      // GIVEN: two hours, past midnight
      const givenHours = 2;

      // SYSTEM UNDER TEST: 23:30 on 5 November 2026
      const sut = CivilDateTime.of(2026, 11, 5, 23, 30);

      // WHEN
      const actualDateTime: CivilDateTime = sut.addHours(givenHours);

      // EXPECTATIONS: the wall time Date computes for the same addition
      const tmpDate = new Date(Date.UTC(2026, 10, 5, 23, 30));
      tmpDate.setUTCHours(tmpDate.getUTCHours() + 2);
      const expectedDateTime = CivilDateTime.of(
        tmpDate.getUTCFullYear(), tmpDate.getUTCMonth() + 1, tmpDate.getUTCDate(), tmpDate.getUTCHours(), tmpDate.getUTCMinutes());

      // THEN
      assert.equal(String(actualDateTime), String(expectedDateTime));
    });
    // END datetime-plus-hours
  });

  describe("inZone", () => {
    /**
     * Verifies that CivilDateTime.inZone keeps the wall time and the zone name as an intention.
     * Compared with: a Date cannot hold a zone; `Intl.DateTimeFormat` with `timeZone` only formats an instant that
     * already exists, using the host's tz database of today.
     */
    it("keeps the wall time and the zone when given a zone id", () => {
      // GIVEN: the Paris time zone
      const givenZoneId = "Europe/Paris";

      // SYSTEM UNDER TEST: 09:00 on 1 June 2027
      const sut = CivilDateTime.of(2027, 6, 1, 9, 0);

      // WHEN
      const actualMeeting: ZonedIntent = sut.inZone(givenZoneId);

      // EXPECTATIONS: the wall time and the zone, with no offset decided yet
      const expectedText = "2027-06-01T09:00[Europe/Paris]";

      // THEN
      assert.equal(actualMeeting.toString(), expectedText);
    });
  });

  describe("atOffset", () => {
    /**
     * Verifies that CivilDateTime.atOffset pins the wall time to a fixed UTC offset.
     * Compared with: `new Date("2026-11-05T14:30:00+01:00")` holds the same instant, and forgets the offset.
     */
    it("pins the wall time to the offset when given offset seconds", () => {
      // GIVEN: one hour east of UTC
      const givenOffsetSeconds = 3600;

      // SYSTEM UNDER TEST: 14:30 on 5 November 2026
      const sut = CivilDateTime.of(2026, 11, 5, 14, 30);

      // WHEN
      const actualDateTime: FixedOffsetDateTime = sut.atOffset(givenOffsetSeconds);

      // EXPECTATIONS: the instant Date parses from the same wall time and offset, and the offset kept alongside it
      const expectedInstant = Instant.fromDate(new Date("2026-11-05T14:30:00+01:00"));
      const expectedText = "2026-11-05T14:30+01:00";

      // THEN
      assert.equal(String(actualDateTime.toInstant()), String(expectedInstant));
      assert.equal(actualDateTime.toString(), expectedText);
    });
  });
});
