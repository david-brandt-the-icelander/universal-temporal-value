package net.icelander.utv.examples.utvfirst;

import static org.junit.jupiter.api.Assertions.assertEquals;
import static org.junit.jupiter.api.Assertions.assertTrue;

import net.icelander.utv.GregorianCivil;
import net.icelander.utv.Status;
import net.icelander.utv.UtvResult;
import org.junit.jupiter.api.Nested;
import org.junit.jupiter.api.Test;

/**
 * How to use {@code GregorianCivil.validate}, the semantic check of civil values in the proleptic Gregorian calendar at
 * the JSON layer, compared with java.time, where a {@code LocalDate} that exists is always valid and a date in another
 * chronology is converted through its epoch day.
 */
class GregorianCivilTest {

    @Nested
    class Validate {

        /**
         * Verifies that GregorianCivil.validate returns valid when the JSON names a real Gregorian date.
         * Compared with: {@code LocalDate.of(2026, 11, 5)} exists too.
         */
        @Test
        void returnsValid_whenTheDateExists() {
            // GIVEN: 5 November 2026 as UTV JSON: fields in a named calendar, with no time and no zone
            String givenJson = """
                { "schema": "utv/1.0", "kind": "civil",
                  "value": { "form": "date", "fields": { "year": "2026", "month": 11, "day": 5 },
                             "calendar": { "id": "gregory", "chronology": "proleptic" } } }
                """;

            // WHEN
            UtvResult actualResult = GregorianCivil.validate(givenJson);

            // EXPECTATIONS
            Status expectedStatus = Status.VALID;

            // THEN
            assertEquals(expectedStatus, actualResult.status());
        }

        /**
         * Verifies that GregorianCivil.validate returns invalid when well-formed JSON names a missing day.
         * Compared with: java.time has one check, in {@code LocalDate.of}, which throws; UTV keeps structure and
         * meaning as separate checks.
         */
        @Test
        void returnsInvalid_whenWellFormedJsonNamesAMissingDay() {
            // GIVEN: 29 February 2026 as UTV JSON: structurally valid, but 2026 is a common year
            String givenJson = """
                { "schema": "utv/1.0", "kind": "civil",
                  "value": { "form": "date", "fields": { "year": "2026", "month": 2, "day": 29 },
                             "calendar": { "id": "gregory", "chronology": "proleptic" } } }
                """;

            // WHEN
            UtvResult actualResult = GregorianCivil.validate(givenJson);

            // EXPECTATIONS: invalid, with the reason, instead of rolling over to 1 March
            Status expectedStatus = Status.INVALID;
            String expectedCode = "day-out-of-range";

            // THEN
            assertEquals(expectedStatus, actualResult.status());
            assertTrue(actualResult.diagnostics().stream().anyMatch(d -> d.code().equals(expectedCode)), actualResult.toJson().toString());
        }

        /**
         * Verifies that GregorianCivil.validate returns unsupported when the date is in another calendar.
         * Compared with: java.time has no Hebrew calendar; its other chronologies, such as {@code HijrahDate}, convert
         * to a Gregorian {@code LocalDate} through the epoch day (see {@code nativefirst.time.chrono.HijrahDateTest}).
         */
        @Test
        void returnsUnsupported_whenTheCalendarIsNotGregorian() {
            // GIVEN: 14 Heshvan 5787 as UTV JSON, in the Hebrew calendar
            String givenJson = """
                { "schema": "utv/1.0", "kind": "civil",
                  "value": { "form": "date", "fields": { "year": "5787", "month": 2, "day": 14 },
                             "calendar": { "id": "hebrew", "chronology": "proleptic" } } }
                """;

            // WHEN
            UtvResult actualResult = GregorianCivil.validate(givenJson);

            // EXPECTATIONS: unsupported, kept as written, rather than guessed or converted
            Status expectedStatus = Status.UNSUPPORTED;
            String expectedCode = "utv.unsupported";

            // THEN
            assertEquals(expectedStatus, actualResult.status());
            assertTrue(actualResult.diagnostics().stream().anyMatch(d -> d.code().equals(expectedCode)), actualResult.toJson().toString());
        }

        /**
         * Verifies that GregorianCivil.validate returns valid when the value is only a year.
         * Compared with: {@code Year.of(1347)} holds a year too, but most APIs that take a date need
         * {@code LocalDate.of(1347, 1, 1)}, which claims it was 1 January.
         */
        @Test
        void returnsValid_whenTheValueIsOnlyAYear() {
            // GIVEN: the year 1347 as UTV JSON
            String givenJson = """
                { "schema": "utv/1.0", "kind": "civil",
                  "value": { "form": "year", "fields": { "year": "1347" },
                             "calendar": { "id": "gregory", "chronology": "proleptic" } } }
                """;

            // WHEN
            UtvResult actualResult = GregorianCivil.validate(givenJson);

            // EXPECTATIONS
            Status expectedStatus = Status.VALID;

            // THEN
            assertEquals(expectedStatus, actualResult.status());
        }
    }
}
