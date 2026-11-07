package net.icelander.utv.examples.nativefirst.time;

import static org.junit.jupiter.api.Assertions.assertEquals;

import java.time.Duration;
import java.time.LocalDateTime;
import java.time.ZoneId;
import java.time.ZonedDateTime;
import net.icelander.utv.CivilDateTime;
import net.icelander.utv.Gap;
import net.icelander.utv.Overlap;
import net.icelander.utv.PosixSeconds;
import net.icelander.utv.TemporalContext;
import net.icelander.utv.ZonedIntent;
import net.icelander.utv.examples.Fixtures;
import org.junit.jupiter.api.Nested;
import org.junit.jupiter.api.Test;

/**
 * What java.time's {@code java.time.ZonedDateTime} does, compared with UTV's {@code ZonedIntent},
 * {@code Instant.inZone} and {@code TemporalContext.compare}.
 *
 * <p>{@code ZonedDateTime} reads the host's tz database, so these tests need one that knows the 2026–2027 rules of New
 * York and Paris. UTV reads only the pinned demo rules it is given.
 */
class ZonedDateTimeTest {

    @Nested
    class Of {

        /**
         * Verifies that ZonedDateTime.of names the instant when the wall time happens exactly once.
         * Compared with: {@code ZonedIntent.resolve} finds the same instant in the pinned rules.
         */
        @Test
        void namesTheInstant_whenTheWallTimeHappensOnce() {
            // GIVEN: a meeting at 09:00 on 1 June 2027
            LocalDateTime givenWallTime = LocalDateTime.of(2027, 6, 1, 9, 0);

            // SETUP: Paris, from the host's tz database
            ZoneId envParis = ZoneId.of("Europe/Paris");

            // WHEN
            ZonedDateTime actualMeeting = ZonedDateTime.of(givenWallTime, envParis);

            // EXPECTATIONS: the instant UTV resolves from the pinned rules
            TemporalContext tmpContext = TemporalContext.with(Fixtures.demoZoneRules());
            net.icelander.utv.Instant tmpInstant = CivilDateTime.of(2027, 6, 1, 9, 0).inZone("Europe/Paris").resolve(tmpContext).single();
            java.time.Instant expectedInstant = tmpInstant.toJavaInstant().single();

            // THEN
            assertEquals(expectedInstant, actualMeeting.toInstant());
        }

        /**
         * Verifies that ZonedDateTime.of moves the wall time forward by the gap, silently, when the wall time falls in a
         * daylight-saving gap.
         * Compared with: {@code ZonedIntent.resolve} returns none (gap), and gives this instant only when asked with
         * {@code Gap.SHIFT_FORWARD}.
         */
        @Test
        void movesTheWallTimeForward_whenTheWallTimeFallsInAGap() {
            // GIVEN: 02:30 on 8 March 2026, when New York's clocks jump from 02:00 to 03:00
            LocalDateTime givenWallTime = LocalDateTime.of(2026, 3, 8, 2, 30);

            // SETUP: New York, from the host's tz database
            ZoneId envNewYork = ZoneId.of("America/New_York");

            // WHEN
            ZonedDateTime actualDateTime = ZonedDateTime.of(givenWallTime, envNewYork);

            // EXPECTATIONS: the instant UTV picks only when asked to shift forward: 03:30 daylight time
            TemporalContext tmpContext = TemporalContext.with(Fixtures.demoZoneRules());
            ZonedIntent tmpWallTime = CivilDateTime.of(2026, 3, 8, 2, 30).inZone("America/New_York");
            java.time.Instant expectedInstant = tmpWallTime.resolve(tmpContext, Gap.SHIFT_FORWARD).single().toJavaInstant().single();

            // THEN
            assertEquals(expectedInstant, actualDateTime.toInstant());
        }

