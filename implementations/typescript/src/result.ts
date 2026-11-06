/** utv-result/1.0 result contract (spec/RESULT-CONTRACT.md). */
import type { Json } from "./json.js";

export type Status =
  | "invalid" | "unsupported" | "indeterminate" | "none" | "unique" | "multiple"
  | "range" | "symbolic" | "resource-limit" | "error" | "valid";

export type Severity = "error" | "warning" | "info";

export type LossClassification = "lossless" | "information-losing" | "assumption-dependent" | "unrepresentable";

export interface Diagnostic {
  code: string;
  severity: Severity;
  /** JSON Pointer into the input. */
  path: string;
  message: string;
  evidenceRefs?: string[];
}

export interface UtvResult {
  schema: "utv-result/1.0";
  /** `validate` for validation; the everyday operation name (`parseIso`, `add`, `resolve`, …) otherwise. */
  operation: string;
  status: Status;
  /**
   * UTV values, or plain JSON for operations that answer with a string, number or object.
   * `unique` has exactly one, `multiple` all of them in ascending order, every other status none.
   */
  candidates: Json[];
  manifest: { dependencies: Json[] };
  assumptions: unknown[];
  loss: { classification: LossClassification; fields: string[] };
  diagnostics: Diagnostic[];
}

export interface ResultOptions {
  operation?: string;
  candidates?: Json[];
  dependencies?: Json[];
  loss?: { classification: LossClassification; fields: string[] };
}

export function makeResult(status: Status, diagnostics: Diagnostic[], options: ResultOptions = {}): UtvResult {
  return {
    schema: "utv-result/1.0",
    operation: options.operation ?? "validate",
    status,
    candidates: options.candidates ?? [],
    manifest: { dependencies: options.dependencies ?? [] },
    assumptions: [],
    loss: options.loss ?? { classification: "lossless", fields: [] },
    diagnostics,
  };
}

/** The single candidate of a `unique` result, otherwise `undefined`. */
export function candidate(result: UtvResult): Json | undefined {
  return result.status === "unique" ? result.candidates[0] : undefined;
}
