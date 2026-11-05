package net.icelander.utv.examples;

import static org.junit.jupiter.api.Assertions.*;

import com.fasterxml.jackson.databind.JsonNode;
import java.nio.file.Path;
import java.util.ArrayList;
import java.util.List;
import net.icelander.utv.GregorianCivil;
import net.icelander.utv.Utv;
import org.junit.jupiter.api.DynamicTest;
import org.junit.jupiter.api.Test;
import org.junit.jupiter.api.TestFactory;

/** Every expectation in examples/traditional.json, checked with the example runner's own logic. */
class TraditionalTest {
    static JsonNode catalog() throws Exception {
        return Traditional.loadCatalog(Path.of(System.getProperty("user.dir")).resolve(Traditional.CATALOG));
    }

    @TestFactory
    List<DynamicTest> everyCatalogOutcomeIsAsExpected() throws Exception {
        List<DynamicTest> tests = new ArrayList<>();
        for (JsonNode e : catalog().get("entries")) {
            tests.add(DynamicTest.dynamicTest(e.get("id").textValue(), () ->
                    assertEquals(List.of(), Traditional.check(e).mismatches(), e.get("title").textValue())));
        }
        assertFalse(tests.isEmpty());
        return tests;
    }

    @Test
    void handBuiltDatesValidateAsDescribed() throws Exception {
        String good = Traditional.civilDate(2026, 11, 5).toString();
        String feb30 = Traditional.civilDate(2026, 2, 30).toString();
        assertEquals("valid", Utv.validateStructural(feb30).status().wire());
        assertEquals("valid", GregorianCivil.validate(good).status().wire());
        assertEquals("invalid", GregorianCivil.validate(feb30).status().wire());
    }
}
