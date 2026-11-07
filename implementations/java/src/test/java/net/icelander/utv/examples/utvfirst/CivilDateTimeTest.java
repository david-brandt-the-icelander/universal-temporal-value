package net.icelander.utv.examples.utvfirst;

import static org.junit.jupiter.api.Assertions.assertEquals;
import static org.junit.jupiter.api.Assertions.assertTrue;

import java.time.LocalDateTime;
import java.time.OffsetDateTime;
import java.time.ZoneOffset;
import net.icelander.utv.CivilDateTime;
import net.icelander.utv.FixedOffsetDateTime;
import net.icelander.utv.Outcome;
import net.icelander.utv.Status;
import net.icelander.utv.ZonedIntent;
import org.junit.jupiter.api.Nested;
import org.junit.jupiter.api.Test;

/**
 * How to use {@code CivilDateTime}, a wall-clock date and time with no zone, compared with java.time's
 * {@code LocalDateTime}.
 */
class CivilDateTimeTest {

    @Nested
    class Of {

        /**
         * Verifies that CivilDateTime.of creates a wall-clock date-time with no zone when given a date and a time.
         * Compared with: {@code LocalDateTime.of(2026, 11, 5, 14, 30)} is the same wall time, also with no zone.
         */
        @Test
        void createsAWallClockDateTime_whenGivenADateAndATime() {
            // GIVEN: 14:30 on 5 November 2026, in no particular zone
            int givenYear = 2026;
            int givenMonth = 11;
            int givenDay = 5;
            int givenHour = 14;
            int givenMinute = 30;

            // WHEN
            CivilDateTime actualDateTime = CivilDateTime.of(givenYear, givenMonth, givenDay, givenHour, givenMinute);

            // EXPECTATIONS: the wall time java.time holds, written the same way, without an offset or a zone
            LocalDateTime expectedDateTime = LocalDateTime.of(2026, 11, 5, 14, 30);
            String expectedText = LocalDateTime.of(2026, 11, 5, 14, 30).toString();

            // THEN
            assertEquals(expectedDateTime, actualDateTime.toLocalDateTime().single());
            assertEquals(expectedText, actualDateTime.toString());
        }
    }

    @Nested
    class TryOf {

        /**
         * Verifies that CivilDateTime.tryOf returns unsupported when the second is a leap second, 60.
         * Compared with: {@code LocalDateTime.of(2016, 12, 31, 23, 59, 60)} throws a {@code DateTimeException}, as if
         * the second could never exist.
         */
        @Test
        void returnsUnsupported_whenTheSecondIsALeapSecond() {
            // GIVEN: 23:59:60 on 31 December 2016, a real leap second
            int givenYear = 2016;
            int givenMonth = 12;
            int givenDay = 31;
            int givenHour = 23;
            int givenMinute = 59;
            int givenSecond = 60;
            int givenNanosecond = 0;

            // WHEN
            Outcome<CivilDateTime> actualOutcome =
                    CivilDateTime.tryOf(givenYear, givenMonth, givenDay, givenHour, givenMinute, givenSecond, givenNanosecond);

            // EXPECTATIONS: unsupported, not invalid: judging it needs a leap-second table
            Status expectedStatus = Status.UNSUPPORTED;
            String expectedCode = "leap-second";

            // THEN
            assertEquals(expectedStatus, actualOutcome.status());
            assertTrue(actualOutcome.diagnostics().stream().anyMatch(d -> d.code().equals(expectedCode)), actualOutcome.why());
        }
    }

    @Nested
    class AddHours {

        // BEGIN datetime-plus-hours
        /**
         * Verifies that CivilDateTime.addHours carries into the next day when the hours pass midnight.
         * Compared with: {@code LocalDateTime.plusHours} gives the same wall time; {@code LocalTime.plusHours} would
         * lose the day.
         */
        @Test
        void carriesIntoTheNextDay_whenTheHoursPassMidnight() {
            // GIVEN: two hours, past midnight
            int givenHours = 2;

            // SYSTEM UNDER TEST: 23:30 on 5 November 2026
            CivilDateTime sut = CivilDateTime.of(2026, 11, 5, 23, 30);

            // WHEN
            CivilDateTime actualDateTime = sut.addHours(givenHours);

            // EXPECTATIONS: the wall time java.time computes for the same addition
            LocalDateTime expectedDateTime = LocalDateTime.of(2026, 11, 5, 23, 30).plusHours(2);

            // THEN
            assertEquals(expectedDateTime, actualDateTime.toLocalDateTime().single());
        }
        // END datetime-plus-hours
    }

    @Nested
    class InZone {

        /**
         * Verifies that CivilDateTime.inZone keeps the wall time and the zone name as an intention when given a zone id.
         * Compared with: {@code ZonedDateTime.of} fixes an offset straight away, from the host's tz database as it is
         * today.
         */
        @Test
        void keepsTheWallTimeAndTheZone_whenGivenAZoneId() {
            // GIVEN: the Paris time zone
            String givenZoneId = "Europe/Paris";

            // SYSTEM UNDER TEST: 09:00 on 1 June 2027
            CivilDateTime sut = CivilDateTime.of(2027, 6, 1, 9, 0);

            // WHEN
            ZonedIntent actualMeeting = sut.inZone(givenZoneId);

            // EXPECTATIONS: the wall time and the zone, with no offset decided yet
            String expectedText = "2027-06-01T09:00[Europe/Paris]";

            // THEN
            assertEquals(expectedText, actualMeeting.toString());
        }
    }

    @Nested
    class AtOffset {

        /**
         * Verifies that CivilDateTime.atOffset pins the wall time to a fixed UTC offset when given offset seconds.
         * Compared with: {@code OffsetDateTime.of(2026, 11, 5, 14, 30, 0, 0, ZoneOffset.ofHours(1))} is the same value.
         */
        @Test
        void pinsTheWallTimeToTheOffset_whenGivenOffsetSeconds() {
            // GIVEN: one hour east of UTC
            int givenOffsetSeconds = 3600;

            // SYSTEM UNDER TEST: 14:30 on 5 November 2026
            CivilDateTime sut = CivilDateTime.of(2026, 11, 5, 14, 30);

            // WHEN
            FixedOffsetDateTime actualDateTime = sut.atOffset(givenOffsetSeconds);

            // EXPECTATIONS: the value java.time's OffsetDateTime holds
            OffsetDateTime expectedDateTime = OffsetDateTime.of(2026, 11, 5, 14, 30, 0, 0, ZoneOffset.ofHours(1));

            // THEN
            assertEquals(expectedDateTime, actualDateTime.toOffsetDateTime().single());
        }
    }
}
