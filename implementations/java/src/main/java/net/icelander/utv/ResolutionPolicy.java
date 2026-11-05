package net.icelander.utv;

import com.fasterxml.jackson.databind.JsonNode;

/**
 * How resolving treats a wall time that a zone skips ({@link Gap}) or repeats ({@link Overlap}).
 * The default, {@link #STRICT}, refuses to choose: a gap gives {@code none}, an overlap gives
 * {@code multiple} with both instants.
 */
public record ResolutionPolicy(Gap gap, Overlap overlap) {
    /** Reject gaps and overlaps (the default). */
    public static final ResolutionPolicy STRICT = new ResolutionPolicy(Gap.REJECT, Overlap.REJECT);
    /** Reject gaps; give every instant of an overlap without a diagnostic. */
    public static final ResolutionPolicy ALL_CANDIDATES = new ResolutionPolicy(Gap.REJECT, Overlap.ALL);
    /** @deprecated since 0.3: use {@link #STRICT}. */
    @Deprecated(since = "0.3")
    public static final ResolutionPolicy REJECT = STRICT;

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
            for (Gap x : Gap.values()) if (x.wire().equals(w)) g = x;
            if (g == null) throw Fail.unsupported("unsupported-policy", path + "/gap", "unknown gap policy " + w);
        }
        if (n.has("overlap")) {
            String w = n.get("overlap").asText("");
            o = null;
            for (Overlap x : Overlap.values()) if (x.wire().equals(w)) o = x;
            if (o == null) throw Fail.unsupported("unsupported-policy", path + "/overlap", "unknown overlap policy " + w);
        }
        return new ResolutionPolicy(g, o);
    }
}
