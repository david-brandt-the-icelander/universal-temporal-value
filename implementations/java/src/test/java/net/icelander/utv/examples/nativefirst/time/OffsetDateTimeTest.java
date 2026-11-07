package net.icelander.utv.examples.nativefirst.time;

import static org.junit.jupiter.api.Assertions.assertEquals;

import java.time.OffsetDateTime;
import java.time.ZoneOffset;
import net.icelander.utv.CivilDateTime;
import net.icelander.utv.FixedOffsetDateTime;
import net.icelander.utv.PosixSeconds;
import org.junit.jupiter.api.Nested;
import org.junit.jupiter.api.Test;

/**
 * What java.time's {@code java.time.OffsetDateTime} does, compared with UTV's {@code FixedOffsetDateTime} and
 * {@code Instant}.
 */
class OffsetDateTimeTest {

    @Nested
    class Of {

        /**
         * Verifies that OffsetDateTime.of pins the wall time to the offset when given one.
         * Compared with: {@code CivilDateTime.of(2026, 11, 5, 14, 30).atOffset(3600)} is the same value, and its
         * {@code toInstant()} is the same moment.
         */
        @Test
        void pinsTheWallTimeToTheOffset_whenGivenAnOffset() {
            // GIVEN: one hour east of UTC
            ZoneOffset givenOffset = ZoneOffset.ofHours(1);

            // WHEN
            OffsetDateTime actualDateTime = OffsetDateTime.of(2026, 11, 5, 14, 30, 0, 0, givenOffset);

            // EXPECTATIONS: the value UTV holds for the same wall time and offset, and the moment it names
            FixedOffsetDateTime tmpUtv = CivilDateTime.of(2026, 11, 5, 14, 30).atOffset(3600);
            OffsetDateTime expectedDateTime = tmpUtv.toOffsetDateTime().single();
            java.time.Instant expectedInstant = tmpUtv.toInstant().toJavaInstant().single();

            // THEN
            assertEquals(expectedDateTime, actualDateTime);
            assertEquals(expectedInstant, actualDateTime.toInstant());
        }
    }

    @Nested
    class Parse {

        /**
         * Verifies that OffsetDateTime.parse gives the same value for text ending in Z as for text ending in +00:00.
         * Compared with: {@code TemporalValue.parse} returns an {@code Instant} for Z and a {@code FixedOffsetDateTime}
         * for +00:00; the two texts mean different things.
         */
        @Test
        void cannotTellZFromPlusZero_whenBothNameTheSameMoment() {
            // GIVEN: 14:30 UTC, written with Z
            String givenText = "2026-11-05T14:30:00Z";

            // WHEN
            OffsetDateTime actualValue = OffsetDateTime.parse(givenText);

            // EXPECTATIONS: exactly the value java.time gets from what UTV reads as a wall time at +00:00
            OffsetDateTime expectedValue = FixedOffsetDateTime.parse("2026-11-05T14:30:00+00:00").single().toOffsetDateTime().single();

            // THEN
            assertEquals(expectedValue, actualValue);
        }
    }

    @Nested
    class PlusDays {

        /**
         * Verifies that OffsetDateTime.plusDays keeps the offset when the day crosses a daylight-saving change.
         * Compared with: {@code ZonedIntent.addDays} keeps the wall time, noon, and lets the offset follow the zone; this
         * result is {@code Instant.add(PosixSeconds.of(86_400))}, 13:00 in New York.
         */
        @Test
        void keepsTheOffset_whenTheDayCrossesADstChange() {
            // GIVEN: one day
            long givenDays = 1;

            // SYSTEM UNDER TEST: noon on 7 March 2026 in New York, standard time, the day before clocks spring forward
            OffsetDateTime sut = OffsetDateTime.of(2026, 3, 7, 12, 0, 0, 0, ZoneOffset.ofHours(-5));

            // WHEN
            OffsetDateTime actualDayLater = sut.plusDays(givenDays);

            // EXPECTATIONS: exactly 86,400 seconds later, as UTV adds them, still at -05:00
            net.icelander.utv.Instant tmpNoon = net.icelander.utv.Instant.from(OffsetDateTime.of(2026, 3, 7, 12, 0, 0, 0, ZoneOffset.ofHours(-5)));
            java.time.Instant expectedInstant = tmpNoon.add(PosixSeconds.of(86_400)).toJavaInstant().single();
            ZoneOffset expectedOffset = ZoneOffset.ofHours(-5);

            // THEN
            assertEquals(expectedInstant, actualDayLater.toInstant());
            assertEquals(expectedOffset, actualDayLater.getOffset());
        }
    }

    @Nested
    class Equals {

        /**
         * Verifies that OffsetDateTime.equals returns true when the value made a round trip through UTV.
         * Compared with: {@code FixedOffsetDateTime.from} and {@code toOffsetDateTime} lose nothing.
         */
        @Test
        void returnsTrue_whenTheValueMadeARoundTripThroughUtv() {
            // GIVEN: the same lunch, after a round trip through UTV's FixedOffsetDateTime
            OffsetDateTime givenRoundTrip = FixedOffsetDateTime
                    .from(OffsetDateTime.of(2026, 11, 5, 14, 30, 0, 0, ZoneOffset.ofHours(1)))
                    .toOffsetDateTime()
                    .single();

            // SYSTEM UNDER TEST: lunch at 14:30 on 5 November 2026, one hour east of UTC
            OffsetDateTime sut = OffsetDateTime.of(2026, 11, 5, 14, 30, 0, 0, ZoneOffset.ofHours(1));

            // WHEN
            boolean actualEqual = sut.equals(givenRoundTrip);

            // EXPECTATIONS
            boolean expectedEqual = true;

            // THEN
            assertEquals(expectedEqual, actualEqual);
        }
    }
}
