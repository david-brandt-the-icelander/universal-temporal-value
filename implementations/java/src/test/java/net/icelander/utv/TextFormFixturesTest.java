package net.icelander.utv;

import static org.junit.jupiter.api.Assertions.*;

import com.fasterxml.jackson.databind.JsonNode;
import java.util.ArrayList;
import java.util.List;
import org.junit.jupiter.api.DynamicTest;
import org.junit.jupiter.api.TestFactory;

/**
 * Runs every fixture in the shared {@code conformance/operations/text-form.json}, read in place,
 * through the JSON-level {@link TextForm#parseText} and {@link TextForm#formatText}.
 */
class TextFormFixturesTest {
    static final String FIXTURES = "conformance/operations/text-form.json";

    @TestFactory
    List<DynamicTest> textFormFixtures() throws Exception {
        List<DynamicTest> tests = new ArrayList<>();
        for (JsonNode f : ConformanceTest.load(FIXTURES).get("fixtures")) {
            String id = f.get("id").textValue();
            tests.add(DynamicTest.dynamicTest(id, () -> check(f)));
        }
        assertFalse(tests.isEmpty(), "no fixtures in " + FIXTURES);
        return tests;
    }

    static void check(JsonNode f) throws Exception {
        String direction = f.get("direction").textValue();
        JsonNode expect = f.path("expect");
        String status = expect.path("status").asText("unique");
        if (direction.equals("both") || direction.equals("parse")) {
            UtvResult r = TextForm.parseText(f.get("text").textValue());
            String where = f.get("id").textValue() + " parse -> " + r.toJson();
            assertEquals("parseText", r.operation(), where);
            assertEquals(status, r.status().wire(), where);
            if (status.equals("unique")) {
                JsonNode want = f.has("value") ? f.get("value") : expect.get("value");
                assertEquals(want, EverydayFixturesTest.normalize(r.value().orElseThrow()), where);
            }
            EverydayFixturesTest.assertDiagnostics(expect, r, where);
        }
        if (direction.equals("both") || direction.equals("format")) {
            UtvResult r = TextForm.formatText(f.get("value"));
            String where = f.get("id").textValue() + " format -> " + r.toJson();
            assertEquals("formatText", r.operation(), where);
            assertEquals(status, r.status().wire(), where);
            if (status.equals("unique")) assertEquals(f.get("text").textValue(), r.value().orElseThrow().textValue(), where);
            EverydayFixturesTest.assertDiagnostics(expect, r, where);
        }
    }
}