        /**
         * Verifies that ZonedDateTime.of picks the earlier, daylight-time instant, silently, when the wall time falls in
         * an overlap.
         * Compared with: {@code ZonedIntent.resolve} returns both instants (ambiguous); this one is what
         * {@code Overlap.EARLIEST} picks.
         */
        @Test
        void picksTheEarlierInstant_whenTheWallTimeFallsInAnOverlap() {
            // GIVEN: 01:30 on 1 November 2026, when New York's clocks go back from 02:00 to 01:00
            LocalDateTime givenWallTime = LocalDateTime.of(2026, 11, 1, 1, 30);

            // SETUP: New York, from the host's tz database
            ZoneId envNewYork = ZoneId.of("America/New_York");

            // WHEN
            ZonedDateTime actualDateTime = ZonedDateTime.of(givenWallTime, envNewYork);

            // EXPECTATIONS: the instant UTV picks only when asked for the earliest
            TemporalContext tmpContext = TemporalContext.with(Fixtures.demoZoneRules());
            ZonedIntent tmpWallTime = CivilDateTime.of(2026, 11, 1, 1, 30).inZone("America/New_York");
            java.time.Instant expectedInstant = tmpWallTime.resolve(tmpContext, Overlap.EARLIEST).single().toJavaInstant().single();

            // THEN
            assertEquals(expectedInstant, actualDateTime.toInstant());
        }
    }

    @Nested
    class WithLaterOffsetAtOverlap {

        /**
         * Verifies that ZonedDateTime.withLaterOffsetAtOverlap moves to the later, standard-time instant when the wall
         * time falls in an overlap.
         * Compared with: {@code ZonedIntent.resolve} with {@code Overlap.LATEST} picks the same instant.
         */
        @Test
        void picksTheLaterInstant_whenTheWallTimeFallsInAnOverlap() {
            // SYSTEM UNDER TEST: 01:30 on 1 November 2026 in New York, from the host's tz database, as java.time first picks it
            ZonedDateTime sut = ZonedDateTime.of(LocalDateTime.of(2026, 11, 1, 1, 30), ZoneId.of("America/New_York"));

            // WHEN
            ZonedDateTime actualDateTime = sut.withLaterOffsetAtOverlap();

            // EXPECTATIONS: the instant UTV picks when asked for the latest
            TemporalContext tmpContext = TemporalContext.with(Fixtures.demoZoneRules());
            ZonedIntent tmpWallTime = CivilDateTime.of(2026, 11, 1, 1, 30).inZone("America/New_York");
            java.time.Instant expectedInstant = tmpWallTime.resolve(tmpContext, Overlap.LATEST).single().toJavaInstant().single();

            // THEN
            assertEquals(expectedInstant, actualDateTime.toInstant());
        }
    }

    @Nested
    class WithZoneSameInstant {

        /**
         * Verifies that ZonedDateTime.withZoneSameInstant returns the wall time in the other zone when given a zone.
         * Compared with: {@code Instant.inZone} gives the same wall time and offset from the pinned rules.
         */
        @Test
        void returnsTheWallTimeInTheOtherZone_whenGivenAZone() {
            // GIVEN: New York, from the host's tz database
            ZoneId givenZone = ZoneId.of("America/New_York");

            // SYSTEM UNDER TEST: a meeting at 09:00 on 1 June 2027 in Paris, from the host's tz database
            ZonedDateTime sut = ZonedDateTime.of(LocalDateTime.of(2027, 6, 1, 9, 0), ZoneId.of("Europe/Paris"));

            // WHEN
            ZonedDateTime actualDateTime = sut.withZoneSameInstant(givenZone);

            // EXPECTATIONS: the wall time UTV computes from the pinned rules, 03:00 daylight time
            TemporalContext tmpContext = TemporalContext.with(Fixtures.demoZoneRules());
            net.icelander.utv.Instant tmpStart = CivilDateTime.of(2027, 6, 1, 9, 0).inZone("Europe/Paris").resolve(tmpContext).single();
            ZonedDateTime expectedDateTime = tmpStart.inZone("America/New_York", tmpContext).single().toZonedDateTime().single();

            // THEN
            assertEquals(expectedDateTime, actualDateTime);
        }
    }

    @Nested
    class PlusDays {

