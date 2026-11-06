package net.icelander.utv;

import com.fasterxml.jackson.databind.JsonNode;
import com.fasterxml.jackson.databind.node.ObjectNode;
import java.math.BigInteger;
import java.util.Iterator;
import java.util.LinkedHashMap;
import java.util.List;
import java.util.Map;
import java.util.regex.Pattern;

/**
 * Everyday calendar operations on UTV values: {@code fields}, {@code add}, {@code compare},
 * {@code daysBetween} and {@code posixSecondsBetween}. Pure: never reads the clock, zone or locale.
 * Semantic outcomes are statuses in the returned {@link UtvResult}, never exceptions.
 */
public final class Operations {
    private static final BigInteger B12 = BigInteger.valueOf(12), B7 = BigInteger.valueOf(7), DAY = BigInteger.valueOf(86400);
    private static final Pattern INTEGER = Pattern.compile("-?(0|[1-9][0-9]*)");
    private static final Pattern DECIMAL = Pattern.compile("-?[0-9]+\\.[0-9]+");
    private static final List<String> COMPONENTS = List.of("years", "months", "weeks", "days", "hours", "minutes", "seconds");

    /**
     * The 0.2 spelling of {@link net.icelander.utv.MonthEnd}.
     *
     * @deprecated since 0.3: use the top-level {@link net.icelander.utv.MonthEnd}.
     */
    @Deprecated(since = "0.3")
    public enum MonthEnd {
        REJECT, CONSTRAIN;

        public String wire() { return policy().wire(); }

        net.icelander.utv.MonthEnd policy() {
            return this == REJECT ? net.icelander.utv.MonthEnd.REJECT : net.icelander.utv.MonthEnd.CONSTRAIN;
        }
    }

    private Operations() {}

    // ======================================================================== fields

    /** Day of week (Monday = 1), day of year, ISO week and year, days in month and leap year for a date or datetime. */
    public static UtvResult fields(JsonNode value) {
        return Fail.run("fields", () -> {
            Civil c = Civil.decode(value, "value");
            if (!c.form().equals("date") && !c.form().equals("datetime")) {
                throw Fail.unsupported("unsupported-operand", "/value/form", "fields applies to date and datetime values");
            }
            BigInteger ed = c.epochDay();
            int dow = Gregorian.dayOfWeek(ed);
            int doy = ed.subtract(Gregorian.epochDay(c.year(), 1, 1)).intValueExact() + 1;
            BigInteger isoYear = c.year();
            int week = (doy - dow + 10) / 7;
            if (week < 1) {
                isoYear = isoYear.subtract(BigInteger.ONE);
                week = weeksInIsoYear(isoYear);
            } else if (week > weeksInIsoYear(isoYear)) {
                isoYear = isoYear.add(BigInteger.ONE);
                week = 1;
            }
            ObjectNode r = Json.F.objectNode().put("dayOfWeek", dow).put("dayOfYear", doy)
                    .put("isoWeekYear", isoYear.toString()).put("isoWeek", week)
                    .put("daysInMonth", GregorianCivil.daysInMonth(c.year(), c.month()))
                    .put("isLeapYear", GregorianCivil.isLeapYear(c.year()));
            return UtvResult.unique("fields", r);
        });
    }

    private static int weeksInIsoYear(BigInteger y) {
        int jan1 = Gregorian.dayOfWeek(Gregorian.epochDay(y, 1, 1));
        return jan1 == 4 || (jan1 == 3 && GregorianCivil.isLeapYear(y)) ? 53 : 52;
    }

    // ======================================================================== add

    /** {@code add} with the default {@link net.icelander.utv.MonthEnd#REJECT} policy. */
    public static UtvResult add(JsonNode value, JsonNode amount) { return add(value, amount, net.icelander.utv.MonthEnd.REJECT); }

    /** @deprecated since 0.3: pass the top-level {@link net.icelander.utv.MonthEnd}. */
    @Deprecated(since = "0.3")
    public static UtvResult add(JsonNode value, JsonNode amount, MonthEnd monthEnd) {
        return add(value, amount, monthEnd == null ? null : monthEnd.policy());
    }

