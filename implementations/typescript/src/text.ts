/**
 * The UTV text form (docs/text-form.md, profile `utv-text/1`): `parseText` and `formatText`.
 *
 * Points are exactly what {@link parseIso} / {@link formatIso} handle. On top of them come ISO 8601
 * periods (`P1M`, `-P1DT2H`) and intervals in bracket notation (`[2026-11-01, 2026-12-01)`, with `..`
 * for an unbounded end and `?` for an unknown one). EDTF slash intervals are accepted on input.
 */
import type { Json } from "./json.js";
import type { Diagnostic, UtvResult } from "./result.js";
import { SCHEMA } from "./builders.js";
import { formatIso, parseIso } from "./iso.js";
import { compare } from "./ops.js";
import { COMPONENTS, envelope, fail, isObj, readAmount, run, unique, Fail, type Obj, type Outcome } from "./internal.js";

const syntax = (message: string): never => fail("invalid", "syntax", "", message);
const noTextForm = (path: string, message: string): never => fail("unsupported", "no-text-form", path, message);

/** Ends a text-form operation with the outcome of an inner one (a point that did not parse or format). */
function pass(r: UtvResult): never {
  const d: Diagnostic = r.diagnostics[0] ?? { code: r.status, severity: "error", path: "", message: r.status };
  throw new Fail(r.status, d);
}

// ---------------------------------------------------------------------------------------------
// parseText

const PERIOD = /^(-)?P(?:(\d+)Y)?(?:(\d+)M)?(?:(\d+)W)?(?:(\d+)D)?(?:T(?=\d)(?:(\d+)H)?(?:(\d+)M)?(?:(\d+)S)?)?$/;
const N = String.raw`\d+(?:[.,]\d+)?`;
const DECIMAL_PERIOD = new RegExp(`^-?P(?:${N}Y)?(?:${N}M)?(?:${N}W)?(?:${N}D)?(?:T(?=\\d)(?:${N}H)?(?:${N}M)?(?:${N}S)?)?$`);
/** EDTF dates with a qualifier (`1347~`, `1347?`, `2004-06%`) or unspecified digits (`134X`). */
const EDTF_QUALIFIED = /^[+-]?(?=.*\d)(?=.*[~?%X])[\dX~?%-]+$/;

type Endpoint = { state: "included" | "excluded"; point: Obj } | { state: "unbounded" | "unknown" };

/** Parses any value in the text form: a point, a period or an interval. */
export function parseText(text: string): UtvResult {
  return run("parseText", () => {
    if (typeof text !== "string") fail("invalid", "type", "", "parseText takes a string");
    if (text.startsWith("[") || text.startsWith("(")) return unique(bracketInterval(text));
    const slash = slashAt(text);
    if (slash !== undefined) return unique(edtfInterval(text, slash));
    if (text.startsWith("P") || text.startsWith("-P")) return unique(periodOf(text));
    if (EDTF_QUALIFIED.test(text)) fail("unsupported", "unsupported-form", "", "EDTF qualifiers (~ ? % X) are not mapped to UTV yet");
    const r = parseIso(text);
    return r.status === "unique" ? unique(r.candidates[0]!) : pass(r);
  });
}

function periodOf(text: string): Obj {
  const m = PERIOD.exec(text);
  if (m === null) {
    if (DECIMAL_PERIOD.test(text) && /\d/.test(text)) fail("unsupported", "unsupported-form", "", "period components are whole numbers; decimal fractions are not supported");
    return syntax(`not an ISO 8601 period: ${JSON.stringify(text)}`);
  }
  const values = m.slice(2, 9);
  if (values.every((v) => v === undefined)) syntax("a period needs at least one component, e.g. P0D");
  const sign = m[1] === "-" ? -1n : 1n;
  const components: Obj = {};
  COMPONENTS.forEach((k, i) => {
    const v = values[i];
    if (v !== undefined && BigInt(v) !== 0n) components[k] = (sign * BigInt(v)).toString();
  });
  return { schema: SCHEMA, kind: "amount", value: { basis: "calendar", components } };
}

/** The index of the single `/` outside an RFC 9557 `[…]` annotation, if there is one. */
function slashAt(text: string): number | undefined {
  let depth = 0;
  const found: number[] = [];
  for (let i = 0; i < text.length; i++) {
    const ch = text[i];
    if (ch === "[") depth++;
    else if (ch === "]") depth--;
    else if (ch === "/" && depth === 0) found.push(i);
  }
  if (found.length > 1) syntax("an interval has exactly one '/'");
  return found[0];
}

function bracketInterval(text: string): Obj {
  const open = text[0]!, close = text[text.length - 1]!;
  if (close !== "]" && close !== ")") syntax("an interval ends with ']' or ')'");
  const inner = text.slice(1, -1);
  const comma = inner.indexOf(",");
  if (comma < 0) syntax("an interval has two endpoints separated by ', '");
  const a = inner.slice(0, comma), b = inner.slice(comma + 1).replace(/^ /, "");
  return intervalOf(endpoint(a, open, "start"), endpoint(b, close, "end"));
}

function endpoint(t: string, bracket: string, side: "start" | "end"): Endpoint {
  const isOpen = bracket === (side === "start" ? "(" : ")");
  if (t === ".." || t === "?") {
    if (!isOpen) syntax(`an ${t === ".." ? "unbounded" : "unknown"} ${side} sits on an open bracket: ${side === "start" ? "(" : ")"}`);
    return { state: t === ".." ? "unbounded" : "unknown" };
  }
  return { state: isOpen ? "excluded" : "included", point: point(t) };
}

