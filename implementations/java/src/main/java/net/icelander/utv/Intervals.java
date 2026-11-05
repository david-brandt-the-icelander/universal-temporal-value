package net.icelander.utv;

import com.fasterxml.jackson.databind.JsonNode;
import java.math.BigInteger;

/**
 * {@code contains} and {@code overlaps} on civil-date intervals and instant intervals, with
 * three-valued answers: {@code "definitely"}, {@code "possibly"} or {@code "impossible"}.
 */
public final class Intervals {
    private Intervals() {}

    private enum Tri {
        TRUE, FALSE, UNKNOWN;
        Tri and(Tri o) {
            if (this == FALSE || o == FALSE) return FALSE;
            return this == TRUE && o == TRUE ? TRUE : UNKNOWN;
        }
        String answer() { return this == TRUE ? "definitely" : this == FALSE ? "impossible" : "possibly"; }
        static Tri of(boolean b) { return b ? TRUE : FALSE; }
    }

    private enum State { INCLUDED, EXCLUDED, UNBOUNDED, UNKNOWN }

    private record End(State state, Rational at) {
        boolean known() { return at != null; }
    }

    /** A decoded interval; points are epoch days (civil/gregory) or exact POSIX seconds (physical). */
    private record Span(boolean dates, End start, End end, boolean flaggedEmpty) {
        /** Discrete dates normalized to half-open [start, end). */
        Span halfOpen() {
            if (!dates) return this;
            End s = start.state == State.EXCLUDED ? new End(State.INCLUDED, start.at.add(Rational.of(1))) : start;
            End e = end.state == State.INCLUDED ? new End(State.EXCLUDED, end.at.add(Rational.of(1))) : end;
            return new Span(true, s, e, flaggedEmpty);
        }

        boolean provablyEmpty() {
            if (flaggedEmpty) return true;
            if (!start.known() || !end.known()) return false;
            int c = start.at.compareTo(end.at);
            return c > 0 || (c == 0 && !(start.state == State.INCLUDED && end.state == State.INCLUDED));
        }
    }

    /** Whether the interval contains the point (a date for civil/gregory, an instant for physical). */
    public static UtvResult contains(JsonNode interval, JsonNode point) {
        return Fail.run("contains", () -> {
            Span s = decode(interval, "interval");
            Rational p = point(point, s.dates, "point");
            sameZone(interval, point);
            if (s.halfOpen().provablyEmpty()) return answer("contains", Tri.FALSE);
            Tri lo = switch (s.start.state) {
                case INCLUDED -> Tri.of(s.start.at.compareTo(p) <= 0);
                case EXCLUDED -> Tri.of(s.start.at.compareTo(p) < 0);
                case UNBOUNDED -> Tri.TRUE;
                case UNKNOWN -> Tri.UNKNOWN;
            };
            Tri hi = switch (s.end.state) {
                case INCLUDED -> Tri.of(p.compareTo(s.end.at) <= 0);
                case EXCLUDED -> Tri.of(p.compareTo(s.end.at) < 0);
                case UNBOUNDED -> Tri.TRUE;
                case UNKNOWN -> Tri.UNKNOWN;
            };
            return answer("contains", lo.and(hi));
        });
    }

    /** Whether two intervals of the same domain share at least one point. */
    public static UtvResult overlaps(JsonNode a, JsonNode b) {
        return Fail.run("overlaps", () -> {
            Span x = decode(a, "a"), y = decode(b, "b");
            if (x.dates != y.dates) throw Fail.unsupported("utv.interval.domain", "/value/domain", "cannot mix civil-date and instant intervals");
            sameZone(a, b);
            x = x.halfOpen();
            y = y.halfOpen();
            if (x.provablyEmpty() || y.provablyEmpty()) return answer("overlaps", Tri.FALSE);
            return answer("overlaps", before(x.start, y.end).and(before(y.start, x.end)));
        });
    }

