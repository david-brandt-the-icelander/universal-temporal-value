package net.icelander.utv;

import com.fasterxml.jackson.databind.JsonNode;
import com.fasterxml.jackson.databind.node.ObjectNode;
import java.util.ArrayList;
import java.util.List;
import java.util.TreeSet;

/**
 * Turning a local time in a zone into instants ({@link #resolve}) and back ({@link #toZone},
 * {@link #toOffset}). Zone rules are always an explicit {@link RuleBundle}: the library never
 * reads the host's zone or tz database.
 */
public final class Zones {
    private static final Rational DAY = Rational.of(86400);

    private Zones() {}

    /** {@code resolve} with the value's own {@code resolutionPolicy}, or reject/reject. */
    public static UtvResult resolve(JsonNode dateTime, RuleBundle rules) {
        return Fail.run("resolve", () -> doResolve(dateTime, rules, null));
    }

    /** Resolves a civil date-time to instant(s) under the given policy. */
    public static UtvResult resolve(JsonNode dateTime, RuleBundle rules, ResolutionPolicy policy) {
        return Fail.run("resolve", () -> doResolve(dateTime, rules, policy));
    }

    /** {@code resolve} with a JSON policy {@code {"gap": ..., "overlap": ...}}; null uses the value's own policy. */
    public static UtvResult resolve(JsonNode dateTime, RuleBundle rules, JsonNode policy) {
        return Fail.run("resolve", () -> doResolve(dateTime, rules,
                policy == null || policy.isNull() ? null : ResolutionPolicy.fromJson(policy, "")));
    }

    private static UtvResult doResolve(JsonNode dateTime, RuleBundle rules, ResolutionPolicy given) {
        Civil.structural(dateTime, "dateTime");
        if (!Civil.isCivil(dateTime)) throw Fail.unsupported("unsupported-operand", "/kind", "resolve takes a civil date-time");
        Civil c = Civil.decode(dateTime, "dateTime");
        // 1
        if (!c.form().equals("datetime")) throw Fail.unsupported("unsupported-operand", "/value/form", "a " + c.form() + " is not one instant");
        if (c.secondOr0() == 60) throw Fail.unsupported("leap-second", "/value/fields/second", "second 60 needs a leap-second table");
        Rational local = c.wallSeconds();
        // 2
        if (c.zone().floating()) {
            return UtvResult.of("resolve", Status.SYMBOLIC, new Diagnostic("no-zone", "info", "/value/zone",
                    "no zone is bound, so there is nothing to resolve yet"));
        }
        // 3
        if (c.zone().fixed()) return UtvResult.unique("resolve", Json.instant(local.subtract(Rational.of(c.zone().seconds()))));
        // 4
        if (rules == null) throw Fail.unsupported("needs-zone-rules", "", "no zone rules were given");
        RuleBundle.ZoneData zone = rules.zone(c.zone().id());
        if (zone == null) throw Fail.unsupported("unknown-zone", "/value/zone/id", "zone " + c.zone().id() + " is not in the rule bundle");
        // 5
        if (local.subtract(DAY).compareTo(Rational.of(rules.validFrom)) < 0 || local.add(DAY).compareTo(Rational.of(rules.validUntil)) >= 0) {
            throw Fail.unsupported("outside-rule-window", "/value/fields", "the local time is outside the rule bundle's validity window");
        }
        ResolutionPolicy policy = given;
        if (policy == null) {
            JsonNode own = c.source().get("resolutionPolicy");
            policy = own != null && own.isObject() ? ResolutionPolicy.fromJson(own, "/value/resolutionPolicy") : ResolutionPolicy.STRICT;
        }

        // offsets in force within a day of L
        Rational lo = local.subtract(DAY), hi = local.add(DAY);
        TreeSet<Integer> offsets = new TreeSet<>();
        offsets.add(zone.offsetAt(lo));
        int before = zone.initialOffset();
        Integer gapBefore = null;
        for (RuleBundle.Transition tr : zone.transitions()) {
            Rational at = Rational.of(tr.at());
            if (at.compareTo(lo) > 0 && at.compareTo(hi) <= 0) {
                offsets.add(tr.offset());
                // a gap: wall times [at + before, at + after) never happen
                if (tr.offset() > before && at.add(Rational.of(before)).compareTo(local) <= 0
                        && local.compareTo(at.add(Rational.of(tr.offset()))) < 0) {
                    gapBefore = before;
                }
            }
            if (at.compareTo(hi) <= 0) before = tr.offset();
        }
        TreeSet<Rational> found = new TreeSet<>();
        for (int o : offsets) {
            Rational t = local.subtract(Rational.of(o));
            if (zone.offsetAt(t) == o && (c.offsetAssertion() == null || c.offsetAssertion() == o)) found.add(t);
        }
        if (c.offsetAssertion() != null && found.isEmpty()) {
            throw new Fail(Status.INVALID, "offset-mismatch", "/value/offsetAssertion", "the asserted offset "
                    + Iso.offset(c.offsetAssertion()) + " is not in force in " + c.zone().id() + " at this local time under these rules");
        }
        List<JsonNode> instants = new ArrayList<>();
        for (Rational t : found) instants.add(Json.instant(t));

        if (instants.size() == 1) return UtvResult.unique("resolve", instants.get(0));
        if (instants.isEmpty()) {
            if (policy.gap() == Gap.SHIFT_FORWARD && gapBefore != null) {
                return UtvResult.unique("resolve", Json.instant(local.subtract(Rational.of(gapBefore))));
            }
            return UtvResult.of("resolve", Status.NONE, new Diagnostic("gap", "warning", "/value/fields",
                    "this wall time is skipped in " + c.zone().id()));
        }
        return switch (policy.overlap()) {
            case EARLIEST -> UtvResult.unique("resolve", instants.get(0));
            case LATEST -> UtvResult.unique("resolve", instants.get(instants.size() - 1));
            case ALL -> new UtvResult("resolve", Status.MULTIPLE, List.of(), instants);
            case REJECT -> new UtvResult("resolve", Status.MULTIPLE, List.of(new Diagnostic("ambiguous", "warning", "/value/fields",
                    "this wall time occurs " + (instants.size() == 2 ? "twice" : instants.size() + " times") + " in " + c.zone().id())), instants);
        };
    }

