package net.icelander.utv;

/** A three-valued answer, as from {@link Interval#contains}: unknown endpoints make some answers {@link #POSSIBLY}. */
public enum Truth {
    DEFINITELY("definitely"), POSSIBLY("possibly"), IMPOSSIBLE("impossible");

    private final String wire;

    Truth(String wire) { this.wire = wire; }

    /** The JSON spelling, also returned by {@code toString()}. */
    public String wire() { return wire; }

    @Override public String toString() { return wire; }

    static Truth fromWire(String w) {
        for (Truth t : values()) if (t.wire.equals(w)) return t;
        throw new IllegalArgumentException("not a truth value: " + w);
    }
}
