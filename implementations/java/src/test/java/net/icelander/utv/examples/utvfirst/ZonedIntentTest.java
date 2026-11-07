package net.icelander.utv.examples.utvfirst;

import static org.junit.jupiter.api.Assertions.assertEquals;
import static org.junit.jupiter.api.Assertions.assertInstanceOf;
import static org.junit.jupiter.api.Assertions.assertTrue;

import java.time.LocalDateTime;
import java.time.OffsetDateTime;
import java.time.ZoneId;
import java.time.ZoneOffset;
import java.time.ZonedDateTime;
import java.util.List;
import net.icelander.utv.CivilDateTime;
import net.icelander.utv.Gap;
import net.icelander.utv.Instant;
import net.icelander.utv.Outcome;
import net.icelander.utv.Overlap;
import net.icelander.utv.Status;
import net.icelander.utv.TemporalContext;
import net.icelander.utv.ZonedIntent;
import net.icelander.utv.examples.Fixtures;
import org.junit.jupiter.api.Nested;
import org.junit.jupiter.api.Test;

/**
 * How to use {@code ZonedIntent}, a wall time in a named zone that stays an intention until it is resolved against
 * explicit zone rules, compared with java.time's {@code ZonedDateTime}, which fixes an offset at once from the host's
 * tz database.
 *
 * <p>UTV reads only the pinned demo rules. The java.time side of the expectations reads the host's tz database, so
 * these tests need one that knows the 2026–2027 rules of New York and Paris.
 */
class ZonedIntentTest {

    @Nested
    class Resolve {

        // BEGIN meeting-in-paris
        /**
         * Verifies that ZonedIntent.resolve returns the one instant when the wall time happens exactly once.
         * Compared with: {@code ZonedDateTime.of(...).toInstant()} finds the same instant in the host's tz database.
         */
        @Test
        void returnsTheInstant_whenTheWallTimeHappensOnce() {
            // SETUP: the pinned demo zone rules, never the host's tz database
            TemporalContext envContext = TemporalContext.with(Fixtures.demoZoneRules());

            // SYSTEM UNDER TEST: a meeting at 09:00 on 1 June 2027 in Paris
            ZonedIntent sut = CivilDateTime.of(2027, 6, 1, 9, 0).inZone("Europe/Paris");

            // WHEN
            Outcome<Instant> actualOutcome = sut.resolve(envContext);

            // EXPECTATIONS: the instant java.time finds for the same wall time
            ZonedDateTime tmpMeeting = ZonedDateTime.of(LocalDateTime.of(2027, 6, 1, 9, 0), ZoneId.of("Europe/Paris"));
            Instant expectedInstant = Instant.from(tmpMeeting.toInstant());

            // THEN
            assertEquals(expectedInstant, actualOutcome.single());
        }
        // END meeting-in-paris

        // BEGIN dst-gap
        /**
         * Verifies that ZonedIntent.resolve returns none when the wall time falls in a daylight-saving gap.
         * Compared with: {@code ZonedDateTime.of} moves the wall time to 03:30 daylight time without saying so.
         */
        @Test
        void returnsNone_whenTheWallTimeFallsInAGap() {
            // SETUP: the pinned demo zone rules, never the host's tz database
            TemporalContext envContext = TemporalContext.with(Fixtures.demoZoneRules());

            // SYSTEM UNDER TEST: 02:30 on 8 March 2026 in New York, when clocks jump from 02:00 to 03:00
            ZonedIntent sut = CivilDateTime.of(2026, 3, 8, 2, 30).inZone("America/New_York");

            // WHEN
            Outcome<Instant> actualOutcome = sut.resolve(envContext);

            // EXPECTATIONS: no such instant, explained by the gap diagnostic
            Status expectedStatus = Status.NONE;
            String expectedCode = "gap";

            // THEN
            assertEquals(expectedStatus, actualOutcome.status());
            assertTrue(actualOutcome.diagnostics().stream().anyMatch(d -> d.code().equals(expectedCode)), actualOutcome.why());
        }

