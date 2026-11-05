package net.icelander.utv.examples;

import com.fasterxml.jackson.databind.JsonNode;
import java.io.IOException;
import java.io.PrintStream;
import java.nio.charset.StandardCharsets;
import java.nio.file.Files;
import java.nio.file.Path;
import java.time.DateTimeException;
import java.time.Duration;
import java.time.Instant;
import java.time.LocalDate;
import java.time.LocalDateTime;
import java.time.LocalTime;
import java.time.OffsetDateTime;
import java.time.Period;
import java.time.ZoneId;
import java.time.ZonedDateTime;
import java.time.format.DateTimeFormatter;
import java.time.temporal.ChronoUnit;
import java.time.temporal.IsoFields;
import java.time.zone.ZoneRulesProvider;
import java.util.Date;
import java.util.List;
import java.util.stream.Collectors;
import net.icelander.utv.Diagnostic;
import net.icelander.utv.Intervals;
import net.icelander.utv.Iso;
import net.icelander.utv.JavaTime;
import net.icelander.utv.NativeResult;
import net.icelander.utv.Operations;
import net.icelander.utv.ResolutionPolicy;
import net.icelander.utv.RuleBundle;
import net.icelander.utv.Status;
import net.icelander.utv.Utv;
import net.icelander.utv.UtvResult;
import net.icelander.utv.Values;
import net.icelander.utv.Zones;

/**
 * Everyday date and time tasks, side by side: what you write today with {@code java.time}, and
 * the same task with UTV.
 *
 * <p>The native lines print whatever {@code java.time} gives on this host; some of them depend on
 * the host's clock, default zone or the JDK's tz database, and say so. The UTV lines are
 * deterministic: the example checks each one and exits with status 1 if any differs.
 *
 * <p>Run from {@code implementations/java}: {@code mvn -q test-compile exec:java@everyday}
 */
public final class Everyday {
    /** The demo zone rules, relative to the project directory ({@code implementations/java}). */
    public static final String RULES = "../../conformance/rules/demo-zones.json";

    private final PrintStream out;
    private final RuleBundle rules;
    private int mismatches;

    private Everyday(PrintStream out, RuleBundle rules) {
        this.out = out;
        this.rules = rules;
    }

    public static void main(String[] args) throws IOException {
        PrintStream out = new PrintStream(System.out, true, StandardCharsets.UTF_8);
        RuleBundle rules = loadRules(Path.of(args.length > 0 ? args[0] : RULES));
        if (run(rules, out) > 0) System.exit(1);
    }

    public static RuleBundle loadRules(Path path) throws IOException {
        return RuleBundle.parse(Files.readString(path.toAbsolutePath().normalize()));
    }

    /** Prints every section and returns the number of UTV outcomes that differ from what the example expects. */
    public static int run(RuleBundle rules, PrintStream out) {
        Everyday e = new Everyday(out, rules);
        e.now();
        e.aDate();
        e.invalidDates();
        e.adding();
        e.daysBetween();
        e.timesAndDateTimes();
        e.parsingWithOffsets();
        e.timeZones();
        e.dstGap();
        e.dstOverlap();
        e.timestamps();
        e.durationsVsPeriods();
        e.ranges();
        e.roundTrip();
        out.println();
        out.println(e.mismatches == 0 ? "Every UTV outcome as expected." : e.mismatches + " UTV outcomes differ from what the example expects.");
        return e.mismatches;
    }

    // ------------------------------------------------------------------ 1
    void now() {
        section("1. Now");
        Instant now = Instant.now();                                // reads the platform clock
        nat("Instant.now()", now);
        nat("ZonedDateTime.now()", ZonedDateTime.now());           // also reads the host's default zone
        note("ZonedDateTime.now() used the host's default zone, " + ZoneId.systemDefault() + "; it varies by machine.");

        // UTV never reads the clock. The example reads it (above) and converts that reading explicitly.
        UtvResult utvNow = Iso.formatIso(JavaTime.fromInstant(now));
        utvStatus("JavaTime.fromInstant(now)", utvNow, "unique");
        note("Deliberate: the clock reading is an input you can see, log and replay; the library has no now().");
    }

