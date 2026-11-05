import { test } from "node:test";
import assert from "node:assert/strict";
import { checkEntry, loadCatalog } from "../examples/traditional.js";

// Every outcome in the shared catalog examples/traditional.json must match this library.
const entries = loadCatalog();

test("catalog is not empty and ids are unique", () => {
  assert.ok(entries.length > 0);
  assert.equal(new Set(entries.map((e) => e.id)).size, entries.length);
});

for (const entry of entries) {
  test(`catalog: ${entry.id}`, () => {
    const { mismatches } = checkEntry(entry);
    assert.equal(mismatches.length, 0, mismatches.map((m) => `${m.check} expected ${m.expected}, got ${m.got}`).join("; "));
  });
}
