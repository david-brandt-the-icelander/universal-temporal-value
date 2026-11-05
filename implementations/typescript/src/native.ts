/**
 * Explicit adapters between UTV values and JavaScript's own types (docs/everyday-api.md, "Native interop").
 *
 * - The caller names the intent: a `Date` becomes an instant, or a date-time at an offset or in a named zone.
 * - Nothing is read in the host's local zone: `"local"` is refused with `unsupported`.
 * - Conversions back to native types report loss (sub-millisecond precision, a dropped offset) or are
 *   `unsupported` when the native type cannot hold the value.
 *
 * `Temporal` would be the natural native target, but it is not available by default in Node 22.
 */
import type { Json } from "./json.js";
import { makeResult, type Diagnostic, type LossClassification, type Status, type UtvResult } from "./result.js";
import { date as buildDate } from "./builders.js";
import { floorDiv, rat, split, sub, type Rational } from "./rational.js";
import { envelope, fail, readCivil, readInstant, run, unique, wallSeconds, writeInstant, Fail } from "./internal.js";
import { toOffset, toZone, type ZoneRules } from "./zones.js";

/** Milliseconds a `Date` can hold: ±8.64e15 (±100,000,000 days around 1970). */
const DATE_LIMIT_MS = 8_640_000_000_000_000n;

/** The outcome of a conversion to a native type. `value` is set when `status` is `unique`. */
export interface NativeResult<T> {
  status: Status;
  value?: T;
  loss: { classification: LossClassification; fields: string[] };
  diagnostics: Diagnostic[];
}

function native<T>(body: () => { value: T; lossFields?: string[]; diagnostics?: Diagnostic[] }): NativeResult<T> {
  try {
    const { value, lossFields = [], diagnostics = [] } = body();
    return { status: "unique", value, loss: { classification: lossFields.length ? "information-losing" : "lossless", fields: lossFields }, diagnostics };
  } catch (e) {
    if (e instanceof Fail) return { status: e.status, loss: { classification: "lossless", fields: [] }, diagnostics: [e.diagnostic] };
    return { status: "error", loss: { classification: "lossless", fields: [] }, diagnostics: [{ code: "internal-error", severity: "error", path: "", message: String(e) }] };
  }
}

const msToSeconds = (ms: bigint): Rational => rat(ms, 1000n);

// ---------------------------------------------------------------------------------------------
// Date

/**
 * What a `Date` should become:
 * - `"instant"`: the instant it holds (a `Date` is a UTC millisecond count);
 * - `{ offsetSeconds }`: the civil date-time at that fixed offset;
 * - `{ zone, rules }`: the civil date-time in a named zone, using explicit rules;
 * - `"local"`: refused. Reading the host's zone would make the result depend on the machine.
 */
export type DateIntent = "instant" | "local" | { offsetSeconds: number } | { zone: string; rules: ZoneRules | string | Json };

/** Converts a `Date` with an explicit intent. Never reads the host zone. */
export function fromDate(d: Date, intent: DateIntent): UtvResult {
  const op = "fromDate";
  if (!(d instanceof Date) || Number.isNaN(d.getTime())) {
    return makeResult("invalid", [{ code: "invalid-date", severity: "error", path: "/0", message: "not a valid Date (Invalid Date)" }], { operation: op });
  }
  const inst = writeInstant(msToSeconds(BigInt(d.getTime())));
  if (intent === "instant") return makeResult("unique", [], { operation: op, candidates: [inst] });
  if (typeof intent === "object" && intent !== null && "offsetSeconds" in intent) return { ...toOffset(inst, intent.offsetSeconds), operation: op };
  if (typeof intent === "object" && intent !== null && "zone" in intent) return { ...toZone(inst, intent.zone, intent.rules), operation: op };
  return makeResult("unsupported", [{
    code: "host-zone-refused", severity: "error", path: "/1",
    message: "a local civil reading of a Date depends on the host's time zone; name a zone ({zone, rules}) or an offset ({offsetSeconds}) instead",
  }], { operation: op });
}

/**
 * Converts an instant, or a fixed-offset date-time, to a `Date`. Sub-millisecond precision is floored
 * and reported as loss; a fixed offset is reported as lost (a `Date` keeps only the instant). Zoned and
 * floating date-times are `unsupported`: resolve them to an instant first.
 */
