package net.icelander.utv;

import java.util.HashMap;
import java.util.List;
import java.util.Map;
import java.util.Set;

/** Per-kind structural rules for the 26 core kinds, generated from schema/utv-1.0.schema.json. */
final class KindTable {
    record Rule(boolean closed, Set<String> required, Set<String> allowed) {}

    static final Map<String, Rule> RULES;
    static {
        Map<String, Rule> m = new HashMap<>();
        k(m, "instant", true, List.of("system"), List.of("coordinate","quality","subTick","system","ticks"));
        k(m, "civil", true, List.of("form","fields","calendar"), List.of("calendar","fields","form","location","offsetAssertion","quality","resolutionPolicy","zone","zoneIntent"));
        k(m, "amount", true, List.of("basis"), List.of("basis","components","rules","seconds","sequence"));
        k(m, "interval", true, List.of("domain"), List.of("amount","arithmeticPolicy","construction","domain","empty","end","start"));
        k(m, "recurrence", true, List.of("intent","algorithm","rule"), List.of("adapterEdition","algorithm","duration","exceptions","exclusionDates","exclusionRules","inclusionDates","inclusionRules","intent","overrides","recurrenceIdentity","resolutionPolicy","rule","zoneRuleBinding"));
        k(m, "uncertain", true, List.of("target","model"), List.of("model","parameters","quality","target"));
        k(m, "set", true, List.of("operation","members"), List.of("members","operation","universe"));
        k(m, "clock", true, List.of("clockRef","generation","ticks","tickUnit"), List.of("clockRef","generation","quality","tickUnit","ticks"));
        k(m, "observation", true, List.of("role","reading","source"), List.of("latency","quality","reading","role","source"));
        k(m, "logical-clock", true, List.of("algorithm","actorNamespace","generation","state"), List.of("actorNamespace","algorithm","generation","state"));
        k(m, "relative", true, List.of("language","expression","anchor","context"), List.of("anchor","context","expression","language"));
        k(m, "relation", true, List.of("relation","left","right"), List.of("domain","left","relation","right"));
        k(m, "assertion", true, List.of("assertionId","subjectId","claimedTime","source"), List.of("assertionId","claimedTime","recordedAt","source","subjectId"));
        k(m, "event-history", true, List.of("subjectId","revisions"), List.of("revisions","subjectId"));
        k(m, "location-intent", true, List.of("authority","place"), List.of("authority","mapping","place","time"));
        k(m, "constraint", true, List.of("language","variables","expression"), List.of("expression","language","variables"));
        k(m, "calendar-rule", true, List.of("rules","intent"), List.of("intent","rules"));
        k(m, "temporal-reference", true, List.of("reference","coordinate"), List.of("coordinate","frame","reference"));
        k(m, "ordinal-position", true, List.of("system","label"), List.of("label","relationContext","sourceTerm","system"));
        k(m, "event-relative", true, List.of("anchor","matching","relation"), List.of("anchor","cardinality","fallback","matching","offset","referenceContext","relation","window"));
        k(m, "selection", true, List.of("source","predicate","take"), List.of("algorithm","cardinality","count","index","indexBase","orderBy","predicate","source","take"));
        k(m, "sample", true, List.of("subject","phenomenonTime","payload"), List.of("acquisitionTime","kindOfSupport","metadata","payload","phenomenonTime","processingTime","publicationTime","quality","resultTime","samplingTime","subject","timeCoordinates","validTime"));
        k(m, "series", true, List.of("id","temporalAxis"), List.of("aggregation","chunks","id","interpolation","measurement","missingData","ordering","quality","samples","samplingModel","snapshot","subject","temporalAxis","watermark"));
        k(m, "temporal-graph", true, List.of("nodes","edges","domain"), List.of("domain","edges","logic","nodes"));
        k(m, "sequence", true, List.of("ordering","cardinality"), List.of("cardinality","domain","duplicatePolicy","generator","members","ordering"));
        k(m, "pattern", true, List.of("frequency","per"), List.of("completionCriterion","constraints","frequency","onEvent","ontology","per","spacing","window"));
        RULES = Map.copyOf(m);
    }

    private static void k(Map<String, Rule> m, String kind, boolean closed, List<String> req, List<String> allowed) {
        m.put(kind, new Rule(closed, Set.copyOf(req), Set.copyOf(allowed)));
    }

    private KindTable() {}
}
