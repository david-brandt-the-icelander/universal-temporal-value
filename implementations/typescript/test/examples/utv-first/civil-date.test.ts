// How to use CivilDate, a day in the Gregorian calendar with no time and no zone, compared with JavaScript's Date.
// Where the two agree, the expected value is computed with Date. JavaScript has no date-only type, so Date stands in
// as midnight UTC, built with Date.UTC and read with the getUTC… methods: the host's time zone cannot change the
// answer. (The local methods behave the same way, in the host's zone.)
import { describe, it } from "node:test";
import assert from "node:assert/strict";
import { CivilDate, MonthEnd, Period, Weekday, type Outcome } from "../../../src/index.js";

describe("CivilDate", () => {
  describe("of", () => {
    // BEGIN a-date
    /**
     * Verifies that CivilDate.of creates the date when the fields name a real day.
     * Compared with: `new Date(Date.UTC(2026, 10, 5))` names the same day as midnight UTC, with months counted from 0.
     */
    it("creates the date when the fields name a real day", () => {
      // GIVEN: 5 November 2026
      const givenYear = 2026;
      const givenMonth = 11;
      const givenDay = 5;

      // WHEN
      const actualDate: CivilDate = CivilDate.of(givenYear, givenMonth, givenDay);

      // EXPECTATIONS: the same day as JavaScript's Date
      const tmpDate = new Date(Date.UTC(2026, 10, 5));
      const expectedDate = CivilDate.of(tmpDate.getUTCFullYear(), tmpDate.getUTCMonth() + 1, tmpDate.getUTCDate());
      const expectedText = "2026-11-05";

      // THEN
      assert.equal(String(actualDate), String(expectedDate));
      assert.equal(actualDate.toString(), expectedText);
    });
    // END a-date

    /**
     * Verifies that CivilDate.of accepts 29 February when the year is a leap year.
     * Compared with: `Date.UTC(2024, 1, 29)` keeps the day in February too.
     */
    it("accepts the leap day when the year is a leap year", () => {
      // GIVEN: 29 February 2024, a year divisible by 4
      const givenYear = 2024;
      const givenMonth = 2;
      const givenDay = 29;

      // WHEN
      const actualDate = CivilDate.of(givenYear, givenMonth, givenDay);

      // EXPECTATIONS: the same day as JavaScript's Date
      const tmpDate = new Date(Date.UTC(2024, 1, 29));
      const expectedDate = CivilDate.of(tmpDate.getUTCFullYear(), tmpDate.getUTCMonth() + 1, tmpDate.getUTCDate());

      // THEN
      assert.equal(String(actualDate), String(expectedDate));
    });

    /**
     * Verifies that CivilDate.of accepts 29 February when the year is a century divisible by 400.
     * Compared with: `Date.UTC(2000, 1, 29)` keeps the day in February too.
     */
    it("accepts the leap day when the century is divisible by 400", () => {
      // GIVEN: 29 February 2000, a century year divisible by 400
      const givenYear = 2000;
      const givenMonth = 2;
      const givenDay = 29;

      // WHEN
      const actualDate = CivilDate.of(givenYear, givenMonth, givenDay);

      // EXPECTATIONS: the same day as JavaScript's Date
      const tmpDate = new Date(Date.UTC(2000, 1, 29));
      const expectedDate = CivilDate.of(tmpDate.getUTCFullYear(), tmpDate.getUTCMonth() + 1, tmpDate.getUTCDate());

      // THEN
      assert.equal(String(actualDate), String(expectedDate));
    });

    /**
     * Verifies that CivilDate.of throws a RangeError, with the reason, when the day does not exist in the month.
     * Compared with: `new Date(2026, 1, 30)` throws nothing and rolls over to 2 March.
     */
    it("throws when the day does not exist", () => {
      // GIVEN: 30 February 2026
      const givenYear = 2026;
      const givenMonth = 2;
      const givenDay = 30;

      // WHEN
      let actualError: unknown;
      try {
        CivilDate.of(givenYear, givenMonth, givenDay);
      } catch (error) {
        actualError = error;
      }

      // EXPECTATIONS: a RangeError that ends with the diagnostic code
      const expectedErrorType = RangeError;
      const expectedMessage = /\(day-out-of-range\)$/;

      // THEN
      assert.ok(actualError instanceof expectedErrorType);
      assert.match(actualError.message, expectedMessage);
    });

    /**
     * Verifies that CivilDate.of creates the date when the year is 275760, beyond any four-digit year.
     * Compared with: `new Date(8.64e15)` is the last moment a Date can hold, midnight UTC at the start of the same day.
     */
    it("creates the date when the year is the last year a Date can reach", () => {
      // GIVEN: 13 September 275760
      const givenYear = 275_760;
      const givenMonth = 9;
      const givenDay = 13;

      // WHEN
      const actualDate = CivilDate.of(givenYear, givenMonth, givenDay);

      // EXPECTATIONS: the day of Date's last moment, and an expanded year written with a sign
      const tmpDate = new Date(8.64e15);
      const expectedDate = CivilDate.of(tmpDate.getUTCFullYear(), tmpDate.getUTCMonth() + 1, tmpDate.getUTCDate());
      const expectedText = "+275760-09-13";

      // THEN
      assert.equal(String(actualDate), String(expectedDate));
      assert.equal(actualDate.toString(), expectedText);
    });

    /**
     * Verifies that CivilDate.of creates the date when the year is before year 1.
     * Compared with: `Date.UTC(-500, 2, 1)` reaches the same day of the proleptic Gregorian calendar, and
     * `toISOString` writes it with a six-digit year, `-000500-03-01T00:00:00.000Z`.
     */
    it("creates the date when the year is negative", () => {
      // GIVEN: 1 March of year -500 in the proleptic Gregorian calendar
      const givenYear = -500;
      const givenMonth = 3;
      const givenDay = 1;

      // WHEN
      const actualDate = CivilDate.of(givenYear, givenMonth, givenDay);

      // EXPECTATIONS: the same day as JavaScript's Date, written with a signed four-digit year
      const tmpDate = new Date(Date.UTC(-500, 2, 1));
      const expectedDate = CivilDate.of(tmpDate.getUTCFullYear(), tmpDate.getUTCMonth() + 1, tmpDate.getUTCDate());
      const expectedText = "-0500-03-01";

      // THEN
      assert.equal(String(actualDate), String(expectedDate));
      assert.equal(actualDate.toString(), expectedText);
    });
  });

  describe("tryOf", () => {
    // BEGIN feb-30
    /**
     * Verifies that CivilDate.tryOf returns invalid when the day is 30 February.
     * Compared with: `new Date(2026, 1, 30)` rolls over to 2 March without a word; tryOf reports the problem as a
     * value instead.
     */
    it("returns invalid when the day is 30 February", () => {
      // GIVEN: 30 February 2026
      const givenYear = 2026;
      const givenMonth = 2;
      const givenDay = 30;

      // WHEN
      const actualOutcome: Outcome<CivilDate> = CivilDate.tryOf(givenYear, givenMonth, givenDay);

      // EXPECTATIONS: invalid, because February 2026 has 28 days
      const expectedStatus = "invalid";
      const expectedCodes = ["day-out-of-range"];

      // THEN
      assert.equal(actualOutcome.status, expectedStatus);
      assert.deepEqual(actualOutcome.diagnostics.map((diagnostic) => diagnostic.code), expectedCodes);
    });
    // END feb-30

    /**
     * Verifies that CivilDate.tryOf returns invalid when 29 February falls in a common year.
     * Compared with: `Date.UTC(2026, 1, 29)` rolls over to 1 March.
     */
    it("returns invalid when 29 February falls in a common year", () => {
      // GIVEN: 29 February 2026, a year not divisible by 4
      const givenYear = 2026;
      const givenMonth = 2;
      const givenDay = 29;

      // WHEN
      const actualOutcome = CivilDate.tryOf(givenYear, givenMonth, givenDay);

      // EXPECTATIONS: invalid, rather than rolled over to 1 March
      const expectedStatus = "invalid";
      const expectedCodes = ["day-out-of-range"];

      // THEN
      assert.equal(actualOutcome.status, expectedStatus);
      assert.deepEqual(actualOutcome.diagnostics.map((diagnostic) => diagnostic.code), expectedCodes);
    });

    /**
     * Verifies that CivilDate.tryOf returns invalid when 29 February falls in a century not divisible by 400.
     * Compared with: `Date.UTC(1900, 1, 29)` rolls over to 1 March, because 1900 was not a leap year either.
     */
    it("returns invalid when 29 February falls in a century not divisible by 400", () => {
      // GIVEN: 29 February 1900, divisible by 100 but not by 400
      const givenYear = 1900;
      const givenMonth = 2;
      const givenDay = 29;

      // WHEN
      const actualOutcome = CivilDate.tryOf(givenYear, givenMonth, givenDay);

      // EXPECTATIONS: invalid, because 1900 was not a leap year
      const expectedStatus = "invalid";
      const expectedCodes = ["day-out-of-range"];

      // THEN
      assert.equal(actualOutcome.status, expectedStatus);
      assert.deepEqual(actualOutcome.diagnostics.map((diagnostic) => diagnostic.code), expectedCodes);
    });

    /**
     * Verifies that CivilDate.tryOf returns invalid when the month is 13.
     * Compared with: `Date.UTC(2026, 12, 1)` (month index 12) rolls over to 1 January 2027.
     */
    it("returns invalid when the month is 13", () => {
      // GIVEN: the first day of a thirteenth month
      const givenYear = 2026;
      const givenMonth = 13;
      const givenDay = 1;

      // WHEN
      const actualOutcome = CivilDate.tryOf(givenYear, givenMonth, givenDay);

      // EXPECTATIONS: invalid, because a Gregorian year has 12 months
      const expectedStatus = "invalid";
      const expectedCodes = ["month-out-of-range"];

      // THEN
      assert.equal(actualOutcome.status, expectedStatus);
      assert.deepEqual(actualOutcome.diagnostics.map((diagnostic) => diagnostic.code), expectedCodes);
    });
  });

  describe("parse", () => {
    /**
     * Verifies that CivilDate.parse returns the date when the text is an ISO 8601 calendar date.
     * Compared with: `Date.parse("2026-11-05")` reads the same day, as midnight UTC (date-only text is UTC).
     */
    it("returns the date when the text is an ISO date", () => {
      // GIVEN: the ISO 8601 text of 5 November 2026
      const givenText = "2026-11-05";

      // WHEN
      const actualOutcome = CivilDate.parse(givenText);

      // EXPECTATIONS: the day Date parses from the same text
      const tmpDate = new Date(Date.parse("2026-11-05"));
      const expectedDate = CivilDate.of(tmpDate.getUTCFullYear(), tmpDate.getUTCMonth() + 1, tmpDate.getUTCDate());

      // THEN
      assert.equal(String(actualOutcome.single()), String(expectedDate));
    });

    /**
     * Verifies that CivilDate.parse returns invalid, with the reason, when the text names a day that does not exist.
     * Compared with: Node's `Date.parse("2026-02-30")` rolls over to 2 March and gives no reason.
     */
    it("returns invalid when the text names a day that does not exist", () => {
      // GIVEN: well-formed text for 30 February 2026
      const givenText = "2026-02-30";

      // WHEN
      const actualOutcome = CivilDate.parse(givenText);

      // EXPECTATIONS: invalid, and the diagnostic says which field is wrong
      const expectedStatus = "invalid";
      const expectedCodes = ["day-out-of-range"];

      // THEN
      assert.equal(actualOutcome.status, expectedStatus);
      assert.deepEqual(actualOutcome.diagnostics.map((diagnostic) => diagnostic.code), expectedCodes);
    });
  });

  describe("dayOfWeek and isoWeek", () => {
    // BEGIN weekday-week
    /**
     * Verifies that CivilDate.dayOfWeek returns the weekday of the date.
     * Compared with: `getUTCDay()` returns 4 for the same day, numbering Sunday 0 where ISO numbers it 7;
     * `Intl.DateTimeFormat` names it.
     */
    it("returns Thursday when the date is 5 November 2026", () => {
      // SYSTEM UNDER TEST: 5 November 2026
      const sut = CivilDate.of(2026, 11, 5);

      // WHEN
      const actualWeekday: Weekday = sut.dayOfWeek;

      // EXPECTATIONS: the weekday Intl names for the same day
      const expectedName = new Intl.DateTimeFormat("en-US", { weekday: "long", timeZone: "UTC" }).format(Date.UTC(2026, 10, 5));

      // THEN
      assert.equal(Weekday[actualWeekday], expectedName);
    });

    /**
     * Verifies that CivilDate.isoWeek returns the ISO 8601 week number of the date.
     * Compared with: Date has no week numbers; code computes them by hand from `getUTCDay()` and the year's first
     * Thursday.
     */
    it("returns 45 when the date is 5 November 2026", () => {
      // SYSTEM UNDER TEST: 5 November 2026
      const sut = CivilDate.of(2026, 11, 5);

      // WHEN
      const actualWeek = sut.isoWeek;

      // EXPECTATIONS: week 45, the week from Monday 2 to Sunday 8 November
      const expectedWeek = 45;

      // THEN
      assert.equal(actualWeek, expectedWeek);
    });
    // END weekday-week
  });

  describe("isLeapYear", () => {
    const givenYears = [
      2024, // divisible by 4: leap
      2026, // not divisible by 4: common
      1900, // divisible by 100 but not by 400: common
      2000, // divisible by 400: leap
    ];
    for (const givenYear of givenYears) {
      /**
       * Verifies that CivilDate.isLeapYear follows the Gregorian leap-year rules for every kind of year.
       * Compared with: Date has no isLeapYear; the usual check, whether `Date.UTC(year, 1, 29)` stays in February,
       * gives the same answers.
       */
      it(`agrees with Date when the year is ${givenYear}`, () => {
        // SYSTEM UNDER TEST: 1 January of the given year
        const sut = CivilDate.of(givenYear, 1, 1);

        // WHEN
        const actualIsLeapYear = sut.isLeapYear;

        // EXPECTATIONS: Date's answer for the same year
        const expectedIsLeapYear = new Date(Date.UTC(givenYear, 1, 29)).getUTCMonth() === 1;

        // THEN
        assert.equal(actualIsLeapYear, expectedIsLeapYear);
      });
    }
  });

  describe("addDays", () => {
    // BEGIN add-days
    /**
     * Verifies that CivilDate.addDays returns the date that many days later, carrying into the next month.
     * Compared with: `setUTCDate(getUTCDate() + 30)` reaches the same date, by changing the Date in place.
     */
    it("returns the later date when the days cross a month end", () => {
      // GIVEN: 30 days, more than remain in November
      const givenDays = 30;

      // SYSTEM UNDER TEST: 5 November 2026
      const sut = CivilDate.of(2026, 11, 5);

      // WHEN
      const actualDate: CivilDate = sut.addDays(givenDays);

      // EXPECTATIONS: the date Date reaches with the same addition
      const tmpDate = new Date(Date.UTC(2026, 10, 5));
      tmpDate.setUTCDate(tmpDate.getUTCDate() + 30);
      const expectedDate = CivilDate.of(tmpDate.getUTCFullYear(), tmpDate.getUTCMonth() + 1, tmpDate.getUTCDate());

      // THEN
      assert.equal(String(actualDate), String(expectedDate));
    });
    // END add-days
  });

  describe("addMonths", () => {
    // BEGIN month-end
    /**
     * Verifies that CivilDate.addMonths returns none when the day is missing from the target month.
     * Compared with: `setUTCMonth(getUTCMonth() + 1)` on 31 January rolls over to 3 March without saying so.
     */
    it("returns none when the day is missing from the target month", () => {
      // GIVEN: one month, into a February that has no day 31
      const givenMonths = 1;

      // SYSTEM UNDER TEST: 31 January 2026
      const sut = CivilDate.of(2026, 1, 31);

      // WHEN
      const actualOutcome: Outcome<CivilDate> = sut.addMonths(givenMonths);

      // EXPECTATIONS: no such date, explained by the month-end diagnostic
      const expectedStatus = "none";
      const expectedCodes = ["month-end"];

      // THEN
      assert.equal(actualOutcome.status, expectedStatus);
      assert.deepEqual(actualOutcome.diagnostics.map((diagnostic) => diagnostic.code), expectedCodes);
    });

    /**
     * Verifies that CivilDate.addMonths with MonthEnd.Constrain returns the last day of the target month when the
     * day is missing from it.
     * Compared with: `setUTCMonth` gives 3 March instead; the by-hand clamp, day 0 of the month after, gives this date.
     */
    it("returns the last day of the month when Constrain is requested", () => {
      // GIVEN: one month, constrained to the end of the target month
      const givenMonths = 1;
      const givenPolicy = MonthEnd.Constrain;

      // SYSTEM UNDER TEST: 31 January 2026
      const sut = CivilDate.of(2026, 1, 31);

      // WHEN
      const actualDate: CivilDate = sut.addMonths(givenMonths, givenPolicy);

      // EXPECTATIONS: the last day of February, which Date finds as day 0 of March (month index 2)
      const tmpDate = new Date(Date.UTC(2026, 2, 0));
      const expectedDate = CivilDate.of(tmpDate.getUTCFullYear(), tmpDate.getUTCMonth() + 1, tmpDate.getUTCDate());

      // THEN
      assert.equal(String(actualDate), String(expectedDate));
    });
    // END month-end
  });

  describe("add", () => {
    /**
     * Verifies that CivilDate.add returns none when a one-month period lands on a missing day.
     * Compared with: JavaScript has no calendar period type; a month is whatever `setUTCMonth` makes of it.
     */
    it("returns none when a one-month period lands on a missing day", () => {
      // GIVEN: a calendar period of one month
      const givenPeriod = Period.months(1);

      // SYSTEM UNDER TEST: 31 January 2026
      const sut = CivilDate.of(2026, 1, 31);

      // WHEN
      const actualOutcome = sut.add(givenPeriod);

      // EXPECTATIONS: no such date under the default, rejecting month-end policy
      const expectedStatus = "none";
      const expectedCodes = ["month-end"];

      // THEN
      assert.equal(actualOutcome.status, expectedStatus);
      assert.deepEqual(actualOutcome.diagnostics.map((diagnostic) => diagnostic.code), expectedCodes);
    });
  });

  describe("daysUntil", () => {
    // BEGIN days-between
    /**
     * Verifies that CivilDate.daysUntil returns the number of days to a later date.
     * Compared with: the difference of the two time values at midnight UTC, divided by 86,400,000 ms, is the same;
     * local midnights would leave a fraction of a day whenever a DST change falls in between.
     */
    it("returns the day count when the other date is later", () => {
      // GIVEN: Christmas Day 2026
      const givenChristmas = CivilDate.of(2026, 12, 25);

      // SYSTEM UNDER TEST: 5 November 2026
      const sut = CivilDate.of(2026, 11, 5);

      // WHEN
      const actualDays: number = sut.daysUntil(givenChristmas);

      // EXPECTATIONS: the difference of Date's time values at midnight UTC, in days
      const expectedDays = (Date.UTC(2026, 11, 25) - Date.UTC(2026, 10, 5)) / 86_400_000;

      // THEN
      assert.equal(actualDays, expectedDays);
    });
    // END days-between
  });

  describe("isBefore", () => {
    // BEGIN compare-dates
    /**
     * Verifies that CivilDate.isBefore is true when this date comes first.
     * Compared with: `<` on two Dates compares their time values and gives the same answer; `===` would compare the
     * objects instead. Plain dates always have an order.
     */
    it("returns true when this date comes first", () => {
      // GIVEN: Christmas Day 2026
      const givenChristmas = CivilDate.of(2026, 12, 25);

      // SYSTEM UNDER TEST: 5 November 2026
      const sut = CivilDate.of(2026, 11, 5);

      // WHEN
      const actualComesFirst = sut.isBefore(givenChristmas);

      // EXPECTATIONS: Date's answer for the same two days
      const expectedComesFirst = new Date(Date.UTC(2026, 10, 5)) < new Date(Date.UTC(2026, 11, 25));

      // THEN
      assert.equal(actualComesFirst, expectedComesFirst);
    });
    // END compare-dates
  });
});
