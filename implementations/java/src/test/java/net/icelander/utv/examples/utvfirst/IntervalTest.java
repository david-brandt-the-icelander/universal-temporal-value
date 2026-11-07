package net.icelander.utv.examples.utvfirst;

import static org.junit.jupiter.api.Assertions.assertEquals;

import java.time.LocalDate;
import net.icelander.utv.Bound;
import net.icelander.utv.CivilDate;
import net.icelander.utv.Interval;
import net.icelander.utv.Truth;
import org.junit.jupiter.api.Nested;
import org.junit.jupiter.api.Test;

/**
 * How to use {@code Interval}, a range of dates or instants whose ends say whether they are included, unbounded or
 * unknown, compared with java.time, which has no range type: bounds are compared by hand and a missing end is
 * {@code null}, whatever the reason. There is no native-first counterpart.
 */
class IntervalTest {

    @Nested
    class HalfOpen {

        // BEGIN ranges
        /**
         * Verifies that a half-open Interval definitely contains the day when the day is between its ends.
         * Compared with: {@code !day.isBefore(first) && day.isBefore(end)} on {@code LocalDate} values says the same.
         */
        @Test
        void definitelyContainsTheDay_whenTheDayIsBetweenTheEnds() {
            // GIVEN: 15 November 2026
            CivilDate givenDay = CivilDate.of(2026, 11, 15);

            // SYSTEM UNDER TEST: November 2026, from the 1st up to but not including 1 December
            Interval<CivilDate> sut = Interval.halfOpen(CivilDate.of(2026, 11, 1), CivilDate.of(2026, 12, 1));

            // WHEN
            Truth actualContains = sut.contains(givenDay);

            // EXPECTATIONS: the answer java.time's hand-written bounds give
            LocalDate tmpDay = LocalDate.of(2026, 11, 15);
            boolean tmpNativeContains = !tmpDay.isBefore(LocalDate.of(2026, 11, 1)) && tmpDay.isBefore(LocalDate.of(2026, 12, 1));
            Truth expectedContains = tmpNativeContains ? Truth.DEFINITELY : Truth.IMPOSSIBLE;

            // THEN
            assertEquals(expectedContains, actualContains);
        }

        /**
         * Verifies that a half-open Interval cannot contain the day when the day is its excluded end.
         * Compared with: the hand-written {@code day.isBefore(end)} excludes it too, if nobody writes
         * {@code !day.isAfter(end)} by mistake.
         */
        @Test
        void cannotContainTheDay_whenTheDayIsTheExcludedEnd() {
            // GIVEN: 1 December 2026, the end of the interval
            CivilDate givenDay = CivilDate.of(2026, 12, 1);

            // SYSTEM UNDER TEST: November 2026, from the 1st up to but not including 1 December
            Interval<CivilDate> sut = Interval.halfOpen(CivilDate.of(2026, 11, 1), CivilDate.of(2026, 12, 1));

            // WHEN
            Truth actualContains = sut.contains(givenDay);

            // EXPECTATIONS: impossible, and written with the end's bracket open
            Truth expectedContains = Truth.IMPOSSIBLE;
            String expectedText = "[2026-11-01, 2026-12-01)";

            // THEN
            assertEquals(expectedContains, actualContains);
            assertEquals(expectedText, sut.toString());
        }
        // END ranges
    }

    @Nested
    class Closed {

        /**
         * Verifies that a closed Interval definitely contains the day when the day is its included end.
         * Compared with: {@code !day.isBefore(first) && !day.isAfter(last)} on {@code LocalDate} values says the same.
         */
        @Test
        void definitelyContainsTheDay_whenTheDayIsTheIncludedEnd() {
            // GIVEN: 30 November 2026, the last day of the interval
            CivilDate givenDay = CivilDate.of(2026, 11, 30);

            // SYSTEM UNDER TEST: November 2026, from the 1st to the 30th, both included
            Interval<CivilDate> sut = Interval.closed(CivilDate.of(2026, 11, 1), CivilDate.of(2026, 11, 30));

            // WHEN
            Truth actualContains = sut.contains(givenDay);

            // EXPECTATIONS: the answer java.time's hand-written bounds give
            LocalDate tmpDay = LocalDate.of(2026, 11, 30);
            boolean tmpNativeContains = !tmpDay.isBefore(LocalDate.of(2026, 11, 1)) && !tmpDay.isAfter(LocalDate.of(2026, 11, 30));
            Truth expectedContains = tmpNativeContains ? Truth.DEFINITELY : Truth.IMPOSSIBLE;

            // THEN
            assertEquals(expectedContains, actualContains);
        }
    }

    @Nested
    class Of {

        /**
         * Verifies that an Interval definitely contains a later day when its end is unbounded.
         * Compared with: a {@code LocalDate} end of {@code null} means "no end" here, by convention only.
         */
        @Test
        void definitelyContainsALaterDay_whenTheEndIsUnbounded() {
            // GIVEN: 1 January 3000
            CivilDate givenDay = CivilDate.of(3000, 1, 1);

            // SYSTEM UNDER TEST: from 1 November 2026 on, with no end at all
            Interval<CivilDate> sut = Interval.of(Bound.included(CivilDate.of(2026, 11, 1)), Bound.unbounded());

            // WHEN
            Truth actualContains = sut.contains(givenDay);

            // EXPECTATIONS: definitely, and written with ".." for the missing end
            Truth expectedContains = Truth.DEFINITELY;
            String expectedText = "[2026-11-01, ..)";

            // THEN
            assertEquals(expectedContains, actualContains);
            assertEquals(expectedText, sut.toString());
        }

        // BEGIN ranges
        /**
         * Verifies that an Interval only possibly contains a later day when its end is unknown.
         * Compared with: the same {@code null} end would be read as "no end", and the answer would wrongly be "yes".
         */
        @Test
        void possiblyContainsALaterDay_whenTheEndIsUnknown() {
            // GIVEN: 1 June 2027
            CivilDate givenDay = CivilDate.of(2027, 6, 1);

            // SYSTEM UNDER TEST: from 1 November 2026, until an end nobody knows yet
            Interval<CivilDate> sut = Interval.of(Bound.included(CivilDate.of(2026, 11, 1)), Bound.unknown());

            // WHEN
            Truth actualContains = sut.contains(givenDay);

            // EXPECTATIONS: possibly, and written with "?" for the unknown end
            Truth expectedContains = Truth.POSSIBLY;
            String expectedText = "[2026-11-01, ?)";

            // THEN
            assertEquals(expectedContains, actualContains);
            assertEquals(expectedText, sut.toString());
        }
        // END ranges
    }
}
