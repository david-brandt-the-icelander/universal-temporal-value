package net.icelander.utv.examples.utvfirst;

import static org.junit.jupiter.api.Assertions.assertEquals;
import static org.junit.jupiter.api.Assertions.assertThrows;
import static org.junit.jupiter.api.Assertions.assertTrue;

import java.time.LocalDate;
import java.time.Year;
import java.time.temporal.ChronoUnit;
import java.time.temporal.IsoFields;
import net.icelander.utv.CivilDate;
import net.icelander.utv.MonthEnd;
import net.icelander.utv.Outcome;
import net.icelander.utv.Period;
import net.icelander.utv.Status;
import net.icelander.utv.Weekday;
import org.junit.jupiter.api.Nested;
import org.junit.jupiter.api.Test;
import org.junit.jupiter.params.ParameterizedTest;
import org.junit.jupiter.params.provider.ValueSource;

/**
 * How to use {@code CivilDate}, a day in the Gregorian calendar with no time and no zone, compared with java.time's
 * {@code LocalDate}. Where the two agree, the expected value is computed with {@code LocalDate}.
 */
class CivilDateTest {

    @Nested
    class Of {

        // BEGIN a-date
        /**
         * Verifies that CivilDate.of creates the date when the fields name a real day.
         * Compared with: {@code LocalDate.of(2026, 11, 5)} names the same day and writes the same text.
         */
        @Test
        void createsTheDate_whenTheFieldsNameARealDay() {
            // GIVEN: 5 November 2026
            int givenYear = 2026;
            int givenMonth = 11;
            int givenDay = 5;

            // WHEN
            CivilDate actualDate = CivilDate.of(givenYear, givenMonth, givenDay);

            // EXPECTATIONS: the same day as java.time's LocalDate
            CivilDate expectedDate = CivilDate.from(LocalDate.of(2026, 11, 5));
            String expectedText = LocalDate.of(2026, 11, 5).toString();

            // THEN
            assertEquals(expectedDate, actualDate);
            assertEquals(expectedText, actualDate.toString());
        }
        // END a-date

        /**
         * Verifies that CivilDate.of accepts 29 February when the year is a leap year.
         * Compared with: {@code LocalDate.of(2024, 2, 29)} accepts it too.
         */
        @Test
        void acceptsTheLeapDay_whenTheYearIsALeapYear() {
            // GIVEN: 29 February 2024, a year divisible by 4
            int givenYear = 2024;
            int givenMonth = 2;
            int givenDay = 29;

            // WHEN
            CivilDate actualDate = CivilDate.of(givenYear, givenMonth, givenDay);

            // EXPECTATIONS: the same day as java.time's LocalDate
            CivilDate expectedDate = CivilDate.from(LocalDate.of(2024, 2, 29));

            // THEN
            assertEquals(expectedDate, actualDate);
        }

        /**
         * Verifies that CivilDate.of accepts 29 February when the year is a century divisible by 400.
         * Compared with: {@code LocalDate.of(2000, 2, 29)} accepts it too.
         */
        @Test
        void acceptsTheLeapDay_whenTheCenturyIsDivisibleBy400() {
            // GIVEN: 29 February 2000, a century year divisible by 400
            int givenYear = 2000;
            int givenMonth = 2;
            int givenDay = 29;

            // WHEN
            CivilDate actualDate = CivilDate.of(givenYear, givenMonth, givenDay);

            // EXPECTATIONS: the same day as java.time's LocalDate
            CivilDate expectedDate = CivilDate.from(LocalDate.of(2000, 2, 29));

            // THEN
            assertEquals(expectedDate, actualDate);
        }

        /**
         * Verifies that CivilDate.of throws IllegalArgumentException when the day does not exist in the month.
         * Compared with: {@code LocalDate.of(2026, 2, 30)} throws a {@code DateTimeException} instead.
         */
        @Test
        void throws_whenTheDayDoesNotExist() {
            // GIVEN: 30 February 2026
            int givenYear = 2026;
            int givenMonth = 2;
            int givenDay = 30;

            // WHEN
            IllegalArgumentException actualException =
                    assertThrows(IllegalArgumentException.class, () -> CivilDate.of(givenYear, givenMonth, givenDay));

            // EXPECTATIONS: a message that ends in the diagnostic code
            String expectedMessage = "2026-02 has no day 30 (day-out-of-range)";

            // THEN
            assertEquals(expectedMessage, actualException.getMessage());
        }

