package net.icelander.utv;

import static org.junit.jupiter.api.Assertions.assertEquals;

import java.util.List;
import net.icelander.utv.examples.Fixtures;
import org.junit.jupiter.api.Nested;
import org.junit.jupiter.api.Test;

/**
 * Tests of {@link TemporalContext}: the diagnostics {@code compare} reports when the order of two values depends on
 * a daylight-saving overlap.
 */
class TemporalContextTest {

    @Nested
    class Compare {

        /**
         * Verifies that TemporalContext.compare reports one ambiguous diagnostic when both wall times fall in the same
         * overlap.
         */
        @Test
        void reportsOneAmbiguousDiagnostic_whenBothWallTimesFallInAnOverlap() {
            // GIVEN: 01:30 and 01:45 on the night New York's clocks go back from 02:00 to 01:00
            ZonedIntent givenCall = CivilDateTime.of(2026, 11, 1, 1, 30).inZone("America/New_York");
            ZonedIntent givenStandup = CivilDateTime.of(2026, 11, 1, 1, 45).inZone("America/New_York");

            // SETUP: the pinned demo zone rules
            ZoneRules envRules = Fixtures.demoZoneRules();

            // SYSTEM UNDER TEST
            TemporalContext sut = TemporalContext.with(envRules);

            // WHEN
            Outcome<Ordering> actualOutcome = sut.compare(givenCall, givenStandup);

            // EXPECTATIONS: either order is possible, explained once
            Status expectedStatus = Status.MULTIPLE;
            List<String> expectedCodes = List.of("ambiguous");

            // THEN
            assertEquals(expectedStatus, actualOutcome.status());
            assertEquals(expectedCodes, actualOutcome.diagnostics().stream().map(Diagnostic::code).toList());
        }

        /**
         * Verifies that TemporalContext.compare reports one ambiguous diagnostic when only one wall time falls in an
         * overlap.
         */
        @Test
        void reportsOneAmbiguousDiagnostic_whenOneWallTimeFallsInAnOverlap() {
            // GIVEN: 01:30 on the night New York's clocks go back, and 06:00 UTC, between its two instants
            ZonedIntent givenCall = CivilDateTime.of(2026, 11, 1, 1, 30).inZone("America/New_York");
            Instant givenInstant = Instant.parse("2026-11-01T06:00:00Z").single();

            // SETUP: the pinned demo zone rules
            ZoneRules envRules = Fixtures.demoZoneRules();

            // SYSTEM UNDER TEST
            TemporalContext sut = TemporalContext.with(envRules);

            // WHEN
            Outcome<Ordering> actualOutcome = sut.compare(givenCall, givenInstant);

            // EXPECTATIONS: either order is possible, explained once
            Status expectedStatus = Status.MULTIPLE;
            List<String> expectedCodes = List.of("ambiguous");

            // THEN
            assertEquals(expectedStatus, actualOutcome.status());
            assertEquals(expectedCodes, actualOutcome.diagnostics().stream().map(Diagnostic::code).toList());
        }
    }
}