function point(t: string): Obj {
  const r = parseIso(t);
  if (r.status !== "unique") pass(r);
  return r.candidates[0] as Obj;
}

/** EDTF: `a/b` is closed; `..` is an unbounded end and an empty side is an unknown one. */
function edtfInterval(text: string, slash: number): Obj {
  const side = (t: string): Endpoint =>
    t === "" ? { state: "unknown" } : t === ".." ? { state: "unbounded" } : { state: "included", point: point(t) };
  return intervalOf(side(text.slice(0, slash)), side(text.slice(slash + 1)));
}

/** What must match for two points to share an interval: kind, civil form and zone type. */
function shape(p: Obj): string {
  const v = p["value"] as Obj;
  if (p["kind"] !== "civil") return String(p["kind"]);
  const zone = isObj(v["zone"]) ? String(v["zone"]["type"]) : "floating";
  return `civil ${String(v["form"])} ${zone}`;
}

function intervalOf(start: Endpoint, end: Endpoint): Obj {
  const points = [start, end].flatMap((e) => ("point" in e ? [e.point] : []));
  if (points.length === 0) fail("unsupported", "unsupported-form", "", "an interval with no point has no domain");
  const [a, b] = points as [Obj, Obj | undefined];
  if (b !== undefined) {
    if (shape(a) !== shape(b)) fail("invalid", "kind-mismatch", "", `cannot bound an interval by a ${shape(a)} and a ${shape(b)}`);
    if (compare(a, b).candidates[0] === "after") fail("invalid", "reversed-interval", "", "the end is before the start");
  }
  const domain = a["kind"] === "instant" ? "physical" : "civil/gregory";
  return { schema: SCHEMA, kind: "interval", value: { domain, start, end } };
}

// ---------------------------------------------------------------------------------------------
// formatText

/** Formats a value in the text form, or `unsupported` (`no-text-form`) when the notation can't express it. */
export function formatText(value: Json): UtvResult {
  return run("formatText", () => {
    const { kind, value: v } = envelope(value, "");
    if (kind === "civil" || kind === "instant") return formatted(formatIso(value));
    if (kind === "amount") return unique(formatPeriod(value, v));
    if (kind === "interval") return unique(formatInterval(v));
    return noTextForm("/kind", `a ${kind} value has no text form; write it as JSON`);
  });
}

function formatted(r: UtvResult): Outcome {
  return r.status === "unique" ? unique(r.candidates[0]!) : pass(r);
}

function formatPeriod(value: Json, v: Obj): string {
  if (v["basis"] !== "calendar" || "sequence" in v) {
    noTextForm("/value/basis", "only calendar periods have a text form; exact seconds have a display string only");
  }
  const a = readAmount(value, "");
  if (a.basis !== "calendar") return noTextForm("/value/basis", "only calendar periods have a text form");
  const signs = new Set(COMPONENTS.map((k) => a.c[k]).filter((x) => x !== 0n).map((x) => x < 0n));
  if (signs.size > 1) noTextForm("/value/components", "a period with mixed signs has no ISO 8601 notation");
  const negative = signs.has(true);
  const part = (k: typeof COMPONENTS[number], unit: string) => {
    const x = a.c[k];
    return x === 0n ? "" : `${x < 0n ? -x : x}${unit}`;
  };
  const date = part("years", "Y") + part("months", "M") + part("weeks", "W") + part("days", "D");
  const time = part("hours", "H") + part("minutes", "M") + part("seconds", "S");
  const body = date + (time === "" ? "" : `T${time}`);
  return `${negative ? "-" : ""}P${body === "" ? "0D" : body}`;
}

function formatInterval(v: Obj): string {
  const construction = v["construction"];
  if ((construction !== undefined && construction !== "start-end") || "amount" in v) {
    noTextForm("/value/construction", "only start-end intervals have a text form");
  }
  if (v["empty"] === true) noTextForm("/value/empty", "an interval marked empty has no text form");
  const domain = v["domain"];
  if (domain !== "civil/gregory" && domain !== "physical") noTextForm("/value/domain", `interval domain ${JSON.stringify(domain)} has no text form`);
  const side = (name: "start" | "end") => {
    const e = v[name];
    const path = `/value/${name}`;
    if (!isObj(e)) return fail("invalid", "type", path, "an endpoint must be an object");
    const state = e["state"];
    if (state === "unbounded" || state === "unknown") {
      return { text: state === "unbounded" ? ".." : "?", bracket: name === "start" ? "(" : ")", point: undefined };
    }
    if (state !== "included" && state !== "excluded") return fail("invalid", "type", `${path}/state`, `unknown endpoint state ${JSON.stringify(state)}`);
    const p = e["point"];
    if (!isObj(p) || p["kind"] !== (domain === "physical" ? "instant" : "civil")) noTextForm(`${path}/point`, `the point does not belong to domain ${String(domain)}`);
    const r = formatIso(p as Obj);
    if (r.status !== "unique") pass(r);
    const bracket = name === "start" ? (state === "included" ? "[" : "(") : (state === "included" ? "]" : ")");
    return { text: String(r.candidates[0]), bracket, point: p as Obj };
  };
  const s = side("start"), e = side("end");
  if (s.point !== undefined && e.point !== undefined) {
    if (shape(s.point) !== shape(e.point)) noTextForm("/value", "the endpoints are of different kinds or forms");
    if (compare(s.point, e.point).candidates[0] === "after") fail("invalid", "reversed-interval", "/value", "the end is before the start");
  }
  return `${s.bracket}${s.text}, ${e.text}${e.bracket}`;
}
