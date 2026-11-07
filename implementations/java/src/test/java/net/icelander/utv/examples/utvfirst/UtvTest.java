package net.icelander.utv.examples.utvfirst;

import static org.junit.jupiter.api.Assertions.assertEquals;
import static org.junit.jupiter.api.Assertions.assertTrue;

import net.icelander.utv.Status;
import net.icelander.utv.Utv;
import net.icelander.utv.UtvResult;
import org.junit.jupiter.api.Nested;
import org.junit.jupiter.api.Test;

/**
 * How to use {@code Utv.validateStructural}, which checks that JSON is well-formed UTV before anything interprets it,
 * compared with java.time, which has no temporal values beyond the everyday types: the values here are written as UTV
 * JSON because java.time has nothing to write them with. Java SE has no JSON parser either, so none of these tests
 * has a native-first counterpart.
 */
class UtvTest {

    @Nested
    class ValidateStructural {

        /**
         * Verifies that Utv.validateStructural returns valid when the value is only a year.
         * Compared with: {@code java.time.Year.of(1347)} holds a year, but a date-shaped field needs
         * {@code LocalDate.of(1347, 1, 1)}, which claims a day nobody knows.
         */
        @Test
        void returnsValid_whenTheValueIsOnlyAYear() {
            // GIVEN: the year 1347, with no month or day
            String givenJson = """
                { "schema": "utv/1.0", "kind": "civil",
                  "value": { "form": "year", "fields": { "year": "1347" },
                             "calendar": { "id": "gregory", "chronology": "proleptic" } } }
                """;

            // WHEN
            UtvResult actualResult = Utv.validateStructural(givenJson);

            // EXPECTATIONS
            Status expectedStatus = Status.VALID;

            // THEN
            assertEquals(expectedStatus, actualResult.status());
        }

        /**
         * Verifies that Utv.validateStructural returns valid when the second is a leap second, leaving its meaning to a
         * later check.
         * Compared with: {@code LocalDateTime.of(2016, 12, 31, 23, 59, 60)} throws before anything can be stored.
         */
        @Test
        void returnsValid_whenTheSecondIsALeapSecond() {
            // GIVEN: 23:59:60 UTC on 31 December 2016, a real leap second
            String givenJson = """
                { "schema": "utv/1.0", "kind": "civil",
                  "value": { "form": "datetime",
                             "fields": { "year": "2016", "month": 12, "day": 31, "hour": 23, "minute": 59, "second": 60 },
                             "calendar": { "id": "gregory", "chronology": "proleptic" },
                             "zone": { "type": "fixed-offset", "seconds": 0 }, "zoneIntent": "fixed-offset" } }
                """;

            // WHEN
            UtvResult actualResult = Utv.validateStructural(givenJson);

            // EXPECTATIONS: well-formed; confirming the label needs a leap-second table
            Status expectedStatus = Status.VALID;

            // THEN
            assertEquals(expectedStatus, actualResult.status());
        }

        /**
         * Verifies that Utv.validateStructural returns valid when the interval is a start plus an amount.
         * Compared with: a java.time pair (start, start.plus(duration)) stores a computed end, and forgets how it was
         * written.
         */
        @Test
        void returnsValid_whenTheIntervalIsAStartPlusAnAmount() {
            // GIVEN: a two-hour meeting starting at 12:30 UTC on 4 November 2026
            String givenJson = """
                { "schema": "utv/1.0", "kind": "interval",
                  "value": { "domain": "physical", "construction": "start-amount",
                             "start": { "state": "included",
                                        "point": { "schema": "utv/1.0", "kind": "instant",
                                                   "value": { "system": { "system": "utv.timescale", "id": "unix-posix", "version": "1" },
                                                              "ticks": "1793795400" } } },
                             "amount": { "schema": "utv/1.0", "kind": "amount",
                                         "value": { "basis": "elapsed", "seconds": { "numerator": "7200", "denominator": "1" } } } } }
                """;

            // WHEN
            UtvResult actualResult = Utv.validateStructural(givenJson);

            // EXPECTATIONS
            Status expectedStatus = Status.VALID;

            // THEN
            assertEquals(expectedStatus, actualResult.status());
        }

