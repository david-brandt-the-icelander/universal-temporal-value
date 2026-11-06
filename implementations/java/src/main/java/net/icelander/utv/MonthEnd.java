package net.icelander.utv;

/** What month arithmetic does when the day doesn't exist in the resulting month (31 Jan + 1 month). */
public enum MonthEnd {
    /** The default: no answer (status {@code none}, diagnostic {@code month-end}). */
    REJECT("reject"),
    /** Clamp to the last day of the month. */
    CONSTRAIN("constrain");

    private final String wire;

    MonthEnd(String wire) { this.wire = wire; }

    /** The JSON spelling ({@code "reject"}, {@code "constrain"}). */
    public String wire() { return wire; }

    static MonthEnd fromWire(String w) {
        for (MonthEnd m : values()) if (m.wire.equals(w)) return m;
        return null;
    }
}
