package net.icelander.utv.examples.utvfirst;

import static org.junit.jupiter.api.Assertions.assertEquals;

import java.time.YearMonth;
import net.icelander.utv.CivilYearMonth;
import org.junit.jupiter.api.Nested;
import org.junit.jupiter.api.Test;

/**
 * How to use {@code CivilYearMonth}, a month of a Gregorian year with no day, compared with java.time's
 * {@code YearMonth}, which is the same idea. Where the two agree, the expected value is computed with
 * {@code YearMonth}.
 */
class CivilYearMonthTest {

    @Nested
    class Of {

        /**
         * Verifies that CivilYearMonth.of creates a month without inventing a day when given a year and a month.
         * Compared with: {@code YearMonth.of(2026, 11)} is the same month and writes the same text.
         */
        @Test
        void createsAMonthWithoutADay_whenGivenAYearAndAMonth() {
            // GIVEN: November 2026
            int givenYear = 2026;
            int givenMonth = 11;

            // WHEN
            CivilYearMonth actualMonth = CivilYearMonth.of(givenYear, givenMonth);

            // EXPECTATIONS: java.time's month, and text with no day in it
            YearMonth tmpMonth = YearMonth.of(2026, 11);
            CivilYearMonth expectedMonth = CivilYearMonth.of(tmpMonth.getYear(), tmpMonth.getMonthValue());
            String expectedText = YearMonth.of(2026, 11).toString();

            // THEN
            assertEquals(expectedMonth, actualMonth);
            assertEquals(expectedText, actualMonth.toString());
        }
    }

    @Nested
    class AddMonths {

        /**
         * Verifies that CivilYearMonth.addMonths returns the later month when the months cross a year end.
         * Compared with: {@code YearMonth.plusMonths} lands in the same month.
         */
        @Test
        void returnsTheLaterMonth_whenTheMonthsCrossAYearEnd() {
            // GIVEN: three months, past December
            int givenMonths = 3;

            // SYSTEM UNDER TEST: November 2026
            CivilYearMonth sut = CivilYearMonth.of(2026, 11);

            // WHEN
            CivilYearMonth actualMonth = sut.addMonths(givenMonths);

            // EXPECTATIONS: the month java.time lands in
            YearMonth tmpMonth = YearMonth.of(2026, 11).plusMonths(3);
            CivilYearMonth expectedMonth = CivilYearMonth.of(tmpMonth.getYear(), tmpMonth.getMonthValue());

            // THEN
            assertEquals(expectedMonth, actualMonth);
        }
    }
}
