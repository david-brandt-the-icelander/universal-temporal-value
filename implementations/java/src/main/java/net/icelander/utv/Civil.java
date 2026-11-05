package net.icelander.utv;

import com.fasterxml.jackson.databind.JsonNode;
import com.fasterxml.jackson.databind.node.ObjectNode;
import java.math.BigInteger;
import java.util.Iterator;
import java.util.List;
import java.util.Map;
import java.util.Set;
import java.util.regex.Pattern;

/**
 * A decoded, validated proleptic Gregorian civil value ({@code year-month}, {@code date},
 * {@code time} or {@code datetime}). Absent fields are null.
 */
record Civil(String form, BigInteger year, Integer month, Integer day, Integer hour, Integer minute,
             Integer second, Integer nano, Zone zone, Integer offsetAssertion, ObjectNode source) {

    /** A zone: {@code floating} (also for an absent zone), {@code fixed-offset} or {@code iana}. */
    record Zone(String type, int seconds, String id) {
        static final Zone FLOATING = new Zone("floating", 0, null);
        boolean floating() { return type.equals("floating"); }
        boolean fixed() { return type.equals("fixed-offset"); }
        boolean iana() { return type.equals("iana"); }
    }

    static final int MAX_OFFSET = 18 * 3600;
    private static final Pattern INTEGER = Pattern.compile("-?(0|[1-9][0-9]*)");
    private static final Map<String, List<String>> FIELDS = Map.of(
            "year-month", List.of("year", "month"),
            "date", List.of("year", "month", "day"),
            "time", List.of("hour", "minute", "second", "nano"),
            "datetime", List.of("year", "month", "day", "hour", "minute", "second", "nano"));
    private static final Set<String> OPTIONAL = Set.of("second", "nano");

    static boolean isCivil(JsonNode v) {
        return v != null && "civil".equals(v.path("kind").asText(null));
    }

    /** Checks that node is a structurally valid UTV value, or throws the structural outcome. */
    static void structural(JsonNode node, String what) {
        if (node == null || node.isNull() || node.isMissingNode()) {
            throw Fail.invalid("utv.argument.missing", "", what + " is missing");
        }
        UtvResult r = Utv.validateStructural(node);
        if (r.status() != Status.VALID) {
            Diagnostic d = r.diagnostics().isEmpty()
                    ? new Diagnostic("utv.structural", "error", "", "not a valid UTV value") : r.diagnostics().get(0);
            throw new Fail(r.status(), d.code(), d.path(), what + ": " + d.message());
        }
    }

    /** Decodes and validates a civil value, throwing {@code invalid} or {@code unsupported}. */
    static Civil decode(JsonNode node, String what) {
        structural(node, what);
        if (!isCivil(node)) throw Fail.unsupported("utv.kind", "/kind", what + " must be a civil value");
        ObjectNode v = (ObjectNode) node.get("value");
        JsonNode cal = v.get("calendar");
        if (!"gregory".equals(cal.path("id").asText(null)) || !"proleptic".equals(cal.path("chronology").asText(null))) {
            throw Fail.unsupported("utv.calendar", "/value/calendar", "only the proleptic Gregorian calendar is implemented");
        }
        String form = v.path("form").asText("");
        List<String> allowed = FIELDS.get(form);
        if (allowed == null) throw Fail.unsupported("utv.civil.form", "/value/form", "form " + form + " is not handled by this operation");
        JsonNode f = v.get("fields");
        if (f == null || !f.isObject()) throw Fail.invalid("utv.civil.fields", "/value/fields", "fields must be an object");
        for (Iterator<String> it = f.fieldNames(); it.hasNext(); ) {
            String name = it.next();
            if (!allowed.contains(name)) throw Fail.invalid("utv.civil.fields", "/value/fields/" + name, name + " is not a field of form " + form);
        }
        for (String name : allowed) {
            if (!OPTIONAL.contains(name) && !f.has(name)) throw Fail.invalid("utv.civil.fields", "/value/fields/" + name, form + " requires " + name);
        }
        BigInteger year = null;
        if (f.has("year")) {
            JsonNode y = f.get("year");
            if (!y.isTextual() || !INTEGER.matcher(y.textValue()).matches()) {
                throw Fail.invalid("utv.civil.year", "/value/fields/year", "year must be an integer string");
            }
            year = new BigInteger(y.textValue());
        }
        Integer month = num(f, "month", 1, 12);
        Integer day = f.has("day") ? num(f, "day", 1, GregorianCivil.daysInMonth(year, month)) : null;
        Integer hour = num(f, "hour", 0, 23);
        Integer minute = num(f, "minute", 0, 59);
        Integer second = num(f, "second", 0, 60);
        Integer nano = num(f, "nano", 0, 999_999_999);
        if (nano != null && second == null) throw Fail.invalid("utv.civil.nano", "/value/fields/nano", "nano requires second");
        Zone zone = Zone.FLOATING;
        JsonNode z = v.get("zone");
        if (z != null) {
            String type = z.path("type").asText("");
            switch (type) {
                case "floating" -> { }
                case "fixed-offset" -> {
                    JsonNode s = z.get("seconds");
                    if (s == null || !s.isInt() || Math.abs(s.intValue()) > MAX_OFFSET) {
                        throw Fail.invalid("utv.zone.offset", "/value/zone/seconds", "offset must be an integer number of seconds within +-18:00");
                    }
                    zone = new Zone(type, s.intValue(), null);
                }
                case "iana" -> {
                    JsonNode id = z.get("id");
                    if (id == null || !id.isTextual() || id.textValue().isEmpty()) {
                        throw Fail.invalid("utv.zone.id", "/value/zone/id", "an iana zone needs an id");
                    }
                    zone = new Zone(type, 0, id.textValue());
                }
                default -> throw Fail.unsupported("utv.zone.type", "/value/zone/type", "zone type " + type + " is not handled");
            }
        }
        Integer assertion = null;
        JsonNode a = v.get("offsetAssertion");
        if (a != null) {
            if (!a.isInt() || Math.abs(a.intValue()) > MAX_OFFSET) {
                throw Fail.invalid("utv.zone.offset", "/value/offsetAssertion", "offsetAssertion must be seconds within +-18:00");
            }
            assertion = a.intValue();
        }
        return new Civil(form, year, month, day, hour, minute, second, nano, zone, assertion, v);
    }

    private static Integer num(JsonNode f, String name, int lo, int hi) {
        JsonNode n = f.get(name);
        if (n == null) return null;
        if (!n.isInt() || n.intValue() < lo || n.intValue() > hi) {
            throw Fail.invalid("utv.civil." + name, "/value/fields/" + name,
                    name.equals("day") ? "day does not exist in that month" : name + " must be " + lo + ".." + hi);
        }
        return n.intValue();
    }

    int secondOr0() { return second == null ? 0 : second; }
    int nanoOr0() { return nano == null ? 0 : nano; }

    BigInteger epochDay() { return Gregorian.epochDay(year, month, day); }

    /** The wall time read as if it were UTC, in exact seconds (datetime only). */
    Rational wallSeconds() {
        BigInteger s = epochDay().multiply(BigInteger.valueOf(86400))
                .add(BigInteger.valueOf(hour * 3600L + minute * 60L + secondOr0()));
        return Rational.of(s).add(Rational.of(BigInteger.valueOf(nanoOr0()), Rational.BILLION));
    }

    /** Builds the canonical civil envelope (no zone). */
    static ObjectNode build(String form, BigInteger year, Integer month, Integer day, Integer hour, Integer minute,
                            Integer second, Integer nano) {
        ObjectNode o = Json.envelope("civil");
        ObjectNode v = o.putObject("value").put("form", form);
        ObjectNode f = v.putObject("fields");
        if (year != null) f.put("year", year.toString());
        if (month != null) f.put("month", month);
        if (day != null) f.put("day", day);
        if (hour != null) f.put("hour", hour);
        if (minute != null) f.put("minute", minute);
        if (second != null) f.put("second", second);
        if (nano != null && nano != 0) f.put("nano", nano);
        v.set("calendar", Json.calendar());
        return o;
    }

    /** A datetime at exact local seconds (wall time read as UTC); second always present, nano only if non-zero. */
    static ObjectNode datetimeAt(Rational local) {
        BigInteger secs = local.floor();
        BigInteger days = Gregorian.floorDiv(secs, BigInteger.valueOf(86400));
        int sod = secs.subtract(days.multiply(BigInteger.valueOf(86400))).intValueExact();
        Rational nanos = local.fraction().multiply(Rational.BILLION);
        if (!nanos.isInteger()) throw Fail.unsupported("utv.precision", "/value/subTick", "the fraction of a second is finer than a nanosecond");
        Gregorian.Ymd d = Gregorian.fromEpochDay(days);
        return build("datetime", d.year(), d.month(), d.day(), sod / 3600, sod / 60 % 60, sod % 60, nanos.num.intValueExact());
    }
}
