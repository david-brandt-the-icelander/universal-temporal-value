/**
 * `Outcome<T>` (docs/library-api.md §3): the answer to a question time can refuse to answer.
 *
 * A discriminated union on `status`. Narrow it with `switch (o.status)` and finish with
 * `default: assertNever(o)`, so that a missing case fails to type-check.
 */
import type { Json } from "./json.js";
import { makeResult, type Diagnostic, type UtvResult } from "./result.js";

/** The five statuses an `Outcome` can have (the wire strings of `utv-result/1.0`). */
export type OutcomeStatus = "unique" | "multiple" | "none" | "unsupported" | "invalid";

/** Thrown by {@link OutcomeBase.single} when the outcome is not `unique`. */
export class OutcomeError extends Error {
  override readonly name = "OutcomeError";
  constructor(readonly status: OutcomeStatus, readonly diagnostics: readonly Diagnostic[]) {
    super(`expected exactly one answer, got ${describe(status, diagnostics)}${diagnostics[0] ? `: ${diagnostics[0].message}` : ""}`);
  }
}

const describe = (status: string, diagnostics: readonly Diagnostic[]) =>
  diagnostics.length === 0 ? status : `${status} (${diagnostics.map((d) => d.code).join(", ")})`;

/** A typed value's UTV JSON, or the plain answer itself (an ordering, a truth value, a number). */
const toJson = (x: unknown): Json =>
  typeof x === "object" && x !== null && typeof (x as { toJSON?: unknown }).toJSON === "function"
    ? ((x as { toJSON(): Json }).toJSON())
    : (x as Json);

/** What every outcome can do. None of these helpers ever picks an answer silently. */
abstract class OutcomeBase<T> {
  abstract readonly status: OutcomeStatus;
  readonly #operation: string;
  constructor(readonly diagnostics: readonly Diagnostic[], operation: string) {
    this.#operation = operation;
  }

  /** The unique value, or `fallback` for any other status. */
  uniqueOr<F>(fallback: F): T | F {
    const self = this as unknown as Outcome<T>;
    return self.status === "unique" ? self.value : fallback;
  }

  /** The unique value; throws {@link OutcomeError} (with the status and diagnostics) otherwise. */
  single(): T {
    const self = this as unknown as Outcome<T>;
    if (self.status === "unique") return self.value;
    throw new OutcomeError(self.status, self.diagnostics);
  }

  /** The value's text when unique; otherwise the status and the diagnostic codes, e.g. `none (month-end)`. */
  toString(): string {
    const self = this as unknown as Outcome<T>;
    return self.status === "unique" ? String(self.value) : describe(self.status, self.diagnostics);
  }

