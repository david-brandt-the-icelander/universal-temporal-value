package net.icelander.utv;

import com.fasterxml.jackson.databind.JsonNode;
import com.fasterxml.jackson.databind.node.ObjectNode;
import java.time.OffsetDateTime;

/**
 * A wall time at a fixed offset from UTC, {@code 2026-11-05T14:30+01:00}. The offset pins it to
 * one instant without any zone rules. {@code +00:00} is an offset of zero, not the same as
 * {@code Z} (which parses as an {@link Instant}).
 */
public final class FixedOffsetDateTime implements TemporalValue, Comparable<FixedOffsetDateTime> {
    private final CivilDateTime dateTime;
    private final int offsetSeconds;

    private FixedOffsetDateTime(CivilDateTime dateTime, int offsetSeconds) {
        this.dateTime = dateTime;
        this.offsetSeconds = offsetSeconds;
    }

    /** Throws {@link IllegalArgumentException} for an offset beyond ±18:00. */
    public static FixedOffsetDateTime of(CivilDateTime dateTime, int offsetSeconds) {
        return Typed.argument(fromJson(Values.withOffset(dateTime.toJson(), offsetSeconds)));
    }

    /** Parses {@code 2026-11-05T14:30+01:00}. */
    public static Outcome<FixedOffsetDateTime> parse(String text) { return Typed.parse(text, FixedOffsetDateTime::fromJson); }

    /** Reads a civil {@code datetime} with a {@code fixed-offset} zone. */
    public static Outcome<FixedOffsetDateTime> fromJson(JsonNode json) {
        return Typed.decode(json, j -> {
            Civil c = Typed.civil(j, "datetime", "fixed-offset");
            return new FixedOffsetDateTime(CivilDateTime.read(c), c.zone().seconds());
        });
    }

    static FixedOffsetDateTime read(JsonNode json) { return fromJson(json).single(); }

    /** From {@code java.time}, read as a wall time at its offset. ({@code Instant.from(odt)} reads it as an instant.) */
    public static FixedOffsetDateTime from(OffsetDateTime t) {
        return read(JavaTime.fromOffsetDateTime(t, JavaTime.As.OFFSET_DATE_TIME));
    }

    public Outcome<OffsetDateTime> toOffsetDateTime() { return Typed.fromNative(JavaTime.toOffsetDateTime(toJson())); }

    @Override public ObjectNode toJson() { return Values.withOffset(dateTime.toJson(), offsetSeconds); }

    public CivilDateTime dateTime() { return dateTime; }
    public int offsetSeconds() { return offsetSeconds; }

    /** The instant this wall time names. Needs no zone rules. */
    public Instant toInstant() { return Typed.total(Zones.resolve(toJson(), null, ResolutionPolicy.STRICT), Instant::read); }

    /** Order on the timeline, so {@code 14:30+01:00} and {@code 13:30+00:00} compare as equal (but aren't {@code equals}). */
    @Override public int compareTo(FixedOffsetDateTime other) { return Typed.order(Operations.compare(toJson(), other.toJson())); }

    @Override public boolean equals(Object o) {
        return o instanceof FixedOffsetDateTime f && dateTime.equals(f.dateTime) && offsetSeconds == f.offsetSeconds;
    }

    @Override public int hashCode() { return dateTime.hashCode() * 31 + offsetSeconds; }

    /** The text form, {@code 2026-11-05T14:30+01:00}. */
    @Override public String toString() { return Typed.text(this); }
}