        /**
         * Verifies that Utv.validateStructural returns valid when the value is a union of two separate ranges.
         * Compared with: java.time has no range type, so two ranges are a list of hand-made pairs.
         */
        @Test
        void returnsValid_whenTheValueIsAUnionOfRanges() {
            // GIVEN: 1–5 November and 20–25 November 2026, as one value
            String givenJson = """
                { "schema": "utv/1.0", "kind": "set",
                  "value": { "operation": "union", "members": [
                    { "schema": "utv/1.0", "kind": "interval",
                      "value": { "domain": "civil/gregory",
                                 "start": { "state": "included", "point": { "schema": "utv/1.0", "kind": "civil",
                                            "value": { "form": "date", "fields": { "year": "2026", "month": 11, "day": 1 },
                                                       "calendar": { "id": "gregory", "chronology": "proleptic" } } } },
                                 "end": { "state": "excluded", "point": { "schema": "utv/1.0", "kind": "civil",
                                          "value": { "form": "date", "fields": { "year": "2026", "month": 11, "day": 6 },
                                                     "calendar": { "id": "gregory", "chronology": "proleptic" } } } } } },
                    { "schema": "utv/1.0", "kind": "interval",
                      "value": { "domain": "civil/gregory",
                                 "start": { "state": "included", "point": { "schema": "utv/1.0", "kind": "civil",
                                            "value": { "form": "date", "fields": { "year": "2026", "month": 11, "day": 20 },
                                                       "calendar": { "id": "gregory", "chronology": "proleptic" } } } },
                                 "end": { "state": "excluded", "point": { "schema": "utv/1.0", "kind": "civil",
                                          "value": { "form": "date", "fields": { "year": "2026", "month": 11, "day": 26 },
                                                     "calendar": { "id": "gregory", "chronology": "proleptic" } } } } } } ] } }
                """;

            // WHEN
            UtvResult actualResult = Utv.validateStructural(givenJson);

            // EXPECTATIONS
            Status expectedStatus = Status.VALID;

            // THEN
            assertEquals(expectedStatus, actualResult.status());
        }

        /**
         * Verifies that Utv.validateStructural returns valid when the value is a weekly recurrence anchored in a named
         * zone.
         * Compared with: java.time has no recurrence type; a list of computed {@code ZonedDateTime}s goes stale when
         * zone rules change.
         */
        @Test
        void returnsValid_whenTheValueIsAWeeklyRecurrence() {
            // GIVEN: every Tuesday at 09:00 in Paris, from 3 November 2026, as an RFC 5545 rule
            String givenJson = """
                { "schema": "utv/1.0", "kind": "recurrence",
                  "value": { "intent": { "schema": "utv/1.0", "kind": "civil",
                                         "value": { "form": "datetime",
                                                    "fields": { "year": "2026", "month": 11, "day": 3, "hour": 9, "minute": 0 },
                                                    "calendar": { "id": "gregory", "chronology": "proleptic" },
                                                    "zone": { "type": "iana", "id": "Europe/Paris" }, "zoneIntent": "follow-zone-id" } },
                             "algorithm": { "system": "rfc", "id": "5545", "version": "1" },
                             "rule": "FREQ=WEEKLY;BYDAY=TU" } }
                """;

            // WHEN
            UtvResult actualResult = Utv.validateStructural(givenJson);

            // EXPECTATIONS
            Status expectedStatus = Status.VALID;

            // THEN
            assertEquals(expectedStatus, actualResult.status());
        }

        /**
         * Verifies that Utv.validateStructural returns valid when the year is uncertain, known only to lie within bounds.
         * Compared with: java.time can only store one value, such as {@code Year.of(1347)}, and hope nobody asks.
         */
        @Test
        void returnsValid_whenTheYearIsUncertain() {
            // GIVEN: about 1347, somewhere between 1340 and 1355
            String givenJson = """
                { "schema": "utv/1.0", "kind": "uncertain",
                  "value": { "target": { "schema": "utv/1.0", "kind": "civil",
                                         "value": { "form": "year", "fields": { "year": "1347" },
                                                    "calendar": { "id": "gregory", "chronology": "proleptic" } } },
                             "model": "bounds",
                             "parameters": { "earliest": { "year": "1340" }, "latest": { "year": "1355" },
                                             "interpretation": "possible range, not 95% confidence" } } }
                """;

            // WHEN
            UtvResult actualResult = Utv.validateStructural(givenJson);

            // EXPECTATIONS
            Status expectedStatus = Status.VALID;

            // THEN
            assertEquals(expectedStatus, actualResult.status());
        }

