package net.icelander.utv.examples.utvfirst;

import static org.junit.jupiter.api.Assertions.assertEquals;
import static org.junit.jupiter.api.Assertions.assertTrue;

import java.time.Duration;
import java.time.OffsetDateTime;
import java.time.ZoneId;
import java.time.ZoneOffset;
import java.time.ZonedDateTime;
import net.icelander.utv.CivilDateTime;
import net.icelander.utv.ElapsedSeconds;
import net.icelander.utv.Instant;
import net.icelander.utv.Outcome;
import net.icelander.utv.PosixSeconds;
import net.icelander.utv.Status;
import net.icelander.utv.TemporalContext;
import net.icelander.utv.ZonedIntent;
import net.icelander.utv.examples.Fixtures;
import org.junit.jupiter.api.Nested;
import org.junit.jupiter.api.Test;

/**
 * How to use {@code Instant}, an exact moment on the POSIX timescale with no calendar and no zone, compared with
 * java.time's {@code Instant} and {@code Duration}. {@code Instant} below is UTV's; java.time's is written in full.
 *
 * <p>UTV reads only the pinned demo rules. The java.time side of the zone expectations reads the host's tz database,
 * so these tests need one that knows the 2026–2027 rules of New York.
 */
class InstantTest {

    @Nested
    class From {

        // BEGIN now
        /**
         * Verifies that Instant.from takes a clock reading as an input when the program reads the clock.
         * Compared with: {@code java.time.Instant.now()} reads the clock itself; UTV never does, so tests can fix "now".
         */
        @Test
        void returnsTheSameMoment_whenGivenAClockReading() {
            // GIVEN: a clock reading, as java.time.Instant.now() would return it
            java.time.Instant givenReading = java.time.Instant.parse("2026-11-05T13:30:00.123456789Z");

            // WHEN
            Instant actualNow = Instant.from(givenReading);

            // EXPECTATIONS: the same moment, counted as java.time counts it: seconds since 1970 and nanoseconds
            Instant expectedNow = Instant.fromUnixSeconds(givenReading.getEpochSecond(), givenReading.getNano());

            // THEN
            assertEquals(expectedNow, actualNow);
        }
        // END now
    }

    @Nested
    class FromUnixSeconds {

        /**
         * Verifies that Instant.fromUnixSeconds returns the Unix epoch when given zero seconds.
         * Compared with: {@code java.time.Instant.ofEpochSecond(0)} is the same moment and writes the same text.
         */
        @Test
        void returnsTheEpoch_whenGivenZero() {
            // GIVEN: zero seconds since 1970-01-01T00:00:00Z
            long givenSeconds = 0;

            // WHEN
            Instant actualInstant = Instant.fromUnixSeconds(givenSeconds);

            // EXPECTATIONS: the moment java.time returns for the same count, written in UTC
            Instant expectedInstant = Instant.from(java.time.Instant.ofEpochSecond(0));
            String expectedText = java.time.Instant.ofEpochSecond(0).toString();

            // THEN
            assertEquals(expectedInstant, actualInstant);
            assertEquals(expectedText, actualInstant.toString());
        }

        /**
         * Verifies that Instant.fromUnixSeconds keeps all nine fractional digits when given nanoseconds.
         * Compared with: {@code java.time.Instant.ofEpochSecond(seconds, nanos)} keeps all nine too.
         */
        @Test
        void keepsEveryNanosecond_whenGivenNanoseconds() {
            // GIVEN: a moment on 4 November 2026, to the nanosecond
            long givenSeconds = 1_793_795_400;
            long givenNanoseconds = 123_456_789;

            // WHEN
            Instant actualInstant = Instant.fromUnixSeconds(givenSeconds, givenNanoseconds);

            // EXPECTATIONS: all nine digits, as java.time writes them
            String expectedText = java.time.Instant.ofEpochSecond(1_793_795_400, 123_456_789).toString();
            int expectedNanoseconds = java.time.Instant.ofEpochSecond(1_793_795_400, 123_456_789).getNano();

            // THEN
            assertEquals(expectedText, actualInstant.toString());
            assertEquals(expectedNanoseconds, actualInstant.nanoseconds());
        }

