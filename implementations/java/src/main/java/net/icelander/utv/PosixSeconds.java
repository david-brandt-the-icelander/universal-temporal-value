package net.icelander.utv;

import com.fasterxml.jackson.databind.JsonNode;
import com.fasterxml.jackson.databind.node.ObjectNode;
import java.math.BigInteger;
import java.time.Duration;

/**
 * An exact number of POSIX seconds, the way {@link Instant}s count: 86,400 per day, with no leap
 * seconds. Fractions are exact rationals ({@code 1/3 s}). There is no text form; {@code toString()}
 * is a display string such as {@code 3600.75 s (POSIX)}.
 */
public final class PosixSeconds implements TemporalValue {
    private final Rational seconds;

    private PosixSeconds(Rational seconds) { this.seconds = seconds; }

    public static PosixSeconds of(long seconds) { return new PosixSeconds(Rational.of(seconds)); }

    /** {@code numerator / denominator} seconds, reduced; throws {@link IllegalArgumentException} for a zero denominator. */
    public static PosixSeconds of(long numerator, long denominator) { return of(BigInteger.valueOf(numerator), BigInteger.valueOf(denominator)); }

    public static PosixSeconds of(BigInteger numerator, BigInteger denominator) {
        if (denominator.signum() == 0) throw new IllegalArgumentException("zero denominator");
        return new PosixSeconds(Rational.of(numerator, denominator));
    }

    /** Reads a {@code clock-ticks} amount on the unix-posix timescale. */
    public static Outcome<PosixSeconds> fromJson(JsonNode json) {
        return Typed.decode(json, j -> {
            Typed.requireKind(j, "amount");
            if (!"clock-ticks".equals(j.get("value").path("basis").asText())) throw Typed.kindMismatch("expected a POSIX seconds (clock-ticks) amount");
            return new PosixSeconds(Instants.posixAmount(j, "amount"));
        });
    }

    static PosixSeconds read(JsonNode json) { return fromJson(json).single(); }

    /** From {@code java.time}: a {@code Duration} counts POSIX seconds too. Exact. */
    public static PosixSeconds from(Duration d) { return read(JavaTime.fromDuration(d)); }

    /** To {@code java.time}; a fraction finer than a nanosecond is truncated and reported ({@code precision-loss}). */
    public Outcome<Duration> toDuration() { return Typed.fromNative(JavaTime.toDuration(toJson())); }

    @Override public ObjectNode toJson() { return Json.posixSeconds(seconds); }

    public BigInteger numerator() { return seconds.num; }
    public BigInteger denominator() { return seconds.den; }

    public PosixSeconds negate() { return new PosixSeconds(seconds.negate()); }

    @Override public boolean equals(Object o) { return o instanceof PosixSeconds p && seconds.equals(p.seconds); }

    @Override public int hashCode() { return seconds.hashCode(); }

    /** A display string, not parseable: {@code 3600.75 s (POSIX)}, {@code 1/3 s (POSIX)}. */
    @Override public String toString() { return Typed.seconds(seconds, "POSIX"); }
}