    /** "start is before end": <= when both are included, < otherwise; unbounded is true, unknown unknown. */
    private static Tri before(End start, End end) {
        if (start.state == State.UNKNOWN || end.state == State.UNKNOWN) {
            return start.state == State.UNBOUNDED || end.state == State.UNBOUNDED ? Tri.TRUE : Tri.UNKNOWN;
        }
        if (start.state == State.UNBOUNDED || end.state == State.UNBOUNDED) return Tri.TRUE;
        int c = start.at.compareTo(end.at);
        return Tri.of(start.state == State.INCLUDED && end.state == State.INCLUDED ? c <= 0 : c < 0);
    }

    private static UtvResult answer(String op, Tri t) {
        return UtvResult.unique(op, Json.F.textNode(t.answer()));
    }

    private static Span decode(JsonNode node, String what) {
        Civil.structural(node, what);
        if (!"interval".equals(node.path("kind").asText(null))) throw Fail.unsupported("utv.kind", "/kind", what + " must be an interval");
        JsonNode v = node.get("value");
        String construction = v.path("construction").asText("start-end");
        if (!construction.equals("start-end")) throw Fail.unsupported("utv.interval.construction", "/value/construction", "only start-end intervals are handled");
        boolean dates = switch (v.path("domain").asText("")) {
            case "civil/gregory" -> true;
            case "physical" -> false;
            default -> throw Fail.unsupported("utv.interval.domain", "/value/domain", "only civil/gregory and physical intervals are handled");
        };
        return new Span(dates, end(v.get("start"), dates, "/value/start"), end(v.get("end"), dates, "/value/end"),
                v.path("empty").asBoolean(false));
    }

    private static End end(JsonNode e, boolean dates, String path) {
        if (e == null || !e.isObject()) throw Fail.invalid("utv.interval.endpoint", path, "endpoint must be an object");
        State s = switch (e.path("state").asText("")) {
            case "included" -> State.INCLUDED;
            case "excluded" -> State.EXCLUDED;
            case "unbounded" -> State.UNBOUNDED;
            case "unknown" -> State.UNKNOWN;
            default -> throw Fail.invalid("utv.interval.endpoint", path + "/state", "unknown endpoint state");
        };
        if (s == State.UNBOUNDED || s == State.UNKNOWN) return new End(s, null);
        if (!e.has("point")) throw Fail.invalid("utv.interval.endpoint", path + "/point", "an included or excluded endpoint needs a point");
        return new End(s, point(e.get("point"), dates, "endpoint"));
    }

    /** Civil dates must be all unzoned or all in the same zone; instants carry no zone. */
    private static void sameZone(JsonNode... values) {
        java.util.Set<Civil.Zone> zones = new java.util.HashSet<>();
        for (JsonNode v : values) collectZones(v, zones);
        if (zones.size() > 1) {
            throw Fail.unsupported("utv.interval.zone", "/value/zone", "dates must be all unzoned or all in the same zone");
        }
    }

    private static void collectZones(JsonNode v, java.util.Set<Civil.Zone> zones) {
        if (Civil.isCivil(v)) {
            zones.add(Civil.decode(v, "point").zone());
        } else if ("interval".equals(v.path("kind").asText(null))) {
            for (String side : new String[] {"start", "end"}) {
                JsonNode p = v.get("value").path(side).get("point");
                if (p != null) collectZones(p, zones);
            }
        }
    }

    private static Rational point(JsonNode p, boolean dates, String what) {
        Civil.structural(p, what);
        if (dates) {
            if (!Civil.isCivil(p)) throw Fail.unsupported("utv.interval.domain", "/kind", "a civil/gregory interval holds dates");
            Civil c = Civil.decode(p, what);
            if (!c.form().equals("date")) {
                throw Fail.unsupported("utv.interval.point", "/value/form", "civil intervals are handled for dates only");
            }
            BigInteger d = c.epochDay();
            return Rational.of(d);
        }
        if (!Instants.isInstant(p)) throw Fail.unsupported("utv.interval.domain", "/kind", "a physical interval holds instants");
        return Instants.decode(p, what);
    }
}
