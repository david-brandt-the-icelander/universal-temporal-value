package net.icelander.utv.examples.nativefirst.time;

import static org.junit.jupiter.api.Assertions.assertEquals;
import static org.junit.jupiter.api.Assertions.assertThrows;

import java.time.DateTimeException;
import java.time.DayOfWeek;
import java.time.LocalDate;
import java.time.format.DateTimeFormatter;
import java.time.format.DateTimeParseException;
import java.time.temporal.ChronoUnit;
import java.util.Locale;
import net.icelander.utv.CivilDate;
import net.icelander.utv.MonthEnd;
import org.junit.jupiter.api.Nested;
import org.junit.jupiter.api.Test;

/**
 * What java.time's {@code java.time.LocalDate} does, compared with UTV's {@code CivilDate}. Where the two agree, the
 * expected value is computed with UTV.
 */
class LocalDateTest {

    @Nested
    class Of {

        /**
         * Verifies that LocalDate.of creates the date when the fields name a real day.
         * Compared with: {@code CivilDate.of(2026, 11, 5)} names the same day.
         */
        @Test
        void createsTheDate_whenTheFieldsNameARealDay() {
            // GIVEN: 5 November 2026
            int givenYear = 2026;
            int givenMonth = 11;
            int givenDay = 5;

            // WHEN
            LocalDate actualDate = LocalDate.of(givenYear, givenMonth, givenDay);

            // EXPECTATIONS: the same day as UTV's CivilDate
            LocalDate expectedDate = CivilDate.of(2026, 11, 5).toLocalDate().single();

            // THEN
            assertEquals(expectedDate, actualDate);
        }

        /**
         * Verifies that LocalDate.of throws a DateTimeException when the day does not exist in the month.
         * Compared with: {@code CivilDate.of} throws an {@code IllegalArgumentException} that names the diagnostic
         * (day-out-of-range); {@code CivilDate.tryOf} returns invalid instead.
         */
        @Test
        void throws_whenTheDayDoesNotExist() {
            // GIVEN: 30 February 2026
            int givenYear = 2026;
            int givenMonth = 2;
            int givenDay = 30;

            // WHEN
            DateTimeException actualException = assertThrows(DateTimeException.class, () -> LocalDate.of(givenYear, givenMonth, givenDay));

            // EXPECTATIONS: a message that names the day, but no code a program could act on
            String expectedMessage = "Invalid date 'FEBRUARY 30'";

            // THEN
            assertEquals(expectedMessage, actualException.getMessage());
        }

        /**
         * Verifies that LocalDate.of throws a DateTimeException when the month is 13.
         * Compared with: {@code CivilDate.tryOf(2026, 13, 1)} returns invalid with month-out-of-range.
         */
        @Test
        void throws_whenTheMonthIs13() {
            // GIVEN: the first day of a thirteenth month
            int givenYear = 2026;
            int givenMonth = 13;
            int givenDay = 1;

            // WHEN
            DateTimeException actualException = assertThrows(DateTimeException.class, () -> LocalDate.of(givenYear, givenMonth, givenDay));

            // EXPECTATIONS
            String expectedMessage = "Invalid value for MonthOfYear (valid values 1 - 12): 13";

            // THEN
            assertEquals(expectedMessage, actualException.getMessage());
        }

        /**
         * Verifies that LocalDate.of creates the date when the year is beyond 9999.
         * Compared with: {@code CivilDate.of(275760, 9, 13)} holds the same day and writes the same text.
         */
        @Test
        void createsTheDate_whenTheYearIsBeyond9999() {
            // GIVEN: 13 September 275760
            int givenYear = 275_760;
            int givenMonth = 9;
            int givenDay = 13;

            // WHEN
            LocalDate actualDate = LocalDate.of(givenYear, givenMonth, givenDay);

            // EXPECTATIONS: the day UTV holds, written with a sign
            LocalDate expectedDate = CivilDate.of(275_760, 9, 13).toLocalDate().single();
            String expectedText = CivilDate.of(275_760, 9, 13).toString();

            // THEN
            assertEquals(expectedDate, actualDate);
            assertEquals(expectedText, actualDate.toString());
        }

        /**
         * Verifies that LocalDate.of creates the date when the year is before year 1.
         * Compared with: {@code CivilDate.of(-500, 3, 1)} holds the same day in the same proleptic calendar.
         */
        @Test
        void createsTheDate_whenTheYearIsNegative() {
            // GIVEN: 1 March of year -500
            int givenYear = -500;
            int givenMonth = 3;
            int givenDay = 1;

            // WHEN
            LocalDate actualDate = LocalDate.of(givenYear, givenMonth, givenDay);

            // EXPECTATIONS: the day UTV holds, written with a sign
            LocalDate expectedDate = CivilDate.of(-500, 3, 1).toLocalDate().single();
            String expectedText = CivilDate.of(-500, 3, 1).toString();

            // THEN
            assertEquals(expectedDate, actualDate);
            assertEquals(expectedText, actualDate.toString());
        }
    }

