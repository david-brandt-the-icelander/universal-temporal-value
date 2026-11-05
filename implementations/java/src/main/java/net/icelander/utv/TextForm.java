package net.icelander.utv;

import com.fasterxml.jackson.databind.JsonNode;
import com.fasterxml.jackson.databind.node.ObjectNode;
import java.math.BigInteger;
import java.util.List;
import java.util.Map;
import java.util.regex.Matcher;
import java.util.regex.Pattern;

/**
 * The UTV text form (profile {@code utv-text/1}, {@code docs/text-form.md}) at the JSON layer:
 * {@link #parseText} and {@link #formatText}. Points are the ISO 8601 / RFC 9557 forms of
 * {@link Iso}; periods are ISO durations ({@code P1M}, {@code -P1DT2H}); intervals use brackets
 * ({@code [2026-11-01, 2026-12-01)}, {@code [2026-11-01, ..)}, {@code [2026-11-01, ?)}), and the
 * EDTF slash forms are accepted on input. Pure: never reads the clock, time zone or locale.
 */
public final class TextForm {
    private static final List<String> COMPONENTS = List.of("years", "months", "weeks", "days", "hours", "minutes", "seconds");
    private static final Pattern PERIOD = Pattern.compile(
            "(-)?P(?:(\\d+)Y)?(?:(\\d+)M)?(?:(\\d+)W)?(?:(\\d+)D)?(?:T(?:(\\d+)H)?(?:(\\d+)M)?(?:(\\d+)S)?)?");
    private static final String DEC = "\\d+(?:[.,]\\d+)?";
    private static final Pattern DECIMAL_PERIOD = Pattern.compile(
            "-?P(?:" + DEC + "Y)?(?:" + DEC + "M)?(?:" + DEC + "W)?(?:" + DEC + "D)?(?:T(?:" + DEC + "H)?(?:" + DEC + "M)?(?:" + DEC + "S)?)?");
    private static final Pattern EDTF_QUALIFIED = Pattern.compile("[+-]?[0-9X]{4,}(?:-[0-9X]{2}(?:-[0-9X]{2})?)?[?~%]?");

    private TextForm() {}

    // ======================================================================== parseText

    /** Parses UTV text into a UTV value: a point, a period or an interval. */
    public static UtvResult parseText(String text) {
        return Fail.run("parseText", () -> UtvResult.unique("parseText", parse(text)));
    }

    static ObjectNode parse(String text) {
        if (text == null || text.isEmpty()) throw Fail.invalid("syntax", "", "no text");
        if (text.startsWith("[") || text.startsWith("(")) return bracketInterval(text);
        int slash = topLevelSlash(text);
        if (slash >= 0) return slashInterval(text, slash);
        if (text.startsWith("P") || text.startsWith("-P")) return period(text);
        if (EDTF_QUALIFIED.matcher(text).matches() && (text.contains("X") || text.matches(".*[?~%]"))) {
            throw Fail.unsupported("unsupported-form", "", "EDTF qualifiers and unspecified digits are not mapped to UTV yet: " + text);
        }
        return Iso.parse(text);
    }

    private static ObjectNode period(String text) {
        Matcher m = PERIOD.matcher(text);
        boolean hasT = text.contains("T");
        if (!m.matches()) {
            if (DECIMAL_PERIOD.matcher(text).matches()) {
                throw Fail.unsupported("unsupported-form", "", "calendar components are whole numbers: " + text);
            }
            throw Fail.invalid("syntax", "", "not an ISO 8601 duration: " + text);
        }
        boolean any = false, anyTime = false;
        for (int i = 2; i <= 8; i++) {
            if (m.group(i) != null) {
                any = true;
                if (i >= 6) anyTime = true;
            }
        }
        if (!any || (hasT && !anyTime)) throw Fail.invalid("syntax", "", "a duration needs at least one component (and one after T): " + text);
        boolean negative = m.group(1) != null;
        ObjectNode o = Json.envelope("amount");
        ObjectNode c = o.putObject("value").put("basis", "calendar").putObject("components");
        for (int i = 0; i < COMPONENTS.size(); i++) {
            String g = m.group(i + 2);
            if (g == null) continue;
            BigInteger n = new BigInteger(g);
            if (n.signum() != 0) c.put(COMPONENTS.get(i), (negative ? n.negate() : n).toString());
        }
        return o;
    }

    /** {@code [a, b)}, {@code (a, b]}, with {@code ..} (unbounded) or {@code ?} (unknown) on an open side. */
    private static ObjectNode bracketInterval(String text) {
        char open = text.charAt(0), close = text.charAt(text.length() - 1);
        if (text.length() < 2 || (close != ')' && close != ']')) throw Fail.invalid("syntax", "", "an interval ends with ) or ]: " + text);
        String inner = text.substring(1, text.length() - 1);
        int comma = inner.indexOf(',');
        if (comma < 0 || inner.indexOf(',', comma + 1) >= 0) throw Fail.invalid("syntax", "", "an interval has two endpoints separated by a comma: " + text);
        String a = inner.substring(0, comma), b = inner.substring(comma + 1).replaceFirst("^\\s+", "");
        return interval(endpoint(a, open == '[', open == '(', "start"), endpoint(b, close == ']', close == ')', "end"));
    }

