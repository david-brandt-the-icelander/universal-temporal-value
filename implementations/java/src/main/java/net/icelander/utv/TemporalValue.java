package net.icelander.utv;

import com.fasterxml.jackson.databind.JsonNode;
import com.fasterxml.jackson.databind.node.ObjectNode;

/**
 * Any typed UTV value. Each one is exactly one UTV JSON value: {@link #toJson()} gives it, and
 * {@link #fromJson} reads it back. {@code toString()} is the text form ({@code docs/text-form.md}).
 *
 * <p>Use a pattern {@code switch} to find out what a parsed value is:
 *
 * <pre>{@code
 * switch (TemporalValue.parse("2026-11-05T13:30:00Z").single()) {
 *     case Instant i -> ...;
 *     case FixedOffsetDateTime f -> ...;
 *     default -> ...;
 * }
 * }</pre>
 */
public sealed interface TemporalValue
        permits IntervalPoint, CivilYearMonth, CivilTime, CivilDateTime, FixedOffsetDateTime, ZonedIntent,
                Period, PosixSeconds, ElapsedSeconds, Interval {

    /** The UTV JSON for this value (a fresh copy). */
    ObjectNode toJson();

    /** Parses any value that has a text form: a date, a time, an instant, a period, an interval… */
    static Outcome<TemporalValue> parse(String text) {
        return Typed.parse(text, TemporalValue::fromJson);
    }

    /** Reads any UTV JSON value that has a typed form; others are {@code unsupported}. */
    static Outcome<TemporalValue> fromJson(JsonNode json) {
        return Typed.anyFromJson(json);
    }
}
