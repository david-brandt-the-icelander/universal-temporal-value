package net.icelander.utv;

import static org.junit.jupiter.api.Assertions.*;

import java.math.BigInteger;
import java.nio.file.Path;
import java.util.List;
import net.icelander.utv.Outcome.Invalid;
import net.icelander.utv.Outcome.Multiple;
import net.icelander.utv.Outcome.None;
import net.icelander.utv.Outcome.Unique;
import net.icelander.utv.Outcome.Unsupported;
import org.junit.jupiter.api.Test;

/** The typed layer (docs/library-api.md): at least one test per row of §2, plus round trips and constructors. */
class TypedApiTest {
    static final TemporalContext CTX = context();

    static TemporalContext context() {
        try {
            return TemporalContext.with(ZoneRules.load(ConformanceTest.ROOT.resolve("conformance/rules/demo-zones.json")));
        } catch (java.io.IOException e) {
            throw new IllegalStateException(e);
        }
    }

    static void assertCodes(Outcome<?> o, Status status, String... codes) {
        assertEquals(status, o.status(), o::toString);
        List<String> got = o.diagnostics().stream().map(Diagnostic::code).toList();
        for (String c : codes) assertTrue(got.contains(c), "missing " + c + " in " + o);
    }

    // ================================================================ §2 rows

    @Test
    void civilDate() {
        CivilDate nov5 = CivilDate.of(2026, 11, 5);
        assertEquals("2026-11-05", nov5.toString());
        assertEquals(BigInteger.valueOf(2026), nov5.year());
        assertEquals(11, nov5.month());
        assertEquals(5, nov5.day());
        assertEquals(Weekday.THURSDAY, nov5.dayOfWeek());
        assertEquals("Thursday", nov5.dayOfWeek().toString());
        assertEquals(309, nov5.dayOfYear());
        assertEquals(45, nov5.isoWeek());
        assertEquals(BigInteger.valueOf(2026), nov5.isoWeekYear());
        assertEquals(30, nov5.daysInMonth());
        assertFalse(nov5.isLeapYear());
        assertEquals(53, CivilDate.of(2026, 12, 31).isoWeek());

        // total
        assertEquals(CivilDate.of(2026, 12, 5), nov5.addDays(30));
        assertEquals(CivilDate.of(2026, 10, 6), nov5.addDays(-30));
        assertEquals(CivilDate.of(2026, 11, 19), nov5.addWeeks(2));
        CivilDate jan31 = CivilDate.of(2026, 1, 31);
        assertEquals(CivilDate.of(2026, 2, 28), jan31.addMonths(1, MonthEnd.CONSTRAIN));
        assertEquals(CivilDate.of(2025, 2, 28), CivilDate.of(2024, 2, 29).addYears(1, MonthEnd.CONSTRAIN));
        assertThrows(IllegalArgumentException.class, () -> jan31.addMonths(1, MonthEnd.REJECT));
        assertEquals(50, nov5.daysUntil(CivilDate.of(2026, 12, 25)));
        assertEquals(-50, CivilDate.of(2026, 12, 25).daysUntil(nov5));
        assertEquals("2026-11-05T09:30", nov5.at(CivilTime.of(9, 30)).toString());
        assertTrue(nov5.compareTo(CivilDate.of(2026, 12, 25)) < 0);
        assertTrue(nov5.isBefore(CivilDate.of(2026, 12, 25)));

        // partial
        assertCodes(jan31.addMonths(1), Status.NONE, "month-end");
        assertEquals(CivilDate.of(2026, 12, 5), nov5.addMonths(1).single());
        assertCodes(CivilDate.of(2024, 2, 29).addYears(1), Status.NONE, "month-end");
        assertEquals(CivilDate.of(2026, 12, 15), nov5.add(Period.of(0, 1, 1, 3, 0, 0, 0)).single());
        assertCodes(nov5.add(Period.hours(1)), Status.INVALID, "kind-mismatch");
        assertCodes(jan31.add(Period.months(1)), Status.NONE, "month-end");
    }

    @Test
    void civilYearMonth() {
        CivilYearMonth nov = CivilYearMonth.of(2026, 11);
        assertEquals("2026-11", nov.toString());
        assertEquals(CivilYearMonth.of(2027, 1), nov.addMonths(2));
        assertEquals(CivilYearMonth.of(2025, 11), nov.addYears(-1));
        assertTrue(nov.compareTo(CivilYearMonth.of(2027, 1)) < 0);
        assertThrows(IllegalArgumentException.class, () -> CivilYearMonth.of(2026, 13));
    }

