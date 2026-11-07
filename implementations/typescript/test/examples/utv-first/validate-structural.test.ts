// How to use validateStructural, which checks that JSON is well-formed UTV before anything interprets it, compared
// with JavaScript, which has no temporal values beyond Date: the values here are written as UTV JSON because
// JavaScript has nothing to write them with, and JSON.parse accepts any well-formed text.
import { describe, it } from "node:test";
import assert from "node:assert/strict";
import { validateStructural, type UtvResult } from "../../../src/index.js";

describe("validateStructural", () => {
  /**
   * Verifies that validateStructural accepts a value that is only a year.
   * Compared with: `new Date(Date.UTC(1347, 0, 1))`, the usual stand-in, claims a day nobody knows.
   */
  it("returns valid when the value is only a year", () => {
    // GIVEN: the year 1347, with no month or day
    const givenJson = `
      { "schema": "utv/1.0", "kind": "civil",
        "value": { "form": "year", "fields": { "year": "1347" },
                   "calendar": { "id": "gregory", "chronology": "proleptic" } } }`;

    // WHEN
    const actualResult: UtvResult = validateStructural(givenJson);

    // EXPECTATIONS
    const expectedStatus = "valid";

    // THEN
    assert.equal(actualResult.status, expectedStatus);
  });

  /**
   * Verifies that validateStructural accepts a leap-second label, leaving its meaning to a later check.
   * Compared with: `Date.UTC(2016, 11, 31, 23, 59, 60)` rolls over to the next year before anything can be stored.
   */
  it("returns valid when the second is a leap second", () => {
    // GIVEN: 23:59:60 UTC on 31 December 2016, a real leap second
    const givenJson = `
      { "schema": "utv/1.0", "kind": "civil",
        "value": { "form": "datetime",
                   "fields": { "year": "2016", "month": 12, "day": 31, "hour": 23, "minute": 59, "second": 60 },
                   "calendar": { "id": "gregory", "chronology": "proleptic" },
                   "zone": { "type": "fixed-offset", "seconds": 0 }, "zoneIntent": "fixed-offset" } }`;

    // WHEN
    const actualResult = validateStructural(givenJson);

    // EXPECTATIONS: well-formed; confirming the label needs a leap-second table
    const expectedStatus = "valid";

    // THEN
    assert.equal(actualResult.status, expectedStatus);
  });

  /**
   * Verifies that validateStructural accepts an interval stored as a start plus an amount.
   * Compared with: a pair of Dates, start and start + 7,200,000 ms, stores a computed end and forgets how it was
   * written.
   */
  it("returns valid when the interval is a start plus an amount", () => {
    // GIVEN: a two-hour meeting starting at 12:30 UTC on 4 November 2026
    const givenJson = `
      { "schema": "utv/1.0", "kind": "interval",
        "value": { "domain": "physical", "construction": "start-amount",
                   "start": { "state": "included",
                              "point": { "schema": "utv/1.0", "kind": "instant",
                                         "value": { "system": { "system": "utv.timescale", "id": "unix-posix", "version": "1" },
                                                    "ticks": "1793795400" } } },
                   "amount": { "schema": "utv/1.0", "kind": "amount",
                               "value": { "basis": "elapsed", "seconds": { "numerator": "7200", "denominator": "1" } } } } }`;

    // WHEN
    const actualResult = validateStructural(givenJson);

    // EXPECTATIONS
    const expectedStatus = "valid";

    // THEN
    assert.equal(actualResult.status, expectedStatus);
  });

  /**
   * Verifies that validateStructural accepts a union of two separate date ranges.
   * Compared with: JavaScript has no range type, so two ranges are an array of hand-made pairs of Dates.
   */
  it("returns valid when the value is a union of ranges", () => {
    // GIVEN: 1–5 November and 20–25 November 2026, as one value
    const givenJson = `
      { "schema": "utv/1.0", "kind": "set",
        "value": { "operation": "union", "members": [
          { "schema": "utv/1.0", "kind": "interval",
            "value": { "domain": "civil/gregory",
                       "start": { "state": "included", "point": { "schema": "utv/1.0", "kind": "civil",
                                  "value": { "form": "date", "fields": { "year": "2026", "month": 11, "day": 1 },
                                             "calendar": { "id": "gregory", "chronology": "proleptic" } } } },
                       "end": { "state": "excluded", "point": { "schema": "utv/1.0", "kind": "civil",
                                "value": { "form": "date", "fields": { "year": "2026", "month": 11, "day": 6 },
                                           "calendar": { "id": "gregory", "chronology": "proleptic" } } } } } },
          { "schema": "utv/1.0", "kind": "interval",
            "value": { "domain": "civil/gregory",
                       "start": { "state": "included", "point": { "schema": "utv/1.0", "kind": "civil",
                                  "value": { "form": "date", "fields": { "year": "2026", "month": 11, "day": 20 },
                                             "calendar": { "id": "gregory", "chronology": "proleptic" } } } },
                       "end": { "state": "excluded", "point": { "schema": "utv/1.0", "kind": "civil",
                                "value": { "form": "date", "fields": { "year": "2026", "month": 11, "day": 26 },
                                           "calendar": { "id": "gregory", "chronology": "proleptic" } } } } } } ] } }`;

    // WHEN
    const actualResult = validateStructural(givenJson);

    // EXPECTATIONS
    const expectedStatus = "valid";

    // THEN
    assert.equal(actualResult.status, expectedStatus);
  });

  /**
   * Verifies that validateStructural accepts a weekly recurrence anchored in a named zone.
   * Compared with: JavaScript has no recurrence type; a list of computed Dates goes stale when zone rules change.
   */
  it("returns valid when the value is a weekly recurrence", () => {
    // GIVEN: every Tuesday at 09:00 in Paris, from 3 November 2026, as an RFC 5545 rule
    const givenJson = `
      { "schema": "utv/1.0", "kind": "recurrence",
        "value": { "intent": { "schema": "utv/1.0", "kind": "civil",
                               "value": { "form": "datetime",
                                          "fields": { "year": "2026", "month": 11, "day": 3, "hour": 9, "minute": 0 },
                                          "calendar": { "id": "gregory", "chronology": "proleptic" },
                                          "zone": { "type": "iana", "id": "Europe/Paris" }, "zoneIntent": "follow-zone-id" } },
                   "algorithm": { "system": "rfc", "id": "5545", "version": "1" },
                   "rule": "FREQ=WEEKLY;BYDAY=TU" } }`;

    // WHEN
    const actualResult = validateStructural(givenJson);

    // EXPECTATIONS
    const expectedStatus = "valid";

    // THEN
    assert.equal(actualResult.status, expectedStatus);
  });

  /**
   * Verifies that validateStructural accepts a year known only to lie within bounds.
   * Compared with: a Date can only store one moment, such as 1347-01-01, and hope nobody asks.
   */
  it("returns valid when the year is uncertain", () => {
    // GIVEN: about 1347, somewhere between 1340 and 1355
    const givenJson = `
      { "schema": "utv/1.0", "kind": "uncertain",
        "value": { "target": { "schema": "utv/1.0", "kind": "civil",
                               "value": { "form": "year", "fields": { "year": "1347" },
                                          "calendar": { "id": "gregory", "chronology": "proleptic" } } },
                   "model": "bounds",
                   "parameters": { "earliest": { "year": "1340" }, "latest": { "year": "1355" },
                                   "interpretation": "possible range, not 95% confidence" } } }`;

    // WHEN
    const actualResult = validateStructural(givenJson);

    // EXPECTATIONS
    const expectedStatus = "valid";

    // THEN
    assert.equal(actualResult.status, expectedStatus);
  });

  /**
   * Verifies that validateStructural returns unsupported when the value is from a newer version.
   * Compared with: `JSON.parse` reads the text as if nothing had changed; noticing the version is left to the caller.
   */
  it("returns unsupported when the version is newer", () => {
    // GIVEN: a well-formed date labelled with a future specification version
    const givenJson = `
      { "schema": "utv/2.0", "kind": "civil",
        "value": { "form": "date", "fields": { "year": "2026", "month": 11, "day": 5 },
                   "calendar": { "id": "gregory", "chronology": "proleptic" } } }`;

    // WHEN
    const actualResult = validateStructural(givenJson);

    // EXPECTATIONS: unsupported, not invalid: the value may be fine, this library just doesn't read it
    const expectedStatus = "unsupported";
    const expectedCodes = ["unsupported-version"];

    // THEN
    assert.equal(actualResult.status, expectedStatus);
    assert.deepEqual(actualResult.diagnostics.map((diagnostic) => diagnostic.code), expectedCodes);
  });

  /**
   * Verifies that validateStructural returns invalid when the version label is malformed.
   * Compared with: `JSON.parse` accepts the text; nothing in JavaScript checks the label.
   */
  it("returns invalid when the version label is malformed", () => {
    // GIVEN: a date labelled "utv/4", an unreleased draft label
    const givenJson = `
      { "schema": "utv/4", "kind": "civil",
        "value": { "form": "date", "fields": { "year": "2026", "month": 11, "day": 5 },
                   "calendar": { "id": "gregory", "chronology": "proleptic" } } }`;

    // WHEN
    const actualResult = validateStructural(givenJson);

    // EXPECTATIONS
    const expectedStatus = "invalid";
    const expectedCodes = ["invalid-schema-identifier"];

    // THEN
    assert.equal(actualResult.status, expectedStatus);
    assert.deepEqual(actualResult.diagnostics.map((diagnostic) => diagnostic.code), expectedCodes);
  });

  /**
   * Verifies that validateStructural returns invalid when a key appears twice.
   * Compared with: `JSON.parse` accepts the text and keeps the last month, December.
   */
  it("returns invalid when a key appears twice", () => {
    // GIVEN: a date with two different months
    const givenJson =
      `{"schema":"utv/1.0","kind":"civil","value":{"form":"date","fields":{"year":"2026","month":11,"month":12,"day":5},"calendar":{"id":"gregory","chronology":"proleptic"}}}`;

    // WHEN
    const actualResult = validateStructural(givenJson);

    // EXPECTATIONS: invalid, rather than whichever month a parser happens to keep
    const expectedStatus = "invalid";
    const expectedCodes = ["duplicate-key"];

    // THEN
    assert.equal(actualResult.status, expectedStatus);
    assert.deepEqual(actualResult.diagnostics.map((diagnostic) => diagnostic.code), expectedCodes);
  });

  /**
   * Verifies that validateStructural returns invalid when the kind is unknown.
   * Compared with: `JSON.parse` accepts the text; JavaScript has no idea what a temporal value should look like.
   */
  it("returns invalid when the kind is unknown", () => {
    // GIVEN: a value of a kind the specification does not define
    const givenJson = `{ "schema": "utv/1.0", "kind": "moment", "value": {} }`;

    // WHEN
    const actualResult = validateStructural(givenJson);

    // EXPECTATIONS: fail closed: an unknown kind is rejected, not ignored
    const expectedStatus = "invalid";
    const expectedCodes = ["unknown-kind"];

    // THEN
    assert.equal(actualResult.status, expectedStatus);
    assert.deepEqual(actualResult.diagnostics.map((diagnostic) => diagnostic.code), expectedCodes);
  });
});
