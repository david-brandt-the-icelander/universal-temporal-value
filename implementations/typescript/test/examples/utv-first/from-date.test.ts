// How to use fromDate, the JSON-layer adapter that turns a JavaScript Date into a UTV value with an intent the caller
// names, compared with Date's local methods, which read the host's time zone without being asked.
import { describe, it } from "node:test";
import assert from "node:assert/strict";
import { fromDate, type UtvResult } from "../../../src/index.js";

describe("fromDate", () => {
  // BEGIN now
  /**
   * Verifies that fromDate returns unsupported when asked for the Date's local wall time.
   * Compared with: `getHours()` and the other local methods read the host's time zone, so the same Date shows a
   * different wall time on every machine.
   */
  it("returns unsupported when the intent is local", () => {
    // GIVEN: a reading as Date.now() would give it, and a request for its wall time on this machine
    const givenReading = new Date(1_793_885_400_123);
    const givenIntent = "local";

    // WHEN
    const actualResult: UtvResult = fromDate(givenReading, givenIntent);

    // EXPECTATIONS: refused, because the host's zone is hidden input; name a zone or an offset instead
    const expectedStatus = "unsupported";
    const expectedCodes = ["local-zone"];

    // THEN
    assert.equal(actualResult.status, expectedStatus);
    assert.deepEqual(actualResult.diagnostics.map((diagnostic) => diagnostic.code), expectedCodes);
  });
  // END now
});
