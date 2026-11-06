/**
 * Typed values (docs/library-api.md §1–§2): one immutable class per everyday UTV value.
 *
 * - Valid by construction: `CivilDate.of(2026, 2, 30)` throws a `RangeError`; `tryOf` returns an `Outcome`.
 * - Total operations return values; partial ones return `Outcome`s.
 * - `toString()` is the text form, `toJSON()` the UTV JSON, `equals()` compares by value.
 *
 * Every operation calls the 0.2 JSON layer underneath, so the typed layer agrees with the fixtures.
 */
import { parseStrict, type Json } from "./json.js";
import type { UtvResult } from "./result.js";
import * as build from "./builders.js";
import * as ops from "./ops.js";
import * as zones from "./zones.js";
import * as native from "./native.js";
import { formatText, parseText } from "./text.js";
import { ZONE_NAME } from "./iso.js";
import { validateStructural } from "./structural.js";
import {
  canonical, envelope, fail, isObj, readAmount, readCivil, readInstant, readRational, writeCivil, writeInstant,
  COMPONENTS, Fail, type Civil, type Component, type Obj,
} from "./internal.js";
import { rat, split, type Rational } from "./rational.js";
import {
  failed, outcomeOf, Unique, type MonthEnd, type Ordering, type Outcome, type ResolutionPolicy, type Truth, type Weekday,
} from "./outcome.js";
import type { TemporalContext } from "./context.js";

/** An integer argument: a `number` (a safe integer) or a `bigint`. */
export type Int = number | bigint;

/** An exact rational in lowest terms with a positive denominator, e.g. `Instant.subSecond`. */
export interface SubSecond { readonly numerator: bigint; readonly denominator: bigint }

// ---------------------------------------------------------------------------------------------
// Internal plumbing

/** Runs a body that may end with a JSON-layer failure, as an `Outcome`. */
function attempt<T>(operation: string, body: () => T): Outcome<T> {
  try {
    return new Unique(body(), [], operation);
  } catch (e) {
    if (e instanceof Fail) return failed(e.status, [e.diagnostic], operation);
    throw e;
  }
}

/** The value of a constructor's outcome, or the ordinary argument error. */
function orThrow<T>(o: Outcome<T>): T {
  if (o.status === "unique") return o.value;
  const d = o.diagnostics[0];
  throw new RangeError(d === undefined ? o.status : `${d.message} (${d.code})`);
}

/** The single answer of an operation that cannot fail for valid inputs. */
function total(r: UtvResult): Json {
  if (r.status !== "unique") {
    throw new Error(`internal error: ${r.operation} gave ${r.status} (${r.diagnostics.map((d) => d.code).join(", ")})`);
  }
  return r.candidates[0]!;
}

function integer(x: unknown, name: string): bigint {
  if (typeof x === "bigint") return x;
  if (typeof x === "number" && Number.isSafeInteger(x)) return BigInt(x);
  return fail("invalid", "field-out-of-range", "", `${name} must be an integer, got ${String(x)}`);
}

function deepFreeze<T>(v: T): T {
  if (typeof v === "object" && v !== null && !Object.isFrozen(v)) {
    for (const k of Object.keys(v)) deepFreeze((v as Record<string, unknown>)[k]);
    Object.freeze(v);
  }
  return v;
}

const ORDER: Record<string, -1 | 0 | 1> = { before: -1, equal: 0, after: 1 };

type Class<T> = { prototype: T; name: string };

/** Decodes a value produced by the JSON layer into the expected class (never fails for library output). */
function as<T>(cls: Class<T>): (j: Json) => T {
  return (j) => {
    const v = decode(j);
    if (!(v instanceof (cls as unknown as abstract new () => T))) throw new Error(`internal error: expected a ${cls.name}`);
    return v as T;
  };
}

/** `T.fromJson`: JSON (an object or its text) to a typed value of one class. */
function typedFromJson<T>(json: Json | string, cls: Class<T>): Outcome<T> {
  const operation = `${cls.name}.fromJson`;
  if (typeof json === "string") {
    let parsed: Json;
    try {
      parsed = parseStrict(json);
    } catch {
      const r = validateStructural(json);
      return failed(r.status, r.diagnostics, operation);
    }
    return typedFromJson(parsed, cls);
  }
  return attempt(operation, () => expect(decode(json), cls));
}

/** `T.parse`: text-form input to a typed value of one class. */
function typedParse<T>(text: string, cls: Class<T>): Outcome<T> {
  const r = parseText(text);
  if (r.status !== "unique") return failed(r.status, r.diagnostics, `${cls.name}.parse`);
  return attempt(`${cls.name}.parse`, () => expect(decode(r.candidates[0]!), cls));
}

function expect<T>(v: UtvValue, cls: Class<T>): T {
  if (!(v instanceof (cls as unknown as abstract new () => T))) {
    fail("invalid", "kind-mismatch", "", `expected a ${cls.name}, got a ${v.constructor.name} (${String(v)})`);
  }
  return v as T;
}