        /**
         * Verifies that Instant.fromUnixSeconds keeps a fraction of a second when given a quarter second.
         * Compared with: {@code java.time.Instant} holds the same moment, but writes it {@code 13:30:00.250Z}, in groups
         * of three digits.
         */
        @Test
        void keepsTheFraction_whenGivenAQuarterSecond() {
            // GIVEN: 13:30:00.25 on 5 November 2026
            long givenSeconds = 1_793_885_400;
            long givenNanoseconds = 250_000_000;

            // WHEN
            Instant actualInstant = Instant.fromUnixSeconds(givenSeconds, givenNanoseconds);

            // EXPECTATIONS: the moment java.time holds, with the quarter second written as short as it can be
            Instant expectedInstant = Instant.from(java.time.Instant.ofEpochMilli(1_793_885_400_250L));
            String expectedText = "2026-11-05T13:30:00.25Z";

            // THEN
            assertEquals(expectedInstant, actualInstant);
            assertEquals(expectedText, actualInstant.toString());
        }
    }

    @Nested
    class Until {

        // BEGIN timestamps
        /**
         * Verifies that Instant.until returns the exact number of seconds, as a fraction, when the instants are a
         * fraction of a second apart.
         * Compared with: {@code Duration.between} gives the same amount, exact to the nanosecond.
         */
        @Test
        void returnsAnExactFraction_whenTheInstantsAreAFractionOfASecondApart() {
            // GIVEN: one hour, 0.75 seconds later
            Instant givenLater = Instant.fromUnixSeconds(1_793_889_001);

            // SYSTEM UNDER TEST: 13:30:00.25 on 5 November 2026
            Instant sut = Instant.fromUnixSeconds(1_793_885_400, 250_000_000);

            // WHEN
            PosixSeconds actualSeconds = sut.until(givenLater);

            // EXPECTATIONS: exactly 14403/4 seconds, the amount java.time measures
            Duration tmpNative = Duration.between(java.time.Instant.ofEpochMilli(1_793_885_400_250L), java.time.Instant.ofEpochSecond(1_793_889_001));
            PosixSeconds expectedSeconds = PosixSeconds.from(tmpNative);
            String expectedText = "3600.75 s (POSIX)";

            // THEN
            assertEquals(expectedSeconds, actualSeconds);
            assertEquals(expectedText, actualSeconds.toString());
        }
        // END timestamps

        /**
         * Verifies that Instant.until returns 23 hours when the instants are noon on both sides of a spring-forward
         * night.
         * Compared with: {@code Duration.between} agrees once both noons have their offsets; the calendar day is 23
         * hours long.
         */
        @Test
        void returns23Hours_whenTheDayBetweenLosesAnHourToDst() {
            // GIVEN: noon on 8 March 2026 in New York, daylight time
            Instant givenNextNoon = Instant.from(OffsetDateTime.of(2026, 3, 8, 12, 0, 0, 0, ZoneOffset.ofHours(-4)).toInstant());

            // SYSTEM UNDER TEST: noon on 7 March 2026 in New York, standard time
            Instant sut = Instant.from(OffsetDateTime.of(2026, 3, 7, 12, 0, 0, 0, ZoneOffset.ofHours(-5)).toInstant());

            // WHEN
            PosixSeconds actualSeconds = sut.until(givenNextNoon);

            // EXPECTATIONS: 82,800 seconds, as java.time's Duration counts them
            Duration tmpNative = Duration.between(
                    OffsetDateTime.of(2026, 3, 7, 12, 0, 0, 0, ZoneOffset.ofHours(-5)),
                    OffsetDateTime.of(2026, 3, 8, 12, 0, 0, 0, ZoneOffset.ofHours(-4)));
            PosixSeconds expectedSeconds = PosixSeconds.from(tmpNative);

            // THEN
            assertEquals(expectedSeconds, actualSeconds);
        }
    }

    @Nested
    class Add {

