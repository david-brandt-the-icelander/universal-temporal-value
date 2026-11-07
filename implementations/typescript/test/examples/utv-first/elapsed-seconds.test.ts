// How to use ElapsedSeconds, SI seconds as a stopwatch measures them, compared with JavaScript, which counts every
// amount of time in plain milliseconds and has one kind of second for every purpose.
import { describe, it } from "node:test";
import assert from "node:assert/strict";
import { ElapsedSeconds, Period, PosixSeconds } from "../../../src/index.js";

describe("ElapsedSeconds", () => {
  describe("of", () => {
    /**
     * Verifies that ElapsedSeconds.of holds exactly the elapsed seconds it is given.
     * Compared with: `90 * 60 * 1000` ms is the same number of seconds, as a bare number that says nothing about
     * which kind of second it counts.
     */
    it("holds the seconds when given ninety minutes", () => {
      // GIVEN: 90 minutes of elapsed time, in seconds
      const givenSeconds = 5_400;

      // WHEN
      const actualAmount: ElapsedSeconds = ElapsedSeconds.of(givenSeconds);

      // EXPECTATIONS: the seconds JavaScript counts in 90 minutes of milliseconds, labelled as SI seconds
      const expectedSeconds = (90 * 60 * 1000) / 1000;
      const expectedText = "5400 s (SI)";

      // THEN
      assert.equal(Number(actualAmount.numerator) / Number(actualAmount.denominator), expectedSeconds);
      assert.equal(actualAmount.toString(), expectedText);
    });

    /**
     * Verifies that ElapsedSeconds.of creates an amount that is neither a calendar day nor POSIX seconds when given
     * 86,400 seconds.
     * Compared with: `86_400_000` ms is all three at once in JavaScript.
     */
    it("is not a calendar day when given 86,400 seconds", () => {
      // GIVEN: exactly 86,400 SI seconds
      const givenSeconds = 86_400;

      // WHEN
      const actualAmount = ElapsedSeconds.of(givenSeconds);

      // EXPECTATIONS: different from a calendar day and from 86,400 POSIX seconds
      const expectedCalendarDay = Period.days(1);
      const expectedPosixDay = PosixSeconds.of(86_400);

      // THEN
      assert.equal(actualAmount.equals(expectedCalendarDay), false);
      assert.equal(actualAmount.equals(expectedPosixDay), false);
    });
  });
});