        /**
         * Verifies that CivilDate.of creates the date when the year is beyond 9999.
         * Compared with: {@code LocalDate.of(275760, 9, 13)} holds it too, and writes the year with the same sign.
         */
        @Test
        void createsTheDate_whenTheYearIsBeyond9999() {
            // GIVEN: 13 September 275760, the last day a JavaScript Date can hold
            int givenYear = 275_760;
            int givenMonth = 9;
            int givenDay = 13;

            // WHEN
            CivilDate actualDate = CivilDate.of(givenYear, givenMonth, givenDay);

            // EXPECTATIONS: the same day as java.time's LocalDate, an expanded year written with a sign
            CivilDate expectedDate = CivilDate.from(LocalDate.of(275_760, 9, 13));
            String expectedText = LocalDate.of(275_760, 9, 13).toString();

            // THEN
            assertEquals(expectedDate, actualDate);
            assertEquals(expectedText, actualDate.toString());
        }

        /**
         * Verifies that CivilDate.of creates the date when the year is before year 1.
         * Compared with: {@code LocalDate.of(-500, 3, 1)} holds it too, in the same proleptic calendar with a year 0.
         */
        @Test
        void createsTheDate_whenTheYearIsNegative() {
            // GIVEN: 1 March of year -500 in the proleptic Gregorian calendar
            int givenYear = -500;
            int givenMonth = 3;
            int givenDay = 1;

            // WHEN
            CivilDate actualDate = CivilDate.of(givenYear, givenMonth, givenDay);

            // EXPECTATIONS: the same day as java.time's LocalDate, a signed, four-digit year
            CivilDate expectedDate = CivilDate.from(LocalDate.of(-500, 3, 1));
            String expectedText = LocalDate.of(-500, 3, 1).toString();

            // THEN
            assertEquals(expectedDate, actualDate);
            assertEquals(expectedText, actualDate.toString());
        }
    }

    @Nested
    class TryOf {

        // BEGIN feb-30
        /**
         * Verifies that CivilDate.tryOf returns invalid when the day is 30 February.
         * Compared with: {@code LocalDate.of(2026, 2, 30)} throws; tryOf reports the problem as a value instead.
         */
        @Test
        void returnsInvalid_whenTheDayIs30February() {
            // GIVEN: 30 February 2026
            int givenYear = 2026;
            int givenMonth = 2;
            int givenDay = 30;

            // WHEN
            Outcome<CivilDate> actualOutcome = CivilDate.tryOf(givenYear, givenMonth, givenDay);

            // EXPECTATIONS: invalid, because February 2026 has 28 days
            Status expectedStatus = Status.INVALID;
            String expectedCode = "day-out-of-range";

            // THEN
            assertEquals(expectedStatus, actualOutcome.status());
            assertTrue(actualOutcome.diagnostics().stream().anyMatch(d -> d.code().equals(expectedCode)), actualOutcome.why());
        }
        // END feb-30

        /**
         * Verifies that CivilDate.tryOf returns invalid when 29 February falls in a common year.
         * Compared with: {@code Year.isLeap(2026)} is false and {@code LocalDate.of(2026, 2, 29)} throws.
         */
        @Test
        void returnsInvalid_when29FebruaryFallsInACommonYear() {
            // GIVEN: 29 February 2026, a year not divisible by 4
            int givenYear = 2026;
            int givenMonth = 2;
            int givenDay = 29;

            // WHEN
            Outcome<CivilDate> actualOutcome = CivilDate.tryOf(givenYear, givenMonth, givenDay);

            // EXPECTATIONS: invalid, rather than rolled over to 1 March
            Status expectedStatus = Status.INVALID;
            String expectedCode = "day-out-of-range";

            // THEN
            assertEquals(expectedStatus, actualOutcome.status());
            assertTrue(actualOutcome.diagnostics().stream().anyMatch(d -> d.code().equals(expectedCode)), actualOutcome.why());
        }

