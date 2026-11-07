package net.icelander.utv.examples.nativefirst.time.temporal;

import static org.junit.jupiter.api.Assertions.assertEquals;

import java.time.LocalDate;
import java.time.temporal.IsoFields;
import net.icelander.utv.CivilDate;
import org.junit.jupiter.api.Nested;
import org.junit.jupiter.api.Test;

/**
 * What java.time's {@code java.time.temporal.IsoFields} does, compared with UTV's {@code CivilDate.isoWeek}.
 */
class IsoFieldsTest {

    @Nested
    class WeekOfWeekBasedYear {

        /**
         * Verifies that IsoFields.WEEK_OF_WEEK_BASED_YEAR reads 45 when the date is 5 November 2026.
         * Compared with: {@code CivilDate.isoWeek()} gives the same ISO 8601 week.
         */
        @Test
        void reads45_whenTheDateIs5November2026() {
            // GIVEN: 5 November 2026
            LocalDate givenDate = LocalDate.of(2026, 11, 5);

            // WHEN
            int actualWeek = givenDate.get(IsoFields.WEEK_OF_WEEK_BASED_YEAR);

            // EXPECTATIONS: the week UTV computes for the same day
            int expectedWeek = CivilDate.of(2026, 11, 5).isoWeek();

            // THEN
            assertEquals(expectedWeek, actualWeek);
        }
    }
}