    // ------------------------------------------------------------------ 2
    void aDate() {
        section("2. A date");
        LocalDate d = LocalDate.of(2026, 11, 5);
        nat("LocalDate.of(2026, 11, 5)", d.format(DateTimeFormatter.ISO_LOCAL_DATE));
        LocalDate p = LocalDate.parse("2026-11-05");
        nat("parse -> day of week, ISO week", p.getDayOfWeek() + ", week " + p.get(IsoFields.WEEK_OF_WEEK_BASED_YEAR)
                + " of " + p.get(IsoFields.WEEK_BASED_YEAR));

        utv("Iso.formatIso(Values.date(2026, 11, 5))", Iso.formatIso(Values.date(2026, 11, 5)), "unique 2026-11-05");
        UtvResult parsed = Iso.parseIso("2026-11-05");
        utv("Iso.parseIso(\"2026-11-05\")", parsed, "unique 2026-11-05");
        utv("Operations.fields(parsed)", Operations.fields(parsed.value().orElseThrow()),
                "unique {\"dayOfWeek\":4,\"dayOfYear\":309,\"isoWeekYear\":\"2026\",\"isoWeek\":45,\"daysInMonth\":30,\"isLeapYear\":false}");
    }

    // ------------------------------------------------------------------ 3
    void invalidDates() {
        section("3. Invalid dates");
        try {
            nat("LocalDate.of(2026, 2, 30)", LocalDate.of(2026, 2, 30));
        } catch (DateTimeException e) {
            nat("LocalDate.of(2026, 2, 30)", "throws " + e.getClass().getSimpleName() + ": " + e.getMessage());
        }
        try {
            LocalDate lenient = LocalDate.parse("2026-02-30", DateTimeFormatter.ofPattern("uuuu-MM-dd"));
            nat("LocalDate.parse(\"2026-02-30\", ofPattern(\"uuuu-MM-dd\"))", lenient);
            note("The pattern formatter (default ResolverStyle.SMART) returned " + lenient + " without an error.");
        } catch (DateTimeException e) {
            nat("LocalDate.parse(\"2026-02-30\", ofPattern(\"uuuu-MM-dd\"))", "throws " + e.getMessage());
        }

        // UTV: building never throws; the operations say the day does not exist.
        var feb30 = Values.date(2026, 2, 30);
        utv("Iso.parseIso(\"2026-02-30\")", Iso.parseIso("2026-02-30"), "invalid (utv.civil.day)");
        utv("Operations.fields(Values.date(2026, 2, 30))", Operations.fields(feb30), "invalid (utv.civil.day)");
    }

    // ------------------------------------------------------------------ 4
    void adding() {
        section("4. Adding");
        LocalDate nov5 = LocalDate.of(2026, 11, 5);
        nat("nov5.plusDays(30)", nov5.plusDays(30));
        nat("nov5.plusMonths(1)", nov5.plusMonths(1));
        LocalDate clamped = LocalDate.of(2026, 1, 31).plusMonths(1);
        nat("LocalDate.of(2026, 1, 31).plusMonths(1)", clamped);
        note("plusMonths gave " + clamped + " for a 31 February that does not exist, without saying so.");

        var date = Values.date(2026, 11, 5);
        utv("Operations.add(date, Values.days(30))", Operations.add(date, Values.days(30)), "unique 2026-12-05");
        utv("Operations.add(date, Values.months(1))", Operations.add(date, Values.months(1)), "unique 2026-12-05");
        var jan31 = Values.date(2026, 1, 31);
        utv("Operations.add(jan31, Values.months(1))", Operations.add(jan31, Values.months(1)), "none (month-end)");
        utv("... MonthEnd.CONSTRAIN", Operations.add(jan31, Values.months(1), Operations.MonthEnd.CONSTRAIN), "unique 2026-02-28");
        note("UTV refuses to guess by default; clamping is a policy you name.");
    }

