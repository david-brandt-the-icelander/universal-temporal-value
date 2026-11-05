using System;

namespace Utv;

/// <summary>What month arithmetic does when the day doesn't exist in the resulting month (31 January + 1 month).</summary>
public enum MonthEnd
{
    /// <summary>The default: no answer, <c>none</c> with diagnostic <c>month-end</c>.</summary>
    Reject,
    /// <summary>Clamp to the last day of the month: 31 January + 1 month = 28 February.</summary>
    Constrain,
}

/// <summary>What resolution does with a wall time that falls in a gap (the clocks skip it).</summary>
public enum Gap
{
    /// <summary>The default: no answer, <c>none</c> with diagnostic <c>gap</c>.</summary>
    Reject,
    /// <summary>No answer either (<c>none</c>, <c>gap</c>); the wall time is kept as written.</summary>
    PreserveUnresolved,
    /// <summary>Move forward by the length of the gap: 02:30 in a one-hour spring-forward gap becomes 03:30.</summary>
    ShiftForward,
}

/// <summary>What resolution does with a wall time that happens more than once (the clocks go back).</summary>
public enum Overlap
{
    /// <summary>The default: every instant, as <c>multiple</c>, with diagnostic <c>ambiguous</c>.</summary>
    Reject,
    /// <summary>Every instant, as <c>multiple</c>, without a diagnostic.</summary>
    All,
    /// <summary>The earlier instant.</summary>
    Earliest,
    /// <summary>The later instant.</summary>
    Latest,
}

/// <summary>A three-valued answer to a yes/no question such as "does this interval contain that day?".</summary>
public enum Truth
{
    /// <summary>Yes, whatever the unknowns turn out to be.</summary>
    Definitely,
    /// <summary>It depends on something unknown, such as an end date nobody knows yet.</summary>
    Possibly,
    /// <summary>No, whatever the unknowns turn out to be.</summary>
    Impossible,
}

/// <summary>How one value is ordered relative to another.</summary>
public enum Ordering
{
    /// <summary>The first comes before the second.</summary>
    Before = -1,
    /// <summary>They are at the same position.</summary>
    Equal = 0,
    /// <summary>The first comes after the second.</summary>
    After = 1,
}

/// <summary>An ISO 8601 day of the week, numbered Monday = 1 to Sunday = 7 (<see cref="DayOfWeek"/> has Sunday = 0).</summary>
public enum Weekday
{
    /// <summary>Day 1.</summary>
    Monday = 1,
    /// <summary>Day 2.</summary>
    Tuesday = 2,
    /// <summary>Day 3.</summary>
    Wednesday = 3,
    /// <summary>Day 4.</summary>
    Thursday = 4,
    /// <summary>Day 5.</summary>
    Friday = 5,
    /// <summary>Day 6.</summary>
    Saturday = 6,
    /// <summary>Day 7.</summary>
    Sunday = 7,
}

/// <summary>The state of one end of an interval.</summary>
public enum BoundState
{
    /// <summary>The point is in the interval.</summary>
    Included,
    /// <summary>The point is just outside the interval.</summary>
    Excluded,
    /// <summary>There is no end on this side.</summary>
    Unbounded,
    /// <summary>There is an end, but nobody knows it yet.</summary>
    Unknown,
}

/// <summary>Wire strings and conversions of the typed-layer enums.</summary>
public static class TypedWire
{
    /// <summary><c>before</c>, <c>equal</c> or <c>after</c>, as the JSON layer's <c>compare</c> answers.</summary>
    public static string ToWireString(this Ordering ordering) => ordering switch
    {
        Ordering.Before => "before",
        Ordering.Equal => "equal",
        Ordering.After => "after",
        _ => throw new ArgumentOutOfRangeException(nameof(ordering), ordering, "not an ordering"),
    };

    /// <summary><c>definitely</c>, <c>possibly</c> or <c>impossible</c>, as the JSON layer's <c>contains</c> answers.</summary>
    public static string ToWireString(this Truth truth) => truth switch
    {
        Truth.Definitely => "definitely",
        Truth.Possibly => "possibly",
        Truth.Impossible => "impossible",
        _ => throw new ArgumentOutOfRangeException(nameof(truth), truth, "not a truth value"),
    };

    internal static Ordering ParseOrdering(string wire) => wire switch
    {
        "before" => Ordering.Before,
        "equal" => Ordering.Equal,
        "after" => Ordering.After,
        _ => throw new InvalidOperationException("unexpected ordering " + wire),
    };

    internal static Truth ParseTruth(string wire) => wire switch
    {
        "definitely" => Truth.Definitely,
        "possibly" => Truth.Possibly,
        "impossible" => Truth.Impossible,
        _ => throw new InvalidOperationException("unexpected truth value " + wire),
    };

    internal static MonthEndPolicy ToPolicy(this MonthEnd monthEnd) => monthEnd switch
    {
        MonthEnd.Reject => MonthEndPolicy.Reject,
        MonthEnd.Constrain => MonthEndPolicy.Constrain,
        _ => throw new ArgumentOutOfRangeException(nameof(monthEnd), monthEnd, "not a month-end policy"),
    };

    internal static GapPolicy ToPolicy(this Gap gap) => gap switch
    {
        Gap.Reject => GapPolicy.Reject,
        Gap.PreserveUnresolved => GapPolicy.PreserveUnresolved,
        Gap.ShiftForward => GapPolicy.ShiftForwardByGap,
        _ => throw new ArgumentOutOfRangeException(nameof(gap), gap, "not a gap policy"),
    };

    internal static OverlapPolicy ToPolicy(this Overlap overlap) => overlap switch
    {
        Overlap.Reject => OverlapPolicy.Reject,
        Overlap.All => OverlapPolicy.All,
        Overlap.Earliest => OverlapPolicy.Earliest,
        Overlap.Latest => OverlapPolicy.Latest,
        _ => throw new ArgumentOutOfRangeException(nameof(overlap), overlap, "not an overlap policy"),
    };
}