    @Test
    void civilTime() {
        assertEquals("09:30", CivilTime.of(9, 30).toString());
        assertEquals("09:30:00", CivilTime.of(9, 30, 0).toString());
        assertEquals("09:30:15.25", CivilTime.of(9, 30, 15, 250_000_000).toString());
        assertNotEquals(CivilTime.of(9, 30), CivilTime.of(9, 30, 0));
        assertEquals(0, CivilTime.of(9, 30).compareTo(CivilTime.of(9, 30, 0)));
        assertTrue(CivilTime.of(9, 30).compareTo(CivilTime.of(9, 30, 1)) < 0);
        assertFalse(CivilTime.of(9, 30).hasSecond());
    }

    @Test
    void civilDateTime() {
        CivilDateTime late = CivilDateTime.of(2026, 11, 5, 23, 30);
        assertEquals("2026-11-06T01:30", late.addHours(2).toString());
        assertEquals("2026-11-05T23:31", late.addMinutes(1).toString());
        assertEquals("2026-11-05T23:30:30", late.addSeconds(30).toString());
        assertEquals("2026-11-06T23:30", late.addDays(1).toString());
        assertEquals("2026-11-12T23:30", late.addWeeks(1).toString());
        assertEquals(CivilDate.of(2026, 11, 5), late.date());
        assertEquals(CivilTime.of(23, 30), late.time());
        CivilDateTime jan31 = CivilDateTime.of(2026, 1, 31, 9, 0);
        assertCodes(jan31.addMonths(1), Status.NONE, "month-end");
        assertEquals("2026-02-28T09:00", jan31.addMonths(1, MonthEnd.CONSTRAIN).toString());
        assertEquals("2026-03-01T10:00", jan31.add(Period.of(0, 0, 0, 29, 1, 0, 0)).single().toString());
        assertEquals("2027-06-01T09:00[Europe/Paris]", CivilDateTime.of(2027, 6, 1, 9, 0).inZone("Europe/Paris").toString());
        assertEquals("2026-11-05T23:30+01:00", late.atOffset(3600).toString());
        assertTrue(late.compareTo(late.addSeconds(1)) < 0);
    }

    @Test
    void fixedOffsetDateTime() {
        FixedOffsetDateTime paris = CivilDateTime.of(2026, 11, 5, 14, 30).atOffset(3600);
        FixedOffsetDateTime london = CivilDateTime.of(2026, 11, 5, 13, 30).atOffset(0);
        assertEquals(Instant.parse("2026-11-05T13:30:00Z").single(), paris.toInstant());
        assertEquals(3600, paris.offsetSeconds());
        assertEquals(CivilDateTime.of(2026, 11, 5, 14, 30), paris.dateTime());
        assertEquals(0, paris.compareTo(london));
        assertNotEquals(paris, london);
        assertThrows(IllegalArgumentException.class, () -> CivilDateTime.of(2026, 11, 5, 14, 30).atOffset(19 * 3600));
    }

