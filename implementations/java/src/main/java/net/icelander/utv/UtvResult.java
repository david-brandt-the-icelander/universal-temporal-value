package net.icelander.utv;

import com.fasterxml.jackson.databind.JsonNode;
import com.fasterxml.jackson.databind.node.ArrayNode;
import com.fasterxml.jackson.databind.node.JsonNodeFactory;
import com.fasterxml.jackson.databind.node.ObjectNode;
import java.util.List;
import java.util.Optional;

/**
 * A utv-result/1.0 envelope (subset: schema, operation, status, diagnostics, candidates).
 *
 * <p>Candidates are UTV values, or plain JSON for operations that answer with a string, number or
 * object ({@code compare}, {@code fields}, {@code daysBetween}, {@code contains}, {@code overlaps},
 * {@code formatIso}). {@code unique} has exactly one candidate, {@code multiple} has all of them in
 * ascending order, every other status has none.
 */
public record UtvResult(String operation, Status status, List<Diagnostic> diagnostics, List<JsonNode> candidates) {
    public static final String SCHEMA = "utv-result/1.0";

    public UtvResult {
        diagnostics = List.copyOf(diagnostics);
        candidates = List.copyOf(candidates);
    }

    /** A result without candidates (validation, and every status other than unique/multiple). */
    public UtvResult(String operation, Status status, List<Diagnostic> diagnostics) {
        this(operation, status, diagnostics, List.of());
    }

    public String schema() { return SCHEMA; }

    /** The single candidate when the status is {@code unique}; empty otherwise. */
    public Optional<JsonNode> value() {
        return status == Status.UNIQUE && candidates.size() == 1 ? Optional.of(candidates.get(0)) : Optional.empty();
    }

    /** True when the status is {@code unique}. */
    public boolean isUnique() { return status == Status.UNIQUE; }

    /** This result as utv-result/1.0 JSON. */
    public ObjectNode toJson() {
        JsonNodeFactory f = JsonNodeFactory.instance;
        ObjectNode o = f.objectNode().put("schema", SCHEMA).put("operation", operation).put("status", status.wire());
        ArrayNode c = o.putArray("candidates");
        candidates.forEach(c::add);
        ArrayNode d = o.putArray("diagnostics");
        for (Diagnostic x : diagnostics) {
            d.addObject().put("code", x.code()).put("severity", x.severity()).put("path", x.path()).put("message", x.message());
        }
        return o;
    }

    static UtvResult unique(String op, JsonNode candidate) {
        return new UtvResult(op, Status.UNIQUE, List.of(), List.of(candidate));
    }

    static UtvResult of(String op, Status status, Diagnostic d) {
        return new UtvResult(op, status, List.of(d), List.of());
    }
}