    /**
     * Adds an amount: a calendar amount to a civil value (years and months first, then weeks and
     * days, then wall-clock hours, minutes and seconds), or a posixSeconds amount to an instant.
     */
    public static UtvResult add(JsonNode value, JsonNode amount, net.icelander.utv.MonthEnd monthEnd) {
        return Fail.run("add", () -> doAdd(value, amount, monthEnd == null ? net.icelander.utv.MonthEnd.REJECT : monthEnd));
    }

    /** {@code add} with a JSON policy {@code {"monthEnd": "reject" | "constrain"}}; null means reject. */
    public static UtvResult add(JsonNode value, JsonNode amount, JsonNode policy) {
        return Fail.run("add", () -> {
            net.icelander.utv.MonthEnd p = net.icelander.utv.MonthEnd.REJECT;
            JsonNode me = policy == null || policy.isNull() ? null : policy.get("monthEnd");
            if (me != null) {
                p = net.icelander.utv.MonthEnd.fromWire(me.asText(""));
                if (p == null) throw Fail.unsupported("unsupported-policy", "/monthEnd", "unknown monthEnd policy " + me);
            }
            return doAdd(value, amount, p);
        });
    }

    private static UtvResult doAdd(JsonNode value, JsonNode amount, net.icelander.utv.MonthEnd monthEnd) {
        UtvResult r = doAddFields(value, amount, monthEnd);
        JsonNode v = r.value().map(x -> x.get("value")).orElse(null);
        if (v != null && v.has("offsetAssertion")) {
            ObjectNode o = r.value().get().deepCopy();
            ((ObjectNode) o.get("value")).remove("offsetAssertion");
            return new UtvResult("add", Status.UNIQUE, List.of(new Diagnostic("offset-assertion-dropped", "info", "/value/offsetAssertion",
                    "the asserted offset may not hold at the new time; the zone and zoneIntent are kept")), List.of(o));
        }
        return r;
    }

