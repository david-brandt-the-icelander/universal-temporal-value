package net.icelander.utv;

import com.fasterxml.jackson.databind.JsonNode;
import java.math.BigInteger;
import java.util.regex.Pattern;

/** Decoding of POSIX instants into exact rational seconds since 1970-01-01T00:00:00Z. */
final class Instants {
    private static final Pattern INTEGER = Pattern.compile("-?(0|[1-9][0-9]*)");

    private Instants() {}

    static boolean isInstant(JsonNode v) {
        return v != null && "instant".equals(v.path("kind").asText(null));
    }

    static Rational decode(JsonNode node, String what) {
        Civil.structural(node, what);
        if (!isInstant(node)) throw Fail.unsupported("utv.kind", "/kind", what + " must be an instant");
        JsonNode v = node.get("value");
        if (!Json.isPosix(v.get("system"))) {
            throw Fail.unsupported("utv.timescale", "/value/system", "only the unix-posix timescale is implemented");
        }
        if (v.has("coordinate") || v.has("quality")) {
            throw Fail.unsupported("utv.instant.quality", "/value", "instants with coordinate or quality are not handled");
        }
        BigInteger ticks = integer(v.get("ticks"), "/value/ticks");
        Rational t = Rational.of(ticks);
        JsonNode sub = v.get("subTick");
        if (sub != null) {
            BigInteger n = integer(sub.get("numerator"), "/value/subTick/numerator");
            BigInteger d = integer(sub.get("denominator"), "/value/subTick/denominator");
            if (d.signum() <= 0 || n.signum() < 0 || n.compareTo(d) >= 0) {
                throw Fail.invalid("utv.instant.subtick", "/value/subTick", "subTick must be in [0, 1) with a positive denominator");
            }
            t = t.add(Rational.of(n, d));
        }
        return t;
    }

    static BigInteger integer(JsonNode n, String path) {
        if (n == null || !n.isTextual() || !INTEGER.matcher(n.textValue()).matches()) {
            throw Fail.invalid("utv.integer", path, "must be an integer decimal string");
        }
        return new BigInteger(n.textValue());
    }

    /** Exact seconds of a {@code posixSeconds} amount ({@code clock-ticks} on unix-posix). */
    static Rational posixAmount(JsonNode amount, String what) {
        Civil.structural(amount, what);
        if (!"amount".equals(amount.path("kind").asText(null))) throw Fail.unsupported("utv.kind", "/kind", what + " must be an amount");
        JsonNode v = amount.get("value");
        String basis = v.path("basis").asText("");
        if (!basis.equals("clock-ticks")) {
            throw Fail.unsupported("utv.amount.basis", "/value/basis", what + " must be a posixSeconds (clock-ticks) amount");
        }
        if (!Json.isPosix(v.get("rules"))) throw Fail.unsupported("utv.timescale", "/value/rules", "only unix-posix clock ticks are implemented");
        JsonNode s = v.get("seconds");
        if (s == null) throw Fail.invalid("utv.amount.seconds", "/value/seconds", "clock-ticks amount needs seconds");
        BigInteger n = integer(s.get("numerator"), "/value/seconds/numerator");
        BigInteger d = integer(s.get("denominator"), "/value/seconds/denominator");
        if (d.signum() <= 0) throw Fail.invalid("utv.amount.seconds", "/value/seconds/denominator", "denominator must be positive");
        return Rational.of(n, d);
    }
}