// Factories for the private constructors, registered by each class's static block.
let newDate: (c: Civil) => CivilDate;
let newYearMonth: (c: Civil) => CivilYearMonth;
let newTime: (c: Civil) => CivilTime;
let newDateTime: (c: Civil) => CivilDateTime;
let newOffsetDateTime: (c: Civil) => FixedOffsetDateTime;
let newZoned: (c: Civil) => ZonedIntent;
let newInstant: (t: Rational) => Instant;
let newPeriod: (c: Record<Component, bigint>) => Period;
let newPosix: (s: Rational) => PosixSeconds;
let newElapsed: (s: Rational) => ElapsedSeconds;
let newInterval: (start: Bound<IntervalPoint>, end: Bound<IntervalPoint>, domain: Domain) => Interval<IntervalPoint>;

/** Any UTV JSON value this layer has a type for. Throws `Fail` otherwise. */
function decode(json: Json): UtvValue {
  const { kind, value } = envelope(json, "");
  switch (kind) {
    case "civil": return civilValue(readCivil(json, ""));
    case "instant": return newInstant(readInstant(json, ""));
    case "amount": {
      if (value["basis"] === "elapsed" && !("sequence" in value)) return newElapsed(readRational(value["seconds"], "/value/seconds"));
      const a = readAmount(json, "");
      if (a.basis === "calendar") return newPeriod(a.c);
      if (a.basis === "clock-ticks") return newPosix(a.seconds);
      break;
    }
    case "interval": return intervalValue(value);
  }
  return fail("unsupported", "unsupported-operand", "/kind", `this library has no typed value for kind "${kind}"; use the JSON layer`);
}

function civilValue(c: Civil): UtvValue {
  const plain = c.offsetAssertion === undefined && c.resolutionPolicy === undefined;
  const floating = plain && c.zone.type === "floating";
  if (floating && c.form === "date") return newDate(c);
  if (floating && c.form === "year-month") return newYearMonth(c);
  if (floating && c.form === "time") return newTime(c);
  if (floating && c.form === "datetime") return newDateTime(c);
  if (plain && c.form === "datetime" && c.zone.type === "fixed-offset") return newOffsetDateTime(c);
  if (c.form === "datetime" && c.zone.type === "iana") return newZoned(c);
  return fail("unsupported", "unsupported-operand", "/value", `this library has no typed value for a ${c.zone.type} civil ${c.form} with these properties; use the JSON layer`);
}

function intervalValue(v: Obj): UtvValue {
  const construction = v["construction"];
  if ((construction !== undefined && construction !== "start-end") || "amount" in v || "arithmeticPolicy" in v || v["empty"] === true) {
    fail("unsupported", "unsupported-operand", "/value", "only start-end intervals have a typed value");
  }
  const domain = v["domain"];
  if (domain !== "civil/gregory" && domain !== "physical") return fail("unsupported", "unsupported-operand", "/value/domain", `interval domain ${JSON.stringify(domain)} has no typed value`);
  const bound = (name: "start" | "end"): Bound<IntervalPoint> => {
    const e = v[name];
    if (!isObj(e)) return fail("invalid", "type", `/value/${name}`, "an endpoint must be an object");
    const state = e["state"];
    if (state === "unbounded") return Bound.unbounded;
    if (state === "unknown") return Bound.unknown;
    if (state !== "included" && state !== "excluded") return fail("invalid", "type", `/value/${name}/state`, `unknown endpoint state ${JSON.stringify(state)}`);
    const p = decode(e["point"] ?? null);
    if (!(p instanceof CivilDate || p instanceof Instant)) return fail("unsupported", "unsupported-operand", `/value/${name}/point`, "interval points must be dates or instants");
    return { state, value: p };
  };
  return newInterval(bound("start"), bound("end"), domain) as Interval<CivilDate> | Interval<Instant>;
}

// ---------------------------------------------------------------------------------------------
// Base classes

const INSPECT = Symbol.for("nodejs.util.inspect.custom");

