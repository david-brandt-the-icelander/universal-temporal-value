package net.icelander.utv;

import com.fasterxml.jackson.databind.JsonNode;
import com.fasterxml.jackson.databind.node.ObjectNode;
import java.io.IOException;
import java.math.BigDecimal;
import java.math.BigInteger;
import java.util.List;
import java.util.function.Function;

/** The bridge from the typed layer to the JSON layer. Every typed operation goes through here. */
final class Typed {
    private Typed() {}

    /** A JSON-layer result as an outcome, mapping each candidate. */
    static <T> Outcome<T> outcome(UtvResult r, Function<JsonNode, T> map) {
        List<Diagnostic> d = r.diagnostics();
        return switch (r.status()) {
            case UNIQUE -> new Outcome.Unique<>(map.apply(r.candidates().get(0)), d);
            case MULTIPLE -> new Outcome.Multiple<>(r.candidates().stream().map(map).toList(), d);
            case NONE -> new Outcome.None<>(d);
            case UNSUPPORTED -> new Outcome.Unsupported<>(d);
            case INVALID -> new Outcome.Invalid<>(d);
            default -> throw new IllegalStateException("the JSON layer answered " + r.toJson());
        };
    }

    /** The value of a total operation; anything but unique is a bug in this library. */
    static <T> T total(UtvResult r, Function<JsonNode, T> map) {
        if (!r.isUnique()) throw new IllegalStateException("a total operation answered " + r.toJson());
        return map.apply(r.candidates().get(0));
    }

    /** The value of a constructor's outcome, or {@link IllegalArgumentException}. */
    static <T> T argument(Outcome<T> o) {
        if (o instanceof Outcome.Unique<T> u) return u.value();
        throw new IllegalArgumentException(o.why());
    }

    static <T> Outcome<T> failure(Fail f) {
        List<Diagnostic> d = f.diagnostics;
        return switch (f.status) {
            case INVALID -> new Outcome.Invalid<>(d);
            case NONE -> new Outcome.None<>(d);
            default -> new Outcome.Unsupported<>(d);
        };
    }

    static Fail kindMismatch(String message) { return Fail.invalid("kind-mismatch", "", message); }

    /**
     * Decodes JSON with {@code build}, which throws {@link Fail} for bad input. The value must be
     * canonical: if its own {@code toJson()} differs, the input carried something the typed value
     * can't hold, and the answer is {@code unsupported} rather than a silent loss.
     */
    static <T extends TemporalValue> Outcome<T> decode(JsonNode json, Function<JsonNode, T> build) {
        T v;
        try {
            Civil.structural(json, "value");
            v = build.apply(json);
        } catch (Fail f) {
            return failure(f);
        }
        if (!canonical(json).equals(canonical(v.toJson()))) {
            return new Outcome.Unsupported<>(List.of(new Diagnostic("unsupported-operand", "error", "",
                    "the value carries properties a " + v.getClass().getSimpleName() + " doesn't hold; use the JSON layer")));
        }
        return Outcome.unique(v);
    }

    /** JSON re-read through text (so number node classes agree), with a floating zone written as none. */
    private static JsonNode canonical(JsonNode n) {
        try {
            JsonNode c = Utv.parse(n.toString());
            JsonNode v = c.get("value");
            if (v instanceof ObjectNode o && "floating".equals(o.path("zone").path("type").asText(null))) {
                o.remove("zone");
                if ("floating".equals(o.path("zoneIntent").asText(null))) o.remove("zoneIntent");
            }
            return c;
        } catch (IOException e) {
            throw new IllegalStateException(e);
        }
    }

    /** A decoded civil value of the given form and zone type ({@code floating}, {@code fixed-offset}, {@code iana}). */
    static Civil civil(JsonNode j, String form, String zoneType) {
        if (!Civil.isCivil(j)) throw kindMismatch("expected a civil " + form + ", got a " + j.path("kind").asText());
        Civil c = Civil.decode(j, "value");
        boolean anotherTypeHoldsIt = c.form().equals("datetime") || c.zone().floating();
        if (!c.form().equals(form) || !c.zone().type().equals(zoneType)) {
            String got = describe(c.form(), c.zone().type());
            if (!anotherTypeHoldsIt) {
                throw Fail.unsupported("unsupported-operand", "/value/zone", "no typed value holds a " + got + "; use the JSON layer");
            }
            throw kindMismatch("expected a " + describe(form, zoneType) + ", got a " + got);
        }
        if (c.second() != null && c.second() == 60) throw Fail.unsupported("leap-second", "/value/fields/second", "second 60 needs a leap-second table");
        return c;
    }

