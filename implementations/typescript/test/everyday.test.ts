import { test } from "node:test";
import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import {
  add, compare, contains, daysBetween, fields, formatIso, loadRules, overlaps, parseIso, posixSecondsBetween,
  resolve, toOffset, toZone, type Json, type UtvResult, type ZoneRules,
} from "../src/index.js";

// Runs every fixture in the shared conformance/operations/everyday.json, read from the repository
// (compiled to dist/test/, so the repository root is four levels up).
const repo = new URL("../../../../", import.meta.url);
interface Fixture { id: string; op: string; args: Json[]; expect: { status: string; result?: Json; results?: Json[]; diagnostics?: string[] } }
const file = JSON.parse(readFileSync(new URL("conformance/operations/everyday.json", repo), "utf8")) as {
  rules: Record<string, string>; fixtures: Fixture[];
};

const rules = new Map<string, ZoneRules>();
for (const [name, rel] of Object.entries(file.rules)) rules.set(name, loadRules(readFileSync(new URL(rel, repo), "utf8")));
const rulesArg = (name: Json | undefined): ZoneRules => {
  const r = rules.get(name as string);
  if (!r) throw new Error(`unknown rules ${String(name)}`);
  return r;
};

type Op = (args: Json[]) => UtvResult;
const OPS: Record<string, Op> = {
  parseIso: ([t]) => parseIso(t as string),
  formatIso: ([v]) => formatIso(v!),
  fields: ([v]) => fields(v!),
  add: ([v, a, p]) => add(v!, a!, p as never),
  compare: ([a, b]) => compare(a!, b!),
  daysBetween: ([a, b]) => daysBetween(a!, b!),
  posixSecondsBetween: ([a, b]) => posixSecondsBetween(a!, b!),
  contains: ([i, p]) => contains(i!, p!),
  overlaps: ([a, b]) => overlaps(a!, b!),
  resolve: ([v, r, p]) => resolve(v!, rulesArg(r), p as never),
  toZone: ([v, z, r]) => toZone(v!, z as string, rulesArg(r)),
  toOffset: ([v, s]) => toOffset(v!, s as number),
};

/** JSON text with object keys sorted, so comparison ignores key order. */
const canon = (v: Json | undefined): string =>
  Array.isArray(v) ? `[${v.map(canon).join(",")}]`
  : typeof v === "object" && v !== null ? `{${Object.keys(v).sort().map((k) => `${JSON.stringify(k)}:${canon(v[k])}`).join(",")}}`
  : JSON.stringify(v ?? null);

test("everyday.json has fixtures with unique ids", () => {
  assert.ok(file.fixtures.length > 0);
  assert.equal(new Set(file.fixtures.map((f) => f.id)).size, file.fixtures.length);
});

let passed = 0;
for (const fx of file.fixtures) {
  test(`everyday: ${fx.id}`, () => {
    const op = OPS[fx.op];
    assert.ok(op, `${fx.id}: unknown op ${fx.op}`);
    const r = op(fx.args);
    const why = `${fx.id}: ${JSON.stringify(r.diagnostics)}`;
    assert.equal(r.status, fx.expect.status, why);
    const expected: Json[] = fx.expect.results ?? (fx.expect.result !== undefined ? [fx.expect.result] : []);
    assert.equal(canon(r.candidates), canon(expected), `${fx.id}: candidates differ`);
    if (r.status === "unique") assert.equal(r.candidates.length, 1, `${fx.id}: unique needs one candidate`);
    const codes = r.diagnostics.map((d) => d.code);
    for (const code of fx.expect.diagnostics ?? []) assert.ok(codes.includes(code), `${fx.id}: expected diagnostic ${code}, got [${codes.join(", ")}]`);
    passed++;
  });
}
test("all everyday fixtures passed", () => assert.equal(passed, file.fixtures.length, `${passed}/${file.fixtures.length} everyday fixtures passed`));
