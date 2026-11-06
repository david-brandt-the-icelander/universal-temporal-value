package net.icelander.utv.examples;

import java.io.IOException;
import java.io.PrintStream;
import java.nio.charset.StandardCharsets;
import java.nio.file.Path;
import java.time.DateTimeException;
import java.time.Duration;
import java.time.LocalDate;
import java.time.LocalDateTime;
import java.time.LocalTime;
import java.time.OffsetDateTime;
import java.time.ZoneId;
import java.time.ZonedDateTime;
import java.time.format.DateTimeFormatter;
import java.time.temporal.ChronoUnit;
import java.time.temporal.IsoFields;
import java.util.Date;
import net.icelander.utv.Bound;
import net.icelander.utv.CivilDate;
import net.icelander.utv.CivilDateTime;
import net.icelander.utv.CivilTime;
import net.icelander.utv.ElapsedSeconds;
import net.icelander.utv.FixedOffsetDateTime;
import net.icelander.utv.Gap;
import net.icelander.utv.Instant;
import net.icelander.utv.Interval;
import net.icelander.utv.MonthEnd;
import net.icelander.utv.Outcome;
import net.icelander.utv.Outcome.Invalid;
import net.icelander.utv.Outcome.Multiple;
import net.icelander.utv.Outcome.None;
import net.icelander.utv.Outcome.Unique;
import net.icelander.utv.Outcome.Unsupported;
import net.icelander.utv.Overlap;
import net.icelander.utv.PosixSeconds;
import net.icelander.utv.TemporalContext;
import net.icelander.utv.TemporalValue;
import net.icelander.utv.ZoneRules;
import net.icelander.utv.ZonedIntent;

/**
 * Everyday date and time tasks, done twice: first with {@code java.time}, then with UTV.
 *
 * <p>{@code Instant} and {@code Period} below are UTV's; the {@code java.time} ones are written in
 * full. Rows marked ‡ depend on this machine (the clock, the default zone, the JDK's tz database).
 * The UTV rows never do: zone rules are the demo file, loaded explicitly.
 *
 * <p>Run from {@code implementations/java}: {@code mvn -q test-compile exec:java@everyday}. The
 * output is checked against {@code src/examples/resources/everyday.expected.txt}.
 */
public final class Everyday {
    static final String RULES = "../../conformance/rules/demo-zones.json";
    static PrintStream out = System.out;

    public static void main(String[] args) throws IOException {
        out = new PrintStream(System.out, true, StandardCharsets.UTF_8);
        TemporalContext ctx = TemporalContext.with(ZoneRules.load(Path.of(args.length > 0 ? args[0] : RULES)));

        out.println("1. Now");
        now();
        out.println("\n2. A date");
        aDate();
        weekdayWeek();
        out.println("\n3. Invalid dates");
        feb30();
        out.println("\n4. Adding");
        addDays();
        monthEnd();
        out.println("\n5. Days between");
        daysBetween();
        compareDates(ctx);
        out.println("\n6. Times and date-times");
        timeOfDay();
        dateTimePlusHours();
        out.println("\n7. Parsing with offsets");
        parseOffsets();
        out.println("\n8. Time zones");
        meetingInParis(ctx);
        out.println("\n9. DST gap");
        dstGap(ctx);
        out.println("\n10. DST overlap");
        dstOverlap(ctx);
        out.println("\n11. Timestamps");
        timestamps();
        out.println("\n12. Durations vs periods");
        dayVs24h(ctx);
        out.println("\n13. Ranges");
        ranges();
        out.println("\n14. Round trip");
        roundTrip();
    }

    static void now() {
        out.println("   Now");
        // BEGIN now
        java.time.Instant clock = java.time.Instant.now();          // the program reads the clock…
        row("Java", clock, "Instant.now() ‡");                      // 2026-11-05T22:22:00.055196971Z ‡
        row("Java", ZonedDateTime.now(), "and the host's zone ‡");  // 2026-11-05T22:22:00.056595349Z[Etc/UTC] ‡

        Instant now = Instant.from(clock);                          // …and hands the reading to UTV
        row("UTV", now, "the reading, as an input ‡");              // 2026-11-05T22:22:00.055196971Z ‡
        // END now
    }

