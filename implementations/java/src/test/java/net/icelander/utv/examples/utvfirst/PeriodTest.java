package net.icelander.utv.examples.utvfirst;

import static org.junit.jupiter.api.Assertions.assertEquals;
import static org.junit.jupiter.api.Assertions.assertNotEquals;

import java.time.Duration;
import net.icelander.utv.Period;
import net.icelander.utv.PosixSeconds;
import org.junit.jupiter.api.Nested;
import org.junit.jupiter.api.Test;

/**
 * How to use {@code Period}, an amount of calendar components such as days and months, compared with java.time's
 * {@code Period}, which is the same idea, and its {@code Duration}, which counts seconds. {@code Period} below is
 * UTV's; java.time's is written in full.
 */
class PeriodTest {

    @Nested
    class Days {

        /**
         * Verifies that Period.days creates a calendar day, which is not 86,400 seconds, when given one day.
         * Compared with: {@code java.time.Period.ofDays(1)} is the same calendar day, and is kept apart from
         * {@code Duration.ofDays(1)}, which is 86,400 seconds.
         */
        @Test
        void isNotEqualTo86400Seconds_whenGivenOneDay() {
            // GIVEN: one day
            long givenDays = 1;

            // WHEN
            Period actualPeriod = Period.days(givenDays);

            // EXPECTATIONS: java.time's calendar day, written the same way, and a different value from its 86,400 seconds
            Period expectedPeriod = Period.from(java.time.Period.ofDays(1));
            String expectedText = java.time.Period.ofDays(1).toString();
            PosixSeconds expectedOtherAmount = PosixSeconds.from(Duration.ofDays(1));

            // THEN
            assertEquals(expectedPeriod, actualPeriod);
            assertEquals(expectedText, actualPeriod.toString());
            assertNotEquals(expectedOtherAmount, actualPeriod);
        }
    }

    @Nested
    class Months {

        /**
         * Verifies that Period.months writes P1M, distinct from a minute, when given one month.
         * Compared with: {@code java.time.Period.ofMonths(1)} writes P1M too, and {@code Duration.ofMinutes(1)} PT1M.
         */
        @Test
        void writesP1M_whenGivenOneMonth() {
            // GIVEN: one month
            long givenMonths = 1;

            // WHEN
            Period actualPeriod = Period.months(givenMonths);

            // EXPECTATIONS: P1M as java.time writes it, never the PT1M of a minute
            String expectedText = java.time.Period.ofMonths(1).toString();
            String expectedMinuteText = Duration.ofMinutes(1).toString();

            // THEN
            assertEquals(expectedText, actualPeriod.toString());
            assertNotEquals(expectedMinuteText, actualPeriod.toString());
        }
    }
}
