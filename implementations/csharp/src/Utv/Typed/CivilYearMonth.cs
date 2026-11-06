using System;
using System.Diagnostics;
using System.Numerics;
using System.Text.Json;
using System.Text.Json.Nodes;
using Utv.Internal.Everyday;

namespace Utv;

/// <summary>A month of a proleptic Gregorian year, with no zone: <c>2026-11</c>. Month arithmetic always works.</summary>
[DebuggerDisplay("CivilYearMonth({ToString(),nq})")]
public sealed record CivilYearMonth : ITemporalValue, IComparable<CivilYearMonth>
{
    internal CivilYearMonth(BigInteger year, int month)
    {
        Year = year;
        Month = month;
    }

    /// <summary>The year, astronomical numbering.</summary>
    public BigInteger Year { get; }

    /// <summary>The month, 1 to 12.</summary>
    public int Month { get; }

    /// <summary>A month such as <c>CivilYearMonth.Of(2026, 11)</c>.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The month is not 1 to 12.</exception>
    public static CivilYearMonth Of(BigInteger year, int month) => Typed.OrThrow(TryOf(year, month));

    /// <summary>A month, or <c>invalid</c> (<c>month-out-of-range</c>).</summary>
    public static Outcome<CivilYearMonth> TryOf(BigInteger year, int month) =>
        Typed.FromJson<CivilYearMonth>(Values.ToElement(Values.Civil("year-month", new JsonObject { ["year"] = Values.FormatYear(year), ["month"] = month })), "a year-month");

    /// <summary>Parses text such as <c>2026-11</c>.</summary>
    public static Outcome<CivilYearMonth> Parse(string text) => Typed.Parse<CivilYearMonth>(text, "a year-month");

    /// <summary>Reads a UTV JSON <c>year-month</c>.</summary>
    public static Outcome<CivilYearMonth> FromJson(JsonElement json) => Typed.FromJson<CivilYearMonth>(json, "a year-month");

    /// <summary>Reads UTV JSON text.</summary>
    public static Outcome<CivilYearMonth> FromJson(string json) => Typed.FromJson<CivilYearMonth>(json, "a year-month");

    /// <summary><paramref name="months"/> months later (earlier if negative).</summary>
    public CivilYearMonth AddMonths(long months) => Add(Build.Period(months: months));

    /// <summary><paramref name="years"/> years later (earlier if negative).</summary>
    public CivilYearMonth AddYears(long years) => Add(Build.Period(years: years));

    /// <inheritdoc/>
    public JsonElement ToJson() =>
        Values.ToElement(Values.Civil("year-month", new JsonObject { ["year"] = Values.FormatYear(Year), ["month"] = Month }));

    /// <summary>The text form, for example <c>2026-11</c>.</summary>
    public override string ToString() => Typed.Text(this);

    /// <summary>Calendar order.</summary>
    public int CompareTo(CivilYearMonth? other) => other is null ? 1 : (int)TypedWire.ParseOrdering(Calendar.Compare(ToJson(), other.ToJson()).GetString());

    /// <summary>Earlier than.</summary>
    public static bool operator <(CivilYearMonth left, CivilYearMonth right) => left.CompareTo(right) < 0;

    /// <summary>Later than.</summary>
    public static bool operator >(CivilYearMonth left, CivilYearMonth right) => left.CompareTo(right) > 0;

    /// <summary>Earlier than or the same month.</summary>
    public static bool operator <=(CivilYearMonth left, CivilYearMonth right) => left.CompareTo(right) <= 0;

    /// <summary>Later than or the same month.</summary>
    public static bool operator >=(CivilYearMonth left, CivilYearMonth right) => left.CompareTo(right) >= 0;

    private CivilYearMonth Add(JsonElement period) => Typed.Total(Calendar.Add(ToJson(), period), Typed.Trusted<CivilYearMonth>);
}
