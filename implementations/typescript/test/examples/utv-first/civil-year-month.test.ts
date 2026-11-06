// How to use CivilYearMonth, a month of a Gregorian year with no day, compared with JavaScript, which has no
// year-month type and invents day 1 of the month with Date. The stand-in is built with Date.UTC and read with the
// getUTC… methods, so the host's time zone cannot change the answer.
import { describe, it } from "node:test";
import assert from "node:assert/strict";
import { CivilYearMonth } from "../../../src/index.js";

describe("CivilYearMonth", () => {
  describe("of", () => {
    /**
     * Verifies that CivilYearMonth.of creates a month without inventing a day when given a year and a month.
     * Compared with: `new Date(Date.UTC(2026, 10, 1))` has the same year and month, plus a day and a time nobody
     * asked for.
     */
    it("creates a month without a day when given a year and a month", () => {
      // GIVEN: November 2026
      const givenYear = 2026;
      const givenMonth = 11;

      // WHEN
      const actualMonth: CivilYearMonth = CivilYearMonth.of(givenYear, givenMonth);

      // EXPECTATIONS: the year and month of Date's stand-in, and text with no day in it
      const expectedStandIn = new Date(Date.UTC(2026, 10, 1));
      const expectedText = "2026-11";

      // THEN
      assert.equal(Number(actualMonth.year), expectedStandIn.getUTCFullYear());
      assert.equal(actualMonth.month, expectedStandIn.getUTCMonth() + 1);
      assert.equal(actualMonth.toString(), expectedText);
    });
  });

  describe("addMonths", () => {
    /**
     * Verifies that CivilYearMonth.addMonths returns the later month, carrying into the next year.
     * Compared with: `setUTCMonth(getUTCMonth() + 3)` on the stand-in day 1 lands in the same month.
     */
    it("returns the later month when the months cross a year end", () => {
      // GIVEN: three months, past December
      const givenMonths = 3;

      // SYSTEM UNDER TEST: November 2026
      const sut = CivilYearMonth.of(2026, 11);

      // WHEN
      const actualMonth: CivilYearMonth = sut.addMonths(givenMonths);

      // EXPECTATIONS: the month Date lands in from the stand-in day
      const tmpStandIn = new Date(Date.UTC(2026, 10, 1));
      tmpStandIn.setUTCMonth(tmpStandIn.getUTCMonth() + 3);
      const expectedMonth = CivilYearMonth.of(tmpStandIn.getUTCFullYear(), tmpStandIn.getUTCMonth() + 1);

      // THEN
      assert.equal(String(actualMonth), String(expectedMonth));
    });
  });
});
