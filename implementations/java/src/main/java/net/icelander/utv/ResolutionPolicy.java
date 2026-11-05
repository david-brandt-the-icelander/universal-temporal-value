package net.icelander.utv;

import com.fasterxml.jackson.databind.JsonNode;

/**
 * How {@link Zones#resolve} treats a local time that a zone skips (gap) or repeats (overlap).
 * The default, {@link #REJECT}, refuses to choose: a gap gives {@code none}, an overlap gives
 * {@code multiple} with both instants.
 */
public record ResolutionPolicy(Gap gap, Overlap overlap) {
    public static final ResolutionPolicy REJECT = new ResolutionPolicy(Gap.REJECT, Overlap.REJECT);

    public enum Gap {
        REJECT("reject"), PRESERVE_UNRESOLVED("preserve-unresolved"), SHIFT_FORWARD_BY_GAP("shift-forward-by-gap");
        private final String wire;
        Gap(String wire) { this.wire = wire; }
        public String wire() { return wire; }
    }

    public enum Overlap {
        REJECT("reject"), ALL("all"), EARLIEST("earliest"), LATEST("latest");
        private final String wire;
        Overlap(String wire) { this.wire = wire; }
        public String wire() { return wire; }
    }

    public ResolutionPolicy {
        if (gap == null) gap = Gap.REJECT;
        if (overlap == null) overlap = Overlap.REJECT;
    }

    public ResolutionPolicy withGap(Gap g) { return new ResolutionPolicy(g, overlap); }
    public ResolutionPolicy withOverlap(Overlap o) { return new ResolutionPolicy(gap, o); }

    /** Reads {@code {"gap": ..., "overlap": ...}}; a missing key means reject; an unknown value is unsupported. */
    static ResolutionPolicy fromJson(JsonNode n, String path) {
        Gap g = Gap.REJECT;
        Overlap o = Overlap.REJECT;
        if (n.has("gap")) {
            String w = n.get("gap").asText("");
            g = null;
            for (Gap x : Gap.values()) if (x.wire.equals(w)) g = x;
            if (g == null) throw Fail.unsupported("utv.policy", path + "/gap", "unknown gap policy " + w);
        }
        if (n.has("overlap")) {
            String w = n.get("overlap").asText("");
            o = null;
            for (Overlap x : Overlap.values()) if (x.wire.equals(w)) o = x;
            if (o == null) throw Fail.unsupported("utv.policy", path + "/overlap", "unknown overlap policy " + w);
        }
        return new ResolutionPolicy(g, o);
    }
}
