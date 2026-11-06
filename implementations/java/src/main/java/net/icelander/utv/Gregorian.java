package net.icelander.utv;

import java.math.BigInteger;

/** Proleptic Gregorian day arithmetic on arbitrary-precision years (days from 1970-01-01). */
final class Gregorian {
    private static final BigInteger B400 = BigInteger.valueOf(400), B146097 = BigInteger.valueOf(146097),
            B719468 = BigInteger.valueOf(719468);

    record Ymd(BigInteger year, int month, int day) {}

    private Gregorian() {}

    /** Days from 1970-01-01 to the given date (Hinnant's days_from_civil, exact for any year). */
    static BigInteger epochDay(BigInteger year, int month, int day) {
        BigInteger y = month <= 2 ? year.subtract(BigInteger.ONE) : year;
        BigInteger era = floorDiv(y, B400);
        int yoe = y.subtract(era.multiply(B400)).intValueExact();
        int mp = (month + 9) % 12;
        int doy = (153 * mp + 2) / 5 + day - 1;
        int doe = yoe * 365 + yoe / 4 - yoe / 100 + doy;
        return era.multiply(B146097).add(BigInteger.valueOf(doe)).subtract(B719468);
    }

    /** The date for a day count from 1970-01-01. */
    static Ymd fromEpochDay(BigInteger epochDay) {
        BigInteger z = epochDay.add(B719468);
        BigInteger era = floorDiv(z, B146097);
        int doe = z.subtract(era.multiply(B146097)).intValueExact();
        int yoe = (doe - doe / 1460 + doe / 36524 - doe / 146096) / 365;
        int doy = doe - (365 * yoe + yoe / 4 - yoe / 100);
        int mp = (5 * doy + 2) / 153;
        int d = doy - (153 * mp + 2) / 5 + 1;
        int m = mp < 10 ? mp + 3 : mp - 9;
        BigInteger y = era.multiply(B400).add(BigInteger.valueOf(yoe));
        if (m <= 2) y = y.add(BigInteger.ONE);
        return new Ymd(y, m, d);
    }

    /** ISO day of week, Monday = 1 .. Sunday = 7. */
    static int dayOfWeek(BigInteger epochDay) {
        return epochDay.add(BigInteger.valueOf(3)).mod(BigInteger.valueOf(7)).intValue() + 1;
    }

    static BigInteger floorDiv(BigInteger a, BigInteger b) {
        BigInteger[] qr = a.divideAndRemainder(b);
        return qr[1].signum() != 0 && (qr[1].signum() != b.signum()) ? qr[0].subtract(BigInteger.ONE) : qr[0];
    }
}
