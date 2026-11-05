package net.icelander.utv;

import java.util.List;

/** A utv-result/1.0 envelope (subset: schema, operation, status, diagnostics). */
public record UtvResult(String operation, Status status, List<Diagnostic> diagnostics) {
    public static final String SCHEMA = "utv-result/1.0";
    public UtvResult { diagnostics = List.copyOf(diagnostics); }
    public String schema() { return SCHEMA; }
}