    @Test
    void zonedIntent() {
        ZonedIntent meeting = CivilDateTime.of(2027, 6, 1, 9, 0).inZone("Europe/Paris");
        assertEquals("Europe/Paris", meeting.zoneId());
        assertTrue(meeting.assertedOffsetSeconds().isEmpty());
        assertEquals("2027-06-02T09:00[Europe/Paris]", meeting.addDays(1).toString());
        assertEquals("2027-06-01T10:00[Europe/Paris]", meeting.addHours(1).toString());
        assertEquals("2027-06-01T09:01[Europe/Paris]", meeting.addMinutes(1).toString());
        assertEquals("2027-06-01T09:00:01[Europe/Paris]", meeting.addSeconds(1).toString());
        assertEquals("2027-06-08T09:00[Europe/Paris]", meeting.addWeeks(1).toString());
        assertEquals("2027-07-01T09:00[Europe/Paris]", meeting.addMonths(1).single().toString());
        // adding drops an asserted offset
        ZonedIntent asserted = ZonedIntent.parse("2027-06-01T09:00+02:00[Europe/Paris]").single();
        assertEquals(7200, asserted.assertedOffsetSeconds().getAsInt());
        assertTrue(asserted.addDays(1).assertedOffsetSeconds().isEmpty());

        // resolve
        assertEquals("2027-06-01T07:00:00Z", meeting.resolve(CTX).single().toString());
        ZonedIntent gap = CivilDateTime.of(2026, 3, 8, 2, 30).inZone("America/New_York");
        assertCodes(gap.resolve(CTX), Status.NONE, "gap");
        assertEquals("2026-03-08T07:30:00Z", gap.resolve(CTX, Gap.SHIFT_FORWARD).single().toString());
        ZonedIntent overlap = CivilDateTime.of(2026, 11, 1, 1, 30).inZone("America/New_York");
        Outcome<Instant> both = overlap.resolve(CTX);
        assertCodes(both, Status.MULTIPLE, "ambiguous");
        assertEquals(List.of("2026-11-01T05:30:00Z", "2026-11-01T06:30:00Z"),
                ((Multiple<Instant>) both).values().stream().map(Object::toString).toList());
        assertEquals("2026-11-01T05:30:00Z", overlap.resolve(CTX, Overlap.EARLIEST).single().toString());
        assertEquals("2026-11-01T06:30:00Z", overlap.resolve(CTX, new ResolutionPolicy(Gap.REJECT, Overlap.LATEST)).single().toString());
        assertEquals(Status.MULTIPLE, overlap.resolve(CTX, ResolutionPolicy.ALL_CANDIDATES).status());
        assertCodes(CivilDateTime.of(2027, 6, 1, 9, 0).inZone("Mars/Olympus").resolve(CTX), Status.UNSUPPORTED, "unknown-zone");
        assertCodes(CivilDateTime.of(2030, 6, 1, 9, 0).inZone("Europe/Paris").resolve(CTX), Status.UNSUPPORTED, "outside-rule-window");
        assertCodes(ZonedIntent.parse("2027-06-01T09:00+05:00[Europe/Paris]").single().resolve(CTX), Status.INVALID, "offset-mismatch");

        // no comparison operators; the context compares
        assertFalse(Comparable.class.isAssignableFrom(ZonedIntent.class));
        ZonedIntent ny = CivilDateTime.of(2027, 6, 1, 3, 0).inZone("America/New_York");
        assertEquals(Ordering.EQUAL, CTX.compare(meeting, ny).single());
        assertEquals(Ordering.BEFORE, CTX.compare(ny.addHours(-1), meeting).single());
        // 06:00Z lies between the two readings of 01:30 New York: the order depends on which one is meant
        Outcome<Ordering> depends = CTX.compare(overlap, Instant.parse("2026-11-01T06:00:00Z").single());
        assertEquals(List.of(Ordering.BEFORE, Ordering.AFTER), ((Multiple<Ordering>) depends).values());
        assertCodes(CTX.compare(gap, meeting), Status.NONE, "gap");
    }

    @Test
    void instant() {
        Instant t = Instant.fromUnixSeconds(1793885400L, 250_000_000);
        assertEquals("2026-11-05T13:30:00.25Z", t.toString());
        assertEquals(1793885400L, t.unixSeconds());
        assertEquals(250_000_000, t.nanoseconds());
        assertEquals(t, Instant.fromUnixNanoseconds(1793885400_250_000_000L));
        assertEquals("2026-11-05T14:30:00.25Z", t.add(PosixSeconds.of(3600)).toString());
        Instant later = Instant.parse("2026-11-06T13:30:00Z").single();
        assertEquals("86399.75 s (POSIX)", t.until(later).toString());
        assertEquals(PosixSeconds.of(345599, 4), t.until(later));
        assertEquals("2026-11-05T14:30:00.25+01:00", t.atOffset(3600).toString());
        assertTrue(t.compareTo(later) < 0);
        assertEquals("2026-11-05T08:30:00.25-05:00[America/New_York]", t.inZone("America/New_York", CTX).single().toString());
        assertCodes(t.inZone("Mars/Olympus", CTX), Status.UNSUPPORTED, "unknown-zone");
        assertCodes(Instant.fromUnixSeconds(0).inZone("Europe/Paris", CTX), Status.UNSUPPORTED, "outside-rule-window");
        assertCodes(t.add(ElapsedSeconds.of(86400)), Status.UNSUPPORTED, "needs-leap-seconds");
        Instant third = Instant.fromUnixSeconds(0).add(PosixSeconds.of(1, 3));
        assertEquals(PosixSeconds.of(1, 3), Instant.fromUnixSeconds(0).until(third));
        assertThrows(ArithmeticException.class, () -> third.atOffset(0));
    }

