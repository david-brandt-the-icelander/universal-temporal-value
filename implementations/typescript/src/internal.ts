/** Internal helpers shared by the everyday operations: input readers, writers and the result wrapper. */
import type { Json } from "./json.js";
import { makeResult, type Diagnostic, type LossClassification, type Status, type UtvResult } from "./result.js";
import { validateParsed } from "./structural.js";
import { daysInMonth, daysFromCivil } from "./civil.js";
import { GREGORY, POSIX, SCHEMA } from "./builders.js";
import { INT, rat, split, type Rational } from "./rational.js";

export type Obj = { [k: string]: Json };
export const isObj = (v: Json | undefined): v is Obj => typeof v === "object" && v !== null && !Array.isArray(v);

/** Canonical JSON text (object keys sorted), used for equality of JSON values. */
export function canonical(v: Json | undefined): string {
  if (Array.isArray(v)) return `[${v.map(canonical).join(",")}]`;
  if (isObj(v)) return `{${Object.keys(v).sort().map((k) => `${JSON.stringify(k)}:${canonical(v[k])}`).join(",")}}`;
  return JSON.stringify(v ?? null);
}
export const jsonEqual = (a: Json | undefined, b: Json | undefined) => canonical(a) === canonical(b);

// ---------------------------------------------------------------------------------------------
// Outcomes

/** Thrown inside an operation to end it with a non-candidate outcome. Never escapes {@link run}. */
export class Fail {
  constructor(readonly status: Status, readonly diagnostic: Diagnostic) {}
}
export function fail(status: Status, code: string, path: string, message: string): never {
  throw new Fail(status, { code, severity: "error", path, message });
}

export interface Outcome {
  status: Status;
  candidates?: Json[];
  diagnostics?: Diagnostic[];
  dependencies?: Json[];
  loss?: { classification: LossClassification; fields: string[] };
}

/** Runs an operation body; semantic outcomes and unexpected errors both become results, never exceptions. */
export function run(operation: string, body: () => Outcome): UtvResult {
  try {
    const o = body();
    return makeResult(o.status, o.diagnostics ?? [], { operation, candidates: o.candidates ?? [], dependencies: o.dependencies ?? [], loss: o.loss });
  } catch (e) {
    if (e instanceof Fail) return makeResult(e.status, [e.diagnostic], { operation });
    return makeResult("error", [{ code: "internal-error", severity: "error", path: "", message: String(e) }], { operation });
  }
}
export const unique = (c: Json, extra: Omit<Outcome, "status" | "candidates"> = {}): Outcome => ({ status: "unique", candidates: [c], ...extra });

// ---------------------------------------------------------------------------------------------
// Readers

/** Checks a UTV value structurally (fail closed) and returns its kind and `value` object. */
export function envelope(v: Json | undefined, path: string): { kind: string; value: Obj } {
  if (!isObj(v)) fail("invalid", "not-a-value", path, "expected a UTV value (a JSON object)");
  const r = validateParsed(v);
  if (r.status !== "valid") {
    const d = r.diagnostics[0]!;
    throw new Fail(r.status, { ...d, path: path + d.path });
  }
  return { kind: v["kind"] as string, value: v["value"] as Obj };
}

export type Zone = { type: "floating" } | { type: "iana"; id: string } | { type: "fixed-offset"; seconds: number };
export type Form = "date" | "year-month" | "time" | "datetime";

export interface Civil {
  form: Form;
  year: bigint; month: number; day: number;
  hour: number; minute: number; second: number; nano: number;
  hasSecond: boolean;
  zone: Zone;
  offsetAssertion?: number;
  resolutionPolicy?: Obj;
}

