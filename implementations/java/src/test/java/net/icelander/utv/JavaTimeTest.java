package net.icelander.utv;

import static org.junit.jupiter.api.Assertions.*;

import com.fasterxml.jackson.databind.JsonNode;
import java.math.BigInteger;
import java.time.Duration;
import java.time.Instant;
import java.time.LocalDate;
import java.time.LocalDateTime;
import java.time.LocalTime;
import java.time.OffsetDateTime;
import java.time.Period;
import java.time.ZoneId;
import java.time.ZoneOffset;
import java.time.ZonedDateTime;
import java.util.Date;
import org.junit.jupiter.api.Test;

/** Native interop: the caller names the intent, local-zone values are refused, lossy conversions say so. */
class JavaTimeTest {
    static String iso(JsonNode v) { return Iso.formatOrNull(v); }

    @Test
    void localDateAndTimeConvertDirectly() {
        LocalDate d = LocalDate.of(2026, 11, 5);
        assertEquals(Values.date(2026, 11, 5), JavaTime.fromLocalDate(d));
        assertEquals(d, JavaTime.toLocalDate(JavaTime.fromLocalDate(d)).get().orElseThrow());
        LocalTime t = LocalTime.of(9, 30, 15, 250_000_000);
        assertEquals("09:30:15.25", iso(JavaTime.fromLocalTime(t)));
        assertEquals(t, JavaTime.toLocalTime(JavaTime.fromLocalTime(t)).get().orElseThrow());
        assertEquals("09:30:00", iso(JavaTime.fromLocalTime(LocalTime.of(9, 30))));
    }

    @Test
    void localDateTimeIsFloating() {
        JsonNode v = JavaTime.fromLocalDateTime(LocalDateTime.of(2026, 11, 5, 14, 30));
        assertFalse(v.get("value").has("zone"));
        assertEquals("2026-11-05T14:30:00", iso(v));
        assertEquals(Status.SYMBOLIC, Zones.resolve(v, null).status());
        NativeResult<LocalDateTime> back = JavaTime.toLocalDateTime(v);
        assertFalse(back.lossy());
        assertEquals(LocalDateTime.of(2026, 11, 5, 14, 30), back.value());
    }

    @Test
    void offsetDateTimeTakesTheCallersIntent() {
        OffsetDateTime odt = OffsetDateTime.parse("2026-11-05T14:30:00+01:00");
        JsonNode instant = JavaTime.fromOffsetDateTime(odt, JavaTime.As.INSTANT);
        JsonNode offset = JavaTime.fromOffsetDateTime(odt, JavaTime.As.OFFSET_DATE_TIME);
        JsonNode local = JavaTime.fromOffsetDateTime(odt, JavaTime.As.LOCAL_DATE_TIME);
        assertEquals("instant", instant.get("kind").textValue());
        assertEquals("2026-11-05T13:30:00Z", iso(instant));
        assertEquals("2026-11-05T14:30:00+01:00", iso(offset));
        assertEquals("2026-11-05T14:30:00", iso(local));
        assertEquals(odt, JavaTime.toOffsetDateTime(offset).get().orElseThrow());
        assertEquals(odt.toInstant(), JavaTime.toInstant(instant).get().orElseThrow());
        // +00:00 stays an offset of zero, unlike Z
        JsonNode zero = JavaTime.fromOffsetDateTime(OffsetDateTime.parse("2026-11-05T14:30:00+00:00"), JavaTime.As.OFFSET_DATE_TIME);
        assertEquals("2026-11-05T14:30:00+00:00", iso(zero));
    }

    @Test
    void zonedDateTimeBecomesAZonedIntentionWithItsOffsetAsserted() {
        ZonedDateTime zdt = ZonedDateTime.of(LocalDateTime.of(2027, 6, 1, 9, 0), ZoneId.of("Europe/Paris"));
        JsonNode v = JavaTime.fromZonedDateTime(zdt);
        assertEquals("follow-zone-id", v.get("value").get("zoneIntent").textValue());
        assertEquals(7200, v.get("value").get("offsetAssertion").intValue());
        assertEquals("2027-06-01T09:00:00+02:00[Europe/Paris]", iso(v));
        NativeResult<ZonedDateTime> back = JavaTime.toZonedDateTime(v);
        assertFalse(back.lossy());
        assertEquals(zdt, back.value());
        // the second of the two 01:30s in New York keeps its own offset through the round trip
        ZonedDateTime later = ZonedDateTime.of(LocalDateTime.of(2026, 11, 1, 1, 30), ZoneId.of("America/New_York")).withLaterOffsetAtOverlap();
        assertEquals(later, JavaTime.toZonedDateTime(JavaTime.fromZonedDateTime(later)).value());
        // a zone that is only an offset is a fixed-offset date-time
        JsonNode fixed = JavaTime.fromZonedDateTime(ZonedDateTime.of(LocalDateTime.of(2026, 11, 5, 14, 30), ZoneOffset.ofHours(1)));
        assertEquals("fixed-offset", fixed.get("value").get("zone").get("type").textValue());
    }

