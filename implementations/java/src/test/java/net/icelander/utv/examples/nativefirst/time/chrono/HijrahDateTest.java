package net.icelander.utv.examples.nativefirst.time.chrono;

import static org.junit.jupiter.api.Assertions.assertEquals;

import java.time.LocalDate;
import java.time.chrono.HijrahDate;
import org.junit.jupiter.api.Nested;
import org.junit.jupiter.api.Test;

/**
 * What java.time's {@code java.time.chrono.HijrahDate} does, compared with UTV, which keeps a date in another calendar
 * as its own fields and reports {@code unsupported} from its Gregorian check rather than converting. java.time has no
 * Hebrew calendar (the scenario's other languages use one); its Hijrah (Umm al-Qura) calendar plays the same part here.
 *
 * <p>{@code HijrahDate} reads the Umm al-Qura tables bundled with the JDK, which are fixed for a given JDK.
 */
class HijrahDateTest {

    @Nested
    class Of {

        /**
         * Verifies that HijrahDate.of creates a date that is the same day as a Gregorian LocalDate when given Hijrah
         * fields.
         * Compared with: UTV stores another calendar's fields as written, and {@code GregorianCivil.validate} returns
         * unsupported (utv.unsupported) instead of converting; it has no Hijrah rules.
         */
        @Test
        void createsTheSameDayAsAGregorianDate_whenGivenHijrahFields() {
            // GIVEN: 14 Jumada al-Ula 1448
            int givenYear = 1448;
            int givenMonth = 5;
            int givenDay = 14;

            // WHEN
            HijrahDate actualDate = HijrahDate.of(givenYear, givenMonth, givenDay);

            // EXPECTATIONS: the fields as written, and the day of 25 October 2026 (the day of 14 Heshvan 5787 as well)
            String expectedText = "Hijrah-umalqura AH 1448-05-14";
            long expectedEpochDay = LocalDate.of(2026, 10, 25).toEpochDay();

            // THEN
            assertEquals(expectedText, actualDate.toString());
            assertEquals(expectedEpochDay, actualDate.toEpochDay());
        }
    }
}