    @Test
    void amounts() {
        Period p = Period.of(1, 2, 0, 10, 2, 30, 0);
        assertEquals("P1Y2M10DT2H30M", p.toString());
        assertEquals(2, p.months());
        assertEquals("-P1Y2M10DT2H30M", p.negate().toString());
        assertEquals(Period.months(1), Period.parse("P1M").single());
        assertEquals(Period.minutes(1), Period.parse("PT1M").single());
        assertEquals("P0D", Period.ZERO.toString());
        assertTrue(Period.of(0, 1, 0, -1, 0, 0, 0).toString().startsWith("{"), "mixed signs print as JSON");
        assertEquals("3600.75 s (POSIX)", PosixSeconds.of(14403, 4).toString());
        assertEquals("1/3 s (POSIX)", PosixSeconds.of(1, 3).toString());
        assertEquals("-1/3 s (POSIX)", PosixSeconds.of(1, 3).negate().toString());
        assertEquals(BigInteger.valueOf(3), PosixSeconds.of(2, 6).denominator());
        assertEquals("86400 s (SI)", ElapsedSeconds.of(86400).toString());
        assertEquals(ElapsedSeconds.of(-5), ElapsedSeconds.of(5).negate());
    }

    @Test
    void intervals() {
        CivilDate nov1 = CivilDate.of(2026, 11, 1), dec1 = CivilDate.of(2026, 12, 1);
        Interval<CivilDate> november = Interval.halfOpen(nov1, dec1);
        assertEquals("[2026-11-01, 2026-12-01)", november.toString());
        assertEquals(Bound.included(nov1), november.start());
        assertEquals(Bound.excluded(dec1), november.end());
        assertEquals(Truth.DEFINITELY, november.contains(CivilDate.of(2026, 11, 15)));
        assertEquals(Truth.IMPOSSIBLE, november.contains(dec1));
        assertEquals(Truth.IMPOSSIBLE, november.overlaps(Interval.halfOpen(dec1, CivilDate.of(2027, 1, 1))));
        assertEquals(Truth.DEFINITELY, Interval.closed(nov1, dec1).overlaps(Interval.halfOpen(dec1, CivilDate.of(2027, 1, 1))));
        Interval<CivilDate> unknownEnd = Interval.of(Bound.included(nov1), Bound.unknown());
        assertEquals("[2026-11-01, ?)", unknownEnd.toString());
        assertEquals(Truth.POSSIBLY, unknownEnd.contains(CivilDate.of(2027, 6, 1)));
        assertEquals(Truth.IMPOSSIBLE, unknownEnd.contains(CivilDate.of(2026, 10, 1)));
        assertEquals("[2026-11-01, ..)", Interval.of(Bound.included(nov1), Bound.unbounded()).toString());
        assertThrows(IllegalArgumentException.class, () -> Interval.halfOpen(dec1, nov1));
        assertCodes(Interval.tryOf(Bound.included(dec1), Bound.excluded(nov1)), Status.INVALID, "reversed-interval");

        Instant a = Instant.parse("2026-11-05T13:30:00Z").single(), b = Instant.parse("2026-11-05T15:30:00Z").single();
        Interval<Instant> meeting = Interval.halfOpen(a, b);
        assertEquals("[2026-11-05T13:30:00Z, 2026-11-05T15:30:00Z)", meeting.toString());
        assertEquals(Truth.DEFINITELY, meeting.contains(a));
        assertEquals(Truth.IMPOSSIBLE, meeting.contains(b));

        // representation equality: the same days, written differently, are different values
        assertNotEquals(november, Interval.closed(nov1, CivilDate.of(2026, 11, 30)));
        assertEquals(Interval.closed(nov1, CivilDate.of(2026, 11, 30)), Interval.parse("2026-11-01/2026-11-30", CivilDate.class).single());
        assertCodes(Interval.parse("[2026-11-01, 2026-12-01)", Instant.class), Status.INVALID, "kind-mismatch");
    }

    // ================================================================ outcome helpers

