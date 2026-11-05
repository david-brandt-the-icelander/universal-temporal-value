using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text.Json;
using System.Text.Json.Nodes;
using Utv.Internal.Everyday;
using static System.FormattableString;

namespace Utv;

/// <summary>
/// Turning local date-times in a zone into instants and back, with rules the caller passes in
/// (<see cref="ZoneRules"/>). The host's time zone database is never consulted.
/// </summary>
public static class Zones
{
    private const int Day = CivilMath.SecondsPerDay;

    /// <summary>
    /// Resolves a local <c>datetime</c> to instant(s). The policy is <paramref name="policy"/>, else the value's
    /// own <c>resolutionPolicy</c>, else <see cref="ResolutionPolicy.Default"/> (reject both).
    /// </summary>
    /// <returns>
    /// <c>unique</c> with one instant; <c>none</c> in a gap (diagnostic <c>gap</c>) unless the policy is
    /// <see cref="GapPolicy.ShiftForwardByGap"/>; <c>multiple</c> with both instants, ascending, in an overlap
    /// (diagnostic <c>ambiguous</c> under <see cref="OverlapPolicy.Reject"/>) unless the policy is
    /// <see cref="OverlapPolicy.Earliest"/> or <see cref="OverlapPolicy.Latest"/>; <c>symbolic</c> for an unzoned
    /// date-time (no zone is bound yet); <c>unsupported</c> for other forms, second 60, zones not in the rules and
    /// times outside the rules' validity window (never extrapolated). A fixed-offset date-time needs no rules.
    /// If the value carries an <c>offsetAssertion</c>, it selects among the candidates, and a mismatch is <c>invalid</c>.
    /// </returns>
    public static UtvResult Resolve(JsonElement dateTime, ZoneRules rules, ResolutionPolicy? policy = null)
    {
        ArgumentNullException.ThrowIfNull(rules);
        return Op.Run("resolve", () => ResolveCore(dateTime, rules, policy));
    }

    /// <summary>
    /// The wall-clock <c>datetime</c> of an instant in a named zone, with <c>offsetAssertion</c> set to the offset in
    /// force. Always includes <c>second</c>; includes <c>nano</c> only when non-zero.
    /// </summary>
    /// <returns><c>unique</c>; <c>unsupported</c> if the zone is not in the rules or the instant is outside their validity window.</returns>
    public static UtvResult ToZone(JsonElement instant, string zoneId, ZoneRules rules)
    {
        ArgumentNullException.ThrowIfNull(zoneId);
        ArgumentNullException.ThrowIfNull(rules);
        return Op.Run("toZone", () =>
        {
            Rational t = Calendar.RequireInstant(instant, "instant");
            if (!rules.TryGetZone(zoneId, out ZoneTimeline zone))
            {
                throw UnknownZone(zoneId, rules);
            }
            if (t < Rational.FromInteger(rules.ValidFrom) || t >= Rational.FromInteger(rules.ValidUntil))
            {
                throw OutsideWindow(rules);
            }
            int offset = zone.OffsetAt(t.Floor());
            JsonObject node = Values.Civil("datetime", Values.WallFields(t, offset, "/value/subTick"));
            JsonObject value = node["value"]!.AsObject();
            value["zone"] = new JsonObject { ["type"] = "iana", ["id"] = zoneId };
            value["zoneIntent"] = "follow-zone-id";
            value["offsetAssertion"] = offset;
            return Op.Unique("toZone", node);
        });
    }

