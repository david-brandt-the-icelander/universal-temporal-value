package net.icelander.utv;

import com.fasterxml.jackson.databind.JsonNode;
import com.fasterxml.jackson.databind.node.ObjectNode;
import java.time.LocalDateTime;

/**
 * A date and a time of day with no zone, {@code 2026-11-05T14:30}: a "floating" local date-time.
 * It isn't a point on the timeline until you bind a zone ({@link #inZone}) or an offset
 * ({@link #atOffset}). Arithmetic is wall-clock arithmetic, carrying into days.
 */
public final class CivilDateTime implements TemporalValue, Comparable<CivilDateTime> {
    private final CivilDate date;
    private final CivilTime time;

    private CivilDateTime(CivilDate date, CivilTime time) {
        this.date = date;
        this.time = time;
    }

    public static CivilDateTime of(CivilDate date, CivilTime time) { return new CivilDateTime(date, time); }

    public static CivilDateTime of(long year, int month, int day, int hour, int minute) {
        return of(CivilDate.of(year, month, day), CivilTime.of(hour, minute));
    }

    public static CivilDateTime of(long year, int month, int day, int hour, int minute, int second) {
        return of(CivilDate.of(year, month, day), CivilTime.of(hour, minute, second));
    }

    public static CivilDateTime of(long year, int month, int day, int hour, int minute, int second, int nanosecond) {
        return of(CivilDate.of(year, month, day), CivilTime.of(hour, minute, second, nanosecond));
    }

    /** The date-time, or {@code invalid} / {@code unsupported} with the reason. */
    public static Outcome<CivilDateTime> tryOf(long year, int month, int day, int hour, int minute, int second, int nanosecond) {
        return fromJson(Values.dateTime(year, month, day, hour, minute, second, nanosecond));
    }

    /** Parses {@code 2026-11-05T14:30}. */
    public static Outcome<CivilDateTime> parse(String text) { return Typed.parse(text, CivilDateTime::fromJson); }

    /** Reads a civil {@code datetime} without a zone. */
    public static Outcome<CivilDateTime> fromJson(JsonNode json) {
        return Typed.decode(json, j -> read(Typed.civil(j, "datetime", "floating")));
    }

    static CivilDateTime read(Civil c) {
        return new CivilDateTime(CivilDate.of(c.year(), c.month(), c.day()), CivilTime.read(c));
    }

    static CivilDateTime read(JsonNode json) { return fromJson(json).single(); }

    public static CivilDateTime from(LocalDateTime t) { return of(CivilDate.from(t.toLocalDate()), CivilTime.from(t.toLocalTime())); }

    public Outcome<LocalDateTime> toLocalDateTime() { return Typed.fromNative(JavaTime.toLocalDateTime(toJson())); }

    @Override public ObjectNode toJson() {
        return Civil.build("datetime", date.year(), date.month(), date.day(), time.hour(), time.minute(), time.secondOrNull(), time.nanosecond());
    }

    public CivilDate date() { return date; }
    public CivilTime time() { return time; }

    // ---------------------------------------------------------------- arithmetic (wall clock)

    public CivilDateTime addDays(long n) { return plus(Values.days(n)); }
    public CivilDateTime addWeeks(long n) { return plus(Values.weeks(n)); }
    public CivilDateTime addHours(long n) { return plus(Values.hours(n)); }
    public CivilDateTime addMinutes(long n) { return plus(Values.minutes(n)); }
    public CivilDateTime addSeconds(long n) { return plus(Values.seconds(n)); }

    /** {@code none} ({@code month-end}) when the day doesn't exist in the resulting month. */
    public Outcome<CivilDateTime> addMonths(long months) { return add(Period.months(months)); }

    /** Clamps to the month's last day; {@code policy} must be {@link MonthEnd#CONSTRAIN}. */
    public CivilDateTime addMonths(long months, MonthEnd policy) {
        if (policy != MonthEnd.CONSTRAIN) {
            throw new IllegalArgumentException("only MonthEnd.CONSTRAIN always has an answer; addMonths(n) returns an Outcome");
        }
        return Typed.total(Operations.add(toJson(), Values.months(months), MonthEnd.CONSTRAIN), CivilDateTime::read);
    }

    /** Every component, wall clock; {@code none} ({@code month-end}) when an intermediate day doesn't exist. */
    public Outcome<CivilDateTime> add(Period period) {
        return Typed.outcome(Operations.add(toJson(), period.toJson()), CivilDateTime::read);
    }

    private CivilDateTime plus(ObjectNode amount) { return Typed.total(Operations.add(toJson(), amount), CivilDateTime::read); }

    // ---------------------------------------------------------------- zones

    /** This wall time in a named zone: an intention, resolved to an instant later with zone rules. */
    public ZonedIntent inZone(String zoneId) { return ZonedIntent.of(this, zoneId); }

    /** This wall time at a fixed offset from UTC, in seconds (within ±18:00). */
    public FixedOffsetDateTime atOffset(int offsetSeconds) { return FixedOffsetDateTime.of(this, offsetSeconds); }

    // ---------------------------------------------------------------- comparison, equality, text

    /** Wall-clock order; a missing second counts as 0. */
    @Override public int compareTo(CivilDateTime other) { return Typed.order(Operations.compare(toJson(), other.toJson())); }

    @Override public boolean equals(Object o) { return o instanceof CivilDateTime d && date.equals(d.date) && time.equals(d.time); }

    @Override public int hashCode() { return date.hashCode() * 31 + time.hashCode(); }

    /** The text form, {@code 2026-11-05T14:30}. */
    @Override public String toString() { return Typed.text(this); }
}
