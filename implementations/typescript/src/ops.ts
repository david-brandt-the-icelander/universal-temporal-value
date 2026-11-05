/** Everyday calendar and instant operations (docs/everyday-api.md): fields, add, compare, daysBetween, posixSecondsBetween, contains, overlaps. */
import type { Json } from "./json.js";
import type { Diagnostic, UtvResult } from "./result.js";
import { civilFromDays, daysFromCivil, daysInMonth, isLeapYear } from "./civil.js";
import { add as addR, cmp, floorDiv, mod, rat, sub, type Rational } from "./rational.js";
import {
  canonical, envelope, fail, isObj, readAmount, readCivil, readInstant, run, unique, wallSeconds,
  writeCivil, writeInstant, writePosixSeconds, type Civil, type Obj,
} from "./internal.js";

// ---------------------------------------------------------------------------------------------
// fields

const dayOfWeek = (epochDay: bigint) => Number(mod(epochDay + 3n, 7n)) + 1; // 1970-01-01 was a Thursday (4)

function isoWeeksInYear(year: bigint): number {
  const jan1 = dayOfWeek(daysFromCivil(year, 1, 1));
  return jan1 === 4 || (jan1 === 3 && isLeapYear(year)) ? 53 : 52;
}

/** Calendar facts about a Gregorian `date` or `datetime`: day of week (Monday = 1), day of year, ISO week, … */
export function fields(value: Json): UtvResult {
  return run("fields", () => {
    const { kind } = envelope(value, "");
    if (kind !== "civil") fail("unsupported", "unsupported-kind", "/kind", "fields applies to civil date and datetime values");
    const c = readCivil(value, "");
    if (c.form !== "date" && c.form !== "datetime") fail("unsupported", "unsupported-form", "/value/form", `fields applies to date and datetime, not ${c.form}`);
    const ed = daysFromCivil(c.year, c.month, c.day);
    const dow = dayOfWeek(ed);
    const doy = Number(ed - daysFromCivil(c.year, 1, 1)) + 1;
    let weekYear = c.year;
    let week = Math.floor((doy - dow + 10) / 7);
    if (week < 1) { weekYear -= 1n; week = isoWeeksInYear(weekYear); }
    else if (week > isoWeeksInYear(c.year)) { weekYear += 1n; week = 1; }
    return unique({
      dayOfWeek: dow, dayOfYear: doy, isoWeekYear: weekYear.toString(), isoWeek: week,
      daysInMonth: daysInMonth(c.year, c.month), isLeapYear: isLeapYear(c.year),
    });
  });
}

// ---------------------------------------------------------------------------------------------
// add

export interface AddPolicy { monthEnd?: "reject" | "constrain" }

/**
 * Adds an amount. Civil values take calendar periods (wall-clock arithmetic; a zone is carried over
 * unchanged); POSIX instants take `posixSeconds` amounts exactly. A day that does not exist after
 * adding months gives `none` unless `policy.monthEnd` is `"constrain"`.
 */
