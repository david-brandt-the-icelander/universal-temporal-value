using System;
using System.Diagnostics;
using System.Globalization;
using System.Numerics;
using System.Text.Json;
using System.Text.Json.Nodes;
using Utv.Internal.Everyday;

namespace Utv;

/// <summary>The components of a <see cref="Period"/>; each may be negative.</summary>
/// <param name="Years">Calendar years.</param>
/// <param name="Months">Calendar months.</param>
/// <param name="Weeks">Weeks (7 calendar days).</param>
/// <param name="Days">Calendar days.</param>
/// <param name="Hours">Wall-clock hours.</param>
/// <param name="Minutes">Wall-clock minutes.</param>
/// <param name="Seconds">Wall-clock seconds.</param>
public readonly record struct PeriodComponents(long Years, long Months, long Weeks, long Days, long Hours, long Minutes, long Seconds);

/// <summary>
/// A calendar amount: <c>P1M</c> is a month and <c>PT1M</c> a minute. Calendar units have no fixed length (a month is
/// 28 to 31 days, a day 23 to 25 hours across DST); for exact seconds use <see cref="PosixSeconds"/>.
/// </summary>
[DebuggerDisplay("Period({ToString(),nq})")]
public sealed record Period : ITemporalValue
{
    internal Period(long years, long months, long weeks, long days, long hours, long minutes, long seconds)
    {
        Components = new PeriodComponents(years, months, weeks, days, hours, minutes, seconds);
    }

    /// <summary>The empty period, <c>P0D</c>.</summary>
    public static Period Zero { get; } = new(0, 0, 0, 0, 0, 0, 0);

    /// <summary>The components (use <c>period.Components.Days</c>, …).</summary>
    public PeriodComponents Components { get; }

    /// <summary>A period from components: <c>Period.Of(months: 1, days: 3)</c>.</summary>
    public static Period Of(long years = 0, long months = 0, long weeks = 0, long days = 0, long hours = 0, long minutes = 0, long seconds = 0) =>
        new(years, months, weeks, days, hours, minutes, seconds);

    /// <summary><c>P{n}Y</c>.</summary>
    public static Period Years(long years) => Of(years: years);

    /// <summary><c>P{n}M</c>.</summary>
    public static Period Months(long months) => Of(months: months);

    /// <summary><c>P{n}W</c>.</summary>
    public static Period Weeks(long weeks) => Of(weeks: weeks);

    /// <summary><c>P{n}D</c>.</summary>
    public static Period Days(long days) => Of(days: days);

    /// <summary><c>PT{n}H</c>.</summary>
    public static Period Hours(long hours) => Of(hours: hours);

    /// <summary><c>PT{n}M</c>.</summary>
    public static Period Minutes(long minutes) => Of(minutes: minutes);

    /// <summary><c>PT{n}S</c>.</summary>
    public static Period Seconds(long seconds) => Of(seconds: seconds);

    /// <summary>Parses text such as <c>P1M</c>, <c>P1Y2M10DT2H30M</c> or <c>-P1D</c>.</summary>
    public static Outcome<Period> Parse(string text) => Typed.Parse<Period>(text, "a period");

    /// <summary>Reads a UTV JSON <c>calendar</c> amount.</summary>
    public static Outcome<Period> FromJson(JsonElement json) => Typed.FromJson<Period>(json, "a period");

    /// <summary>Reads UTV JSON text.</summary>
    public static Outcome<Period> FromJson(string json) => Typed.FromJson<Period>(json, "a period");

    /// <summary>Every component negated.</summary>
    /// <exception cref="OverflowException">A component is <see cref="long.MinValue"/>.</exception>
    public Period Negate()
    {
        PeriodComponents c = Components;
        return checked(new Period(-c.Years, -c.Months, -c.Weeks, -c.Days, -c.Hours, -c.Minutes, -c.Seconds));
    }

    /// <inheritdoc/>
    public JsonElement ToJson()
    {
        PeriodComponents c = Components;
        return Build.Period(c.Years, c.Months, c.Weeks, c.Days, c.Hours, c.Minutes, c.Seconds);
    }

    /// <summary>The text form, for example <c>P1M</c>; a period with mixed signs has none and prints as JSON.</summary>
    public override string ToString() => Typed.Text(this);
}

/// <summary>
/// An exact number of POSIX seconds (a POSIX day is always 86,400 of them): the difference between two
/// <see cref="Instant"/>s, kept as a reduced fraction. It has no text form; <c>ToString()</c> gives a display string
/// such as <c>3600.75 s (POSIX)</c> or <c>1/3 s (POSIX)</c>.
/// </summary>
[DebuggerDisplay("PosixSeconds({ToString(),nq})")]
public sealed record PosixSeconds : ITemporalValue
{
    private readonly Rational _value;

    internal PosixSeconds(Rational value)
    {
        _value = value;
    }

    /// <summary>The numerator of the reduced fraction (it carries the sign).</summary>
    public BigInteger Numerator => _value.Numerator;

    /// <summary>The denominator of the reduced fraction (positive).</summary>
    public BigInteger Denominator => _value.Denominator;

    /// <summary>Whole seconds.</summary>
    public static PosixSeconds Of(long seconds) => new(Rational.FromInteger(seconds));

