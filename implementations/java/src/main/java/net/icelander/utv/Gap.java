package net.icelander.utv;

/** What resolving does with a wall time that a zone skips (spring forward). */
public enum Gap {
    /** The default: no answer (status {@code none}, diagnostic {@code gap}). */
    REJECT("reject"),
    /** Also no answer; the value stays an unresolved intention. */
    PRESERVE_UNRESOLVED("preserve-unresolved"),
    /** Move forward by the length of the gap: 02:30 in a one-hour gap becomes 03:30. */
    SHIFT_FORWARD("shift-forward-by-gap");

    private final String wire;

    Gap(String wire) { this.wire = wire; }

    /** The JSON spelling. */
    public String wire() { return wire; }
}
