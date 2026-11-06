// Tests of the shared test data in fixtures.ts.
import { describe, it } from "node:test";
import assert from "node:assert/strict";
import type { ZoneRules } from "../../src/index.js";
import { demoZoneRules } from "./fixtures.js";

describe("demoZoneRules", () => {
  /** Verifies that demoZoneRules loads the three demo zones when called. */
  it("contains the three demo zones when loaded", () => {
    // WHEN
    const actualRules: ZoneRules = demoZoneRules();

    // EXPECTATIONS: the zones in the order the file lists them
    const expectedZoneIds = ["America/New_York", "Europe/Paris", "Atlantic/Reykjavik"];

    // THEN
    assert.deepEqual(actualRules.zoneIds, expectedZoneIds);
  });
});