        /**
         * Verifies that Utv.validateStructural returns unsupported when the value is from a newer version.
         * Compared with: java.time has no versioned format; a hand-written reader would fail with a parse error, or read
         * the value as if nothing had changed.
         */
        @Test
        void returnsUnsupported_whenTheVersionIsNewer() {
            // GIVEN: a well-formed date labelled with a future specification version
            String givenJson = """
                { "schema": "utv/2.0", "kind": "civil",
                  "value": { "form": "date", "fields": { "year": "2026", "month": 11, "day": 5 },
                             "calendar": { "id": "gregory", "chronology": "proleptic" } } }
                """;

            // WHEN
            UtvResult actualResult = Utv.validateStructural(givenJson);

            // EXPECTATIONS: unsupported, not invalid: the value may be fine, this library just doesn't read it
            Status expectedStatus = Status.UNSUPPORTED;
            String expectedCode = "utv.version.unsupported";

            // THEN
            assertEquals(expectedStatus, actualResult.status());
            assertTrue(actualResult.diagnostics().stream().anyMatch(d -> d.code().equals(expectedCode)), actualResult.toJson().toString());
        }

        /**
         * Verifies that Utv.validateStructural returns invalid when the version label is malformed.
         * Compared with: java.time has no versioned format to check.
         */
        @Test
        void returnsInvalid_whenTheVersionLabelIsMalformed() {
            // GIVEN: a date labelled "utv/4", an unreleased draft label
            String givenJson = """
                { "schema": "utv/4", "kind": "civil",
                  "value": { "form": "date", "fields": { "year": "2026", "month": 11, "day": 5 },
                             "calendar": { "id": "gregory", "chronology": "proleptic" } } }
                """;

            // WHEN
            UtvResult actualResult = Utv.validateStructural(givenJson);

            // EXPECTATIONS
            Status expectedStatus = Status.INVALID;
            String expectedCode = "utv.envelope.schema";

            // THEN
            assertEquals(expectedStatus, actualResult.status());
            assertTrue(actualResult.diagnostics().stream().anyMatch(d -> d.code().equals(expectedCode)), actualResult.toJson().toString());
        }

        /**
         * Verifies that Utv.validateStructural returns invalid when a key appears twice.
         * Compared with: Java SE has no JSON parser, so there is no native-first counterpart; a lenient parser (such as
         * Jackson's default {@code ObjectMapper}) keeps the last month.
         */
        @Test
        void returnsInvalid_whenAKeyAppearsTwice() {
            // GIVEN: a date with two different months
            String givenJson = """
                {"schema":"utv/1.0","kind":"civil","value":{"form":"date","fields":{"year":"2026","month":11,"month":12,"day":5},"calendar":{"id":"gregory","chronology":"proleptic"}}}
                """;

            // WHEN
            UtvResult actualResult = Utv.validateStructural(givenJson);

            // EXPECTATIONS: invalid, rather than whichever month a parser happens to keep
            Status expectedStatus = Status.INVALID;
            String expectedCode = "duplicate-key";

            // THEN
            assertEquals(expectedStatus, actualResult.status());
            assertTrue(actualResult.diagnostics().stream().anyMatch(d -> d.code().equals(expectedCode)), actualResult.toJson().toString());
        }

        /**
         * Verifies that Utv.validateStructural returns invalid when the kind is unknown.
         * Compared with: java.time has no envelope; an unknown kind is rejected here, not ignored.
         */
        @Test
        void returnsInvalid_whenTheKindIsUnknown() {
            // GIVEN: a value of a kind the specification does not define
            String givenJson = """
                { "schema": "utv/1.0", "kind": "moment", "value": {} }
                """;

            // WHEN
            UtvResult actualResult = Utv.validateStructural(givenJson);

            // EXPECTATIONS: fail closed: an unknown kind is rejected, not ignored
            Status expectedStatus = Status.INVALID;
            String expectedCode = "utv.kind.unknown";

            // THEN
            assertEquals(expectedStatus, actualResult.status());
            assertTrue(actualResult.diagnostics().stream().anyMatch(d -> d.code().equals(expectedCode)), actualResult.toJson().toString());
        }
    }
}
