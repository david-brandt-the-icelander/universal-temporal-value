package net.icelander.utv.examples.utvfirst;

import static org.junit.jupiter.api.Assertions.assertEquals;
import static org.junit.jupiter.api.Assertions.assertNotEquals;

import java.math.BigInteger;
import java.time.Duration;
import net.icelander.utv.ElapsedSeconds;
import net.icelander.utv.Period;
import net.icelander.utv.PosixSeconds;
import org.junit.jupiter.api.Nested;
import org.junit.jupiter.api.Test;

/**
 * How to use {@code ElapsedSeconds}, SI seconds as a stopwatch measures them, compared with java.time's
 * {@code Duration}, which has one kind of second for every purpose.
 */
class ElapsedSecondsTest {

    @Nested
    class Of {

        /**
         * Verifies that ElapsedSeconds.of holds exactly the seconds it is given when given ninety minutes.
         * Compared with: {@code Duration.ofMinutes(90).getSeconds()} is the same number.
         */
        @Test
        void holdsTheSeconds_whenGivenNinetyMinutes() {
            // GIVEN: 90 minutes of elapsed time, in seconds
            long givenSeconds = 5_400;

            // WHEN
            ElapsedSeconds actualAmount = ElapsedSeconds.of(givenSeconds);

            // EXPECTATIONS: the seconds java.time counts in 90 minutes, labelled as SI seconds
            BigInteger expectedSeconds = BigInteger.valueOf(Duration.ofMinutes(90).getSeconds());
            String expectedText = "5400 s (SI)";

            // THEN
            assertEquals(expectedSeconds, actualAmount.numerator());
            assertEquals(expectedText, actualAmount.toString());
        }

        /**
         * Verifies that ElapsedSeconds.of creates an amount that is neither a calendar day nor POSIX seconds when given
         * 86,400 seconds.
         * Compared with: {@code Duration.ofDays(1)} is both 86,400 elapsed seconds and 86,400 POSIX seconds.
         */
        @Test
        void isNotACalendarDay_whenGiven86400Seconds() {
            // GIVEN: exactly 86,400 SI seconds
            long givenSeconds = 86_400;

            // WHEN
            ElapsedSeconds actualAmount = ElapsedSeconds.of(givenSeconds);

            // EXPECTATIONS: different from a calendar day and from 86,400 POSIX seconds
            Period expectedCalendarDay = Period.days(1);
            PosixSeconds expectedPosixDay = PosixSeconds.of(86_400);

            // THEN
            assertNotEquals(expectedCalendarDay, actualAmount);
            assertNotEquals(expectedPosixDay, actualAmount);
        }
    }
}
