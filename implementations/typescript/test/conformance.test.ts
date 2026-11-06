import { test } from "node:test";
import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import { validateStructural } from "../src/index.js";

// Compiled to dist/test/; the package directory is two levels up, the repo root four.
const repo = new URL("../../../../", import.meta.url);
const load = (rel: string): Record<string, unknown> =>
  JSON.parse(readFileSync(new URL(rel, repo), "utf8")) as Record<string, unknown>;

const valid = load("conformance/examples/valid.json");
for (const [name, value] of Object.entries(valid)) {
  test(`valid: ${name}`, () => {
    const r = validateStructural(JSON.stringify(value));
    assert.equal(r.status, "valid", JSON.stringify(r.diagnostics));
  });
}

const invalid = load("conformance/examples/invalid-structural.json");
for (const [name, value] of Object.entries(invalid)) {
  test(`rejected: ${name}`, () => {
    const r = validateStructural(JSON.stringify(value));
    const sid = (value as { schema?: unknown }).schema;
    const wellFormedOther = typeof sid === "string" && sid !== "utv/1.0" && /^utv\/(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)$/.test(sid);
    assert.equal(r.status, wellFormedOther ? "unsupported" : "invalid", `${name} -> ${r.status}`);
    assert.ok(r.diagnostics.length > 0);
  });
}

import { validateSemantic } from "../src/index.js";
for (const [name, value] of Object.entries(valid)) {
  test(`valid semantic (never invalid): ${name}`, () => {
    const r = validateSemantic(JSON.stringify(value));
    assert.ok(r.status === "valid" || r.status === "unsupported", `${name} -> ${r.status}`);
  });
}