const FORM_FIELDS: Record<Form, { required: string[]; optional: string[] }> = {
  date: { required: ["year", "month", "day"], optional: [] },
  "year-month": { required: ["year", "month"], optional: [] },
  time: { required: ["hour", "minute"], optional: ["second", "nano"] },
  datetime: { required: ["year", "month", "day", "hour", "minute"], optional: ["second", "nano"] },
};
export const MAX_OFFSET = 18 * 3600;
const isInt = (x: Json | undefined): x is number => typeof x === "number" && Number.isInteger(x);

/** Reads a civil value: proleptic Gregorian only, every field checked. */
export function readCivil(v: Json | undefined, path: string): Civil {
  const { kind, value } = envelope(v, path);
  const vp = `${path}/value`;
  if (kind !== "civil") fail("unsupported", "unsupported-operand", `${path}/kind`, `expected a civil value, got kind "${kind}"`);
  if (!jsonEqual(value["calendar"], GREGORY)) fail("unsupported", "unsupported-calendar", `${vp}/calendar`, "only the proleptic Gregorian calendar is implemented");
  const form = value["form"];
  if (typeof form !== "string" || !Object.hasOwn(FORM_FIELDS, form)) fail("unsupported", "unsupported-operand", `${vp}/form`, `civil form ${JSON.stringify(form)} is not implemented`);
  for (const p of ["location", "quality"]) if (p in value) fail("unsupported", "unsupported-operand", `${vp}/${p}`, `civil "${p}" is not implemented`);
  const fields = value["fields"];
  const fp = `${vp}/fields`;
  if (!isObj(fields)) fail("invalid", "type", fp, "fields must be an object");
  const spec = FORM_FIELDS[form as Form];
  for (const k of Object.keys(fields)) if (!spec.required.includes(k) && !spec.optional.includes(k)) fail("invalid", "unexpected-field", `${fp}/${k}`, `a ${form} has no field "${k}"`);
  for (const k of spec.required) if (!(k in fields)) fail("invalid", "missing-field", `${fp}/${k}`, `a ${form} needs field "${k}"`);
  const range = (k: string, lo: number, hi: number, dflt: number): number => {
    const x = fields[k];
    if (x === undefined) return dflt;
    const code = k === "month" ? "month-out-of-range" : k === "day" ? "day-out-of-range" : "field-out-of-range";
    if (!isInt(x) || x < lo || x > hi) fail("invalid", code, `${fp}/${k}`, `${k} must be an integer ${lo}..${hi}`);
    return x;
  };
  let year = 0n;
  if ("year" in fields) {
    const y = fields["year"];
    if (typeof y !== "string" || !INT.test(y)) fail("invalid", "type", `${fp}/year`, "year must be a decimal integer string");
    year = BigInt(y);
  }
  const month = range("month", 1, 12, 1);
  const day = range("day", 1, 31, 1);
  if ("day" in fields && day > daysInMonth(year, month)) fail("invalid", "day-out-of-range", `${fp}/day`, `${year}-${String(month).padStart(2, "0")} has no day ${day}`);
  const hour = range("hour", 0, 23, 0);
  const minute = range("minute", 0, 59, 0);
  const second = range("second", 0, 60, 0);
  const nano = range("nano", 0, 999_999_999, 0);
  if ("nano" in fields && !("second" in fields)) fail("invalid", "missing-field", `${fp}/second`, "nano needs a second field");
  if (second === 60) fail("unsupported", "leap-second", `${fp}/second`, "second 60 needs a leap-second table");
  const civil: Civil = { form: form as Form, year, month, day, hour, minute, second, nano, hasSecond: "second" in fields, zone: readZone(value, vp) };
  const oa = value["offsetAssertion"];
  if (oa !== undefined) {
    if (!isInt(oa) || Math.abs(oa) > MAX_OFFSET) fail("invalid", "offset-out-of-range", `${vp}/offsetAssertion`, "offsetAssertion must be whole seconds within ±18:00");
    civil.offsetAssertion = oa;
  }
  const rp = value["resolutionPolicy"];
  if (rp !== undefined) {
    if (!isObj(rp)) fail("invalid", "type", `${vp}/resolutionPolicy`, "resolutionPolicy must be an object");
    civil.resolutionPolicy = rp;
  }
  return civil;
}

