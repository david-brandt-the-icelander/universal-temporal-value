package net.icelander.utv;

import static org.junit.jupiter.api.Assertions.*;

import com.fasterxml.jackson.databind.JsonNode;
import com.fasterxml.jackson.databind.ObjectMapper;
import java.nio.file.Files;
import java.util.ArrayList;
import java.util.HashMap;
import java.util.List;
import java.util.Map;
import org.junit.jupiter.api.DynamicTest;
import org.junit.jupiter.api.TestFactory;

/**
 * Runs every fixture in the shared {@code conformance/operations/everyday.json}, read in place from
 * the repository (never a copy). Each fixture is one dynamic test named by its id. The status,
 * the candidates and the diagnostic codes listed in {@code expect.diagnostics} are all checked.
 */
class EverydayFixturesTest {
    static final String FIXTURES = "conformance/operations/everyday.json";
    private static final ObjectMapper JSON = new ObjectMapper();

    @TestFactory
    List<DynamicTest> everydayFixtures() throws Exception {
        JsonNode file = ConformanceTest.load(FIXTURES);
        Map<String, RuleBundle> rules = new HashMap<>();
        file.get("rules").fields().forEachRemaining(e -> {
            try {
                rules.put(e.getKey(), RuleBundle.parse(Files.readString(ConformanceTest.ROOT.resolve(e.getValue().textValue()))));
            } catch (java.io.IOException x) {
                throw new IllegalStateException(x);
            }
        });
        List<DynamicTest> tests = new ArrayList<>();
        for (JsonNode f : file.get("fixtures")) {
            String id = f.get("id").textValue();
            tests.add(DynamicTest.dynamicTest(id, () -> check(id, f, rules)));
        }
        assertFalse(tests.isEmpty(), "no fixtures in " + FIXTURES);
        return tests;
    }

    static UtvResult run(String op, JsonNode a, Map<String, RuleBundle> rules) {
        JsonNode third = a.size() > 2 ? a.get(2) : null;
        return switch (op) {
            case "parseIso" -> Iso.parseIso(a.get(0).textValue());
            case "formatIso" -> Iso.formatIso(a.get(0));
            case "fields" -> Operations.fields(a.get(0));
            case "add" -> Operations.add(a.get(0), a.get(1), third);
            case "compare" -> Operations.compare(a.get(0), a.get(1));
            case "daysBetween" -> Operations.daysBetween(a.get(0), a.get(1));
            case "posixSecondsBetween" -> Operations.posixSecondsBetween(a.get(0), a.get(1));
            case "contains" -> Intervals.contains(a.get(0), a.get(1));
            case "overlaps" -> Intervals.overlaps(a.get(0), a.get(1));
            case "resolve" -> Zones.resolve(a.get(0), rules(rules, a.get(1)), third);
            case "toZone" -> Zones.toZone(a.get(0), a.get(1).textValue(), rules(rules, a.get(2)));
            case "toOffset" -> Zones.toOffset(a.get(0), a.get(1).intValue());
            default -> throw new AssertionError("unknown op " + op);
        };
    }

    private static RuleBundle rules(Map<String, RuleBundle> rules, JsonNode name) {
        RuleBundle r = rules.get(name.textValue());
        assertNotNull(r, "unknown rules " + name);
        return r;
    }

    /** Re-reads a node through text so numbers compare as JSON values, whatever node class built them. */
    static JsonNode normalize(JsonNode n) throws Exception {
        return JSON.readTree(JSON.writeValueAsString(n));
    }

    static void check(String id, JsonNode f, Map<String, RuleBundle> rules) throws Exception {
        JsonNode expect = f.get("expect");
        UtvResult r = run(f.get("op").textValue(), f.get("args"), rules);
        String where = id + " -> " + r.toJson();
        assertEquals(expect.get("status").textValue(), r.status().wire(), where);
        assertEquals("utv-result/1.0", r.schema(), where);
        assertEquals(f.get("op").textValue(), r.operation(), where);
        List<JsonNode> got = new ArrayList<>();
        for (JsonNode c : r.candidates()) got.add(normalize(c));
        switch (r.status()) {
            case UNIQUE -> assertEquals(List.of(expect.get("result")), got, where);
            case MULTIPLE -> {
                List<JsonNode> want = new ArrayList<>();
                expect.get("results").forEach(want::add);
                assertEquals(want, got, where);
            }
            default -> assertEquals(List.of(), got, where);
        }
        assertDiagnostics(expect, r, where);
    }

    /** Every code in {@code expect.diagnostics} must appear among the result's diagnostics (others may too). */
    static void assertDiagnostics(JsonNode expect, UtvResult r, String where) {
        List<String> codes = r.diagnostics().stream().map(Diagnostic::code).toList();
        for (JsonNode want : expect.path("diagnostics")) {
            assertTrue(codes.contains(want.textValue()), "missing diagnostic " + want.textValue() + ": " + where);
        }
    }
}
