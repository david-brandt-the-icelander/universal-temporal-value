package net.icelander.utv.examples.utvfirst;

import static org.junit.jupiter.api.Assertions.assertEquals;
import static org.junit.jupiter.api.Assertions.assertTrue;

import java.util.Date;
import net.icelander.utv.JavaTime;
import net.icelander.utv.Status;
import net.icelander.utv.UtvResult;
import org.junit.jupiter.api.Nested;
import org.junit.jupiter.api.Test;

/**
 * How to use {@code JavaTime}, the JSON-layer adapters between UTV values and Java's own date and time types, compared
 * with what those types do on their own.
 */
class JavaTimeTest {

    @Nested
    class FromDate {

        // BEGIN now
        /**
         * Verifies that JavaTime.fromDate returns unsupported when asked for the wall time of a java.util.Date.
         * Compared with: {@code Date.toString()} and {@code LocalDateTime.now()} take the wall time from
         * {@code ZoneId.systemDefault()}, a zone that only means something on this machine.
         */
        @Test
        void returnsUnsupported_whenAskedForTheWallTime() {
            // GIVEN: a reading shaped like new Date()'s, whose fields come from the host's default zone
            Date givenReading = Date.from(java.time.Instant.parse("2026-11-05T13:30:00Z"));
            JavaTime.As givenIntent = JavaTime.As.LOCAL_DATE_TIME;

            // WHEN
            UtvResult actualResult = JavaTime.fromDate(givenReading, givenIntent);

            // EXPECTATIONS: refused, because the host's zone is hidden input
            Status expectedStatus = Status.UNSUPPORTED;
            String expectedCode = "local-zone";

            // THEN
            assertEquals(expectedStatus, actualResult.status());
            assertTrue(actualResult.diagnostics().stream().anyMatch(d -> d.code().equals(expectedCode)), actualResult.toJson().toString());
        }
        // END now
    }
}