/** What every typed value has: text, JSON and value equality. */
export abstract class TemporalValue {
  readonly #json: Json;
  protected constructor(json: Json) { this.#json = deepFreeze(json); }

  /** The UTV JSON (frozen). `JSON.stringify(value)` uses it. */
  toJSON(): Json { return this.#json; }

  /** The text form (docs/text-form.md); a value it can't express prints as its JSON. */
  toString(): string {
    const r = formatText(this.#json);
    return r.status === "unique" ? String(r.candidates[0]) : JSON.stringify(this.#json);
  }

  /** Representation equality: same class, same JSON. */
  equals(other: unknown): boolean {
    return other instanceof TemporalValue && other.constructor === this.constructor && canonical(this.#json) === canonical(other.#json);
  }

  /** `console.log` / the Node debugger show `CivilDate(2026-11-05)`. */
  [INSPECT](): string { return `${this.constructor.name}(${this.toString()})`; }
}

/** A value with a total order against values of its own type. */
export abstract class Ordered<Self extends TemporalValue> extends TemporalValue {
  /** -1, 0 or 1: this value relative to `other`. */
  compare(other: Self): -1 | 0 | 1 {
    return ORDER[total(ops.compare(this.toJSON(), other.toJSON())) as Ordering]!;
  }
  isBefore(other: Self): boolean { return this.compare(other) < 0; }
  isAfter(other: Self): boolean { return this.compare(other) > 0; }
}

/**
 * The total `addMonths(n, monthEnd)` / `addYears(n, monthEnd)` overloads take only `MonthEnd.Constrain`
 * (library-api §2, "Policy overloads"). Rejecting is the overload without a policy, which returns an
 * `Outcome`; passing `Reject` (or the literal `"reject"`) is a programming error.
 */
function constrainOnly(monthEnd: unknown): void {
  if (monthEnd !== undefined && monthEnd !== "constrain") {
    throw new RangeError(`the policy argument takes only MonthEnd.Constrain, got ${JSON.stringify(monthEnd)}; omit it to get an Outcome`);
  }
}

/** Adds a calendar period through the JSON layer: the result of `ops.add`. */
const addPeriod = (v: TemporalValue, parts: build.PeriodParts, monthEnd?: MonthEnd): UtvResult =>
  ops.add(v.toJSON(), build.period(parts), monthEnd === undefined ? undefined : { monthEnd });

// ---------------------------------------------------------------------------------------------
// CivilDate

/** The JSON layer's `fields` answer. */
interface DateFacts { dayOfWeek: number; dayOfYear: number; isoWeekYear: string; isoWeek: number; daysInMonth: number; isLeapYear: boolean }

/** A proleptic Gregorian calendar date, with no time and no zone. */
export class CivilDate extends Ordered<CivilDate> {
  readonly #c: Civil;
  #facts?: DateFacts;
  private constructor(c: Civil) { super(writeCivil(c)); this.#c = c; }
  static { newDate = (c) => new CivilDate(c); }

  /** Throws a `RangeError` for a date that doesn't exist, e.g. 30 February. Months are 1-based. */
  static of(year: Int, month: number, day: number): CivilDate { return orThrow(CivilDate.tryOf(year, month, day)); }
  static tryOf(year: Int, month: number, day: number): Outcome<CivilDate> {
    return attempt("CivilDate.of", () => newDate(readCivil(build.date(integer(year, "year"), month, day), "")));
  }
  static parse(text: string): Outcome<CivilDate> { return typedParse(text, CivilDate); }
  static fromJson(json: Json | string): Outcome<CivilDate> { return typedFromJson(json, CivilDate); }

  get year(): bigint { return this.#c.year; }
  get month(): number { return this.#c.month; }
  get day(): number { return this.#c.day; }

  #fields(): DateFacts { return (this.#facts ??= total(ops.fields(this.toJSON())) as unknown as DateFacts); }
  get dayOfWeek(): Weekday { return this.#fields().dayOfWeek as Weekday; }
  get dayOfYear(): number { return this.#fields().dayOfYear; }
  get isoWeek(): number { return this.#fields().isoWeek; }
  get isoWeekYear(): bigint { return BigInt(this.#fields().isoWeekYear); }
  get daysInMonth(): number { return this.#fields().daysInMonth; }
  get isLeapYear(): boolean { return this.#fields().isLeapYear; }

  addDays(days: Int): CivilDate { return as(CivilDate)(total(addPeriod(this, { days }))); }
  addWeeks(weeks: Int): CivilDate { return as(CivilDate)(total(addPeriod(this, { weeks }))); }
  /** `none` (`month-end`) when the day doesn't exist in the target month, unless you pass `MonthEnd.Constrain`. */
  addMonths(months: Int): Outcome<CivilDate>;
  addMonths(months: Int, monthEnd: typeof MonthEnd.Constrain): CivilDate;
  addMonths(months: Int, monthEnd?: MonthEnd): CivilDate | Outcome<CivilDate> {
    constrainOnly(monthEnd);
    const r = addPeriod(this, { months }, monthEnd);
    return monthEnd === "constrain" ? as(CivilDate)(total(r)) : outcomeOf(r, as(CivilDate));
  }
  addYears(years: Int): Outcome<CivilDate>;
  addYears(years: Int, monthEnd: typeof MonthEnd.Constrain): CivilDate;
  addYears(years: Int, monthEnd?: MonthEnd): CivilDate | Outcome<CivilDate> {
    constrainOnly(monthEnd);
    const r = addPeriod(this, { years }, monthEnd);
    return monthEnd === "constrain" ? as(CivilDate)(total(r)) : outcomeOf(r, as(CivilDate));
  }
  /** `none` on a month end; `invalid` (`kind-mismatch`) if the period has hours, minutes or seconds. */
  add(period: Period, monthEnd?: MonthEnd): Outcome<CivilDate> {
    return outcomeOf(ops.add(this.toJSON(), period.toJSON(), monthEnd === undefined ? undefined : { monthEnd }), as(CivilDate));
  }

  /** Whole days from this date to `other` (negative if `other` is earlier). */
  daysUntil(other: CivilDate): number { return total(ops.daysBetween(this.toJSON(), other.toJSON())) as number; }

  /** This date at a time of day: a `CivilDateTime`, still without a zone. */
  at(time: CivilTime): CivilDateTime {
    const t = time.toJSON() as Obj;
    const tf = (t["value"] as Obj)["fields"] as Obj;
    const fields = { ...((this.toJSON() as Obj)["value"] as Obj)["fields"] as Obj, ...tf };
    return as(CivilDateTime)({ ...(t as Obj), value: { ...(t["value"] as Obj), form: "datetime", fields } });
  }
}

// ---------------------------------------------------------------------------------------------
// CivilYearMonth

/** A month of a proleptic Gregorian year, e.g. `2026-11`. */
export class CivilYearMonth extends Ordered<CivilYearMonth> {
  readonly #c: Civil;
  private constructor(c: Civil) { super(writeCivil(c)); this.#c = c; }
  static { newYearMonth = (c) => new CivilYearMonth(c); }

  static of(year: Int, month: number): CivilYearMonth { return orThrow(CivilYearMonth.tryOf(year, month)); }
  static tryOf(year: Int, month: number): Outcome<CivilYearMonth> {
    return attempt("CivilYearMonth.of", () => newYearMonth(readCivil(build.yearMonth(integer(year, "year"), month), "")));
  }
  static parse(text: string): Outcome<CivilYearMonth> { return typedParse(text, CivilYearMonth); }
  static fromJson(json: Json | string): Outcome<CivilYearMonth> { return typedFromJson(json, CivilYearMonth); }

  get year(): bigint { return this.#c.year; }
  get month(): number { return this.#c.month; }

  addMonths(months: Int): CivilYearMonth { return as(CivilYearMonth)(total(addPeriod(this, { months }))); }
  addYears(years: Int): CivilYearMonth { return as(CivilYearMonth)(total(addPeriod(this, { years }))); }
}

// ---------------------------------------------------------------------------------------------
// CivilTime

/** A time of day, with no date and no zone. It remembers whether a second was given (`09:30` vs `09:30:00`). */
export class CivilTime extends Ordered<CivilTime> {
  readonly #c: Civil;
  private constructor(c: Civil) { super(writeCivil(c)); this.#c = c; }
  static { newTime = (c) => new CivilTime(c); }

  /** Throws a `RangeError` for hour 24, minute 60 and the like. Second 60 needs a leap-second table. */
  static of(hour: number, minute: number, second?: number, nanosecond?: number): CivilTime {
    return orThrow(CivilTime.tryOf(hour, minute, second, nanosecond));
  }
  static tryOf(hour: number, minute: number, second?: number, nanosecond?: number): Outcome<CivilTime> {
    return attempt("CivilTime.of", () => newTime(readCivil(build.time(hour, minute, second ?? (nanosecond ? 0 : undefined), nanosecond), "")));
  }
  static parse(text: string): Outcome<CivilTime> { return typedParse(text, CivilTime); }
  static fromJson(json: Json | string): Outcome<CivilTime> { return typedFromJson(json, CivilTime); }

  get hour(): number { return this.#c.hour; }
  get minute(): number { return this.#c.minute; }
  /** 0 when no second was given. */
  get second(): number { return this.#c.second; }
  get nanosecond(): number { return this.#c.nano; }
}

// ---------------------------------------------------------------------------------------------
// CivilDateTime

/** A date and a time of day with no zone: a wall-clock reading that isn't yet a moment. */
export class CivilDateTime extends Ordered<CivilDateTime> {
  readonly #c: Civil;
  private constructor(c: Civil) { super(writeCivil(c)); this.#c = c; }
  static { newDateTime = (c) => new CivilDateTime(c); }

  static of(year: Int, month: number, day: number, hour: number, minute: number, second?: number, nanosecond?: number): CivilDateTime {
    return orThrow(CivilDateTime.tryOf(year, month, day, hour, minute, second, nanosecond));
  }
  static tryOf(year: Int, month: number, day: number, hour: number, minute: number, second?: number, nanosecond?: number): Outcome<CivilDateTime> {
    return attempt("CivilDateTime.of", () =>
      newDateTime(readCivil(build.dateTime(integer(year, "year"), month, day, hour, minute, second ?? (nanosecond ? 0 : undefined), nanosecond), "")));
  }
  static parse(text: string): Outcome<CivilDateTime> { return typedParse(text, CivilDateTime); }
  static fromJson(json: Json | string): Outcome<CivilDateTime> { return typedFromJson(json, CivilDateTime); }

  get date(): CivilDate { return newDate({ ...this.#c, form: "date" }); }
  get time(): CivilTime { return newTime({ ...this.#c, form: "time" }); }

  addDays(days: Int): CivilDateTime { return as(CivilDateTime)(total(addPeriod(this, { days }))); }
  addWeeks(weeks: Int): CivilDateTime { return as(CivilDateTime)(total(addPeriod(this, { weeks }))); }
  /** Wall-clock hours, carrying into days. There is no zone, so there is no DST. */
  addHours(hours: Int): CivilDateTime { return as(CivilDateTime)(total(addPeriod(this, { hours }))); }
  addMinutes(minutes: Int): CivilDateTime { return as(CivilDateTime)(total(addPeriod(this, { minutes }))); }
  addSeconds(seconds: Int): CivilDateTime { return as(CivilDateTime)(total(addPeriod(this, { seconds }))); }
  addMonths(months: Int): Outcome<CivilDateTime>;
  addMonths(months: Int, monthEnd: typeof MonthEnd.Constrain): CivilDateTime;
  addMonths(months: Int, monthEnd?: MonthEnd): CivilDateTime | Outcome<CivilDateTime> {
    constrainOnly(monthEnd);
    const r = addPeriod(this, { months }, monthEnd);
    return monthEnd === "constrain" ? as(CivilDateTime)(total(r)) : outcomeOf(r, as(CivilDateTime));
  }
  add(period: Period, monthEnd?: MonthEnd): Outcome<CivilDateTime> {
    return outcomeOf(ops.add(this.toJSON(), period.toJSON(), monthEnd === undefined ? undefined : { monthEnd }), as(CivilDateTime));
  }

  /** The same wall clock, meant in an IANA zone. Resolving it to an instant needs rules: `resolve(ctx)`. */
  inZone(zoneId: string): ZonedIntent {
    if (typeof zoneId !== "string" || !ZONE_NAME.test(zoneId)) throw new RangeError(`not an IANA zone name: ${String(zoneId)}`);
    return newZoned({ ...this.#c, zone: { type: "iana", id: zoneId } });
  }
  /** The same wall clock at a fixed UTC offset, in seconds east of UTC (within ±18:00). */
  atOffset(offsetSeconds: number): FixedOffsetDateTime {
    return orThrow(attempt("atOffset", () => newOffsetDateTime(readCivil(build.withOffset(this.toJSON(), offsetSeconds), ""))));
  }
}

// ---------------------------------------------------------------------------------------------
// FixedOffsetDateTime

/** A wall clock at a fixed UTC offset, e.g. `2026-11-05T14:30+01:00`. It names exactly one instant. */
export class FixedOffsetDateTime extends Ordered<FixedOffsetDateTime> {
  readonly #c: Civil;
  private constructor(c: Civil) { super(writeCivil(c)); this.#c = c; }
  static { newOffsetDateTime = (c) => new FixedOffsetDateTime(c); }

  static parse(text: string): Outcome<FixedOffsetDateTime> { return typedParse(text, FixedOffsetDateTime); }
  static fromJson(json: Json | string): Outcome<FixedOffsetDateTime> { return typedFromJson(json, FixedOffsetDateTime); }

  get dateTime(): CivilDateTime { return newDateTime({ ...this.#c, zone: { type: "floating" } }); }
  get offsetSeconds(): number { return (this.#c.zone as { seconds: number }).seconds; }

  /** The instant it names. No rules needed. */
  toInstant(): Instant { return as(Instant)(total(zones.resolve(this.toJSON(), NO_RULES))); }
  // compare (inherited) orders by instant: 14:30+01:00 equals 13:30+00:00 in order, though not in `equals`.
}

/** Fixed offsets resolve without rules; the JSON layer still takes a rules argument. */
const NO_RULES = new zones.ZoneRules({}, 0n, 0n, new Map());

// ---------------------------------------------------------------------------------------------
// ZonedIntent

/**
 * A wall clock meant in an IANA zone, following the zone's rules: `2027-06-01T09:00[Europe/Paris]`.
 * It isn't an instant yet: `resolve(ctx)` answers `none` in a DST gap and `multiple` in an overlap.
 * It has no `compare`; use `ctx.compare(a, b)`.
 */
export class ZonedIntent extends TemporalValue {
  readonly #c: Civil;
  private constructor(c: Civil) { super(writeCivil(c)); this.#c = c; }
  static { newZoned = (c) => new ZonedIntent(c); }

  static parse(text: string): Outcome<ZonedIntent> { return typedParse(text, ZonedIntent); }
  static fromJson(json: Json | string): Outcome<ZonedIntent> { return typedFromJson(json, ZonedIntent); }

  get dateTime(): CivilDateTime { return newDateTime({ form: "datetime", year: this.#c.year, month: this.#c.month, day: this.#c.day, hour: this.#c.hour, minute: this.#c.minute, second: this.#c.second, nano: this.#c.nano, hasSecond: this.#c.hasSecond, zone: { type: "floating" } }); }
  get zoneId(): string { return (this.#c.zone as { id: string }).id; }
  /** The offset written in the text (`+02:00[Europe/Paris]`), if any. Resolving checks it against the rules. */
  get assertedOffsetSeconds(): number | undefined { return this.#c.offsetAssertion; }

  // Wall-clock arithmetic: the zone is kept, an asserted offset is dropped (it may no longer hold).
  addDays(days: Int): ZonedIntent { return as(ZonedIntent)(total(addPeriod(this, { days }))); }
  addWeeks(weeks: Int): ZonedIntent { return as(ZonedIntent)(total(addPeriod(this, { weeks }))); }
  addHours(hours: Int): ZonedIntent { return as(ZonedIntent)(total(addPeriod(this, { hours }))); }
  addMinutes(minutes: Int): ZonedIntent { return as(ZonedIntent)(total(addPeriod(this, { minutes }))); }
  addSeconds(seconds: Int): ZonedIntent { return as(ZonedIntent)(total(addPeriod(this, { seconds }))); }
  addMonths(months: Int): Outcome<ZonedIntent>;
  addMonths(months: Int, monthEnd: typeof MonthEnd.Constrain): ZonedIntent;
  addMonths(months: Int, monthEnd?: MonthEnd): ZonedIntent | Outcome<ZonedIntent> {
    constrainOnly(monthEnd);
    const r = addPeriod(this, { months }, monthEnd);
    return monthEnd === "constrain" ? as(ZonedIntent)(total(r)) : outcomeOf(r, as(ZonedIntent));
  }

  /** The instant(s) this wall time names under the context's rules. Strict by default. */
  resolve(ctx: TemporalContext, policy?: ResolutionPolicy): Outcome<Instant> {
    return outcomeOf(zones.resolve(this.toJSON(), ctx.rules, policy), as(Instant));
  }
}

// ---------------------------------------------------------------------------------------------
// Instant

/** A POSIX instant: exact seconds since 1970-01-01T00:00:00Z. */
export class Instant extends Ordered<Instant> {
  readonly #t: Rational;
  private constructor(t: Rational) { super(writeInstant(t)); this.#t = t; }
  static { newInstant = (t) => new Instant(t); }

  static fromUnixSeconds(seconds: Int, nanoseconds: Int = 0): Instant {
    return orThrow(attempt("Instant.fromUnixSeconds", () => as(Instant)(build.instant(integer(seconds, "seconds"), integer(nanoseconds, "nanoseconds")))));
  }
  static fromUnixNanoseconds(nanoseconds: bigint): Instant {
    return orThrow(attempt("Instant.fromUnixNanoseconds", () => as(Instant)(build.instant(0, integer(nanoseconds, "nanoseconds")))));
  }
  /** From `Date.now()` or `date.getTime()`. */
  static fromUnixMilliseconds(milliseconds: Int): Instant {
    return orThrow(outcomeOf(native.fromEpochMilliseconds(milliseconds), as(Instant)));
  }
  /** The instant a `Date` holds. Throws a `RangeError` for an Invalid Date. */
  static fromDate(date: Date): Instant { return orThrow(outcomeOf(native.fromDate(date, "instant"), as(Instant))); }
  static parse(text: string): Outcome<Instant> { return typedParse(text, Instant); }
  static fromJson(json: Json | string): Outcome<Instant> { return typedFromJson(json, Instant); }

  /** Whole seconds since the epoch (floor). */
  get unixSeconds(): bigint { return split(this.#t).whole; }
  /** The fraction of the second, in whole nanoseconds (floor of {@link subSecond}). */
  get nanoseconds(): number { const { frac } = split(this.#t); return Number((frac.n * 1_000_000_000n) / frac.d); }
  /** The exact fraction of the second, in [0, 1), in lowest terms: e.g. 1/3 for `PosixSeconds.of(1, 3)` past a whole second. */
  get subSecond(): SubSecond {
    const { frac } = split(this.#t);
    return Object.freeze({ numerator: frac.n, denominator: frac.d });
  }

  /** Exact POSIX arithmetic. */
  add(amount: PosixSeconds): Instant;
  /** SI seconds need a leap-second table: `unsupported` (`needs-leap-seconds`). */
  add(amount: ElapsedSeconds): Outcome<Instant>;
  add(amount: PosixSeconds | ElapsedSeconds): Instant | Outcome<Instant> {
    const r = ops.add(this.toJSON(), amount.toJSON());
    return amount instanceof PosixSeconds ? as(Instant)(total(r)) : outcomeOf(r, as(Instant));
  }
  /** Exact POSIX seconds from this instant to `other`. */
  until(other: Instant): PosixSeconds { return as(PosixSeconds)(total(ops.posixSecondsBetween(this.toJSON(), other.toJSON()))); }
  /** The wall clock at a fixed offset (seconds east of UTC). */
  atOffset(offsetSeconds: number): FixedOffsetDateTime {
    return orThrow(outcomeOf(zones.toOffset(this.toJSON(), offsetSeconds), as(FixedOffsetDateTime)));
  }
  /** The wall clock in a zone, with the offset in force asserted. `unsupported` outside the rules. */
  inZone(zoneId: string, ctx: TemporalContext): Outcome<ZonedIntent> {
    return outcomeOf(zones.toZone(this.toJSON(), zoneId, ctx.rules), as(ZonedIntent));
  }
  /** A `Date`. Sub-millisecond precision is floored, with a `precision-loss` warning. */
  toDate(): Outcome<Date> {
    const r = native.toDate(this.toJSON());
    return r.status === "unique" ? new Unique(r.value!, r.diagnostics, "toDate") : failed(r.status, r.diagnostics, "toDate");
  }
}

// ---------------------------------------------------------------------------------------------
// Amounts

export interface PeriodParts { years?: Int; months?: Int; weeks?: Int; days?: Int; hours?: Int; minutes?: Int; seconds?: Int }

/** A calendar amount: years, months, weeks, days and wall-clock hours, minutes, seconds. `P1M`, `PT1M`. */
export class Period extends TemporalValue {
  readonly #c: Record<Component, bigint>;
  private constructor(c: Record<Component, bigint>) {
    super(build.period(c));
    this.#c = c;
  }
  static { newPeriod = (c) => new Period(c); }

  static of(parts: PeriodParts): Period {
    return orThrow(attempt("Period.of", () => {
      const c = Object.fromEntries(COMPONENTS.map((k) => [k, parts[k] === undefined ? 0n : integer(parts[k], k)])) as Record<Component, bigint>;
      return newPeriod(c);
    }));
  }
  static years(n: Int): Period { return Period.of({ years: n }); }
  static months(n: Int): Period { return Period.of({ months: n }); }
  static weeks(n: Int): Period { return Period.of({ weeks: n }); }
  static days(n: Int): Period { return Period.of({ days: n }); }
  static hours(n: Int): Period { return Period.of({ hours: n }); }
  static minutes(n: Int): Period { return Period.of({ minutes: n }); }
  static seconds(n: Int): Period { return Period.of({ seconds: n }); }
  static parse(text: string): Outcome<Period> { return typedParse(text, Period); }
  static fromJson(json: Json | string): Outcome<Period> { return typedFromJson(json, Period); }

  get years(): bigint { return this.#c.years; }
  get months(): bigint { return this.#c.months; }
  get weeks(): bigint { return this.#c.weeks; }
  get days(): bigint { return this.#c.days; }
  get hours(): bigint { return this.#c.hours; }
  get minutes(): bigint { return this.#c.minutes; }
  get seconds(): bigint { return this.#c.seconds; }

  negate(): Period {
    return newPeriod(Object.fromEntries(COMPONENTS.map((k) => [k, -this.#c[k]])) as Record<Component, bigint>);
  }
}

/** `3600.75`, or `1/3` when the decimal doesn't terminate. */
function decimal(r: Rational): string {
  let d = r.d, twos = 0, fives = 0;
  while (d % 2n === 0n) { d /= 2n; twos++; }
  while (d % 5n === 0n) { d /= 5n; fives++; }
  if (d !== 1n) return `${r.n}/${r.d}`;
  const k = Math.max(twos, fives);
  const scaled = (r.n * 10n ** BigInt(k)) / r.d;
  const digits = (scaled < 0n ? -scaled : scaled).toString().padStart(k + 1, "0");
  const whole = digits.slice(0, digits.length - k), frac = digits.slice(digits.length - k);
  return `${scaled < 0n ? "-" : ""}${whole}${k > 0 ? `.${frac}` : ""}`;
}

function seconds(numerator: Int, denominator: Int): Rational {
  const d = integer(denominator, "denominator");
  if (d === 0n) fail("invalid", "field-out-of-range", "", "the denominator must not be zero");
  return rat(integer(numerator, "numerator"), d);
}

/** An exact amount of POSIX seconds (not SI seconds across a leap second). It has no text form. */
export class PosixSeconds extends TemporalValue {
  readonly #s: Rational;
  private constructor(s: Rational) { super(build.posixSeconds(s.n, s.d)); this.#s = s; }
  static { newPosix = (s) => new PosixSeconds(s); }

  /** `PosixSeconds.of(86_400)`, or an exact fraction `PosixSeconds.of(1, 3)`. */
  static of(numerator: Int, denominator: Int = 1): PosixSeconds {
    return orThrow(attempt("PosixSeconds.of", () => newPosix(seconds(numerator, denominator))));
  }
  static fromJson(json: Json | string): Outcome<PosixSeconds> { return typedFromJson(json, PosixSeconds); }

  get numerator(): bigint { return this.#s.n; }
  get denominator(): bigint { return this.#s.d; }
  negate(): PosixSeconds { return newPosix(rat(-this.#s.n, this.#s.d)); }
  /** A display string, not parseable: `3600.75 s (POSIX)`, `1/3 s (POSIX)`. */
  override toString(): string { return `${decimal(this.#s)} s (POSIX)`; }
}

/** An exact amount of SI seconds, as elapsed on a clock. Adding it to a POSIX instant needs leap seconds. */
export class ElapsedSeconds extends TemporalValue {
  readonly #s: Rational;
  private constructor(s: Rational) {
    super({ schema: build.SCHEMA, kind: "amount", value: { basis: "elapsed", seconds: { numerator: s.n.toString(), denominator: s.d.toString() } } });
    this.#s = s;
  }
  static { newElapsed = (s) => new ElapsedSeconds(s); }

  static of(numerator: Int, denominator: Int = 1): ElapsedSeconds {
    return orThrow(attempt("ElapsedSeconds.of", () => newElapsed(seconds(numerator, denominator))));
  }
  static fromJson(json: Json | string): Outcome<ElapsedSeconds> { return typedFromJson(json, ElapsedSeconds); }

  get numerator(): bigint { return this.#s.n; }
  get denominator(): bigint { return this.#s.d; }
  negate(): ElapsedSeconds { return newElapsed(rat(-this.#s.n, this.#s.d)); }
  /** A display string, not parseable: `86400 s (SI)`. */
  override toString(): string { return `${decimal(this.#s)} s (SI)`; }
}

// ---------------------------------------------------------------------------------------------
// Intervals

/** What an interval can hold: calendar dates or instants. */
export type IntervalPoint = CivilDate | Instant;
type Domain = "civil/gregory" | "physical";

/** An interval endpoint: a point that is included or excluded, no bound at all, or a bound nobody knows. */
export type Bound<T> =
  | { readonly state: "included"; readonly value: T }
  | { readonly state: "excluded"; readonly value: T }
  | { readonly state: "unbounded" }
  | { readonly state: "unknown" };

export const Bound = {
  included: <T>(value: T): Bound<T> => Object.freeze({ state: "included", value }),
  excluded: <T>(value: T): Bound<T> => Object.freeze({ state: "excluded", value }),
  /** There is no end (or no start). */
  unbounded: Object.freeze({ state: "unbounded" }) as { readonly state: "unbounded" },
  /** There is an end, but nobody knows it. */
  unknown: Object.freeze({ state: "unknown" }) as { readonly state: "unknown" },
};

const boundJson = (b: Bound<IntervalPoint>): Json =>
  b.state === "unbounded" || b.state === "unknown" ? { state: b.state } : { state: b.state, point: b.value.toJSON() };

/** Checks an interval's endpoints and returns its domain. Throws `Fail`. */
function checkInterval(start: Bound<IntervalPoint>, end: Bound<IntervalPoint>, domain?: Domain): Domain {
  const points = [start, end].flatMap((b) => ("value" in b ? [b.value] : []));
  for (const p of points) if (!(p instanceof CivilDate || p instanceof Instant)) fail("invalid", "kind-mismatch", "", "interval points are CivilDate or Instant values");
  const d: Domain | undefined = points[0] instanceof Instant ? "physical" : points[0] instanceof CivilDate ? "civil/gregory" : domain;
  if (d === undefined) return fail("unsupported", "unsupported-form", "", "an interval needs at least one point to fix its domain");
  for (const p of points) {
    if ((p instanceof Instant ? "physical" : "civil/gregory") !== d) fail("invalid", "kind-mismatch", "", "an interval's points must be of one kind");
  }
  if (points.length === 2 && total(ops.compare(points[0]!.toJSON(), points[1]!.toJSON())) === "after") {
    fail("invalid", "reversed-interval", "", `the end ${String(points[1])} is before the start ${String(points[0])}`);
  }
  return d;
}

/** A range of dates or instants, e.g. `[2026-11-01, 2026-12-01)`. Ends may be unbounded (`..`) or unknown (`?`). */
export class Interval<T extends IntervalPoint> extends TemporalValue {
  private constructor(readonly start: Bound<T>, readonly end: Bound<T>, domain: Domain) {
    super({ schema: build.SCHEMA, kind: "interval", value: { domain, start: boundJson(start), end: boundJson(end) } });
  }
  static { newInterval = (start, end, domain) => new Interval(start, end, checkInterval(start, end, domain)); }

  /** `[start, end)`: the usual way to say "November". Throws a `RangeError` if the end is before the start. */
  static halfOpen<T extends IntervalPoint>(start: T, end: T): Interval<T> { return Interval.of(Bound.included(start), Bound.excluded(end)); }
  /** `[start, end]`. */
  static closed<T extends IntervalPoint>(start: T, end: T): Interval<T> { return Interval.of(Bound.included(start), Bound.included(end)); }
  static of<T extends IntervalPoint>(start: Bound<T>, end: Bound<T>): Interval<T> { return orThrow(Interval.tryOf(start, end)); }
  static tryOf<T extends IntervalPoint>(start: Bound<T>, end: Bound<T>): Outcome<Interval<T>> {
    return attempt("Interval.of", () => new Interval(start, end, checkInterval(start, end)));
  }
  static parse(text: string): Outcome<Interval<CivilDate> | Interval<Instant>> {
    return typedParse(text, Interval) as Outcome<Interval<CivilDate> | Interval<Instant>>;
  }
  static fromJson(json: Json | string): Outcome<Interval<CivilDate> | Interval<Instant>> {
    return typedFromJson(json, Interval) as Outcome<Interval<CivilDate> | Interval<Instant>>;
  }

  /** `Definitely`, `Possibly` (an unknown end leaves it open) or `Impossible`. */
  contains(point: T): Truth { return total(ops.contains(this.toJSON(), point.toJSON())) as Truth; }
  /** Whether the two intervals share a point. */
  overlaps(other: Interval<T>): Truth { return total(ops.overlaps(this.toJSON(), other.toJSON())) as Truth; }
}

// ---------------------------------------------------------------------------------------------
// Any value

/** Every typed value. Narrow it with `instanceof`. */
export type UtvValue =
  | CivilDate | CivilYearMonth | CivilTime | CivilDateTime | FixedOffsetDateTime | ZonedIntent | Instant
  | Period | PosixSeconds | ElapsedSeconds | Interval<CivilDate> | Interval<Instant>;

/** Parses any value in the text form. Bad text is an `Outcome`, never an exception. */
export function parse(text: string): Outcome<UtvValue> {
  const r = parseText(text);
  if (r.status !== "unique") return failed(r.status, r.diagnostics, "parse");
  return attempt("parse", () => decode(r.candidates[0]!));
}

/** Any UTV JSON value (an object or its text) this library has a type for. */
export function fromJson(json: Json | string): Outcome<UtvValue> {
  return typedFromJson(json, TemporalValue as unknown as Class<UtvValue>);
}
