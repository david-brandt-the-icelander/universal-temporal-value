package net.icelander.utv.examples.nativefirst.time;

import static org.junit.jupiter.api.Assertions.assertEquals;
import static org.junit.jupiter.api.Assertions.assertThrows;

import java.time.Clock;
import java.time.DateTimeException;
import java.time.LocalDateTime;
import java.time.ZoneId;
import net.icelander.utv.CivilDateTime;
import net.icelander.utv.TemporalContext;
import net.icelander.utv.examples.Fixtures;
import org.junit.jupiter.api.Nested;
import org.junit.jupiter.api.Test;

/**
 * What java.time's {@code java.time.LocalDateTime} does, compared with UTV's {@code CivilDateTime}.
 *
 * <p>{@code LocalDateTime.now(clock)} reads the clock's zone from the host's tz database, so that test needs one that
 * knows the 2026 rules of Paris. UTV reads only the pinned demo rules it is given.
 */
class LocalDateTimeTest {

    @Nested
    class Of {

        /**
         * Verifies that LocalDateTime.of creates a wall time with no zone when given a date and a time.
         * Compared with: {@code CivilDateTime.of(2026, 11, 5, 14, 30)} is the same wall time, also with no zone.
         */
        @Test
        void createsAWallTimeWithNoZone_whenGivenADateAndATime() {
            // GIVEN: 14:30 on 5 November 2026
            int givenYear = 2026;
            int givenMonth = 11;
            int givenDay = 5;
            int givenHour = 14;
            int givenMinute = 30;

            // WHEN
            LocalDateTime actualDateTime = LocalDateTime.of(givenYear, givenMonth, givenDay, givenHour, givenMinute);

            // EXPECTATIONS: the wall time UTV holds, written the same way
            LocalDateTime expectedDateTime = CivilDateTime.of(2026, 11, 5, 14, 30).toLocalDateTime().single();
            String expectedText = CivilDateTime.of(2026, 11, 5, 14, 30).toString();

            // THEN
            assertEquals(expectedDateTime, actualDateTime);
            assertEquals(expectedText, actualDateTime.toString());
        }

        /**
         * Verifies that LocalDateTime.of throws a DateTimeException when the second is a leap second, 60.
         * Compared with: {@code CivilDateTime.tryOf(2016, 12, 31, 23, 59, 60, 0)} returns unsupported (leap-second):
         * the second may well exist, and judging it needs a leap-second table.
         */
        @Test
        void throws_whenTheSecondIsALeapSecond() {
            // GIVEN: 23:59:60 on 31 December 2016, a real leap second
            int givenSecond = 60;

            // WHEN
            DateTimeException actualException =
                    assertThrows(DateTimeException.class, () -> LocalDateTime.of(2016, 12, 31, 23, 59, givenSecond));

            // EXPECTATIONS: the second is treated as impossible
            String expectedMessage = "Invalid value for SecondOfMinute (valid values 0 - 59): 60";

            // THEN
            assertEquals(expectedMessage, actualException.getMessage());
        }
    }

    @Nested
    class PlusHours {

        /**
         * Verifies that LocalDateTime.plusHours carries into the next day when the hours pass midnight.
         * Compared with: {@code CivilDateTime.addHours} gives the same wall time.
         */
        @Test
        void carriesIntoTheNextDay_whenTheHoursPassMidnight() {
            // GIVEN: two hours, past midnight
            long givenHours = 2;

            // SYSTEM UNDER TEST: 23:30 on 5 November 2026
            LocalDateTime sut = LocalDateTime.of(2026, 11, 5, 23, 30);

            // WHEN
            LocalDateTime actualDateTime = sut.plusHours(givenHours);

            // EXPECTATIONS: the wall time UTV computes for the same addition
            LocalDateTime expectedDateTime = CivilDateTime.of(2026, 11, 5, 23, 30).addHours(2).toLocalDateTime().single();

            // THEN
            assertEquals(expectedDateTime, actualDateTime);
        }
    }

    @Nested
    class Now {

        /**
         * Verifies that LocalDateTime.now returns the wall time in the clock's zone when given a clock.
         * Compared with: UTV never reads a clock or a zone on its own; the reading goes in as an {@code Instant} and
         * {@code Instant.inZone} names the zone. Without a clock, {@code LocalDateTime.now()} takes the zone from
         * {@code ZoneId.systemDefault()}, and the result keeps no trace of it.
         */
        @Test
        void returnsTheWallTimeInTheClocksZone_whenGivenAClock() {
            // SETUP: a clock stopped at 13:30 UTC on 5 November 2026, in Paris
            Clock envClock = Clock.fixed(java.time.Instant.parse("2026-11-05T13:30:00Z"), ZoneId.of("Europe/Paris"));

            // WHEN
            LocalDateTime actualNow = LocalDateTime.now(envClock);

            // EXPECTATIONS: the wall time UTV computes for the same instant in Paris
            TemporalContext tmpContext = TemporalContext.with(Fixtures.demoZoneRules());
            net.icelander.utv.Instant tmpReading = net.icelander.utv.Instant.from(java.time.Instant.parse("2026-11-05T13:30:00Z"));
            LocalDateTime expectedNow = tmpReading.inZone("Europe/Paris", tmpContext).single().dateTime().toLocalDateTime().single();

            // THEN
            assertEquals(expectedNow, actualNow);
        }
    }
}
