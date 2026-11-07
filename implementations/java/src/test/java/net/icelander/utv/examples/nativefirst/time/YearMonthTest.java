package net.icelander.utv.examples.nativefirst.time;

import static org.junit.jupiter.api.Assertions.assertEquals;

import java.time.YearMonth;
import net.icelander.utv.CivilYearMonth;
import org.junit.jupiter.api.Nested;
import org.junit.jupiter.api.Test;

/**
 * What java.time's {@code java.time.YearMonth} does, compared with UTV's {@code CivilYearMonth}. The two are the same
 * idea: a month with no day.
 */
class YearMonthTest {

    @Nested
    class Of {

        /**
         * Verifies that YearMonth.of creates a month without a day when given a year and a month.
         * Compared with: {@code CivilYearMonth.of(2026, 11)} is the same month and writes the same text.
         */
        @Test
        void createsAMonthWithoutADay_whenGivenAYearAndAMonth() {
            // GIVEN: November 2026
            int givenYear = 2026;
            int givenMonth = 11;

            // WHEN
            YearMonth actualMonth = YearMonth.of(givenYear, givenMonth);

            // EXPECTATIONS: UTV's text for the same month
            String expectedText = CivilYearMonth.of(2026, 11).toString();

            // THEN
            assertEquals(expectedText, actualMonth.toString());
        }
    }

    @Nested
    class PlusMonths {

        /**
         * Verifies that YearMonth.plusMonths returns the later month when the months cross a year end.
         * Compared with: {@code CivilYearMonth.addMonths} lands in the same month.
         */
        @Test
        void returnsTheLaterMonth_whenTheMonthsCrossAYearEnd() {
            // GIVEN: three months, past December
            long givenMonths = 3;

            // SYSTEM UNDER TEST: November 2026
            YearMonth sut = YearMonth.of(2026, 11);

            // WHEN
            YearMonth actualMonth = sut.plusMonths(givenMonths);

            // EXPECTATIONS: the month UTV lands in
            CivilYearMonth tmpMonth = CivilYearMonth.of(2026, 11).addMonths(3);
            YearMonth expectedMonth = YearMonth.of(tmpMonth.year().intValueExact(), tmpMonth.month());

            // THEN
            assertEquals(expectedMonth, actualMonth);
        }
    }
}