        /**
         * Verifies that ZonedIntent.resolve with Gap.SHIFT_FORWARD moves the wall time forward by the length of the gap
         * when asked to.
         * Compared with: {@code ZonedDateTime.of} gives the same instant, but without being asked.
         */
        @Test
        void shiftsForwardByTheGap_whenShiftForwardIsRequested() {
            // GIVEN: the policy that moves a skipped wall time forward by the gap
            Gap givenPolicy = Gap.SHIFT_FORWARD;

            // SETUP: the pinned demo zone rules, never the host's tz database
            TemporalContext envContext = TemporalContext.with(Fixtures.demoZoneRules());

            // SYSTEM UNDER TEST: 02:30 on 8 March 2026 in New York, when clocks jump from 02:00 to 03:00
            ZonedIntent sut = CivilDateTime.of(2026, 3, 8, 2, 30).inZone("America/New_York");

            // WHEN
            Outcome<Instant> actualOutcome = sut.resolve(envContext, givenPolicy);

            // EXPECTATIONS: 03:30 daylight time, the instant java.time moves to on its own
            ZonedDateTime tmpShifted = ZonedDateTime.of(LocalDateTime.of(2026, 3, 8, 2, 30), ZoneId.of("America/New_York"));
            Instant expectedInstant = Instant.from(tmpShifted.toInstant());

            // THEN
            assertEquals(expectedInstant, actualOutcome.single());
        }
        // END dst-gap

        // BEGIN dst-overlap
        /**
         * Verifies that ZonedIntent.resolve returns both instants when the wall time falls in an overlap.
         * Compared with: {@code ZonedDateTime.of} silently picks one of them, the earlier, daylight-time one.
         */
        @Test
        void returnsBothInstants_whenTheWallTimeFallsInAnOverlap() {
            // SETUP: the pinned demo zone rules, never the host's tz database
            TemporalContext envContext = TemporalContext.with(Fixtures.demoZoneRules());

            // SYSTEM UNDER TEST: 01:30 on 1 November 2026 in New York, when clocks go back from 02:00 to 01:00
            ZonedIntent sut = CivilDateTime.of(2026, 11, 1, 1, 30).inZone("America/New_York");

            // WHEN
            Outcome<Instant> actualOutcome = sut.resolve(envContext);

            // EXPECTATIONS: 01:30 daylight time, then 01:30 standard time; java.time's single answer is one of them
            List<Instant> expectedInstants = List.of(
                    Instant.from(OffsetDateTime.of(2026, 11, 1, 1, 30, 0, 0, ZoneOffset.ofHours(-4)).toInstant()),
                    Instant.from(OffsetDateTime.of(2026, 11, 1, 1, 30, 0, 0, ZoneOffset.ofHours(-5)).toInstant()));
            String expectedCode = "ambiguous";
            ZonedDateTime tmpNativePick = ZonedDateTime.of(LocalDateTime.of(2026, 11, 1, 1, 30), ZoneId.of("America/New_York"));
            Instant expectedNativePick = Instant.from(tmpNativePick.toInstant());

            // THEN
            Outcome.Multiple<?> actualMultiple = assertInstanceOf(Outcome.Multiple.class, actualOutcome);
            assertEquals(expectedInstants, actualMultiple.values());
            assertTrue(actualOutcome.diagnostics().stream().anyMatch(d -> d.code().equals(expectedCode)), actualOutcome.why());
            assertTrue(actualMultiple.values().contains(expectedNativePick));
        }

