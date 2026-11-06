package net.icelander.utv;

import com.fasterxml.jackson.databind.JsonNode;
import com.fasterxml.jackson.databind.node.ObjectNode;
import java.util.List;
import java.util.Objects;
import java.util.function.Function;

/**
 * A range of dates or instants with explicit ends: {@code [2026-11-01, 2026-12-01)},
 * {@code [2026-11-01, ..)} (no end), {@code [2026-11-01, ?)} (an end nobody knows yet).
 * {@link #contains} and {@link #overlaps} answer {@link Truth#DEFINITELY}, {@link Truth#POSSIBLY}
 * or {@link Truth#IMPOSSIBLE}.
 */
public final class Interval<T extends IntervalPoint<T>> implements TemporalValue {
    private final Bound<T> start, end;

    private Interval(Bound<T> start, Bound<T> end) {
        this.start = start;
        this.end = end;
    }

    /** {@code [start, end)}: the usual range, start included and end excluded. */
    public static <T extends IntervalPoint<T>> Interval<T> halfOpen(T start, T end) { return of(Bound.included(start), Bound.excluded(end)); }

    /** {@code [start, end]}: both ends included. */
    public static <T extends IntervalPoint<T>> Interval<T> closed(T start, T end) { return of(Bound.included(start), Bound.included(end)); }

    /** Throws {@link IllegalArgumentException} when the end is before the start, or neither end has a point. */
    public static <T extends IntervalPoint<T>> Interval<T> of(Bound<T> start, Bound<T> end) { return Typed.argument(tryOf(start, end)); }

    /**
     * {@code invalid} ({@code reversed-interval}) when the end is before the start; {@code unsupported}
     * ({@code unsupported-form}) when neither end has a point, as for the text {@code (.., ..)}.
     */
    public static <T extends IntervalPoint<T>> Outcome<Interval<T>> tryOf(Bound<T> start, Bound<T> end) {
        try {
            return Outcome.unique(build(start, end));
        } catch (Fail f) {
            return Typed.failure(f);
        }
    }

    private static <T extends IntervalPoint<T>> Interval<T> build(Bound<T> start, Bound<T> end) {
        T a = Objects.requireNonNull(start, "start").pointOrNull(), b = Objects.requireNonNull(end, "end").pointOrNull();
        if (a == null && b == null) throw Fail.unsupported("unsupported-form", "", "an interval needs a point on at least one end to fix its domain");
        if (a != null && b != null && a.compareTo(b) > 0) throw Fail.invalid("reversed-interval", "", "the end " + b + " is before the start " + a);
        return new Interval<>(start, end);
    }

    /** Parses {@code [2026-11-01, 2026-12-01)} (and the EDTF forms {@code 2026-11-01/..}) as an interval of {@code type}. */
    public static <T extends IntervalPoint<T>> Outcome<Interval<T>> parse(String text, Class<T> type) {
        return Typed.parse(text, j -> fromJson(j, type));
    }

    /** Parses an interval of dates or instants. */
    public static Outcome<Interval<?>> parse(String text) { return Typed.parse(text, Interval::fromJson); }

    /** Reads a {@code start-end} interval of {@code type} points; other point types are {@code invalid} ({@code kind-mismatch}). */
    @SuppressWarnings("unchecked")
    public static <T extends IntervalPoint<T>> Outcome<Interval<T>> fromJson(JsonNode json, Class<T> type) {
        Outcome<Interval<?>> o = fromJson(json);
        if (o instanceof Outcome.Unique<Interval<?>> u) {
            Interval<?> i = u.value();
            Object p = i.start.pointOrNull() != null ? i.start.pointOrNull() : i.end.pointOrNull();
            if (!type.isInstance(p)) {
                return new Outcome.Invalid<>(List.of(new Diagnostic("kind-mismatch", "error", "/value/domain",
                        "expected an interval of " + type.getSimpleName() + ", got one of " + p.getClass().getSimpleName())));
            }
        }
        return o.map(i -> (Interval<T>) i);
    }

    /** Reads a {@code start-end} interval: {@code civil/gregory} dates or {@code physical} instants. */
    public static Outcome<Interval<?>> fromJson(JsonNode json) {
        return Typed.decode(json, j -> {
            Typed.requireKind(j, "interval");
            JsonNode v = j.get("value");
            if (!"start-end".equals(v.path("construction").asText("start-end"))) {
                throw Fail.unsupported("unsupported-operand", "/value/construction", "only start-end intervals have a typed form");
            }
            return switch (v.path("domain").asText("")) {
                case "civil/gregory" -> decode(v, CivilDate::fromJson);
                case "physical" -> decode(v, Instant::fromJson);
                default -> throw Fail.unsupported("unsupported-operand", "/value/domain", "only civil/gregory and physical intervals have a typed form");
            };
        });
    }

    private static <T extends IntervalPoint<T>> Interval<T> decode(JsonNode v, Function<JsonNode, Outcome<T>> read) {
        return build(bound(v.get("start"), read), bound(v.get("end"), read));
    }

    private static <T> Bound<T> bound(JsonNode e, Function<JsonNode, Outcome<T>> read) {
        String state = e == null ? "" : e.path("state").asText("");
        return switch (state) {
            case "unbounded" -> Bound.unbounded();
            case "unknown" -> Bound.unknown();
            case "included", "excluded" -> {
                Outcome<T> p = read.apply(e.get("point"));
                if (!(p instanceof Outcome.Unique<T> u)) {
                    Diagnostic d = p.diagnostics().get(0);
                    throw new Fail(p.status(), d.code(), d.path(), "endpoint: " + d.message());
                }
                yield state.equals("included") ? Bound.included(u.value()) : Bound.excluded(u.value());
            }
            default -> throw Fail.invalid("malformed-value", "", "unknown endpoint state " + state);
        };
    }

    @Override public ObjectNode toJson() {
        return Values.interval(json(start), json(end), start instanceof Bound.Included<T>, end instanceof Bound.Included<T>);
    }

    private static JsonNode json(Bound<?> b) {
        return switch (b) {
            case Bound.Included<?> i -> i.value() instanceof TemporalValue t ? t.toJson() : null;
            case Bound.Excluded<?> x -> x.value() instanceof TemporalValue t ? t.toJson() : null;
            case Bound.Unbounded<?> u -> Values.unbounded();
            case Bound.Unknown<?> u -> Values.unknown();
        };
    }

    public Bound<T> start() { return start; }
    public Bound<T> end() { return end; }

    /** Whether the point is in the range; {@link Truth#POSSIBLY} when that depends on an unknown end. */
    public Truth contains(T point) {
        return Typed.total(Intervals.contains(toJson(), point.toJson()), j -> Truth.fromWire(j.textValue()));
    }

    /** Whether the two ranges share a point; {@link Truth#POSSIBLY} when that depends on an unknown end. */
    public Truth overlaps(Interval<T> other) {
        return Typed.total(Intervals.overlaps(toJson(), other.toJson()), j -> Truth.fromWire(j.textValue()));
    }

    @Override public boolean equals(Object o) { return o instanceof Interval<?> i && start.equals(i.start) && end.equals(i.end); }

    @Override public int hashCode() { return start.hashCode() * 31 + end.hashCode(); }

    /** The text form, {@code [2026-11-01, 2026-12-01)}. */
    @Override public String toString() { return Typed.text(this); }
}
