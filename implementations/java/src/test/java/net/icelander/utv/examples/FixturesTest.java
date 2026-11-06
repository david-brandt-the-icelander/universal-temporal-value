package net.icelander.utv.examples;

import static org.junit.jupiter.api.Assertions.assertEquals;

import java.util.Set;
import net.icelander.utv.ZoneRules;
import org.junit.jupiter.api.Nested;
import org.junit.jupiter.api.Test;

/** Tests of the shared test data in {@link Fixtures}. */
class FixturesTest {

    @Nested
    class DemoZoneRules {

        /**
         * Verifies that Fixtures.demoZoneRules loads the three demo zones when called.
         */
        @Test
        void containsTheThreeDemoZones_whenLoaded() {
            // WHEN
            ZoneRules actualRules = Fixtures.demoZoneRules();

            // EXPECTATIONS
            Set<String> expectedZoneIds = Set.of("America/New_York", "Atlantic/Reykjavik", "Europe/Paris");

            // THEN
            assertEquals(expectedZoneIds, actualRules.zoneIds());
        }
    }
}
