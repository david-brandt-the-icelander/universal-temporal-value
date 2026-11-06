package net.icelander.utv.examples.utvfirst;

import static org.junit.jupiter.api.Assertions.assertEquals;
import static org.junit.jupiter.api.Assertions.assertInstanceOf;

import java.time.OffsetDateTime;
import net.icelander.utv.FixedOffsetDateTime;
import net.icelander.utv.Instant;
import net.icelander.utv.Outcome;
import net.icelander.utv.TemporalValue;
import org.junit.jupiter.api.Nested;
import org.junit.jupiter.api.Test;

/**
 * How to use {@code TemporalValue.parse}, which reads any value in the UTV text form and returns the type the text
 * describes, compared with java.time's {@code OffsetDateTime.parse}. ({@code TemporalValue.parse} is the Java spelling
 * of the contract's {@code Utv.parse}.)
 */
class TemporalValueTest {

    @Nested
    class Parse {

        // BEGIN parse-offsets
        /**
         * Verifies that TemporalValue.parse returns an Instant when the text ends in Z.
         * Compared with: {@code OffsetDateTime.parse} gives an offset of +00:00, the same as for the text "+00:00".
         */
        @Test
        void returnsAnInstant_whenTheTextEndsInZ() {
            // GIVEN: 14:30 UTC, written with Z: a moment, with no local wall time implied
            String givenText = "2026-11-05T14:30:00Z";

            // WHEN
            Outcome<TemporalValue> actualOutcome = TemporalValue.parse(givenText);

            // EXPECTATIONS: the moment java.time parses from the same text
            Instant expectedInstant = Instant.from(OffsetDateTime.parse("2026-11-05T14:30:00Z").toInstant());

            // THEN
            Instant actualInstant = assertInstanceOf(Instant.class, actualOutcome.single());
            assertEquals(expectedInstant, actualInstant);
        }

        /**
         * Verifies that TemporalValue.parse returns a FixedOffsetDateTime when the text ends in +00:00.
         * Compared with: {@code OffsetDateTime.parse} cannot tell this text from the one ending in Z, and writes both as
         * {@code 2026-11-05T14:30Z}.
         */
        @Test
        void returnsAFixedOffsetDateTime_whenTheTextEndsInPlusZero() {
            // GIVEN: 14:30 at offset +00:00: a wall time somewhere that is at UTC+0 right now
            String givenText = "2026-11-05T14:30:00+00:00";

            // WHEN
            Outcome<TemporalValue> actualOutcome = TemporalValue.parse(givenText);

            // EXPECTATIONS: the value java.time parses from the same text, and the text kept as written
            FixedOffsetDateTime expectedDateTime = FixedOffsetDateTime.from(OffsetDateTime.parse("2026-11-05T14:30:00+00:00"));
            String expectedText = "2026-11-05T14:30:00+00:00";

            // THEN
            FixedOffsetDateTime actualDateTime = assertInstanceOf(FixedOffsetDateTime.class, actualOutcome.single());
            assertEquals(expectedDateTime, actualDateTime);
            assertEquals(expectedText, actualDateTime.toString());
        }
        // END parse-offsets
    }
}
