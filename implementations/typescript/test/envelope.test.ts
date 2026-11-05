import { test } from "node:test";
import assert from "node:assert/strict";
import { validateStructural } from "../src/index.js";

const inst = (schema: string, extra = "") =>
  `{"schema":${JSON.stringify(schema)},"kind":"instant","value":{"system":{"system":"utv.timescale","id":"unix-posix","version":"1"},"ticks":"0"}${extra}}`;

test("utv/1.0 instant is valid", () => assert.equal(validateStructural(inst("utv/1.0")).status, "valid"));
for (const s of ["utv/1.7", "utv/2.0", "utv/0.9", "utv/10.0"]) {
  test(`well-formed ${s} is unsupported`, () => assert.equal(validateStructural(inst(s)).status, "unsupported"));
}
test("missing schema is invalid", () =>
  assert.equal(validateStructural(`{"kind":"instant","value":{"system":{},"ticks":"0"}}`).status, "invalid"));
test("non-string schema is invalid", () =>
  assert.equal(validateStructural(`{"schema":1.0,"kind":"instant","value":{"system":{},"ticks":"0"}}`).status, "invalid"));
for (const s of ["utv/4", "utv/1", "utv/1.01", "utv/01.0", "utv/1.", "UTV/1.0", "utv/1.0.0", "utv/1.0 "]) {
  test(`schema ${s} is invalid`, () => assert.equal(validateStructural(inst(s)).status, "invalid"));
}
test("duplicate keys are invalid", () => {
  const r = validateStructural(`{"schema":"utv/1.0","schema":"utv/1.0","kind":"instant","value":{"system":{},"ticks":"0"}}`);
  assert.equal(r.status, "invalid");
  assert.equal(r.diagnostics[0]?.code, "duplicate-key");
});
test("nested duplicate keys are invalid", () =>
  assert.equal(validateStructural(`{"schema":"utv/1.0","kind":"instant","value":{"system":{"id":"a","id":"b"},"ticks":"0"}}`).status, "invalid"));
test("unknown top-level property is invalid", () => assert.equal(validateStructural(inst("utv/1.0", `,"extra":1`)).status, "invalid"));
test("unknown kind is invalid", () =>
  assert.equal(validateStructural(`{"schema":"utv/1.0","kind":"moment","value":{}}`).status, "invalid"));
test("malformed JSON is invalid", () => assert.equal(validateStructural(`{"schema":`).status, "invalid"));
test("instant with both ticks and coordinate is invalid", () =>
  assert.equal(validateStructural(`{"schema":"utv/1.0","kind":"instant","value":{"system":{},"ticks":"0","coordinate":"0"}}`).status, "invalid"));
test("result envelope shape", () => {
  const r = validateStructural(inst("utv/1.0"));
  assert.equal(r.schema, "utv-result/1.0");
  assert.equal(r.operation, "validate");
});
