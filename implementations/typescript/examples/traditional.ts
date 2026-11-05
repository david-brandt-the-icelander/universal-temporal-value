// A tour of traditional date and time tasks in UTV.
// Run with `npm run example` from implementations/typescript/.
import { readFileSync } from "node:fs";
import { pathToFileURL } from "node:url";
import { validateStructural } from "../src/structural.js";
import { validateSemantic } from "../src/civil.js";
import type { Status, UtvResult } from "../src/result.js";

// --- Part 1: build two dates from TypeScript values and handle the outcome ---

/** A Gregorian calendar date as a UTV value. The year is a string so it has no size limit. */
function gregorianDate(year: number, month: number, day: number) {
  return {
    schema: "utv/1.0",
    kind: "civil",
    value: { form: "date", fields: { year: String(year), month, day }, calendar: { id: "gregory", chronology: "proleptic" } },
  };
}

function handWritten(): void {
  console.log("== Building dates by hand ==\n");
  for (const [label, date] of [["5 November 2026", gregorianDate(2026, 11, 5)], ["30 February 2026", gregorianDate(2026, 2, 30)]] as const) {
    const text = JSON.stringify(date);
    console.log(`${label}\n  structural: ${validateStructural(text).status}`);
    const result = validateSemantic(text); // runs the structural check first
    switch (result.status) {
      case "valid":
        console.log("  gregorian:  valid, the date exists");
        break;
      case "invalid":
        console.log("  gregorian:  invalid, the value is well-formed but the date does not exist");
        for (const d of result.diagnostics) console.log(`    ${d.severity} ${d.code} at ${d.path}: ${d.message}`);
        break;
      case "unsupported":
        console.log("  gregorian:  unsupported, this library cannot check it yet");
        break;
      default:
        console.log(`  gregorian:  ${result.status}`);
    }
    console.log();
  }
}

// --- Part 2: walk the shared catalog in examples/traditional.json ---

export type Check = "structural" | "gregorian";

export interface CatalogEntry {
  id: string;
  section: string;
  title: string;
  usual: string;
  point: string;
  value?: unknown;
  text?: string;
  later?: string;
  expect: { structural: Status; gregorian?: Status };
}

export interface Mismatch { check: Check; expected: Status; got: Status }

export interface EntryOutcome {
  entry: CatalogEntry;
  structural: UtvResult;
  /** Present only when the entry has `expect.gregorian`. */
  gregorian?: UtvResult;
  mismatches: Mismatch[];
}

// Compiled to dist/examples/; the package directory is two levels up, the repository root four.
const PACKAGE_DIR = new URL("../../", import.meta.url);
export const CATALOG_URL = new URL("../../examples/traditional.json", PACKAGE_DIR);

export function loadCatalog(url: URL = CATALOG_URL): CatalogEntry[] {
  return (JSON.parse(readFileSync(url, "utf8")) as { entries: CatalogEntry[] }).entries;
}

/** The text to validate: raw `text` when given (e.g. duplicate keys), otherwise the serialized `value`. */
export function entryText(entry: CatalogEntry): string {
  return entry.text ?? JSON.stringify(entry.value);
}

export function checkEntry(entry: CatalogEntry): EntryOutcome {
  const text = entryText(entry);
  const structural = validateStructural(text);
  const mismatches: Mismatch[] = [];
  if (structural.status !== entry.expect.structural) {
    mismatches.push({ check: "structural", expected: entry.expect.structural, got: structural.status });
  }
  let gregorian: UtvResult | undefined;
  if (entry.expect.gregorian !== undefined) {
    gregorian = validateSemantic(text);
    if (gregorian.status !== entry.expect.gregorian) {
      mismatches.push({ check: "gregorian", expected: entry.expect.gregorian, got: gregorian.status });
    }
  }
  return { entry, structural, gregorian, mismatches };
}

export function checkCatalog(entries: CatalogEntry[] = loadCatalog()): EntryOutcome[] {
  return entries.map(checkEntry);
}

/** Prints the tour and returns the number of mismatches. */
function tour(outcomes: EntryOutcome[]): number {
  let section: string | undefined;
  let mismatches = 0;
  for (const { entry, structural, gregorian, mismatches: ms } of outcomes) {
    if (entry.section !== section) {
      section = entry.section;
      console.log(`== ${section} ==\n`);
    }
    console.log(entry.title);
    console.log(`  usual:      ${entry.usual}`);
    console.log(`  structural: ${structural.status}`);
    if (gregorian) console.log(`  gregorian:  ${gregorian.status}`);
    console.log(`  why:        ${entry.point}`);
    if (entry.later !== undefined) console.log(`  later:      ${entry.later}`);
    for (const m of ms) console.log(`  MISMATCH: ${m.check} expected ${m.expected}, got ${m.got}`);
    console.log();
    mismatches += ms.length;
  }
  console.log(mismatches === 0 ? `${outcomes.length} examples, every outcome as expected.` : `${mismatches} mismatches.`);
  return mismatches;
}

const isMain = process.argv[1] !== undefined && import.meta.url === pathToFileURL(process.argv[1]).href;
if (isMain) {
  handWritten();
  if (tour(checkCatalog()) > 0) process.exitCode = 1;
}
