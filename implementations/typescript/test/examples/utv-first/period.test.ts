// How to use Period, an amount of calendar components such as days and months, compared with JavaScript, which has
// no duration type at all: an amount of time is a number of milliseconds, so a calendar day is 86,400,000 by
// convention and a month cannot be written down.
import { describe, it } from "node:test";
import assert from "node:assert/strict";
import { Period, PosixSeconds } from "../../../src/index.js";

describe("Period", () => {
  describe("days", () => {
    /**
     * Verifies that Period.days creates a calendar day, which is not a number of seconds.
     * Compared with: in JavaScript a day is `86_400_000` ms, the same number as 86,400 seconds, so the difference is
     * lost.
     */
    it("is not equal to 86,400 seconds when given one day", () => {
      // GIVEN: one day
      const givenDays = 1;

      // WHEN
      const actualPeriod: Period = Period.days(givenDays);

      // EXPECTATIONS: a calendar component, written in ISO 8601, and a different value from JavaScript's day of
      // 86,400,000 ms, counted as seconds
      const expectedText = "P1D";
      const expectedOtherAmount = PosixSeconds.of(86_400_000 / 1000);

      // THEN
      assert.equal(actualPeriod.toString(), expectedText);
      assert.equal(actualPeriod.equals(expectedOtherAmount), false);
    });
  });

  describe("months", () => {
    /**
     * Verifies that Period.months creates a calendar month, distinct from a minute.
     * Compared with: JavaScript has no months as an amount; ISO 8601 writes P1M for a month and PT1M for a minute.
     */
    it("writes P1M when given one month", () => {
      // GIVEN: one month
      const givenMonths = 1;

      // WHEN
      const actualPeriod = Period.months(givenMonths);

      // EXPECTATIONS: P1M, never PT1M
      const expectedText = "P1M";
      const expectedMinuteText = Period.minutes(1).toString();

      // THEN
      assert.equal(actualPeriod.toString(), expectedText);
      assert.notEqual(actualPeriod.toString(), expectedMinuteText);
    });
  });
});