        /**
         * Verifies that Instant.add with POSIX seconds moves the instant by exactly those seconds when given
         * PosixSeconds.
         * Compared with: {@code java.time.Instant.plusSeconds(86_400)} lands on the same moment: 13:00 in New York,
         * not noon.
         */
        @Test
        void movesByExactlyThoseSeconds_whenGivenPosixSeconds() {
            // GIVEN: 86,400 POSIX seconds, one nominal day
            PosixSeconds givenSeconds = PosixSeconds.of(86_400);

            // SYSTEM UNDER TEST: noon on 7 March 2026 in New York, the day before clocks spring forward
            Instant sut = Instant.from(OffsetDateTime.of(2026, 3, 7, 12, 0, 0, 0, ZoneOffset.ofHours(-5)).toInstant());

            // WHEN
            Instant actualInstant = sut.add(givenSeconds);

            // EXPECTATIONS: the moment java.time computes for the same addition
            java.time.Instant tmpNative = OffsetDateTime.of(2026, 3, 7, 12, 0, 0, 0, ZoneOffset.ofHours(-5)).toInstant().plusSeconds(86_400);
            Instant expectedInstant = Instant.from(tmpNative);

            // THEN
            assertEquals(expectedInstant, actualInstant);
        }

        // BEGIN day-vs-24h
        /**
         * Verifies that Instant.add with SI seconds returns unsupported when no leap-second table is available.
         * Compared with: {@code Duration} has one kind of second, so java.time cannot tell SI seconds from POSIX
         * seconds.
         */
        @Test
        void returnsUnsupported_whenGivenSiSecondsWithoutALeapSecondTable() {
            // GIVEN: 86,400 SI seconds, as a physicist measures a day
            ElapsedSeconds givenSeconds = ElapsedSeconds.of(86_400);

            // SYSTEM UNDER TEST: noon on 7 March 2026 in New York
            Instant sut = Instant.from(OffsetDateTime.of(2026, 3, 7, 12, 0, 0, 0, ZoneOffset.ofHours(-5)).toInstant());

            // WHEN
            Outcome<Instant> actualOutcome = sut.add(givenSeconds);

            // EXPECTATIONS: unsupported, because SI seconds on a POSIX timescale need leap seconds
            Status expectedStatus = Status.UNSUPPORTED;
            String expectedCode = "needs-leap-seconds";

            // THEN
            assertEquals(expectedStatus, actualOutcome.status());
            assertTrue(actualOutcome.diagnostics().stream().anyMatch(d -> d.code().equals(expectedCode)), actualOutcome.why());
        }
        // END day-vs-24h
    }

    @Nested
    class InZone {

        // BEGIN meeting-in-paris
        /**
         * Verifies that Instant.inZone returns the local wall time and offset when the zone has rules for the instant.
         * Compared with: {@code java.time.Instant.atZone} gives the same wall time from the host's tz database.
         */
        @Test
        void returnsTheLocalWallTime_whenTheZoneHasRules() {
            // GIVEN: New York
            String givenZoneId = "America/New_York";

            // SETUP: the pinned demo zone rules, never the host's tz database
            TemporalContext envContext = TemporalContext.with(Fixtures.demoZoneRules());

            // SYSTEM UNDER TEST: 07:00 UTC on 1 June 2027, when the meeting starts in Paris
            Instant sut = Instant.from(java.time.Instant.parse("2027-06-01T07:00:00Z"));

            // WHEN
            Outcome<ZonedIntent> actualOutcome = sut.inZone(givenZoneId, envContext);

            // EXPECTATIONS: the wall time java.time computes, with the offset UTV asserts alongside it
            ZonedDateTime tmpNative = java.time.Instant.parse("2027-06-01T07:00:00Z").atZone(ZoneId.of("America/New_York"));
            CivilDateTime expectedWallTime = CivilDateTime.from(tmpNative.toLocalDateTime());
            String expectedText = "2027-06-01T03:00:00-04:00[America/New_York]";

            // THEN
            assertEquals(expectedWallTime, actualOutcome.single().dateTime());
            assertEquals(expectedText, actualOutcome.single().toString());
        }
        // END meeting-in-paris
    }
}