        /**
         * Verifies that ZonedIntent.resolve with Overlap.EARLIEST returns the first of the two instants when asked to.
         * Compared with: {@code ZonedDateTime.of} picks the same, earlier instant, but without being asked.
         */
        @Test
        void returnsTheEarlierInstant_whenEarliestIsRequested() {
            // GIVEN: the policy that picks the earlier of two instants
            Overlap givenPolicy = Overlap.EARLIEST;

            // SETUP: the pinned demo zone rules, never the host's tz database
            TemporalContext envContext = TemporalContext.with(Fixtures.demoZoneRules());

            // SYSTEM UNDER TEST: 01:30 on 1 November 2026 in New York, when clocks go back from 02:00 to 01:00
            ZonedIntent sut = CivilDateTime.of(2026, 11, 1, 1, 30).inZone("America/New_York");

            // WHEN
            Outcome<Instant> actualOutcome = sut.resolve(envContext, givenPolicy);

            // EXPECTATIONS: 01:30 daylight time, the instant java.time picks on its own
            ZonedDateTime tmpNativePick = ZonedDateTime.of(LocalDateTime.of(2026, 11, 1, 1, 30), ZoneId.of("America/New_York"));
            Instant expectedInstant = Instant.from(tmpNativePick.toInstant());

            // THEN
            assertEquals(expectedInstant, actualOutcome.single());
        }
        // END dst-overlap
    }

    @Nested
    class AddDays {

        // BEGIN day-vs-24h
        /**
         * Verifies that ZonedIntent.addDays keeps the wall time when the day crosses a daylight-saving change.
         * Compared with: {@code ZonedDateTime.plusDays} keeps noon too; {@code OffsetDateTime.plusDays} keeps the offset
         * instead, so the wall time moves to 13:00.
         */
        @Test
        void keepsTheWallTime_whenTheDayCrossesADstChange() {
            // GIVEN: one calendar day
            int givenDays = 1;

            // SYSTEM UNDER TEST: noon on 7 March 2026 in New York, the day before clocks spring forward
            ZonedIntent sut = CivilDateTime.of(2026, 3, 7, 12, 0).inZone("America/New_York");

            // WHEN
            ZonedIntent actualNextNoon = sut.addDays(givenDays);

            // EXPECTATIONS: the wall time java.time reaches, in the same zone
            ZonedDateTime tmpNextNoon = ZonedDateTime.of(LocalDateTime.of(2026, 3, 7, 12, 0), ZoneId.of("America/New_York")).plusDays(1);
            ZonedIntent expectedNextNoon = CivilDateTime.of(
                    tmpNextNoon.getYear(), tmpNextNoon.getMonthValue(), tmpNextNoon.getDayOfMonth(),
                    tmpNextNoon.getHour(), tmpNextNoon.getMinute()).inZone("America/New_York");

            // THEN
            assertEquals(expectedNextNoon, actualNextNoon);
        }
        // END day-vs-24h
    }

    @Nested
    class ToZonedDateTime {

        /**
         * Verifies that ZonedIntent.toZonedDateTime gives back exactly the native value when the value came from a
         * ZonedDateTime.
         * Compared with: {@code ZonedIntent.from} kept the zone and asserted java.time's offset; this conversion is the
         * one place UTV consults the host's tz database, to check that offset.
         */
        @Test
        void returnsTheOriginal_whenTheValueCameFromAZonedDateTime() {
            // GIVEN: a meeting at 09:00 on 1 June 2027 in Paris, as java.time holds it
            ZonedDateTime givenMeeting = ZonedDateTime.of(LocalDateTime.of(2027, 6, 1, 9, 0), ZoneId.of("Europe/Paris"));

            // SYSTEM UNDER TEST: the same meeting as a UTV value
            ZonedIntent sut = ZonedIntent.from(givenMeeting);

            // WHEN
            Outcome<ZonedDateTime> actualOutcome = sut.toZonedDateTime();

            // EXPECTATIONS: the very same native value
            ZonedDateTime expectedMeeting = ZonedDateTime.of(LocalDateTime.of(2027, 6, 1, 9, 0), ZoneId.of("Europe/Paris"));

            // THEN
            assertEquals(expectedMeeting, actualOutcome.single());
        }
    }
}
