/** Zone rules and the conversions between local date-times and instants: resolve, toZone, toOffset. */
import { parseStrict, type Json } from "./json.js";
import type { Diagnostic, UtvResult } from "./result.js";
import { civilFromDays } from "./civil.js";
import { add as addR, cmp, floorDiv, mod, rat, split, sub, toNano, type Rational } from "./rational.js";
import {
  canonical, envelope, fail, isObj, readCivil, readInstant, run, unique, wallSeconds, writeCivil, writeInstant,
  MAX_OFFSET, type Civil, type Obj, type Outcome, type Zone,
} from "./internal.js";

export class RulesError extends Error {}

interface Transition { at: bigint; offset: number }
interface ZoneData { initialOffset: number; transitions: Transition[] }

/**
 * A parsed rule bundle in the format of `conformance/rules/demo-zones.json`: per-zone UTC offsets,
 * valid only for `validFrom ≤ t < validUntil`. Nothing outside the window is ever extrapolated.
 */
export class ZoneRules {
  constructor(
    /** `{system, id, version}` of the bundle, reported in result manifests. */
    readonly ruleSet: Json,
    readonly validFrom: bigint,
    readonly validUntil: bigint,
    private readonly zones: ReadonlyMap<string, ZoneData>,
  ) {}

  has(zoneId: string): boolean { return this.zones.has(zoneId); }
  get zoneIds(): string[] { return [...this.zones.keys()]; }
  covers(t: Rational): boolean { return cmp(t, rat(this.validFrom)) >= 0 && cmp(t, rat(this.validUntil)) < 0; }

  /** The UTC offset in force at instant `t` (seconds). The caller checks the zone and window first. */
  offsetAt(zoneId: string, t: Rational): number {
    const z = this.zones.get(zoneId)!;
    let o = z.initialOffset;
    for (const tr of z.transitions) { if (cmp(rat(tr.at), t) <= 0) o = tr.offset; else break; }
    return o;
  }

  /** Transitions of a zone, with the offset in force just before each. */
  transitions(zoneId: string): { at: bigint; before: number; after: number }[] {
    const z = this.zones.get(zoneId)!;
    return z.transitions.map((tr, i) => ({ at: tr.at, before: i === 0 ? z.initialOffset : z.transitions[i - 1]!.offset, after: tr.offset }));
  }
}

/**
 * Parses a rule bundle (JSON text or an already-parsed object). Throws {@link RulesError} if it is
 * malformed. Pure: reading the file is the caller's job.
 */
export function loadRules(source: string | Json): ZoneRules {
  let doc: Json;
  try { doc = typeof source === "string" ? parseStrict(source) : source; } catch (e) { throw new RulesError(`rule bundle is not valid JSON: ${(e as Error).message}`); }
  const need = (ok: boolean, what: string) => { if (!ok) throw new RulesError(`rule bundle: ${what}`); };
  need(isObj(doc), "must be an object");
  const o = doc as Obj;
  const intStr = (v: Json | undefined) => typeof v === "string" && /^-?(0|[1-9][0-9]*)$/.test(v);
  const offset = (v: Json | undefined) => typeof v === "number" && Number.isInteger(v) && Math.abs(v) <= MAX_OFFSET;
  need(isObj(o["ruleSet"]), "ruleSet must be an object");
  need(intStr(o["validFrom"]) && intStr(o["validUntil"]), "validFrom and validUntil must be decimal integer strings");
  need(isObj(o["zones"]), "zones must be an object");
  const zones = new Map<string, ZoneData>();
  for (const [id, z] of Object.entries(o["zones"] as Obj)) {
    need(isObj(z) && offset(z["initialOffset"]) && Array.isArray(z["transitions"]), `zone ${id} needs initialOffset and transitions`);
    const zo = z as Obj;
    const transitions: Transition[] = (zo["transitions"] as Json[]).map((t) => {
      need(isObj(t) && intStr(t["at"]) && offset(t["offset"]), `zone ${id}: each transition needs "at" (integer string) and "offset" (seconds)`);
      return { at: BigInt((t as Obj)["at"] as string), offset: (t as Obj)["offset"] as number };
    });
    for (let i = 1; i < transitions.length; i++) need(transitions[i - 1]!.at < transitions[i]!.at, `zone ${id}: transitions must be strictly ascending`);
    zones.set(id, { initialOffset: zo["initialOffset"] as number, transitions });
  }
  return new ZoneRules(o["ruleSet"]!, BigInt(o["validFrom"] as string), BigInt(o["validUntil"] as string), zones);
}

