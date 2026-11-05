package net.icelander.utv;

import com.fasterxml.jackson.databind.JsonNode;
import java.math.BigInteger;
import java.util.ArrayList;
import java.util.List;
import java.util.regex.Pattern;

/**
 * Semantic validation of proleptic Gregorian civil dates (forms {@code date} and {@code datetime}).
 * Years are arbitrary-precision decimal strings. Pure: no clock, zone or locale access.
 */
public final class GregorianCivil {
    private static final Pattern INTEGER = Pattern.compile("-?(0|[1-9][0-9]*)");
    private static final BigInteger B4 = BigInteger.valueOf(4), B100 = BigInteger.valueOf(100), B400 = BigInteger.valueOf(400);

    private GregorianCivil() {}

    /** Proleptic Gregorian leap-year rule for any integer year (astronomical numbering). */
    public static boolean isLeapYear(BigInteger year) {
        return year.mod(B4).signum() == 0 && (year.mod(B100).signum() != 0 || year.mod(B400).signum() == 0);
    }

    /** Days in a month (1..12) of the given proleptic Gregorian year. */
    public static int daysInMonth(BigInteger year, int month) {
        return switch (month) {
            case 2 -> isLeapYear(year) ? 29 : 28;
            case 4, 6, 9, 11 -> 30;
            default -> 31;
        };
    }

    /**
     * Structural validation followed by Gregorian date validation. Returns {@code valid},
     * {@code invalid}, or {@code unsupported} for anything other than a proleptic-Gregorian
     * civil {@code year}, {@code year-month}, {@code date} or {@code datetime}.
     */
    public static UtvResult validate(String json) {
        UtvResult structural = Utv.validateStructural(json);
        if (structural.status() != Status.VALID) return structural;
        JsonNode node;
        try {
            node = Utv.parse(json);
        } catch (java.io.IOException e) {
            throw new IllegalStateException(e);
        }
        List<Diagnostic> d = new ArrayList<>();
        JsonNode v = node.get("value");
        String form = v.path("form").asText("");
        JsonNode cal = v.path("calendar");
        if (!"civil".equals(node.get("kind").asText())
                || !(form.equals("year") || form.equals("year-month") || form.equals("date") || form.equals("datetime"))
                || !"gregory".equals(cal.path("id").asText(""))
                || !"proleptic".equals(cal.path("chronology").asText(""))) {
            d.add(new Diagnostic("utv.unsupported", "error", "", "Only proleptic Gregorian civil year, year-month, date and datetime are implemented"));
            return new UtvResult("validate", Status.UNSUPPORTED, d);
        }
        JsonNode f = v.get("fields");
        JsonNode y = f.get("year"), m = f.get("month"), dd = f.get("day");
        boolean needsMonth = !form.equals("year");
        boolean needsDay = form.equals("date") || form.equals("datetime");
        if (y == null || (needsMonth && m == null) || (needsDay && dd == null)) {
            d.add(Diagnostic.error("utv.civil.fields", "/value/fields",
                    form + " requires " + (needsDay ? "year, month and day" : needsMonth ? "year and month" : "year")));
        } else if (!y.isTextual() || !INTEGER.matcher(y.textValue()).matches()) {
            d.add(Diagnostic.error("utv.civil.year", "/value/fields/year", "year must be an integer string"));
        } else if (!needsMonth) {
            // year only: nothing more to check
        } else if (!m.isInt() || m.intValue() < 1 || m.intValue() > 12) {
            d.add(Diagnostic.error("month-out-of-range", "/value/fields/month", "month must be 1..12"));
        } else if (!needsDay) {
            // year-month: month already checked
        } else if (!dd.isInt() || dd.intValue() < 1
                || dd.intValue() > daysInMonth(new BigInteger(y.textValue()), m.intValue())) {
            d.add(Diagnostic.error("day-out-of-range", "/value/fields/day", "day does not exist in that month"));
        }
        if (form.equals("datetime") && d.isEmpty()) {
            range(f, "hour", 0, 23, d);
            range(f, "minute", 0, 59, d);
            range(f, "second", 0, 60, d); // 60 is a structurally possible leap-second label
            range(f, "nano", 0, 999_999_999, d);
        }
        return new UtvResult("validate", d.isEmpty() ? Status.VALID : Status.INVALID, d);
    }

    private static void range(JsonNode f, String name, int lo, int hi, List<Diagnostic> d) {
        JsonNode n = f.get(name);
        if (n != null && (!n.isInt() || n.intValue() < lo || n.intValue() > hi)) {
            d.add(Diagnostic.error(Civil.rangeCode(name), "/value/fields/" + name, name + " must be " + lo + ".." + hi));
        }
    }
}
