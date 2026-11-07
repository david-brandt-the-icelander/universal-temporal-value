package net.icelander.utv.examples.nativefirst.time.zone;

import static org.junit.jupiter.api.Assertions.assertEquals;

import java.time.LocalDateTime;
import java.time.ZoneId;
import java.time.ZoneOffset;
import java.time.zone.ZoneRules;
import java.util.List;
import net.icelander.utv.CivilDateTime;
import net.icelander.utv.Instant;
import net.icelander.utv.Outcome;
import net.icelander.utv.TemporalContext;
import net.icelander.utv.examples.Fixtures;
import org.junit.jupiter.api.Nested;
import org.junit.jupiter.api.Test;

/**
 * What java.time's {@code java.time.zone.ZoneRules} does, compared with UTV's {@code ZonedIntent.resolve}.
 * {@code ZoneRules} below is java.time's; UTV's rules come from the fixture.
 *
 * <p>{@code ZoneRules} comes from the host's tz database, so these tests need one that knows the 2026 rules of New
 * York. UTV reads only the pinned demo rules it is given.
 */
class ZoneRulesTest {

    @Nested
    class GetValidOffsets {

        /**
         * Verifies that ZoneRules.getValidOffsets returns no offsets when the wall time falls in a daylight-saving gap.
         * Compared with: {@code ZonedIntent.resolve} reports the same thing as none (gap); {@code ZonedDateTime.of}
         * ignores it and moves the wall time.
         */
        @Test
        void returnsNoOffsets_whenTheWallTimeFallsInAGap() {
            // GIVEN: 02:30 on 8 March 2026, when New York's clocks jump from 02:00 to 03:00
            LocalDateTime givenWallTime = LocalDateTime.of(2026, 3, 8, 2, 30);

            // SYSTEM UNDER TEST: New York's rules, from the host's tz database
            ZoneRules sut = ZoneId.of("America/New_York").getRules();

            // WHEN
            List<ZoneOffset> actualOffsets = sut.getValidOffsets(givenWallTime);

            // EXPECTATIONS: no offset exactly when UTV finds no instant
            TemporalContext tmpContext = TemporalContext.with(Fixtures.demoZoneRules());
            Outcome<Instant> tmpOutcome = CivilDateTime.of(2026, 3, 8, 2, 30).inZone("America/New_York").resolve(tmpContext);
            boolean expectedNoOffset = tmpOutcome instanceof Outcome.None<Instant>;

            // THEN
            assertEquals(expectedNoOffset, actualOffsets.isEmpty());
        }

        /**
         * Verifies that ZoneRules.getValidOffsets returns both offsets when the wall time falls in an overlap.
         * Compared with: {@code ZonedIntent.resolve} reports the same thing as multiple (ambiguous), with both instants,
         * in the same order.
         */
        @Test
        void returnsBothOffsets_whenTheWallTimeFallsInAnOverlap() {
            // GIVEN: 01:30 on 1 November 2026, when New York's clocks go back from 02:00 to 01:00
            LocalDateTime givenWallTime = LocalDateTime.of(2026, 11, 1, 1, 30);

            // SYSTEM UNDER TEST: New York's rules, from the host's tz database
            ZoneRules sut = ZoneId.of("America/New_York").getRules();

            // WHEN
            List<ZoneOffset> actualOffsets = sut.getValidOffsets(givenWallTime);

            // EXPECTATIONS: the offsets in force at each of the instants UTV finds: daylight time, then standard time
            TemporalContext tmpContext = TemporalContext.with(Fixtures.demoZoneRules());
            Outcome<Instant> tmpOutcome = CivilDateTime.of(2026, 11, 1, 1, 30).inZone("America/New_York").resolve(tmpContext);
            List<ZoneOffset> expectedOffsets = ((Outcome.Multiple<Instant>) tmpOutcome).values().stream()
                    .map(i -> ZoneOffset.ofTotalSeconds(i.inZone("America/New_York", tmpContext).single().assertedOffsetSeconds().getAsInt()))
                    .toList();

            // THEN
            assertEquals(expectedOffsets, actualOffsets);
        }
    }
}