    private static UtvResult doAddFields(JsonNode value, JsonNode amount, net.icelander.utv.MonthEnd monthEnd) {
        Civil.structural(value, "value");
        Civil.structural(amount, "amount");
        if (!"amount".equals(amount.path("kind").asText(null))) throw Fail.invalid("kind-mismatch", "/kind", "the second argument must be an amount");
        String basis = amount.get("value").path("basis").asText("");
        if (Instants.isInstant(value)) {
            Rational t = Instants.decode(value, "value");
            return switch (basis) {
                case "calendar" -> throw Fail.invalid("kind-mismatch", "/value/basis",
                        "a calendar amount has no meaning on an instant: convert to a zone first");
                case "elapsed" -> throw Fail.unsupported("needs-leap-seconds", "/value/basis",
                        "SI elapsed seconds on a POSIX instant need a leap-second table");
                default -> UtvResult.unique("add", Json.instant(t.add(Instants.posixAmount(amount, "amount"))));
            };
        }
        Civil c = Civil.decode(value, "value");
        if (c.form().equals("time")) {
            throw Fail.unsupported("unsupported-operand", "/value/form", "adding to a time of day would wrap past midnight and lose information");
        }
        if (!basis.equals("calendar")) {
            throw Fail.unsupported("unsupported-operand", "/value/basis", "only calendar amounts apply to civil values; resolve to an instant for posixSeconds");
        }
        Map<String, BigInteger> k = components(amount.get("value").get("components"));
        for (Map.Entry<String, BigInteger> e : k.entrySet()) {
            if (e.getValue().signum() == 0) continue;
            String n = e.getKey();
            boolean timePart = n.equals("hours") || n.equals("minutes") || n.equals("seconds");
            if (c.form().equals("date") && timePart) throw Fail.invalid("kind-mismatch", "/value/components/" + n, "a date has no " + n);
            if (c.form().equals("year-month") && !(n.equals("years") || n.equals("months"))) {
                throw Fail.invalid("kind-mismatch", "/value/components/" + n, "a year-month accepts only years and months");
            }
        }
        if (c.second() != null && c.second() == 60) throw Fail.unsupported("leap-second", "/value/fields/second", "second 60 needs a leap-second table");

        // 1. years and months together
        BigInteger ym = c.year().multiply(B12).add(BigInteger.valueOf(c.month() - 1))
                .add(k.get("years").multiply(B12)).add(k.get("months"));
        BigInteger year = Gregorian.floorDiv(ym, B12);
        int month = ym.subtract(year.multiply(B12)).intValueExact() + 1;
        if (c.form().equals("year-month")) {
            return UtvResult.unique("add", withFields(value, Civil.build("year-month", year, month, null, null, null, null, null)));
        }
        // 2. month end
        int day = c.day();
        int dim = GregorianCivil.daysInMonth(year, month);
        if (day > dim) {
            if (monthEnd == net.icelander.utv.MonthEnd.REJECT) {
                return UtvResult.of("add", Status.NONE, new Diagnostic("month-end", "warning", "/value/fields/day",
                        Iso.year(year) + "-" + (month < 10 ? "0" : "") + month + " has no day " + day));
            }
            day = dim;
        }
        // 3. weeks and days
        BigInteger ed = Gregorian.epochDay(year, month, day).add(k.get("weeks").multiply(B7)).add(k.get("days"));
        if (c.form().equals("date")) {
            Gregorian.Ymd d = Gregorian.fromEpochDay(ed);
            return UtvResult.unique("add", withFields(value, Civil.build("date", d.year(), d.month(), d.day(), null, null, null, null)));
        }
        // 4. wall-clock hours, minutes and seconds, carrying into days
        BigInteger sod = BigInteger.valueOf(c.hour() * 3600L + c.minute() * 60L + c.secondOr0())
                .add(k.get("hours").multiply(BigInteger.valueOf(3600))).add(k.get("minutes").multiply(BigInteger.valueOf(60)))
                .add(k.get("seconds"));
        BigInteger carry = Gregorian.floorDiv(sod, DAY);
        int s = sod.subtract(carry.multiply(DAY)).intValueExact();
        Gregorian.Ymd d = Gregorian.fromEpochDay(ed.add(carry));
        Integer second = c.second() != null || s % 60 != 0 ? Integer.valueOf(s % 60) : null;
        ObjectNode built = Civil.build("datetime", d.year(), d.month(), d.day(), s / 3600, s / 60 % 60, second, c.nano());
        if (c.nano() != null && c.nano() == 0) ((ObjectNode) built.get("value").get("fields")).put("nano", 0);
        return UtvResult.unique("add", withFields(value, built));
    }

    /** The input value with its fields replaced (zone, zoneIntent and every other property carried over). */
    private static ObjectNode withFields(JsonNode original, ObjectNode built) {
        ObjectNode o = original.deepCopy();
        ((ObjectNode) o.get("value")).set("fields", built.get("value").get("fields"));
        return o;
    }

    static Map<String, BigInteger> components(JsonNode comps) {
        if (comps == null || !comps.isObject()) throw Fail.invalid("malformed-value", "/value/components", "a calendar amount needs components");
        Map<String, BigInteger> m = new LinkedHashMap<>();
        for (String n : COMPONENTS) m.put(n, BigInteger.ZERO);
        for (Iterator<Map.Entry<String, JsonNode>> it = comps.fields(); it.hasNext(); ) {
            Map.Entry<String, JsonNode> e = it.next();
            String path = "/value/components/" + e.getKey();
            if (!COMPONENTS.contains(e.getKey())) throw Fail.unsupported("unsupported-operand", path, "component " + e.getKey() + " is not handled");
            JsonNode v = e.getValue();
            if (v.isTextual() && INTEGER.matcher(v.textValue()).matches()) {
                m.put(e.getKey(), new BigInteger(v.textValue()));
            } else if (v.isTextual() && DECIMAL.matcher(v.textValue()).matches()) {
                throw Fail.unsupported("unsupported-form", path, "fractional calendar components are not handled");
            } else {
                throw Fail.invalid("malformed-value", path, "component values are integer decimal strings");
            }
        }
        return m;
    }

    // ======================================================================== compare

