package net.icelander.utv;

import static org.junit.jupiter.api.Assertions.*;

import com.fasterxml.jackson.databind.JsonNode;
import java.nio.file.Files;
import java.nio.file.Path;
import java.util.ArrayList;
import java.util.Iterator;
import java.util.List;
import java.util.Map;
import org.junit.jupiter.api.DynamicTest;
import org.junit.jupiter.api.Test;
import org.junit.jupiter.api.TestFactory;

class ConformanceTest {
    /** Repo root, resolved relative to the project directory (implementations/java). */
    static final Path ROOT = Path.of(System.getProperty("user.dir")).resolve("../..").normalize();

    static JsonNode load(String rel) throws Exception {
        return Utv.parse(Files.readString(ROOT.resolve(rel)));
    }

    static List<DynamicTest> each(String rel, java.util.function.BiConsumer<String, JsonNode> body) throws Exception {
        JsonNode all = load(rel);
        List<DynamicTest> tests = new ArrayList<>();
        for (Iterator<Map.Entry<String, JsonNode>> it = all.fields(); it.hasNext(); ) {
            Map.Entry<String, JsonNode> e = it.next();
            tests.add(DynamicTest.dynamicTest(e.getKey(), () -> body.accept(e.getKey(), e.getValue())));
        }
        assertFalse(tests.isEmpty());
        return tests;
    }

    @TestFactory
    List<DynamicTest> validExamplesAreValid() throws Exception {
        return each("conformance/examples/valid.json", (name, v) -> {
            UtvResult r = Utv.validateStructural(v.toString());
            assertEquals(Status.VALID, r.status(), name + ": " + r.diagnostics());
        });
    }

    @TestFactory
    List<DynamicTest> invalidStructuralExamplesAreRejected() throws Exception {
        return each("conformance/examples/invalid-structural.json", (name, v) -> {
            UtvResult r = Utv.validateStructural(v.toString());
            assertNotEquals(Status.VALID, r.status(), name);
            assertFalse(r.diagnostics().isEmpty(), name);
        });
    }

    @TestFactory
    List<DynamicTest> validExamplesAreNeverInvalidAsGregorianDates() throws Exception {
        return each("conformance/examples/valid.json", (name, v) -> {
            Status s = GregorianCivil.validate(v.toString()).status();
            assertTrue(s == Status.VALID || s == Status.UNSUPPORTED, name + ": " + s);
        });
    }

    static final String INSTANT_VALUE = "\"value\":{\"system\":{\"system\":\"utv.timescale\",\"id\":\"unix-posix\",\"version\":\"1\"},\"ticks\":\"0\"}";

    @Test
    void envelopeRules() {
        assertEquals(Status.VALID, Utv.validateStructural("{\"schema\":\"utv/1.0\",\"kind\":\"instant\"," + INSTANT_VALUE + "}").status());
        assertEquals(Status.UNSUPPORTED, Utv.validateStructural("{\"schema\":\"utv/1.3\",\"kind\":\"instant\"," + INSTANT_VALUE + "}").status());
        assertEquals(Status.INVALID, Utv.validateStructural("{\"schema\":\"utv/1.01\",\"kind\":\"instant\"," + INSTANT_VALUE + "}").status());
        assertEquals(Status.UNSUPPORTED, Utv.validateStructural("{\"schema\":\"utv/2.0\",\"kind\":\"instant\"," + INSTANT_VALUE + "}").status());
        assertEquals(Status.UNSUPPORTED, Utv.validateStructural("{\"schema\":\"utv/0.9\",\"kind\":\"instant\"," + INSTANT_VALUE + "}").status());
        assertEquals(Status.INVALID, Utv.validateStructural("{\"schema\":\"utv/4\",\"kind\":\"instant\"," + INSTANT_VALUE + "}").status());
        assertEquals(Status.INVALID, Utv.validateStructural("{\"schema\":\"utv/1\",\"kind\":\"instant\"," + INSTANT_VALUE + "}").status());
        assertEquals(Status.INVALID, Utv.validateStructural("{\"schema\":1,\"kind\":\"instant\"," + INSTANT_VALUE + "}").status());
        assertEquals(Status.INVALID, Utv.validateStructural("{\"kind\":\"instant\"," + INSTANT_VALUE + "}").status());
        assertEquals(Status.INVALID, Utv.validateStructural("{\"schema\":\"utv/1.0\",\"kind\":\"instant\",\"x\":1," + INSTANT_VALUE + "}").status());
        assertEquals(Status.INVALID, Utv.validateStructural("{\"schema\":\"utv/1.0\",\"schema\":\"utv/1.0\",\"kind\":\"instant\"," + INSTANT_VALUE + "}").status());
        assertEquals(Status.INVALID, Utv.validateStructural("{\"schema\":\"utv/1.0\",\"kind\":\"bogus\"," + INSTANT_VALUE + "}").status());
        assertEquals(26, KindTable.RULES.size());
        UtvResult r = Utv.validateStructural("not json");
        assertEquals("utv-result/1.0", r.schema());
        assertEquals("invalid", r.status().wire());
    }
}
