package net.icelander.utv;

import com.fasterxml.jackson.databind.JsonNode;
import com.fasterxml.jackson.databind.node.ObjectNode;
import java.math.BigInteger;
import java.time.DateTimeException;
import java.time.Duration;
import java.time.Instant;
import java.time.LocalDate;
import java.time.LocalDateTime;
import java.time.LocalTime;
import java.time.OffsetDateTime;
import java.time.Period;
import java.time.ZoneId;
import java.time.ZoneOffset;
import java.time.ZonedDateTime;
import java.util.Date;
import java.util.List;
import java.util.Map;
import java.util.function.Supplier;

/**
 * Explicit adapters between UTV values and {@code java.time}.
 *
 * <ul>
 *   <li><b>The caller names the intent</b> where a native value has more than one reading: an
 *       {@link OffsetDateTime} becomes an instant or a fixed-offset date-time only as asked
 *       ({@link As}).
 *   <li><b>No local zone.</b> Nothing here calls {@code ZoneId.systemDefault()}, the clock or the
 *       locale. A native value whose civil reading depends on the host's default zone
 *       ({@link java.util.Date}, and {@code java.sql.Timestamp} through it) is refused with
 *       {@code unsupported} for anything but its instant.
 *   <li><b>Lossy conversions back say so</b> through {@link NativeResult#lossy()}, or are refused.
 * </ul>
 *
 * <p>{@link #toZonedDateTime} is the one adapter that uses the JDK's bundled tz database, because
 * {@link ZonedDateTime} cannot exist without it; it checks the value's asserted offset against it.
 */
public final class JavaTime {
    /** How to read a native value that has more than one meaning. */
    public enum As {
        /** A point on the timeline (UTV {@code instant}); the local offset is dropped. */
        INSTANT,
        /** A local date-time at a fixed UTC offset (UTV {@code withOffset}). */
        OFFSET_DATE_TIME,
        /** The local date-time alone, offset deliberately dropped (UTV floating {@code dateTime}). */
        LOCAL_DATE_TIME
    }

    private JavaTime() {}

    // ======================================================================== java.time -> UTV

    public static ObjectNode fromLocalDate(LocalDate d) {
        return Values.date(d.getYear(), d.getMonthValue(), d.getDayOfMonth());
    }

    /** A UTV time; {@code second} is always present (java.time always has it), {@code nano} only if non-zero. */
    public static ObjectNode fromLocalTime(LocalTime t) {
        return Values.time(t.getHour(), t.getMinute(), t.getSecond(), t.getNano());
    }

    /** A floating (unzoned) UTV date-time. */
    public static ObjectNode fromLocalDateTime(LocalDateTime t) {
        return Values.dateTime(t.getYear(), t.getMonthValue(), t.getDayOfMonth(), t.getHour(), t.getMinute(), t.getSecond(), t.getNano());
    }

    public static ObjectNode fromInstant(Instant i) {
        return Values.instant(i.getEpochSecond(), i.getNano());
    }

    /** An {@link OffsetDateTime} read as the caller says: an instant, an offset date-time or a local date-time. */
    public static ObjectNode fromOffsetDateTime(OffsetDateTime t, As as) {
        return switch (as) {
            case INSTANT -> fromInstant(t.toInstant());
            case OFFSET_DATE_TIME -> Values.withOffset(fromLocalDateTime(t.toLocalDateTime()), t.getOffset().getTotalSeconds());
            case LOCAL_DATE_TIME -> fromLocalDateTime(t.toLocalDateTime());
        };
    }

    /**
     * A {@link ZonedDateTime} as a zoned intention ({@code inZone}) that keeps its offset as
     * {@code offsetAssertion}. A zone that is only a fixed offset ({@code +01:00},
     * {@code UTC+01:00}) becomes a fixed-offset date-time instead.
     */
    public static ObjectNode fromZonedDateTime(ZonedDateTime t) {
        ObjectNode local = fromLocalDateTime(t.toLocalDateTime());
        ZoneId z = t.getZone();
        if (z instanceof ZoneOffset || z.getId().matches("(UTC|GMT|UT)[+-].*")) {
            return Values.withOffset(local, t.getOffset().getTotalSeconds());
        }
        ObjectNode v = Values.inZone(local, z.getId());
        ((ObjectNode) v.get("value")).put("offsetAssertion", t.getOffset().getTotalSeconds());
        return v;
    }

    /**
     * A legacy {@link Date} (or {@code java.sql.Timestamp}) read as the caller says. Only
     * {@link As#INSTANT} is accepted: its civil fields come from the host's default zone
     * ({@code ZoneId.systemDefault()}), so any civil reading is refused with {@code unsupported}.
     */
    public static UtvResult fromDate(Date d, As as) {
        if (as == As.INSTANT) return UtvResult.unique("fromDate", fromInstant(d.toInstant()));
        return UtvResult.of("fromDate", Status.UNSUPPORTED, new Diagnostic("local-zone", "error", "",
                "java.util.Date has no zone of its own: its date and time fields come from ZoneId.systemDefault(). "
                        + "Read it As.INSTANT, then pick a zone explicitly."));
    }