    /** The instant as a date-time in the named zone, with the offset in force as {@code offsetAssertion}. */
    public static UtvResult toZone(JsonNode instant, String zoneId, RuleBundle rules) {
        return Fail.run("toZone", () -> {
            Rational t = Instants.decode(instant, "instant");
            if (rules == null) throw Fail.unsupported("needs-zone-rules", "", "no zone rules were given");
            RuleBundle.ZoneData zone = rules.zone(zoneId);
            if (zone == null) throw Fail.unsupported("unknown-zone", "", "zone " + zoneId + " is not in the rule bundle");
            if (!rules.covers(t)) throw Fail.unsupported("outside-rule-window", "/value", "the instant is outside the rule bundle's validity window");
            int offset = zone.offsetAt(t);
            ObjectNode dt = Values.inZone(Civil.datetimeAt(t.add(Rational.of(offset))), zoneId);
            ((ObjectNode) dt.get("value")).put("offsetAssertion", offset);
            return UtvResult.unique("toZone", dt);
        });
    }

    /** The instant as a date-time at a fixed offset (seconds east of UTC, within +-18:00). */
    public static UtvResult toOffset(JsonNode instant, int offsetSeconds) {
        return Fail.run("toOffset", () -> {
            Rational t = Instants.decode(instant, "instant");
            if (Math.abs(offsetSeconds) > Civil.MAX_OFFSET) throw Fail.invalid("offset-out-of-range", "", "offset must be within +-18:00");
            return UtvResult.unique("toOffset", Values.withOffset(Civil.datetimeAt(t.add(Rational.of(offsetSeconds))), offsetSeconds));
        });
    }

}
