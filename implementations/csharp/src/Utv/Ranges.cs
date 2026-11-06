using System.Numerics;
using System.Text.Json;
using Utv.Internal.Everyday;
using static System.FormattableString;

namespace Utv;

/// <summary>
/// Interval questions with three-valued answers: <c>"definitely"</c>, <c>"possibly"</c> or <c>"impossible"</c>.
/// Works on civil-date intervals with date points and on instant intervals with instant points.
/// </summary>
public static class Ranges
{
    private enum Tri
    {
        False,
        Unknown,
        True,
    }

    private readonly record struct Bound(string State, Rational Point)
    {
        public bool HasPoint => State is "included" or "excluded";
    }

    private sealed record IntervalRead(string Domain, Bound Start, Bound End, bool Empty);

    /// <summary>Does <paramref name="interval"/> contain <paramref name="point"/>?</summary>
    /// <returns>
    /// <c>unique</c> with <c>"definitely"</c>, <c>"possibly"</c> (an unknown bound decides it) or <c>"impossible"</c>;
    /// <c>unsupported</c> when the point's domain differs from the interval's.
    /// </returns>
    public static UtvResult Contains(JsonElement interval, JsonElement point) => Op.Run("contains", () =>
    {
        var zones = new ZoneAgreement();
        IntervalRead i = ReadInterval(interval, "interval", zones);
        Rational p = ReadPoint(point, i.Domain, "point", zones);
        if (i.Empty)
        {
            return Answer("contains", Tri.False);
        }
        Tri start = i.Start.State switch
        {
            "included" => Of(i.Start.Point <= p),
            "excluded" => Of(i.Start.Point < p),
            "unbounded" => Tri.True,
            _ => Tri.Unknown,
        };
        Tri end = i.End.State switch
        {
            "included" => Of(p <= i.End.Point),
            "excluded" => Of(p < i.End.Point),
            "unbounded" => Tri.True,
            _ => Tri.Unknown,
        };
        return Answer("contains", And(start, end));
    });

    /// <summary>Do intervals <paramref name="a"/> and <paramref name="b"/> share at least one point?</summary>
    /// <returns>
    /// <c>unique</c> with <c>"definitely"</c>, <c>"possibly"</c> or <c>"impossible"</c>; <c>unsupported</c> when the
    /// domains differ. Date intervals are first normalized to half-open (dates are discrete), so
    /// <c>[1 Nov, 30 Nov]</c> and <c>[1 Dec, 1 Jan)</c> do not overlap. An interval that is provably empty overlaps nothing.
    /// </returns>
    public static UtvResult Overlaps(JsonElement a, JsonElement b) => Op.Run("overlaps", () =>
    {
        var zones = new ZoneAgreement();
        IntervalRead x = ReadInterval(a, "a", zones);
        IntervalRead y = ReadInterval(b, "b", zones);
        if (x.Domain != y.Domain)
        {
            throw OpFailure.Unsupported("unsupported-operand", "/value/domain", Invariant($"cannot relate a `{x.Domain}` interval to a `{y.Domain}` interval"));
        }
        if (x.Domain == "civil/gregory")
        {
            x = HalfOpen(x);
            y = HalfOpen(y);
        }
        if (IsProvablyEmpty(x) || IsProvablyEmpty(y))
        {
            return Answer("overlaps", Tri.False);
        }
        return Answer("overlaps", And(Before(x.Start, y.End), Before(y.Start, x.End)));
    });

    private static Tri Before(Bound start, Bound end)
    {
        if (start.State == "unbounded" || end.State == "unbounded")
        {
            return Tri.True;
        }
        if (!start.HasPoint || !end.HasPoint)
        {
            return Tri.Unknown;
        }
        return start.State == "included" && end.State == "included"
            ? Of(start.Point <= end.Point)
            : Of(start.Point < end.Point);
    }

    private static bool IsProvablyEmpty(IntervalRead i)
    {
        if (i.Empty)
        {
            return true;
        }
        if (!i.Start.HasPoint || !i.End.HasPoint)
        {
            return false;
        }
        return i.Start.State == "included" && i.End.State == "included"
            ? i.Start.Point > i.End.Point
            : i.Start.Point >= i.End.Point;
    }

    /// <summary>Dates are discrete: an included end d becomes an excluded d + 1, an excluded start d an included d + 1.</summary>
    private static IntervalRead HalfOpen(IntervalRead i)
    {
        Rational one = Rational.FromInteger(BigInteger.One);
        Bound start = i.Start.State == "excluded" ? new Bound("included", i.Start.Point + one) : i.Start;
        Bound end = i.End.State == "included" ? new Bound("excluded", i.End.Point + one) : i.End;
        return i with { Start = start, End = end };
    }

    private static IntervalRead ReadInterval(JsonElement interval, string argument, ZoneAgreement zones)
    {
        Values.RequireStructural(interval, argument);
        string kind = Values.KindOf(interval);
        if (kind != "interval")
        {
            throw OpFailure.Unsupported("unsupported-operand", "/kind", Invariant($"{argument}: expected an `interval`, not `{kind}`"));
        }
        JsonElement v = interval.GetProperty("value");
        string domain = v.GetProperty("domain").GetString()!;
        if (domain is not ("civil/gregory" or "physical"))
        {
            throw OpFailure.Unsupported("unsupported-operand", "/value/domain", Invariant($"{argument}: domain `{domain}` is not implemented"));
        }
        if (v.TryGetProperty("construction", out JsonElement construction) && construction.GetString() != "start-end")
        {
            throw OpFailure.Unsupported("unsupported-operand", "/value/construction",
                Invariant($"{argument}: only `start-end` intervals are implemented"));
        }
        bool empty = v.TryGetProperty("empty", out JsonElement e) && e.GetBoolean();
        return new IntervalRead(domain, ReadBound(v.GetProperty("start"), domain, argument, zones), ReadBound(v.GetProperty("end"), domain, argument, zones), empty);
    }

    private static Bound ReadBound(JsonElement bound, string domain, string argument, ZoneAgreement zones)
    {
        string state = bound.GetProperty("state").GetString()!;
        return bound.TryGetProperty("point", out JsonElement point)
            ? new Bound(state, ReadPoint(point, domain, argument, zones))
            : new Bound(state, default);
    }

    /// <summary>A point as a number on the domain's line: a day number for dates, POSIX seconds for instants.</summary>
    private static Rational ReadPoint(JsonElement point, string domain, string argument, ZoneAgreement zones)
    {
        Values.RequireStructural(point, argument);
        string kind = Values.KindOf(point);
        if (domain == "physical")
        {
            if (kind != "instant")
            {
                throw OpFailure.Unsupported("unsupported-operand", "/kind", Invariant($"{argument}: a `physical` interval holds instants, not `{kind}` values"));
            }
            return Values.ReadPosixInstant(point, argument);
        }
        if (kind != "civil")
        {
            throw OpFailure.Unsupported("unsupported-operand", "/kind", Invariant($"{argument}: a `civil/gregory` interval holds dates, not `{kind}` values"));
        }
        return Rational.FromInteger(Calendar.DayNumber(point, argument, zones));
    }

    private static Tri Of(bool b) => b ? Tri.True : Tri.False;

    private static Tri And(Tri a, Tri b) => a < b ? a : b;

    private static UtvResult Answer(string operation, Tri t) =>
        Op.Unique(operation, t switch
        {
            Tri.True => "definitely",
            Tri.False => "impossible",
            _ => "possibly",
        });
}