    static void aDate() {
        out.println("   5 November 2026");
        // BEGIN a-date
        LocalDate javaDate = LocalDate.of(2026, 11, 5);
        row("Java", javaDate, "");                                  // 2026-11-05

        CivilDate date = CivilDate.of(2026, 11, 5);
        row("UTV", date, "");                                       // 2026-11-05
        // END a-date
    }

    static void weekdayWeek() {
        out.println("   Day of week and ISO week");
        // BEGIN weekday-week
        LocalDate javaDate = LocalDate.of(2026, 11, 5);
        int javaWeek = javaDate.get(IsoFields.WEEK_OF_WEEK_BASED_YEAR);
        row("Java", javaDate.getDayOfWeek() + ", ISO week " + javaWeek, "");  // THURSDAY, ISO week 45

        CivilDate date = CivilDate.of(2026, 11, 5);
        row("UTV", date.dayOfWeek() + ", ISO week " + date.isoWeek(), "");    // Thursday, ISO week 45
        // END weekday-week
    }

    static void feb30() {
        out.println("   30 February 2026");
        // BEGIN feb-30
        try {
            LocalDate.of(2026, 2, 30);
        } catch (DateTimeException e) {
            row("Java", "throws", e.getMessage());                  // throws  Invalid date 'FEBRUARY 30'
        }
        LocalDate lenient = LocalDate.parse("2026-02-30", DateTimeFormatter.ofPattern("uuuu-MM-dd"));
        row("Java", lenient, "a pattern parse clamps silently");    // 2026-02-28

        try {
            CivilDate.of(2026, 2, 30);
        } catch (IllegalArgumentException e) {
            row("UTV", "throws", e.getMessage());                   // throws  2026-02 has no day 30 (day-out-of-range)
        }
        switch (CivilDate.parse("2026-02-30")) {                    // parsing never throws: it returns an Outcome
            case Invalid<CivilDate> invalid -> row("UTV", "invalid", invalid.why());
                                                                    // invalid  2026-02 has no day 30 (day-out-of-range)
            case Outcome<CivilDate> other -> row("UTV", other, "");
        }
        // END feb-30
    }

    static void addDays() {
        out.println("   5 Nov + 30 days");
        // BEGIN add-days
        row("Java", LocalDate.of(2026, 11, 5).plusDays(30), "");    // 2026-12-05
        // Adding days can't fail, so addDays returns a plain CivilDate, not an Outcome.
        row("UTV", CivilDate.of(2026, 11, 5).addDays(30), "");      // 2026-12-05
        // END add-days
    }

    static void monthEnd() {
        out.println("   31 Jan 2026 + 1 month");
        // BEGIN month-end
        LocalDate jan31 = LocalDate.of(2026, 1, 31);
        row("Java", jan31.plusMonths(1), "clamped silently");       // 2026-02-28

        CivilDate utvJan31 = CivilDate.of(2026, 1, 31);
        switch (utvJan31.addMonths(1)) {                            // an Outcome<CivilDate>
            case Unique<CivilDate> u -> row("UTV", u.value(), "");
            case None<CivilDate> none -> row("UTV", "no such date", none.why());
                                                                    // no such date  2026-02 has no day 31 (month-end)
            case Outcome<CivilDate> other -> row("UTV", other, "");
        }
        row("UTV", utvJan31.addMonths(1, MonthEnd.CONSTRAIN), "with MonthEnd.CONSTRAIN");  // 2026-02-28
        // END month-end
    }

    static void daysBetween() {
        out.println("   5 Nov → 25 Dec");
        // BEGIN days-between
        row("Java", ChronoUnit.DAYS.between(LocalDate.of(2026, 11, 5), LocalDate.of(2026, 12, 25)), "");  // 50
        row("UTV", CivilDate.of(2026, 11, 5).daysUntil(CivilDate.of(2026, 12, 25)), "");                 // 50
        // END days-between
    }

