package net.icelander.utv;

import com.fasterxml.jackson.databind.JsonNode;
import com.fasterxml.jackson.databind.node.ObjectNode;
import java.math.BigInteger;
import java.time.OffsetDateTime;

/**
 * A point on the POSIX timeline, {@code 2026-11-05T13:30:00Z}, held exactly (fractions of a second
 * are rationals, so a third of a second stays a third). Same meaning as {@code java.time.Instant}.
 *
 * <p>There is no {@code now()}: read the clock yourself and pass the reading in, so the dependency
 * is visible: {@code Instant.from(java.time.Instant.now())}.
 */
public final class Instant implements IntervalPoint<Instant> {
    private final Rational seconds;

    private Instant(Rational seconds) { this.seconds = seconds; }

    public static Instant fromUnixSeconds(long seconds) { return new Instant(Rational.of(seconds)); }

    /** Seconds since 1970-01-01T00:00:00Z plus nanoseconds (any value; it carries into seconds). */
    public static Instant fromUnixSeconds(long seconds, long nanoseconds) {
        return new Instant(Rational.of(seconds).add(Rational.of(BigInteger.valueOf(nanoseconds), Rational.BILLION)));
    }

    public static Instant fromUnixNanoseconds(long nanoseconds) { return fromUnixSeconds(0, nanoseconds); }

    /** Parses {@code 2026-11-05T13:30:00Z}. */
    public static Outcome<Instant> parse(String text) { return Typed.parse(text, Instant::fromJson); }

    /** Reads an {@code instant} on the unix-posix timescale. */
    public static Outcome<Instant> fromJson(JsonNode json) {
        return Typed.decode(json, j -> {
            Typed.requireKind(j, "instant");
            return new Instant(Instants.decode(j, "value"));
        });
    }

    static Instant read(JsonNode json) { return fromJson(json).single(); }

    /** From {@code java.time}: exact. */
    public static Instant from(java.time.Instant t) { return fromUnixSeconds(t.getEpochSecond(), t.getNano()); }

    /** From {@code java.time}, read as the instant it names (its offset is dropped). */
    public static Instant from(OffsetDateTime t) { return from(t.toInstant()); }

    /** To {@code java.time}; a fraction finer than a nanosecond is truncated and reported ({@code precision-loss}). */
    public Outcome<java.time.Instant> toJavaInstant() { return Typed.fromNative(JavaTime.toInstant(toJson())); }

    @Override public ObjectNode toJson() { return Json.instant(seconds); }

    /** Whole seconds since 1970-01-01T00:00:00Z, rounded down; {@link ArithmeticException} beyond a {@code long}. */
    public long unixSeconds() { return seconds.floor().longValueExact(); }

    /** The exact part of a second after {@link #unixSeconds()}, in [0, 1): a third of a second stays 1/3. */
    public PosixSeconds subSecond() { return PosixSeconds.of(seconds.fraction().num, seconds.fraction().den); }

    /** Nanoseconds within the second, the floor of {@link #subSecond()} (0..999,999,999). */
    public int nanoseconds() { return seconds.fraction().multiply(Rational.BILLION).floor().intValueExact(); }

    // ---------------------------------------------------------------- arithmetic

    /** Exact: {@code t + 1/3 s} keeps the third. */
    public Instant add(PosixSeconds amount) { return Typed.total(Operations.add(toJson(), amount.toJson()), Instant::read); }

    /** {@code unsupported} ({@code needs-leap-seconds}): SI seconds on a POSIX instant need a leap-second table. */
    public Outcome<Instant> add(ElapsedSeconds amount) { return Typed.outcome(Operations.add(toJson(), amount.toJson()), Instant::read); }

    /** The exact POSIX seconds from this instant to {@code other}. */
    public PosixSeconds until(Instant other) {
        return Typed.total(Operations.posixSecondsBetween(toJson(), other.toJson()), PosixSeconds::read);
    }

    /**
     * This instant as a wall time at a fixed offset (seconds, within ±18:00). Throws
     * {@link ArithmeticException} when the instant has a fraction finer than a nanosecond.
     */
    public FixedOffsetDateTime atOffset(int offsetSeconds) {
        if (Math.abs(offsetSeconds) > Civil.MAX_OFFSET) throw new IllegalArgumentException("offset beyond ±18:00: " + offsetSeconds);
        UtvResult r = Zones.toOffset(toJson(), offsetSeconds);
        if (!r.isUnique()) throw new ArithmeticException(Typed.outcome(r, j -> j).why());
        return FixedOffsetDateTime.read(r.candidates().get(0));
    }

    /** The wall time in a named zone, with the offset in force asserted; {@code unsupported} outside the rules. */
    public Outcome<ZonedIntent> inZone(String zoneId, TemporalContext ctx) { return ctx.inZone(this, zoneId); }

    // ---------------------------------------------------------------- comparison, equality, text

    @Override public int compareTo(Instant other) { return Typed.order(Operations.compare(toJson(), other.toJson())); }

    public boolean isBefore(Instant other) { return compareTo(other) < 0; }

    public boolean isAfter(Instant other) { return compareTo(other) > 0; }

    @Override public boolean equals(Object o) { return o instanceof Instant i && seconds.equals(i.seconds); }

    @Override public int hashCode() { return seconds.hashCode(); }

    /** The text form, {@code 2026-11-05T13:30:00.25Z}. */
    @Override public String toString() { return Typed.text(this); }
}
