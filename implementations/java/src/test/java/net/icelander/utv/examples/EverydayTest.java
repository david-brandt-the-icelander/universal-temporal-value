package net.icelander.utv.examples;

import static org.junit.jupiter.api.Assertions.*;

import java.io.ByteArrayOutputStream;
import java.io.PrintStream;
import java.nio.charset.StandardCharsets;
import java.nio.file.Files;
import java.nio.file.Path;
import java.util.ArrayList;
import java.util.List;
import java.util.Locale;
import java.util.TimeZone;
import org.junit.jupiter.api.Test;

/**
 * The snapshot test for the everyday example (docs/library-api.md §7). It runs the example and
 * compares every line with {@code src/examples/resources/everyday.expected.txt}, except rows ending
 * in {@code " ‡"}, which depend on the host (clock, default zone, the JDK's tz database).
 *
 * <p>To re-record after a deliberate change, from {@code implementations/java}:
 * {@code mvn -q test-compile exec:java@everyday > src/examples/resources/everyday.expected.txt}
 */
class EverydayTest {
    static final Path DIR = Path.of(System.getProperty("user.dir"));
    static final Path SNAPSHOT = DIR.resolve("src/examples/resources/everyday.expected.txt");
    static final Path SOURCE = DIR.resolve("src/examples/java/net/icelander/utv/examples/Everyday.java");
    static final String HOST = " ‡";

    static final List<String> TASKS = List.of("now", "a-date", "weekday-week", "feb-30", "add-days", "month-end",
            "days-between", "compare-dates", "time-of-day", "datetime-plus-hours", "parse-offsets", "meeting-in-paris",
            "dst-gap", "dst-overlap", "timestamps", "day-vs-24h", "ranges", "round-trip");

    static List<String> run() throws Exception {
        PrintStream saved = System.out;
        ByteArrayOutputStream buf = new ByteArrayOutputStream();
        try {
            System.setOut(new PrintStream(buf, true, StandardCharsets.UTF_8));
            Everyday.main(new String[] {DIR.resolve(Everyday.RULES).normalize().toString()});
        } finally {
            System.setOut(saved);
        }
        return buf.toString(StandardCharsets.UTF_8).lines().toList();
    }

    static void assertMatchesSnapshot(List<String> got) throws Exception {
        List<String> want = Files.readAllLines(SNAPSHOT, StandardCharsets.UTF_8);
        String all = String.join("\n", got);
        assertEquals(want.size(), got.size(), "line count differs from the snapshot:\n" + all);
        for (int i = 0; i < want.size(); i++) {
            if (want.get(i).endsWith(HOST)) {
                assertTrue(got.get(i).endsWith(HOST), "line " + (i + 1) + " should be a host-dependent row: " + got.get(i));
            } else {
                assertEquals(want.get(i), got.get(i), "line " + (i + 1) + " differs from the snapshot");
            }
        }
    }

    @Test
    void outputMatchesTheSnapshot() throws Exception {
        assertMatchesSnapshot(run());
    }

    @Test
    void utvRowsDoNotDependOnTheHostZoneOrLocale() throws Exception {
        TimeZone zone = TimeZone.getDefault();
        Locale locale = Locale.getDefault();
        try {
            TimeZone.setDefault(TimeZone.getTimeZone("Pacific/Kiritimati"));
            Locale.setDefault(Locale.forLanguageTag("th-TH-u-nu-thai"));
            assertMatchesSnapshot(run());
        } finally {
            TimeZone.setDefault(zone);
            Locale.setDefault(locale);
        }
    }

    @Test
    void linesStayWithin100Columns() throws Exception {
        for (String line : run()) assertTrue(line.codePointCount(0, line.length()) <= 100, line);
    }

    @Test
    void everyTaskIsMarkedOnceInOrder() throws Exception {
        List<String> source = Files.readAllLines(SOURCE, StandardCharsets.UTF_8);
        List<String> begins = new ArrayList<>();
        for (int i = 0; i < source.size(); i++) {
            String line = source.get(i).strip();
            if (line.startsWith("// BEGIN ")) {
                String id = line.substring("// BEGIN ".length());
                begins.add(id);
                int end = source.subList(i, source.size()).stream().map(String::strip).toList().indexOf("// END " + id);
                assertTrue(end > 0, "no // END " + id);
            }
        }
        assertEquals(TASKS, begins);
    }
}