    @Nested
    class Max {

        /**
         * Verifies that LocalDate.MAX is the last day of year 999,999,999 when read.
         * Compared with: {@code CivilDate.of(1_000_000_000, 1, 1).toLocalDate()} is unsupported (out-of-native-range)
         * for exactly this reason; UTV's years are unbounded.
         */
        @Test
        void isTheLastDayOfYear999999999_whenRead() {
            // WHEN
            LocalDate actualMax = LocalDate.MAX;

            // EXPECTATIONS: 31 December 999,999,999, as UTV converts it
            LocalDate expectedMax = CivilDate.of(999_999_999, 12, 31).toLocalDate().single();

            // THEN
            assertEquals(expectedMax, actualMax);
        }
    }

    @Nested
    class Parse {

        /**
         * Verifies that LocalDate.parse reads the date when the text is an ISO 8601 calendar date.
         * Compared with: {@code CivilDate.parse("2026-11-05")} reads the same day.
         */
        @Test
        void readsTheDate_whenTheTextIsAnIsoDate() {
            // GIVEN: the ISO 8601 text of 5 November 2026
            String givenText = "2026-11-05";

            // WHEN
            LocalDate actualDate = LocalDate.parse(givenText);

            // EXPECTATIONS: the day UTV parses from the same text
            LocalDate expectedDate = CivilDate.parse("2026-11-05").single().toLocalDate().single();

            // THEN
            assertEquals(expectedDate, actualDate);
        }

        /**
         * Verifies that LocalDate.parse throws a DateTimeParseException when the text names a day that does not exist.
         * Compared with: {@code CivilDate.parse("2026-02-30")} returns invalid with day-out-of-range, and never throws.
         */
        @Test
        void throws_whenTheTextNamesADayThatDoesNotExist() {
            // GIVEN: well-formed text for 30 February 2026
            String givenText = "2026-02-30";

            // WHEN
            DateTimeParseException actualException = assertThrows(DateTimeParseException.class, () -> LocalDate.parse(givenText));

            // EXPECTATIONS: the text the parser gave up on
            String expectedParsedText = "2026-02-30";

            // THEN
            assertEquals(expectedParsedText, actualException.getParsedString());
        }

        /**
         * Verifies that LocalDate.parse with a pattern formatter clamps to the last day of the month when the text names
         * a day that does not exist.
         * Compared with: {@code CivilDate.parse("2026-02-30")} returns invalid (day-out-of-range); UTV never moves a day
         * on its own.
         */
        @Test
        void clampsToTheLastDay_whenAPatternFormatterReadsADayThatDoesNotExist() {
            // GIVEN: well-formed text for 30 February 2026
            String givenText = "2026-02-30";

            // SETUP: a formatter built from a pattern, with java.time's default SMART resolver, and a fixed locale
            DateTimeFormatter envFormatter = DateTimeFormatter.ofPattern("uuuu-MM-dd", Locale.ROOT);

            // WHEN
            LocalDate actualDate = LocalDate.parse(givenText, envFormatter);

            // EXPECTATIONS: 28 February, a day UTV gives only for an explicit CONSTRAIN policy
            LocalDate expectedDate = LocalDate.of(2026, 2, 28);

            // THEN
            assertEquals(expectedDate, actualDate);
        }
    }

    @Nested
    class GetDayOfWeek {

        /**
         * Verifies that LocalDate.getDayOfWeek returns Thursday when the date is 5 November 2026.
         * Compared with: {@code CivilDate.dayOfWeek()} names the same day.
         */
        @Test
        void returnsThursday_whenTheDateIs5November2026() {
            // SYSTEM UNDER TEST: 5 November 2026
            LocalDate sut = LocalDate.of(2026, 11, 5);

            // WHEN
            DayOfWeek actualDayOfWeek = sut.getDayOfWeek();

            // EXPECTATIONS: the weekday UTV names for the same day
            DayOfWeek expectedDayOfWeek = DayOfWeek.of(CivilDate.of(2026, 11, 5).dayOfWeek().number());

            // THEN
            assertEquals(expectedDayOfWeek, actualDayOfWeek);
        }

        /**
         * Verifies that LocalDate.getDayOfWeek numbers Sunday 7 when the date is a Sunday.
         * Compared with: UTV's {@code Weekday.number()} follows the same ISO 8601 numbering, Monday 1 to Sunday 7.
         */
        @Test
        void numbersSundaySeven_whenTheDateIsASunday() {
            // SYSTEM UNDER TEST: Sunday 8 November 2026
            LocalDate sut = LocalDate.of(2026, 11, 8);

            // WHEN
            int actualNumber = sut.getDayOfWeek().getValue();

            // EXPECTATIONS: the number UTV gives the same day
            int expectedNumber = CivilDate.of(2026, 11, 8).dayOfWeek().number();

            // THEN
            assertEquals(expectedNumber, actualNumber);
        }
    }

