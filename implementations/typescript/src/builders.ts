/**
 * Builders for the canonical everyday shapes (docs/everyday-api.md, "Canonical shapes").
 * Builders only assemble JSON; they do not validate. `date(2026, 2, 30)` builds a value that
 * `fields`, `formatIso` or `validateSemantic` then report as `invalid`.
 */
import type { Json } from "./json.js";
import { rat, split } from "./rational.js";

type Obj = { [k: string]: Json };
/** An integer given as a number, a bigint or a decimal string. */
export type Int = number | bigint | string;

export const SCHEMA = "utv/1.0";
export const GREGORY: Obj = { id: "gregory", chronology: "proleptic" };
export const POSIX: Obj = { system: "utv.timescale", id: "unix-posix", version: "1" };

const big = (x: Int): bigint => (typeof x === "bigint" ? x : BigInt(x));
const civil = (form: string, fields: Obj): Obj => ({
  schema: SCHEMA, kind: "civil", value: { form, fields, calendar: { ...GREGORY } },
});
const timeFields = (hour: number, minute: number, second?: number, nano?: number): Obj => {
  const f: Obj = { hour, minute };
  if (second !== undefined) f["second"] = second;
  if (nano !== undefined && nano !== 0) f["nano"] = nano;
  return f;
};

/** A proleptic Gregorian calendar date. Months and days are 1-based. */
export function date(year: Int, month: number, day: number): Obj {
  return civil("date", { year: big(year).toString(), month, day });
}

export function yearMonth(year: Int, month: number): Obj {
  return civil("year-month", { year: big(year).toString(), month });
}

/** A time of day. `second` is kept only if given; `nano` only if given and non-zero. */
export function time(hour: number, minute: number, second?: number, nano?: number): Obj {
  return civil("time", timeFields(hour, minute, second, nano));
}

/** An unzoned ("floating") local date-time. Bind it with {@link inZone} or {@link withOffset}. */
export function dateTime(year: Int, month: number, day: number, hour: number, minute: number, second?: number, nano?: number): Obj {
  return civil("datetime", { year: big(year).toString(), month, day, ...timeFields(hour, minute, second, nano) });
}

const withValue = (v: Json, extra: Obj): Obj => {
  const o = v as Obj;
  return { ...o, value: { ...(o["value"] as Obj), ...extra } };
};

/** The same civil value, meant in the IANA zone `zoneId` (follows the zone's rules). */
export function inZone(dt: Json, zoneId: string): Obj {
  return withValue(dt, { zone: { type: "iana", id: zoneId }, zoneIntent: "follow-zone-id" });
}

/** The same civil value at a fixed UTC offset, in seconds east of UTC. */
export function withOffset(dt: Json, seconds: number): Obj {
  return withValue(dt, { zone: { type: "fixed-offset", seconds }, zoneIntent: "fixed-offset" });
}

/** A POSIX instant: seconds since 1970-01-01T00:00:00Z plus optional nanoseconds. */
export function instant(epochSeconds: Int, nano: Int = 0): Obj {
  const { whole, frac } = split(rat(big(epochSeconds) * 1_000_000_000n + big(nano), 1_000_000_000n));
  const value: Obj = { system: { ...POSIX }, ticks: whole.toString() };
  if (frac.n !== 0n) value["subTick"] = { numerator: frac.n.toString(), denominator: frac.d.toString() };
  return { schema: SCHEMA, kind: "instant", value };
}

export interface PeriodParts { years?: Int; months?: Int; weeks?: Int; days?: Int; hours?: Int; minutes?: Int; seconds?: Int }
const PERIOD_KEYS = ["years", "months", "weeks", "days", "hours", "minutes", "seconds"] as const;

/**
 * A calendar period. Call it positionally, `period(years, months, weeks, days, hours, minutes, seconds)`,
 * or with named parts, `period({ months: 1 })`. Only non-zero components are kept.
 */
export function period(parts: PeriodParts): Obj;
export function period(years?: Int, months?: Int, weeks?: Int, days?: Int, hours?: Int, minutes?: Int, seconds?: Int): Obj;
export function period(first?: Int | PeriodParts, ...rest: (Int | undefined)[]): Obj {
  const parts: PeriodParts = typeof first === "object" && first !== null && typeof first !== "bigint"
    ? first
    : Object.fromEntries(PERIOD_KEYS.map((k, i) => [k, i === 0 ? first : rest[i - 1]]));
  const components: Obj = {};
  for (const k of PERIOD_KEYS) {
    const v = parts[k];
    if (v !== undefined && big(v) !== 0n) components[k] = big(v).toString();
  }
  return { schema: SCHEMA, kind: "amount", value: { basis: "calendar", components } };
}

/** An exact amount of POSIX seconds (not SI seconds across a leap second): `numerator / denominator`. */
export function posixSeconds(numerator: Int, denominator: Int = 1): Obj {
  const r = rat(big(numerator), big(denominator));
  return {
    schema: SCHEMA, kind: "amount",
    value: { basis: "clock-ticks", rules: { ...POSIX }, seconds: { numerator: r.n.toString(), denominator: r.d.toString() } },
  };
}

/** An interval endpoint that is not a point: no bound at all, or a bound that exists but is not known. */
export type OpenEnd = "unbounded" | "unknown";

/**
 * An interval, half-open `[start, end)` by default. Either end may be `"unbounded"` or `"unknown"`.
 * The domain is `civil/gregory` for civil points and `physical` for instants; pass `domain` when
 * neither end is a point.
 */
export function interval(start: Json | OpenEnd, end: Json | OpenEnd, startIncluded = true, endIncluded = false, domain?: string): Obj {
  const endpoint = (p: Json | OpenEnd, included: boolean): Obj =>
    p === "unbounded" || p === "unknown" ? { state: p } : { state: included ? "included" : "excluded", point: p };
  const point = [start, end].find((p) => typeof p === "object" && p !== null) as Obj | undefined;
  const d = domain ?? (point?.["kind"] === "instant" ? "physical" : "civil/gregory");
  return { schema: SCHEMA, kind: "interval", value: { domain: d, start: endpoint(start, startIncluded), end: endpoint(end, endIncluded) } };
}