export function toDate(value: Json): NativeResult<Date> {
  return native(() => {
    const { kind } = envelope(value, "");
    const lossFields: string[] = [];
    let t: Rational;
    if (kind === "instant") t = readInstant(value, "");
    else if (kind === "civil") {
      const c = readCivil(value, "");
      if (c.form !== "datetime" || c.zone.type !== "fixed-offset") {
        fail("unsupported", "resolve-first", "/value/zone", c.zone.type === "iana"
          ? "a Date keeps no zone, so this zoned intention would be lost; resolve it with explicit rules first"
          : "only an instant or a fixed-offset date-time names one moment; a floating value would need the host zone");
      }
      t = sub(wallSeconds(c), rat(BigInt(c.zone.seconds)));
      lossFields.push("zone");
    } else return fail("unsupported", "unsupported-kind", "/kind", `a Date cannot hold kind "${kind}"`);
    const scaled = rat(t.n * 1000n, t.d);
    const ms = floorDiv(scaled.n, scaled.d);
    if (ms * scaled.d !== scaled.n) lossFields.push("subTick");
    if (ms > DATE_LIMIT_MS || ms < -DATE_LIMIT_MS) fail("unsupported", "out-of-range", "", "outside the range a Date can hold (±8.64e15 ms)");
    return { value: new Date(Number(ms)), lossFields };
  });
}

// ---------------------------------------------------------------------------------------------
// epoch milliseconds

/** An instant from a count of milliseconds since 1970-01-01T00:00:00Z (`Date.now()`, `date.getTime()`). */
export function fromEpochMilliseconds(ms: number | bigint): UtvResult {
  return run("fromEpochMilliseconds", () => {
    if (typeof ms === "number" && !Number.isSafeInteger(ms)) fail("invalid", "not-an-integer", "/0", "epoch milliseconds must be a safe integer (or a bigint)");
    return unique(writeInstant(msToSeconds(BigInt(ms))));
  });
}

/** Milliseconds since the epoch. Sub-millisecond precision is floored and reported as loss. */
export function toEpochMilliseconds(value: Json): NativeResult<number> {
  return native(() => {
    const t = readInstant(value, "");
    const { whole, frac } = split(rat(t.n * 1000n, t.d));
    if (whole > BigInt(Number.MAX_SAFE_INTEGER) || whole < -BigInt(Number.MAX_SAFE_INTEGER)) fail("unsupported", "out-of-range", "", "does not fit a safe integer number of milliseconds");
    return { value: Number(whole), lossFields: frac.n === 0n ? [] : ["subTick"] };
  });
}

// ---------------------------------------------------------------------------------------------
// plain { year, month, day }

/** A plain calendar date object. Months are 1-based (unlike `Date`). */
export interface PlainDate { year: number; month: number; day: number }

/** A UTV date from a plain object. The date is checked: 30 February is `invalid`, never rolled over. */
export function fromPlainDate(p: PlainDate): UtvResult {
  return run("fromPlainDate", () => {
    if (typeof p !== "object" || p === null || ![p.year, p.month, p.day].every(Number.isSafeInteger)) fail("invalid", "type", "/0", "year, month and day must be integers");
    const v = buildDate(p.year, p.month, p.day);
    readCivil(v, "/0");
    return unique(v);
  });
}

/** A plain object from a UTV date. Years beyond a safe integer are `unsupported`. */
export function toPlainDate(value: Json): NativeResult<PlainDate> {
  return native(() => {
    const { kind } = envelope(value, "");
    if (kind !== "civil") fail("unsupported", "unsupported-kind", "/kind", "expected a civil date");
    const c = readCivil(value, "");
    if (c.form !== "date") fail("unsupported", "unsupported-form", "/value/form", `expected a date, got a ${c.form}`);
    if (c.zone.type !== "floating") fail("unsupported", "zone-would-be-lost", "/value/zone", "a plain date object has no zone");
    if (c.year > BigInt(Number.MAX_SAFE_INTEGER) || c.year < -BigInt(Number.MAX_SAFE_INTEGER)) fail("unsupported", "out-of-range", "/value/fields/year", "year does not fit a safe integer");
    return { value: { year: Number(c.year), month: c.month, day: c.day } };
  });
}
