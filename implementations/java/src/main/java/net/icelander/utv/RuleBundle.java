package net.icelander.utv;

import com.fasterxml.jackson.databind.JsonNode;
import java.io.IOException;
import java.math.BigInteger;
import java.util.ArrayList;
import java.util.Collections;
import java.util.Iterator;
import java.util.LinkedHashMap;
import java.util.List;
import java.util.Map;
import java.util.Set;

/**
 * An immutable zone-rule bundle in the format of {@code conformance/rules/demo-zones.json}:
 * per-zone transitions, valid only from {@code validFrom} (inclusive) to {@code validUntil}
 * (exclusive), in POSIX seconds. Outside that window, or for a zone the bundle lacks, the zone
 * operations answer {@code unsupported}; they never extrapolate.
 */
public final class RuleBundle {
    record Transition(BigInteger at, int offset) {}

    record ZoneData(int initialOffset, List<Transition> transitions) {
        int offsetAt(Rational t) {
            int o = initialOffset;
            for (Transition tr : transitions) {
                if (Rational.of(tr.at).compareTo(t) <= 0) o = tr.offset; else break;
            }
            return o;
        }
    }

    private final JsonNode ruleSet;
    final BigInteger validFrom, validUntil;
    private final Map<String, ZoneData> zones;

    private RuleBundle(JsonNode ruleSet, BigInteger validFrom, BigInteger validUntil, Map<String, ZoneData> zones) {
        this.ruleSet = ruleSet;
        this.validFrom = validFrom;
        this.validUntil = validUntil;
        this.zones = zones;
    }

    /** Parses bundle JSON text. Throws {@link IllegalArgumentException} if it is not a well-formed bundle. */
    public static RuleBundle parse(String json) {
        try {
            return fromJson(Utv.parse(json));
        } catch (IOException e) {
            throw new IllegalArgumentException("rule bundle is not JSON: " + e.getMessage(), e);
        }
    }

    /** Reads an already-parsed bundle. Throws {@link IllegalArgumentException} if it is malformed. */
    public static RuleBundle fromJson(JsonNode n) {
        try {
            Map<String, ZoneData> zones = new LinkedHashMap<>();
            for (Iterator<Map.Entry<String, JsonNode>> it = n.get("zones").fields(); it.hasNext(); ) {
                Map.Entry<String, JsonNode> e = it.next();
                List<Transition> ts = new ArrayList<>();
                for (JsonNode t : e.getValue().get("transitions")) {
                    ts.add(new Transition(new BigInteger(t.get("at").textValue()), offset(t.get("offset"))));
                }
                ts.sort((a, b) -> a.at.compareTo(b.at));
                zones.put(e.getKey(), new ZoneData(offset(e.getValue().get("initialOffset")), List.copyOf(ts)));
            }
            return new RuleBundle(n.get("ruleSet"), new BigInteger(n.get("validFrom").textValue()),
                    new BigInteger(n.get("validUntil").textValue()), Collections.unmodifiableMap(zones));
        } catch (RuntimeException e) {
            throw new IllegalArgumentException("malformed rule bundle: " + e, e);
        }
    }

    private static int offset(JsonNode n) {
        if (n == null || !n.isInt()) throw new IllegalArgumentException("offset must be an integer");
        return n.intValue();
    }

    /** The bundle's {@code ruleSet} identity ({@code {system, id, version}}), or null. */
    public JsonNode ruleSet() { return ruleSet; }

    /** The zone ids this bundle has rules for. */
    public Set<String> zoneIds() { return zones.keySet(); }

    ZoneData zone(String id) { return zones.get(id); }

    boolean covers(Rational t) {
        return Rational.of(validFrom).compareTo(t) <= 0 && t.compareTo(Rational.of(validUntil)) < 0;
    }
}