    static void compareDates(TemporalContext ctx) {
        out.println("   Which comes first");
        // BEGIN compare-dates
        row("Java", LocalDate.of(2026, 11, 5).isBefore(LocalDate.of(2026, 12, 25)), "");  // true
        row("UTV", CivilDate.of(2026, 11, 5).isBefore(CivilDate.of(2026, 12, 25)), "");   // true

        // Zoned values have no isBefore: their order depends on zone rules, so the context compares.
        ZonedIntent paris = CivilDateTime.of(2027, 6, 1, 9, 0).inZone("Europe/Paris");
        ZonedIntent newYork = CivilDateTime.of(2027, 6, 1, 3, 0).inZone("America/New_York");
        row("UTV", ctx.compare(paris, newYork), "09:00 Paris vs 03:00 New York");        // equal
        // END compare-dates
    }

    static void timeOfDay() {
        out.println("   09:30");
        // BEGIN time-of-day
        row("Java", LocalTime.of(9, 30), "");                       // 09:30
        row("Java", LocalTime.of(9, 30, 0), "the :00 is dropped");  // 09:30
        row("UTV", CivilTime.of(9, 30), "");                        // 09:30
        row("UTV", CivilTime.of(9, 30, 0), "the second was given"); // 09:30:00
        // END time-of-day
    }

    static void dateTimePlusHours() {
        out.println("   23:30 + 2 hours");
        // BEGIN datetime-plus-hours
        row("Java", LocalDateTime.of(2026, 11, 5, 23, 30).plusHours(2), "");   // 2026-11-06T01:30
        row("UTV", CivilDateTime.of(2026, 11, 5, 23, 30).addHours(2), "");     // 2026-11-06T01:30
        // END datetime-plus-hours
    }

    static void parseOffsets() {
        out.println("   Z vs +00:00");
        // BEGIN parse-offsets
        OffsetDateTime z = OffsetDateTime.parse("2026-11-05T13:30:00Z");
        OffsetDateTime zero = OffsetDateTime.parse("2026-11-05T13:30:00+00:00");
        row("Java", z, "from …Z");                                  // 2026-11-05T13:30Z
        row("Java", zero, "from …+00:00");                          // 2026-11-05T13:30Z
        row("Java", z.equals(zero), "the difference is gone");      // true

        // Z says "UTC is known, the local offset isn't": an Instant. +00:00 is a wall time at offset zero.
        TemporalValue utvZ = TemporalValue.parse("2026-11-05T13:30:00Z").single();
        TemporalValue utvZero = TemporalValue.parse("2026-11-05T13:30:00+00:00").single();
        row("UTV", utvZ, utvZ.getClass().getSimpleName());          // 2026-11-05T13:30:00Z       Instant
        row("UTV", utvZero, utvZero.getClass().getSimpleName());    // 2026-11-05T13:30:00+00:00  FixedOffsetDateTime
        row("UTV", utvZ.equals(utvZero), "");                       // false
        // END parse-offsets
    }

    static void meetingInParis(TemporalContext ctx) {
        out.println("   09:00 in Paris, seen from New York");
        // BEGIN meeting-in-paris
        ZonedDateTime javaMeeting = ZonedDateTime.of(LocalDateTime.of(2027, 6, 1, 9, 0), ZoneId.of("Europe/Paris"));
        row("Java", javaMeeting, "offset fixed today ‡");           // 2027-06-01T09:00+02:00[Europe/Paris] ‡
        row("Java", javaMeeting.withZoneSameInstant(ZoneId.of("America/New_York")), "‡");
                                                                    // 2027-06-01T03:00-04:00[America/New_York] ‡

        ZonedIntent meeting = CivilDateTime.of(2027, 6, 1, 9, 0).inZone("Europe/Paris");
        row("UTV", meeting, "an intention: no offset yet");         // 2027-06-01T09:00[Europe/Paris]
        Instant start = meeting.resolve(ctx).single();              // single(): exactly one answer, or it throws
        row("UTV", start, "with rules " + ctx.rules());
                                                                    // 2027-06-01T07:00:00Z  with rules demo-zones 2026-11-05
        row("UTV", start.inZone("America/New_York", ctx).single(), "");
                                                                    // 2027-06-01T03:00:00-04:00[America/New_York]
        // END meeting-in-paris
    }