    private static String describe(String form, String zoneType) {
        return zoneType.equals("floating") ? form : form + " with a " + zoneType + " zone";
    }

    static void requireKind(JsonNode j, String kind) {
        if (!kind.equals(j.path("kind").asText(null))) throw kindMismatch("expected a " + kind + ", got a " + j.path("kind").asText());
    }

    static long exactLong(BigInteger n, String what) {
        if (n.bitLength() > 63) throw Fail.unsupported("implementation-limit", "", what + " is beyond this library's 64-bit range");
        return n.longValue();
    }

    /** Parses text, then reads the JSON as the requested type. */
    static <T> Outcome<T> parse(String text, Function<JsonNode, Outcome<T>> fromJson) {
        UtvResult r = TextForm.parseText(text);
        if (!r.isUnique()) return outcome(r, j -> null);
        return fromJson.apply(r.candidates().get(0));
    }

    /** The text form, or the JSON when the value has none. */
    static String text(TemporalValue v) {
        ObjectNode json = v.toJson();
        UtvResult r = TextForm.formatText(json);
        return r.isUnique() ? r.candidates().get(0).textValue() : json.toString();
    }

    /** Seconds for display: {@code 3600.75 s (POSIX)}, or {@code 1/3 s (POSIX)} when the decimal doesn't end. */
    static String seconds(Rational r, String basis) {
        String n;
        try {
            n = new BigDecimal(r.num).divide(new BigDecimal(r.den)).stripTrailingZeros().toPlainString();
        } catch (ArithmeticException nonTerminating) {
            n = r.num + "/" + r.den;
        }
        return n + " s (" + basis + ")";
    }

    static int order(UtvResult compare) {
        return switch (total(compare, JsonNode::textValue)) {
            case "before" -> -1;
            case "equal" -> 0;
            default -> 1;
        };
    }

    static JsonNode toJsonCandidate(Object v) {
        if (v instanceof TemporalValue t) return t.toJson();
        if (v instanceof Number n) return Json.integer(new BigInteger(n.toString()));
        return Json.F.textNode(String.valueOf(v));
    }

    static <T> Outcome<T> fromNative(NativeResult<T> r) {
        return switch (r.status()) {
            case UNIQUE -> new Outcome.Unique<>(r.value(), r.diagnostics());
            case INVALID -> new Outcome.Invalid<>(r.diagnostics());
            default -> new Outcome.Unsupported<>(r.diagnostics());
        };
    }

    // ======================================================================== any value

    static Outcome<TemporalValue> anyFromJson(JsonNode j) {
        try {
            Civil.structural(j, "value");
        } catch (Fail f) {
            return failure(f);
        }
        JsonNode v = j.get("value");
        Outcome<? extends TemporalValue> o = switch (j.get("kind").textValue()) {
            case "civil" -> switch (v.path("form").asText("")) {
                case "date" -> CivilDate.fromJson(j);
                case "year-month" -> CivilYearMonth.fromJson(j);
                case "time" -> CivilTime.fromJson(j);
                case "datetime" -> switch (v.path("zone").path("type").asText("floating")) {
                    case "fixed-offset" -> FixedOffsetDateTime.fromJson(j);
                    case "iana" -> ZonedIntent.fromJson(j);
                    default -> CivilDateTime.fromJson(j);
                };
                default -> unsupported("a civil " + v.path("form").asText("") + " has no typed form yet");
            };
            case "instant" -> Instant.fromJson(j);
            case "amount" -> switch (v.path("basis").asText("")) {
                case "calendar" -> Period.fromJson(j);
                case "clock-ticks" -> PosixSeconds.fromJson(j);
                case "elapsed" -> ElapsedSeconds.fromJson(j);
                default -> unsupported("an amount with basis " + v.path("basis").asText("") + " has no typed form yet");
            };
            case "interval" -> Interval.fromJson(j);
            default -> unsupported("a " + j.get("kind").textValue() + " has no typed form yet; use the JSON layer");
        };
        return o.map(x -> (TemporalValue) x);
    }

    private static <T> Outcome<T> unsupported(String message) {
        return new Outcome.Unsupported<>(List.of(new Diagnostic("unsupported-operand", "error", "", message)));
    }
}