    /** A calendar amount (years, months, days). */
    public static ObjectNode fromPeriod(Period p) {
        return Values.period(p.getYears(), p.getMonths(), 0, p.getDays(), 0, 0, 0);
    }

    /** A posixSeconds amount: {@link Duration} counts seconds the way {@link Instant} does, without leap seconds. */
    public static ObjectNode fromDuration(Duration d) {
        return Json.posixSeconds(Rational.of(d.getSeconds()).add(Rational.of(BigInteger.valueOf(d.getNano()), Rational.BILLION)));
    }

    // ======================================================================== UTV -> java.time

    public static NativeResult<LocalDate> toLocalDate(JsonNode v) {
        return convert(() -> {
            Civil c = civil(v, "date");
            LocalDate d = LocalDate.of(year(c), c.month(), c.day());
            return c.zone().floating() ? NativeResult.exact(d) : NativeResult.lossy(d, "/value/zone", "the zone was dropped");
        });
    }

    public static NativeResult<LocalTime> toLocalTime(JsonNode v) {
        return convert(() -> {
            Civil c = civil(v, "time");
            noLeapSecond(c);
            LocalTime t = LocalTime.of(c.hour(), c.minute(), c.secondOr0(), c.nanoOr0());
            return c.zone().floating() ? NativeResult.exact(t) : NativeResult.lossy(t, "/value/zone", "the zone was dropped");
        });
    }

    /** A date-time's local fields. A zone or offset on the value is dropped and reported as a loss. */
    public static NativeResult<LocalDateTime> toLocalDateTime(JsonNode v) {
        return convert(() -> {
            Civil c = civil(v, "datetime");
            LocalDateTime t = local(c);
            return c.zone().floating() ? NativeResult.exact(t)
                    : NativeResult.lossy(t, "/value/zone", "the " + (c.zone().iana() ? "zone " + c.zone().id() : "offset") + " was dropped");
        });
    }

    /**
     * A fixed-offset date-time converts exactly. A zoned intention converts only when it carries an
     * {@code offsetAssertion}, and then reports the loss of its zone id. Anything else is refused.
     */
    public static NativeResult<OffsetDateTime> toOffsetDateTime(JsonNode v) {
        return convert(() -> {
            Civil c = civil(v, "datetime");
            LocalDateTime t = local(c);
            if (c.zone().fixed()) return NativeResult.exact(OffsetDateTime.of(t, ZoneOffset.ofTotalSeconds(c.zone().seconds())));
            if (c.zone().iana()) {
                if (c.offsetAssertion() == null) {
                    throw Fail.unsupported("unsupported-operand", "/value/offsetAssertion", "a zoned intention has no offset yet: resolve it or use toZone first");
                }
                return NativeResult.lossy(OffsetDateTime.of(t, ZoneOffset.ofTotalSeconds(c.offsetAssertion())), "/value/zone",
                        "OffsetDateTime keeps only the offset: the zone " + c.zone().id() + " (and following its future rule changes) was dropped");
            }
            throw Fail.unsupported("unsupported-operand", "/value/zone", "a floating date-time has no offset; use toLocalDateTime");
        });
    }

    /**
     * A zoned intention with an {@code offsetAssertion} (as made by {@link #fromZonedDateTime} or
     * {@link Zones#toZone}) or a fixed-offset date-time. The asserted offset is checked against the
     * JDK's tz database ({@link ZonedDateTime#ofStrict}); without one the value is refused, because
     * choosing an offset in a gap or overlap is {@link Zones#resolve}'s job.
     */
    public static NativeResult<ZonedDateTime> toZonedDateTime(JsonNode v) {
        return convert(() -> {
            Civil c = civil(v, "datetime");
            LocalDateTime t = local(c);
            if (c.zone().fixed()) return NativeResult.exact(ZonedDateTime.of(t, ZoneOffset.ofTotalSeconds(c.zone().seconds())));
            if (!c.zone().iana()) throw Fail.unsupported("unsupported-operand", "/value/zone", "a floating date-time has no zone; use toLocalDateTime");
            if (c.offsetAssertion() == null) {
                throw Fail.unsupported("unsupported-operand", "/value/offsetAssertion", "no offsetAssertion: resolve the value (or use toZone) first");
            }
            ZoneId zone;
            try {
                zone = ZoneId.of(c.zone().id());
            } catch (DateTimeException e) {
                throw Fail.unsupported("unknown-zone", "/value/zone/id", "the JDK's tz database has no zone " + c.zone().id());
            }
            try {
                return NativeResult.exact(ZonedDateTime.ofStrict(t, ZoneOffset.ofTotalSeconds(c.offsetAssertion()), zone));
            } catch (DateTimeException e) {
                throw Fail.invalid("offset-mismatch", "/value/offsetAssertion",
                        "the JDK's tz database says the offset " + Iso.offset(c.offsetAssertion()) + " is not valid in " + zone + " at that time");
            }
        });
    }