    /// <summary>The wall-clock <c>datetime</c> of an instant at a fixed offset (seconds east of UTC). No rules needed.</summary>
    /// <returns><c>unique</c>; <c>invalid</c> for an offset beyond ±18:00.</returns>
    public static UtvResult ToOffset(JsonElement instant, int offsetSeconds) => Op.Run("toOffset", () =>
    {
        Rational t = Calendar.RequireInstant(instant, "instant");
        if (Math.Abs(offsetSeconds) > Values.MaxOffsetSeconds)
        {
            throw OpFailure.Invalid("offset-out-of-range", "", Invariant($"offset {offsetSeconds} s is beyond ±18:00"));
        }
        JsonObject node = Values.Civil("datetime", Values.WallFields(t, offsetSeconds, "/value/subTick"));
        JsonObject value = node["value"]!.AsObject();
        value["zone"] = new JsonObject { ["type"] = "fixed-offset", ["seconds"] = offsetSeconds };
        value["zoneIntent"] = "fixed-offset";
        return Op.Unique("toOffset", node);
    });

    private static UtvResult ResolveCore(JsonElement dateTime, ZoneRules rules, ResolutionPolicy? policy)
    {
        CivilRead c = Calendar.RequireCivil(dateTime, "dateTime");

        // 1. Form and leap second.
        if (c.Form != "datetime")
        {
            throw OpFailure.Unsupported("unsupported-form", "/value/form", Invariant($"`resolve` takes a `datetime`, not a `{c.Form}`"));
        }
        if (c.Second == 60)
        {
            throw OpFailure.Unsupported("leap-second", "/value/fields/second", "second 60 needs a leap-second table, which this library does not ship");
        }
        Values.RequireGregorian(c);
        Rational wall = Calendar.WallSeconds(c);

        switch (c.Zone)
        {
            case ZoneKind.Floating: // 2.
                return new UtvResult("resolve", UtvStatus.Symbolic, new[]
                {
                    new Diagnostic("no-zone", DiagnosticSeverity.Info, "/value",
                        "an unzoned date-time names no instant until a zone is bound; use Build.InZone or Build.WithOffset"),
                });
            case ZoneKind.FixedOffset: // 3.
                return Op.Unique("resolve", Values.Instant(wall - Rational.FromInteger(c.ZoneSeconds)));
            case ZoneKind.Other:
                throw OpFailure.Unsupported("unsupported-zone", "/value/zone/type", Invariant($"zone type `{c.ZoneType}` is not implemented"));
        }

        // 4. Named zone in the bundle.
        if (!rules.TryGetZone(c.ZoneId!, out ZoneTimeline zone))
        {
            throw UnknownZone(c.ZoneId!, rules);
        }

        // 5. Never extrapolate.
        BigInteger lo = wall.Floor() - Day;
        if (wall - Rational.FromInteger(Day) < Rational.FromInteger(rules.ValidFrom)
            || wall + Rational.FromInteger(Day) >= Rational.FromInteger(rules.ValidUntil))
        {
            throw OutsideWindow(rules);
        }

        ResolutionPolicy chosen = policy ?? ValuePolicy(c) ?? ResolutionPolicy.Default;
        if (chosen.Gap is not (GapPolicy.Reject or GapPolicy.PreserveUnresolved or GapPolicy.ShiftForwardByGap))
        {
            throw OpFailure.Unsupported("unsupported-policy", "/value/resolutionPolicy/gap", Invariant($"gap policy `{chosen.Gap.ToWireString()}` is not implemented"));
        }
        if (chosen.Overlap is not (OverlapPolicy.Reject or OverlapPolicy.All or OverlapPolicy.Earliest or OverlapPolicy.Latest))
        {
            throw OpFailure.Unsupported("unsupported-policy", "/value/resolutionPolicy/overlap", Invariant($"overlap policy `{chosen.Overlap.ToWireString()}` is not implemented"));
        }

        // The offsets in force within a day of the wall time; each that maps back to itself gives a candidate.
        BigInteger hi = wall.Floor() + Day;
        var offsets = new SortedSet<int> { zone.OffsetAt(lo) };
        foreach ((BigInteger at, int o) in zone.Transitions)
        {
            if (at > lo && at <= hi)
            {
                offsets.Add(o);
            }
        }
        var candidates = offsets
            .Select(o => (Time: wall - Rational.FromInteger(o), Offset: o))
            .Where(x => zone.OffsetAt(x.Time.Floor()) == x.Offset)
            .OrderBy(x => x.Time)
            .ToList();

        if (c.OffsetAssertion is int asserted)
        {
            var matching = candidates.Where(x => x.Offset == asserted).ToList();
            if (matching.Count == 0)
            {
                throw OpFailure.Invalid("offset-mismatch", "/value/offsetAssertion",
                    Invariant($"the asserted offset {asserted} s is not in force at this wall time in {c.ZoneId}"));
            }
            candidates = matching;
        }

        switch (candidates.Count)
        {
            case 1:
                return Op.Unique("resolve", Values.Instant(candidates[0].Time));
            case 0:
                if (chosen.Gap == GapPolicy.ShiftForwardByGap)
                {
                    int before = OffsetBeforeGap(zone, wall, lo, hi);
                    return Op.Unique("resolve", Values.Instant(wall - Rational.FromInteger(before)));
                }
                return new UtvResult("resolve", UtvStatus.None, new[]
                {
                    new Diagnostic("gap", DiagnosticSeverity.Warning, "/value/fields",
                        Invariant($"this wall time is skipped in {c.ZoneId} (a gap); pass GapPolicy.ShiftForwardByGap to move forward by the gap")),
                });
            default:
                IEnumerable<JsonElement> all = candidates.Select(x => Values.ToElement(Values.Instant(x.Time)));
                return chosen.Overlap switch
                {
                    OverlapPolicy.Earliest => Op.Unique("resolve", Values.Instant(candidates[0].Time)),
                    OverlapPolicy.Latest => Op.Unique("resolve", Values.Instant(candidates[^1].Time)),
                    OverlapPolicy.All => new UtvResult("resolve", UtvStatus.Multiple, null, all),
                    _ => new UtvResult("resolve", UtvStatus.Multiple, new[]
                    {
                        new Diagnostic("ambiguous", DiagnosticSeverity.Warning, "/value/fields",
                            Invariant($"this wall time occurs {candidates.Count} times in {c.ZoneId} (an overlap); pass OverlapPolicy.Earliest or Latest to pick one")),
                    }, all),
                };
        }
    }