    @Test
    void outcomeHelpersNeverPickSilently() {
        Outcome<CivilDate> none = CivilDate.of(2026, 1, 31).addMonths(1);
        assertTrue(none instanceof None<CivilDate>);
        assertEquals("none (month-end)", none.toString());
        assertEquals("2026-02 has no day 31 (month-end)", none.why());
        assertTrue(none.tryGetUnique().isEmpty());
        assertEquals(CivilDate.of(2000, 1, 1), none.uniqueOr(CivilDate.of(2000, 1, 1)));
        OutcomeException e = assertThrows(OutcomeException.class, none::single);
        assertEquals(Status.NONE, e.status());
        assertEquals("month-end", e.diagnostics().get(0).code());
        assertEquals("none", none.toResult().status().wire());

        Outcome<CivilDate> one = CivilDate.of(2026, 1, 15).addMonths(1);
        assertTrue(one instanceof Unique<CivilDate>);
        assertEquals("2026-02-15", one.toString());
        assertEquals(CivilDate.of(2026, 2, 15), one.tryGetUnique().orElseThrow());
        assertEquals(Values.date(2026, 2, 15), one.toResult().value().orElseThrow());

        Outcome<Instant> two = CivilDateTime.of(2026, 11, 1, 1, 30).inZone("America/New_York").resolve(CTX);
        assertThrows(OutcomeException.class, two::single);
        assertEquals("multiple (ambiguous)", two.toString());
        assertEquals(2, two.toResult().candidates().size());

        String handled = switch (two) {
            case Unique<Instant> u -> "one";
            case Multiple<Instant> m -> m.values().size() + " instants";
            case None<Instant> n -> "none";
            case Unsupported<Instant> u -> "unsupported";
            case Invalid<Instant> i -> "invalid";
        };
        assertEquals("2 instants", handled);
    }

    // ================================================================ constructors, text and JSON

    @Test
    void constructorsRejectValuesThatCannotExist() {
        assertThrows(IllegalArgumentException.class, () -> CivilDate.of(2026, 2, 30));
        assertThrows(IllegalArgumentException.class, () -> CivilDate.of(2026, 13, 1));
        assertThrows(IllegalArgumentException.class, () -> CivilTime.of(25, 0));
        assertThrows(IllegalArgumentException.class, () -> CivilTime.of(9, 60));
        assertThrows(IllegalArgumentException.class, () -> CivilTime.of(23, 59, 60));
        assertThrows(IllegalArgumentException.class, () -> CivilDateTime.of(2026, 2, 29, 9, 0));
        assertThrows(IllegalArgumentException.class, () -> CivilDateTime.of(2027, 6, 1, 9, 0).inZone("not a zone"));
        assertThrows(IllegalArgumentException.class, () -> PosixSeconds.of(1, 0));
        assertCodes(CivilDate.tryOf(2026, 2, 30), Status.INVALID, "day-out-of-range");
        assertCodes(CivilDate.tryOf(2026, 13, 1), Status.INVALID, "month-out-of-range");
        assertCodes(CivilTime.tryOf(9, 60), Status.INVALID, "field-out-of-range");
        assertCodes(CivilTime.tryOf(23, 59, 60), Status.UNSUPPORTED, "leap-second");
        assertEquals(CivilDate.of(BigInteger.TEN.pow(30), 2, 29).year(), BigInteger.TEN.pow(30));
    }

    static final List<String> TEXTS = List.of("2026-11-05", "+275760-09-13", "-0500-03-01", "2026-11", "09:30", "09:30:15.25",
            "2026-11-05T14:30", "2026-11-05T14:30:00", "2026-11-05T14:30+01:00", "2026-11-05T14:30+00:00",
            "2027-06-01T09:00[Europe/Paris]", "2027-06-01T09:00+02:00[Europe/Paris]", "2026-11-05T13:30:00Z",
            "1970-01-01T00:00:00.5Z", "P1M", "PT1M", "-P1DT2H", "P1W3D", "P0D",
            "[2026-11-01, 2026-12-01)", "(.., 2026-12-01]", "[2026-11-01, ?)", "[2026-11-05T13:30:00Z, 2026-11-05T15:30:00Z)");

