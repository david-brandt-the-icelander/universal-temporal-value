import { test } from "node:test";
import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import { formatText, parseText, type Json, type UtvResult } from "../src/index.js";

// Runs every fixture in the shared conformance/operations/text-form.json through the JSON-level
// parseText / formatText (compiled to dist/test/, so the repository root is four levels up).
const repo = new URL("../../../../", import.meta.url);
interface Fixture {
  id: string;
  direction: "both" | "parse" | "format";
  text?: string;
  value?: Json;
  expect?: { status: string; value?: Json; text?: string; diagnostics?: string[] };
}
const file = JSON.parse(readFileSync(new URL("conformance/operations/text-form.json", repo), "utf8")) as { fixtures: Fixture[] };

const canon = (v: Json | undefined): string =>
  Array.isArray(v) ? `[${v.map(canon).join(",")}]`
  : typeof v === "object" && v !== null ? `{${Object.keys(v).sort().map((k) => `${JSON.stringify(k)}:${canon(v[k])}`).join(",")}}`
  : JSON.stringify(v ?? null);

function check(id: string, r: UtvResult, status: string, candidate: Json | undefined, codes: string[] = []): void {
  assert.equal(r.status, status, `${id}: ${JSON.stringify(r.diagnostics)}`);
  if (status === "unique") assert.equal(canon(r.candidates[0]), canon(candidate), `${id}: result differs`);
  else assert.equal(r.candidates.length, 0, `${id}: only unique has a candidate`);
  const got = r.diagnostics.map((d) => d.code);
  for (const code of codes) assert.ok(got.includes(code), `${id}: expected diagnostic ${code}, got [${got.join(", ")}]`);
}

test("text-form.json has fixtures with unique ids", () => {
  assert.ok(file.fixtures.length > 0);
  assert.equal(new Set(file.fixtures.map((f) => f.id)).size, file.fixtures.length);
});

let passed = 0;
for (const fx of file.fixtures) {
  test(`text-form: ${fx.id} (${fx.direction})`, () => {
    if (fx.direction === "both") {
      check(`${fx.id} parse`, parseText(fx.text!), "unique", fx.value);
      check(`${fx.id} format`, formatText(fx.value!), "unique", fx.text);
    } else if (fx.direction === "parse") {
      check(fx.id, parseText(fx.text!), fx.expect!.status, fx.expect!.value, fx.expect!.diagnostics);
    } else {
      check(fx.id, formatText(fx.value!), fx.expect!.status, fx.expect!.text, fx.expect!.diagnostics);
    }
    passed++;
  });
}
test("all text-form fixtures passed", () => assert.equal(passed, file.fixtures.length, `${passed}/${file.fixtures.length} text-form fixtures passed`));
