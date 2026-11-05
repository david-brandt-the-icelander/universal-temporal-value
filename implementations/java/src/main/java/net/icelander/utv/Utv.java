package net.icelander.utv;

import com.fasterxml.jackson.core.JsonParser;
import com.fasterxml.jackson.databind.JsonNode;
import com.fasterxml.jackson.databind.ObjectMapper;
import java.util.ArrayList;
import java.util.Iterator;
import java.util.List;
import java.util.Set;
import java.util.regex.Pattern;

/**
 * Structural validation of UTV 1.0 values. Pure: never reads the clock, time zone or locale.
 */
public final class Utv {
    public static final String SPEC_VERSION = "utv/1.0";
    private static final Pattern WELL_FORMED = Pattern.compile("utv/(0|[1-9][0-9]*)\\.(0|[1-9][0-9]*)");
    private static final Set<String> TOP = Set.of("schema", "kind", "value", "provenance", "extensions");
    private static final ObjectMapper MAPPER = new ObjectMapper()
            .enable(JsonParser.Feature.STRICT_DUPLICATE_DETECTION);

    private Utv() {}

    /** Parse JSON text with duplicate-key detection. */
    public static JsonNode parse(String json) throws java.io.IOException {
        return MAPPER.readTree(json);
    }

    /** Structurally validate a UTV JSON document. */
    public static UtvResult validateStructural(String json) {
        JsonNode node;
        try {
            node = parse(json);
        } catch (java.io.IOException e) {
            return result(Status.INVALID, List.of(Diagnostic.error("utv.json.malformed", "", "Malformed JSON or duplicate key: " + e.getMessage())));
        }
        return validateStructural(node);
    }

    /** Structurally validate an already-parsed UTV value. */
    public static UtvResult validateStructural(JsonNode node) {
        List<Diagnostic> d = new ArrayList<>();
        Status s = check(node, "", d);
        return result(s, d);
    }

    private static UtvResult result(Status s, List<Diagnostic> d) {
        return new UtvResult("validate", s, d);
    }

    /** Returns VALID, INVALID or UNSUPPORTED for one envelope (recursing into nested envelopes). */
    static Status check(JsonNode node, String path, List<Diagnostic> d) {
        if (node == null || !node.isObject()) {
            d.add(Diagnostic.error("utv.envelope.not-object", path, "UTV value must be a JSON object"));
            return Status.INVALID;
        }
        JsonNode schema = node.get("schema");
        if (schema == null || !schema.isTextual()) {
            d.add(Diagnostic.error("utv.envelope.schema", path + "/schema", "Missing or non-string schema"));
            return Status.INVALID;
        }
        String sv = schema.textValue();
        if (!SPEC_VERSION.equals(sv)) {
            if (WELL_FORMED.matcher(sv).matches()) {
                d.add(new Diagnostic("utv.version.unsupported", "error", path + "/schema", "Specification version " + sv + " is not implemented"));
                return Status.UNSUPPORTED;
            }
            d.add(Diagnostic.error("utv.envelope.schema", path + "/schema", "Unknown schema identifier " + sv));
            return Status.INVALID;
        }
        Status st = Status.VALID;
        for (Iterator<String> it = node.fieldNames(); it.hasNext(); ) {
            String f = it.next();
            if (!TOP.contains(f)) {
                d.add(Diagnostic.error("utv.envelope.unknown-property", path + "/" + esc(f), "Unknown top-level property " + f));
                st = Status.INVALID;
            }
        }
        for (String f : new String[] {"provenance", "extensions"}) {
            if (node.has(f) && !node.get(f).isObject()) {
                d.add(Diagnostic.error("utv.envelope.type", path + "/" + f, f + " must be an object"));
                st = Status.INVALID;
            }
        }
        JsonNode kind = node.get("kind");
        KindTable.Rule rule = kind != null && kind.isTextual() ? KindTable.RULES.get(kind.textValue()) : null;
        if (rule == null) {
            d.add(Diagnostic.error("utv.kind.unknown", path + "/kind", "Missing or unknown kind"));
            return Status.INVALID;
        }
        JsonNode value = node.get("value");
        if (value == null || !value.isObject()) {
            d.add(Diagnostic.error("utv.value.missing", path + "/value", "Missing or non-object value"));
            return Status.INVALID;
        }
        String vp = path + "/value";
        for (String r : rule.required()) {
            if (!value.has(r)) {
                d.add(Diagnostic.error("utv.value.required", vp + "/" + esc(r), "Missing required property " + r + " for kind " + kind.textValue()));
                st = Status.INVALID;
            }
        }
        if (rule.closed()) {
            for (Iterator<String> it = value.fieldNames(); it.hasNext(); ) {
                String f = it.next();
                if (!rule.allowed().contains(f)) {
                    d.add(Diagnostic.error("utv.value.unknown-property", vp + "/" + esc(f), "Unknown property " + f));
                    st = Status.INVALID;
                }
            }
        }
        st = worse(st, KindRules.check(kind.textValue(), value, vp, d));
        return st;
    }

    static Status worse(Status a, Status b) {
        if (a == Status.INVALID || b == Status.INVALID) return Status.INVALID;
        if (a == Status.UNSUPPORTED || b == Status.UNSUPPORTED) return Status.UNSUPPORTED;
        return Status.VALID;
    }

    static String esc(String s) {
        return s.replace("~", "~0").replace("/", "~1");
    }
}