    @Test
    void instantsPeriodsAndDurations() {
        Instant i = Instant.ofEpochSecond(1793885400L, 250_000_000);
        assertEquals("2026-11-05T13:30:00.25Z", iso(JavaTime.fromInstant(i)));
        assertEquals(i, JavaTime.toInstant(JavaTime.fromInstant(i)).value());
        assertEquals(Period.of(1, 2, 3), JavaTime.toPeriod(JavaTime.fromPeriod(Period.of(1, 2, 3))).value());
        assertEquals(Period.ofDays(10), JavaTime.toPeriod(Values.period(0, 0, 1, 3, 0, 0, 0)).value());
        assertEquals(Duration.ofMillis(-1500), JavaTime.toDuration(JavaTime.fromDuration(Duration.ofMillis(-1500))).value());
        assertEquals(Values.posixSeconds(86400), JavaTime.fromDuration(Duration.ofDays(1)));
    }

    @Test
    void localZoneValuesAreRefused() {
        Date d = Date.from(Instant.parse("2026-11-05T13:30:00Z"));
        UtvResult asInstant = JavaTime.fromDate(d, JavaTime.As.INSTANT);
        assertEquals(Status.UNIQUE, asInstant.status());
        assertEquals("2026-11-05T13:30:00Z", iso(asInstant.value().orElseThrow()));
        for (JavaTime.As civil : new JavaTime.As[] {JavaTime.As.LOCAL_DATE_TIME, JavaTime.As.OFFSET_DATE_TIME}) {
            UtvResult r = JavaTime.fromDate(d, civil);
            assertEquals(Status.UNSUPPORTED, r.status());
            assertTrue(r.candidates().isEmpty());
            assertTrue(r.diagnostics().get(0).message().contains("systemDefault"));
        }
    }

    @Test
    void lossyConversionsBackSaySo() {
        JsonNode zoned = Iso.parseIso("2027-06-01T09:00+02:00[Europe/Paris]").value().orElseThrow();
        NativeResult<OffsetDateTime> odt = JavaTime.toOffsetDateTime(zoned);
        assertEquals(Status.UNIQUE, odt.status());
        assertTrue(odt.lossy());
        assertEquals("utv.loss", odt.diagnostics().get(0).code());
        assertEquals(OffsetDateTime.parse("2027-06-01T09:00+02:00"), odt.value());

        NativeResult<LocalDateTime> ldt = JavaTime.toLocalDateTime(zoned);
        assertTrue(ldt.lossy());

        // sub-nanosecond precision is truncated and reported
        JsonNode third = Json.instant(Rational.of(BigInteger.ONE, BigInteger.valueOf(3)));
        NativeResult<Instant> i = JavaTime.toInstant(third);
        assertTrue(i.lossy());
        assertEquals(Instant.ofEpochSecond(0, 333_333_333), i.value());
    }

    @Test
    void conversionsThatCannotHoldTheValueAreRefused() {
        assertEquals(Status.UNSUPPORTED, JavaTime.toLocalDate(Values.date(BigInteger.TEN.pow(10), 1, 1)).status());
        assertEquals(Status.INVALID, JavaTime.toLocalDate(Values.date(2026, 2, 30)).status());
        JsonNode noAssertion = Values.inZone(Values.dateTime(2027, 6, 1, 9, 0), "Europe/Paris");
        assertEquals(Status.UNSUPPORTED, JavaTime.toOffsetDateTime(noAssertion).status());
        assertEquals(Status.UNSUPPORTED, JavaTime.toZonedDateTime(noAssertion).status());
        assertEquals(Status.UNSUPPORTED, JavaTime.toOffsetDateTime(Values.dateTime(2027, 6, 1, 9, 0)).status());
        assertEquals(Status.UNSUPPORTED, JavaTime.toPeriod(Values.hours(1)).status());
        assertEquals(Status.UNSUPPORTED, JavaTime.toLocalTime(Values.time(23, 59, 60)).status());
        JsonNode wrongOffset = Iso.parseIso("2027-06-01T09:00+05:00[Europe/Paris]").value().orElseThrow();
        assertEquals(Status.INVALID, JavaTime.toZonedDateTime(wrongOffset).status());
        assertEquals(Status.UNSUPPORTED, JavaTime.toZonedDateTime(
                Iso.parseIso("2027-06-01T09:00+02:00[Not/AZone]").value().orElseThrow()).status());
        assertTrue(JavaTime.toLocalDate(Values.date(2026, 2, 30)).get().isEmpty());
    }
}