    @Test
    void toStringRoundTripsThroughParse() {
        for (String text : TEXTS) {
            TemporalValue v = TemporalValue.parse(text).single();
            assertEquals(text, v.toString(), text);
            assertEquals(v, TemporalValue.parse(v.toString()).single(), text);
        }
        assertEquals(CivilDate.of(2026, 11, 5), CivilDate.parse("2026-11-05").single());
        assertEquals(Status.INVALID, CivilDate.parse("2026-02-30").status());
        assertCodes(CivilDate.parse("2026-11-05T14:30"), Status.INVALID, "kind-mismatch");
        assertCodes(TemporalValue.parse("next Tuesday"), Status.INVALID, "syntax");
        assertCodes(TemporalValue.parse("1347~"), Status.UNSUPPORTED, "unsupported-form");
    }

    @Test
    void parsedKindsAreTypes() {
        assertInstanceOf(Instant.class, TemporalValue.parse("2026-11-05T13:30:00Z").single());
        assertInstanceOf(FixedOffsetDateTime.class, TemporalValue.parse("2026-11-05T13:30:00+00:00").single());
        assertInstanceOf(ZonedIntent.class, TemporalValue.parse("2027-06-01T09:00[Europe/Paris]").single());
        assertInstanceOf(CivilDateTime.class, TemporalValue.parse("2026-11-05T14:30").single());
        assertInstanceOf(Period.class, TemporalValue.parse("P1M").single());
        assertInstanceOf(Interval.class, TemporalValue.parse("[2026-11-01, ..)").single());
    }

    @Test
    void fromJsonOfToJsonIsTheSameValue() {
        for (String text : TEXTS) {
            TemporalValue v = TemporalValue.parse(text).single();
            assertEquals(v, TemporalValue.fromJson(v.toJson()).single(), text);
        }
        List<TemporalValue> more = List.of(PosixSeconds.of(1, 3), ElapsedSeconds.of(86400), Period.of(0, 1, 0, -1, 0, 0, 0),
                Instant.fromUnixSeconds(0).add(PosixSeconds.of(1, 3)), CivilYearMonth.of(-44, 3));
        for (TemporalValue v : more) assertEquals(v, TemporalValue.fromJson(v.toJson()).single(), v::toString);
        assertEquals(CivilDate.of(2026, 11, 5), CivilDate.fromJson(Values.date(2026, 11, 5)).single());
    }

    @Test
    void fromJsonRefusesWrongKindsAndLosses() {
        assertCodes(CivilDate.fromJson(Values.dateTime(2026, 11, 5, 9, 0)), Status.INVALID, "kind-mismatch");
        assertCodes(Instant.fromJson(Values.date(2026, 11, 5)), Status.INVALID, "kind-mismatch");
        assertCodes(CivilDateTime.fromJson(Values.inZone(Values.dateTime(2026, 11, 5, 9, 0), "Europe/Paris")), Status.INVALID, "kind-mismatch");
        assertCodes(Period.fromJson(Values.posixSeconds(5)), Status.INVALID, "kind-mismatch");
        assertCodes(CivilDate.fromJson(Values.date(2026, 2, 30)), Status.INVALID, "day-out-of-range");
        // a resolutionPolicy has no place in a ZonedIntent: refused, not silently dropped
        var withPolicy = Values.inZone(Values.dateTime(2026, 11, 5, 9, 0), "Europe/Paris");
        withPolicy.with("value").putObject("resolutionPolicy").put("gap", "reject");
        assertEquals(Status.UNSUPPORTED, ZonedIntent.fromJson(withPolicy).status());
        // an explicit floating zone is the same as none
        var floating = Values.dateTime(2026, 11, 5, 9, 0);
        floating.with("value").putObject("zone").put("type", "floating");
        assertEquals(CivilDateTime.of(2026, 11, 5, 9, 0), CivilDateTime.fromJson(floating).single());
    }