    @Nested
    class PlusDays {

        /**
         * Verifies that LocalDate.plusDays returns the date that many days later when the days cross a month end.
         * Compared with: {@code CivilDate.addDays} gives the same date.
         */
        @Test
        void returnsTheLaterDate_whenTheDaysCrossAMonthEnd() {
            // GIVEN: 30 days, more than remain in November
            long givenDays = 30;

            // SYSTEM UNDER TEST: 5 November 2026
            LocalDate sut = LocalDate.of(2026, 11, 5);

            // WHEN
            LocalDate actualDate = sut.plusDays(givenDays);

            // EXPECTATIONS: the date UTV computes for the same addition
            LocalDate expectedDate = CivilDate.of(2026, 11, 5).addDays(30).toLocalDate().single();

            // THEN
            assertEquals(expectedDate, actualDate);
        }
    }

    @Nested
    class PlusMonths {

        /**
         * Verifies that LocalDate.plusMonths clamps to the last day of the month when the day is missing from the target
         * month.
         * Compared with: {@code CivilDate.addMonths(1)} returns none (month-end), and gives this date only when asked
         * with {@code MonthEnd.CONSTRAIN}.
         */
        @Test
        void clampsToTheLastDay_whenTheDayIsMissingFromTheTargetMonth() {
            // GIVEN: one month, into a February that has no day 31
            long givenMonths = 1;

            // SYSTEM UNDER TEST: 31 January 2026
            LocalDate sut = LocalDate.of(2026, 1, 31);

            // WHEN
            LocalDate actualDate = sut.plusMonths(givenMonths);

            // EXPECTATIONS: the date UTV gives only with the explicit CONSTRAIN policy
            LocalDate expectedDate = CivilDate.of(2026, 1, 31).addMonths(1, MonthEnd.CONSTRAIN).toLocalDate().single();

            // THEN
            assertEquals(expectedDate, actualDate);
        }
    }

    @Nested
    class Plus {

        /**
         * Verifies that LocalDate.plus clamps to the last day of the month when a one-month Period lands on a missing day.
         * Compared with: {@code CivilDate.add(Period.months(1))} returns none (month-end).
         */
        @Test
        void clampsToTheLastDay_whenAOneMonthPeriodLandsOnAMissingDay() {
            // GIVEN: a calendar period of one month
            java.time.Period givenPeriod = java.time.Period.ofMonths(1);

            // SYSTEM UNDER TEST: 31 January 2026
            LocalDate sut = LocalDate.of(2026, 1, 31);

            // WHEN
            LocalDate actualDate = sut.plus(givenPeriod);

            // EXPECTATIONS: the date UTV gives only with the explicit CONSTRAIN policy
            LocalDate expectedDate = CivilDate.of(2026, 1, 31).addMonths(1, MonthEnd.CONSTRAIN).toLocalDate().single();

            // THEN
            assertEquals(expectedDate, actualDate);
        }
    }

    @Nested
    class Until {

        /**
         * Verifies that LocalDate.until in days returns the day count when the other date is later.
         * Compared with: {@code CivilDate.daysUntil} returns the same count.
         */
        @Test
        void returnsTheDayCount_whenTheOtherDateIsLater() {
            // GIVEN: Christmas Day 2026, counted in days
            LocalDate givenChristmas = LocalDate.of(2026, 12, 25);
            ChronoUnit givenUnit = ChronoUnit.DAYS;

            // SYSTEM UNDER TEST: 5 November 2026
            LocalDate sut = LocalDate.of(2026, 11, 5);

            // WHEN
            long actualDays = sut.until(givenChristmas, givenUnit);

            // EXPECTATIONS: the count UTV computes
            long expectedDays = CivilDate.of(2026, 11, 5).daysUntil(CivilDate.of(2026, 12, 25));

            // THEN
            assertEquals(expectedDays, actualDays);
        }
    }

    @Nested
    class IsBefore {

        /**
         * Verifies that LocalDate.isBefore returns true when this date comes first.
         * Compared with: {@code CivilDate.isBefore} gives the same answer.
         */
        @Test
        void returnsTrue_whenThisDateComesFirst() {
            // GIVEN: Christmas Day 2026
            LocalDate givenChristmas = LocalDate.of(2026, 12, 25);

            // SYSTEM UNDER TEST: 5 November 2026
            LocalDate sut = LocalDate.of(2026, 11, 5);

            // WHEN
            boolean actualComesFirst = sut.isBefore(givenChristmas);

            // EXPECTATIONS: UTV's answer for the same two days
            boolean expectedComesFirst = CivilDate.of(2026, 11, 5).isBefore(CivilDate.of(2026, 12, 25));

            // THEN
            assertEquals(expectedComesFirst, actualComesFirst);
        }
    }
}
