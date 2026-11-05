package net.icelander.utv;

import com.fasterxml.jackson.databind.JsonNode;
import com.fasterxml.jackson.databind.node.ObjectNode;
import java.time.ZoneOffset;
import java.time.ZonedDateTime;
import java.util.Objects;
import java.util.OptionalInt;

/**
 * A wall time in a named zone, {@code 2027-06-01T09:00[Europe/Paris]}: "09:00 in Paris, whatever
 * Paris's rules are then". It is an intention, not an instant: {@link #resolve} turns it into
 * instants with explicit zone rules, and can answer none (a DST gap) or two (an overlap).
 *
 * <p>It may carry an asserted offset ({@code 2027-06-01T09:00+02:00[Europe/Paris]}), which
 * {@code resolve} checks against the rules. There are no comparison operators: the order of two
 * zoned values depends on zone rules, so use {@link TemporalContext#compare}.
 */
public final class ZonedIntent implements TemporalValue {
    private final CivilDateTime dateTime;
    private final String zoneId;
    private final Integer assertedOffset;

    private ZonedIntent(CivilDateTime dateTime, String zoneId, Integer assertedOffset) {
        this.dateTime = dateTime;
        this.zoneId = zoneId;
        this.assertedOffset = assertedOffset;
    }

    /** Throws {@link IllegalArgumentException} for a zone id that isn't {@code Area/Location}-shaped. */
    public static ZonedIntent of(CivilDateTime dateTime, String zoneId) {
        if (zoneId == null || !Iso.ZONE_NAME.matcher(zoneId).matches()) throw new IllegalArgumentException("not a zone id: " + zoneId);
        return new ZonedIntent(dateTime, zoneId, null);
    }

    /** Parses {@code 2027-06-01T09:00[Europe/Paris]} or {@code 2027-06-01T09:00+02:00[Europe/Paris]}. */
    public static Outcome<ZonedIntent> parse(String text) { return Typed.parse(text, ZonedIntent::fromJson); }

    /** Reads a civil {@code datetime} in an {@code iana} zone, with an optional {@code offsetAssertion}. */
    public static Outcome<ZonedIntent> fromJson(JsonNode json) {
        return Typed.decode(json, j -> {
            Civil c = Typed.civil(j, "datetime", "iana");
            if (!Iso.ZONE_NAME.matcher(c.zone().id()).matches()) throw Fail.unsupported("unsupported-operand", "/value/zone/id", "zone id " + c.zone().id());
            return new ZonedIntent(CivilDateTime.read(c), c.zone().id(), c.offsetAssertion());
        });
    }

    static ZonedIntent read(JsonNode json) { return fromJson(json).single(); }

    /**
     * From {@code java.time}: the wall time and zone, with the offset {@code java.time} chose as the
     * asserted offset. A {@code ZonedDateTime} whose zone is only an offset is refused: use
     * {@link FixedOffsetDateTime#from}.
     */
    public static ZonedIntent from(ZonedDateTime t) {
        if (t.getZone() instanceof ZoneOffset) throw new IllegalArgumentException("the zone is only an offset; use FixedOffsetDateTime.from(t.toOffsetDateTime())");
        return read(JavaTime.fromZonedDateTime(t));
    }

    /**
     * To {@code java.time}. Needs an asserted offset (resolve first, or come from {@link Instant#inZone});
     * the JDK's own tz database checks it ({@code invalid}, {@code offset-mismatch}, if it disagrees).
     */
    public Outcome<ZonedDateTime> toZonedDateTime() { return Typed.fromNative(JavaTime.toZonedDateTime(toJson())); }

    @Override public ObjectNode toJson() {
        ObjectNode o = Values.inZone(dateTime.toJson(), zoneId);
        if (assertedOffset != null) ((ObjectNode) o.get("value")).put("offsetAssertion", assertedOffset);
        return o;
    }

    public CivilDateTime dateTime() { return dateTime; }
    public String zoneId() { return zoneId; }
    /** The asserted offset in seconds, if any. */
    public OptionalInt assertedOffsetSeconds() { return assertedOffset == null ? OptionalInt.empty() : OptionalInt.of(assertedOffset); }

    // ---------------------------------------------------------------- resolving

    /** The instant(s) this wall time names under the context's rules, rejecting gaps and overlaps. */
    public Outcome<Instant> resolve(TemporalContext ctx) { return ctx.resolve(this); }

    public Outcome<Instant> resolve(TemporalContext ctx, ResolutionPolicy policy) { return ctx.resolve(this, policy); }

    /** {@code resolve} with a gap policy (overlaps still rejected). */
    public Outcome<Instant> resolve(TemporalContext ctx, Gap gap) { return ctx.resolve(this, ResolutionPolicy.STRICT.withGap(gap)); }

    /** {@code resolve} with an overlap policy (gaps still rejected). */
    public Outcome<Instant> resolve(TemporalContext ctx, Overlap overlap) { return ctx.resolve(this, ResolutionPolicy.STRICT.withOverlap(overlap)); }

    // ---------------------------------------------------------------- arithmetic (wall clock, zone kept)

    /** Same wall time n days later in the same zone. An asserted offset is dropped: it may no longer hold. */
    public ZonedIntent addDays(long n) { return plus(Values.days(n)); }
    public ZonedIntent addWeeks(long n) { return plus(Values.weeks(n)); }
    public ZonedIntent addHours(long n) { return plus(Values.hours(n)); }
    public ZonedIntent addMinutes(long n) { return plus(Values.minutes(n)); }
    public ZonedIntent addSeconds(long n) { return plus(Values.seconds(n)); }

    /** {@code none} ({@code month-end}) when the day doesn't exist in the resulting month. */
    public Outcome<ZonedIntent> addMonths(long months) {
        return Typed.outcome(Operations.add(toJson(), Values.months(months)), ZonedIntent::read);
    }

    private ZonedIntent plus(ObjectNode amount) { return Typed.total(Operations.add(toJson(), amount), ZonedIntent::read); }

    // ---------------------------------------------------------------- equality, text

    @Override public boolean equals(Object o) {
        return o instanceof ZonedIntent z && dateTime.equals(z.dateTime) && zoneId.equals(z.zoneId) && Objects.equals(assertedOffset, z.assertedOffset);
    }

    @Override public int hashCode() { return Objects.hash(dateTime, zoneId, assertedOffset); }

    /** The text form, {@code 2027-06-01T09:00[Europe/Paris]}. */
    @Override public String toString() { return Typed.text(this); }
}
