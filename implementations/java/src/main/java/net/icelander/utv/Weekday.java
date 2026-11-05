package net.icelander.utv;

/** An ISO day of the week, Monday first. {@code toString()} gives the English name, {@code "Thursday"}. */
public enum Weekday {
    MONDAY, TUESDAY, WEDNESDAY, THURSDAY, FRIDAY, SATURDAY, SUNDAY;

    /** The ISO number: Monday = 1 … Sunday = 7. */
    public int number() { return ordinal() + 1; }

    /** The day with ISO number 1 (Monday) to 7 (Sunday). */
    public static Weekday of(int isoNumber) {
        if (isoNumber < 1 || isoNumber > 7) throw new IllegalArgumentException("ISO weekday numbers are 1..7: " + isoNumber);
        return values()[isoNumber - 1];
    }

    @Override public String toString() { return name().charAt(0) + name().substring(1).toLowerCase(java.util.Locale.ROOT); }
}
