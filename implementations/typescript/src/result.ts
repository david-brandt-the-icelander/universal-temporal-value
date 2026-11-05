/** utv-result/1.0 result contract (spec/RESULT-CONTRACT.md). */
export type Status =
  | "invalid" | "unsupported" | "indeterminate" | "none" | "unique" | "multiple"
  | "range" | "symbolic" | "resource-limit" | "error" | "valid";

export type Severity = "error" | "warning" | "info";

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
  operation: "validate";
  status: Status;
  manifest: { dependencies: unknown[] };
  assumptions: unknown[];
  loss: { classification: "lossless"; fields: string[] };
  diagnostics: Diagnostic[];
}

export function makeResult(status: Status, diagnostics: Diagnostic[]): UtvResult {
  return {
    schema: "utv-result/1.0",
    operation: "validate",
    status,
    manifest: { dependencies: [] },
    assumptions: [],
    loss: { classification: "lossless", fields: [] },
    diagnostics,
  };
}
