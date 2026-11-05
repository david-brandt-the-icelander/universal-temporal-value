import { parseStrict, JsonError, type Json } from "./json.js";
import { makeResult, type Diagnostic, type UtvResult } from "./result.js";

type Obj = { [k: string]: Json };
const isObj = (v: Json | undefined): v is Obj => typeof v === "object" && v !== null && !Array.isArray(v);

interface KindSpec { required: string[]; allowed: string[] }
const k = (required: string[], optional: string[]): KindSpec => ({ required, allowed: [...required, ...optional] });

/** Per-kind `value` members, transcribed from schema/utv-1.0.schema.json. */
export const KINDS: Readonly<Record<string, KindSpec>> = {
  instant: k(["system"], ["ticks", "subTick", "coordinate", "quality"]),
  civil: k(["form", "fields", "calendar"], ["zone", "zoneIntent", "resolutionPolicy", "offsetAssertion", "location", "quality"]),
  amount: k(["basis"], ["seconds", "components", "rules", "sequence"]),
  interval: k(["domain"], ["start", "end", "empty", "construction", "amount", "arithmeticPolicy"]),
  recurrence: k(["intent", "algorithm", "rule"], ["resolutionPolicy", "zoneRuleBinding", "exceptions", "duration", "inclusionRules", "exclusionRules", "inclusionDates", "exclusionDates", "overrides", "recurrenceIdentity", "adapterEdition"]),
  uncertain: k(["target", "model"], ["parameters", "quality"]),
  set: k(["operation", "members"], ["universe"]),
  clock: k(["clockRef", "generation", "ticks", "tickUnit"], ["quality"]),
  observation: k(["role", "reading", "source"], ["quality", "latency"]),
  "logical-clock": k(["algorithm", "actorNamespace", "generation", "state"], []),
  relative: k(["language", "expression", "anchor", "context"], []),
  relation: k(["relation", "left", "right"], ["domain"]),
  assertion: k(["assertionId", "subjectId", "claimedTime", "source"], ["recordedAt"]),
  "event-history": k(["subjectId", "revisions"], []),
  "location-intent": k(["authority", "place"], ["mapping", "time"]),
  constraint: k(["language", "variables", "expression"], []),
  "calendar-rule": k(["rules", "intent"], []),
  "temporal-reference": k(["reference", "coordinate"], ["frame"]),
  "ordinal-position": k(["system", "label"], ["sourceTerm", "relationContext"]),
  "event-relative": k(["anchor", "matching", "relation"], ["offset", "window", "fallback", "cardinality", "referenceContext"]),
  selection: k(["source", "predicate", "take"], ["orderBy", "index", "indexBase", "count", "cardinality", "algorithm"]),
  sample: k(["subject", "phenomenonTime", "payload"], ["resultTime", "validTime", "samplingTime", "acquisitionTime", "processingTime", "publicationTime", "quality", "timeCoordinates", "kindOfSupport", "metadata"]),
  series: k(["id", "temporalAxis"], ["subject", "samples", "chunks", "measurement", "ordering", "samplingModel", "interpolation", "aggregation", "missingData", "quality", "snapshot", "watermark"]),
  "temporal-graph": k(["nodes", "edges", "domain"], ["logic"]),
  sequence: k(["ordering", "cardinality"], ["members", "generator", "duplicatePolicy", "domain"]),
  pattern: k(["frequency", "per"], ["spacing", "window", "onEvent", "completionCriterion", "ontology", "constraints"]),
};

const ENVELOPE_KEYS = new Set(["schema", "kind", "value", "provenance", "extensions"]);

export class Ctx {
  invalid: Diagnostic[] = [];
  unsupported: Diagnostic[] = [];
  err(code: string, path: string, message: string) { this.invalid.push({ code, severity: "error", path, message }); }
}

/** Version check per VERSIONING.md reader rules. Returns false if the value must not be read further. */
function checkSchemaId(s: Json | undefined, path: string, ctx: Ctx): boolean {
  if (s === "utv/1.0") return true;
  // Any well-formed utv/M.N other than utv/1.0 (newer minor or other major) is unsupported, not invalid.
  if (typeof s === "string" && /^utv\/(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)$/.test(s)) {
    ctx.unsupported.push({ code: "unsupported-version", severity: "error", path, message: `this reader implements utv/1.0 only; got ${s}` });
    return false;
  }
  ctx.err("invalid-schema-identifier", path, `schema must be "utv/1.0", got ${JSON.stringify(s)}`);
  return false;
}