    /**
     * Orders a relative to b: {@code "before"}, {@code "equal"} or {@code "after"}. Unzoned civil
     * values of one form compare by fields; instants and fixed-offset date-times compare exactly.
     * Anything with a named zone is {@code unsupported}: resolve it first.
     */
    public static UtvResult compare(JsonNode a, JsonNode b) {
        return Fail.run("compare", () -> {
            Civil.structural(a, "a");
            Civil.structural(b, "b");
            int cmp;
            if (Instants.isInstant(a) && Instants.isInstant(b)) {
                cmp = Instants.decode(a, "a").compareTo(Instants.decode(b, "b"));
            } else if (Civil.isCivil(a) && Civil.isCivil(b)) {
                Civil x = Civil.decode(a, "a"), y = Civil.decode(b, "b");
                if (x.zone().iana() || y.zone().iana()) {
                    throw Fail.unsupported("needs-zone-rules", "/value/zone",
                            "ordering values in a named zone depends on zone rules: resolve them to instants first");
                }
                if (!x.form().equals(y.form())) throw Fail.unsupported("unsupported-operand", "/value/form", "cannot order a " + x.form() + " against a " + y.form());
                if (x.zone().fixed() && y.zone().fixed() && x.form().equals("datetime")) {
                    if (x.secondOr0() == 60 || y.secondOr0() == 60) throw Fail.unsupported("leap-second", "/value/fields/second", "second 60 needs a leap-second table");
                    cmp = x.wallSeconds().subtract(Rational.of(x.zone().seconds()))
                            .compareTo(y.wallSeconds().subtract(Rational.of(y.zone().seconds())));
                } else if (x.zone().floating() && y.zone().floating()) {
                    cmp = civilOrder(x, y);
                } else {
                    throw Fail.unsupported("unsupported-operand", "/value/zone", "cannot order an unzoned value against one with an offset");
                }
            } else {
                throw Fail.unsupported("unsupported-operand", "/kind", "compare needs two civil values or two instants");
            }
            return UtvResult.unique("compare", Json.F.textNode(cmp < 0 ? "before" : cmp == 0 ? "equal" : "after"));
        });
    }

    private static int civilOrder(Civil x, Civil y) {
        int c = 0;
        if (x.year() != null) c = x.year().compareTo(y.year());
        if (c == 0 && x.month() != null) c = Integer.compare(x.month(), y.month());
        if (c == 0 && x.day() != null) c = Integer.compare(x.day(), y.day());
        if (c == 0 && x.hour() != null) c = Integer.compare(x.hour(), y.hour());
        if (c == 0 && x.minute() != null) c = Integer.compare(x.minute(), y.minute());
        if (c == 0 && x.hour() != null) c = Integer.compare(x.secondOr0(), y.secondOr0());
        if (c == 0 && x.hour() != null) c = Integer.compare(x.nanoOr0(), y.nanoOr0());
        return c;
    }

    // ======================================================================== differences

    /** Whole days from date a to date b (b - a), as a JSON integer. */
    public static UtvResult daysBetween(JsonNode a, JsonNode b) {
        return Fail.run("daysBetween", () -> {
            Civil x = Civil.decode(a, "a"), y = Civil.decode(b, "b");
            if (!x.form().equals("date") || !y.form().equals("date")) {
                throw Fail.unsupported("unsupported-operand", "/value/form", "daysBetween takes two dates");
            }
            if (!x.zone().equals(y.zone())) {
                throw Fail.unsupported("different-zones", "/value/zone", "daysBetween needs two unzoned dates or two dates in the same zone");
            }
            return UtvResult.unique("daysBetween", Json.integer(y.epochDay().subtract(x.epochDay())));
        });
    }

    /** POSIX seconds from instant a to instant b (b - a), exact, as a posixSeconds amount. */
    public static UtvResult posixSecondsBetween(JsonNode a, JsonNode b) {
        return Fail.run("posixSecondsBetween", () -> UtvResult.unique("posixSecondsBetween",
                Json.posixSeconds(Instants.decode(b, "b").subtract(Instants.decode(a, "a")))));
    }
}
