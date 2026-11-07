// How to use TemporalContext, which holds the zone rules that zoned operations need, compared with JavaScript, where
// every zone conversion through Intl.DateTimeFormat reads the host's tz database.
import { describe, it } from "node:test";
import assert from "node:assert/strict";
import { CivilDateTime, TemporalContext, type Ordering, type Outcome } from "../../../src/index.js";
import { demoZoneRules } from "../fixtures.js";

describe("TemporalContext", () => {
  describe("compare", () => {
    // BEGIN compare-dates
    /**
     * Verifies that TemporalContext.compare returns both orders when both wall times fall in the same overlap.
     * Compared with: JavaScript has no zoned type; two Dates built from the wall times with `Date.UTC` compare by
     * wall time and always say 01:30 comes first.
     */
    it("returns multiple when both wall times fall in an overlap", () => {
      // GIVEN: a call at 01:30 and a stand-up at 01:45, on the night New York's clocks go back from 02:00 to 01:00
      const givenCall = CivilDateTime.of(2026, 11, 1, 1, 30).inZone("America/New_York");
      const givenStandup = CivilDateTime.of(2026, 11, 1, 1, 45).inZone("America/New_York");

      // SETUP: the pinned demo zone rules, never the host's tz database
      const envRules = demoZoneRules();

      // SYSTEM UNDER TEST
      const sut = TemporalContext.with(envRules);

      // WHEN
      const actualOutcome: Outcome<Ordering> = sut.compare(givenCall, givenStandup);

      // EXPECTATIONS: either could come first (01:30 standard time is after 01:45 daylight time); one diagnostic per
      // ambiguous wall time
      const expectedStatus = "multiple";
      const expectedCodes = ["ambiguous", "ambiguous"];

      // THEN
      assert.equal(actualOutcome.status, expectedStatus);
      assert.deepEqual(actualOutcome.diagnostics.map((diagnostic) => diagnostic.code), expectedCodes);
    });
    // END compare-dates
  });
});