/** Validates one UTV envelope at `path`. Optionally requires a given kind. */
export function checkEnvelope(v: Json | undefined, path: string, ctx: Ctx, expectKind?: string): void {
  if (!isObj(v)) { ctx.err("not-an-object", path, "UTV value must be a JSON object"); return; }
  for (const key of Object.keys(v)) {
    if (!ENVELOPE_KEYS.has(key)) ctx.err("unknown-property", `${path}/${key}`, `unknown top-level property "${key}"`);
  }
  for (const key of ["schema", "kind", "value"]) {
    if (!(key in v)) ctx.err("missing-property", `${path}/${key}`, `missing required property "${key}"`);
  }
  if (!checkSchemaId(v["schema"], `${path}/schema`, ctx)) return;
  const kind = v["kind"];
  if (typeof kind !== "string" || !Object.hasOwn(KINDS, kind)) {
    ctx.err("unknown-kind", `${path}/kind`, `kind ${JSON.stringify(kind)} is not one of the 26 core kinds`);
    return;
  }
  if (expectKind !== undefined && kind !== expectKind) ctx.err("wrong-kind", `${path}/kind`, `expected kind "${expectKind}", got "${kind}"`);
  if (v["extensions"] !== undefined && !isObj(v["extensions"])) ctx.err("type", `${path}/extensions`, "extensions must be an object");
  const value = v["value"];
  if (value === undefined) return;
  if (!isObj(value)) { ctx.err("type", `${path}/value`, "value must be an object"); return; }
  const spec = KINDS[kind]!;
  const vp = `${path}/value`;
  for (const r of spec.required) if (!(r in value)) ctx.err("missing-property", `${vp}/${r}`, `${kind}: missing required property "${r}"`);
  for (const key of Object.keys(value)) if (!spec.allowed.includes(key)) ctx.err("unknown-property", `${vp}/${key}`, `${kind}: unknown core property "${key}"`);
  kindRules(kind, value, vp, ctx);
  // Nested envelopes anywhere inside the value must themselves be valid.
  walkNested(value, vp, ctx, kind);
}

function kindRules(kind: string, value: Obj, vp: string, ctx: Ctx): void {
  if (kind === "instant") {
    const t = "ticks" in value, c = "coordinate" in value;
    if (t === c) ctx.err("one-of", vp, "instant: exactly one of ticks or coordinate is required");
  } else if (kind === "interval") {
    const need: Record<string, string[]> = { "start-end": ["start", "end"], "start-amount": ["start", "amount"], "amount-end": ["amount", "end"] };
    const c = value["construction"];
    if (typeof c === "string" && need[c]) {
      for (const r of need[c]!) if (!(r in value)) ctx.err("missing-property", `${vp}/${r}`, `interval construction "${c}" requires "${r}"`);
    } else if (c === undefined) {
      for (const r of ["start", "end"]) if (!(r in value)) ctx.err("missing-property", `${vp}/${r}`, `interval without construction requires "${r}"`);
    } else {
      ctx.err("enum", `${vp}/construction`, `unknown interval construction ${JSON.stringify(c)}`);
    }
  } else if (kind === "series") {
    const s = value["samples"];
    if (s !== undefined) {
      if (!Array.isArray(s)) ctx.err("type", `${vp}/samples`, "samples must be an array");
      else s.forEach((x, i) => checkEnvelope(x, `${vp}/samples/${i}`, ctx, "sample"));
    }
  }
}

const looksLikeEnvelope = (o: Obj) => typeof o["schema"] === "string" && (o["schema"] as string).startsWith("utv/") && "kind" in o;

function walkNested(node: Json, path: string, ctx: Ctx, kind: string, top = true): void {
  if (Array.isArray(node)) { node.forEach((x, i) => walkNested(x, `${path}/${i}`, ctx, kind, false)); return; }
  if (!isObj(node)) return;
  if (!top && looksLikeEnvelope(node)) {
    if (!(kind === "series" && /\/value\/samples\/\d+$/.test(path))) checkEnvelope(node, path, ctx);
    return;
  }
  for (const [key, x] of Object.entries(node)) walkNested(x, `${path}/${key}`, ctx, kind, false);
}

/** Parses UTV JSON text and checks it structurally against utv/1.0. Pure: no clock, zone or locale access. */
export function validateStructural(text: string): UtvResult {
  let doc: Json;
  try {
    doc = parseStrict(text);
  } catch (e) {
    const je = e instanceof JsonError ? e : undefined;
    return makeResult("invalid", [{ code: je?.code ?? "malformed-json", severity: "error", path: je?.path ?? "", message: String((e as Error).message) }]);
  }
  return validateParsed(doc);
}

/** Same as validateStructural, for an already-parsed (duplicate-free) JSON value. */
export function validateParsed(doc: Json): UtvResult {
  const ctx = new Ctx();
  checkEnvelope(doc, "", ctx);
  if (ctx.invalid.length > 0) return makeResult("invalid", [...ctx.invalid, ...ctx.unsupported]);
  if (ctx.unsupported.length > 0) return makeResult("unsupported", ctx.unsupported);
  return makeResult("valid", []);
}