    // ------------------------------------------------------------------ 5
    void daysBetween() {
        section("5. Days between");
        LocalDate nov5 = LocalDate.of(2026, 11, 5), xmas = LocalDate.of(2026, 12, 25);
        nat("ChronoUnit.DAYS.between(nov5, xmas)", ChronoUnit.DAYS.between(nov5, xmas));
        nat("nov5.isBefore(xmas)", nov5.isBefore(xmas));

        var a = Values.date(2026, 11, 5);
        var b = Values.date(2026, 12, 25);
        utv("Operations.daysBetween(a, b)", Operations.daysBetween(a, b), "unique 50");
        utv("Operations.compare(a, b)", Operations.compare(a, b), "unique before");
    }

    // ------------------------------------------------------------------ 6
    void timesAndDateTimes() {
        section("6. Times and date-times");
        nat("LocalTime.of(9, 30)", LocalTime.of(9, 30));
        nat("LocalDateTime.of(2026, 11, 5, 23, 30).plusHours(2)", LocalDateTime.of(2026, 11, 5, 23, 30).plusHours(2));

        utv("Iso.formatIso(Values.time(9, 30))", Iso.formatIso(Values.time(9, 30)), "unique 09:30");
        utv("Operations.add(Values.dateTime(2026, 11, 5, 23, 30), Values.hours(2))",
                Operations.add(Values.dateTime(2026, 11, 5, 23, 30), Values.hours(2)), "unique 2026-11-06T01:30");
    }

    // ------------------------------------------------------------------ 7
    void parsingWithOffsets() {
        section("7. Parsing with offsets");
        OffsetDateTime plusOne = OffsetDateTime.parse("2026-11-05T14:30:00+01:00");
        OffsetDateTime z = OffsetDateTime.parse("2026-11-05T13:30:00Z");
        OffsetDateTime plusZero = OffsetDateTime.parse("2026-11-05T13:30:00+00:00");
        nat("OffsetDateTime.parse(\"...14:30:00+01:00\")", plusOne);
        nat("OffsetDateTime.parse(\"...13:30:00Z\")", z + " offset " + z.getOffset().getId());
        nat("OffsetDateTime.parse(\"...13:30:00+00:00\")", plusZero + " offset " + plusZero.getOffset().getId());
        nat("z.equals(plusZero)", z.equals(plusZero));
        note("java.time parsed Z and +00:00 to the same ZoneOffset " + z.getOffset().getId() + "; the difference is gone.");

        UtvResult a = Iso.parseIso("2026-11-05T14:30:00+01:00");
        UtvResult b = Iso.parseIso("2026-11-05T13:30:00Z");
        UtvResult c = Iso.parseIso("2026-11-05T13:30:00+00:00");
        utv("Iso.parseIso(\"...14:30:00+01:00\")", a, "unique 2026-11-05T14:30:00+01:00");
        utv("Iso.parseIso(\"...13:30:00Z\")", b, "unique 2026-11-05T13:30:00Z");
        utv("Iso.parseIso(\"...13:30:00+00:00\")", c, "unique 2026-11-05T13:30:00+00:00");
        note("Kinds: +01:00 -> " + kind(a) + "; Z -> " + kind(b) + " (UTC known, local offset unknown); +00:00 -> " + kind(c) + " (offset 0).");
        utv("Operations.compare(a, c)", Operations.compare(a.value().orElseThrow(), c.value().orElseThrow()), "unique equal");
        utv("Operations.compare(b, c)", Operations.compare(b.value().orElseThrow(), c.value().orElseThrow()), "unsupported (utv.compare.kind)");
    }