    /// <summary><paramref name="numerator"/>/<paramref name="denominator"/> seconds, stored reduced.</summary>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="denominator"/> is zero.</exception>
    public static PosixSeconds Of(BigInteger numerator, BigInteger denominator) => new(new Rational(numerator, denominator));

    /// <summary>A .NET <see cref="TimeSpan"/> as POSIX seconds (exact).</summary>
    public static PosixSeconds FromTimeSpan(TimeSpan value) => Typed.Trusted<PosixSeconds>(Interop.PosixSecondsFromTimeSpan(value));

    /// <summary>Reads a UTV JSON POSIX <c>clock-ticks</c> amount.</summary>
    public static Outcome<PosixSeconds> FromJson(JsonElement json) => Typed.FromJson<PosixSeconds>(json, "POSIX seconds");

    /// <summary>Reads UTV JSON text.</summary>
    public static Outcome<PosixSeconds> FromJson(string json) => Typed.FromJson<PosixSeconds>(json, "POSIX seconds");

    /// <summary>The same amount, negated.</summary>
    public PosixSeconds Negate() => new(-_value);

    /// <summary>As a .NET <see cref="TimeSpan"/>: <c>unsupported</c> (<c>out-of-native-range</c>) below 100 ns or beyond its range.</summary>
    public Outcome<TimeSpan> ToTimeSpan() => Typed.ToOutcome(Interop.ToTimeSpan(ToJson()), "toTimeSpan");

    /// <inheritdoc/>
    public JsonElement ToJson() => Values.ToElement(Values.PosixSeconds(_value));

    /// <summary>A display string (not parseable): <c>3600.75 s (POSIX)</c>, <c>1/3 s (POSIX)</c>.</summary>
    public override string ToString() => SecondsDisplay.Of(_value, "POSIX");
}

/// <summary>
/// An exact number of SI seconds (<c>elapsed</c>), as a physical clock measures them. Adding them to a POSIX
/// <see cref="Instant"/> needs a leap-second table, so it is <c>unsupported</c> here. <c>ToString()</c> gives a display
/// string such as <c>86400 s (SI)</c>.
/// </summary>
[DebuggerDisplay("ElapsedSeconds({ToString(),nq})")]
public sealed record ElapsedSeconds : ITemporalValue
{
    private readonly Rational _value;

    internal ElapsedSeconds(Rational value)
    {
        _value = value;
    }

    /// <summary>The numerator of the reduced fraction (it carries the sign).</summary>
    public BigInteger Numerator => _value.Numerator;

    /// <summary>The denominator of the reduced fraction (positive).</summary>
    public BigInteger Denominator => _value.Denominator;

    /// <summary>Whole SI seconds.</summary>
    public static ElapsedSeconds Of(long seconds) => new(Rational.FromInteger(seconds));

    /// <summary><paramref name="numerator"/>/<paramref name="denominator"/> SI seconds, stored reduced.</summary>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="denominator"/> is zero.</exception>
    public static ElapsedSeconds Of(BigInteger numerator, BigInteger denominator) => new(new Rational(numerator, denominator));

    /// <summary>Reads a UTV JSON <c>elapsed</c> amount.</summary>
    public static Outcome<ElapsedSeconds> FromJson(JsonElement json) => Typed.FromJson<ElapsedSeconds>(json, "SI seconds");

    /// <summary>Reads UTV JSON text.</summary>
    public static Outcome<ElapsedSeconds> FromJson(string json) => Typed.FromJson<ElapsedSeconds>(json, "SI seconds");

    /// <summary>The same amount, negated.</summary>
    public ElapsedSeconds Negate() => new(-_value);

    /// <inheritdoc/>
    public JsonElement ToJson() => Values.ToElement(new JsonObject
    {
        ["schema"] = UtvValidator.SpecVersion,
        ["kind"] = "amount",
        ["value"] = new JsonObject { ["basis"] = "elapsed", ["seconds"] = Values.RationalNode(_value) },
    });

    /// <summary>A display string (not parseable): <c>86400 s (SI)</c>.</summary>
    public override string ToString() => SecondsDisplay.Of(_value, "SI");
}

internal static class SecondsDisplay
{
    /// <summary>A decimal when the fraction terminates (<c>3600.75 s (POSIX)</c>), otherwise <c>1/3 s (POSIX)</c>.</summary>
    public static string Of(Rational value, string basis)
    {
        BigInteger d = value.Denominator;
        int twos = 0;
        int fives = 0;
        while (d.IsEven)
        {
            d /= 2;
            twos++;
        }
        while ((d % 5).IsZero)
        {
            d /= 5;
            fives++;
        }
        if (!d.IsOne)
        {
            return value.Numerator.ToString(CultureInfo.InvariantCulture) + "/" + value.Denominator.ToString(CultureInfo.InvariantCulture) + " s (" + basis + ")";
        }
        int places = Math.Max(twos, fives);
        BigInteger scaled = BigInteger.Abs(value.Numerator) * BigInteger.Pow(10, places) / value.Denominator;
        string digits = scaled.ToString(CultureInfo.InvariantCulture).PadLeft(places + 1, '0');
        string text = places == 0 ? digits : digits[..^places] + "." + digits[^places..];
        return (value.Numerator.Sign < 0 ? "-" : "") + text + " s (" + basis + ")";
    }
}
