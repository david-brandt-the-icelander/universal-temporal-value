package net.icelander.utv.examples;

import static org.junit.jupiter.api.Assertions.*;

import java.io.ByteArrayOutputStream;
import java.io.PrintStream;
import java.nio.charset.StandardCharsets;
import java.nio.file.Path;
import java.util.Locale;
import java.util.TimeZone;
import net.icelander.utv.RuleBundle;
import org.junit.jupiter.api.Test;

/**
 * Runs the everyday example end to end. Only its UTV outcomes are checked (they are deterministic);
 * the native lines may differ by host. Running it again under an unusual default zone and locale
 * shows the UTV side does not depend on either.
 */
class EverydayTest {
    static RuleBundle rules() throws Exception {
        return Everyday.loadRules(Path.of(System.getProperty("user.dir")).resolve(Everyday.RULES));
    }

    static String run(RuleBundle rules) {
        ByteArrayOutputStream buf = new ByteArrayOutputStream();
        int mismatches = Everyday.run(rules, new PrintStream(buf, true, StandardCharsets.UTF_8));
        String out = buf.toString(StandardCharsets.UTF_8);
        assertEquals(0, mismatches, out);
        return out;
    }

    @Test
    void everySectionRunsAndEveryUtvOutcomeIsAsExpected() throws Exception {
        String out = run(rules());
        for (String s : new String[] {"1. Now", "2. A date", "3. Invalid dates", "4. Adding", "5. Days between",
                "6. Times and date-times", "7. Parsing with offsets", "8. Time zones", "9. DST gap", "10. DST overlap",
                "11. Timestamps", "12. Durations vs periods", "13. Ranges", "14. Round trip"}) {
            assertTrue(out.contains("== " + s + " =="), s);
        }
        assertFalse(out.contains("MISMATCH"), out);
    }

    @Test
    void utvOutcomesDoNotDependOnTheHostZoneOrLocale() throws Exception {
        TimeZone zone = TimeZone.getDefault();
        Locale locale = Locale.getDefault();
        try {
            TimeZone.setDefault(TimeZone.getTimeZone("Pacific/Kiritimati"));
            Locale.setDefault(Locale.forLanguageTag("th-TH-u-nu-thai"));
            run(rules());
        } finally {
            TimeZone.setDefault(zone);
            Locale.setDefault(locale);
        }
    }
}
