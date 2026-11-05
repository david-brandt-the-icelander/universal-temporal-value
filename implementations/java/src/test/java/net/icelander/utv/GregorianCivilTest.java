package net.icelander.utv;

import static org.junit.jupiter.api.Assertions.*;

import java.math.BigInteger;
import org.junit.jupiter.api.Test;

class GregorianCivilTest {
    static String date(String year, int month, int day) {
        return "{\"schema\":\"utv/1.0\",\"kind\":\"civil\",\"value\":{\"form\":\"date\",\"fields\":{\"year\":\"" + year
                + "\",\"month\":" + month + ",\"day\":" + day + "},\"calendar\":{\"id\":\"gregory\",\"chronology\":\"proleptic\"}}}";
    }

    @Test
    void leapYears() {
        assertTrue(GregorianCivil.isLeapYear(BigInteger.valueOf(2000)));
        assertTrue(GregorianCivil.isLeapYear(BigInteger.valueOf(2024)));
        assertTrue(GregorianCivil.isLeapYear(BigInteger.ZERO));
        assertTrue(GregorianCivil.isLeapYear(BigInteger.valueOf(-4)));
        assertFalse(GregorianCivil.isLeapYear(BigInteger.valueOf(1900)));
        assertFalse(GregorianCivil.isLeapYear(BigInteger.valueOf(2026)));
        assertFalse(GregorianCivil.isLeapYear(BigInteger.valueOf(-100)));
    }

    @Test
    void dates() {
        assertEquals(Status.VALID, GregorianCivil.validate(date("2024", 2, 29)).status());
        assertEquals(Status.VALID, GregorianCivil.validate(date("2026", 12, 31)).status());
        assertEquals(Status.VALID, GregorianCivil.validate(date("123456789012345678901234567890", 1, 1)).status());
        assertEquals(Status.INVALID, GregorianCivil.validate(date("2026", 2, 29)).status());
        assertEquals(Status.INVALID, GregorianCivil.validate(date("1900", 2, 29)).status());
        assertEquals(Status.INVALID, GregorianCivil.validate(date("2024", 2, 30)).status());
        assertEquals(Status.INVALID, GregorianCivil.validate(date("2026", 4, 31)).status());
        assertEquals(Status.INVALID, GregorianCivil.validate(date("2026", 13, 1)).status());
        assertEquals(Status.INVALID, GregorianCivil.validate(date("2026", 1, 0)).status());
    }

    @Test
    void otherCalendarsUnsupported() {
        String hebrew = date("5786", 1, 1).replace("gregory", "hebrew");
        assertEquals(Status.UNSUPPORTED, GregorianCivil.validate(hebrew).status());
    }
}