  /** The `utv-result/1.0` envelope of the JSON layer. */
  toResult(): UtvResult {
    const self = this as unknown as Outcome<T>;
    const candidates = self.status === "unique" ? [toJson(self.value)] : self.status === "multiple" ? self.values.map(toJson) : [];
    return makeResult(self.status, [...self.diagnostics], { operation: this.#operation, candidates });
  }
}

/** Exactly one answer. */
export class Unique<T> extends OutcomeBase<T> {
  readonly status = "unique";
  constructor(readonly value: T, diagnostics: readonly Diagnostic[] = [], operation = "outcome") { super(diagnostics, operation); }
}

/** Several answers, ascending: e.g. both instants of a DST overlap. */
export class Multiple<T> extends OutcomeBase<T> {
  readonly status = "multiple";
  constructor(readonly values: readonly T[], diagnostics: readonly Diagnostic[] = [], operation = "outcome") { super(diagnostics, operation); }
}

/** Proven: no answer exists (a day past the month end, a wall time in a DST gap). */
export class None<T> extends OutcomeBase<T> {
  readonly status = "none";
  constructor(diagnostics: readonly Diagnostic[] = [], operation = "outcome") { super(diagnostics, operation); }
}

/** This library lacks the rules or the capability to answer. */
export class Unsupported<T> extends OutcomeBase<T> {
  readonly status = "unsupported";
  constructor(diagnostics: readonly Diagnostic[] = [], operation = "outcome") { super(diagnostics, operation); }
}

/** The input contradicts itself or the rules. */
export class Invalid<T> extends OutcomeBase<T> {
  readonly status = "invalid";
  constructor(diagnostics: readonly Diagnostic[] = [], operation = "outcome") { super(diagnostics, operation); }
}

export type Outcome<T> = Unique<T> | Multiple<T> | None<T> | Unsupported<T> | Invalid<T>;

/**
 * Marks the end of an exhaustive `switch`: `default: assertNever(o)`. If a case is missing, `o` is
 * not `never` there and the call fails to type-check. At run time it throws.
 */
export function assertNever(x: never, message = "unexpected case"): never {
  throw new Error(`${message}: ${JSON.stringify((x as { status?: unknown })?.status ?? x)}`);
}

/** An outcome without an answer, from a status and its diagnostics. Internal. */
export function failed<T>(status: string, diagnostics: readonly Diagnostic[], operation: string): Outcome<T> {
  switch (status) {
    case "none": return new None(diagnostics, operation);
    case "unsupported": return new Unsupported(diagnostics, operation);
    case "invalid": return new Invalid(diagnostics, operation);
    default: throw new Error(`${operation}: unexpected status ${describe(status, diagnostics)}`);
  }
}

/** Converts a JSON-layer result, mapping each candidate. Internal. */
export function outcomeOf<T>(r: UtvResult, map: (candidate: Json) => T): Outcome<T> {
  switch (r.status) {
    case "unique": return new Unique(map(r.candidates[0]!), r.diagnostics, r.operation);
    case "multiple": return new Multiple(r.candidates.map(map), r.diagnostics, r.operation);
    default: return failed(r.status, r.diagnostics, r.operation);
  }
}

// ---------------------------------------------------------------------------------------------
// Small typed vocabularies (each is also its own string-union type, so literals are accepted too)

/** Three-valued answer of `contains` and `overlaps`. */
export const Truth = { Definitely: "definitely", Possibly: "possibly", Impossible: "impossible" } as const;
export type Truth = (typeof Truth)[keyof typeof Truth];

/** The order of two values, `a` relative to `b`. */
export const Ordering = { Before: "before", Equal: "equal", After: "after" } as const;
export type Ordering = (typeof Ordering)[keyof typeof Ordering];

/** ISO day of week: Monday is 1, Sunday is 7. `Weekday[d.dayOfWeek]` is the English name. */
export enum Weekday { Monday = 1, Tuesday, Wednesday, Thursday, Friday, Saturday, Sunday }

/** What adding months does when the day doesn't exist in the target month (31 Jan + 1 month). */
export const MonthEnd = { Reject: "reject", Constrain: "constrain" } as const;
export type MonthEnd = (typeof MonthEnd)[keyof typeof MonthEnd];

/** What resolving does with a wall time skipped by a transition (spring forward). */
export const Gap = { Reject: "reject", PreserveUnresolved: "preserve-unresolved", ShiftForward: "shift-forward-by-gap" } as const;
export type Gap = (typeof Gap)[keyof typeof Gap];

/** What resolving does with a wall time that happens twice (fall back). */
export const Overlap = { Reject: "reject", All: "all", Earliest: "earliest", Latest: "latest" } as const;
export type Overlap = (typeof Overlap)[keyof typeof Overlap];

/** A gap and an overlap policy. A missing one means `reject`. */
export interface ResolutionPolicy { readonly gap?: Gap; readonly overlap?: Overlap }
export const ResolutionPolicy = {
  /** Reject gaps (`none`) and overlaps (`multiple`). The default. */
  Strict: Object.freeze({ gap: Gap.Reject, overlap: Overlap.Reject }) as ResolutionPolicy,
  /** Every candidate: `multiple` in an overlap, `none` in a gap, without an `ambiguous` diagnostic. */
  AllCandidates: Object.freeze({ gap: Gap.Reject, overlap: Overlap.All }) as ResolutionPolicy,
  of: (gap: Gap, overlap: Overlap): ResolutionPolicy => Object.freeze({ gap, overlap }),
};
