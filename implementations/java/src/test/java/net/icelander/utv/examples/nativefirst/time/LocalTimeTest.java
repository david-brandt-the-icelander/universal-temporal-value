package net.icelander.utv.examples.nativefirst.time;

import static org.junit.jupiter.api.Assertions.assertEquals;

import java.time.LocalTime;
import net.icelander.utv.CivilDateTime;
import net.icelander.utv.CivilTime;
import org.junit.jupiter.api.Nested;
import org.junit.jupiter.api.Test;

/**
 * What java.time's {@code java.time.LocalTime} does, compared with UTV's {@code CivilTime}.
 */
class LocalTimeTest {

    @Nested
    class ToString {

        /**
         * Verifies that LocalTime.toString writes no seconds when no seconds were given.
         * Compared with: {@code CivilTime.of(9, 30)} writes the same text, {@code 09:30}.
         */
        @Test
        void writesNoSeconds_whenNoSecondsWereGiven() {
            // SYSTEM UNDER TEST: half past nine, to the minute
            LocalTime sut = LocalTime.of(9, 30);

            // WHEN
            String actualText = sut.toString();

            // EXPECTATIONS: UTV's text for the same time
            String expectedText = CivilTime.of(9, 30).toString();

            // THEN
            assertEquals(expectedText, actualText);
        }

        /**
         * Verifies that LocalTime.toString drops the seconds when they were given as zero.
         * Compared with: {@code CivilTime.of(9, 30, 0)} remembers that a second was given, and writes {@code 09:30:00}.
         */
        @Test
        void dropsTheSeconds_whenTheyWereGivenAsZero() {
            // SYSTEM UNDER TEST: half past nine, to the second
            LocalTime sut = LocalTime.of(9, 30, 0);

            // WHEN
            String actualText = sut.toString();

            // EXPECTATIONS: the text UTV writes only for a time given to the minute
            String expectedText = CivilTime.of(9, 30).toString();

            // THEN
            assertEquals(expectedText, actualText);
        }
    }

    @Nested
    class PlusHours {

        /**
         * Verifies that LocalTime.plusHours wraps around midnight and loses the day when the hours pass midnight.
         * Compared with: {@code CivilTime} has no arithmetic at all; {@code CivilDateTime.addHours} carries into the next
         * day and keeps the date.
         */
        @Test
        void wrapsAndLosesTheDay_whenTheHoursPassMidnight() {
            // GIVEN: two hours, past midnight
            long givenHours = 2;

            // SYSTEM UNDER TEST: 23:30
            LocalTime sut = LocalTime.of(23, 30);

            // WHEN
            LocalTime actualTime = sut.plusHours(givenHours);

            // EXPECTATIONS: the time of day UTV reaches, without the next date it also reaches
            LocalTime expectedTime = CivilDateTime.of(2026, 11, 5, 23, 30).addHours(2).time().toLocalTime().single();

            // THEN
            assertEquals(expectedTime, actualTime);
        }
    }
}
