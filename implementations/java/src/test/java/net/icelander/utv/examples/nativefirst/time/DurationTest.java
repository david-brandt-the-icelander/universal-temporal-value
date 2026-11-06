package net.icelander.utv.examples.nativefirst.time;

import static org.junit.jupiter.api.Assertions.assertEquals;

import java.time.Duration;
import net.icelander.utv.ElapsedSeconds;
import net.icelander.utv.PosixSeconds;
import org.junit.jupiter.api.Nested;
import org.junit.jupiter.api.Test;

/**
 * What java.time's {@code java.time.Duration} does, compared with UTV's {@code PosixSeconds} and
 * {@code ElapsedSeconds}. A {@code Duration} counts seconds the way an {@code Instant} does, to the nanosecond.
 */
class DurationTest {

    @Nested
    class OfDays {

        /**
         * Verifies that Duration.ofDays(1) equals 86,400 seconds when given one day.
         * Compared with: UTV keeps {@code Period.days(1)}, {@code PosixSeconds.of(86_400)} and
         * {@code ElapsedSeconds.of(86_400)} apart, because a calendar day can be 23 or 25 hours long; java.time keeps the
         * calendar day apart too, as {@code java.time.Period.ofDays(1)}.
         */
        @Test
        void equals86400Seconds_whenGivenOneDay() {
            // GIVEN: one day
            long givenDays = 1;

            // WHEN
            Duration actualDay = Duration.ofDays(givenDays);

            // EXPECTATIONS: the POSIX seconds UTV would convert into this Duration
            Duration expectedDay = PosixSeconds.of(86_400).toDuration().single();

            // THEN
            assertEquals(expectedDay, actualDay);
        }
    }

    @Nested
    class OfMinutes {

        /**
         * Verifies that Duration.ofMinutes counts sixty seconds a minute when given ninety minutes.
         * Compared with: {@code ElapsedSeconds.of(5_400)} holds the same number of seconds as an exact SI amount.
         */
        @Test
        void countsSixtySecondsAMinute_whenGivenNinetyMinutes() {
            // GIVEN: 90 minutes
            long givenMinutes = 90;

            // WHEN
            Duration actualDuration = Duration.ofMinutes(givenMinutes);

            // EXPECTATIONS: the seconds UTV holds for the same duration
            long expectedSeconds = ElapsedSeconds.of(5_400).numerator().longValueExact();

            // THEN
            assertEquals(expectedSeconds, actualDuration.getSeconds());
        }
    }

    @Nested
    class Between {

        /**
         * Verifies that Duration.between returns the exact amount when the instants are a fraction of a second apart.
         * Compared with: {@code net.icelander.utv.Instant.until} returns the same amount, as the fraction 14403/4.
         */
        @Test
        void returnsTheExactAmount_whenTheInstantsAreAFractionOfASecondApart() {
            // GIVEN: one hour, 0.75 seconds later
            java.time.Instant givenLater = java.time.Instant.ofEpochSecond(1_793_889_001);

            // SYSTEM UNDER TEST: 13:30:00.25 on 5 November 2026
            java.time.Instant sut = java.time.Instant.ofEpochMilli(1_793_885_400_250L);

            // WHEN
            Duration actualDuration = Duration.between(sut, givenLater);

            // EXPECTATIONS: UTV's exact answer, as a Duration
            PosixSeconds tmpExact = net.icelander.utv.Instant.fromUnixSeconds(1_793_885_400, 250_000_000)
                    .until(net.icelander.utv.Instant.fromUnixSeconds(1_793_889_001));
            Duration expectedDuration = tmpExact.toDuration().single();

            // THEN
            assertEquals(expectedDuration, actualDuration);
        }
    }

    @Nested
    class DividedBy {

        /**
         * Verifies that Duration.dividedBy truncates to whole nanoseconds when the result is a third of a second.
         * Compared with: {@code PosixSeconds.of(1, 3)} holds the third exactly; its {@code toDuration()} truncates the
         * same way and says so (precision-loss).
         */
        @Test
        void truncatesToWholeNanoseconds_whenTheResultIsAThirdOfASecond() {
            // GIVEN: three equal parts
            long givenDivisor = 3;

            // SYSTEM UNDER TEST: one second
            Duration sut = Duration.ofSeconds(1);

            // WHEN
            Duration actualThird = sut.dividedBy(givenDivisor);

            // EXPECTATIONS: the Duration UTV converts its exact third into
            Duration expectedThird = PosixSeconds.of(1, 3).toDuration().single();

            // THEN
            assertEquals(expectedThird, actualThird);
        }
    }
}
