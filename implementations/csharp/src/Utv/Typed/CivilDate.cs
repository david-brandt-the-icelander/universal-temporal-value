using System;
using System.Diagnostics;
using System.Numerics;
using System.Text.Json;
using System.Text.Json.Nodes;
using Utv.Internal.Everyday;

namespace Utv;

/// <summary>
/// A day in the proleptic Gregorian calendar, with no zone: <c>2026-11-05</c>. Valid by construction; years are
/// unbounded (<see cref="BigInteger"/>).
/// </summary>
/// <remarks>
/// Adding days or weeks always works, so <see cref="AddDays"/> returns a date. Adding months can land on a day
/// that doesn't exist (31 January + 1 month), so <see cref="AddMonths(long)"/> returns an <see cref="Outcome{T}"/>
/// unless you pass <see cref="MonthEnd.Constrain"/>.
/// </remarks>
[DebuggerDisplay("CivilDate({ToString(),nq})")]
public sealed record CivilDate : IIntervalPoint, IComparable<CivilDate>
{
    internal CivilDate(BigInteger year, int month, int day)
    {
        Year = year;
        Month = month;
        Day = day;
    }

    /// <summary>The year, astronomical numbering (year 0 is 1 BC).</summary>
    public BigInteger Year { get; }

    /// <summary>The month, 1 to 12.</summary>
    public int Month { get; }

    /// <summary>The day of the month, 1 to <see cref="DaysInMonth"/>.</summary>
    public int Day { get; }

    string IIntervalPoint.Domain => "civil/gregory";

    /// <summary>A date such as <c>CivilDate.Of(2026, 11, 5)</c>.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The date doesn't exist, such as 30 February.</exception>
    public static CivilDate Of(BigInteger year, int month, int day) => Typed.OrThrow(TryOf(year, month, day));

    /// <summary>A date, or <c>invalid</c> (<c>month-out-of-range</c>, <c>day-out-of-range</c>) if it doesn't exist.</summary>
    public static Outcome<CivilDate> TryOf(BigInteger year, int month, int day) =>
        Typed.FromJson<CivilDate>(Values.ToElement(Values.Civil("date", Values.DateFields(year, month, day))), "a date");

    /// <summary>Parses text such as <c>2026-11-05</c>; anything else is an outcome, never an exception.</summary>
    public static Outcome<CivilDate> Parse(string text) => Typed.Parse<CivilDate>(text, "a date");

    /// <summary>Reads a UTV JSON <c>date</c>; other kinds or forms are <c>invalid</c> (<c>kind-mismatch</c>).</summary>
    public static Outcome<CivilDate> FromJson(JsonElement json) => Typed.FromJson<CivilDate>(json, "a date");

    /// <summary>Reads UTV JSON text.</summary>
    public static Outcome<CivilDate> FromJson(string json) => Typed.FromJson<CivilDate>(json, "a date");

    /// <summary>A .NET <see cref="DateOnly"/> as a date (always possible).</summary>
    public static CivilDate FromDateOnly(DateOnly date) => Typed.Trusted<CivilDate>(Interop.FromDateOnly(date));

    /// <summary>The ISO day of the week (Monday first).</summary>
    public Weekday DayOfWeek => (Weekday)Field("dayOfWeek").GetInt32();

    /// <summary>The day of the year, 1 to 366.</summary>
    public int DayOfYear => Field("dayOfYear").GetInt32();

    /// <summary>The ISO 8601 week number, 1 to 53.</summary>
    public int IsoWeek => Field("isoWeek").GetInt32();

