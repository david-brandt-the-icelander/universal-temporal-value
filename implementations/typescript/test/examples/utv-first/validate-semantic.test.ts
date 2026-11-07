// How to use validateSemantic, the JSON layer's check that a civil value means something in the proleptic Gregorian
// calendar (after the structural check), compared with JavaScript, where a Date is always some valid moment because
// impossible fields roll over, and other calendars exist only as display formats of Intl.DateTimeFormat.
import { describe, it } from "node:test";
import assert from "node:assert/strict";
import { validateSemantic, type UtvResult } from "../../../src/index.js";

describe("validateSemantic", () => {
  /**
   * Verifies that validateSemantic returns valid when the JSON names a real Gregorian date.
   * Compared with: `Date.UTC(2026, 10, 5)` names the same day, as midnight UTC.
   */
  it("returns valid when the date exists", () => {
    // GIVEN: 5 November 2026 as UTV JSON: fields in a named calendar, with no time and no zone
    const givenJson = `
      { "schema": "utv/1.0", "kind": "civil",
        "value": { "form": "date", "fields": { "year": "2026", "month": 11, "day": 5 },
                   "calendar": { "id": "gregory", "chronology": "proleptic" } } }`;

    // WHEN
    const actualResult: UtvResult = validateSemantic(givenJson);

    // EXPECTATIONS
    const expectedStatus = "valid";

    // THEN
    assert.equal(actualResult.status, expectedStatus);
  });

  /**
   * Verifies that validateSemantic returns invalid when well-formed JSON names a day that does not exist.
   * Compared with: `Date.UTC(2026, 1, 29)` rolls over to 1 March; JavaScript has no check to fail.
   */
  it("returns invalid when well-formed JSON names a missing day", () => {
    // GIVEN: 29 February 2026 as UTV JSON: structurally valid, but 2026 is a common year
    const givenJson = `
      { "schema": "utv/1.0", "kind": "civil",
        "value": { "form": "date", "fields": { "year": "2026", "month": 2, "day": 29 },
                   "calendar": { "id": "gregory", "chronology": "proleptic" } } }`;

    // WHEN
    const actualResult = validateSemantic(givenJson);

    // EXPECTATIONS: invalid, with the reason, instead of rolling over to 1 March
    const expectedStatus = "invalid";
    const expectedCodes = ["day-out-of-range"];

    // THEN
    assert.equal(actualResult.status, expectedStatus);
    assert.deepEqual(actualResult.diagnostics.map((diagnostic) => diagnostic.code), expectedCodes);
  });

  /**
   * Verifies that validateSemantic returns unsupported when the date is in another calendar.
   * Compared with: `Intl.DateTimeFormat` with `calendar: "hebrew"` can write a Gregorian Date as "14 Heshvan 5787",
   * but nothing in JavaScript reads Hebrew fields or checks them.
   */
  it("returns unsupported when the calendar is not Gregorian", () => {
    // GIVEN: 14 Heshvan 5787 as UTV JSON, in the Hebrew calendar
    const givenJson = `
      { "schema": "utv/1.0", "kind": "civil",
        "value": { "form": "date", "fields": { "year": "5787", "month": 2, "day": 14 },
                   "calendar": { "id": "hebrew", "chronology": "proleptic" } } }`;

    // WHEN
    const actualResult = validateSemantic(givenJson);

    // EXPECTATIONS: unsupported, kept as written, rather than guessed or converted
    const expectedStatus = "unsupported";
    const expectedCodes = ["unsupported-calendar"];

    // THEN
    assert.equal(actualResult.status, expectedStatus);
    assert.deepEqual(actualResult.diagnostics.map((diagnostic) => diagnostic.code), expectedCodes);
  });

  /**
   * Verifies that validateSemantic returns valid when the value is a year with no month or day.
   * Compared with: `new Date(Date.UTC(1347, 0, 1))`, the usual stand-in, would claim it was 1 January.
   */
  it("returns valid when the value is only a year", () => {
    // GIVEN: the year 1347 as UTV JSON
    const givenJson = `
      { "schema": "utv/1.0", "kind": "civil",
        "value": { "form": "year", "fields": { "year": "1347" },
                   "calendar": { "id": "gregory", "chronology": "proleptic" } } }`;

    // WHEN
    const actualResult = validateSemantic(givenJson);

    // EXPECTATIONS
    const expectedStatus = "valid";

    // THEN
    assert.equal(actualResult.status, expectedStatus);
  });
});
