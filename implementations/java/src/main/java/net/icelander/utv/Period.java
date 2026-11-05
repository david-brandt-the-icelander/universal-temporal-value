package net.icelander.utv;

import com.fasterxml.jackson.databind.JsonNode;
import com.fasterxml.jackson.databind.node.ObjectNode;
import java.math.BigInteger;
import java.util.Arrays;
import java.util.Map;

/**
 * A calendar amount, {@code P1M} or {@code P1Y2M10DT2H30M}: months, days and wall-clock hours,
 * not a number of seconds. One day is "the same wall time tomorrow", which across a DST change is
 * 23 or 25 hours of elapsed time; for seconds on the timeline use {@link PosixSeconds}.
 * Components may be negative.
 */
public final class Period implements TemporalValue {
    /** The empty period, {@code P0D}. */
    public static final Period ZERO = new Period(new long[7]);

    private final long[] c; // years, months, weeks, days, hours, minutes, seconds

    private Period(long[] components) { this.c = components; }

    public static Period of(long years, long months, long weeks, long days, long hours, long minutes, long seconds) {
        return new Period(new long[] {years, months, weeks, days, hours, minutes, seconds});
    }

    public static Period years(long n) { return of(n, 0, 0, 0, 0, 0, 0); }
    public static Period months(long n) { return of(0, n, 0, 0, 0, 0, 0); }
    public static Period weeks(long n) { return of(0, 0, n, 0, 0, 0, 0); }
    public static Period days(long n) { return of(0, 0, 0, n, 0, 0, 0); }
    public static Period hours(long n) { return of(0, 0, 0, 0, n, 0, 0); }
    public static Period minutes(long n) { return of(0, 0, 0, 0, 0, n, 0); }
    public static Period seconds(long n) { return of(0, 0, 0, 0, 0, 0, n); }

    /** Parses {@code P1M}, {@code PT1M}, {@code -P1DT2H}. */
    public static Outcome<Period> parse(String text) { return Typed.parse(text, Period::fromJson); }

    /** Reads a {@code calendar} amount; components beyond 64 bits are {@code unsupported} ({@code implementation-limit}). */
    public static Outcome<Period> fromJson(JsonNode json) {
        return Typed.decode(json, j -> {
            Typed.requireKind(j, "amount");
            if (!"calendar".equals(j.get("value").path("basis").asText())) throw Typed.kindMismatch("expected a calendar amount");
            Map<String, BigInteger> k = Operations.components(j.get("value").get("components"));
            long[] c = new long[7];
            int i = 0;
            for (BigInteger n : k.values()) c[i++] = Typed.exactLong(n, "a period component");
            return new Period(c);
        });
    }

    /** From {@code java.time}: years, months and days. */
    public static Period from(java.time.Period p) { return of(p.getYears(), p.getMonths(), 0, p.getDays(), 0, 0, 0); }

    /** To {@code java.time}: {@code unsupported} with hours, minutes or seconds (weeks become 7 days). */
    public Outcome<java.time.Period> toJavaPeriod() { return Typed.fromNative(JavaTime.toPeriod(toJson())); }

    @Override public ObjectNode toJson() { return Values.period(c[0], c[1], c[2], c[3], c[4], c[5], c[6]); }

    public long years() { return c[0]; }
    public long months() { return c[1]; }
    public long weeks() { return c[2]; }
    public long days() { return c[3]; }
    public long hours() { return c[4]; }
    public long minutes() { return c[5]; }
    public long seconds() { return c[6]; }

    /** Every component negated. */
    public Period negate() {
        long[] n = new long[7];
        for (int i = 0; i < 7; i++) n[i] = Math.negateExact(c[i]);
        return new Period(n);
    }

    @Override public boolean equals(Object o) { return o instanceof Period p && Arrays.equals(c, p.c); }

    @Override public int hashCode() { return Arrays.hashCode(c); }

    /** The text form, {@code P1M}; a period with mixed signs has none and prints as JSON. */
    @Override public String toString() { return Typed.text(this); }
}