    /// <summary>The offset in force before the transition whose skipped wall-clock range contains <paramref name="wall"/>.</summary>
    private static int OffsetBeforeGap(ZoneTimeline zone, Rational wall, BigInteger lo, BigInteger hi)
    {
        foreach ((BigInteger at, int after) in zone.Transitions)
        {
            if (at <= lo || at > hi)
            {
                continue;
            }
            int before = zone.OffsetBefore(at);
            if (after > before
                && wall >= Rational.FromInteger(at + before)
                && wall < Rational.FromInteger(at + after))
            {
                return before;
            }
        }
        return zone.OffsetAt(lo);
    }

    private static ResolutionPolicy? ValuePolicy(CivilRead c)
    {
        if (c.ResolutionPolicy is not JsonElement json)
        {
            return null;
        }
        return ResolutionPolicy.TryParse(json, out ResolutionPolicy parsed)
            ? parsed
            : throw OpFailure.Invalid("bad-policy", "/value/resolutionPolicy", "`resolutionPolicy` is not a valid policy");
    }

    private static OpFailure UnknownZone(string zoneId, ZoneRules rules) =>
        OpFailure.Unsupported("unknown-zone", "/value/zone/id",
            Invariant($"zone `{zoneId}` is not in rule set `{rules.RuleSetId}` ({string.Join(", ", rules.ZoneIds)})"));

    private static OpFailure OutsideWindow(ZoneRules rules) =>
        OpFailure.Unsupported("outside-rules", "/value",
            Invariant($"rule set `{rules.RuleSetId}` is valid only from POSIX {rules.ValidFrom} to {rules.ValidUntil}; it is never extrapolated"));
}