    private static ObjectNode endpoint(String token, boolean included, boolean open, String side) {
        if (token.equals("..") || token.equals("?")) {
            if (!open) throw Fail.invalid("syntax", "", "an " + (token.equals("..") ? "unbounded" : "unknown") + " " + side + " needs an open bracket");
            return Json.F.objectNode().put("state", token.equals("..") ? "unbounded" : "unknown");
        }
        if (token.isEmpty()) throw Fail.invalid("syntax", "", "the interval's " + side + " is empty");
        ObjectNode e = Json.F.objectNode().put("state", included ? "included" : "excluded");
        e.set("point", Iso.parse(token));
        return e;
    }

    /** EDTF {@code a/b}: closed; {@code ..} is unbounded and an empty side is unknown. */
    private static ObjectNode slashInterval(String text, int slash) {
        String a = text.substring(0, slash), b = text.substring(slash + 1);
        if (a.isEmpty() && b.isEmpty()) throw Fail.invalid("syntax", "", "an interval needs at least one endpoint: " + text);
        return interval(slashEndpoint(a), slashEndpoint(b));
    }

    private static ObjectNode slashEndpoint(String token) {
        if (token.equals("..")) return Json.F.objectNode().put("state", "unbounded");
        if (token.isEmpty()) return Json.F.objectNode().put("state", "unknown");
        ObjectNode e = Json.F.objectNode().put("state", "included");
        e.set("point", Iso.parse(token));
        return e;
    }

    /** The index of a '/' outside any [...] annotation, or -1. Zone names contain slashes too. */
    private static int topLevelSlash(String text) {
        int depth = 0;
        for (int i = 0; i < text.length(); i++) {
            char ch = text.charAt(i);
            if (ch == '[') depth++;
            else if (ch == ']') depth--;
            else if (ch == '/' && depth == 0) return i;
        }
        return -1;
    }

    private static ObjectNode interval(ObjectNode start, ObjectNode end) {
        JsonNode p = start.get("point"), q = end.get("point");
        if (p == null && q == null) {
            throw Fail.unsupported("unsupported-form", "", "an interval without any point has no domain");
        }
        if (p != null && q != null) {
            if (!sameKindAndForm(p, q)) throw Fail.invalid("kind-mismatch", "", "the endpoints are different kinds or forms of value");
            UtvResult order = Operations.compare(p, q);
            if (order.isUnique() && "after".equals(order.candidates().get(0).textValue())) {
                throw Fail.invalid("reversed-interval", "", "the end is before the start");
            }
        }
        JsonNode point = p != null ? p : q;
        ObjectNode o = Json.envelope("interval");
        ObjectNode v = o.putObject("value");
        v.put("domain", Instants.isInstant(point) ? "physical" : "civil/gregory");
        v.set("start", start);
        v.set("end", end);
        return o;
    }

    private static boolean sameKindAndForm(JsonNode p, JsonNode q) {
        if (!p.get("kind").equals(q.get("kind"))) return false;
        if (!Civil.isCivil(p)) return true;
        JsonNode a = p.get("value"), b = q.get("value");
        return a.get("form").equals(b.get("form")) && a.path("zone").path("type").equals(b.path("zone").path("type"));
    }

    // ======================================================================== formatText

    /** Formats a value as UTV text; the candidate is a JSON string. Values without a text form are {@code unsupported}. */
    public static UtvResult formatText(JsonNode value) {
        return Fail.run("formatText", () -> UtvResult.unique("formatText", Json.F.textNode(format(value))));
    }

    static String format(JsonNode value) {
        Civil.structural(value, "value");
        String kind = value.get("kind").textValue();
        return switch (kind) {
            case "civil", "instant" -> Iso.format(value);
            case "amount" -> period(value);
            case "interval" -> interval(value);
            default -> throw noTextForm("a " + kind + " value has no text form; write it as JSON");
        };
    }

    private static String period(JsonNode value) {
        JsonNode v = value.get("value");
        if (!"calendar".equals(v.path("basis").asText())) {
            throw noTextForm("only calendar amounts (periods) have a text form; exact second counts have none");
        }
        Map<String, BigInteger> k = Operations.components(v.get("components"));
        boolean neg = false, pos = false;
        for (BigInteger n : k.values()) {
            neg |= n.signum() < 0;
            pos |= n.signum() > 0;
        }
        if (neg && pos) throw noTextForm("a period with mixed signs has no ISO 8601 notation");
        if (!neg && !pos) return "P0D";
        StringBuilder sb = new StringBuilder(neg ? "-P" : "P");
        String[] units = {"Y", "M", "W", "D", "H", "M", "S"};
        for (int i = 0; i < COMPONENTS.size(); i++) {
            if (i == 4 && (k.get("hours").signum() != 0 || k.get("minutes").signum() != 0 || k.get("seconds").signum() != 0)) sb.append('T');
            BigInteger n = k.get(COMPONENTS.get(i));
            if (n.signum() != 0) sb.append(n.abs()).append(units[i]);
        }
        return sb.toString();
    }

    private static String interval(JsonNode value) {
        JsonNode v = value.get("value");
        if (!"start-end".equals(v.path("construction").asText("start-end"))) {
            throw noTextForm("only start-end intervals have a text form");
        }
        return side(v.get("start"), true) + ", " + side(v.get("end"), false);
    }

    private static String side(JsonNode e, boolean start) {
        String state = e.path("state").asText("");
        String token = switch (state) {
            case "unbounded" -> "..";
            case "unknown" -> "?";
            case "included", "excluded" -> Iso.format(e.get("point"));
            default -> throw Fail.invalid("malformed-value", "", "unknown endpoint state " + state);
        };
        boolean closed = state.equals("included");
        return start ? (closed ? "[" : "(") + token : token + (closed ? "]" : ")");
    }

    private static Fail noTextForm(String message) {
        return Fail.unsupported("no-text-form", "", message);
    }
}
