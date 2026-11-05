package net.icelander.utv;

import com.fasterxml.jackson.databind.JsonNode;
import java.util.List;
import java.util.Objects;
import java.util.Optional;
import java.util.function.Function;
import java.util.stream.Collectors;

/**
 * The answer to a question time can refuse: exactly one value ({@link Unique}), several
 * ({@link Multiple}), provably none ({@link None}), not something this library can answer
 * ({@link Unsupported}), or an input that contradicts itself or the rules ({@link Invalid}).
 *
 * <p>Only partial operations return an {@code Outcome}. Handle it with a pattern {@code switch};
 * the compiler checks that every case is covered:
 *
 * <pre>{@code
 * switch (CivilDate.of(2026, 1, 31).addMonths(1)) {
 *     case Unique<CivilDate> u      -> use(u.value());
 *     case Multiple<CivilDate> m    -> choose(m.values());
 *     case None<CivilDate> n        -> report(n.why());   // 2026-02 has no day 31 (month-end)
 *     case Unsupported<CivilDate> x -> report(x.why());
 *     case Invalid<CivilDate> x     -> report(x.why());
 * }
 * }</pre>
 *
 * <p>Every case carries diagnostics, possibly empty. None of the helpers ever picks an answer
 * silently: {@link #single()} throws unless the outcome is {@link Unique}.
 */
public sealed interface Outcome<T> {
    /** Diagnostics: {@code {code, severity, path, message}}. The codes are shared by every UTV library. */
    List<Diagnostic> diagnostics();

    /** Exactly one answer. */
    record Unique<T>(T value, List<Diagnostic> diagnostics) implements Outcome<T> {
        public Unique {
            Objects.requireNonNull(value, "value");
            diagnostics = List.copyOf(diagnostics);
        }
        @Override public String toString() { return describe(this); }
    }

    /** Several answers, in ascending order (both instants of a DST overlap). */
    record Multiple<T>(List<T> values, List<Diagnostic> diagnostics) implements Outcome<T> {
        public Multiple {
            values = List.copyOf(values);
            diagnostics = List.copyOf(diagnostics);
        }
        @Override public String toString() { return describe(this); }
    }

    /** Proven: no answer exists (31 February, a wall time in a DST gap). */
    record None<T>(List<Diagnostic> diagnostics) implements Outcome<T> {
        public None { diagnostics = List.copyOf(diagnostics); }
        @Override public String toString() { return describe(this); }
    }

    /** This library lacks the rules or the capability to answer. */
    record Unsupported<T>(List<Diagnostic> diagnostics) implements Outcome<T> {
        public Unsupported { diagnostics = List.copyOf(diagnostics); }
        @Override public String toString() { return describe(this); }
    }

    /** The input contradicts itself or the rules. */
    record Invalid<T>(List<Diagnostic> diagnostics) implements Outcome<T> {
        public Invalid { diagnostics = List.copyOf(diagnostics); }
        @Override public String toString() { return describe(this); }
    }

    /** A {@link Unique} outcome without diagnostics. */
    static <T> Outcome<T> unique(T value) { return new Unique<>(value, List.of()); }

    /** The status ({@code Status.UNIQUE} and so on); {@code status().wire()} is the JSON spelling. */
    default Status status() {
        return switch (this) {
            case Unique<T> u -> Status.UNIQUE;
            case Multiple<T> m -> Status.MULTIPLE;
            case None<T> n -> Status.NONE;
            case Unsupported<T> u -> Status.UNSUPPORTED;
            case Invalid<T> i -> Status.INVALID;
        };
    }

    /** The value when this is {@link Unique}; empty for every other case. */
    default Optional<T> tryGetUnique() {
        return this instanceof Unique<T> u ? Optional.of(u.value()) : Optional.empty();
    }

    /** The value when this is {@link Unique}; otherwise {@code fallback}. */
    default T uniqueOr(T fallback) {
        return this instanceof Unique<T> u ? u.value() : fallback;
    }

    /** The value when this is {@link Unique}; otherwise throws {@link OutcomeException} with the status and diagnostics. */
    default T single() {
        if (this instanceof Unique<T> u) return u.value();
        throw new OutcomeException(status(), diagnostics());
    }

    /** The diagnostics as text, {@code "message (code)"}, joined with {@code "; "}; empty when there are none. */
    default String why() {
        return diagnostics().stream().map(d -> d.message() + " (" + d.code() + ")").collect(Collectors.joining("; "));
    }

    /** The same outcome with each value mapped; the diagnostics are kept. */
    default <R> Outcome<R> map(Function<? super T, ? extends R> f) {
        return switch (this) {
            case Unique<T> u -> new Unique<>(f.apply(u.value()), u.diagnostics());
            case Multiple<T> m -> new Multiple<R>(m.values().stream().<R>map(f).toList(), m.diagnostics());
            case None<T> n -> new None<>(n.diagnostics());
            case Unsupported<T> x -> new Unsupported<>(x.diagnostics());
            case Invalid<T> x -> new Invalid<>(x.diagnostics());
        };
    }

    /** The {@code utv-result/1.0} envelope of the JSON layer (operation {@code "outcome"}). */
    default UtvResult toResult() {
        List<JsonNode> candidates = switch (this) {
            case Unique<T> u -> List.of(Typed.toJsonCandidate(u.value()));
            case Multiple<T> m -> m.values().stream().map(Typed::toJsonCandidate).toList();
            default -> List.of();
        };
        return new UtvResult("outcome", status(), diagnostics(), candidates);
    }

    /** The value's text for {@link Unique}; otherwise the status and the codes, e.g. {@code none (month-end)}. */
    private static String describe(Outcome<?> o) {
        if (o instanceof Unique<?> u) return String.valueOf(u.value());
        String codes = o.diagnostics().stream().map(Diagnostic::code).collect(Collectors.joining(", "));
        return o.status().wire() + (codes.isEmpty() ? "" : " (" + codes + ")");
    }
}