function rulesArg(rules: ZoneRules | string | Json, path: string): ZoneRules {
  if (rules instanceof ZoneRules) return rules;
  try { return loadRules(rules); } catch (e) { return fail("invalid", "invalid-rules", path, (e as Error).message); }
}

// ---------------------------------------------------------------------------------------------
// resolve

export type GapPolicy = "reject" | "preserve-unresolved" | "shift-forward-by-gap";
export type OverlapPolicy = "reject" | "all" | "earliest" | "latest";
export interface ResolutionPolicy { gap?: GapPolicy; overlap?: OverlapPolicy }

const GAP = ["reject", "preserve-unresolved", "shift-forward-by-gap"];
const OVERLAP = ["reject", "all", "earliest", "latest"];
const DAY = rat(86400n);

/**
 * Turns a local date-time into instant(s) using explicit zone rules. A gap gives `none` and an
 * overlap gives `multiple` unless the policy (argument, else the value's `resolutionPolicy`) says otherwise.
 */
export function resolve(value: Json, rules: ZoneRules | string | Json, policy?: ResolutionPolicy): UtvResult {
  return run("resolve", () => {
    const { kind, value: v } = envelope(value, "/0");
    if (kind !== "civil" || v["form"] !== "datetime") fail("unsupported", "unsupported-form", "/0/value/form", "resolve takes a civil datetime");
    const c = readCivil(value, "/0"); // second 60 is `unsupported` here
    const L = wallSeconds(c);
    if (c.zone.type === "floating") return { status: "symbolic", diagnostics: [{ code: "no-zone", severity: "info", path: "/0/value/zone", message: "no zone is bound, so there is nothing to resolve yet; bind one with inZone or withOffset" }] };
    if (c.zone.type === "fixed-offset") {
      if (c.offsetAssertion !== undefined && c.offsetAssertion !== c.zone.seconds) fail("invalid", "offset-mismatch", "/0/value/offsetAssertion", "offsetAssertion contradicts the fixed offset");
      return unique(writeInstant(sub(L, rat(BigInt(c.zone.seconds)))));
    }
    const zoneId = c.zone.id;
    const r = rulesArg(rules, "/1");
    const dependencies = [r.ruleSet];
    if (!r.has(zoneId)) fail("unsupported", "unknown-zone", "/0/value/zone/id", `zone ${zoneId} is not in rule set ${canonical(r.ruleSet)}`);
    if (!r.covers(sub(L, DAY)) || !r.covers(addR(L, DAY))) fail("unsupported", "outside-rules-window", "/0/value/fields", "the wall time is outside the rule bundle's validity window; never extrapolated");

    if (policy !== undefined && !isObj(policy as Json)) fail("invalid", "type", "/2", "policy must be an object");
    const vp = c.resolutionPolicy ?? {};
    const gap = (policy?.gap ?? vp["gap"] ?? "reject") as string;
    const overlap = (policy?.overlap ?? vp["overlap"] ?? "reject") as string;
    if (!GAP.includes(gap)) fail("unsupported", "unsupported-policy", "/2/gap", `gap policy ${JSON.stringify(gap)} is not implemented`);
    if (!OVERLAP.includes(overlap)) fail("unsupported", "unsupported-policy", "/2/overlap", `overlap policy ${JSON.stringify(overlap)} is not implemented`);

    let candidates = candidatesFor(r, zoneId, L);
    const out = (o: Outcome): Outcome => ({ ...o, dependencies });

    if (c.offsetAssertion !== undefined) {
      candidates = candidates.filter((x) => x.offset === c.offsetAssertion);
      if (candidates.length === 0) fail("invalid", "offset-mismatch", "/0/value/offsetAssertion", `offset ${c.offsetAssertion} is not in force in ${zoneId} at that wall time`);
    }
    if (candidates.length === 1) return out(unique(writeInstant(candidates[0]!.t)));
    if (candidates.length === 0) {
      if (gap === "shift-forward-by-gap") {
        const tr = r.transitions(zoneId).find((x) => cmp(rat(x.at + BigInt(x.before)), L) <= 0 && cmp(L, rat(x.at + BigInt(x.after))) < 0)!;
        return out(unique(writeInstant(sub(L, rat(BigInt(tr.before))))));
      }
      return out({ status: "none", diagnostics: [{ code: "gap", severity: "info", path: "/0", message: `the wall time is skipped in ${zoneId} (a DST gap); pass {gap: "shift-forward-by-gap"} to move it forward` }] });
    }
    const instants = candidates.map((x) => writeInstant(x.t));
    if (overlap === "earliest") return out(unique(instants[0]!));
    if (overlap === "latest") return out(unique(instants[instants.length - 1]!));
    const diagnostics: Diagnostic[] = overlap === "reject"
      ? [{ code: "ambiguous", severity: "info", path: "/0", message: `the wall time occurs ${instants.length} times in ${zoneId} (a DST overlap); pass {overlap: "earliest"} or "latest" to pick one` }]
      : [];
    return out({ status: "multiple", candidates: instants, diagnostics });
  });
}

