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
  const unsupported = (code: string, path: string, message: string) =>
    makeResult("unsupported", [{ code, severity: "error", path, message }]);
  if (env["kind"] !== "civil") return unsupported("unsupported-operand", "/kind", `semantic validation of kind "${String(env["kind"])}" is not implemented`);
  const cal = value["calendar"];
  if (!isObj(cal) || cal["id"] !== "gregory" || cal["chronology"] !== "proleptic") {
    return unsupported("unsupported-calendar", "/value/calendar", "only the proleptic Gregorian calendar is implemented");
  }
  const fields = value["fields"];
  if (!isObj(fields)) return makeResult("invalid", [{ code: "type", severity: "error", path: "/value/fields", message: "fields must be an object" }]);
  const diags: Diagnostic[] = [];
  const bad = (f: string, message: string) =>
    diags.push({ code: f === "month" ? "month-out-of-range" : f === "day" ? "day-out-of-range" : "type", severity: "error", path: `/value/fields/${f}`, message });
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

/** Days since 1970-01-01 for a proleptic Gregorian date (exact for any `bigint` year). */
export function daysFromCivil(year: bigint, month: number, day: number): bigint {
  const y = month <= 2 ? year - 1n : year;
  const era = (y >= 0n ? y : y - 399n) / 400n;
  const yoe = y - era * 400n;
  const mp = BigInt((month + 9) % 12);
  const doy = (153n * mp + 2n) / 5n + BigInt(day) - 1n;
  const doe = yoe * 365n + yoe / 4n - yoe / 100n + doy;
  return era * 146097n + doe - 719468n;
}

/** Inverse of {@link daysFromCivil}. */
export function civilFromDays(days: bigint): { year: bigint; month: number; day: number } {
  const z = days + 719468n;
  const era = (z >= 0n ? z : z - 146096n) / 146097n;
  const doe = z - era * 146097n;
  const yoe = (doe - doe / 1460n + doe / 36524n - doe / 146096n) / 365n;
  const doy = doe - (365n * yoe + yoe / 4n - yoe / 100n);
  const mp = (5n * doy + 2n) / 153n;
  const day = Number(doy - (153n * mp + 2n) / 5n + 1n);
  const month = Number(mp < 10n ? mp + 3n : mp - 9n);
  return { year: yoe + era * 400n + (month <= 2 ? 1n : 0n), month, day };
}
