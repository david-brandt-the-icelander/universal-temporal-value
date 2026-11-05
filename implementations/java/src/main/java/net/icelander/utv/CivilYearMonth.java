package net.icelander.utv;

import com.fasterxml.jackson.databind.JsonNode;
import com.fasterxml.jackson.databind.node.ObjectNode;
import java.math.BigInteger;

/** A month of a proleptic Gregorian year, {@code 2026-11}. Month arithmetic always has an answer. */
public final class CivilYearMonth implements TemporalValue, Comparable<CivilYearMonth> {
    private final BigInteger year;
    private final int month;

    private CivilYearMonth(BigInteger year, int month) {
        this.year = year;
        this.month = month;
    }

    /** The month; throws {@link IllegalArgumentException} for a month outside 1..12. */
    public static CivilYearMonth of(long year, int month) { return Typed.argument(tryOf(year, month)); }

    /** The month, or {@code invalid} ({@code month-out-of-range}). */
    public static Outcome<CivilYearMonth> tryOf(long year, int month) { return fromJson(Values.yearMonth(year, month)); }

    /** Parses {@code 2026-11}. */
    public static Outcome<CivilYearMonth> parse(String text) { return Typed.parse(text, CivilYearMonth::fromJson); }

    /** Reads a civil {@code year-month} value. */
    public static Outcome<CivilYearMonth> fromJson(JsonNode json) {
        return Typed.decode(json, j -> {
            Civil c = Typed.civil(j, "year-month", "floating");
            return new CivilYearMonth(c.year(), c.month());
        });
    }

    static CivilYearMonth read(JsonNode json) { return fromJson(json).single(); }

    @Override public ObjectNode toJson() { return Civil.build("year-month", year, month, null, null, null, null, null); }

    public BigInteger year() { return year; }
    public int month() { return month; }

    public CivilYearMonth addMonths(long months) { return Typed.total(Operations.add(toJson(), Values.months(months)), CivilYearMonth::read); }

    public CivilYearMonth addYears(long years) { return Typed.total(Operations.add(toJson(), Values.years(years)), CivilYearMonth::read); }

    @Override public int compareTo(CivilYearMonth other) { return Typed.order(Operations.compare(toJson(), other.toJson())); }

    @Override public boolean equals(Object o) { return o instanceof CivilYearMonth m && year.equals(m.year) && month == m.month; }

    @Override public int hashCode() { return year.hashCode() * 31 + month; }

    /** The text form, {@code 2026-11}. */
    @Override public String toString() { return Typed.text(this); }
}
