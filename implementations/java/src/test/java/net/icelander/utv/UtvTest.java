package net.icelander.utv;

import static org.junit.jupiter.api.Assertions.assertEquals;

import java.util.List;
import org.junit.jupiter.api.Nested;
import org.junit.jupiter.api.Test;

/**
 * Tests of {@link Utv}: the diagnostic codes structural validation reports for JSON text that can't be read
 * (docs/library-api.md §5).
 */
class UtvTest {

    @Nested
    class ValidateStructural {

        /**
         * Verifies that Utv.validateStructural reports duplicate-key, at the repeated key, when an object repeats a key.
         */
        @Test
        void reportsDuplicateKey_whenAnObjectRepeatsAKey() {
            // GIVEN: a date with two different months
            String givenJson = """
                {"schema":"utv/1.0","kind":"civil","value":{"form":"date","fields":{"year":"2026","month":11,"month":12,"day":5},"calendar":{"id":"gregory","chronology":"proleptic"}}}
                """;

            // WHEN
            UtvResult actualResult = Utv.validateStructural(givenJson);

            // EXPECTATIONS
            Status expectedStatus = Status.INVALID;
            List<String> expectedCodes = List.of("duplicate-key");
            List<String> expectedPaths = List.of("/value/fields/month");

            // THEN
            assertEquals(expectedStatus, actualResult.status());
            assertEquals(expectedCodes, actualResult.diagnostics().stream().map(Diagnostic::code).toList());
            assertEquals(expectedPaths, actualResult.diagnostics().stream().map(Diagnostic::path).toList());
        }

        /**
         * Verifies that Utv.validateStructural reports utv.json.malformed when the text is not JSON.
         */
        @Test
        void reportsMalformedJson_whenTheTextIsNotJson() {
            // GIVEN: an object that is never closed
            String givenJson = """
                {"schema":"utv/1.0","kind":"civil"
                """;

            // WHEN
            UtvResult actualResult = Utv.validateStructural(givenJson);

            // EXPECTATIONS
            Status expectedStatus = Status.INVALID;
            List<String> expectedCodes = List.of("utv.json.malformed");

            // THEN
            assertEquals(expectedStatus, actualResult.status());
            assertEquals(expectedCodes, actualResult.diagnostics().stream().map(Diagnostic::code).toList());
        }
    }
}