    // ------------------------------------------------------------------ 8
    void timeZones() {
        section("8. Time zones");
        ZoneId paris = zone("Europe/Paris"), ny = zone("America/New_York");
        if (paris != null && ny != null) {
            ZonedDateTime meeting = ZonedDateTime.of(LocalDateTime.of(2027, 6, 1, 9, 0), paris);
            nat("ZonedDateTime.of(2027-06-01T09:00, Europe/Paris)", meeting);
            nat("meeting.withZoneSameInstant(America/New_York)", meeting.withZoneSameInstant(ny));
            note("java.time used the JDK's tz database, version " + tzdbVersion("Europe/Paris") + " on this host.");
        }

        var meeting = Values.inZone(Values.dateTime(2027, 6, 1, 9, 0), "Europe/Paris");
        utv("Values.inZone(dateTime(2027, 6, 1, 9, 0), \"Europe/Paris\")", Iso.formatIso(meeting), "unique 2027-06-01T09:00[Europe/Paris]");
        UtvResult instant = Zones.resolve(meeting, rules);
        utv("Zones.resolve(meeting, rules)", instant, "unique 2027-06-01T07:00:00Z");
        utv("Zones.toZone(instant, \"America/New_York\", rules)", Zones.toZone(instant.value().orElseThrow(), "America/New_York", rules),
                "unique 2027-06-01T03:00:00-04:00[America/New_York]");
        note("UTV rules come from " + rules.ruleSet().path("id").asText() + " " + rules.ruleSet().path("version").asText()
                + ", loaded explicitly; the meeting stays an intention until resolved.");
    }

    // ------------------------------------------------------------------ 9
    void dstGap() {
        section("9. DST gap");
        ZoneId ny = zone("America/New_York");
        if (ny != null) {
            LocalDateTime wall = LocalDateTime.of(2026, 3, 8, 2, 30);
            ZonedDateTime got = ZonedDateTime.of(wall, ny);
            nat("ZonedDateTime.of(2026-03-08T02:30, America/New_York)", got);
            if (!got.toLocalDateTime().equals(wall)) {
                note("java.time (host tzdb " + tzdbVersion("America/New_York") + ") silently moved 02:30 to " + got.toLocalTime() + ".");
            }
        }

        var wall = Values.inZone(Values.dateTime(2026, 3, 8, 2, 30), "America/New_York");
        utv("Zones.resolve(wall, rules)", Zones.resolve(wall, rules), "none (gap)");
        UtvResult shifted = Zones.resolve(wall, rules, ResolutionPolicy.REJECT.withGap(ResolutionPolicy.Gap.SHIFT_FORWARD_BY_GAP));
        utv("... Gap.SHIFT_FORWARD_BY_GAP", shifted, "unique 2026-03-08T07:30:00Z");
        utv("Zones.toZone(shifted, \"America/New_York\", rules)", Zones.toZone(shifted.value().orElseThrow(), "America/New_York", rules),
                "unique 2026-03-08T03:30:00-04:00[America/New_York]");
    }

    // ------------------------------------------------------------------ 10
    void dstOverlap() {
        section("10. DST overlap");
        ZoneId ny = zone("America/New_York");
        if (ny != null) {
            ZonedDateTime got = ZonedDateTime.of(LocalDateTime.of(2026, 11, 1, 1, 30), ny);
            nat("ZonedDateTime.of(2026-11-01T01:30, America/New_York)", got);
            nat("got.withLaterOffsetAtOverlap()", got.withLaterOffsetAtOverlap());
            note("java.time picked offset " + got.getOffset() + " of the two without saying so (host tzdb).");
        }

        var wall = Values.inZone(Values.dateTime(2026, 11, 1, 1, 30), "America/New_York");
        utv("Zones.resolve(wall, rules)", Zones.resolve(wall, rules), "multiple 2026-11-01T05:30:00Z, 2026-11-01T06:30:00Z (ambiguous)");
        utv("... Overlap.EARLIEST", Zones.resolve(wall, rules, ResolutionPolicy.REJECT.withOverlap(ResolutionPolicy.Overlap.EARLIEST)),
                "unique 2026-11-01T05:30:00Z");
    }

