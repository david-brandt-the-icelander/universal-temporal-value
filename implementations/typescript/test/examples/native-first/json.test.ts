// What JavaScript's JSON does with temporal JSON, compared with UTV's validateStructural.
import { describe, it } from "node:test";
import assert from "node:assert/strict";

describe("JSON", () => {
  describe("parse", () => {
    /**
     * Verifies that JSON.parse accepts duplicate keys and keeps the last value when a key appears twice.
     * Compared with: `validateStructural` rejects the same text as invalid (duplicate-key): two months in one date
     * are a contradiction, not something to settle by taking the last.
     */
    it("keeps the last value when a key appears twice", () => {
      // GIVEN: a UTV date with two different months
      const givenJson =
        `{"schema":"utv/1.0","kind":"civil","value":{"form":"date","fields":{"year":"2026","month":11,"month":12,"day":5},"calendar":{"id":"gregory","chronology":"proleptic"}}}`;

      // WHEN
      const actualValue = JSON.parse(givenJson) as { value: { fields: { month: number } } };

      // EXPECTATIONS: the second month, December
      const expectedMonth = 12;

      // THEN
      assert.equal(actualValue.value.fields.month, expectedMonth);
    });
  });
});
