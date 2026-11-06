package net.icelander.utv.examples.nativefirst.time;

import static org.junit.jupiter.api.Assertions.assertEquals;
import static org.junit.jupiter.api.Assertions.assertNotEquals;

import java.time.Period;
import net.icelander.utv.PosixSeconds;
import org.junit.jupiter.api.Nested;
import org.junit.jupiter.api.Test;

/**
 * What java.time's {@code java.time.Period} does, compared with UTV's {@code Period}. The two are the same idea: an
 * amount of calendar components, not of seconds. {@code Period} below is java.time's; UTV's is written in full.
 */
class PeriodTest {

    @Nested
    class OfDays {

        /**
         * Verifies that Period.ofDays creates a calendar day, not 86,400 seconds, when given one day.
         * Compared with: {@code net.icelander.utv.Period.days(1)} is the same calendar day, and is kept apart from
         * {@code PosixSeconds.of(86_400)} the same way.
         */
        @Test
        void createsACalendarDay_whenGivenOneDay() {
            // GIVEN: one day
            int givenDays = 1;

            // WHEN
            Period actualPeriod = Period.ofDays(givenDays);

            // EXPECTATIONS: UTV's calendar day, written the same way, and not the PT24H of 86,400 seconds
            Period expectedPeriod = net.icelander.utv.Period.days(1).toJavaPeriod().single();
            String expectedText = net.icelander.utv.Period.days(1).toString();
            String expectedOtherText = PosixSeconds.of(86_400).toDuration().single().toString();

            // THEN
            assertEquals(expectedPeriod, actualPeriod);
            assertEquals(expectedText, actualPeriod.toString());
            assertNotEquals(expectedOtherText, actualPeriod.toString());
        }
    }

    @Nested
    class OfMonths {

        /**
         * Verifies that Period.ofMonths writes P1M when given one month.
         * Compared with: {@code net.icelander.utv.Period.months(1)} writes P1M too, and {@code Period.minutes(1)} PT1M.
         */
        @Test
        void writesP1M_whenGivenOneMonth() {
            // GIVEN: one month
            int givenMonths = 1;

            // WHEN
            Period actualPeriod = Period.ofMonths(givenMonths);

            // EXPECTATIONS: the text UTV writes for the same month
            String expectedText = net.icelander.utv.Period.months(1).toString();

            // THEN
            assertEquals(expectedText, actualPeriod.toString());
        }
    }
}
