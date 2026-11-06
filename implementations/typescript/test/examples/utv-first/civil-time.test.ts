// How to use CivilTime, a wall-clock time of day with no date and no zone, compared with JavaScript, which has no
// time-of-day type: a Date on a made-up day stands in, built with Date.UTC and read with the getUTC… methods so the
// host's time zone cannot change the answer.
import { describe, it } from "node:test";
import assert from "node:assert/strict";
import { CivilTime } from "../../../src/index.js";

describe("CivilTime", () => {
  describe("of", () => {
    // BEGIN time-of-day
    /**
     * Verifies that CivilTime.of keeps the time to the minute when no seconds are given.
     * Compared with: a Date needs a date first; `new Date(Date.UTC(1970, 0, 1, 9, 30)).toISOString()` writes
     * `1970-01-01T09:30:00.000Z`, with seconds and milliseconds nobody gave.
     */
    it("writes no seconds when no seconds are given", () => {
      // GIVEN: half past nine, to the minute
      const givenHour = 9;
      const givenMinute = 30;

      // WHEN
      const actualTime: CivilTime = CivilTime.of(givenHour, givenMinute);

      // EXPECTATIONS: the same hour and minute as Date's stand-in, and no seconds in the text
      const expectedTime = new Date(Date.UTC(1970, 0, 1, 9, 30));
      const expectedText = "09:30";

      // THEN
      assert.equal(actualTime.hour, expectedTime.getUTCHours());
      assert.equal(actualTime.minute, expectedTime.getUTCMinutes());
      assert.equal(actualTime.toString(), expectedText);
    });
    // END time-of-day

    /**
     * Verifies that CivilTime.of keeps the seconds when they are given.
     * Compared with: the time part of `toISOString()` on Date's stand-in is the same text, followed by milliseconds.
     */
    it("writes the seconds when seconds are given", () => {
      // GIVEN: half past nine, to the second
      const givenHour = 9;
      const givenMinute = 30;
      const givenSecond = 0;

      // WHEN
      const actualTime = CivilTime.of(givenHour, givenMinute, givenSecond);

      // EXPECTATIONS: the time part of Date's ISO text, without its milliseconds
      const expectedText = new Date(Date.UTC(1970, 0, 1, 9, 30, 0)).toISOString().slice(11, 19);

      // THEN
      assert.equal(actualTime.toString(), expectedText);
    });
  });
});
