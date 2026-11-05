package net.icelander.utv;

import com.fasterxml.jackson.databind.JsonNode;
import java.util.List;

/** Kind-specific structural rules beyond required/allowed property lists. */
final class KindRules {
    private KindRules() {}

    static Status check(String kind, JsonNode v, String vp, List<Diagnostic> d) {
        Status st = Status.VALID;
        switch (kind) {
            case "interval" -> st = interval(v, vp, d);
            case "series" -> {
                JsonNode samples = v.get("samples");
                if (samples != null) {
                    if (!samples.isArray()) {
                        d.add(Diagnostic.error("utv.value.type", vp + "/samples", "samples must be an array"));
                        return Status.INVALID;
                    }
                    for (int i = 0; i < samples.size(); i++) {
                        JsonNode s = samples.get(i);
                        String p = vp + "/samples/" + i;
                        if (!s.path("kind").asText("").equals("sample")) {
                            d.add(Diagnostic.error("utv.value.type", p + "/kind", "series samples must have kind sample"));
                            st = Status.INVALID;
                        } else {
                            st = Utv.worse(st, Utv.check(s, p, d));
                        }
                    }
                }
            }
            default -> { }
        }
        return st;
    }

    private static Status interval(JsonNode v, String vp, List<Diagnostic> d) {
        JsonNode c = v.get("construction");
        String[] need;
        if (c == null) {
            need = new String[] {"start", "end"};
        } else {
            switch (c.asText("")) {
                case "start-end" -> need = new String[] {"start", "end"};
                case "start-amount" -> need = new String[] {"start", "amount"};
                case "amount-end" -> need = new String[] {"amount", "end"};
                default -> {
                    d.add(Diagnostic.error("utv.value.enum", vp + "/construction", "Unknown interval construction"));
                    return Status.INVALID;
                }
            }
        }
        Status st = Status.VALID;
        for (String n : need) {
            if (!v.has(n)) {
                d.add(Diagnostic.error("utv.value.required", vp + "/" + n, "Interval construction requires " + n));
                st = Status.INVALID;
            }
        }
        return st;
    }
}
