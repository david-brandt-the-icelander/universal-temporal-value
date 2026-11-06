package net.icelander.utv;

/** How a relates to b, as from {@link TemporalContext#compare}. */
public enum Ordering {
    BEFORE("before"), EQUAL("equal"), AFTER("after");

    private final String wire;

    Ordering(String wire) { this.wire = wire; }

    /** The JSON spelling, also returned by {@code toString()}. */
    public String wire() { return wire; }

    @Override public String toString() { return wire; }

    static Ordering of(int compare) { return compare < 0 ? BEFORE : compare == 0 ? EQUAL : AFTER; }

    static Ordering fromWire(String w) {
        for (Ordering o : values()) if (o.wire.equals(w)) return o;
        throw new IllegalArgumentException("not an ordering: " + w);
    }
}
