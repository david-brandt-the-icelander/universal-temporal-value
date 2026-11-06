// How to use Instant, an exact moment on the POSIX timescale with no calendar and no zone, compared with JavaScript's
// Date, which holds a whole number of milliseconds since 1970 between ±8.64e15. The zone tests compare with
// Intl.DateTimeFormat, which reads the host's tz database; UTV reads only the pinned demo rules.
import { describe, it } from "node:test";
import assert from "node:assert/strict";
import {
  CivilDateTime, ElapsedSeconds, Instant, PosixSeconds, TemporalContext, type Outcome, type ZonedIntent,
} from "../../../src/index.js";
import { demoZoneRules } from "../fixtures.js";

describe("Instant", () => {
  describe("fromUnixMilliseconds", () => {
    // BEGIN now
    /**
     * Verifies that Instant.fromUnixMilliseconds takes a clock reading as an input when the program reads the clock.
     * Compared with: `Date.now()` reads the clock itself; UTV never does, so tests can fix "now".
     */
    it("returns the same moment when given a clock reading", () => {
      // GIVEN: a clock reading, as Date.now() would return it
      const givenReading = 1_793_885_400_123;

      // WHEN
      const actualNow: Instant = Instant.fromUnixMilliseconds(givenReading);

      // EXPECTATIONS: the same moment, as Date writes it
      const expectedText = new Date(1_793_885_400_123).toISOString();

      // THEN
      assert.equal(actualNow.toString(), expectedText);
    });
    // END now
  });

  describe("fromUnixSeconds", () => {
    /**
     * Verifies that Instant.fromUnixSeconds returns the Unix epoch when given zero seconds.
     * Compared with: `new Date(0)` is the same moment, written `1970-01-01T00:00:00.000Z`.
     */
    it("returns the epoch when given zero", () => {
      // GIVEN: zero seconds since 1970-01-01T00:00:00Z
      const givenSeconds = 0;

      // WHEN
      const actualInstant = Instant.fromUnixSeconds(givenSeconds);

      // EXPECTATIONS: the moment Date holds for the same count, written without empty milliseconds
      const expectedInstant = Instant.fromDate(new Date(0));
      const expectedText = "1970-01-01T00:00:00Z";

      // THEN
      assert.equal(String(actualInstant), String(expectedInstant));
      assert.equal(actualInstant.toString(), expectedText);
    });

    /**
     * Verifies that Instant.fromUnixSeconds keeps all nine fractional digits when given nanoseconds.
     * Compared with: a Date holds whole milliseconds, so it keeps only three of them.
     */
    it("keeps every nanosecond when given nanoseconds", () => {
      // GIVEN: a moment on 4 November 2026, to the nanosecond
      const givenSeconds = 1_793_795_400;
      const givenNanoseconds = 123_456_789;

      // WHEN
      const actualInstant = Instant.fromUnixSeconds(givenSeconds, givenNanoseconds);

      // EXPECTATIONS: all nine digits, where Date's text stops at three
      const expectedText = "2026-11-04T12:30:00.123456789Z";
      const expectedNanoseconds = 123_456_789;

      // THEN
      assert.equal(actualInstant.toString(), expectedText);
      assert.equal(actualInstant.nanoseconds, expectedNanoseconds);
    });

    /**
     * Verifies that Instant.fromUnixSeconds keeps a fraction of a second when given one.
     * Compared with: a Date holds the quarter second as 250 ms, but `Math.floor(date.getTime() / 1000)`, the usual
     * Unix seconds, drops it.
     */
    it("keeps the fraction when given a quarter second", () => {
      // GIVEN: 13:30:00.25 on 5 November 2026
      const givenSeconds = 1_793_885_400;
      const givenNanoseconds = 250_000_000;

      // WHEN
      const actualInstant = Instant.fromUnixSeconds(givenSeconds, givenNanoseconds);

      // EXPECTATIONS: the moment Date holds, with the quarter second written out
      const expectedInstant = Instant.fromDate(new Date(1_793_885_400_250));
      const expectedText = "2026-11-05T13:30:00.25Z";

      // THEN
      assert.equal(String(actualInstant), String(expectedInstant));
      assert.equal(actualInstant.toString(), expectedText);
    });
  });

  describe("until", () => {
    // BEGIN timestamps
    /**
     * Verifies that Instant.until returns the exact number of seconds, as a fraction, between two instants.
     * Compared with: subtracting two Dates gives milliseconds as a number; seconds are a floating-point division.
     */
    it("returns an exact fraction when the instants are a fraction of a second apart", () => {
      // GIVEN: one hour and 0.75 seconds later
      const givenLater = Instant.fromUnixSeconds(1_793_889_001);

      // SYSTEM UNDER TEST: 13:30:00.25 on 5 November 2026
      const sut = Instant.fromUnixSeconds(1_793_885_400, 250_000_000);

      // WHEN
      const actualSeconds: PosixSeconds = sut.until(givenLater);

      // EXPECTATIONS: exactly 14403/4 seconds, the number Date approximates as a float
      const expectedSeconds = PosixSeconds.of(14_403, 4);
      const expectedApproximation = (new Date(1_793_889_001_000).getTime() - new Date(1_793_885_400_250).getTime()) / 1000;

      // THEN
      assert.equal(String(actualSeconds), String(expectedSeconds));
      assert.equal(Number(actualSeconds.numerator) / Number(actualSeconds.denominator), expectedApproximation);
    });
    // END timestamps

    /**
     * Verifies that Instant.until returns 23 hours when the instants are noon on both sides of a spring-forward night.
     * Compared with: subtracting the two Dates gives the same 82,800,000 ms; the calendar day is 23 hours long.
     */
    it("returns 23 hours when the day between loses an hour to DST", () => {
      // GIVEN: noon on 8 March 2026 in New York, daylight time
      const givenNextNoon = CivilDateTime.of(2026, 3, 8, 12, 0).atOffset(-4 * 3600).toInstant();

      // SYSTEM UNDER TEST: noon on 7 March 2026 in New York, standard time
      const sut = CivilDateTime.of(2026, 3, 7, 12, 0).atOffset(-5 * 3600).toInstant();

      // WHEN
      const actualSeconds = sut.until(givenNextNoon);

      // EXPECTATIONS: 82,800 seconds, as Date's milliseconds count them
      const tmpMilliseconds = Date.parse("2026-03-08T12:00:00-04:00") - Date.parse("2026-03-07T12:00:00-05:00");
      const expectedSeconds = PosixSeconds.of(tmpMilliseconds / 1000);

      // THEN
      assert.equal(String(actualSeconds), String(expectedSeconds));
    });
  });

  describe("add", () => {
    /**
     * Verifies that Instant.add with POSIX seconds moves the instant by exactly that many seconds.
     * Compared with: adding 86,400,000 ms to the Date lands on the same moment: 13:00 in New York, not noon.
     */
    it("moves by exactly those seconds when given POSIX seconds", () => {
      // GIVEN: 86,400 POSIX seconds, one nominal day
      const givenSeconds = PosixSeconds.of(86_400);

      // SYSTEM UNDER TEST: noon on 7 March 2026 in New York, the day before clocks spring forward
      const sut = CivilDateTime.of(2026, 3, 7, 12, 0).atOffset(-5 * 3600).toInstant();

      // WHEN
      const actualInstant: Instant = sut.add(givenSeconds);

      // EXPECTATIONS: the moment Date reaches with the same addition
      const expectedInstant = Instant.fromDate(new Date(Date.parse("2026-03-07T12:00:00-05:00") + 86_400_000));

      // THEN
      assert.equal(String(actualInstant), String(expectedInstant));
    });

    // BEGIN day-vs-24h
    /**
     * Verifies that Instant.add with SI seconds returns unsupported when no leap-second table is available.
     * Compared with: JavaScript has one kind of millisecond, so it cannot tell SI seconds from POSIX seconds.
     */
    it("returns unsupported when given SI seconds without a leap-second table", () => {
      // GIVEN: 86,400 SI seconds, as a physicist measures a day
      const givenSeconds = ElapsedSeconds.of(86_400);

      // SYSTEM UNDER TEST: noon on 7 March 2026 in New York
      const sut = CivilDateTime.of(2026, 3, 7, 12, 0).atOffset(-5 * 3600).toInstant();

      // WHEN
      const actualOutcome: Outcome<Instant> = sut.add(givenSeconds);

      // EXPECTATIONS: unsupported, because SI seconds on a POSIX timescale need leap seconds
      const expectedStatus = "unsupported";
      const expectedCodes = ["needs-leap-seconds"];

      // THEN
      assert.equal(actualOutcome.status, expectedStatus);
      assert.deepEqual(actualOutcome.diagnostics.map((diagnostic) => diagnostic.code), expectedCodes);
    });
    // END day-vs-24h
  });

  describe("inZone", () => {
    // BEGIN meeting-in-paris
    /**
     * Verifies that Instant.inZone returns the wall time and offset in the zone when the zone has rules for it.
     * Compared with: `Intl.DateTimeFormat` with `timeZone: "America/New_York"` shows the same wall time, from the
     * host's tz database, as text to be taken apart with `formatToParts`.
     */
    it("returns the local wall time when the zone has rules", () => {
      // GIVEN: New York
      const givenZoneId = "America/New_York";

      // SETUP: the pinned demo zone rules, never the host's tz database
      const envContext = TemporalContext.with(demoZoneRules());

      // SYSTEM UNDER TEST: 07:00 UTC on 1 June 2027, when the meeting starts in Paris
      const sut = Instant.fromUnixSeconds(1_811_833_200);

      // WHEN
      const actualOutcome: Outcome<ZonedIntent> = sut.inZone(givenZoneId, envContext);

      // EXPECTATIONS: the wall time Intl shows in New York for the same moment, and the offset UTV asserts with it
      const tmpParts = new Intl.DateTimeFormat("en-US", {
        timeZone: "America/New_York", hourCycle: "h23",
        year: "numeric", month: "numeric", day: "numeric", hour: "numeric", minute: "numeric", second: "numeric",
      }).formatToParts(Date.UTC(2027, 5, 1, 7, 0));
      const tmpFields = Object.fromEntries(tmpParts.map((part) => [part.type, part.value]));
      const expectedWallTime = CivilDateTime.of(
        Number(tmpFields["year"]), Number(tmpFields["month"]), Number(tmpFields["day"]),
        Number(tmpFields["hour"]), Number(tmpFields["minute"]), Number(tmpFields["second"]));
      const expectedText = "2027-06-01T03:00:00-04:00[America/New_York]";

      // THEN
      assert.equal(String(actualOutcome.single().dateTime), String(expectedWallTime));
      assert.equal(actualOutcome.single().toString(), expectedText);
    });
    // END meeting-in-paris
  });

  describe("toDate", () => {
    // BEGIN round-trip
    /**
     * Verifies that Instant.toDate gives back exactly the Date the instant was made from.
     * Compared with: `getTime()` of the Date that comes back equals the original's, to the millisecond.
     */
    it("returns the original when the instant came from a Date", () => {
      // GIVEN: a Date for 13:30:00.25 UTC on 5 November 2026
      const givenDate = new Date(Date.UTC(2026, 10, 5, 13, 30, 0, 250));

      // SYSTEM UNDER TEST: the same moment as a UTV Instant
      const sut = Instant.fromDate(givenDate);

      // WHEN
      const actualOutcome: Outcome<Date> = sut.toDate();

      // EXPECTATIONS: the very same time value, with nothing lost
      const expectedTime = Date.UTC(2026, 10, 5, 13, 30, 0, 250);
      const expectedCodes: string[] = [];

      // THEN
      assert.equal(actualOutcome.single().getTime(), expectedTime);
      assert.deepEqual(actualOutcome.diagnostics.map((diagnostic) => diagnostic.code), expectedCodes);
    });
    // END round-trip

    /**
     * Verifies that Instant.toDate warns about precision loss when the instant has digits below a millisecond.
     * Compared with: `new Date(1_793_885_400_123.456789)` drops the same digits, silently.
     */
    it("returns the Date with a precision-loss warning when the instant has nanoseconds", () => {
      // SYSTEM UNDER TEST: 13:30:00.123456789 UTC on 5 November 2026
      const sut = Instant.fromUnixNanoseconds(1_793_885_400_123_456_789n);

      // WHEN
      const actualOutcome = sut.toDate();

      // EXPECTATIONS: the whole milliseconds Date keeps of the same moment, and a warning that the rest is gone
      const expectedTime = new Date(1_793_885_400_123.456789).getTime();
      const expectedCodes = ["precision-loss"];

      // THEN
      assert.equal(actualOutcome.single().getTime(), expectedTime);
      assert.deepEqual(actualOutcome.diagnostics.map((diagnostic) => diagnostic.code), expectedCodes);
    });

    /**
     * Verifies that Instant.toDate returns unsupported when the instant is after the last moment a Date can hold.
     * Compared with: `new Date(8.64e15 + 1)` is an Invalid Date, whose `getTime()` is NaN.
     */
    it("returns unsupported when the instant is beyond the range of Date", () => {
      // SYSTEM UNDER TEST: one millisecond after 275760-09-13T00:00:00Z
      const sut = Instant.fromUnixMilliseconds(8_640_000_000_000_001);

      // WHEN
      const actualOutcome = sut.toDate();

      // EXPECTATIONS: unsupported, because the native type cannot hold the value
      const expectedStatus = "unsupported";
      const expectedCodes = ["out-of-native-range"];

      // THEN
      assert.equal(actualOutcome.status, expectedStatus);
      assert.deepEqual(actualOutcome.diagnostics.map((diagnostic) => diagnostic.code), expectedCodes);
    });
  });
});
