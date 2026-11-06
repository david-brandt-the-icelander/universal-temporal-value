// What JavaScript's Date does, compared with UTV's CivilDate, CivilTime, CivilDateTime, FixedOffsetDateTime and
// Instant. Where the two agree, the expected value is computed with UTV, converted to a Date time value through
// Instant.toDate. The tests use the UTC methods (Date.UTC, getUTC…, setUTC…), so the host's time zone cannot change
// the answers; the local methods behave the same way in the host's zone. The one test about the host's zone says so.
import { describe, it } from "node:test";
import assert from "node:assert/strict";
import { CivilDate, CivilDateTime, CivilTime, FixedOffsetDateTime, Instant } from "../../../src/index.js";

describe("Date", () => {
  describe("UTC", () => {
    /**
     * Verifies that Date.UTC returns the time value of midnight UTC at the start of the day when the fields name a
     * real day.
     * Compared with: `CivilDate.of(2026, 11, 5)` names the same day, with months counted from 1 and no time at all.
     */
    it("returns midnight UTC of the day when the fields name a real day", () => {
      // GIVEN: 5 November 2026; months count from 0
      const givenYear = 2026;
      const givenMonthIndex = 10;
      const givenDay = 5;

      // WHEN
      const actualTime: number = Date.UTC(givenYear, givenMonthIndex, givenDay);

      // EXPECTATIONS: midnight UTC at the start of the day UTV names
      const tmpMidnight = CivilDate.of(2026, 11, 5).at(CivilTime.of(0, 0)).atOffset(0).toInstant();
      const expectedTime = tmpMidnight.toDate().single().getTime();

      // THEN
      assert.equal(actualTime, expectedTime);
    });

    /**
     * Verifies that Date.UTC rolls over into March when the day is 30 February.
     * Compared with: `CivilDate.tryOf(2026, 2, 30)` returns invalid (day-out-of-range), and `CivilDate.of` throws.
     */
    it("rolls over to 2 March when the day is 30 February", () => {
      // GIVEN: 30 February 2026
      const givenYear = 2026;
      const givenMonthIndex = 1;
      const givenDay = 30;

      // WHEN
      const actualTime = Date.UTC(givenYear, givenMonthIndex, givenDay);

      // EXPECTATIONS: two days past the end of February, without a word
      const expectedText = "2026-03-02T00:00:00.000Z";

      // THEN
      assert.equal(new Date(actualTime).toISOString(), expectedText);
    });

    /**
     * Verifies that Date.UTC rolls over into the next year when the month index is 12, a thirteenth month.
     * Compared with: `CivilDate.tryOf(2026, 13, 1)` returns invalid (month-out-of-range).
     */
    it("rolls over to January of the next year when the month index is 12", () => {
      // GIVEN: the first day of month index 12, one past December
      const givenYear = 2026;
      const givenMonthIndex = 12;
      const givenDay = 1;

      // WHEN
      const actualTime = Date.UTC(givenYear, givenMonthIndex, givenDay);

      // EXPECTATIONS: 1 January 2027
      const expectedText = "2027-01-01T00:00:00.000Z";

      // THEN
      assert.equal(new Date(actualTime).toISOString(), expectedText);
    });

    const givenYears = [
      2024, // divisible by 4: leap
      2026, // not divisible by 4: common
      1900, // divisible by 100 but not by 400: common
      2000, // divisible by 400: leap
    ];
    for (const givenYear of givenYears) {
      /**
       * Verifies that Date.UTC keeps 29 February in February exactly when the year is a leap year, the usual check in
       * the absence of an isLeapYear.
       * Compared with: `CivilDate.isLeapYear` gives the same answers.
       */
      it(`agrees with UTV on 29 February when the year is ${givenYear}`, () => {
        // WHEN
        const actualStaysInFebruary = new Date(Date.UTC(givenYear, 1, 29)).getUTCMonth() === 1;

        // EXPECTATIONS: UTV's answer for the same year
        const expectedIsLeapYear = CivilDate.of(givenYear, 1, 1).isLeapYear;

        // THEN
        assert.equal(actualStaysInFebruary, expectedIsLeapYear);
      });
    }

    /**
     * Verifies that Date.UTC returns the largest time value a Date can hold, 8.64e15, when the date is
     * 13 September 275760.
     * Compared with: `CivilDate.of(275760, 9, 13)` names the same day, and UTV's years go on past it.
     */
    it("returns the last time value a Date can hold when the date is 13 September 275760", () => {
      // GIVEN: 13 September 275760
      const givenYear = 275_760;
      const givenMonthIndex = 8;
      const givenDay = 13;

      // WHEN
      const actualTime = Date.UTC(givenYear, givenMonthIndex, givenDay);

      // EXPECTATIONS: midnight UTC at the start of the day UTV names, which UTV can still convert to a Date
      const tmpMidnight = CivilDate.of(275_760, 9, 13).at(CivilTime.of(0, 0)).atOffset(0).toInstant();
      const expectedTime = tmpMidnight.toDate().single().getTime();

      // THEN
      assert.equal(actualTime, expectedTime);
    });

    /**
     * Verifies that Date.UTC returns NaN when the moment is after the last one a Date can hold.
     * Compared with: `Instant.toDate` returns unsupported (out-of-native-range) for the same moment, and
     * `CivilDate.of(275760, 9, 14)` is an ordinary date.
     */
    it("returns NaN when the moment is after 13 September 275760", () => {
      // GIVEN: one millisecond after midnight UTC on 13 September 275760
      const givenYear = 275_760;
      const givenMonthIndex = 8;
      const givenDay = 13;
      const givenMillisecond = 1;

      // WHEN
      const actualTime = Date.UTC(givenYear, givenMonthIndex, givenDay, 0, 0, 0, givenMillisecond);

      // EXPECTATIONS: not a number, the time value of an Invalid Date
      const expectedTime = Number.NaN;

      // THEN
      assert.equal(actualTime, expectedTime);
    });

    /**
     * Verifies that Date.UTC returns the day in the proleptic Gregorian calendar when the year is negative.
     * Compared with: `CivilDate.of(-500, 3, 1)` names the same day.
     */
    it("returns the day when the year is negative", () => {
      // GIVEN: 1 March of year -500
      const givenYear = -500;
      const givenMonthIndex = 2;
      const givenDay = 1;

      // WHEN
      const actualTime = Date.UTC(givenYear, givenMonthIndex, givenDay);

      // EXPECTATIONS: midnight UTC at the start of the day UTV names
      const tmpMidnight = CivilDate.of(-500, 3, 1).at(CivilTime.of(0, 0)).atOffset(0).toInstant();
      const expectedTime = tmpMidnight.toDate().single().getTime();

      // THEN
      assert.equal(actualTime, expectedTime);
    });

    /**
     * Verifies that Date.UTC rolls a leap second over into the next minute, here of the next year, when the second
     * is 60.
     * Compared with: `CivilDateTime.tryOf(2016, 12, 31, 23, 59, 60)` returns unsupported (leap-second): the second
     * may well exist, and judging it needs a leap-second table.
     */
    it("rolls over into the next year when the second is a leap second", () => {
      // GIVEN: 23:59:60 on 31 December 2016, a real leap second
      const givenSecond = 60;

      // WHEN
      const actualTime = Date.UTC(2016, 11, 31, 23, 59, givenSecond);

      // EXPECTATIONS: midnight on 1 January 2017, as if the second could never exist
      const expectedText = "2017-01-01T00:00:00.000Z";

      // THEN
      assert.equal(new Date(actualTime).toISOString(), expectedText);
    });
  });

  describe("parse", () => {
    /**
     * Verifies that Date.parse reads midnight UTC when the text is an ISO 8601 calendar date.
     * Compared with: `CivilDate.parse("2026-11-05")` reads the same day, and no time or zone with it.
     */
    it("reads midnight UTC when the text is an ISO date", () => {
      // GIVEN: the ISO 8601 text of 5 November 2026
      const givenText = "2026-11-05";

      // WHEN
      const actualTime = Date.parse(givenText);

      // EXPECTATIONS: midnight UTC at the start of the day UTV parses from the same text
      const tmpMidnight = CivilDate.parse("2026-11-05").single().at(CivilTime.of(0, 0)).atOffset(0).toInstant();
      const expectedTime = tmpMidnight.toDate().single().getTime();

      // THEN
      assert.equal(actualTime, expectedTime);
    });

    /**
     * Verifies that Date.parse in Node rolls over into March, with no reason given, when the text names 30 February.
     * Compared with: `CivilDate.parse("2026-02-30")` returns invalid (day-out-of-range).
     */
    it("rolls over to 2 March when the text names 30 February", () => {
      // GIVEN: well-formed text for 30 February 2026
      const givenText = "2026-02-30";

      // WHEN
      const actualTime = Date.parse(givenText);

      // EXPECTATIONS: 2 March, as if the text had said so
      const expectedText = "2026-03-02T00:00:00.000Z";

      // THEN
      assert.equal(new Date(actualTime).toISOString(), expectedText);
    });

    /**
     * Verifies that Date.parse gives the same time value for text ending in Z and text ending in +00:00.
     * Compared with: `parse` returns an `Instant` for Z and a `FixedOffsetDateTime` for +00:00; the two texts mean
     * different things.
     */
    it("cannot tell Z from +00:00 when both name the same moment", () => {
      // GIVEN: 14:30 UTC, written with Z
      const givenText = "2026-11-05T14:30:00Z";

      // WHEN
      const actualTime = Date.parse(givenText);

      // EXPECTATIONS: the moment of the +00:00 text, which UTV keeps apart as a FixedOffsetDateTime
      const tmpWallTime = FixedOffsetDateTime.parse("2026-11-05T14:30:00+00:00").single();
      const expectedTime = tmpWallTime.toInstant().toDate().single().getTime();

      // THEN
      assert.equal(actualTime, expectedTime);
    });

    /**
     * Verifies that Date.parse keeps only the instant when the text has an offset.
     * Compared with: `CivilDateTime.of(2026, 11, 5, 14, 30).atOffset(3600)` is the same moment, and keeps the wall
     * time 14:30 and the offset +01:00 as well.
     */
    it("keeps only the instant when the text has an offset", () => {
      // GIVEN: lunch at 14:30 on 5 November 2026, one hour east of UTC
      const givenText = "2026-11-05T14:30:00+01:00";

      // WHEN
      const actualTime = Date.parse(givenText);

      // EXPECTATIONS: the moment UTV computes for the same wall time and offset
      const tmpLunch = CivilDateTime.of(2026, 11, 5, 14, 30).atOffset(3600);
      const expectedTime = tmpLunch.toInstant().toDate().single().getTime();

      // THEN
      assert.equal(actualTime, expectedTime);
    });
  });

  describe("constructor", () => {
    /**
     * Verifies that the Date constructor returns the Unix epoch when given zero milliseconds.
     * Compared with: `Instant.fromUnixSeconds(0)` is the same moment.
     */
    it("returns the epoch when given zero", () => {
      // GIVEN: zero milliseconds since 1970-01-01T00:00:00Z
      const givenMilliseconds = 0;

      // WHEN
      const actualEpoch = new Date(givenMilliseconds);

      // EXPECTATIONS: the moment UTV returns for the same count
      const expectedTime = Instant.fromUnixSeconds(0).toDate().single().getTime();

      // THEN
      assert.equal(actualEpoch.getTime(), expectedTime);
    });

    /**
     * Verifies that the Date constructor drops the digits below a millisecond when given a fractional time value.
     * Compared with: `Instant.fromUnixSeconds(1_793_795_400, 123_456_789)` keeps all nine digits, and its `toDate()`
     * says what it drops (precision-loss).
     */
    it("keeps three digits when given a time to the nanosecond", () => {
      // GIVEN: 12:30:00.123456789 UTC on 4 November 2026, in milliseconds
      const givenMilliseconds = 1_793_795_400_123.456789;

      // WHEN
      const actualMoment = new Date(givenMilliseconds);

      // EXPECTATIONS: the Date UTV converts the nanosecond instant to, once the last six digits are dropped
      const expectedTime = Instant.fromUnixSeconds(1_793_795_400, 123_456_789).toDate().single().getTime();

      // THEN
      assert.equal(actualMoment.getTime(), expectedTime);
    });

    /**
     * Verifies that the Date constructor reads the fields in the host's time zone when given local fields.
     * Compared with: UTV refuses to guess (`fromDate(date, "local")` is unsupported, local-zone); the instant UTV
     * computes for the same wall time at the host's offset, named explicitly, is the same.
     *
     * This test depends on the host's time zone on purpose. The expected value uses the host's own offset for that
     * moment, so the assertion holds on every host; only the time value differs from machine to machine.
     */
    it("reads the fields in the host's time zone when given local fields", () => {
      // GIVEN: 14:30 on 5 November 2026; months count from 0
      const givenYear = 2026;
      const givenMonthIndex = 10;
      const givenDay = 5;
      const givenHour = 14;
      const givenMinute = 30;

      // WHEN
      const actualDate = new Date(givenYear, givenMonthIndex, givenDay, givenHour, givenMinute);

      // EXPECTATIONS: the same wall time at the host's offset (getTimezoneOffset counts minutes west of UTC)
      const tmpHostOffsetSeconds = -actualDate.getTimezoneOffset() * 60;
      const tmpLocal = CivilDateTime.of(2026, 11, 5, 14, 30).atOffset(tmpHostOffsetSeconds);
      const expectedTime = tmpLocal.toInstant().toDate().single().getTime();

      // THEN
      assert.equal(actualDate.getTime(), expectedTime);
    });
  });

  describe("now", () => {
    /**
     * Verifies that Date.now returns a reading that UTV takes as an input without changing it.
     * Compared with: UTV never reads the clock; `Instant.fromUnixMilliseconds(Date.now())` is how the reading gets in.
     *
     * This test reads the real clock on purpose: the round trip does not depend on the time read, so the result is the
     * same on every run. If it ever flakes, that is a finding about the conversion, not noise.
     */
    it("survives the trip through UTV when read at any time", () => {
      // WHEN
      const actualReading = Date.now();

      // EXPECTATIONS: the same reading after UTV has held it as an Instant
      const expectedReading = Instant.fromUnixMilliseconds(actualReading).toDate().single().getTime();

      // THEN
      assert.equal(actualReading, expectedReading);
    });
  });

  describe("getUTCDay", () => {
    /**
     * Verifies that Date.getUTCDay numbers Sunday 0 when the date is a Sunday.
     * Compared with: `CivilDate.dayOfWeek` follows ISO 8601, Monday 1 to Sunday 7, so it is `Weekday.Sunday`, 7.
     */
    it("numbers Sunday 0 when the date is a Sunday", () => {
      // SYSTEM UNDER TEST: Sunday 8 November 2026
      const sut = new Date(Date.UTC(2026, 10, 8));

      // WHEN
      const actualDay = sut.getUTCDay();

      // EXPECTATIONS: 0, where UTV numbers the same day 7
      const expectedDay = 0;

      // THEN
      assert.equal(actualDay, expectedDay);
    });
  });

  describe("setUTCDate", () => {
    /**
     * Verifies that Date.setUTCDate returns the date that many days later, carrying into the next month.
     * Compared with: `CivilDate.addDays` gives the same date, as a new value instead of changing the old one.
     */
    it("returns the later date when the days cross a month end", () => {
      // GIVEN: 30 days, more than remain in November
      const givenDays = 30;

      // SYSTEM UNDER TEST: 5 November 2026
      const sut = new Date(Date.UTC(2026, 10, 5));

      // WHEN
      const actualTime = sut.setUTCDate(sut.getUTCDate() + givenDays);

      // EXPECTATIONS: midnight UTC at the start of the day UTV computes for the same addition
      const tmpMidnight = CivilDate.of(2026, 11, 5).addDays(30).at(CivilTime.of(0, 0)).atOffset(0).toInstant();
      const expectedTime = tmpMidnight.toDate().single().getTime();

      // THEN
      assert.equal(actualTime, expectedTime);
    });
  });

  describe("setUTCMonth", () => {
    /**
     * Verifies that Date.setUTCMonth rolls over into the month after when the day is missing from the target month.
     * Compared with: `CivilDate.addMonths(1)` returns none (month-end), and gives 28 February only when asked with
     * `MonthEnd.Constrain`.
     */
    it("rolls over to 3 March when the day is missing from the target month", () => {
      // GIVEN: one month, into a February that has no day 31
      const givenMonths = 1;

      // SYSTEM UNDER TEST: 31 January 2026
      const sut = new Date(Date.UTC(2026, 0, 31));

      // WHEN
      const actualTime = sut.setUTCMonth(sut.getUTCMonth() + givenMonths);

      // EXPECTATIONS: 31 February, which is 3 March, without a word
      const expectedText = "2026-03-03T00:00:00.000Z";

      // THEN
      assert.equal(new Date(actualTime).toISOString(), expectedText);
    });
  });

  describe("setUTCHours", () => {
    /**
     * Verifies that Date.setUTCHours carries into the next day when the hours pass midnight.
     * Compared with: `CivilDateTime.addHours` gives the same wall time.
     */
    it("carries into the next day when the hours pass midnight", () => {
      // GIVEN: two hours, past midnight
      const givenHours = 2;

      // SYSTEM UNDER TEST: 23:30 UTC on 5 November 2026
      const sut = new Date(Date.UTC(2026, 10, 5, 23, 30));

      // WHEN
      const actualTime = sut.setUTCHours(sut.getUTCHours() + givenHours);

      // EXPECTATIONS: the wall time UTV computes for the same addition, read at UTC
      const tmpLater = CivilDateTime.of(2026, 11, 5, 23, 30).addHours(2).atOffset(0).toInstant();
      const expectedTime = tmpLater.toDate().single().getTime();

      // THEN
      assert.equal(actualTime, expectedTime);
    });
  });

  describe("getTime", () => {
    /**
     * Verifies that the difference of two Date.getTime values, divided by 86,400,000, is the number of days between
     * the dates when both are midnight UTC.
     * Compared with: `CivilDate.daysUntil` returns the same count directly, with no milliseconds to divide.
     */
    it("differs by the day count when subtracted", () => {
      // GIVEN: Christmas Day 2026
      const givenChristmas = new Date(Date.UTC(2026, 11, 25));

      // SYSTEM UNDER TEST: 5 November 2026
      const sut = new Date(Date.UTC(2026, 10, 5));

      // WHEN
      const actualDays = (givenChristmas.getTime() - sut.getTime()) / 86_400_000;

      // EXPECTATIONS: the count UTV computes
      const expectedDays = CivilDate.of(2026, 11, 5).daysUntil(CivilDate.of(2026, 12, 25));

      // THEN
      assert.equal(actualDays, expectedDays);
    });

    /**
     * Verifies that subtracting two Date.getTime values gives whole milliseconds when the moments are a fraction of a
     * second apart.
     * Compared with: `Instant.until` returns the exact fraction 14403/4 seconds.
     */
    it("returns milliseconds when the moments are a fraction of a second apart", () => {
      // GIVEN: one hour and 0.75 seconds later
      const givenLater = new Date(1_793_889_001_000);

      // SYSTEM UNDER TEST: 13:30:00.25 on 5 November 2026
      const sut = new Date(1_793_885_400_250);

      // WHEN
      const actualMilliseconds = givenLater.getTime() - sut.getTime();

      // EXPECTATIONS: UTV's exact answer, in milliseconds
      const tmpExact = Instant.fromUnixSeconds(1_793_885_400, 250_000_000).until(Instant.fromUnixSeconds(1_793_889_001));
      const expectedMilliseconds = (Number(tmpExact.numerator) * 1000) / Number(tmpExact.denominator);

      // THEN
      assert.equal(actualMilliseconds, expectedMilliseconds);
    });

    /**
     * Verifies that Date.getTime is the same when the Date made a round trip through UTV.
     * Compared with: `Instant.fromDate` and `Instant.toDate` lose nothing a Date can hold.
     */
    it("returns the same time value when the Date made a round trip through UTV", () => {
      // SYSTEM UNDER TEST: 13:30:00.25 UTC on 5 November 2026
      const sut = new Date(Date.UTC(2026, 10, 5, 13, 30, 0, 250));

      // WHEN
      const actualTime = sut.getTime();

      // EXPECTATIONS: the time value of the same Date after a round trip through UTV's Instant
      const tmpRoundTrip = Instant.fromDate(new Date(Date.UTC(2026, 10, 5, 13, 30, 0, 250))).toDate().single();
      const expectedTime = tmpRoundTrip.getTime();

      // THEN
      assert.equal(actualTime, expectedTime);
    });
  });

  describe("valueOf", () => {
    /**
     * Verifies that `<` on two Dates, which compares their valueOf time values, is true when the left date comes
     * first.
     * Compared with: `CivilDate.isBefore` gives the same answer.
     */
    it("orders the dates when compared with <", () => {
      // GIVEN: Christmas Day 2026
      const givenChristmas = new Date(Date.UTC(2026, 11, 25));

      // SYSTEM UNDER TEST: 5 November 2026
      const sut = new Date(Date.UTC(2026, 10, 5));

      // WHEN
      const actualComesFirst = sut < givenChristmas;

      // EXPECTATIONS: UTV's answer for the same two days
      const expectedComesFirst = CivilDate.of(2026, 11, 5).isBefore(CivilDate.of(2026, 12, 25));

      // THEN
      assert.equal(actualComesFirst, expectedComesFirst);
    });
  });

  describe("toISOString", () => {
    /**
     * Verifies that Date.toISOString writes a date, seconds and milliseconds when the Date stands in for a time of day.
     * Compared with: `CivilTime.of(9, 30)` has no date, remembers that no seconds were given, and writes "09:30".
     */
    it("writes a date, seconds and milliseconds when the Date stands in for a time of day", () => {
      // SYSTEM UNDER TEST: half past nine, on the made-up day 1 January 1970
      const sut = new Date(Date.UTC(1970, 0, 1, 9, 30));

      // WHEN
      const actualText = sut.toISOString();

      // EXPECTATIONS: UTV's text for the same time, padded with a date, zero seconds and zero milliseconds
      const expectedText = `1970-01-01T${CivilTime.of(9, 30)}:00.000Z`;

      // THEN
      assert.equal(actualText, expectedText);
    });
  });
});
