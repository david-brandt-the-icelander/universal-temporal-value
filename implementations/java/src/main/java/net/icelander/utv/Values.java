package net.icelander.utv;

import com.fasterxml.jackson.databind.JsonNode;
import com.fasterxml.jackson.databind.node.ObjectNode;
import java.math.BigInteger;

/**
 * Builders for the canonical UTV value shapes of the everyday API ({@code docs/everyday-api.md}).
 *
 * <p>Builders only build: they do not validate. {@code date(2026, 2, 30)} is a well-formed value
 * that {@link Iso#formatIso}, {@link Operations#fields} and the other operations report as
 * {@code invalid}. Every builder returns a fresh Jackson {@link ObjectNode}.
 */
public final class Values {
    private Values() {}

    // ---- civil ----

    public static ObjectNode date(long year, int month, int day) { return date(BigInteger.valueOf(year), month, day); }

    public static ObjectNode date(BigInteger year, int month, int day) {
        return Civil.build("date", year, month, day, null, null, null, null);
    }

    public static ObjectNode yearMonth(long year, int month) {
        return Civil.build("year-month", BigInteger.valueOf(year), month, null, null, null, null, null);
    }

    public static ObjectNode time(int hour, int minute) { return Civil.build("time", null, null, null, hour, minute, null, null); }
    public static ObjectNode time(int hour, int minute, int second) { return Civil.build("time", null, null, null, hour, minute, second, null); }
    public static ObjectNode time(int hour, int minute, int second, int nano) {
        return Civil.build("time", null, null, null, hour, minute, second, nano);
    }

    /** An unzoned ("floating") local date-time: no {@code zone} property. */
    public static ObjectNode dateTime(long year, int month, int day, int hour, int minute) {
        return Civil.build("datetime", BigInteger.valueOf(year), month, day, hour, minute, null, null);
    }

    public static ObjectNode dateTime(long year, int month, int day, int hour, int minute, int second) {
        return Civil.build("datetime", BigInteger.valueOf(year), month, day, hour, minute, second, null);
    }

    public static ObjectNode dateTime(long year, int month, int day, int hour, int minute, int second, int nano) {
        return Civil.build("datetime", BigInteger.valueOf(year), month, day, hour, minute, second, nano);
    }

    /** A copy of {@code dateTime} that follows the named IANA zone (a zoned intention). */
    public static ObjectNode inZone(JsonNode dateTime, String zoneId) {
        ObjectNode o = dateTime.deepCopy();
        ObjectNode v = (ObjectNode) o.get("value");
        v.putObject("zone").put("type", "iana").put("id", zoneId);
        v.put("zoneIntent", "follow-zone-id");
        return o;
    }

    /** A copy of {@code dateTime} at a fixed UTC offset, in seconds east of UTC. */
    public static ObjectNode withOffset(JsonNode dateTime, int offsetSeconds) {
        ObjectNode o = dateTime.deepCopy();
        ObjectNode v = (ObjectNode) o.get("value");
        v.putObject("zone").put("type", "fixed-offset").put("seconds", offsetSeconds);
        v.put("zoneIntent", "fixed-offset");
        return o;
    }

    // ---- instants and amounts ----

    /** A POSIX instant at whole epoch seconds. */
    public static ObjectNode instant(long epochSeconds) { return instant(epochSeconds, 0); }

    /** A POSIX instant; {@code nano} becomes a reduced {@code subTick} fraction when non-zero. */
    public static ObjectNode instant(long epochSeconds, long nano) {
        return Json.instant(Rational.of(epochSeconds).add(Rational.of(BigInteger.valueOf(nano), Rational.BILLION)));
    }

    /** A calendar amount. Only non-zero components are written; each may be negative. */
    public static ObjectNode period(long years, long months, long weeks, long days, long hours, long minutes, long seconds) {
        ObjectNode o = Json.envelope("amount");
        ObjectNode c = o.putObject("value").put("basis", "calendar").putObject("components");
        String[] names = {"years", "months", "weeks", "days", "hours", "minutes", "seconds"};
        long[] values = {years, months, weeks, days, hours, minutes, seconds};
        for (int i = 0; i < names.length; i++) if (values[i] != 0) c.put(names[i], Long.toString(values[i]));
        return o;
    }

    public static ObjectNode years(long n) { return period(n, 0, 0, 0, 0, 0, 0); }
    public static ObjectNode months(long n) { return period(0, n, 0, 0, 0, 0, 0); }
    public static ObjectNode weeks(long n) { return period(0, 0, n, 0, 0, 0, 0); }
    public static ObjectNode days(long n) { return period(0, 0, 0, n, 0, 0, 0); }
    public static ObjectNode hours(long n) { return period(0, 0, 0, 0, n, 0, 0); }
    public static ObjectNode minutes(long n) { return period(0, 0, 0, 0, 0, n, 0); }
    public static ObjectNode seconds(long n) { return period(0, 0, 0, 0, 0, 0, n); }

    /** POSIX seconds (not SI seconds across a leap second). */
    public static ObjectNode posixSeconds(long n) { return Json.posixSeconds(Rational.of(n)); }

    /** POSIX seconds as an exact fraction numerator/denominator (reduced). */
    public static ObjectNode posixSeconds(BigInteger numerator, BigInteger denominator) {
        return Json.posixSeconds(Rational.of(numerator, denominator));
    }

    // ---- intervals ----

    /** An endpoint marker for {@link #interval}: no bound on that side. */
    public static ObjectNode unbounded() { return Json.F.objectNode().put("state", "unbounded"); }

    /** An endpoint marker for {@link #interval}: the bound exists but is not known. */
    public static ObjectNode unknown() { return Json.F.objectNode().put("state", "unknown"); }

    /** A half-open interval {@code [start, end)}. Either side may be {@link #unbounded()} or {@link #unknown()}. */
    public static ObjectNode interval(JsonNode start, JsonNode end) { return interval(start, end, true, false); }

    public static ObjectNode interval(JsonNode start, JsonNode end, boolean startIncluded, boolean endIncluded) {
        ObjectNode o = Json.envelope("interval");
        ObjectNode v = o.putObject("value");
        JsonNode point = isMarker(start) ? end : start;
        v.put("domain", point != null && Instants.isInstant(point) ? "physical" : "civil/gregory");
        v.set("start", endpoint(start, startIncluded));
        v.set("end", endpoint(end, endIncluded));
        return o;
    }

    private static boolean isMarker(JsonNode n) { return n != null && n.has("state") && !n.has("schema"); }

    private static ObjectNode endpoint(JsonNode p, boolean included) {
        if (isMarker(p)) return (ObjectNode) p.deepCopy();
        ObjectNode e = Json.F.objectNode().put("state", included ? "included" : "excluded");
        e.set("point", p.deepCopy());
        return e;
    }
}
