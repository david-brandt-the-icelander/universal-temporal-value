package net.icelander.utv.examples;

import com.fasterxml.jackson.core.JsonProcessingException;
import com.fasterxml.jackson.databind.JsonNode;
import com.fasterxml.jackson.databind.ObjectMapper;
import com.fasterxml.jackson.databind.node.ObjectNode;
import java.io.IOException;
import java.io.PrintStream;
import java.nio.charset.StandardCharsets;
import java.nio.file.Files;
import java.nio.file.Path;
import java.util.ArrayList;
import java.util.List;
import net.icelander.utv.Diagnostic;
import net.icelander.utv.GregorianCivil;
import net.icelander.utv.Utv;
import net.icelander.utv.UtvResult;

/**
 * A tour of traditional date and time tasks in UTV.
 *
 * <p>First builds two dates in code and shows how a caller handles each outcome. Then walks the
 * shared catalog {@code examples/traditional.json}, validates every entry and compares the result
 * with the outcome the catalog expects. Exits with status 1 if any outcome differs.
 *
 * <p>Run from {@code implementations/java}: {@code mvn -q test-compile exec:java}
 */
public final class Traditional {
    private static final ObjectMapper JSON = new ObjectMapper();

    /** The catalog, relative to the project directory ({@code implementations/java}). */
    public static final String CATALOG = "../../examples/traditional.json";

    private Traditional() {}

    public static void main(String[] args) throws IOException {
        PrintStream out = new PrintStream(System.out, true, StandardCharsets.UTF_8); // the catalog has non-ASCII text
        buildingValuesInCode(out);
        Path catalog = Path.of(args.length > 0 ? args[0] : CATALOG);
        int mismatches = tour(loadCatalog(catalog), out);
        if (mismatches > 0) System.exit(1);
    }

    // ---- Part 1: values built from Java values, and what a caller does with each outcome ----

    static void buildingValuesInCode(PrintStream out) throws JsonProcessingException {
        out.println("== Building values in code ==");
        // java.time.LocalDate.of(2026, 2, 30) would throw. In UTV it is a well-formed value whose
        // meaning a separate semantic check rejects, so the caller decides what to do.
        for (int[] ymd : new int[][] {{2026, 11, 5}, {2026, 2, 30}}) {
            String json = JSON.writeValueAsString(civilDate(ymd[0], ymd[1], ymd[2]));
            UtvResult structural = Utv.validateStructural(json);
            UtvResult gregorian = GregorianCivil.validate(json);
            out.printf("%ncivilDate(%d, %d, %d)%n", ymd[0], ymd[1], ymd[2]);
            out.println("  structural: " + structural.status().wire());
            switch (gregorian.status()) {
                case VALID -> out.println("  gregorian:  valid, a real day: store it");
                case INVALID -> {
                    out.println("  gregorian:  invalid, no such day: reject it, don't roll over");
                    for (Diagnostic d : gregorian.diagnostics()) {
                        out.printf("    %s at %s: %s%n", d.code(), d.path(), d.message());
                    }
                }
                case UNSUPPORTED -> out.println("  gregorian:  unsupported: keep it, but don't guess");
                default -> out.println("  gregorian:  " + gregorian.status().wire());
            }
        }
    }

    /** A proleptic Gregorian civil date as a UTV 1.0 value. Years are decimal strings. */
    static ObjectNode civilDate(int year, int month, int day) {
        ObjectNode utv = JSON.createObjectNode().put("schema", Utv.SPEC_VERSION).put("kind", "civil");
        ObjectNode value = utv.putObject("value").put("form", "date");
        value.putObject("fields").put("year", Integer.toString(year)).put("month", month).put("day", day);
        value.putObject("calendar").put("id", "gregory").put("chronology", "proleptic");
        return utv;
    }

    // ---- Part 2: the shared catalog ----

    /** What the library reports for one catalog entry, and how that differs from the catalog. */
    public record Outcome(String structural, String gregorian, List<String> mismatches) {}

    public static JsonNode loadCatalog(Path path) throws IOException {
        return JSON.readTree(Files.readString(path.toAbsolutePath().normalize()));
    }

    /** The text to validate: the entry's raw {@code text} if present, else its serialized {@code value}. */
    public static String textOf(JsonNode entry) throws JsonProcessingException {
        JsonNode text = entry.get("text");
        return text != null ? text.textValue() : JSON.writeValueAsString(entry.get("value"));
    }

    /** Runs the checks the entry's {@code expect} block names. {@code gregorian} is null when not expected. */
    public static Outcome check(JsonNode entry) throws JsonProcessingException {
        String text = textOf(entry);
        JsonNode expect = entry.get("expect");
        List<String> mismatches = new ArrayList<>();
        String structural = Utv.validateStructural(text).status().wire();
        compare("structural", expect.get("structural").textValue(), structural, mismatches);
        String gregorian = null;
        if (expect.has("gregorian")) {
            gregorian = GregorianCivil.validate(text).status().wire();
            compare("gregorian", expect.get("gregorian").textValue(), gregorian, mismatches);
        }
        return new Outcome(structural, gregorian, mismatches);
    }

    private static void compare(String check, String expected, String got, List<String> mismatches) {
        if (!expected.equals(got)) mismatches.add(check + " expected " + expected + ", got " + got);
    }

    /** Prints the tour and returns the number of mismatched checks. */
    public static int tour(JsonNode catalog, PrintStream out) throws JsonProcessingException {
        JsonNode entries = catalog.get("entries");
        String section = null;
        int mismatches = 0;
        for (JsonNode e : entries) {
            if (!e.get("section").textValue().equals(section)) {
                section = e.get("section").textValue();
                out.println();
                out.println("== " + section + " ==");
            }
            Outcome o = check(e);
            out.println();
            out.println(e.get("title").textValue());
            out.println("  usual:      " + e.get("usual").textValue());
            out.println("  structural: " + o.structural());
            if (o.gregorian() != null) out.println("  gregorian:  " + o.gregorian());
            out.println("  why:        " + e.get("point").textValue());
            if (e.has("later")) out.println("  later:      " + e.get("later").textValue());
            for (String m : o.mismatches()) out.println("  MISMATCH: " + m);
            mismatches += o.mismatches().size();
        }
        out.println();
        out.println(mismatches == 0
                ? entries.size() + " examples, every outcome as expected."
                : mismatches + " mismatches.");
        return mismatches;
    }
}
