using System;
using System.Text.Json;

namespace Utv;

/// <summary>What <see cref="Calendar.Add(System.Text.Json.JsonElement, System.Text.Json.JsonElement, MonthEndPolicy)"/> does when the day does not exist in the resulting month.</summary>
public enum MonthEndPolicy
{
    /// <summary><c>reject</c> (default): the result is <c>none</c> with diagnostic <c>month-end</c>.</summary>
    Reject,
    /// <summary><c>constrain</c>: clamp to the last day of the month (31 January + 1 month = 28 February).</summary>
    Constrain,
}

/// <summary>Resolution of a local time that falls in a gap (spring-forward). Values of the UTV schema.</summary>
public enum GapPolicy
{
    /// <summary><c>reject</c>: <c>none</c>, with diagnostic <c>gap</c>.</summary>
    Reject,
    /// <summary><c>preserve-unresolved</c>: <c>none</c>, with diagnostic <c>gap</c>; the civil value is kept as written.</summary>
    PreserveUnresolved,
    /// <summary><c>shift-forward-by-gap</c>: the instant the wall clock shows after moving forward by the gap (02:30 → 03:30).</summary>
    ShiftForwardByGap,
    /// <summary><c>shift-backward-by-gap</c>: not implemented (<c>unsupported</c>).</summary>
    ShiftBackwardByGap,
    /// <summary><c>next-valid-boundary</c>: not implemented (<c>unsupported</c>).</summary>
    NextValidBoundary,
    /// <summary><c>previous-valid-boundary</c>: not implemented (<c>unsupported</c>).</summary>
    PreviousValidBoundary,
}

/// <summary>Resolution of a local time that occurs twice (fall-back). Values of the UTV schema.</summary>
public enum OverlapPolicy
{
    /// <summary><c>reject</c>: <c>multiple</c> with both instants and diagnostic <c>ambiguous</c>.</summary>
    Reject,
    /// <summary><c>all</c>: <c>multiple</c> with both instants.</summary>
    All,
    /// <summary><c>earliest</c>: the earlier instant.</summary>
    Earliest,
    /// <summary><c>latest</c>: the later instant.</summary>
    Latest,
    /// <summary><c>offset-match</c>: not implemented (<c>unsupported</c>); use <c>offsetAssertion</c> on the value.</summary>
    OffsetMatch,
    /// <summary><c>occurrence-index</c>: not implemented (<c>unsupported</c>).</summary>
    OccurrenceIndex,
}

/// <summary>A <c>resolutionPolicy</c>: what <see cref="Zones.Resolve"/> does in a gap and in an overlap.</summary>
/// <param name="Gap">Gap policy; default <see cref="GapPolicy.Reject"/>.</param>
/// <param name="Overlap">Overlap policy; default <see cref="OverlapPolicy.Reject"/>.</param>
public sealed record ResolutionPolicy(GapPolicy Gap = GapPolicy.Reject, OverlapPolicy Overlap = OverlapPolicy.Reject)
{
    /// <summary><c>{"gap":"reject","overlap":"reject"}</c>.</summary>
    public static ResolutionPolicy Default { get; } = new();

    /// <summary>
    /// Reads a JSON <c>{"gap": …, "overlap": …}</c> object. A missing member defaults to <c>reject</c>.
    /// </summary>
    /// <returns>False if it is not an object, has other members, or names a value outside the UTV schema.</returns>
    public static bool TryParse(JsonElement json, out ResolutionPolicy policy)
    {
        policy = Default;
        if (json.ValueKind != JsonValueKind.Object)
        {
            return false;
        }
        GapPolicy gap = GapPolicy.Reject;
        OverlapPolicy overlap = OverlapPolicy.Reject;
        foreach (JsonProperty p in json.EnumerateObject())
        {
            string? text = p.Value.ValueKind == JsonValueKind.String ? p.Value.GetString() : null;
            switch (p.Name)
            {
                case "gap" when PolicyWire.TryParseGap(text, out gap):
                case "overlap" when PolicyWire.TryParseOverlap(text, out overlap):
                    break;
                default:
                    return false;
            }
        }
        policy = new ResolutionPolicy(gap, overlap);
        return true;
    }
}

/// <summary>Wire strings of the policy enums.</summary>
public static class PolicyWire
{
    /// <summary><c>reject</c> or <c>constrain</c>.</summary>
    public static string ToWireString(this MonthEndPolicy policy) => policy switch
    {
        MonthEndPolicy.Reject => "reject",
        MonthEndPolicy.Constrain => "constrain",
        _ => throw new ArgumentOutOfRangeException(nameof(policy), policy, "not a month-end policy"),
    };

    /// <summary>The schema string, for example <c>shift-forward-by-gap</c>.</summary>
    public static string ToWireString(this GapPolicy policy) => policy switch
    {
        GapPolicy.Reject => "reject",
        GapPolicy.PreserveUnresolved => "preserve-unresolved",
        GapPolicy.ShiftForwardByGap => "shift-forward-by-gap",
        GapPolicy.ShiftBackwardByGap => "shift-backward-by-gap",
        GapPolicy.NextValidBoundary => "next-valid-boundary",
        GapPolicy.PreviousValidBoundary => "previous-valid-boundary",
        _ => throw new ArgumentOutOfRangeException(nameof(policy), policy, "not a gap policy"),
    };

    /// <summary>The schema string, for example <c>earliest</c>.</summary>
    public static string ToWireString(this OverlapPolicy policy) => policy switch
    {
        OverlapPolicy.Reject => "reject",
        OverlapPolicy.All => "all",
        OverlapPolicy.Earliest => "earliest",
        OverlapPolicy.Latest => "latest",
        OverlapPolicy.OffsetMatch => "offset-match",
        OverlapPolicy.OccurrenceIndex => "occurrence-index",
        _ => throw new ArgumentOutOfRangeException(nameof(policy), policy, "not an overlap policy"),
    };

    /// <summary>Parses <c>reject</c> or <c>constrain</c> (exact, case-sensitive).</summary>
    public static bool TryParseMonthEnd(string? text, out MonthEndPolicy policy) => TryParse(text, ToWireString, out policy);

    /// <summary>Parses a gap policy string (exact, case-sensitive).</summary>
    public static bool TryParseGap(string? text, out GapPolicy policy) => TryParse(text, ToWireString, out policy);

    /// <summary>Parses an overlap policy string (exact, case-sensitive).</summary>
    public static bool TryParseOverlap(string? text, out OverlapPolicy policy) => TryParse(text, ToWireString, out policy);

    private static bool TryParse<T>(string? text, Func<T, string> wire, out T value)
        where T : struct, Enum
    {
        foreach (T candidate in Enum.GetValues<T>())
        {
            if (string.Equals(wire(candidate), text, StringComparison.Ordinal))
            {
                value = candidate;
                return true;
            }
        }
        value = default;
        return false;
    }
}
