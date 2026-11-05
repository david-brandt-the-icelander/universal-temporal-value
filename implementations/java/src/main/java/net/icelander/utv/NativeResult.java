package net.icelander.utv;

import java.util.List;
import java.util.Optional;

/**
 * The outcome of converting a UTV value to a {@code java.time} type. {@code status} is
 * {@code unique} when {@code value} is present. {@code lossy} is true when the native type could not
 * hold everything (a zone id, sub-nanosecond precision); the diagnostics say what was dropped.
 */
public record NativeResult<T>(Status status, T value, boolean lossy, List<Diagnostic> diagnostics) {
    public NativeResult { diagnostics = List.copyOf(diagnostics); }

    /** The converted value when the status is {@code unique}. */
    public Optional<T> get() { return status == Status.UNIQUE ? Optional.ofNullable(value) : Optional.empty(); }

    static <T> NativeResult<T> exact(T value) { return new NativeResult<>(Status.UNIQUE, value, false, List.of()); }

    /** A lossy conversion: {@code zone-dropped} when the loss is at a zone path, {@code precision-loss} otherwise. */
    static <T> NativeResult<T> lossy(T value, String path, String whatWasLost) {
        String code = path.endsWith("/zone") ? "zone-dropped" : "precision-loss";
        return new NativeResult<>(Status.UNIQUE, value, true, List.of(new Diagnostic(code, "warning", path, whatWasLost)));
    }

    static <T> NativeResult<T> failed(Fail f) { return new NativeResult<>(f.status, null, false, f.diagnostics); }
}