/** Every instant t = L − o for an offset o in force within a day of L, kept when the zone's offset at t is o. */
function candidatesFor(r: ZoneRules, zoneId: string, L: Rational): { t: Rational; offset: number }[] {
  const lo = sub(L, DAY), hi = addR(L, DAY);
  const offsets = new Set([r.offsetAt(zoneId, lo)]);
  for (const tr of r.transitions(zoneId)) if (cmp(rat(tr.at), lo) > 0 && cmp(rat(tr.at), hi) <= 0) offsets.add(tr.after);
  const found: { t: Rational; offset: number }[] = [];
  for (const o of offsets) {
    const t = sub(L, rat(BigInt(o)));
    if (r.offsetAt(zoneId, t) === o && !found.some((f) => cmp(f.t, t) === 0)) found.push({ t, offset: o });
  }
  return found.sort((a, b) => cmp(a.t, b.t));
}

// ---------------------------------------------------------------------------------------------
// toZone, toOffset

function civilAt(t: Rational, offset: number, zone: Zone): Civil {
  const { whole, frac } = split(t);
  const nano = toNano(frac);
  if (nano === undefined) fail("unsupported", "sub-nanosecond", "/0/value/subTick", "a civil date-time holds whole nanoseconds; this instant is finer");
  const local = whole + BigInt(offset);
  const sod = Number(mod(local, 86400n));
  return {
    form: "datetime", ...civilFromDays(floorDiv(local, 86400n)),
    hour: Math.floor(sod / 3600), minute: Math.floor(sod / 60) % 60, second: sod % 60, nano, hasSecond: true, zone,
  };
}

/** The civil date-time in a named zone at an instant, with the offset in force as `offsetAssertion`. */
export function toZone(value: Json, zoneId: string, rules: ZoneRules | string | Json): UtvResult {
  return run("toZone", () => {
    const t = readInstant(value, "/0");
    const r = rulesArg(rules, "/2");
    if (typeof zoneId !== "string" || !r.has(zoneId)) fail("unsupported", "unknown-zone", "/1", `zone ${String(zoneId)} is not in rule set ${canonical(r.ruleSet)}`);
    if (!r.covers(t)) fail("unsupported", "outside-rules-window", "/0", "the instant is outside the rule bundle's validity window; never extrapolated");
    const offset = r.offsetAt(zoneId, t);
    const c = civilAt(t, offset, { type: "iana", id: zoneId });
    c.offsetAssertion = offset;
    return unique(writeCivil(c), { dependencies: [r.ruleSet] });
  });
}

/** The civil date-time at a fixed UTC offset (seconds east of UTC) at an instant. No rules needed. */
export function toOffset(value: Json, seconds: number): UtvResult {
  return run("toOffset", () => {
    const t = readInstant(value, "/0");
    if (typeof seconds !== "number" || !Number.isInteger(seconds) || Math.abs(seconds) > MAX_OFFSET) fail("invalid", "field-out-of-range", "/1", "offset must be whole seconds within ±18:00");
    return unique(writeCivil(civilAt(t, seconds, { type: "fixed-offset", seconds })));
  });
}