function readZone(value: Obj, vp: string): Zone {
  const z = value["zone"], intent = value["zoneIntent"];
  const intentIs = (...ok: string[]) => {
    if (intent !== undefined && !ok.includes(intent as string)) fail("unsupported", "unsupported-operand", `${vp}/zoneIntent`, `zoneIntent ${JSON.stringify(intent)} is not implemented for this zone`);
  };
  if (z === undefined || (isObj(z) && z["type"] === "floating")) { intentIs("floating-wall"); return { type: "floating" }; }
  if (!isObj(z)) fail("invalid", "type", `${vp}/zone`, "zone must be an object");
  if ("rules" in z) fail("unsupported", "unsupported-operand", `${vp}/zone/rules`, "zone-bound rules are not implemented; pass rules to the operation");
  if (z["type"] === "iana" && typeof z["id"] === "string" && !("seconds" in z)) { intentIs("follow-zone-id"); return { type: "iana", id: z["id"] }; }
  if (z["type"] === "fixed-offset" && !("id" in z)) {
    const s = z["seconds"];
    if (!isInt(s) || Math.abs(s) > MAX_OFFSET) fail("invalid", "offset-out-of-range", `${vp}/zone/seconds`, "offset must be whole seconds within ±18:00");
    intentIs("fixed-offset");
    return { type: "fixed-offset", seconds: s };
  }
  return fail("unsupported", "unsupported-operand", `${vp}/zone`, `zone ${canonical(z)} is not implemented`);
}

/** Reads a POSIX instant as exact seconds since the epoch. */
export function readInstant(v: Json | undefined, path: string): Rational {
  const { kind, value } = envelope(v, path);
  const vp = `${path}/value`;
  if (kind !== "instant") fail("unsupported", "unsupported-operand", `${path}/kind`, `expected an instant, got kind "${kind}"`);
  if (!jsonEqual(value["system"], POSIX)) fail("unsupported", "unsupported-operand", `${vp}/system`, "only the unix-posix timescale is implemented");
  for (const p of ["coordinate", "quality"]) if (p in value) fail("unsupported", "unsupported-operand", `${vp}/${p}`, `instant "${p}" is not implemented`);
  const t = value["ticks"];
  if (typeof t !== "string" || !INT.test(t)) fail("invalid", "type", `${vp}/ticks`, "ticks must be a decimal integer string");
  if (value["subTick"] === undefined) return rat(BigInt(t));
  const f = readRational(value["subTick"], `${vp}/subTick`);
  return rat(BigInt(t) * f.d + f.n, f.d);
}

export function readRational(v: Json | undefined, path: string): Rational {
  if (!isObj(v)) fail("invalid", "type", path, "expected a rational {numerator, denominator}");
  const n = v["numerator"], d = v["denominator"];
  if (typeof n !== "string" || !INT.test(n) || typeof d !== "string" || !/^[1-9][0-9]*$/.test(d)) fail("invalid", "type", path, "numerator and denominator must be decimal integer strings, denominator positive");
  return rat(BigInt(n), BigInt(d));
}

export const COMPONENTS = ["years", "months", "weeks", "days", "hours", "minutes", "seconds"] as const;
export type Component = typeof COMPONENTS[number];
export type Amount =
  | { basis: "calendar"; c: Record<Component, bigint> }
  | { basis: "clock-ticks"; seconds: Rational }
  | { basis: "elapsed" };

