package net.icelander.utv;

import java.util.ArrayList;
import java.util.List;
import java.util.Objects;
import java.util.TreeSet;

/**
 * The zone rules for a piece of work, held once instead of passed to every call. There is no
 * default context: you build one from rules you loaded, so the dependency is visible.
 *
 * <pre>{@code
 * var ctx = TemporalContext.with(ZoneRules.load(Path.of("conformance/rules/demo-zones.json")));
 * }</pre>
 */
public final class TemporalContext {
    private final ZoneRules rules;

    private TemporalContext(ZoneRules rules) { this.rules = rules; }

    public static TemporalContext with(ZoneRules rules) { return new TemporalContext(Objects.requireNonNull(rules, "rules")); }

    public ZoneRules rules() { return rules; }

    /** The instant(s) for a zoned wall time, rejecting gaps ({@code none}) and overlaps ({@code multiple}). */
    public Outcome<Instant> resolve(ZonedIntent zoned) { return resolve(zoned, ResolutionPolicy.STRICT); }

    /** The instant(s) for a zoned wall time under a policy; {@code unsupported} outside the rules. */
    public Outcome<Instant> resolve(ZonedIntent zoned, ResolutionPolicy policy) {
        return Typed.outcome(Zones.resolve(zoned.toJson(), rules.bundle(), Objects.requireNonNull(policy, "policy")), Instant::read);
    }

    /** The instant as a wall time in the zone, with the offset in force asserted. */
    public Outcome<ZonedIntent> inZone(Instant instant, String zoneId) {
        return Typed.outcome(Zones.toZone(instant.toJson(), zoneId, rules.bundle()), ZonedIntent::read);
    }

    /**
     * Orders two values when that needs zone rules. Zoned values are resolved strictly first: a
     * gap gives {@code none}; an overlap gives {@link Outcome.Multiple} when the order depends on
     * which of its instants is meant. Values without a zone compare as {@code compareTo} would.
     */
    public Outcome<Ordering> compare(TemporalValue a, TemporalValue b) {
        if (!(a instanceof ZonedIntent) && !(b instanceof ZonedIntent)) {
            return Typed.outcome(Operations.compare(a.toJson(), b.toJson()), j -> Ordering.fromWire(j.textValue()));
        }
        Outcome<List<Instant>> x = instants(a), y = instants(b);
        if (!(x instanceof Outcome.Unique<List<Instant>> xs)) return x.map(l -> Ordering.EQUAL);
        if (!(y instanceof Outcome.Unique<List<Instant>> ys)) return y.map(l -> Ordering.EQUAL);
        TreeSet<Ordering> orders = new TreeSet<>();
        for (Instant p : xs.value()) for (Instant q : ys.value()) orders.add(Ordering.of(p.compareTo(q)));
        if (orders.size() == 1) return Outcome.unique(orders.first());
        List<Diagnostic> d = new ArrayList<>(xs.diagnostics());
        d.addAll(ys.diagnostics());
        return new Outcome.Multiple<>(List.copyOf(orders), d);
    }

    /** Every instant a value can name: one for an instant or an offset date-time, one or two for a zoned value. */
    private Outcome<List<Instant>> instants(TemporalValue v) {
        return switch (v) {
            case Instant i -> Outcome.unique(List.of(i));
            case FixedOffsetDateTime f -> Outcome.unique(List.of(f.toInstant()));
            case ZonedIntent z -> switch (resolve(z)) {
                case Outcome.Unique<Instant> u -> Outcome.unique(List.of(u.value()));
                case Outcome.Multiple<Instant> m -> new Outcome.Unique<>(m.values(), m.diagnostics());
                case Outcome.None<Instant> n -> new Outcome.None<>(n.diagnostics());
                case Outcome.Unsupported<Instant> u -> new Outcome.Unsupported<>(u.diagnostics());
                case Outcome.Invalid<Instant> i -> new Outcome.Invalid<>(i.diagnostics());
            };
            default -> new Outcome.Unsupported<>(List.of(new Diagnostic("unsupported-operand", "error", "",
                    "a zoned value orders only against instants, offset date-times and other zoned values, not a "
                            + v.getClass().getSimpleName())));
        };
    }
}
