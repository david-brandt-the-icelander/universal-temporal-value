import { test } from "node:test";
import assert from "node:assert/strict";
import { readFileSync, readdirSync } from "node:fs";
import {
  candidate, date, dateTime, formatIso, fromDate, fromEpochMilliseconds, fromPlainDate, inZone, instant,
  loadRules, toDate, toEpochMilliseconds, toPlainDate, withOffset, type Json,
} from "../src/index.js";

const repo = new URL("../../../../", import.meta.url);
const rules = loadRules(readFileSync(new URL("conformance/rules/demo-zones.json", repo), "utf8"));
const iso = (v: Json | undefined) => (v === undefined ? undefined : candidate(formatIso(v)));

// --- Date -> UTV: the caller names the intent ---------------------------------------------------

const d = new Date(Date.UTC(2026, 10, 5, 13, 30, 0, 250)); // 2026-11-05T13:30:00.250Z

test("Date as an instant", () => {
  const r = fromDate(d, "instant");
  assert.equal(r.status, "unique");
  assert.equal(iso(candidate(r)), "2026-11-05T13:30:00.25Z");
});
test("Date at a fixed offset", () => {
  const r = fromDate(d, { offsetSeconds: 3600 });
  assert.equal(r.status, "unique");
  assert.equal(iso(candidate(r)), "2026-11-05T14:30:00.25+01:00");
});
test("Date in a named zone uses explicit rules", () => {
  const r = fromDate(d, { zone: "America/New_York", rules });
  assert.equal(iso(candidate(r)), "2026-11-05T08:30:00.25-05:00[America/New_York]");
  assert.equal(r.manifest.dependencies.length, 1);
});
test("Date as local civil time is refused (host zone)", () => {
  const r = fromDate(d, "local");
  assert.equal(r.status, "unsupported");
  assert.equal(r.diagnostics[0]?.code, "local-zone");
  assert.equal(r.candidates.length, 0);
});
test("Invalid Date is invalid", () => assert.equal(fromDate(new Date(Number.NaN), "instant").status, "invalid"));
test("Date in a zone outside the rules is unsupported", () =>
  assert.equal(fromDate(d, { zone: "Asia/Tokyo", rules }).status, "unsupported"));
test("negative Date milliseconds floor correctly", () =>
  assert.equal(iso(candidate(fromDate(new Date(-1), "instant"))), "1969-12-31T23:59:59.999Z"));

// --- UTV -> Date: loss is reported ----------------------------------------------------------------

test("instant to Date is lossless at millisecond precision", () => {
  const r = toDate(instant(1793885400, 250_000_000));
  assert.equal(r.status, "unique");
  assert.equal(r.value?.getTime(), d.getTime());
  assert.equal(r.loss.classification, "lossless");
});
test("sub-millisecond instant to Date reports loss", () => {
  const r = toDate(instant(0, 1_500_000)); // 1.5 ms
  assert.equal(r.status, "unique");
  assert.equal(r.value?.getTime(), 1);
  assert.equal(r.loss.classification, "information-losing");
  assert.ok(r.loss.fields.includes("subTick"));
});
test("fixed-offset date-time to Date reports the dropped offset", () => {
  const r = toDate(withOffset(dateTime(2026, 11, 5, 14, 30), 3600));
  assert.equal(r.value?.toISOString(), "2026-11-05T13:30:00.000Z");
  assert.ok(r.loss.fields.includes("zone"));
});
test("zoned date-time to Date is unsupported (resolve first)", () =>
  assert.equal(toDate(inZone(dateTime(2027, 6, 1, 9, 0), "Europe/Paris")).status, "unsupported"));
test("floating date-time to Date is unsupported (would need host zone)", () =>
  assert.equal(toDate(dateTime(2027, 6, 1, 9, 0)).status, "unsupported"));
test("instant beyond the Date range is unsupported", () =>
  assert.equal(toDate(instant("8640000000001")).status, "unsupported"));
test("Date round trip", () => {
  const back = toDate(candidate(fromDate(d, "instant"))!);
  assert.equal(back.value?.getTime(), d.getTime());
});

// --- epoch milliseconds ---------------------------------------------------------------------------

test("epoch milliseconds to instant and back", () => {
  const r = fromEpochMilliseconds(1793885400250);
  assert.equal(iso(candidate(r)), "2026-11-05T13:30:00.25Z");
  assert.equal(toEpochMilliseconds(candidate(r)!).value, 1793885400250);
});
test("bigint epoch milliseconds", () => assert.equal(iso(candidate(fromEpochMilliseconds(-1n))), "1969-12-31T23:59:59.999Z"));
test("fractional epoch milliseconds are invalid", () => assert.equal(fromEpochMilliseconds(1.5).status, "invalid"));
test("sub-millisecond to epoch milliseconds reports loss", () => {
  const r = toEpochMilliseconds(instant(-1, 999_999_999)); // -1 ns
  assert.equal(r.value, -1);
  assert.equal(r.loss.classification, "information-losing");
});

// --- plain { year, month, day } -------------------------------------------------------------------

test("plain date to UTV and back", () => {
  const r = fromPlainDate({ year: 2026, month: 11, day: 5 });
  assert.equal(iso(candidate(r)), "2026-11-05");
  assert.equal(JSON.stringify(toPlainDate(candidate(r)!).value), `{"year":2026,"month":11,"day":5}`);
});
test("plain 30 February is invalid, not rolled over", () => assert.equal(fromPlainDate({ year: 2026, month: 2, day: 30 }).status, "invalid"));
test("plain date with month 0 is invalid (months are 1-based)", () => assert.equal(fromPlainDate({ year: 2026, month: 0, day: 1 }).status, "invalid"));
test("huge year to plain date is unsupported", () => assert.equal(toPlainDate(date("90071992547409930", 1, 1)).status, "unsupported"));
test("zoned date to plain date is unsupported", () => assert.equal(toPlainDate(inZone(date(2026, 11, 5), "Europe/Paris")).status, "unsupported"));

// --- purity: library code never reads the clock, host zone or locale ------------------------------

test("library sources never read the clock, host zone or locale", () => {
  const src = new URL("../../src/", import.meta.url); // the TypeScript sources (this file runs from dist/test/)
  const forbidden = [/Date\.now/, /new Date\(\s*\)/, /Intl\./, /getTimezoneOffset/, /toLocale/, /process\./, /\bget(Hours|Minutes|Date|Day|Month|FullYear)\(/, /performance\./];
  const files = readdirSync(src).filter((f) => f.endsWith(".ts"));
  assert.ok(files.includes("native.ts") && files.includes("zones.ts"), "library sources not found");
  for (const f of files) {
    const text = readFileSync(new URL(f, src), "utf8").replace(/\/\*[\s\S]*?\*\//g, "").replace(/\/\/.*$/gm, "");
    for (const re of forbidden) assert.ok(!re.test(text), `${f} matches ${re}`);
  }
});
