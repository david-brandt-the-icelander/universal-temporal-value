package net.icelander.utv;

/** What resolving does with a wall time that happens more than once (fall back). */
public enum Overlap {
    /** The default: every answer (status {@code multiple}, diagnostic {@code ambiguous}). */
    REJECT("reject"),
    /** Every answer, without the {@code ambiguous} diagnostic. */
    ALL("all"),
    /** The first instant. */
    EARLIEST("earliest"),
    /** The last instant. */
    LATEST("latest");

    private final String wire;

    Overlap(String wire) { this.wire = wire; }

    /** The JSON spelling. */
    public String wire() { return wire; }
}
