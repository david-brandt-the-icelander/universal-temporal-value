package net.icelander.utv;

import com.fasterxml.jackson.databind.JsonNode;
import com.fasterxml.jackson.databind.node.ObjectNode;
import java.math.BigInteger;
import java.time.LocalDate;

/**
 * A day in the proleptic Gregorian calendar, with no time and no zone: {@code 2026-11-05}.
 * Valid by construction ({@code CivilDate.of(2026, 2, 30)} throws); years are unbounded.
 *
 * <p>Adding days or weeks always works and returns a {@code CivilDate}. Adding months or years can
 * land on a day that doesn't exist (31 January + 1 month), so {@link #addMonths(long)} returns an
 * {@link Outcome}; {@link #addMonths(long, MonthEnd)} with {@link MonthEnd#CONSTRAIN} clamps instead.
 */
public final class CivilDate implements IntervalPoint<CivilDate> {
    private final BigInteger year;
    private final int month, day;

    private CivilDate(BigInteger year, int month, int day) {
        this.year = year;
        this.month = month;
        this.day = day;
    }

    // ---------------------------------------------------------------- construction

    /** The date; throws {@link IllegalArgumentException} for a day that doesn't exist. */
    public static CivilDate of(long year, int month, int day) { return Typed.argument(tryOf(year, month, day)); }

    /** The date; throws {@link IllegalArgumentException} for a day that doesn't exist. */
    public static CivilDate of(BigInteger year, int month, int day) { return Typed.argument(tryOf(year, month, day)); }

    /** The date, or {@code invalid} ({@code month-out-of-range}, {@code day-out-of-range}). */
    public static Outcome<CivilDate> tryOf(long year, int month, int day) { return tryOf(BigInteger.valueOf(year), month, day); }

    /** The date, or {@code invalid} ({@code month-out-of-range}, {@code day-out-of-range}). */
    public static Outcome<CivilDate> tryOf(BigInteger year, int month, int day) { return fromJson(Values.date(year, month, day)); }

    /** Parses {@code 2026-11-05}. */
    public static Outcome<CivilDate> parse(String text) { return Typed.parse(text, CivilDate::fromJson); }

    /** Reads a civil {@code date} value; other kinds and forms are {@code invalid} ({@code kind-mismatch}). */
    public static Outcome<CivilDate> fromJson(JsonNode json) {
        return Typed.decode(json, j -> {
            Civil c = Typed.civil(j, "date", "floating");
            return new CivilDate(c.year(), c.month(), c.day());
        });
    }

    /** From {@code java.time}: always exact. */
    public static CivilDate from(LocalDate date) { return of(date.getYear(), date.getMonthValue(), date.getDayOfMonth()); }

    /** To {@code java.time}: {@code unsupported} ({@code out-of-native-range}) beyond {@code LocalDate}'s years. */
    public Outcome<LocalDate> toLocalDate() { return Typed.fromNative(JavaTime.toLocalDate(toJson())); }

    static CivilDate read(JsonNode json) { return fromJson(json).single(); }

    @Override public ObjectNode toJson() { return Values.date(year, month, day); }

    // ---------------------------------------------------------------- properties

    public BigInteger year() { return year; }
    public int month() { return month; }
    public int day() { return day; }

    public Weekday dayOfWeek() { return Weekday.of(fields().get("dayOfWeek").intValue()); }
    public int dayOfYear() { return fields().get("dayOfYear").intValue(); }
    public int isoWeek() { return fields().get("isoWeek").intValue(); }
    public BigInteger isoWeekYear() { return new BigInteger(fields().get("isoWeekYear").textValue()); }
    public int daysInMonth() { return fields().get("daysInMonth").intValue(); }
    public boolean isLeapYear() { return fields().get("isLeapYear").booleanValue(); }

    private JsonNode fields() { return Typed.total(Operations.fields(toJson()), j -> j); }

    // ---------------------------------------------------------------- arithmetic

    public CivilDate addDays(long days) { return Typed.total(Operations.add(toJson(), Values.days(days)), CivilDate::read); }

    public CivilDate addWeeks(long weeks) { return Typed.total(Operations.add(toJson(), Values.weeks(weeks)), CivilDate::read); }

    /** {@code none} ({@code month-end}) when the day doesn't exist in the resulting month. */
    public Outcome<CivilDate> addMonths(long months) { return add(Period.months(months)); }

    /** Clamps to the month's last day; {@code policy} must be {@link MonthEnd#CONSTRAIN} (use {@link #addMonths(long)} to reject). */
    public CivilDate addMonths(long months, MonthEnd policy) { return constrained(Values.months(months), policy); }

    /** {@code none} ({@code month-end}) from 29 February into a common year. */
    public Outcome<CivilDate> addYears(long years) { return add(Period.years(years)); }

    /** Clamps 29 February to 28 February; {@code policy} must be {@link MonthEnd#CONSTRAIN}. */
    public CivilDate addYears(long years, MonthEnd policy) { return constrained(Values.years(years), policy); }

    /**
     * Years and months first, then weeks and days. {@code none} ({@code month-end}) when the
     * intermediate day doesn't exist; {@code invalid} ({@code kind-mismatch}) for hours, minutes or seconds.
     */
    public Outcome<CivilDate> add(Period period) { return Typed.outcome(Operations.add(toJson(), period.toJson()), CivilDate::read); }

    private CivilDate constrained(ObjectNode amount, MonthEnd policy) {
        if (policy != MonthEnd.CONSTRAIN) {
            throw new IllegalArgumentException("only MonthEnd.CONSTRAIN always has an answer; addMonths(n) / addYears(n) return an Outcome");
        }
        return Typed.total(Operations.add(toJson(), amount, MonthEnd.CONSTRAIN), CivilDate::read);
    }

    /** Days from this date to {@code other} (negative when other is earlier). */
    public long daysUntil(CivilDate other) {
        return Typed.total(Operations.daysBetween(toJson(), other.toJson()), JsonNode::longValue);
    }

    /** This date at a time of day. */
    public CivilDateTime at(CivilTime time) { return CivilDateTime.of(this, time); }

    // ---------------------------------------------------------------- comparison, equality, text

    @Override public int compareTo(CivilDate other) { return Typed.order(Operations.compare(toJson(), other.toJson())); }

    public boolean isBefore(CivilDate other) { return compareTo(other) < 0; }

    public boolean isAfter(CivilDate other) { return compareTo(other) > 0; }

    @Override public boolean equals(Object o) {
        return o instanceof CivilDate d && year.equals(d.year) && month == d.month && day == d.day;
    }

    @Override public int hashCode() { return (year.hashCode() * 31 + month) * 31 + day; }

    /** The text form, {@code 2026-11-05}. */
    @Override public String toString() { return Typed.text(this); }
}
