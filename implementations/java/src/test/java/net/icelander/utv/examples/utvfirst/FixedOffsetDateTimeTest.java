package net.icelander.utv.examples.utvfirst;

import static org.junit.jupiter.api.Assertions.assertEquals;

import java.time.OffsetDateTime;
import java.time.ZoneOffset;
import net.icelander.utv.CivilDateTime;
import net.icelander.utv.FixedOffsetDateTime;
import net.icelander.utv.Instant;
import net.icelander.utv.Outcome;
import org.junit.jupiter.api.Nested;
import org.junit.jupiter.api.Test;

/**
 * How to use {@code FixedOffsetDateTime}, a wall time pinned to a UTC offset (but to no zone), compared with
 * java.time's {@code OffsetDateTime}.
 */
class FixedOffsetDateTimeTest {

    @Nested
    class From {

        /**
         * Verifies that FixedOffsetDateTime.from keeps the wall time and the offset when given an OffsetDateTime.
         * Compared with: the {@code OffsetDateTime} it was made from has the same wall time and offset;
         * {@code Instant.from} reads the same value as an instant instead, because the caller names the intent.
         */
        @Test
        void keepsTheWallTimeAndOffset_whenGivenAnOffsetDateTime() {
            // GIVEN: lunch at 14:30 on 5 November 2026, one hour east of UTC
            OffsetDateTime givenLunch = OffsetDateTime.of(2026, 11, 5, 14, 30, 0, 0, ZoneOffset.ofHours(1));

            // WHEN
            FixedOffsetDateTime actualLunch = FixedOffsetDateTime.from(givenLunch);

            // EXPECTATIONS: the same wall time and offset as the native value
            CivilDateTime expectedWallTime = CivilDateTime.from(OffsetDateTime.of(2026, 11, 5, 14, 30, 0, 0, ZoneOffset.ofHours(1)).toLocalDateTime());
            int expectedOffsetSeconds = ZoneOffset.ofHours(1).getTotalSeconds();

            // THEN
            assertEquals(expectedWallTime, actualLunch.dateTime());
            assertEquals(expectedOffsetSeconds, actualLunch.offsetSeconds());
        }
    }

    @Nested
    class ToOffsetDateTime {

        // BEGIN round-trip
        /**
         * Verifies that FixedOffsetDateTime.toOffsetDateTime gives back exactly the native value when the value came
         * from an OffsetDateTime.
         * Compared with: {@code OffsetDateTime.equals} checks the wall time and the offset, not just the instant.
         */
        @Test
        void returnsTheOriginal_whenTheValueCameFromAnOffsetDateTime() {
            // GIVEN: lunch at 14:30 on 5 November 2026, one hour east of UTC
            OffsetDateTime givenLunch = OffsetDateTime.of(2026, 11, 5, 14, 30, 0, 0, ZoneOffset.ofHours(1));

            // SYSTEM UNDER TEST: the same lunch as a UTV value
            FixedOffsetDateTime sut = FixedOffsetDateTime.from(givenLunch);

            // WHEN
            Outcome<OffsetDateTime> actualOutcome = sut.toOffsetDateTime();

            // EXPECTATIONS: the very same native value
            OffsetDateTime expectedLunch = OffsetDateTime.of(2026, 11, 5, 14, 30, 0, 0, ZoneOffset.ofHours(1));

            // THEN
            assertEquals(expectedLunch, actualOutcome.single());
        }
        // END round-trip
    }

    @Nested
    class ToInstant {

        /**
         * Verifies that FixedOffsetDateTime.toInstant returns the UTC moment when the offset is known.
         * Compared with: {@code OffsetDateTime.toInstant} names the same moment.
         */
        @Test
        void returnsTheUtcMoment_whenTheOffsetIsKnown() {
            // SYSTEM UNDER TEST: 14:30 on 5 November 2026, one hour east of UTC
            FixedOffsetDateTime sut = CivilDateTime.of(2026, 11, 5, 14, 30).atOffset(3600);

            // WHEN
            Instant actualInstant = sut.toInstant();

            // EXPECTATIONS: the moment java.time computes for the same value
            Instant expectedInstant = Instant.from(OffsetDateTime.of(2026, 11, 5, 14, 30, 0, 0, ZoneOffset.ofHours(1)).toInstant());

            // THEN
            assertEquals(expectedInstant, actualInstant);
        }
    }
}
