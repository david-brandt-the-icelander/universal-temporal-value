package net.icelander.utv;

import com.fasterxml.jackson.databind.JsonNode;
import com.fasterxml.jackson.databind.node.ObjectNode;
import java.math.BigInteger;
import java.util.ArrayList;
import java.util.List;
import java.util.regex.Matcher;
import java.util.regex.Pattern;

/**
 * ISO 8601 extended format and RFC 3339 / RFC 9557 text: {@link #parseIso} and {@link #formatIso}.
 * Pure: never reads the clock, time zone or locale.
 */
public final class Iso {
    private static final String YEAR = "([+-]\\d{4,}|\\d{4})";
    private static final String TIME = "(\\d{2}):(\\d{2})(?::(\\d{2})(?:\\.(\\d{1,9}))?)?";
    private static final Pattern YEAR_MONTH = Pattern.compile(YEAR + "-(\\d{2})");
    private static final Pattern DATE = Pattern.compile(YEAR + "-(\\d{2})-(\\d{2})");
    private static final Pattern TIME_ONLY = Pattern.compile(TIME);
    private static final Pattern DATE_TIME = Pattern.compile(
            YEAR + "-(\\d{2})-(\\d{2})T" + TIME + "(Z|[+-]\\d{2}:\\d{2})?((?:\\[[^\\[\\]]*\\])*)");
    private static final Pattern ANNOTATION = Pattern.compile("\\[([^\\[\\]]*)\\]");
    private static final Pattern ZONE_NAME = Pattern.compile("[A-Za-z._][A-Za-z0-9._+-]*(/[A-Za-z._][A-Za-z0-9._+-]*)*");
    private static final Pattern OFFSET = Pattern.compile("([+-])(\\d{2}):(\\d{2})");
    private static final Pattern WEEK_DATE = Pattern.compile(YEAR + "-?W\\d{2}(-?\\d)?(T.*)?");
    private static final Pattern ORDINAL_DATE = Pattern.compile(YEAR + "-\\d{3}(T.*)?");
    private static final Pattern BASIC = Pattern.compile(
            "[+-]?\\d{8,}(T\\d{2}(\\d{2}(\\d{2}([.,]\\d+)?)?)?(Z|[+-]\\d{2}(\\d{2})?)?)?");

    private Iso() {}

    // ======================================================================== parseIso

    /** Parses ISO 8601 extended / RFC 3339 / RFC 9557 text into a UTV value. */
    public static UtvResult parseIso(String text) {
        return Fail.run("parseIso", () -> UtvResult.unique("parseIso", parse(text)));
    }

