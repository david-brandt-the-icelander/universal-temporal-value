package net.icelander.utv.examples.nativefirst.time;

import static org.junit.jupiter.api.Assertions.assertEquals;

import java.time.Year;
import net.icelander.utv.CivilDate;
import org.junit.jupiter.api.Nested;
import org.junit.jupiter.params.ParameterizedTest;
import org.junit.jupiter.params.provider.ValueSource;

/**
 * What java.time's {@code java.time.Year} does, compared with UTV's {@code CivilDate.isLeapYear}.
 */
class YearTest {

    @Nested
    class IsLeap {

        /**
         * Verifies that Year.isLeap follows the Gregorian leap-year rules when given any kind of year.
         * Compared with: {@code CivilDate.isLeapYear} gives the same answers, so both accept 29 February 2024 and 2000
         * and reject it in 2026 and 1900.
         */
        @ParameterizedTest
        @ValueSource(ints = {
            2024, // divisible by 4: leap
            2026, // not divisible by 4: common
            1900, // divisible by 100 but not 400: common
            2000, // divisible by 400: leap
        })
        void agreesWithUtv_whenGivenAnyKindOfYear(int givenYear) {
            // WHEN
            boolean actualIsLeap = Year.isLeap(givenYear);

            // EXPECTATIONS: UTV's answer for the same year
            boolean expectedIsLeap = CivilDate.of(givenYear, 1, 1).isLeapYear();

            // THEN
            assertEquals(expectedIsLeap, actualIsLeap);
        }
    }
}