    // ------------------------------------------------------------------ 11
    void timestamps() {
        section("11. Timestamps");
        Instant t1 = Instant.ofEpochSecond(1793885400L, 250_000_000);
        Instant t2 = Instant.parse("2026-11-06T13:30:00Z");
        nat("Instant.ofEpochSecond(1793885400, 250_000_000)", t1);
        nat("Duration.between(t1, t2)", Duration.between(t1, t2));

        var a = Values.instant(1793885400L, 250_000_000);
        var b = Iso.parseIso("2026-11-06T13:30:00Z").value().orElseThrow();
        utv("Values.instant(1793885400, 250_000_000)", Iso.formatIso(a), "unique 2026-11-05T13:30:00.25Z");
        utv("Operations.posixSecondsBetween(a, b)", Operations.posixSecondsBetween(a, b), "unique posixSeconds 345599/4");
        note("UTV keeps the difference as an exact fraction of POSIX seconds (86399.75).");
    }

    // ------------------------------------------------------------------ 12
    void durationsVsPeriods() {
        section("12. Durations vs periods");
        nat("Duration.ofDays(1), Period.ofDays(1)", Duration.ofDays(1) + ", " + Period.ofDays(1));
        ZoneId ny = zone("America/New_York");
        if (ny != null) {
            ZonedDateTime sat = ZonedDateTime.of(LocalDateTime.of(2026, 3, 7, 12, 0), ny);
            nat("sat.plus(Period.ofDays(1))", sat.plus(Period.ofDays(1)));
            nat("sat.plus(Duration.ofDays(1))", sat.plus(Duration.ofDays(1)));
            note("Across the DST change the two differ by an hour (host tzdb).");
        }

        utv("Values.period(0, 0, 0, 1, 0, 0, 0)", built(Values.period(0, 0, 0, 1, 0, 0, 0)), "unique period {\"days\":\"1\"}");
        utv("Values.posixSeconds(86400)", built(Values.posixSeconds(86400)), "unique posixSeconds 86400");
        var sat = Values.inZone(Values.dateTime(2026, 3, 7, 12, 0), "America/New_York");
        utv("Operations.add(sat, Values.days(1))", Operations.add(sat, Values.days(1)), "unique 2026-03-08T12:00[America/New_York]");
        var satInstant = Zones.resolve(sat, rules).value().orElseThrow();
        UtvResult later = Operations.add(satInstant, Values.posixSeconds(86400));
        utv("Operations.add(resolved sat, posixSeconds(86400))", later, "unique 2026-03-08T17:00:00Z");
        utv("... toZone America/New_York", Zones.toZone(later.value().orElseThrow(), "America/New_York", rules),
                "unique 2026-03-08T13:00:00-04:00[America/New_York]");
        utv("Operations.add(instant, SI elapsed 86400 s)", Operations.add(satInstant, json(
                "{\"schema\":\"utv/1.0\",\"kind\":\"amount\",\"value\":{\"basis\":\"elapsed\",\"seconds\":{\"numerator\":\"86400\",\"denominator\":\"1\"}}}")),
                "unsupported (utv.add.elapsed)");
        note("A period is calendar intent, posixSeconds are timeline seconds, SI seconds need a leap-second table.");
    }

