package net.icelander.utv;

import com.fasterxml.jackson.databind.JsonNode;
import com.fasterxml.jackson.databind.node.ObjectNode;
import java.time.LocalTime;

/**
 * A time of day, {@code 09:30} or {@code 09:30:15.25}. It remembers whether a second was given:
 * {@code 09:30} and {@code 09:30:00} are different values that compare as equal. There is no
 * arithmetic, because wrapping past midnight loses the day; use a {@link CivilDateTime}.
 */
public final class CivilTime implements TemporalValue, Comparable<CivilTime> {
    private final int hour, minute, nanosecond;
    private final Integer second;

    private CivilTime(int hour, int minute, Integer second, int nanosecond) {
        this.hour = hour;
        this.minute = minute;
        this.second = second;
        this.nanosecond = nanosecond;
    }

    /** {@code 09:30}; throws {@link IllegalArgumentException} out of range. */
    public static CivilTime of(int hour, int minute) { return Typed.argument(tryOf(hour, minute)); }

    /** {@code 09:30:15}; throws {@link IllegalArgumentException} out of range (second 60 included). */
    public static CivilTime of(int hour, int minute, int second) { return Typed.argument(tryOf(hour, minute, second)); }

    public static CivilTime of(int hour, int minute, int second, int nanosecond) {
        return Typed.argument(tryOf(hour, minute, second, nanosecond));
    }

    public static Outcome<CivilTime> tryOf(int hour, int minute) { return fromJson(Values.time(hour, minute)); }

    public static Outcome<CivilTime> tryOf(int hour, int minute, int second) { return fromJson(Values.time(hour, minute, second)); }

    /** The time, or {@code invalid} ({@code field-out-of-range}), or {@code unsupported} for second 60 ({@code leap-second}). */
    public static Outcome<CivilTime> tryOf(int hour, int minute, int second, int nanosecond) {
        return fromJson(Values.time(hour, minute, second, nanosecond));
    }

    /** Parses {@code 09:30}, {@code 09:30:15.25}. */
    public static Outcome<CivilTime> parse(String text) { return Typed.parse(text, CivilTime::fromJson); }

    /** Reads a civil {@code time} value. */
    public static Outcome<CivilTime> fromJson(JsonNode json) {
        return Typed.decode(json, j -> read(Typed.civil(j, "time", "floating")));
    }

    static CivilTime read(Civil c) { return new CivilTime(c.hour(), c.minute(), c.second(), c.nanoOr0()); }

    /** From {@code java.time}; the second is always present, because {@code LocalTime} always has one. */
    public static CivilTime from(LocalTime t) { return of(t.getHour(), t.getMinute(), t.getSecond(), t.getNano()); }

    public Outcome<LocalTime> toLocalTime() { return Typed.fromNative(JavaTime.toLocalTime(toJson())); }

    @Override public ObjectNode toJson() { return Civil.build("time", null, null, null, hour, minute, second, nanosecond); }

    public int hour() { return hour; }
    public int minute() { return minute; }
    /** The second, 0 when none was given. */
    public int second() { return second == null ? 0 : second; }
    public int nanosecond() { return nanosecond; }
    /** Whether a second was given ({@code 09:30:00}) or not ({@code 09:30}). */
    public boolean hasSecond() { return second != null; }

    Integer secondOrNull() { return second; }

    /** Time-of-day order; a missing second counts as 0, so this is not consistent with {@code equals}. */
    @Override public int compareTo(CivilTime other) { return Typed.order(Operations.compare(toJson(), other.toJson())); }

    @Override public boolean equals(Object o) {
        return o instanceof CivilTime t && hour == t.hour && minute == t.minute && nanosecond == t.nanosecond
                && java.util.Objects.equals(second, t.second);
    }

    @Override public int hashCode() { return java.util.Objects.hash(hour, minute, second, nanosecond); }

    /** The text form, {@code 09:30}. */
    @Override public String toString() { return Typed.text(this); }
}
