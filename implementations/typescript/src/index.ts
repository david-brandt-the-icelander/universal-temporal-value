export { parseStrict, JsonError, type Json } from "./json.js";
export {
  makeResult, candidate,
  type Status, type Severity, type Diagnostic, type UtvResult, type LossClassification, type ResultOptions,
} from "./result.js";
export { validateStructural, validateParsed, KINDS } from "./structural.js";
export const SPEC_VERSION = "utv/1.0";
export const LIBRARY_VERSION = "0.3.0";
export { validateSemantic, isLeapYear, daysInMonth, daysFromCivil, civilFromDays } from "./civil.js";

// Everyday API (0.2): builders, ISO text, calendar arithmetic, zone resolution, native interop.
export {
  date, yearMonth, time, dateTime, inZone, withOffset, instant, period, posixSeconds, interval,
  GREGORY, POSIX, SCHEMA, type Int, type PeriodParts, type OpenEnd,
} from "./builders.js";
export { parseIso, formatIso } from "./iso.js";
export { parseText, formatText } from "./text.js";
export { fields, add, compare, daysBetween, posixSecondsBetween, contains, overlaps, type AddPolicy } from "./ops.js";
export {
  resolve, toZone, toOffset, loadRules, ZoneRules, RulesError, type GapPolicy, type OverlapPolicy,
} from "./zones.js";
export {
  fromDate, toDate, fromEpochMilliseconds, toEpochMilliseconds, fromPlainDate, toPlainDate,
  type DateIntent, type NativeResult, type PlainDate,
} from "./native.js";

// Typed layer (0.3, docs/library-api.md): typed values, outcomes, policies and the context.
export {
  Unique, Multiple, None, Unsupported, Invalid, OutcomeError, assertNever,
  Truth, Ordering, Weekday, MonthEnd, Gap, Overlap, ResolutionPolicy,
  type Outcome, type OutcomeStatus,
} from "./outcome.js";
export {
  TemporalValue, Ordered, CivilDate, CivilYearMonth, CivilTime, CivilDateTime, FixedOffsetDateTime, ZonedIntent, Instant,
  Period, PosixSeconds, ElapsedSeconds, Interval, Bound, parse, fromJson,
  type UtvValue, type IntervalPoint, type SubSecond,
} from "./values.js";
export { TemporalContext } from "./context.js";