    /// <summary>The year the ISO week belongs to (which can differ from <see cref="Year"/> near New Year).</summary>
    public BigInteger IsoWeekYear => BigInteger.Parse(Field("isoWeekYear").GetString()!, System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>The number of days in this date's month.</summary>
    public int DaysInMonth => Field("daysInMonth").GetInt32();

    /// <summary>True in a Gregorian leap year.</summary>
    public bool IsLeapYear => Field("isLeapYear").GetBoolean();

    /// <summary><paramref name="days"/> days later (earlier if negative).</summary>
    public CivilDate AddDays(long days) => Add(Build.Period(days: days));

    /// <summary><paramref name="weeks"/> weeks later (earlier if negative).</summary>
    public CivilDate AddWeeks(long weeks) => Add(Build.Period(weeks: weeks));

    /// <summary>
    /// <paramref name="months"/> months later: <c>none</c> (<c>month-end</c>) if the day doesn't exist in that month,
    /// as for 31 January + 1 month.
    /// </summary>
    public Outcome<CivilDate> AddMonths(long months) => Typed.ToOutcome<CivilDate>(Calendar.Add(ToJson(), Build.Period(months: months)));

    /// <summary>
    /// <paramref name="months"/> months later, with <paramref name="monthEnd"/> deciding a missing day:
    /// <see cref="MonthEnd.Constrain"/> clamps to the month's last day, so this always has an answer.
    /// </summary>
    /// <exception cref="ArgumentException"><paramref name="monthEnd"/> is <see cref="MonthEnd.Reject"/>: use the overload without a policy, which returns an <see cref="Outcome{T}"/>.</exception>
    public CivilDate AddMonths(long months, MonthEnd monthEnd)
    {
        if (monthEnd != MonthEnd.Constrain)
        {
            throw new ArgumentException("this overload always answers, so it takes only MonthEnd.Constrain; the overload without a policy returns an Outcome", nameof(monthEnd));
        }
        return Typed.ToOutcome<CivilDate>(Calendar.Add(ToJson(), Build.Period(months: months), monthEnd.ToPolicy())).Single();
    }

    /// <summary><paramref name="years"/> years later: <c>none</c> (<c>month-end</c>) from 29 February to a common year.</summary>
    public Outcome<CivilDate> AddYears(long years) => Typed.ToOutcome<CivilDate>(Calendar.Add(ToJson(), Build.Period(years: years)));

    /// <summary><paramref name="years"/> years later, with <paramref name="monthEnd"/> deciding a missing 29 February.</summary>
    /// <exception cref="ArgumentException"><paramref name="monthEnd"/> is <see cref="MonthEnd.Reject"/>: use the overload without a policy, which returns an <see cref="Outcome{T}"/>.</exception>
    public CivilDate AddYears(long years, MonthEnd monthEnd)
    {
        if (monthEnd != MonthEnd.Constrain)
        {
            throw new ArgumentException("this overload always answers, so it takes only MonthEnd.Constrain; the overload without a policy returns an Outcome", nameof(monthEnd));
        }
        return Typed.ToOutcome<CivilDate>(Calendar.Add(ToJson(), Build.Period(years: years), monthEnd.ToPolicy())).Single();
    }

    /// <summary>
    /// Adds a period: years and months, then the month-end rule, then weeks and days. <c>none</c> (<c>month-end</c>)
    /// if the day doesn't exist; <c>invalid</c> (<c>kind-mismatch</c>) if the period has hours, minutes or seconds.
    /// </summary>
    public Outcome<CivilDate> Add(Period period, MonthEnd monthEnd = MonthEnd.Reject)
    {
        ArgumentNullException.ThrowIfNull(period);
        return Typed.ToOutcome<CivilDate>(Calendar.Add(ToJson(), period.ToJson(), monthEnd.ToPolicy()));
    }

    /// <summary>The number of days from this date to <paramref name="other"/> (negative if it is earlier).</summary>
    public long DaysUntil(CivilDate other)
    {
        ArgumentNullException.ThrowIfNull(other);
        return Calendar.DaysBetween(ToJson(), other.ToJson()).GetInt64();
    }

    /// <summary>This date at a time of day: a <see cref="CivilDateTime"/> with no zone.</summary>
    public CivilDateTime At(CivilTime time)
    {
        ArgumentNullException.ThrowIfNull(time);
        return new CivilDateTime(this, time);
    }

    /// <summary>As a .NET <see cref="DateOnly"/>: <c>unsupported</c> (<c>out-of-native-range</c>) outside years 1 to 9999.</summary>
    public Outcome<DateOnly> ToDateOnly() => Typed.ToOutcome(Interop.ToDateOnly(ToJson()), "toDateOnly");

    /// <inheritdoc/>
    public JsonElement ToJson() => Values.ToElement(Node());

    /// <summary>The text form, for example <c>2026-11-05</c> or <c>+275760-09-13</c>.</summary>
    public override string ToString() => Typed.Text(this);

    /// <summary>Calendar order.</summary>
    public int CompareTo(CivilDate? other) => other is null ? 1 : (int)TypedWire.ParseOrdering(Calendar.Compare(ToJson(), other.ToJson()).GetString());

    /// <summary>Earlier than.</summary>
    public static bool operator <(CivilDate left, CivilDate right) => left.CompareTo(right) < 0;

    /// <summary>Later than.</summary>
    public static bool operator >(CivilDate left, CivilDate right) => left.CompareTo(right) > 0;

    /// <summary>Earlier than or the same day.</summary>
    public static bool operator <=(CivilDate left, CivilDate right) => left.CompareTo(right) <= 0;

    /// <summary>Later than or the same day.</summary>
    public static bool operator >=(CivilDate left, CivilDate right) => left.CompareTo(right) >= 0;

    internal JsonObject Node() => Values.Civil("date", Values.DateFields(Year, Month, Day));

    private CivilDate Add(JsonElement period) => Typed.Total(Calendar.Add(ToJson(), period), Typed.Trusted<CivilDate>);

    private JsonElement Field(string name) => Calendar.Fields(ToJson()).Value.GetProperty(name);
}
