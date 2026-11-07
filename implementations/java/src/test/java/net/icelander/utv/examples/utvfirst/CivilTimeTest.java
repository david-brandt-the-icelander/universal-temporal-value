package net.icelander.utv.examples.utvfirst;

import static org.junit.jupiter.api.Assertions.assertEquals;

import java.time.LocalTime;
import net.icelander.utv.CivilTime;
import org.junit.jupiter.api.Nested;
import org.junit.jupiter.api.Test;

/**
 * How to use {@code CivilTime}, a wall-clock time of day with no date and no zone, compared with java.time's
 * {@code LocalTime}.
 */
class CivilTimeTest {

    @Nested
    class Of {

        // BEGIN time-of-day
        /**
         * Verifies that CivilTime.of writes no seconds when no seconds are given.
         * Compared with: {@code LocalTime.of(9, 30)} writes the same text, {@code 09:30}.
         */
        @Test
        void writesNoSeconds_whenNoSecondsAreGiven() {
            // GIVEN: half past nine, to the minute
            int givenHour = 9;
            int givenMinute = 30;

            // WHEN
            CivilTime actualTime = CivilTime.of(givenHour, givenMinute);

            // EXPECTATIONS: the same hour and minute as java.time, and the same text
            LocalTime expectedTime = LocalTime.of(9, 30);
            String expectedText = LocalTime.of(9, 30).toString();

            // THEN
            assertEquals(expectedTime.getHour(), actualTime.hour());
            assertEquals(expectedTime.getMinute(), actualTime.minute());
            assertEquals(expectedText, actualTime.toString());
        }
        // END time-of-day

        /**
         * Verifies that CivilTime.of writes the seconds when they are given, even when they are zero.
         * Compared with: {@code LocalTime.of(9, 30, 0)} is the same time, but writes {@code 09:30}: java.time cannot
         * tell a time given to the second from one given to the minute.
         */
        @Test
        void writesTheSeconds_whenSecondsAreGiven() {
            // GIVEN: half past nine, to the second
            int givenHour = 9;
            int givenMinute = 30;
            int givenSecond = 0;

            // WHEN
            CivilTime actualTime = CivilTime.of(givenHour, givenMinute, givenSecond);

            // EXPECTATIONS: the time java.time holds, with its seconds written out
            CivilTime expectedTime = CivilTime.from(LocalTime.of(9, 30, 0));
            String expectedText = "09:30:00";

            // THEN
            assertEquals(expectedTime, actualTime);
            assertEquals(expectedText, actualTime.toString());
        }
    }
}