export function add(value: Json, amount: Json, policy?: AddPolicy): UtvResult {
  return run("add", () => {
    let monthEnd = "reject";
    if (policy !== undefined) {
      if (!isObj(policy as Json)) fail("invalid", "type", "/2", "policy must be an object");
      const me = policy.monthEnd;
      if (me !== undefined && me !== "reject" && me !== "constrain") fail("unsupported", "unsupported-policy", "/2/monthEnd", `monthEnd ${JSON.stringify(me)} is not implemented`);
      monthEnd = me ?? "reject";
    }
    const { kind } = envelope(value, "/0");
    if (kind === "instant") {
      const t = readInstant(value, "/0");
      const a = readAmount(amount, "/1");
      if (a.basis === "elapsed") fail("unsupported", "needs-leap-seconds", "/1/value/basis", "adding SI (elapsed) seconds to a POSIX instant needs a leap-second table");
      if (a.basis === "calendar") fail("invalid", "calendar-amount-on-instant", "/1/value/basis", "an instant has no calendar; add a calendar period to a civil value, or use posixSeconds");
      return unique(writeInstant(addR(t, a.seconds)));
    }
    if (kind !== "civil") fail("unsupported", "unsupported-kind", "/0/kind", `add does not handle kind "${kind}"`);
    const c = readCivil(value, "/0");
    const a = readAmount(amount, "/1");
    if (a.basis !== "calendar") fail("unsupported", "not-a-calendar-amount", "/1/value/basis", "a civil value takes a calendar period; resolve it to an instant to add exact seconds");
    if (c.form === "time") fail("unsupported", "time-wraps", "/0/value/form", "adding to a time of day can wrap past midnight and lose the day");
    const p = a.c;
    const notAllowed = c.form === "year-month" ? ["weeks", "days", "hours", "minutes", "seconds"] as const
      : c.form === "date" ? ["hours", "minutes", "seconds"] as const : [] as const;
    for (const k of notAllowed) if (p[k] !== 0n) fail("invalid", "component-not-allowed", `/1/value/components/${k}`, `a ${c.form} cannot take ${k}`);

    const diagnostics: Diagnostic[] = [];
    // 1. years and months together
    const totalMonths = c.year * 12n + BigInt(c.month - 1) + 12n * p.years + p.months;
    let year = floorDiv(totalMonths, 12n);
    let month = Number(mod(totalMonths, 12n)) + 1;
    let day = c.day;
    // 2. month end
    if (c.form !== "year-month" && day > daysInMonth(year, month)) {
      if (monthEnd === "reject") {
        return { status: "none", diagnostics: [{ code: "month-end", severity: "info", path: "/1", message: `day ${day} does not exist in ${year}-${String(month).padStart(2, "0")}; pass {monthEnd: "constrain"} to clamp` }] };
      }
      day = daysInMonth(year, month);
    }
    const out: Civil = { ...c, year, month, day };
    if (c.form !== "year-month") {
      // 3. weeks and days, 4. wall-clock hours, minutes and seconds carried into days
      let ed = daysFromCivil(year, month, day) + 7n * p.weeks + p.days;
      if (c.form === "datetime") {
        const sod = BigInt(c.hour * 3600 + c.minute * 60 + c.second) + 3600n * p.hours + 60n * p.minutes + p.seconds;
        ed += floorDiv(sod, 86400n);
        const s = Number(mod(sod, 86400n));
        Object.assign(out, { hour: Math.floor(s / 3600), minute: Math.floor(s / 60) % 60, second: s % 60, hasSecond: c.hasSecond || p.seconds !== 0n });
      }
      Object.assign(out, civilFromDays(ed));
    }
    if (out.offsetAssertion !== undefined) {
      delete out.offsetAssertion;
      diagnostics.push({ code: "offset-assertion-dropped", severity: "info", path: "/0/value/offsetAssertion", message: "the asserted offset belonged to the old wall time; resolve the result to learn the new one" });
    }
    return unique(writeCivil(out), { diagnostics });
  });
}

// ---------------------------------------------------------------------------------------------
// compare

const order = (x: number): "before" | "equal" | "after" => (x < 0 ? "before" : x > 0 ? "after" : "equal");

