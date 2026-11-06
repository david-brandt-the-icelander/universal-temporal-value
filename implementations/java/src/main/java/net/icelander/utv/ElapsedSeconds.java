package net.icelander.utv;

import com.fasterxml.jackson.databind.JsonNode;
import com.fasterxml.jackson.databind.node.ObjectNode;
import java.math.BigInteger;

/**
 * An exact number of SI seconds, as a stopwatch counts them. Across a leap second this differs
 * from {@link PosixSeconds}, so adding it to an instant needs a leap-second table, which this
 * library doesn't have yet. {@code toString()} is a display string such as {@code 86400 s (SI)}.
 */
public final class ElapsedSeconds implements TemporalValue {
    private final Rational seconds;

    private ElapsedSeconds(Rational seconds) { this.seconds = seconds; }

    public static ElapsedSeconds of(long seconds) { return new ElapsedSeconds(Rational.of(seconds)); }

    public static ElapsedSeconds of(BigInteger numerator, BigInteger denominator) {
        if (denominator.signum() == 0) throw new IllegalArgumentException("zero denominator");
        return new ElapsedSeconds(Rational.of(numerator, denominator));
    }

    /** Reads an {@code elapsed} amount. */
    public static Outcome<ElapsedSeconds> fromJson(JsonNode json) {
        return Typed.decode(json, j -> {
            Typed.requireKind(j, "amount");
            JsonNode v = j.get("value");
            if (!"elapsed".equals(v.path("basis").asText())) throw Typed.kindMismatch("expected an elapsed (SI seconds) amount");
            JsonNode s = v.get("seconds");
            if (s == null) throw Fail.invalid("malformed-value", "/value/seconds", "an elapsed amount needs seconds");
            BigInteger n = Instants.integer(s.get("numerator"), "/value/seconds/numerator");
            BigInteger d = Instants.integer(s.get("denominator"), "/value/seconds/denominator");
            if (d.signum() <= 0) throw Fail.invalid("malformed-value", "/value/seconds/denominator", "denominator must be positive");
            return new ElapsedSeconds(Rational.of(n, d));
        });
    }

    @Override public ObjectNode toJson() {
        ObjectNode o = Json.envelope("amount");
        o.putObject("value").put("basis", "elapsed").set("seconds", Json.ratio(seconds));
        return o;
    }

    public BigInteger numerator() { return seconds.num; }
    public BigInteger denominator() { return seconds.den; }

    public ElapsedSeconds negate() { return new ElapsedSeconds(seconds.negate()); }

    @Override public boolean equals(Object o) { return o instanceof ElapsedSeconds e && seconds.equals(e.seconds); }

    @Override public int hashCode() { return seconds.hashCode(); }

    /** A display string, not parseable: {@code 86400 s (SI)}. */
    @Override public String toString() { return Typed.seconds(seconds, "SI"); }
}