    /** A POSIX instant. A fraction finer than a nanosecond is truncated toward the past and reported as a loss. */
    public static NativeResult<Instant> toInstant(JsonNode v) {
        return convert(() -> {
            Rational t = Instants.decode(v, "value");
            return instant(t, "/value/subTick");
        });
    }

    /** A calendar amount with only years, months, weeks and days (weeks become 7 days, as in {@link Period#ofWeeks}). */
    public static NativeResult<Period> toPeriod(JsonNode v) {
        return convert(() -> {
            Civil.structural(v, "amount");
            JsonNode a = v.get("value");
            if (!"amount".equals(v.path("kind").asText(null)) || !"calendar".equals(a.path("basis").asText(null))) {
                throw Fail.unsupported("unsupported-operand", "/value/basis", "a Period holds only a calendar amount");
            }
            Map<String, BigInteger> k = new java.util.HashMap<>();
            for (String n : List.of("years", "months", "weeks", "days", "hours", "minutes", "seconds")) {
                JsonNode x = a.path("components").get(n);
                k.put(n, x == null ? BigInteger.ZERO : Instants.integer(x, "/value/components/" + n));
            }
            for (String n : List.of("hours", "minutes", "seconds")) {
                if (k.get(n).signum() != 0) throw Fail.unsupported("unsupported-operand", "/value/components/" + n, "a Period cannot hold " + n);
            }
            try {
                return NativeResult.exact(Period.of(k.get("years").intValueExact(), k.get("months").intValueExact(),
                        k.get("weeks").multiply(BigInteger.valueOf(7)).add(k.get("days")).intValueExact()));
            } catch (ArithmeticException e) {
                throw Fail.unsupported("out-of-native-range", "/value/components", "a component is outside Period's int range");
            }
        });
    }

    /** A posixSeconds amount. SI {@code elapsed} seconds are refused; sub-nanosecond fractions are truncated and reported. */
    public static NativeResult<Duration> toDuration(JsonNode v) {
        return convert(() -> {
            Rational s = Instants.posixAmount(v, "amount");
            BigInteger nanos = s.multiply(Rational.BILLION).floor();
            try {
                Duration d = Duration.ofSeconds(Gregorian.floorDiv(nanos, Rational.BILLION).longValueExact(),
                        nanos.mod(Rational.BILLION).longValueExact());
                return s.multiply(Rational.BILLION).isInteger() ? NativeResult.exact(d)
                        : NativeResult.lossy(d, "/value/seconds", "a fraction finer than a nanosecond was truncated");
            } catch (ArithmeticException e) {
                throw Fail.unsupported("out-of-native-range", "/value/seconds", "outside Duration's range");
            }
        });
    }

    // ======================================================================== helpers

    private static <T> NativeResult<T> convert(Supplier<NativeResult<T>> body) {
        try {
            return body.get();
        } catch (Fail f) {
            return NativeResult.failed(f);
        }
    }

    private static Civil civil(JsonNode v, String form) {
        Civil c = Civil.decode(v, "value");
        if (!c.form().equals(form)) throw Fail.unsupported("unsupported-operand", "/value/form", "expected a " + form + ", got a " + c.form());
        return c;
    }

    private static int year(Civil c) {
        if (c.year().compareTo(BigInteger.valueOf(LocalDate.MIN.getYear())) < 0 || c.year().compareTo(BigInteger.valueOf(LocalDate.MAX.getYear())) > 0) {
            throw Fail.unsupported("out-of-native-range", "/value/fields/year", "year " + c.year() + " is outside java.time's range");
        }
        return c.year().intValue();
    }

    private static void noLeapSecond(Civil c) {
        if (c.secondOr0() == 60) throw Fail.unsupported("leap-second", "/value/fields/second", "java.time cannot hold second 60");
    }

    private static LocalDateTime local(Civil c) {
        noLeapSecond(c);
        return LocalDateTime.of(year(c), c.month(), c.day(), c.hour(), c.minute(), c.secondOr0(), c.nanoOr0());
    }

    private static NativeResult<Instant> instant(Rational t, String path) {
        BigInteger nanos = t.multiply(Rational.BILLION).floor();
        BigInteger secs = Gregorian.floorDiv(nanos, Rational.BILLION);
        if (secs.compareTo(BigInteger.valueOf(Instant.MIN.getEpochSecond())) < 0 || secs.compareTo(BigInteger.valueOf(Instant.MAX.getEpochSecond())) > 0) {
            throw Fail.unsupported("out-of-native-range", "/value/ticks", "outside java.time.Instant's range");
        }
        Instant i = Instant.ofEpochSecond(secs.longValue(), nanos.subtract(secs.multiply(Rational.BILLION)).longValue());
        return t.multiply(Rational.BILLION).isInteger() ? NativeResult.exact(i)
                : NativeResult.lossy(i, path, "a fraction finer than a nanosecond was truncated");
    }
}