    @Test
    void settledGaps() {
        // a valid UTV value that no typed type can hold: unsupported, not kind-mismatch
        var zonedDate = Values.inZone(Values.date(2026, 11, 5), "Europe/Paris");
        assertCodes(CivilDate.fromJson(zonedDate), Status.UNSUPPORTED, "unsupported-operand");
        assertCodes(TemporalValue.fromJson(zonedDate), Status.UNSUPPORTED, "unsupported-operand");
        var withPolicy = Values.dateTime(2026, 11, 5, 9, 0);
        withPolicy.with("value").putObject("resolutionPolicy").put("gap", "reject");
        assertCodes(CivilDateTime.fromJson(withPolicy), Status.UNSUPPORTED, "unsupported-operand");
        // JSON of the wrong shape: malformed-value, with the 0.1 structural code alongside
        var badMonth = Values.date(2026, 11, 5);
        badMonth.with("value").with("fields").put("month", "eleven");
        assertCodes(CivilDate.fromJson(badMonth), Status.INVALID, "malformed-value");
        var extra = Values.date(2026, 11, 5);
        extra.put("surprise", true);
        assertCodes(CivilDate.fromJson(extra), Status.INVALID, "malformed-value", "utv.envelope.unknown-property");
        assertCodes(ZoneRules.tryParse("{\"zones\": 3}"), Status.INVALID, "malformed-value");
        assertThrows(IllegalArgumentException.class, () -> ZoneRules.parse("{\"zones\": 3}"));
        // a zone after Z
        assertCodes(TemporalValue.parse("2026-11-05T13:30:00Z[Europe/Paris]"), Status.UNSUPPORTED, "unsupported-annotation");
        // intervals with no point at all are constructor errors
        assertThrows(IllegalArgumentException.class, () -> Interval.<CivilDate>of(Bound.unbounded(), Bound.unknown()));
        assertCodes(Interval.<CivilDate>tryOf(Bound.unbounded(), Bound.unknown()), Status.UNSUPPORTED, "unsupported-form");
        // nanoseconds is the floor; subSecond is exact
        Instant third = Instant.fromUnixSeconds(5).add(PosixSeconds.of(1, 3));
        assertEquals(333_333_333, third.nanoseconds());
        assertEquals(PosixSeconds.of(1, 3), third.subSecond());
        assertEquals(PosixSeconds.of(0), Instant.fromUnixSeconds(5).subSecond());
        assertEquals(PosixSeconds.of(1, 4), Instant.fromUnixSeconds(-1, 250_000_000).subSecond());
    }

    @Test
    void nativeInteropOnTypes() {
        var local = java.time.LocalDate.of(2026, 11, 5);
        assertEquals(local, CivilDate.from(local).toLocalDate().single());
        assertCodes(CivilDate.of(BigInteger.TEN.pow(12), 1, 1).toLocalDate(), Status.UNSUPPORTED, "out-of-native-range");
        var zdt = java.time.ZonedDateTime.of(2027, 6, 1, 9, 0, 0, 0, java.time.ZoneId.of("Europe/Paris"));
        ZonedIntent z = ZonedIntent.from(zdt);
        assertEquals("2027-06-01T09:00:00+02:00[Europe/Paris]", z.toString());
        assertEquals(zdt, z.toZonedDateTime().single());
        assertThrows(IllegalArgumentException.class, () -> ZonedIntent.from(zdt.withZoneSameInstant(java.time.ZoneOffset.UTC)));
        var odt = java.time.OffsetDateTime.parse("2026-11-05T14:30+01:00");
        assertEquals("2026-11-05T14:30:00+01:00", FixedOffsetDateTime.from(odt).toString());
        assertEquals("2026-11-05T13:30:00Z", Instant.from(odt).toString());
        assertEquals(odt, FixedOffsetDateTime.from(odt).toOffsetDateTime().single());
        assertEquals(java.time.Duration.ofMillis(1500), PosixSeconds.from(java.time.Duration.ofMillis(1500)).toDuration().single());
        assertEquals(java.time.Period.of(1, 2, 3), Period.from(java.time.Period.of(1, 2, 3)).toJavaPeriod().single());
        Outcome<java.time.Instant> third = Instant.fromUnixSeconds(0).add(PosixSeconds.of(1, 3)).toJavaInstant();
        assertCodes(third, Status.UNIQUE, "precision-loss");
    }

    @Test
    void contextLoadsRulesExplicitly() throws Exception {
        ZoneRules rules = ZoneRules.load(Path.of(System.getProperty("user.dir")).resolve("../../conformance/rules/demo-zones.json"));
        assertTrue(rules.zoneIds().contains("Europe/Paris"));
        assertEquals("demo-zones 2026-11-05", rules.toString());
        assertSame(rules, TemporalContext.with(rules).rules());
        assertThrows(NullPointerException.class, () -> TemporalContext.with(null));
    }
}