    static void dstGap(TemporalContext ctx) {
        out.println("   02:30 on 8 March 2026 in New York");
        // BEGIN dst-gap
        ZonedDateTime javaWall = ZonedDateTime.of(LocalDateTime.of(2026, 3, 8, 2, 30), ZoneId.of("America/New_York"));
        row("Java", javaWall, "moved to 03:30 silently ‡");         // 2026-03-08T03:30-04:00[America/New_York] ‡

        ZonedIntent wall = CivilDateTime.of(2026, 3, 8, 2, 30).inZone("America/New_York");
        switch (wall.resolve(ctx)) {                                // an Outcome<Instant>
            case Unique<Instant> u -> row("UTV", u.value(), "");
            case Multiple<Instant> m -> row("UTV", m.values(), m.why());
            case None<Instant> none -> row("UTV", "no such time", none.why());
                                                                    // no such time  this wall time is skipped in
                                                                    //               America/New_York (gap)
            case Unsupported<Instant> u -> row("UTV", "can't say", u.why());
            case Invalid<Instant> i -> row("UTV", "invalid", i.why());
        }
        Instant shifted = wall.resolve(ctx, Gap.SHIFT_FORWARD).single();
        row("UTV", shifted.inZone("America/New_York", ctx).single(), "Gap.SHIFT_FORWARD");
                                                                    // 2026-03-08T03:30:00-04:00[America/New_York]
        // END dst-gap
    }

    static void dstOverlap(TemporalContext ctx) {
        out.println("   01:30 on 1 November 2026 in New York");
        // BEGIN dst-overlap
        ZonedDateTime javaWall = ZonedDateTime.of(LocalDateTime.of(2026, 11, 1, 1, 30), ZoneId.of("America/New_York"));
        row("Java", javaWall, "the earlier one, silently ‡");       // 2026-11-01T01:30-04:00[America/New_York] ‡

        ZonedIntent wall = CivilDateTime.of(2026, 11, 1, 1, 30).inZone("America/New_York");
        switch (wall.resolve(ctx)) {
            case Unique<Instant> u -> row("UTV", u.value(), "");
            case Multiple<Instant> both -> {
                row("UTV", both.values().size() + " instants", both.why());
                                                                    // 2 instants  this wall time occurs twice in
                                                                    //             America/New_York (ambiguous)
                for (Instant t : both.values()) {
                    row("UTV", t.inZone("America/New_York", ctx).single(), "");
                }                                                   // 2026-11-01T01:30:00-04:00[America/New_York]
            }                                                       // 2026-11-01T01:30:00-05:00[America/New_York]
            case None<Instant> none -> row("UTV", "no such time", none.why());
            case Unsupported<Instant> u -> row("UTV", "can't say", u.why());
            case Invalid<Instant> i -> row("UTV", "invalid", i.why());
        }
        row("UTV", wall.resolve(ctx, Overlap.EARLIEST).single(), "Overlap.EARLIEST");  // 2026-11-01T05:30:00Z
        // END dst-overlap
    }

    static void timestamps() {
        out.println("   Exact fractions and differences");
        // BEGIN timestamps
        java.time.Instant javaStart = java.time.Instant.ofEpochSecond(1793885400L, 250_000_000);
        java.time.Instant javaEnd = java.time.Instant.parse("2026-11-06T13:30:00Z");
        row("Java", javaStart, "");                                 // 2026-11-05T13:30:00.250Z
        row("Java", Duration.between(javaStart, javaEnd), "");      // PT23H59M59.75S
        row("Java", Duration.ofSeconds(1).dividedBy(3), "a third, truncated");  // PT0.333333333S

        Instant start = Instant.fromUnixSeconds(1793885400L, 250_000_000);
        Instant end = Instant.parse("2026-11-06T13:30:00Z").single();
        row("UTV", start, "");                                      // 2026-11-05T13:30:00.25Z
        row("UTV", start.until(end), "");                           // 86399.75 s (POSIX)
        row("UTV", PosixSeconds.of(1, 3), "a third, exactly");      // 1/3 s (POSIX)
        // END timestamps
    }

