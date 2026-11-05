using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Numerics;
using System.Text.Json;
using static System.FormattableString;

namespace Utv;

/// <summary>
/// An immutable bundle of zone rules in the format of <c>conformance/rules/demo-zones.json</c>: for each zone,
/// an initial offset and a list of transitions, valid only from <see cref="ValidFrom"/> (inclusive) to
/// <see cref="ValidUntil"/> (exclusive). Nothing outside that window is extrapolated.
/// </summary>
/// <remarks>The library never reads the host's time zone database; the caller passes the rules in.</remarks>
public sealed class ZoneRules
{
    private readonly Dictionary<string, ZoneTimeline> _zones;

    private ZoneRules(string system, string id, string version, BigInteger validFrom, BigInteger validUntil, Dictionary<string, ZoneTimeline> zones)
    {
        RuleSetSystem = system;
        RuleSetId = id;
        RuleSetVersion = version;
        ValidFrom = validFrom;
        ValidUntil = validUntil;
        _zones = zones;
        ZoneIds = new ReadOnlyCollection<string>(zones.Keys.OrderBy(k => k, StringComparer.Ordinal).ToArray());
    }

    /// <summary><c>ruleSet.system</c>, for example <c>utv.demo</c>.</summary>
    public string RuleSetSystem { get; }

    /// <summary><c>ruleSet.id</c>, for example <c>demo-zones</c>.</summary>
    public string RuleSetId { get; }

    /// <summary><c>ruleSet.version</c>.</summary>
    public string RuleSetVersion { get; }

    /// <summary>Start of validity, in POSIX seconds (inclusive).</summary>
    public BigInteger ValidFrom { get; }

    /// <summary>End of validity, in POSIX seconds (exclusive).</summary>
    public BigInteger ValidUntil { get; }

    /// <summary>The zone identifiers in the bundle, in ordinal order.</summary>
    public IReadOnlyList<string> ZoneIds { get; }

    /// <summary>Parses a rule bundle from JSON text.</summary>
    /// <exception cref="FormatException">The text is not a well-formed rule bundle.</exception>
    public static ZoneRules Parse(string json)
    {
        ArgumentNullException.ThrowIfNull(json);
        try
        {
            using JsonDocument doc = JsonDocument.Parse(json);
            return Parse(doc.RootElement);
        }
        catch (JsonException e)
        {
            throw new FormatException("zone rules are not well-formed JSON: " + e.Message, e);
        }
    }

    /// <summary>Parses a rule bundle from a JSON element.</summary>
    /// <exception cref="FormatException">The element is not a well-formed rule bundle.</exception>
    public static ZoneRules Parse(JsonElement json)
    {
        try
        {
            JsonElement ruleSet = json.GetProperty("ruleSet");
            BigInteger validFrom = Integer(json.GetProperty("validFrom"));
            BigInteger validUntil = Integer(json.GetProperty("validUntil"));
            if (validFrom >= validUntil)
            {
                throw new FormatException("validFrom must be before validUntil");
            }
            var zones = new Dictionary<string, ZoneTimeline>(StringComparer.Ordinal);
            foreach (JsonProperty zone in json.GetProperty("zones").EnumerateObject())
            {
                int initial = zone.Value.GetProperty("initialOffset").GetInt32();
                var transitions = new List<(BigInteger At, int Offset)>();
                foreach (JsonElement t in zone.Value.GetProperty("transitions").EnumerateArray())
                {
                    BigInteger at = Integer(t.GetProperty("at"));
                    if (transitions.Count > 0 && at <= transitions[^1].At)
                    {
                        throw new FormatException(Invariant($"transitions of {zone.Name} are not in ascending order"));
                    }
                    transitions.Add((at, t.GetProperty("offset").GetInt32()));
                }
                if (!zones.TryAdd(zone.Name, new ZoneTimeline(initial, transitions.ToArray())))
                {
                    throw new FormatException(Invariant($"zone {zone.Name} is listed twice"));
                }
            }
            return new ZoneRules(
                ruleSet.GetProperty("system").GetString()!,
                ruleSet.GetProperty("id").GetString()!,
                ruleSet.GetProperty("version").GetString()!,
                validFrom,
                validUntil,
                zones);
        }
        catch (Exception e) when (e is KeyNotFoundException or InvalidOperationException or ArgumentException or OverflowException)
        {
            throw new FormatException("not a well-formed zone rule bundle: " + e.Message, e);
        }
    }

    /// <summary>True if the bundle has rules for <paramref name="zoneId"/> (exact, case-sensitive).</summary>
    public bool Contains(string zoneId) => _zones.ContainsKey(zoneId);

    internal bool TryGetZone(string zoneId, out ZoneTimeline timeline) => _zones.TryGetValue(zoneId, out timeline!);

    private static BigInteger Integer(JsonElement e) => e.ValueKind switch
    {
        JsonValueKind.String => BigInteger.Parse(e.GetString()!, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture),
        JsonValueKind.Number => BigInteger.Parse(e.GetRawText(), NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture),
        _ => throw new FormatException("expected an integer"),
    };
}

/// <summary>The offsets of one zone over time.</summary>
internal sealed class ZoneTimeline
{
    public ZoneTimeline(int initialOffset, (BigInteger At, int Offset)[] transitions)
    {
        InitialOffset = initialOffset;
        Transitions = transitions;
    }

    public int InitialOffset { get; }

    public IReadOnlyList<(BigInteger At, int Offset)> Transitions { get; }

    /// <summary>The offset in force at POSIX second <paramref name="t"/> (a transition applies from its own second).</summary>
    public int OffsetAt(BigInteger t)
    {
        int offset = InitialOffset;
        foreach ((BigInteger at, int o) in Transitions)
        {
            if (at > t)
            {
                break;
            }
            offset = o;
        }
        return offset;
    }

    /// <summary>The offset in force just before POSIX second <paramref name="t"/>.</summary>
    public int OffsetBefore(BigInteger t) => OffsetAt(t - 1);
}