    private static ObjectNode parse(String text) {
        if (text == null) throw Fail.invalid("utv.iso.syntax", "", "no text");
        Matcher m;
        if ((m = DATE.matcher(text)).matches()) {
            Parts p = new Parts();
            p.date(m.group(1), m.group(2), m.group(3));
            p.check();
            return Civil.build("date", p.year, p.month, p.day, null, null, null, null);
        }
        if ((m = YEAR_MONTH.matcher(text)).matches()) {
            Parts p = new Parts();
            p.year = year(m.group(1));
            p.month = Integer.parseInt(m.group(2));
            p.check();
            return Civil.build("year-month", p.year, p.month, null, null, null, null, null);
        }
        if ((m = TIME_ONLY.matcher(text)).matches()) {
            Parts p = new Parts();
            p.time(m.group(1), m.group(2), m.group(3), m.group(4));
            p.check();
            return Civil.build("time", null, null, null, p.hour, p.minute, p.second, p.nano);
        }
        if ((m = DATE_TIME.matcher(text)).matches()) {
            Parts p = new Parts();
            p.date(m.group(1), m.group(2), m.group(3));
            p.time(m.group(4), m.group(5), m.group(6), m.group(7));
            String offset = m.group(8);
            boolean utc = "Z".equals(offset) || "-00:00".equals(offset);
            Integer offsetSeconds = null;
            if (offset != null && !utc) {
                Matcher om = OFFSET.matcher(offset);
                om.matches();
                int hh = Integer.parseInt(om.group(2)), mm = Integer.parseInt(om.group(3));
                if (mm > 59 || hh * 3600 + mm * 60 > Civil.MAX_OFFSET) p.invalid("utv.iso.offset", "offset beyond +-18:00 or malformed");
                offsetSeconds = (om.group(1).equals("-") ? -1 : 1) * (hh * 3600 + mm * 60);
            }
            String zone = null;
            Matcher am = ANNOTATION.matcher(m.group(9));
            while (am.find()) {
                String a = am.group(1);
                if (a.startsWith("!")) p.unsupported("utv.iso.critical", "critical annotations [!...] are not handled");
                else if (a.contains("=")) p.unsupported("utv.iso.annotation", "annotation [" + a + "] is not handled");
                else if (OFFSET.matcher(a).matches()) p.unsupported("utv.iso.annotation", "an offset as the zone annotation is not handled");
                else if (ZONE_NAME.matcher(a).matches() && zone == null) zone = a;
                else p.unsupported("utv.iso.annotation", "only a single [Area/Location] annotation is handled, not [" + a + "]");
            }
            if (utc && zone != null) p.unsupported("utv.iso.utc-zone", "Z or -00:00 followed by a [zone] is not handled");
            p.check();
            if (utc) {
                Rational t = Rational.of(Gregorian.epochDay(p.year, p.month, p.day).multiply(BigInteger.valueOf(86400))
                        .add(BigInteger.valueOf(p.hour * 3600L + p.minute * 60L + (p.second == null ? 0 : p.second))))
                        .add(Rational.of(BigInteger.valueOf(p.nano == null ? 0 : p.nano), Rational.BILLION));
                return Json.instant(t);
            }
            ObjectNode dt = Civil.build("datetime", p.year, p.month, p.day, p.hour, p.minute, p.second, p.nano);
            if (zone != null) {
                dt = Values.inZone(dt, zone);
                if (offsetSeconds != null) ((ObjectNode) dt.get("value")).put("offsetAssertion", offsetSeconds);
                return dt;
            }
            return offsetSeconds != null ? Values.withOffset(dt, offsetSeconds) : dt;
        }
        if (WEEK_DATE.matcher(text).matches()) throw Fail.unsupported("utv.iso.week-date", "", "week dates are not handled");
        if (ORDINAL_DATE.matcher(text).matches()) throw Fail.unsupported("utv.iso.ordinal-date", "", "ordinal dates are not handled");
        if (BASIC.matcher(text).matches()) throw Fail.unsupported("utv.iso.basic", "", "ISO 8601 basic format is not handled");
        throw Fail.invalid("utv.iso.syntax", "", "not ISO 8601 extended / RFC 3339 text: " + text);
    }

    private static BigInteger year(String s) {
        return new BigInteger(s.startsWith("+") ? s.substring(1) : s);
    }

    /** Parsed fields; out-of-range ones are invalid, unhandled valid ones unsupported. Invalid wins. */
    private static final class Parts {
        BigInteger year;
        Integer month, day, hour, minute, second, nano;
        final List<Fail> invalid = new ArrayList<>(), unsupported = new ArrayList<>();

        void invalid(String code, String msg) { invalid.add(Fail.invalid(code, "", msg)); }
        void unsupported(String code, String msg) { unsupported.add(Fail.unsupported(code, "", msg)); }

        void date(String y, String mo, String d) {
            year = year(y);
            month = Integer.parseInt(mo);
            day = Integer.parseInt(d);
            if (month >= 1 && month <= 12 && (day < 1 || day > GregorianCivil.daysInMonth(year, month))) {
                invalid("utv.civil.day", "day " + day + " does not exist in month " + month);
            }
        }

        void time(String h, String mi, String s, String frac) {
            hour = Integer.parseInt(h);
            minute = Integer.parseInt(mi);
            second = s == null ? null : Integer.parseInt(s);
            nano = frac == null ? null : Integer.parseInt((frac + "00000000").substring(0, 9));
            if (minute > 59) invalid("utv.civil.minute", "minute must be 0..59");
            if (second != null && second > 60) invalid("utv.civil.second", "second must be 0..59");
            if (second != null && second == 60) unsupported("utv.leap-second", "second 60 needs a leap-second table");
            if (hour == 24) {
                if (minute == 0 && (second == null || second == 0) && (nano == null || nano == 0)) {
                    unsupported("utv.iso.end-of-day", "24:00 needs an end-of-day policy");
                } else {
                    invalid("utv.civil.hour", "hour 24 is only 24:00");
                }
            } else if (hour > 24) {
                invalid("utv.civil.hour", "hour must be 0..23");
            }
        }