        /**
         * Verifies that CivilDate.tryOf returns invalid when 29 February falls in a century not divisible by 400.
         * Compared with: {@code Year.isLeap(1900)} is false too.
         */
        @Test
        void returnsInvalid_when29FebruaryFallsInACenturyNotDivisibleBy400() {
            // GIVEN: 29 February 1900, divisible by 100 but not by 400
            int givenYear = 1900;
            int givenMonth = 2;
            int givenDay = 29;

            // WHEN
            Outcome<CivilDate> actualOutcome = CivilDate.tryOf(givenYear, givenMonth, givenDay);

            // EXPECTATIONS: invalid, because 1900 was not a leap year
            Status expectedStatus = Status.INVALID;
            String expectedCode = "day-out-of-range";

            // THEN
            assertEquals(expectedStatus, actualOutcome.status());
            assertTrue(actualOutcome.diagnostics().stream().anyMatch(d -> d.code().equals(expectedCode)), actualOutcome.why());
        }

        /**
         * Verifies that CivilDate.tryOf returns invalid when the month is 13.
         * Compared with: {@code LocalDate.of(2026, 13, 1)} throws a {@code DateTimeException}.
         */
        @Test
        void returnsInvalid_whenTheMonthIs13() {
            // GIVEN: the first day of a thirteenth month
            int givenYear = 2026;
            int givenMonth = 13;
            int givenDay = 1;

            // WHEN
            Outcome<CivilDate> actualOutcome = CivilDate.tryOf(givenYear, givenMonth, givenDay);

            // EXPECTATIONS: invalid, because a Gregorian year has 12 months
            Status expectedStatus = Status.INVALID;
            String expectedCode = "month-out-of-range";

            // THEN
            assertEquals(expectedStatus, actualOutcome.status());
            assertTrue(actualOutcome.diagnostics().stream().anyMatch(d -> d.code().equals(expectedCode)), actualOutcome.why());
        }
    }

    @Nested
    class Parse {

        /**
         * Verifies that CivilDate.parse returns the date when the text is an ISO 8601 calendar date.
         * Compared with: {@code LocalDate.parse("2026-11-05")} reads the same day.
         */
        @Test
        void returnsTheDate_whenTheTextIsAnIsoDate() {
            // GIVEN: the ISO 8601 text of 5 November 2026
            String givenText = "2026-11-05";

            // WHEN
            Outcome<CivilDate> actualOutcome = CivilDate.parse(givenText);

            // EXPECTATIONS: the day java.time parses from the same text
            CivilDate expectedDate = CivilDate.from(LocalDate.parse("2026-11-05"));

            // THEN
            assertEquals(expectedDate, actualOutcome.single());
        }

        /**
         * Verifies that CivilDate.parse returns invalid, with the reason, when the text names a day that does not exist.
         * Compared with: {@code LocalDate.parse} throws a {@code DateTimeParseException}, and a formatter built with
         * {@code DateTimeFormatter.ofPattern("uuuu-MM-dd")} reads the same text as 28 February without a word.
         */
        @Test
        void returnsInvalid_whenTheTextNamesADayThatDoesNotExist() {
            // GIVEN: well-formed text for 30 February 2026
            String givenText = "2026-02-30";

            // WHEN
            Outcome<CivilDate> actualOutcome = CivilDate.parse(givenText);

            // EXPECTATIONS: invalid, and the diagnostic says which field is wrong
            Status expectedStatus = Status.INVALID;
            String expectedCode = "day-out-of-range";

            // THEN
            assertEquals(expectedStatus, actualOutcome.status());
            assertTrue(actualOutcome.diagnostics().stream().anyMatch(d -> d.code().equals(expectedCode)), actualOutcome.why());
        }
    }

    @Nested
    class DayOfWeek {

        // BEGIN weekday-week
        /**
         * Verifies that CivilDate.dayOfWeek returns Thursday when the date is 5 November 2026.
         * Compared with: {@code LocalDate.getDayOfWeek} names the same day, and numbers it the same ISO way, Monday 1
         * to Sunday 7.
         */
        @Test
        void returnsThursday_whenTheDateIs5November2026() {
            // SYSTEM UNDER TEST: 5 November 2026
            CivilDate sut = CivilDate.of(2026, 11, 5);

            // WHEN
            Weekday actualWeekday = sut.dayOfWeek();

            // EXPECTATIONS: the weekday java.time numbers for the same day
            Weekday expectedWeekday = Weekday.of(LocalDate.of(2026, 11, 5).getDayOfWeek().getValue());

            // THEN
            assertEquals(expectedWeekday, actualWeekday);
        }
        // END weekday-week
    }

    @Nested
    class IsoWeek {