    static void dayVs24h(TemporalContext ctx) {
        out.println("   One day vs 86,400 seconds across DST");
        // BEGIN day-vs-24h
        ZonedDateTime javaSat = ZonedDateTime.of(LocalDateTime.of(2026, 3, 7, 12, 0), ZoneId.of("America/New_York"));
        row("Java", javaSat.plus(java.time.Period.ofDays(1)), "Period ‡");
                                                                    // 2026-03-08T12:00-04:00[America/New_York] ‡
        row("Java", javaSat.plus(Duration.ofDays(1)), "Duration ‡");
                                                                    // 2026-03-08T13:00-04:00[America/New_York] ‡

        ZonedIntent sat = CivilDateTime.of(2026, 3, 7, 12, 0).inZone("America/New_York");
        row("UTV", sat.addDays(1), "a calendar day");               // 2026-03-08T12:00[America/New_York]
        Instant noon = sat.resolve(ctx).single();
        row("UTV", noon.add(PosixSeconds.of(86400)).inZone("America/New_York", ctx).single(), "86,400 s");
                                                                    // 2026-03-08T13:00:00-04:00[America/New_York]
        row("UTV", noon.add(ElapsedSeconds.of(86400)), "SI seconds need a leap-second table");
                                                                    // unsupported (needs-leap-seconds)
        // END day-vs-24h
    }

    static void ranges() {
        out.println("   November, open-ended and unknown ends");
        // BEGIN ranges
        LocalDate first = LocalDate.of(2026, 11, 1), next = LocalDate.of(2026, 12, 1), day = LocalDate.of(2026, 11, 15);
        row("Java", !day.isBefore(first) && day.isBefore(next), "no range type: by hand");  // true

        CivilDate nov1 = CivilDate.of(2026, 11, 1);
        Interval<CivilDate> november = Interval.halfOpen(nov1, CivilDate.of(2026, 12, 1));
        row("UTV", november, "");                                   // [2026-11-01, 2026-12-01)
        row("UTV", november.contains(CivilDate.of(2026, 11, 15)), "contains 15 Nov");     // definitely

        Interval<CivilDate> fromNov = Interval.of(Bound.included(nov1), Bound.unbounded());
        row("UTV", fromNov, "no end");                              // [2026-11-01, ..)
        row("UTV", fromNov.contains(CivilDate.of(3000, 1, 1)), "contains 3000-01-01");    // definitely

        Interval<CivilDate> lease = Interval.of(Bound.included(nov1), Bound.unknown());
        row("UTV", lease, "an end nobody knows yet");               // [2026-11-01, ?)
        row("UTV", lease.contains(CivilDate.of(2027, 6, 1)), "contains 2027-06-01");      // possibly
        // END ranges
    }

    static void roundTrip() {
        out.println("   Native → UTV → native");
        // BEGIN round-trip
        ZonedDateTime javaMeeting = ZonedDateTime.of(LocalDateTime.of(2027, 6, 1, 9, 0), ZoneId.of("Europe/Paris"));
        ZonedIntent meeting = ZonedIntent.from(javaMeeting);        // keeps the zone, asserts java.time's offset
        row("UTV", meeting, "‡");                                   // 2027-06-01T09:00:00+02:00[Europe/Paris] ‡
        row("Java", meeting.toZonedDateTime().single().equals(javaMeeting), "back again ‡");  // true ‡

        OffsetDateTime javaOffset = OffsetDateTime.parse("2026-11-05T14:30+01:00");
        row("UTV", FixedOffsetDateTime.from(javaOffset), "read as a wall time");  // 2026-11-05T14:30:00+01:00
        row("UTV", Instant.from(javaOffset), "read as an instant");               // 2026-11-05T13:30:00Z

        Date legacy = Date.from(java.time.Instant.parse("2026-11-05T13:30:00Z"));
        row("Java", legacy, "printed in the host's zone ‡");        // Thu Nov 05 13:30:00 UTC 2026 ‡
        row("UTV", Instant.from(legacy.toInstant()), "only its instant is taken");  // 2026-11-05T13:30:00Z
        // END round-trip
    }

    /**
     * Prints one aligned row: who answered, what they gave, and a note. A value too long for its
     * column gets the note on the next line; a host-dependent row (note ending in ‡) keeps the
     * mark on every line. It only prints.
     */
    static void row(String who, Object value, String note) {
        String text = String.valueOf(value);
        if (text.length() <= 30) {
            out.println(String.format("     %-4s %-30s %s", who, text, note).stripTrailing());
            return;
        }
        out.println(String.format("     %-4s %s", who, text) + (note.endsWith("‡") ? " ‡" : ""));
        if (!note.isEmpty() && !note.equals("‡")) out.println(" ".repeat(41) + note);
    }
}
