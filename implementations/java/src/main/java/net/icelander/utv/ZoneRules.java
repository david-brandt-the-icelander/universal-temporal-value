package net.icelander.utv;

import java.io.IOException;
import java.nio.file.Files;
import java.nio.file.Path;
import java.util.Set;

/**
 * Zone rules loaded explicitly, in the format of {@code conformance/rules/demo-zones.json}. The
 * library has no built-in rules and never reads the host's time zone database. Hold them in a
 * {@link TemporalContext}.
 */
public final class ZoneRules {
    private final RuleBundle bundle;

    private ZoneRules(RuleBundle bundle) { this.bundle = bundle; }

    /** Reads a rule file. Throws {@link IllegalArgumentException} if it isn't a well-formed bundle. */
    public static ZoneRules load(Path path) throws IOException { return parse(Files.readString(path)); }

    /** Parses rule JSON. Throws {@link IllegalArgumentException} if it isn't a well-formed bundle. */
    public static ZoneRules parse(String json) { return new ZoneRules(RuleBundle.parse(json)); }

    /** Parses rule JSON, or {@code invalid} ({@code malformed-value}) if it isn't a well-formed bundle. */
    public static Outcome<ZoneRules> tryParse(String json) {
        try {
            return Outcome.unique(parse(json));
        } catch (IllegalArgumentException e) {
            return new Outcome.Invalid<>(java.util.List.of(new Diagnostic("malformed-value", "error", "", e.getMessage())));
        }
    }

    /** Wraps a bundle already loaded for the JSON layer. */
    public static ZoneRules of(RuleBundle bundle) { return new ZoneRules(java.util.Objects.requireNonNull(bundle)); }

    /** The bundle, for the JSON-layer functions in {@link Zones}. */
    public RuleBundle bundle() { return bundle; }

    /** The zone ids these rules cover. */
    public Set<String> zoneIds() { return bundle.zoneIds(); }

    /** The rule set's id and version, e.g. {@code demo-zones 2026-11-05}. */
    @Override public String toString() {
        return bundle.ruleSet() == null ? "zone rules" : bundle.ruleSet().path("id").asText() + " " + bundle.ruleSet().path("version").asText();
    }
}