    // ------------------------------------------------------------------ 13
    void ranges() {
        section("13. Ranges");
        LocalDate start = LocalDate.of(2026, 11, 1), end = LocalDate.of(2026, 12, 1), d = LocalDate.of(2026, 11, 15);
        nat("!d.isBefore(start) && d.isBefore(end)", !d.isBefore(start) && d.isBefore(end));
        LocalDate decStart = LocalDate.of(2026, 12, 1), decEnd = LocalDate.of(2027, 1, 1);
        nat("decStart.isBefore(end) && start.isBefore(decEnd)", decStart.isBefore(end) && start.isBefore(decEnd));
        note("java.time has no range type: the half-open checks are written by hand, and there is no 'unknown end'.");

        var november = Values.interval(Values.date(2026, 11, 1), Values.date(2026, 12, 1));
        var december = Values.interval(Values.date(2026, 12, 1), Values.date(2027, 1, 1));
        utv("Intervals.contains(november, 2026-11-15)", Intervals.contains(november, Values.date(2026, 11, 15)), "unique definitely");
        utv("Intervals.overlaps(november, december)", Intervals.overlaps(november, december), "unique impossible");
        var fromNov = Values.interval(Values.date(2026, 11, 1), Values.unbounded());
        utv("Intervals.contains([2026-11-01, unbounded), 3000-01-01)", Intervals.contains(fromNov, Values.date(3000, 1, 1)), "unique definitely");
        var openEnded = Values.interval(Values.date(2026, 11, 1), Values.unknown());
        utv("Intervals.contains([2026-11-01, unknown), 2027-06-01)", Intervals.contains(openEnded, Values.date(2027, 6, 1)), "unique possibly");
    }

    // ------------------------------------------------------------------ 14
    void roundTrip() {
        section("14. Round trip");
        ZoneId paris = zone("Europe/Paris");
        if (paris != null) {
            ZonedDateTime zdt = ZonedDateTime.of(LocalDateTime.of(2027, 6, 1, 9, 0), paris);
            var v = JavaTime.fromZonedDateTime(zdt);
            NativeResult<ZonedDateTime> back = JavaTime.toZonedDateTime(v);
            nat("ZonedDateTime -> UTV", Iso.formatOrNull(v));
            nat("UTV -> ZonedDateTime, equals original", back.value() + ", " + zdt.equals(back.value()));
            note("toZonedDateTime checks the asserted offset against the JDK's tz database.");
        }

        OffsetDateTime odt = OffsetDateTime.parse("2026-11-05T14:30:00+01:00");
        var asOffset = JavaTime.fromOffsetDateTime(odt, JavaTime.As.OFFSET_DATE_TIME);
        utv("JavaTime.fromOffsetDateTime(odt, As.OFFSET_DATE_TIME)", Iso.formatIso(asOffset), "unique 2026-11-05T14:30:00+01:00");
        utv("JavaTime.fromOffsetDateTime(odt, As.INSTANT)", Iso.formatIso(JavaTime.fromOffsetDateTime(odt, JavaTime.As.INSTANT)),
                "unique 2026-11-05T13:30:00Z");
        NativeResult<OffsetDateTime> backOdt = JavaTime.toOffsetDateTime(asOffset);
        check("JavaTime.toOffsetDateTime(asOffset)", backOdt.status() + " " + backOdt.value() + " lossy=" + backOdt.lossy(),
                "unique 2026-11-05T14:30+01:00 lossy=false");

        var zoned = Iso.parseIso("2027-06-01T09:00+02:00[Europe/Paris]").value().orElseThrow();
        NativeResult<OffsetDateTime> lossy = JavaTime.toOffsetDateTime(zoned);
        check("JavaTime.toOffsetDateTime(zoned Paris)", lossy.status() + " " + lossy.value() + " lossy=" + lossy.lossy(),
                "unique 2027-06-01T09:00+02:00 lossy=true");
        note(lossy.diagnostics().get(0).message());

        Date legacy = Date.from(Instant.parse("2026-11-05T13:30:00Z"));
        nat("java.util.Date.toString()", legacy);
        note("Date.toString() printed the instant in the host's default zone, " + ZoneId.systemDefault() + ".");
        utv("JavaTime.fromDate(legacy, As.LOCAL_DATE_TIME)", JavaTime.fromDate(legacy, JavaTime.As.LOCAL_DATE_TIME),
                "unsupported (utv.native.local-zone)");
        utv("JavaTime.fromDate(legacy, As.INSTANT)", JavaTime.fromDate(legacy, JavaTime.As.INSTANT), "unique 2026-11-05T13:30:00Z");
    }

