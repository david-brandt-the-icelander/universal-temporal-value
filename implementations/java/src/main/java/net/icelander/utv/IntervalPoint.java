package net.icelander.utv;

/**
 * A value an {@link Interval} can hold: a {@link CivilDate} or an {@link Instant}. Both are
 * totally ordered, so {@link Interval#contains} and {@link Interval#overlaps} are well defined.
 */
public sealed interface IntervalPoint<T extends IntervalPoint<T>> extends TemporalValue, Comparable<T>
        permits CivilDate, Instant {
}