export function readAmount(v: Json | undefined, path: string): Amount {
  const { kind, value } = envelope(v, path);
  const vp = `${path}/value`;
  if (kind !== "amount") fail("invalid", "kind-mismatch", `${path}/kind`, `expected an amount, got kind "${kind}"`);
  if ("sequence" in value) fail("unsupported", "unsupported-operand", `${vp}/sequence`, "amount sequences are not implemented");
  const basis = value["basis"];
  if (basis === "calendar") {
    const comps = value["components"];
    if (!isObj(comps)) fail("invalid", "type", `${vp}/components`, "a calendar amount needs a components object");
    const c = Object.fromEntries(COMPONENTS.map((k) => [k, 0n])) as Record<Component, bigint>;
    for (const [k, x] of Object.entries(comps)) {
      const cp = `${vp}/components/${k}`;
      if (!(COMPONENTS as readonly string[]).includes(k)) fail("invalid", "unknown-component", cp, `unknown calendar component "${k}"`);
      if (typeof x === "string" && /^-?[0-9]+\.[0-9]+$/.test(x)) fail("unsupported", "unsupported-operand", cp, "fractional components are not implemented");
      if (typeof x !== "string" || !INT.test(x)) fail("invalid", "type", cp, "components are decimal integer strings");
      c[k as Component] = BigInt(x);
    }
    return { basis, c };
  }
  if (basis === "clock-ticks") {
    if (!jsonEqual(value["rules"], POSIX)) fail("unsupported", "unsupported-operand", `${vp}/rules`, "only unix-posix clock ticks are implemented");
    return { basis, seconds: readRational(value["seconds"], `${vp}/seconds`) };
  }
  if (basis === "elapsed") return { basis };
  return fail("unsupported", "unsupported-operand", `${vp}/basis`, `amount basis ${JSON.stringify(basis)} is not implemented`);
}

// ---------------------------------------------------------------------------------------------
// Writers

export function writeCivil(c: Civil, extra: Obj = {}): Obj {
  const f: Obj = {};
  if (c.form !== "time") { f["year"] = c.year.toString(); f["month"] = c.month; }
  if (c.form === "date" || c.form === "datetime") f["day"] = c.day;
  if (c.form === "time" || c.form === "datetime") {
    f["hour"] = c.hour; f["minute"] = c.minute;
    if (c.hasSecond) f["second"] = c.second;
    if (c.nano !== 0) f["nano"] = c.nano;
  }
  const value: Obj = { form: c.form, fields: f, calendar: { ...GREGORY } };
  if (c.zone.type === "iana") { value["zone"] = { type: "iana", id: c.zone.id }; value["zoneIntent"] = "follow-zone-id"; }
  if (c.zone.type === "fixed-offset") { value["zone"] = { type: "fixed-offset", seconds: c.zone.seconds }; value["zoneIntent"] = "fixed-offset"; }
  if (c.offsetAssertion !== undefined) value["offsetAssertion"] = c.offsetAssertion;
  if (c.resolutionPolicy !== undefined) value["resolutionPolicy"] = c.resolutionPolicy;
  return { schema: SCHEMA, kind: "civil", value: { ...value, ...extra } };
}

export function writeInstant(t: Rational): Obj {
  const { whole, frac } = split(t);
  const value: Obj = { system: { ...POSIX }, ticks: whole.toString() };
  if (frac.n !== 0n) value["subTick"] = { numerator: frac.n.toString(), denominator: frac.d.toString() };
  return { schema: SCHEMA, kind: "instant", value };
}

export function writePosixSeconds(r: Rational): Obj {
  return { schema: SCHEMA, kind: "amount", value: { basis: "clock-ticks", rules: { ...POSIX }, seconds: { numerator: r.n.toString(), denominator: r.d.toString() } } };
}

/** The wall clock of a civil date-time read as if it were UTC, in exact seconds (with `nano`). */
export function wallSeconds(c: Civil): Rational {
  const s = daysFromCivil(c.year, c.month, c.day) * 86400n + BigInt(c.hour * 3600 + c.minute * 60 + c.second);
  return rat(s * 1_000_000_000n + BigInt(c.nano), 1_000_000_000n);
}
