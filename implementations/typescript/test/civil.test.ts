import { test } from "node:test";
import assert from "node:assert/strict";
import { validateSemantic, isLeapYear } from "../src/index.js";

const date = (year: string | null, month: number, day: number, cal = `{"id":"gregory","chronology":"proleptic"}`) =>
  `{"schema":"utv/1.0","kind":"civil","value":{"form":"date","fields":{${year === null ? "" : `"year":${JSON.stringify(year)},`}"month":${month},"day":${day}},"calendar":${cal}}}`;

test("Feb 30 is invalid", () => assert.equal(validateSemantic(date("2026", 2, 30)).status, "invalid"));
test("Feb 29 2024 is valid", () => assert.equal(validateSemantic(date("2024", 2, 29)).status, "valid"));
test("Feb 29 2026 is invalid", () => assert.equal(validateSemantic(date("2026", 2, 29)).status, "invalid"));
test("Feb 29 1900 is invalid", () => assert.equal(validateSemantic(date("1900", 2, 29)).status, "invalid"));
test("Feb 29 2000 is valid", () => assert.equal(validateSemantic(date("2000", 2, 29)).status, "valid"));
test("Feb 29 year 0 (proleptic) is valid", () => assert.equal(validateSemantic(date("0", 2, 29)).status, "valid"));
test("Feb 29 year -4 is valid", () => assert.equal(validateSemantic(date("-4", 2, 29)).status, "valid"));
test("huge leap year is exact", () => assert.equal(validateSemantic(date("400000000000000000000000", 2, 29)).status, "valid"));
test("Apr 31 is invalid", () => assert.equal(validateSemantic(date("2026", 4, 31)).status, "invalid"));
test("month 13 is invalid", () => assert.equal(validateSemantic(date("2026", 13, 1)).status, "invalid"));
test("day 0 is invalid", () => assert.equal(validateSemantic(date("2026", 1, 0)).status, "invalid"));
test("Dec 31 is valid", () => assert.equal(validateSemantic(date("2026", 12, 31)).status, "valid"));
test("month-day Feb 29 without year is valid", () => assert.equal(validateSemantic(date(null, 2, 29)).status, "valid"));
test("other calendar is unsupported", () =>
  assert.equal(validateSemantic(date("2026", 2, 30, `{"id":"julian","chronology":"proleptic"}`)).status, "unsupported"));
test("other kinds are unsupported", () =>
  assert.equal(validateSemantic(`{"schema":"utv/1.0","kind":"instant","value":{"system":{},"ticks":"0"}}`).status, "unsupported"));
test("structurally invalid stays invalid", () => assert.equal(validateSemantic(`{"schema":"utv/1.0"}`).status, "invalid"));
test("isLeapYear", () => {
  assert.equal(isLeapYear(2024n), true);
  assert.equal(isLeapYear(2100n), false);
});
