import { test } from "node:test";
import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import { execFileSync } from "node:child_process";
import { fileURLToPath } from "node:url";

// Snapshot test for examples/everyday.ts (docs/library-api.md §7): runs the example and compares its
// output with examples/everyday.expected.txt line by line. Lines ending in " ‡" depend on the host
// (clock, time zone, tz database), so only their position is checked. Record a new snapshot with
// `npm run everyday:snapshot` and review the diff.
const pkg = new URL("../../", import.meta.url); // compiled to dist/test/
const HOST = " ‡";

test("everyday example matches examples/everyday.expected.txt", () => {
  const expected = readFileSync(new URL("examples/everyday.expected.txt", pkg), "utf8").split("\n");
  const actual = execFileSync(process.execPath, [fileURLToPath(new URL("dist/examples/everyday.js", pkg))], {
    cwd: fileURLToPath(pkg), encoding: "utf8",
  }).split("\n");
  assert.equal(actual.length, expected.length, `line count: got ${actual.length}, snapshot has ${expected.length}`);
  expected.forEach((want, i) => {
    const got = actual[i]!;
    if (want.endsWith(HOST)) assert.ok(got.endsWith(HOST), `line ${i + 1} should be host-dependent: ${got}`);
    else assert.equal(got, want, `line ${i + 1} differs`);
  });
  for (const id of ["month-end", "dst-gap", "dst-overlap"]) {
    const source = readFileSync(new URL("examples/everyday.ts", pkg), "utf8");
    assert.ok(source.includes(`// BEGIN ${id}\n`) && source.includes(`// END ${id}\n`), `task markers for ${id}`);
  }
});
