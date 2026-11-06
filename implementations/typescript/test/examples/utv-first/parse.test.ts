// How to use parse, which reads any value in the UTV text form and returns the type the text describes, compared with
// JavaScript's Date.parse, which turns every timestamp into a time value and forgets how it was written.
import { describe, it } from "node:test";
import assert from "node:assert/strict";
import { FixedOffsetDateTime, Instant, parse, type Outcome, type UtvValue } from "../../../src/index.js";

describe("parse", () => {
  // BEGIN parse-offsets
  /**
   * Verifies that parse returns an Instant when the text ends in Z.
   * Compared with: `new Date("2026-11-05T14:30:00Z")` holds the same moment, and the same Date as the text ending in
   * +00:00.
   */
  it("returns an Instant when the text ends in Z", () => {
    // GIVEN: 14:30 UTC, written with Z: a moment, with no local wall time implied
    const givenText = "2026-11-05T14:30:00Z";

    // WHEN
    const actualOutcome: Outcome<UtvValue> = parse(givenText);

    // EXPECTATIONS: the moment Date parses from the same text
    const expectedInstant = Instant.fromDate(new Date("2026-11-05T14:30:00Z"));

    // THEN
    const actualValue = actualOutcome.single();
    assert.ok(actualValue instanceof Instant);
    assert.equal(String(actualValue), String(expectedInstant));
  });

  /**
   * Verifies that parse returns a FixedOffsetDateTime when the text ends in +00:00.
   * Compared with: `new Date("2026-11-05T14:30:00+00:00")` cannot be told apart from the Date for the text ending in Z.
   */
  it("returns a FixedOffsetDateTime when the text ends in +00:00", () => {
    // GIVEN: 14:30 at offset +00:00: a wall time somewhere that is at UTC+0 right now
    const givenText = "2026-11-05T14:30:00+00:00";

    // WHEN
    const actualOutcome = parse(givenText);

    // EXPECTATIONS: the moment Date parses from the same text, and the text kept as written
    const expectedInstant = Instant.fromDate(new Date("2026-11-05T14:30:00+00:00"));
    const expectedText = "2026-11-05T14:30:00+00:00";

    // THEN
    const actualValue = actualOutcome.single();
    assert.ok(actualValue instanceof FixedOffsetDateTime);
    assert.equal(String(actualValue.toInstant()), String(expectedInstant));
    assert.equal(actualValue.toString(), expectedText);
  });
  // END parse-offsets
});