/** `"before"`, `"equal"` or `"after"` (a relative to b). Named zones give `unsupported`: resolve first. */
export function compare(a: Json, b: Json): UtvResult {
  return run("compare", () => {
    const ka = envelope(a, "/0").kind, kb = envelope(b, "/1").kind;
    if (ka === "instant" && kb === "instant") return unique(order(cmp(readInstant(a, "/0"), readInstant(b, "/1"))));
    if (ka !== "civil" || kb !== "civil") fail("unsupported", "incomparable", "", `cannot compare kind "${ka}" with kind "${kb}"`);
    const x = readCivil(a, "/0"), y = readCivil(b, "/1");
    if (x.zone.type === "iana" || y.zone.type === "iana") {
      fail("unsupported", "resolve-first", "", "ordering values in a named zone depends on the zone's rules; resolve them to instants first");
    }
    if (x.form !== y.form) fail("unsupported", "incomparable", "", `cannot compare a ${x.form} with a ${y.form}`);
    if (x.zone.type === "floating" && y.zone.type === "floating") {
      const key = (c: Civil) => [c.year, BigInt(c.month), BigInt(c.day), BigInt(c.hour), BigInt(c.minute), BigInt(c.second), BigInt(c.nano)];
      const kx = key(x), ky = key(y);
      for (let i = 0; i < kx.length; i++) if (kx[i] !== ky[i]) return unique(kx[i]! < ky[i]! ? "before" : "after");
      return unique("equal");
    }
    if (x.zone.type === "fixed-offset" && y.zone.type === "fixed-offset" && x.form === "datetime") {
      return unique(order(cmp(sub(wallSeconds(x), rat(BigInt(x.zone.seconds))), sub(wallSeconds(y), rat(BigInt(y.zone.seconds))))));
    }
    return fail("unsupported", "incomparable", "", "cannot compare an unzoned value with a fixed-offset one");
  });
}

// ---------------------------------------------------------------------------------------------
// daysBetween, posixSecondsBetween

/** Reads a Gregorian `date` as an epoch day, checking that every date in one operation has the same zone. */
function dateDay(v: Json, path: string, zones: Set<string>): bigint {
  const { kind } = envelope(v, path);
  if (kind !== "civil") fail("unsupported", "unsupported-kind", `${path}/kind`, `expected a civil date, got kind "${kind}"`);
  const c = readCivil(v, path);
  if (c.form !== "date") fail("unsupported", "unsupported-form", `${path}/value/form`, `expected a date, got a ${c.form}`);
  zones.add(canonical(c.zone as unknown as Json));
  if (zones.size > 1) fail("unsupported", "mixed-zones", `${path}/value/zone`, "dates in different zones cannot be counted against each other");
  return daysFromCivil(c.year, c.month, c.day);
}

/** Whole days from date a to date b (b − a). */
export function daysBetween(a: Json, b: Json): UtvResult {
  return run("daysBetween", () => {
    const zones = new Set<string>();
    const n = dateDay(b, "/1", zones) - dateDay(a, "/0", zones);
    if (n > BigInt(Number.MAX_SAFE_INTEGER) || n < -BigInt(Number.MAX_SAFE_INTEGER)) fail("unsupported", "too-large", "", "the day count does not fit a JSON-safe integer");
    return unique(Number(n));
  });
}

/** b − a between two POSIX instants, as an exact `posixSeconds` amount. */
export function posixSecondsBetween(a: Json, b: Json): UtvResult {
  return run("posixSecondsBetween", () => unique(writePosixSeconds(sub(readInstant(b, "/1"), readInstant(a, "/0")))));
}

// ---------------------------------------------------------------------------------------------
// contains, overlaps

type Domain = "date" | "instant";
type End = { state: "included" | "excluded"; p: Rational } | { state: "unbounded" } | { state: "unknown" };
interface Interval { domain: Domain; start: End; end: End; empty: boolean }
type Truth = boolean | undefined;

function readPoint(v: Json | undefined, domain: Domain, path: string, zones: Set<string>): Rational {
  const { kind } = envelope(v, path);
  if (domain === "instant") {
    if (kind !== "instant") fail("unsupported", "mixed-domains", path, `an instant interval cannot hold kind "${kind}"`);
    return readInstant(v, path);
  }
  if (kind !== "civil") fail("unsupported", "mixed-domains", path, `a civil date interval cannot hold kind "${kind}"`);
  return rat(dateDay(v as Json, path, zones));
}