    // ================================================================== printing and checking

    private void section(String title) {
        out.println();
        out.println("== " + title + " ==");
    }

    private void nat(String label, Object value) {
        out.printf("  native  %-58s %s%n", label, value);
    }

    private void note(String text) {
        out.println("  note    " + text);
    }

    /** Prints a UTV outcome and checks it against the expected rendering. */
    private void utv(String label, UtvResult r, String expected) {
        check(label, render(r), expected);
        if (r.status() != Status.UNIQUE && !r.diagnostics().isEmpty()) {
            out.println("          why: " + r.diagnostics().get(0).message());
        }
    }

    /** Prints a UTV outcome whose value varies (the clock) and checks only its status. */
    private void utvStatus(String label, UtvResult r, String expectedStatus) {
        out.printf("  utv     %-58s %s%n", label, render(r));
        if (!r.status().wire().equals(expectedStatus)) miss(expectedStatus, r.status().wire());
    }

    private void check(String label, String got, String expected) {
        out.printf("  utv     %-58s %s%n", label, got);
        if (!got.equals(expected)) miss(expected, got);
    }

    private void miss(String expected, String got) {
        out.println("  MISMATCH: expected " + expected + ", got " + got);
        mismatches++;
    }

    /** status, then the candidates (formatIso where there is one), then diagnostic codes. */
    static String render(UtvResult r) {
        StringBuilder sb = new StringBuilder(r.status().wire());
        if (!r.candidates().isEmpty()) {
            sb.append(' ').append(r.candidates().stream().map(Everyday::show).collect(Collectors.joining(", ")));
        }
        if (!r.diagnostics().isEmpty()) {
            sb.append(" (").append(r.diagnostics().stream().map(Diagnostic::code).collect(Collectors.joining(", "))).append(')');
        }
        return sb.toString();
    }

    static String show(JsonNode c) {
        if (c.isTextual()) return c.textValue();
        if (!c.has("schema")) return c.toString();
        if ("amount".equals(c.path("kind").asText())) {
            JsonNode v = c.get("value");
            if ("clock-ticks".equals(v.path("basis").asText())) {
                JsonNode s = v.get("seconds");
                String den = s.get("denominator").textValue();
                return "posixSeconds " + s.get("numerator").textValue() + (den.equals("1") ? "" : "/" + den);
            }
            if ("calendar".equals(v.path("basis").asText())) return "period " + v.get("components");
            return v.toString();
        }
        String iso = Iso.formatOrNull(c);
        return iso != null ? iso : c.toString();
    }

    private static String kind(UtvResult r) {
        JsonNode v = r.value().orElseThrow();
        JsonNode zone = v.path("value").path("zone");
        return v.get("kind").textValue() + (zone.isMissingNode() ? "" : " with zone " + zone.path("type").asText());
    }

    /** A built value shown like an operation result, so builders print the same way. */
    private static UtvResult built(JsonNode v) {
        return new UtvResult("build", Status.UNIQUE, List.of(), List.of(v));
    }

    private static JsonNode json(String text) {
        try {
            return Utv.parse(text);
        } catch (IOException e) {
            throw new IllegalArgumentException(e);
        }
    }

    /** The host's zone from the JDK tz database, or null (and a printed note) when it is missing. */
    private ZoneId zone(String id) {
        try {
            return ZoneId.of(id);
        } catch (DateTimeException e) {
            note("Zone " + id + " is not available on this host (" + e.getMessage() + "); skipping the native part.");
            return null;
        }
    }

    private static String tzdbVersion(String id) {
        try {
            return String.join(",", ZoneRulesProvider.getVersions(id).keySet());
        } catch (RuntimeException e) {
            return "unknown";
        }
    }
}