        // BEGIN weekday-week
        /**
         * Verifies that CivilDate.isoWeek returns 45 when the date is 5 November 2026.
         * Compared with: {@code LocalDate.get(IsoFields.WEEK_OF_WEEK_BASED_YEAR)} gives the same ISO 8601 week.
         */
        @Test
        void returns45_whenTheDateIs5November2026() {
            // SYSTEM UNDER TEST: 5 November 2026
            CivilDate sut = CivilDate.of(2026, 11, 5);

            // WHEN
            int actualWeek = sut.isoWeek();

            // EXPECTATIONS: the ISO week java.time computes for the same day
            int expectedWeek = LocalDate.of(2026, 11, 5).get(IsoFields.WEEK_OF_WEEK_BASED_YEAR);

            // THEN
            assertEquals(expectedWeek, actualWeek);
        }
        // END weekday-week
    }

    @Nested
    class IsLeapYear {

        /**
         * Verifies that CivilDate.isLeapYear follows the Gregorian leap-year rules when given any kind of year.
         * Compared with: {@code Year.isLeap} gives the same answers.
         */
        @ParameterizedTest
        @ValueSource(ints = {
            2024, // divisible by 4: leap
            2026, // not divisible by 4: common
            1900, // divisible by 100 but not 400: common
            2000, // divisible by 400: leap
        })
        void agreesWithJavaTime_whenGivenAnyKindOfYear(int givenYear) {
            // SYSTEM UNDER TEST: 1 January of the given year
            CivilDate sut = CivilDate.of(givenYear, 1, 1);

            // WHEN
            boolean actualIsLeapYear = sut.isLeapYear();

            // EXPECTATIONS: java.time's answer for the same year
            boolean expectedIsLeapYear = Year.isLeap(givenYear);

            // THEN
            assertEquals(expectedIsLeapYear, actualIsLeapYear);
        }
    }

    @Nested
    class AddDays {

        // BEGIN add-days
        /**
         * Verifies that CivilDate.addDays returns the date that many days later when the days cross a month end.
         * Compared with: {@code LocalDate.plusDays} gives the same date.
         */
        @Test
        void returnsTheLaterDate_whenTheDaysCrossAMonthEnd() {
            // GIVEN: 30 days, more than remain in November
            int givenDays = 30;

            // SYSTEM UNDER TEST: 5 November 2026
            CivilDate sut = CivilDate.of(2026, 11, 5);

            // WHEN
            CivilDate actualDate = sut.addDays(givenDays);

            // EXPECTATIONS: the date java.time computes for the same addition
            CivilDate expectedDate = CivilDate.from(LocalDate.of(2026, 11, 5).plusDays(30));

            // THEN
            assertEquals(expectedDate, actualDate);
        }
        // END add-days
    }

    @Nested
    class AddMonths {

        // BEGIN month-end
        /**
         * Verifies that CivilDate.addMonths returns none when the day is missing from the target month.
         * Compared with: {@code LocalDate.plusMonths} clamps 31 January to 28 February without saying so.
         */
        @Test
        void returnsNone_whenTheDayIsMissingFromTheTargetMonth() {
            // GIVEN: one month, into a February that has no day 31
            int givenMonths = 1;

            // SYSTEM UNDER TEST: 31 January 2026
            CivilDate sut = CivilDate.of(2026, 1, 31);

            // WHEN
            Outcome<CivilDate> actualOutcome = sut.addMonths(givenMonths);

            // EXPECTATIONS: no such date, explained by the month-end diagnostic
            Status expectedStatus = Status.NONE;
            String expectedCode = "month-end";

            // THEN
            assertEquals(expectedStatus, actualOutcome.status());
            assertTrue(actualOutcome.diagnostics().stream().anyMatch(d -> d.code().equals(expectedCode)), actualOutcome.why());
        }

        /**
         * Verifies that CivilDate.addMonths with MonthEnd.CONSTRAIN returns the last day of the target month when the
         * day is missing from it.
         * Compared with: {@code LocalDate.plusMonths} gives the same date, but without being asked.
         */
        @Test
        void returnsTheLastDayOfTheMonth_whenConstrainIsRequested() {
            // GIVEN: one month, constrained to the end of the target month
            int givenMonths = 1;
            MonthEnd givenPolicy = MonthEnd.CONSTRAIN;

            // SYSTEM UNDER TEST: 31 January 2026
            CivilDate sut = CivilDate.of(2026, 1, 31);

            // WHEN
            CivilDate actualDate = sut.addMonths(givenMonths, givenPolicy);

            // EXPECTATIONS: the date java.time clamps to
            CivilDate expectedDate = CivilDate.from(LocalDate.of(2026, 1, 31).plusMonths(1));

            // THEN
            assertEquals(expectedDate, actualDate);
        }
        // END month-end
    }

