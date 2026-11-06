package net.icelander.utv.examples.utvfirst;

import static org.junit.jupiter.api.Assertions.assertEquals;
import static org.junit.jupiter.api.Assertions.assertTrue;

import java.time.LocalDateTime;
import java.time.ZoneId;
import java.time.ZonedDateTime;
import net.icelander.utv.CivilDateTime;
import net.icelander.utv.Ordering;
import net.icelander.utv.Outcome;
import net.icelander.utv.Status;
import net.icelander.utv.TemporalContext;
import net.icelander.utv.ZoneRules;
import net.icelander.utv.ZonedIntent;
import net.icelander.utv.examples.Fixtures;
import org.junit.jupiter.api.Nested;
import org.junit.jupiter.api.Test;

/**
 * How to use {@code TemporalContext}, which holds the zone rules that zoned operations need, compared with java.time,
 * where every zoned operation reads the host's tz database.
 *
 * <p>UTV reads only the pinned demo rules. The java.time side of the expectations reads the host's tz database, so
 * these tests need one that knows the 2026–2027 rules of New York and Paris.
 */
class TemporalContextTest {

    @Nested
    class Compare {

        // BEGIN compare-dates
        /**
         * Verifies that TemporalContext.compare returns both orders when both wall times fall in the same overlap.
         * Compared with: {@code ZonedDateTime.isBefore} gives both the earlier, daylight-time offset and always says
         * "before".
         */
        @Test
        void returnsMultiple_whenBothWallTimesFallInAnOverlap() {
            // GIVEN: a call at 01:30 and a stand-up at 01:45, on the night New York's clocks go back from 02:00 to 01:00
            ZonedIntent givenCall = CivilDateTime.of(2026, 11, 1, 1, 30).inZone("America/New_York");
            ZonedIntent givenStandup = CivilDateTime.of(2026, 11, 1, 1, 45).inZone("America/New_York");

            // SETUP: the pinned demo zone rules, never the host's tz database
            ZoneRules envRules = Fixtures.demoZoneRules();

            // SYSTEM UNDER TEST
            TemporalContext sut = TemporalContext.with(envRules);

            // WHEN
            Outcome<Ordering> actualOutcome = sut.compare(givenCall, givenStandup);

            // EXPECTATIONS: either could come first (01:30 standard time is after 01:45 daylight time)
            Status expectedStatus = Status.MULTIPLE;
            String expectedCode = "ambiguous";

            // THEN
            assertEquals(expectedStatus, actualOutcome.status());
            assertTrue(actualOutcome.diagnostics().stream().anyMatch(d -> d.code().equals(expectedCode)), actualOutcome.why());
        }
        // END compare-dates

        /**
         * Verifies that TemporalContext.compare returns equal when two zoned wall times name the same instant.
         * Compared with: {@code ZonedDateTime.isEqual} gives the same answer for 09:00 in Paris and 03:00 in New York.
         */
        @Test
        void returnsEqual_whenTwoZonedWallTimesNameTheSameInstant() {
            // GIVEN: a meeting at 09:00 on 1 June 2027 in Paris, and the same meeting seen from New York
            ZonedIntent givenParis = CivilDateTime.of(2027, 6, 1, 9, 0).inZone("Europe/Paris");
            ZonedIntent givenNewYork = CivilDateTime.of(2027, 6, 1, 3, 0).inZone("America/New_York");

            // SETUP: the pinned demo zone rules, never the host's tz database
            ZoneRules envRules = Fixtures.demoZoneRules();

            // SYSTEM UNDER TEST
            TemporalContext sut = TemporalContext.with(envRules);

            // WHEN
            Outcome<Ordering> actualOutcome = sut.compare(givenParis, givenNewYork);

            // EXPECTATIONS: java.time's answer for the same two wall times
            ZonedDateTime tmpParis = ZonedDateTime.of(LocalDateTime.of(2027, 6, 1, 9, 0), ZoneId.of("Europe/Paris"));
            ZonedDateTime tmpNewYork = ZonedDateTime.of(LocalDateTime.of(2027, 6, 1, 3, 0), ZoneId.of("America/New_York"));
            Ordering expectedOrdering = tmpParis.isEqual(tmpNewYork) ? Ordering.EQUAL
                    : tmpParis.isBefore(tmpNewYork) ? Ordering.BEFORE
                    : Ordering.AFTER;

            // THEN
            assertEquals(expectedOrdering, actualOutcome.single());
        }
    }
}
