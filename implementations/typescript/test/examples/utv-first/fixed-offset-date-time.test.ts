// How to use FixedOffsetDateTime, a wall time pinned to a UTC offset (but to no zone), compared with JavaScript's
// Date, which turns a wall time and an offset into an instant and keeps only the instant.
import { describe, it } from "node:test";
import assert from "node:assert/strict";
import { CivilDateTime, FixedOffsetDateTime, Instant } from "../../../src/index.js";

describe("FixedOffsetDateTime", () => {
  describe("parse", () => {
    /**
     * Verifies that FixedOffsetDateTime.parse keeps the wall time and the offset when the text has an offset.
     * Compared with: `Date.parse("2026-11-05T14:30:00+01:00")` keeps only the instant; reading it back gives 13:30 UTC
     * or the host's own wall time, never 14:30 at +01:00.
     */
    it("keeps the wall time and the offset when the text has an offset", () => {
      // GIVEN: lunch at 14:30 on 5 November 2026, one hour east of UTC
      const givenText = "2026-11-05T14:30+01:00";

      // WHEN
      const actualLunch: FixedOffsetDateTime = FixedOffsetDateTime.parse(givenText).single();

      // EXPECTATIONS: the wall time and the offset as written
      const expectedWallTime = CivilDateTime.of(2026, 11, 5, 14, 30);
      const expectedOffsetSeconds = 3600;

      // THEN
      assert.equal(String(actualLunch.dateTime), String(expectedWallTime));
      assert.equal(actualLunch.offsetSeconds, expectedOffsetSeconds);
    });
  });

  describe("toInstant", () => {
    /**
     * Verifies that FixedOffsetDateTime.toInstant returns the instant the wall time and offset describe.
     * Compared with: `new Date("2026-11-05T14:30:00+01:00")` holds the same moment.
     */
    it("returns the UTC moment when the offset is known", () => {
      // SYSTEM UNDER TEST: 14:30 on 5 November 2026, one hour east of UTC
      const sut = CivilDateTime.of(2026, 11, 5, 14, 30).atOffset(3600);

      // WHEN
      const actualInstant: Instant = sut.toInstant();

      // EXPECTATIONS: the moment Date parses from the same wall time and offset
      const expectedInstant = Instant.fromDate(new Date("2026-11-05T14:30:00+01:00"));

      // THEN
      assert.equal(String(actualInstant), String(expectedInstant));
    });
  });
});
