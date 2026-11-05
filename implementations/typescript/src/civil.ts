import { parseStrict, type Json } from "./json.js";
import { makeResult, type Diagnostic, type UtvResult } from "./result.js";
import { validateParsed, validateStructural } from "./structural.js";

type Obj = { [k: string]: Json };
const isObj = (v: Json | undefined): v is Obj => typeof v === "object" && v !== null && !Array.isArray(v);

/** Proleptic Gregorian leap-year rule, on arbitrary-precision years (astronomical numbering, year 0 exists). */
export function isLeapYear(year: bigint): boolean {
  return (year % 4n === 0n && year % 100n !== 0n) || year % 400n === 0n;
}

export function daysInMonth(year: bigint, month: number): number {
  if (month === 2) return isLeapYear(year) ? 29 : 28;
  return [4, 6, 9, 11].includes(month) ? 30 : 31;
}

/**
 * Semantic validation for proleptic Gregorian `civil` dates. Runs structural validation first
 * (structural and semantic validity stay separate checks). Anything this increment does not
 * implement — other kinds, other calendars or chronologies — returns `unsupported`.
 */
export function validateSemantic(text: string): UtvResult {
  let doc: Json;
  try { doc = parseStrict(text); } catch { return validateStructural(text); }
  const structural = validateParsed(doc);
  if (structural.status !== "valid") return structural;
  const env = doc as Obj;
  const value = env["value"] as Obj;
  const unsupported = (path: string, message: string) =>
    makeResult("unsupported", [{ code: "unsupported-feature", severity: "error", path, message }]);
  if (env["kind"] !== "civil") return unsupported("/kind", `semantic validation of kind "${String(env["kind"])}" is not implemented`);
  const cal = value["calendar"];
  if (!isObj(cal) || cal["id"] !== "gregory" || cal["chronology"] !== "proleptic") {
    return unsupported("/value/calendar", "only the proleptic Gregorian calendar is implemented");
  }
  const fields = value["fields"];
  if (!isObj(fields)) return makeResult("invalid", [{ code: "type", severity: "error", path: "/value/fields", message: "fields must be an object" }]);
  const diags: Diagnostic[] = [];
  const bad = (f: string, message: string) => diags.push({ code: "field-out-of-range", severity: "error", path: `/value/fields/${f}`, message });
  const y = fields["year"], m = fields["month"], d = fields["day"];
  let year: bigint | undefined;
  if (y !== undefined) {
    if (typeof y === "string" && /^-?(0|[1-9][0-9]*)$/.test(y)) year = BigInt(y);
    else bad("year", "year must be a decimal integer string");
  }
  if (m !== undefined && !(typeof m === "number" && Number.isInteger(m) && m >= 1 && m <= 12)) bad("month", "month must be an integer 1..12");
  if (d !== undefined && !(typeof d === "number" && Number.isInteger(d) && d >= 1 && d <= 31)) bad("day", "day must be an integer 1..31");
  if (diags.length === 0 && typeof m === "number" && typeof d === "number") {
    // Without a year, Feb 29 is admissible (it exists in some year).
    const max = year === undefined ? (m === 2 ? 29 : daysInMonth(1n, m)) : daysInMonth(year, m);
    if (d > max) bad("day", `day ${d} does not exist in month ${m}${year === undefined ? "" : ` of year ${year}`}`);
  }
  if (diags.length > 0) return makeResult("invalid", diags);
  return makeResult("valid", []);
}
