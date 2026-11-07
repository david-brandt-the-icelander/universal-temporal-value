package net.icelander.utv.examples.nativefirst.time;

import static org.junit.jupiter.api.Assertions.assertEquals;

import java.time.Instant;
import java.time.ZoneId;
import java.time.ZonedDateTime;
import net.icelander.utv.TemporalContext;
import net.icelander.utv.examples.Fixtures;
import org.junit.jupiter.api.Nested;
import org.junit.jupiter.api.Test;

/**
 * What java.time's {@code java.time.Instant} does, compared with UTV's {@code Instant}. {@code Instant} below is
 * java.time's; UTV's is written in full.
 *
 * <p>{@code Instant.atZone} reads the host's tz database, so that test needs one that knows the 2027 rules of New
 * York. UTV reads only the pinned demo rules it is given.
 */
class InstantTest {

    @Nested
    class Now {

        /**
         * Verifies that Instant.now returns a reading that survives the trip through UTV when read at any time.
         * Compared with: UTV never reads the clock; {@code net.icelander.utv.Instant.from} is how the reading gets in,
         * and {@code toJavaInstant} gives it back.
         *
         * <p>This test reads the real clock on purpose: the round trip does not depend on the time read, so the result is
         * the same on every run. If it ever flakes, that is a finding about the conversion, not noise.
         */
        @Test
        void survivesTheTripThroughUtv_whenReadAtAnyTime() {
            // WHEN
            Instant actualReading = Instant.now();

            // EXPECTATIONS: the same reading after UTV has held it
            Instant expectedReading = net.icelander.utv.Instant.from(actualReading).toJavaInstant().single();

            // THEN
            assertEquals(expectedReading, actualReading);
        }
    }

    @Nested
    class OfEpochSecond {

        /**
         * Verifies that Instant.ofEpochSecond returns the Unix epoch when given zero seconds.
         * Compared with: {@code net.icelander.utv.Instant.fromUnixSeconds(0)} is the same moment.
         */
        @Test
        void returnsTheEpoch_whenGivenZero() {
            // GIVEN: zero seconds since 1970-01-01T00:00:00Z
            long givenSeconds = 0;

            // WHEN
            Instant actualEpoch = Instant.ofEpochSecond(givenSeconds);

            // EXPECTATIONS: the moment UTV returns for the same count
            Instant expectedEpoch = net.icelander.utv.Instant.fromUnixSeconds(0).toJavaInstant().single();

            // THEN
            assertEquals(expectedEpoch, actualEpoch);
        }

        /**
         * Verifies that Instant.ofEpochSecond keeps all nine fractional digits when given nanoseconds.
         * Compared with: {@code net.icelander.utv.Instant.fromUnixSeconds(1_793_795_400, 123_456_789)} keeps them too, and
         * writes the same text.
         */
        @Test
        void keepsEveryNanosecond_whenGivenNanoseconds() {
            // GIVEN: a moment on 4 November 2026, to the nanosecond
            long givenSeconds = 1_793_795_400;
            long givenNanoseconds = 123_456_789;

            // WHEN
            Instant actualInstant = Instant.ofEpochSecond(givenSeconds, givenNanoseconds);

            // EXPECTATIONS: the moment and the text UTV holds
            net.icelander.utv.Instant tmpUtv = net.icelander.utv.Instant.fromUnixSeconds(1_793_795_400, 123_456_789);
            Instant expectedInstant = tmpUtv.toJavaInstant().single();
            String expectedText = tmpUtv.toString();

            // THEN
            assertEquals(expectedInstant, actualInstant);
            assertEquals(expectedText, actualInstant.toString());
        }
    }

    @Nested
    class GetEpochSecond {

        /**
         * Verifies that Instant.getEpochSecond drops the fraction of a second when the moment has a quarter second.
         * Compared with: {@code net.icelander.utv.Instant.unixSeconds()} is the same floor, and {@code subSecond()} keeps
         * the rest exactly.
         */
        @Test
        void dropsTheFraction_whenTheMomentHasAQuarterSecond() {
            // SYSTEM UNDER TEST: 13:30:00.25 on 5 November 2026
            Instant sut = Instant.ofEpochMilli(1_793_885_400_250L);

            // WHEN
            long actualSeconds = sut.getEpochSecond();

            // EXPECTATIONS: the whole seconds UTV reports, without its quarter second
            long expectedSeconds = net.icelander.utv.Instant.fromUnixSeconds(1_793_885_400, 250_000_000).unixSeconds();

            // THEN
            assertEquals(expectedSeconds, actualSeconds);
        }
    }

    @Nested
    class AtZone {

        /**
         * Verifies that Instant.atZone returns the local wall time when given a zone.
         * Compared with: {@code net.icelander.utv.Instant.inZone} gives the same wall time and offset from the pinned
         * rules.
         */
        @Test
        void returnsTheLocalWallTime_whenGivenAZone() {
            // GIVEN: New York, from the host's tz database
            ZoneId givenZone = ZoneId.of("America/New_York");

            // SYSTEM UNDER TEST: 07:00 UTC on 1 June 2027
            Instant sut = Instant.parse("2027-06-01T07:00:00Z");

            // WHEN
            ZonedDateTime actualDateTime = sut.atZone(givenZone);

            // EXPECTATIONS: the wall time and offset UTV computes from the pinned rules
            TemporalContext tmpContext = TemporalContext.with(Fixtures.demoZoneRules());
            net.icelander.utv.Instant tmpInstant = net.icelander.utv.Instant.from(Instant.parse("2027-06-01T07:00:00Z"));
            ZonedDateTime expectedDateTime = tmpInstant.inZone("America/New_York", tmpContext).single().toZonedDateTime().single();

            // THEN
            assertEquals(expectedDateTime, actualDateTime);
        }
    }
}