        /**
         * Verifies that ZonedDateTime.plusDays keeps the wall time when the day crosses a daylight-saving change.
         * Compared with: {@code ZonedIntent.addDays} keeps noon too.
         */
        @Test
        void keepsTheWallTime_whenTheDayCrossesADstChange() {
            // GIVEN: one calendar day
            long givenDays = 1;

            // SYSTEM UNDER TEST: noon on 7 March 2026 in New York, from the host's tz database
            ZonedDateTime sut = ZonedDateTime.of(LocalDateTime.of(2026, 3, 7, 12, 0), ZoneId.of("America/New_York"));

            // WHEN
            ZonedDateTime actualNextNoon = sut.plusDays(givenDays);

            // EXPECTATIONS: the wall time UTV reaches
            ZonedIntent tmpNextNoon = CivilDateTime.of(2026, 3, 7, 12, 0).inZone("America/New_York").addDays(1);
            LocalDateTime expectedWallTime = tmpNextNoon.dateTime().toLocalDateTime().single();

            // THEN
            assertEquals(expectedWallTime, actualNextNoon.toLocalDateTime());
        }
    }

    @Nested
    class Plus {

        /**
         * Verifies that ZonedDateTime.plus moves the wall time to 13:00 when given a Duration of one day across a
         * daylight-saving change.
         * Compared with: {@code Instant.add(PosixSeconds.of(86_400))} lands on the same moment; {@code ZonedIntent.addDays}
         * keeps noon instead.
         */
        @Test
        void movesTheWallTimeTo13_whenGivenADurationOfOneDay() {
            // GIVEN: 86,400 seconds
            Duration givenDuration = Duration.ofDays(1);

            // SYSTEM UNDER TEST: noon on 7 March 2026 in New York, from the host's tz database
            ZonedDateTime sut = ZonedDateTime.of(LocalDateTime.of(2026, 3, 7, 12, 0), ZoneId.of("America/New_York"));

            // WHEN
            ZonedDateTime actualDayLater = sut.plus(givenDuration);

            // EXPECTATIONS: the moment UTV reaches by adding 86,400 POSIX seconds
            TemporalContext tmpContext = TemporalContext.with(Fixtures.demoZoneRules());
            net.icelander.utv.Instant tmpNoon = CivilDateTime.of(2026, 3, 7, 12, 0).inZone("America/New_York").resolve(tmpContext).single();
            java.time.Instant expectedInstant = tmpNoon.add(PosixSeconds.of(86_400)).toJavaInstant().single();

            // THEN
            assertEquals(expectedInstant, actualDayLater.toInstant());
        }
    }

    @Nested
    class IsBefore {

        /**
         * Verifies that ZonedDateTime.isBefore returns true when both wall times fall in the same overlap.
         * Compared with: {@code TemporalContext.compare} returns multiple (ambiguous): 01:30 standard time is after
         * 01:45 daylight time, so either could come first. java.time gave both the earlier offset when it built them.
         */
        @Test
        void returnsTrue_whenBothWallTimesFallInAnOverlap() {
            // GIVEN: a stand-up at 01:45, on the night New York's clocks go back from 02:00 to 01:00
            ZonedDateTime givenStandup = ZonedDateTime.of(LocalDateTime.of(2026, 11, 1, 1, 45), ZoneId.of("America/New_York"));

            // SYSTEM UNDER TEST: a call at 01:30 the same night
            ZonedDateTime sut = ZonedDateTime.of(LocalDateTime.of(2026, 11, 1, 1, 30), ZoneId.of("America/New_York"));

            // WHEN
            boolean actualComesFirst = sut.isBefore(givenStandup);

            // EXPECTATIONS: a plain yes, where UTV says it depends
            boolean expectedComesFirst = true;

            // THEN
            assertEquals(expectedComesFirst, actualComesFirst);
        }
    }

    @Nested
    class Now {

        /**
         * Verifies that ZonedDateTime.now returns a value in the host's default zone when given no clock.
         * Compared with: UTV never reads the clock or the host's zone; the reading goes in as an {@code Instant} and the
         * zone is always named.
         *
         * <p>This test reads the real clock and the host's default zone on purpose: the zone does not depend on the time
         * read, so the result is the same on every run. If it ever flakes, that is a finding about ZonedDateTime.now,
         * not noise.
         */
        @Test
        void returnsTheHostsDefaultZone_whenGivenNoClock() {
            // WHEN
            ZonedDateTime actualNow = ZonedDateTime.now();

            // EXPECTATIONS: a value tied to this machine's zone
            ZoneId expectedZone = ZoneId.systemDefault();

            // THEN
            assertEquals(expectedZone, actualNow.getZone());
        }
    }
}
