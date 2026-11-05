import { test } from "node:test";
import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import {
  add, candidate, compare, contains, date, dateTime, formatIso, inZone, instant, interval, loadRules, overlaps,
  parseIso, period, posixSeconds, resolve, time, validateStructural, withOffset, yearMonth, type Json,
} from "../src/index.js";

// Library behaviour beyond the shared fixtures (those run in everyday.test.ts).
const repo = new URL("../../../../", import.meta.url);
const rules = loadRules(readFileSync(new URL("conformance/rules/demo-zones.json", repo), "utf8"));
const iso = (v: Json | undefined) => (v === undefined ? undefined : candidate(formatIso(v)));

test("builders produce structurally valid values", () => {
  for (const v of [date(2026, 11, 5), yearMonth(2026, 11), time(9, 30), time(9, 30, 15, 250_000_000), dateTime(2026, 11, 5, 9, 30),
    inZone(dateTime(2027, 6, 1, 9, 0), "Europe/Paris"), withOffset(dateTime(2026, 11, 5, 9, 30), 3600), instant(0, 500_000_000),
    period({ months: 1 }), period(0, 0, 0, 1), posixSeconds(86400), interval(date(2026, 11, 1), date(2026, 12, 1)), interval(date(2026, 11, 1), "unknown")]) {
    assert.equal(validateStructural(JSON.stringify(v)).status, "valid", JSON.stringify(v));
  }
});

test("period: positional and named forms agree, zeros dropped", () => {
  assert.equal(JSON.stringify(period(0, 1, 0, -2)), JSON.stringify(period({ months: 1, days: -2 })));
});

test("parseIso and formatIso round-trip", () => {
  for (const s of ["2026-11-05", "2026-11", "09:30", "09:30:15.25", "2026-11-05T14:30", "2026-11-05T14:30:00-05:00",
    "2027-06-01T09:00[Europe/Paris]", "2027-06-01T09:00+02:00[Europe/Paris]", "+275760-09-13", "-0500-03-01", "1970-01-01T00:00:00.5Z"]) {
    assert.equal(iso(candidate(parseIso(s))), s);
  }
});

test("parseIso: more invalid and unsupported forms", () => {
  for (const s of ["2026-11-05 14:30", "2026-11-05T14:30z", "2026-1-05", "-0000-01-01", "2026-11-05T14:30:00.1234567890", "2026-11-05T24:30", "2026-11-05T14:30+18:30"]) {
    assert.equal(parseIso(s).status, "invalid", s);
  }
  for (const s of ["2026-11-05T14:30Z[Europe/Paris]", "2026-11-05T14:30-00:00[Europe/Paris]", "24:00", "2026-W45", "20261105T1430Z"]) {
    assert.equal(parseIso(s).status, "unsupported", s);
  }
});

test("add drops a stale offsetAssertion and says so", () => {
  const v = candidate(parseIso("2026-03-28T09:00+01:00[Europe/Paris]"))!;
  const r = add(v, period({ days: 1 }));
  assert.equal(iso(candidate(r)), "2026-03-29T09:00[Europe/Paris]");
  assert.equal(r.diagnostics[0]?.code, "offset-assertion-dropped");
});

test("add: negative months, seconds carry, constrain", () => {
  assert.equal(iso(candidate(add(date(2026, 3, 31), period({ months: -1 }), { monthEnd: "constrain" }))), "2026-02-28");
  assert.equal(iso(candidate(add(dateTime(2026, 12, 31, 23, 59), period({ seconds: 61 })))), "2027-01-01T00:00:01");
  assert.equal(add(date(2026, 1, 1), period({ days: 1 }), { monthEnd: "nearest" as never }).status, "unsupported");
});

test("resolve honours an offsetAssertion in an overlap, and rejects a contradicting one", () => {
  const second = candidate(parseIso("2026-11-01T01:30-05:00[America/New_York]"))!;
  assert.equal(iso(candidate(resolve(second, rules))), "2026-11-01T06:30:00Z");
  const wrong = candidate(parseIso("2026-07-04T12:00+01:00[America/New_York]"))!;
  assert.equal(resolve(wrong, rules).status, "invalid");
});

test("resolve: overlap policy all, unknown policy values, rules as JSON text", () => {
  const v = inZone(dateTime(2026, 11, 1, 1, 30), "America/New_York");
  const all = resolve(v, rules, { overlap: "all" });
  assert.equal(all.status, "multiple");
  assert.equal(all.diagnostics.length, 0);
  assert.equal(resolve(v, rules, { overlap: "offset-match" as never }).status, "unsupported");
  assert.equal(resolve(v, readFileSync(new URL("conformance/rules/demo-zones.json", repo), "utf8")).status, "multiple");
  assert.equal(resolve(v, "{not json").status, "invalid");
});

test("compare: Z and +00:00 are different kinds", () => {
  const z = candidate(parseIso("2026-11-05T14:30:00Z"))!, zero = candidate(parseIso("2026-11-05T14:30:00+00:00"))!;
  assert.equal(compare(z, zero).status, "unsupported");
});

test("intervals: excluded start, empty, unknown start", () => {
  assert.equal(candidate(contains(interval(date(2026, 11, 1), date(2026, 12, 1), false), date(2026, 11, 1))), "impossible");
  assert.equal(candidate(overlaps(interval(date(2026, 11, 5), date(2026, 11, 5)), interval(date(2026, 1, 1), "unbounded"))), "impossible");
  assert.equal(candidate(contains(interval("unknown", date(2026, 12, 1)), date(2026, 11, 1))), "possibly");
  assert.equal(candidate(overlaps(interval(instant(0), instant(10)), interval(instant(10), instant(20)))), "impossible");
});

test("operations never throw on garbage", () => {
  for (const g of [null, 1, "x", [], {}, { schema: "utv/1.0", kind: "civil", value: {} }] as Json[]) {
    for (const r of [formatIso(g), add(g, g), compare(g, g), contains(g, g), resolve(g, rules)]) {
      assert.ok(["invalid", "unsupported"].includes(r.status), `${JSON.stringify(g)} -> ${r.status}`);
    }
  }
});
