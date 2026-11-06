package net.icelander.utv.examples.utvfirst;

import static org.junit.jupiter.api.Assertions.assertEquals;
import static org.junit.jupiter.api.Assertions.assertNotEquals;

import java.time.Duration;
import net.icelander.utv.PosixSeconds;
import org.junit.jupiter.api.Nested;
import org.junit.jupiter.api.Test;

/**
 * How to use {@code PosixSeconds}, an exact number of seconds on the POSIX timescale, compared with java.time's
 * {@code Duration}, which counts whole nanoseconds.
 */
class PosixSecondsTest {

    @Nested
    class Of {

        /**
         * Verifies that PosixSeconds.of holds a third of a second exactly when given a fraction.
         * Compared with: {@code Duration.ofSeconds(1).dividedBy(3)} truncates to 0.333333333 seconds.
         */
        @Test
        void holdsAThirdExactly_whenGivenAFraction() {
            // GIVEN: one third of a second
            long givenNumerator = 1;
            long givenDenominator = 3;

            // WHEN
            PosixSeconds actualAmount = PosixSeconds.of(givenNumerator, givenDenominator);

            // EXPECTATIONS: the fraction itself, which java.time's nanoseconds can only approach
            String expectedText = "1/3 s (POSIX)";
            PosixSeconds expectedOtherAmount = PosixSeconds.from(Duration.ofSeconds(1).dividedBy(3));

            // THEN
            assertEquals(expectedText, actualAmount.toString());
            assertNotEquals(expectedOtherAmount, actualAmount);
        }
    }
}