        void check() {
            if (month != null && (month < 1 || month > 12)) invalid("utv.civil.month", "month must be 1..12");
            if (!invalid.isEmpty()) throw invalid.get(0);
            if (!unsupported.isEmpty()) throw unsupported.get(0);
        }
    }

    // ======================================================================== formatIso

    /** Formats a value as ISO 8601 / RFC 3339 / RFC 9557 text; the candidate is a JSON string. */
    public static UtvResult formatIso(JsonNode value) {
        return Fail.run("formatIso", () -> UtvResult.unique("formatIso", Json.F.textNode(format(value))));
    }

    /** {@code formatIso} for display: the text, or {@code null} when the value has no ISO form. */
    public static String formatOrNull(JsonNode value) {
        return formatIso(value).value().map(JsonNode::textValue).orElse(null);
    }

    private static String format(JsonNode value) {
        Civil.structural(value, "value");
        if (Instants.isInstant(value)) {
            Rational t = Instants.decode(value, "value");
            BigInteger secs = t.floor();
            BigInteger days = Gregorian.floorDiv(secs, BigInteger.valueOf(86400));
            int sod = secs.subtract(days.multiply(BigInteger.valueOf(86400))).intValueExact();
            Gregorian.Ymd d = Gregorian.fromEpochDay(days);
            return date(d.year(), d.month(), d.day()) + "T" + two(sod / 3600) + ":" + two(sod / 60 % 60) + ":"
                    + two(sod % 60) + fraction(t.fraction()) + "Z";
        }
        if (!Civil.isCivil(value)) throw Fail.unsupported("utv.format.kind", "/kind", "only civil values and instants have an ISO form");
        Civil c = Civil.decode(value, "value");
        if (c.second() != null && c.second() == 60) throw Fail.unsupported("utv.leap-second", "/value/fields/second", "second 60 needs a leap-second table");
        if (!c.form().equals("datetime") && !c.zone().floating()) {
            throw Fail.unsupported("utv.format.zone", "/value/zone", "only a date-time can carry a zone in ISO text");
        }
        return switch (c.form()) {
            case "year-month" -> year(c.year()) + "-" + two(c.month());
            case "date" -> date(c.year(), c.month(), c.day());
            case "time" -> time(c);
            default -> date(c.year(), c.month(), c.day()) + "T" + time(c) + zone(c);
        };
    }

    private static String zone(Civil c) {
        Civil.Zone z = c.zone();
        if (z.fixed()) return offset(z.seconds());
        if (z.iana()) return (c.offsetAssertion() == null ? "" : offset(c.offsetAssertion())) + "[" + z.id() + "]";
        return "";
    }

    static String offset(int seconds) {
        if (seconds % 60 != 0) throw Fail.unsupported("utv.format.offset", "/value/zone", "an offset with seconds has no +-hh:mm form");
        int a = Math.abs(seconds);
        return (seconds < 0 ? "-" : "+") + two(a / 3600) + ":" + two(a / 60 % 60);
    }

    private static String time(Civil c) {
        String s = two(c.hour()) + ":" + two(c.minute());
        if (c.second() == null) return s;
        return s + ":" + two(c.second()) + fraction(Rational.of(BigInteger.valueOf(c.nanoOr0()), Rational.BILLION));
    }

    private static String date(BigInteger y, int m, int d) {
        return year(y) + "-" + two(m) + "-" + two(d);
    }

    static String year(BigInteger y) {
        if (y.signum() >= 0 && y.compareTo(BigInteger.valueOf(9999)) <= 0) return pad(y.toString(), 4);
        return (y.signum() < 0 ? "-" : "+") + pad(y.abs().toString(), 4);
    }

    /** ".f..." with the fewest digits, "" for zero; unsupported when the fraction has no finite decimal form. */
    private static String fraction(Rational f) {
        if (f.isZero()) return "";
        StringBuilder sb = new StringBuilder(".");
        Rational r = f;
        for (int i = 0; i < 64 && !r.isZero(); i++) {
            r = r.multiply(BigInteger.TEN);
            BigInteger digit = r.floor();
            sb.append(digit);
            r = r.fraction();
        }
        if (!r.isZero()) throw Fail.unsupported("utv.format.fraction", "/value/subTick", "fraction has no finite decimal form");
        return sb.toString();
    }

    private static String two(int n) { return n < 10 ? "0" + n : Integer.toString(n); }

    private static String pad(String s, int w) { return s.length() >= w ? s : "0".repeat(w - s.length()) + s; }
}
