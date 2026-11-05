package net.icelander.utv;

import java.util.List;
import java.util.stream.Collectors;

/** Thrown by {@link Outcome#single()} when the outcome isn't {@link Outcome.Unique}. */
public final class OutcomeException extends RuntimeException {
    private final Status status;
    private final transient List<Diagnostic> diagnostics;

    public OutcomeException(Status status, List<Diagnostic> diagnostics) {
        super("expected a unique answer, got " + status.wire() + (diagnostics.isEmpty() ? "" : ": "
                + diagnostics.stream().map(d -> d.message() + " (" + d.code() + ")").collect(Collectors.joining("; "))));
        this.status = status;
        this.diagnostics = List.copyOf(diagnostics);
    }

    public Status status() { return status; }

    public List<Diagnostic> diagnostics() { return diagnostics; }
}
