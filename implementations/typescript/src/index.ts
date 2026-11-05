export { parseStrict, JsonError, type Json } from "./json.js";
export { makeResult, type Status, type Severity, type Diagnostic, type UtvResult } from "./result.js";
export { validateStructural, validateParsed, KINDS } from "./structural.js";
export const SPEC_VERSION = "utv/1.0";
export const LIBRARY_VERSION = "0.1.0";
export { validateSemantic, isLeapYear, daysInMonth } from "./civil.js";
