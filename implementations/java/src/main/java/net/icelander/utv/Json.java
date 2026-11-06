package net.icelander.utv;

import com.fasterxml.jackson.databind.JsonNode;
import com.fasterxml.jackson.databind.node.JsonNodeFactory;
import com.fasterxml.jackson.databind.node.ObjectNode;
import java.math.BigInteger;

/** Shared JSON shapes for the everyday API. */
final class Json {
    static final JsonNodeFactory F = JsonNodeFactory.instance;

    private Json() {}

    static ObjectNode envelope(String kind) {
        return F.objectNode().put("schema", Utv.SPEC_VERSION).put("kind", kind);
    }

    /** {"id":"gregory","chronology":"proleptic"} */
    static ObjectNode calendar() {
        return F.objectNode().put("id", "gregory").put("chronology", "proleptic");
    }

    /** {"system":"utv.timescale","id":"unix-posix","version":"1"} */
    static ObjectNode posix() {
        return F.objectNode().put("system", "utv.timescale").put("id", "unix-posix").put("version", "1");
    }

    static boolean isPosix(JsonNode system) {
        return system != null && system.isObject() && system.size() == 3
                && "utv.timescale".equals(system.path("system").asText(null))
                && "unix-posix".equals(system.path("id").asText(null))
                && "1".equals(system.path("version").asText(null));
    }

    static ObjectNode ratio(Rational r) {
        return F.objectNode().put("numerator", r.num.toString()).put("denominator", r.den.toString());
    }

    /** The smallest JSON integer node that holds n, so results compare equal to parsed JSON. */
    static JsonNode integer(BigInteger n) {
        if (n.bitLength() < 32) return F.numberNode(n.intValue());
        if (n.bitLength() < 64) return F.numberNode(n.longValue());
        return F.numberNode(n);
    }

    /** An instant on the POSIX timescale, with subTick only when the fraction is non-zero. */
    static ObjectNode instant(Rational t) {
        ObjectNode o = envelope("instant");
        ObjectNode v = o.putObject("value");
        v.set("system", posix());
        v.put("ticks", t.floor().toString());
        Rational frac = t.fraction();
        if (!frac.isZero()) v.set("subTick", ratio(frac));
        return o;
    }

    /** A posixSeconds amount holding the exact rational r. */
    static ObjectNode posixSeconds(Rational r) {
        ObjectNode o = envelope("amount");
        ObjectNode v = o.putObject("value").put("basis", "clock-ticks");
        v.set("rules", posix());
        v.set("seconds", ratio(r));
        return o;
    }
}
