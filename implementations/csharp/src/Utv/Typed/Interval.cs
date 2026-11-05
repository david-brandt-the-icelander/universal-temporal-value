using System;
using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;
using Utv.Internal.Everyday;

namespace Utv;

/// <summary>
/// A range of dates or instants with explicit ends: <c>[2026-11-01, 2026-12-01)</c>, <c>[2026-11-01, ..)</c> (no end),
/// <c>[2026-11-01, ?)</c> (an end nobody knows yet). Questions about it answer with a three-valued <see cref="Truth"/>.
/// </summary>
/// <typeparam name="T"><see cref="CivilDate"/> or <see cref="Instant"/>.</typeparam>
[DebuggerDisplay("Interval({ToString(),nq})")]
public sealed record Interval<T> : ITemporalValue
    where T : class, IIntervalPoint
{
    internal Interval(Bound<T> start, Bound<T> end)
    {
        Start = start;
        End = end;
    }

    /// <summary>The start.</summary>
    public Bound<T> Start { get; }

    /// <summary>The end.</summary>
    public Bound<T> End { get; }

    /// <summary>Parses text such as <c>[2026-11-01, 2026-12-01)</c> or EDTF <c>2026-11-01/2026-11-30</c>.</summary>
    public static Outcome<Interval<T>> Parse(string text) => Typed.Parse<Interval<T>>(text, "an interval of " + typeof(T).Name);

    /// <summary>Reads a UTV JSON <c>interval</c> (<c>start-end</c>).</summary>
    public static Outcome<Interval<T>> FromJson(JsonElement json) => Typed.FromJson<Interval<T>>(json, "an interval of " + typeof(T).Name);

    /// <summary>Reads UTV JSON text.</summary>
    public static Outcome<Interval<T>> FromJson(string json) => Typed.FromJson<Interval<T>>(json, "an interval of " + typeof(T).Name);

    /// <summary>
    /// Is <paramref name="point"/> inside? <see cref="Truth.Possibly"/> when an unknown end decides it.
    /// </summary>
    public Truth Contains(T point)
    {
        ArgumentNullException.ThrowIfNull(point);
        return TypedWire.ParseTruth(Typed.Total(Ranges.Contains(ToJson(), point.ToJson()), e => e.GetString()!));
    }

    /// <summary>
    /// Do the two share a point? Dates are discrete, so <c>[1 Nov, 30 Nov]</c> and <c>[1 Dec, 1 Jan)</c> don't overlap.
    /// </summary>
    public Truth Overlaps(Interval<T> other)
    {
        ArgumentNullException.ThrowIfNull(other);
        return TypedWire.ParseTruth(Typed.Total(Ranges.Overlaps(ToJson(), other.ToJson()), e => e.GetString()!));
    }

    /// <inheritdoc/>
    public JsonElement ToJson() => Values.ToElement(new JsonObject
    {
        ["schema"] = UtvValidator.SpecVersion,
        ["kind"] = "interval",
        ["value"] = new JsonObject { ["domain"] = Domain, ["start"] = Start.Node(), ["end"] = End.Node() },
    });

    /// <summary>The text form, for example <c>[2026-11-01, 2026-12-01)</c>.</summary>
    public override string ToString() => Typed.Text(this);

    private static string Domain => typeof(T) == typeof(Instant) ? "physical" : "civil/gregory";

    internal static bool IsReversed(Bound<T> start, Bound<T> end) =>
        start.Point is T a && end.Point is T b
        && Calendar.Compare(a.ToJson(), b.ToJson()) is { IsUnique: true } order && order.GetString() == "after";
}