    @Nested
    class Add {

        /**
         * Verifies that CivilDate.add returns none when a one-month period lands on a missing day.
         * Compared with: {@code LocalDate.plus(java.time.Period.ofMonths(1))} clamps to 28 February without saying so.
         */
        @Test
        void returnsNone_whenAOneMonthPeriodLandsOnAMissingDay() {
            // GIVEN: a calendar period of one month
            Period givenPeriod = Period.months(1);

            // SYSTEM UNDER TEST: 31 January 2026
            CivilDate sut = CivilDate.of(2026, 1, 31);

            // WHEN
            Outcome<CivilDate> actualOutcome = sut.add(givenPeriod);

            // EXPECTATIONS: no such date under the default, rejecting month-end policy
            Status expectedStatus = Status.NONE;
            String expectedCode = "month-end";

            // THEN
            assertEquals(expectedStatus, actualOutcome.status());
            assertTrue(actualOutcome.diagnostics().stream().anyMatch(d -> d.code().equals(expectedCode)), actualOutcome.why());
        }
    }

    @Nested
    class DaysUntil {

        // BEGIN days-between
        /**
         * Verifies that CivilDate.daysUntil returns the number of days when the other date is later.
         * Compared with: {@code LocalDate.until(other, ChronoUnit.DAYS)} gives the same count.
         */
        @Test
        void returnsTheDayCount_whenTheOtherDateIsLater() {
            // GIVEN: Christmas Day 2026
            CivilDate givenChristmas = CivilDate.of(2026, 12, 25);

            // SYSTEM UNDER TEST: 5 November 2026
            CivilDate sut = CivilDate.of(2026, 11, 5);

            // WHEN
            long actualDays = sut.daysUntil(givenChristmas);

            // EXPECTATIONS: the count java.time computes
            long expectedDays = LocalDate.of(2026, 11, 5).until(LocalDate.of(2026, 12, 25), ChronoUnit.DAYS);

            // THEN
            assertEquals(expectedDays, actualDays);
        }
        // END days-between
    }

    @Nested
    class IsBefore {

        // BEGIN compare-dates
        /**
         * Verifies that CivilDate.isBefore returns true when this date comes first.
         * Compared with: {@code LocalDate.isBefore} gives the same answer. Plain dates always have an order.
         */
        @Test
        void returnsTrue_whenThisDateComesFirst() {
            // GIVEN: Christmas Day 2026
            CivilDate givenChristmas = CivilDate.of(2026, 12, 25);

            // SYSTEM UNDER TEST: 5 November 2026
            CivilDate sut = CivilDate.of(2026, 11, 5);

            // WHEN
            boolean actualComesFirst = sut.isBefore(givenChristmas);

            // EXPECTATIONS: java.time's answer for the same two days
            boolean expectedComesFirst = LocalDate.of(2026, 11, 5).isBefore(LocalDate.of(2026, 12, 25));

            // THEN
            assertEquals(expectedComesFirst, actualComesFirst);
        }
        // END compare-dates
    }

    @Nested
    class ToLocalDate {

        /**
         * Verifies that CivilDate.toLocalDate returns unsupported when the year is beyond LocalDate's last year.
         * Compared with: {@code LocalDate.MAX} is +999999999-12-31, so there is no LocalDate to return.
         */
        @Test
        void returnsUnsupported_whenTheYearIsBeyondLocalDateMax() {
            // SYSTEM UNDER TEST: 1 January of year 1,000,000,000
            CivilDate sut = CivilDate.of(1_000_000_000, 1, 1);

            // WHEN
            Outcome<LocalDate> actualOutcome = sut.toLocalDate();

            // EXPECTATIONS: unsupported, because the native type cannot hold the value
            Status expectedStatus = Status.UNSUPPORTED;
            String expectedCode = "out-of-native-range";

            // THEN
            assertEquals(expectedStatus, actualOutcome.status());
            assertTrue(actualOutcome.diagnostics().stream().anyMatch(d -> d.code().equals(expectedCode)), actualOutcome.why());
        }
    }
}
