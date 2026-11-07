// How to use Interval, a range of dates or instants whose ends say whether they are included, unbounded or unknown,
// compared with JavaScript, which has no range type: bounds are compared by hand on Dates, and a missing end is null
// or undefined, whatever the reason. The Dates are built with Date.UTC, so the host's time zone cannot change them.
import { describe, it } from "node:test";
import assert from "node:assert/strict";
import { Bound, CivilDate, Interval, Truth } from "../../../src/index.js";

describe("Interval", () => {
  describe("halfOpen", () => {
    // BEGIN ranges
    /**
     * Verifies that a half-open Interval definitely contains a day between its ends.
     * Compared with: `day >= first && day < end` on Dates says the same.
     */
    it("definitely contains the day when the day is between the ends", () => {
      // GIVEN: 15 November 2026
      const givenDay = CivilDate.of(2026, 11, 15);

      // SYSTEM UNDER TEST: November 2026, from the 1st up to but not including 1 December
      const sut = Interval.halfOpen(CivilDate.of(2026, 11, 1), CivilDate.of(2026, 12, 1));

      // WHEN
      const actualContains: Truth = sut.contains(givenDay);

      // EXPECTATIONS: the answer the hand-written bounds give
      const tmpDay = new Date(Date.UTC(2026, 10, 15));
      const tmpNativeContains = tmpDay >= new Date(Date.UTC(2026, 10, 1)) && tmpDay < new Date(Date.UTC(2026, 11, 1));
      const expectedContains = tmpNativeContains ? Truth.Definitely : Truth.Impossible;

      // THEN
      assert.equal(actualContains, expectedContains);
    });

    /**
     * Verifies that a half-open Interval cannot contain its excluded end.
     * Compared with: the hand-written `day < end` excludes it too, if nobody writes `<=` by mistake.
     */
    it("cannot contain the day when the day is the excluded end", () => {
      // GIVEN: 1 December 2026, the end of the interval
      const givenDay = CivilDate.of(2026, 12, 1);

      // SYSTEM UNDER TEST: November 2026, from the 1st up to but not including 1 December
      const sut = Interval.halfOpen(CivilDate.of(2026, 11, 1), CivilDate.of(2026, 12, 1));

      // WHEN
      const actualContains = sut.contains(givenDay);

      // EXPECTATIONS: impossible, and written with the end's bracket open
      const expectedContains = Truth.Impossible;
      const expectedText = "[2026-11-01, 2026-12-01)";

      // THEN
      assert.equal(actualContains, expectedContains);
      assert.equal(sut.toString(), expectedText);
    });
    // END ranges
  });

  describe("closed", () => {
    /**
     * Verifies that a closed Interval definitely contains its last day.
     * Compared with: `day >= first && day <= last` on Dates says the same.
     */
    it("definitely contains the day when the day is the included end", () => {
      // GIVEN: 30 November 2026, the last day of the interval
      const givenDay = CivilDate.of(2026, 11, 30);

      // SYSTEM UNDER TEST: November 2026, from the 1st to the 30th, both included
      const sut = Interval.closed(CivilDate.of(2026, 11, 1), CivilDate.of(2026, 11, 30));

      // WHEN
      const actualContains = sut.contains(givenDay);

      // EXPECTATIONS: the answer the hand-written bounds give
      const tmpDay = new Date(Date.UTC(2026, 10, 30));
      const tmpNativeContains = tmpDay >= new Date(Date.UTC(2026, 10, 1)) && tmpDay <= new Date(Date.UTC(2026, 10, 30));
      const expectedContains = tmpNativeContains ? Truth.Definitely : Truth.Impossible;

      // THEN
      assert.equal(actualContains, expectedContains);
    });
  });

  describe("of", () => {
    /**
     * Verifies that an Interval with an unbounded end definitely contains any later day.
     * Compared with: an end of `null` means "no end" here, by convention only.
     */
    it("definitely contains a later day when the end is unbounded", () => {
      // GIVEN: 1 January 3000
      const givenDay = CivilDate.of(3000, 1, 1);

      // SYSTEM UNDER TEST: from 1 November 2026 on, with no end at all
      const sut = Interval.of(Bound.included(CivilDate.of(2026, 11, 1)), Bound.unbounded);

      // WHEN
      const actualContains = sut.contains(givenDay);

      // EXPECTATIONS: definitely, and written with ".." for the missing end
      const expectedContains = Truth.Definitely;
      const expectedText = "[2026-11-01, ..)";

      // THEN
      assert.equal(actualContains, expectedContains);
      assert.equal(sut.toString(), expectedText);
    });

    // BEGIN ranges
    /**
     * Verifies that an Interval with an unknown end only possibly contains a later day.
     * Compared with: the same `null` end would be read as "no end", and the answer would wrongly be `true`.
     */
    it("possibly contains a later day when the end is unknown", () => {
      // GIVEN: 1 June 2027
      const givenDay = CivilDate.of(2027, 6, 1);

      // SYSTEM UNDER TEST: from 1 November 2026, until an end nobody knows yet
      const sut = Interval.of(Bound.included(CivilDate.of(2026, 11, 1)), Bound.unknown);

      // WHEN
      const actualContains = sut.contains(givenDay);

      // EXPECTATIONS: possibly, and written with "?" for the unknown end
      const expectedContains = Truth.Possibly;
      const expectedText = "[2026-11-01, ?)";

      // THEN
      assert.equal(actualContains, expectedContains);
      assert.equal(sut.toString(), expectedText);
    });
    // END ranges
  });
});