function readInterval(v: Json, path: string, zones: Set<string>): Interval {
  const { kind, value } = envelope(v, path);
  const vp = `${path}/value`;
  if (kind !== "interval") fail("invalid", "wrong-kind", `${path}/kind`, `expected an interval, got kind "${kind}"`);
  const c = value["construction"];
  if ((c !== undefined && c !== "start-end") || "amount" in value || "arithmeticPolicy" in value) fail("unsupported", "unsupported-construction", vp, "only start-end intervals are implemented");
  const d = value["domain"];
  const domain: Domain = d === "civil/gregory" ? "date" : d === "physical" ? "instant" : fail("unsupported", "unsupported-domain", `${vp}/domain`, `interval domain ${JSON.stringify(d)} is not implemented`);
  const end = (e: Json | undefined, ep: string): End => {
    if (!isObj(e)) fail("invalid", "type", ep, "an endpoint must be an object");
    const state = e["state"];
    if (state === "unbounded" || state === "unknown") return { state };
    if (state !== "included" && state !== "excluded") fail("invalid", "enum", `${ep}/state`, `unknown endpoint state ${JSON.stringify(state)}`);
    return { state, p: readPoint(e["point"], domain, `${ep}/point`, zones) };
  };
  const empty = value["empty"];
  if (empty !== undefined && typeof empty !== "boolean") fail("invalid", "type", `${vp}/empty`, "empty must be a boolean");
  return { domain, start: end(value["start"], `${vp}/start`), end: end(value["end"], `${vp}/end`), empty: empty === true };
}

const and = (...xs: Truth[]): Truth => (xs.includes(false) ? false : xs.every((x) => x === true) ? true : undefined);
const verdict = (t: Truth) => (t === true ? "definitely" : t === false ? "impossible" : "possibly");
/** `x < y`, or `x ≤ y` when `orEqual`. */
const before = (x: Rational, y: Rational, orEqual: boolean): boolean => (orEqual ? cmp(x, y) <= 0 : cmp(x, y) < 0);

/** Whether the interval contains the point: `"definitely"`, `"possibly"` (an unknown end) or `"impossible"`. */
export function contains(interval: Json, point: Json): UtvResult {
  return run("contains", () => {
    const zones = new Set<string>();
    const iv = readInterval(interval, "/0", zones);
    const p = readPoint(point, iv.domain, "/1", zones);
    if (iv.empty) return unique("impossible");
    const cond = (e: End, f: (q: Rational, incl: boolean) => boolean): Truth =>
      e.state === "unbounded" ? true : e.state === "unknown" ? undefined : f(e.p, e.state === "included");
    return unique(verdict(and(cond(iv.start, (s, incl) => before(s, p, incl)), cond(iv.end, (e, incl) => before(p, e, incl)))));
  });
}

/** Dates are discrete: rewrite as half-open `[start, end)` so every comparison is a strict `<`. */
function halfOpen(iv: Interval): Interval {
  if (iv.domain !== "date") return iv;
  const one = rat(1n);
  const start: End = iv.start.state === "excluded" ? { state: "included", p: addR(iv.start.p, one) } : iv.start;
  const end: End = iv.end.state === "included" ? { state: "excluded", p: addR(iv.end.p, one) } : iv.end;
  return { ...iv, start, end };
}

/** `s` comes before `e` (a start before an end): `≤` only when both are included instants. */
const startBeforeEnd = (s: End, e: End): Truth => {
  if (s.state === "unbounded" || e.state === "unbounded") return true;
  if (s.state === "unknown" || e.state === "unknown") return undefined;
  return before(s.p, e.p, s.state === "included" && e.state === "included");
};

/** Whether two intervals share a point: `"definitely"`, `"possibly"` or `"impossible"`. */
export function overlaps(a: Json, b: Json): UtvResult {
  return run("overlaps", () => {
    const zones = new Set<string>();
    const x = halfOpen(readInterval(a, "/0", zones)), y = halfOpen(readInterval(b, "/1", zones));
    if (x.domain !== y.domain) fail("unsupported", "mixed-domains", "", "cannot overlap a civil date interval with an instant interval");
    const provablyEmpty = (iv: Interval) => iv.empty || startBeforeEnd(iv.start, iv.end) === false;
    if (provablyEmpty(x) || provablyEmpty(y)) return unique("impossible");
    return unique(verdict(and(startBeforeEnd(x.start, y.end), startBeforeEnd(y.start, x.end))));
  });
}

