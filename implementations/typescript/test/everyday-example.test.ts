import { test } from "node:test";
import assert from "node:assert/strict";
import { runEveryday } from "../examples/everyday.js";

// Runs the side-by-side example (examples/everyday.ts) silently. Only the UTV outcomes are checked:
// they are deterministic. Native lines depend on the host (clock, tz data) and are never asserted.
test("everyday example: every UTV outcome as expected", () => {
  const lines: string[] = [];
  const mismatches = runEveryday((line = "") => lines.push(line));
  assert.equal(mismatches.length, 0, mismatches.join("; "));
  for (let i = 1; i <= 14; i++) assert.ok(lines.some((l) => l.trim().startsWith(`== ${i}. `)), `section ${i} missing`);
});
