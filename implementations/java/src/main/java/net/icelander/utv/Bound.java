package net.icelander.utv;

/**
 * One end of an {@link Interval}: a point that is {@link Included} or {@link Excluded}, no end at
 * all ({@link Unbounded}), or an end that exists but nobody knows ({@link Unknown}).
 */
public sealed interface Bound<T> {
    record Included<T>(T value) implements Bound<T> {}
    record Excluded<T>(T value) implements Bound<T> {}
    record Unbounded<T>() implements Bound<T> {}
    record Unknown<T>() implements Bound<T> {}

    static <T> Bound<T> included(T value) { return new Included<>(java.util.Objects.requireNonNull(value)); }
    static <T> Bound<T> excluded(T value) { return new Excluded<>(java.util.Objects.requireNonNull(value)); }
    static <T> Bound<T> unbounded() { return new Unbounded<>(); }
    static <T> Bound<T> unknown() { return new Unknown<>(); }

    /** The point, or null for {@link Unbounded} and {@link Unknown}. */
    default T pointOrNull() {
        return switch (this) {
            case Included<T> i -> i.value();
            case Excluded<T> e -> e.value();
            case Unbounded<T> u -> null;
            case Unknown<T> u -> null;
        };
    }
}