/// <summary>One end of an <see cref="Interval{T}"/>: a point that is included or excluded, or no point.</summary>
/// <typeparam name="T"><see cref="CivilDate"/> or <see cref="Instant"/>.</typeparam>
[DebuggerDisplay("{State} {Point}")]
public sealed record Bound<T>
    where T : class, IIntervalPoint
{
    internal Bound(BoundState state, T? point)
    {
        State = state;
        Point = point;
    }

    /// <summary>Included, excluded, unbounded or unknown.</summary>
    public BoundState State { get; }

    /// <summary>The point, for an included or excluded end; otherwise null.</summary>
    public T? Point { get; }

    /// <summary><see cref="Bound.Unbounded"/> or <see cref="Bound.Unknown"/>, as an end of this type.</summary>
    public static implicit operator Bound<T>(OpenBound bound) => new(bound.State, null);

    /// <summary>The state and point, for example <c>Included 2026-11-01</c>.</summary>
    public override string ToString() => Point is null ? State.ToString() : State + " " + Point;

    internal JsonObject Node()
    {
        var node = new JsonObject
        {
            ["state"] = State switch
            {
                BoundState.Included => "included",
                BoundState.Excluded => "excluded",
                BoundState.Unbounded => "unbounded",
                _ => "unknown",
            },
        };
        if (Point is not null)
        {
            node["point"] = JsonNode.Parse(Point.ToJson().GetRawText());
        }
        return node;
    }
}

/// <summary>An end with no point, <see cref="Bound.Unbounded"/> or <see cref="Bound.Unknown"/>; it converts to any <see cref="Bound{T}"/>.</summary>
public readonly record struct OpenBound
{
    internal OpenBound(BoundState state)
    {
        State = state;
    }

    /// <summary><see cref="BoundState.Unbounded"/> or <see cref="BoundState.Unknown"/>.</summary>
    public BoundState State { get; }
}

/// <summary>Ends for <see cref="Interval.Of"/>.</summary>
public static class Bound
{
    /// <summary>No end on this side: <c>..</c>.</summary>
    public static OpenBound Unbounded => new(BoundState.Unbounded);

    /// <summary>An end exists but nobody knows it yet: <c>?</c>. Questions that depend on it answer <see cref="Truth.Possibly"/>.</summary>
    public static OpenBound Unknown => new(BoundState.Unknown);

    /// <summary>The point is in the interval: <c>[</c> or <c>]</c>.</summary>
    public static Bound<T> Included<T>(T point)
        where T : class, IIntervalPoint
    {
        ArgumentNullException.ThrowIfNull(point);
        return new Bound<T>(BoundState.Included, point);
    }

    /// <summary>The point is just outside the interval: <c>(</c> or <c>)</c>.</summary>
    public static Bound<T> Excluded<T>(T point)
        where T : class, IIntervalPoint
    {
        ArgumentNullException.ThrowIfNull(point);
        return new Bound<T>(BoundState.Excluded, point);
    }
}

/// <summary>Builders for <see cref="Interval{T}"/>.</summary>
public static class Interval
{
    /// <summary><c>[start, end)</c>: the usual range, start included and end excluded.</summary>
    /// <exception cref="ArgumentException">The end is before the start.</exception>
    public static Interval<T> HalfOpen<T>(T start, T end)
        where T : class, IIntervalPoint => Of(Bound.Included(start), Bound.Excluded(end));

    /// <summary><c>[start, end]</c>: both ends included.</summary>
    /// <exception cref="ArgumentException">The end is before the start.</exception>
    public static Interval<T> Closed<T>(T start, T end)
        where T : class, IIntervalPoint => Of(Bound.Included(start), Bound.Included(end));

    /// <summary>
    /// An interval from two ends, each <see cref="Bound.Included"/>, <see cref="Bound.Excluded"/>,
    /// <see cref="Bound.Unbounded"/> or <see cref="Bound.Unknown"/>.
    /// </summary>
    /// <exception cref="ArgumentException">The end is before the start (<c>reversed-interval</c>), or neither end has a point.</exception>
    public static Interval<T> Of<T>(Bound<T> start, Bound<T> end)
        where T : class, IIntervalPoint
    {
        ArgumentNullException.ThrowIfNull(start);
        ArgumentNullException.ThrowIfNull(end);
        if (start.Point is null && end.Point is null)
        {
            throw new ArgumentException("an interval needs at least one point: with none it has no domain (unsupported-form)", nameof(end));
        }
        if (Interval<T>.IsReversed(start, end))
        {
            throw new ArgumentException("the interval ends before it starts (reversed-interval)", nameof(end));
        }
        return new Interval<T>(start, end);
    }
}
