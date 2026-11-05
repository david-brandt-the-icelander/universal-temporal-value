using System;
using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;
using Utv.Internal.Everyday;

namespace Utv;

/// <summary>
/// A time of day with no date and no zone: <c>09:30</c>, <c>09:30:15.25</c>. It remembers whether a second was
/// given, so <c>09:30</c> and <c>09:30:00</c> are different values (comparison treats a missing second as 0).
/// </summary>
/// <remarks>
/// There is no arithmetic: wrapping past midnight would lose the day. Add to a <see cref="CivilDateTime"/> instead.
/// </remarks>
[DebuggerDisplay("CivilTime({ToString(),nq})")]
public sealed record CivilTime : ITemporalValue, IComparable<CivilTime>
{
    internal CivilTime(int hour, int minute, int? second, int nanosecond)
    {
        Hour = hour;
        Minute = minute;
        Second = second;
        Nanosecond = second is null ? 0 : nanosecond;
    }

    /// <summary>The hour, 0 to 23.</summary>
    public int Hour { get; }

    /// <summary>The minute, 0 to 59.</summary>
    public int Minute { get; }

    /// <summary>The second, 0 to 59, or null when none was given (<c>09:30</c>).</summary>
    public int? Second { get; }

    /// <summary>The nanosecond of the second, 0 to 999,999,999.</summary>
    public int Nanosecond { get; }

    /// <summary><c>CivilTime.Of(9, 30)</c> is <c>09:30</c>, without seconds.</summary>
    /// <exception cref="ArgumentOutOfRangeException">A field is out of range.</exception>
    public static CivilTime Of(int hour, int minute) => Typed.OrThrow(TryOf(hour, minute));

    /// <summary><c>CivilTime.Of(9, 30, 0)</c> is <c>09:30:00</c>.</summary>
    /// <exception cref="ArgumentOutOfRangeException">A field is out of range; second 60 needs a leap-second table.</exception>
    public static CivilTime Of(int hour, int minute, int second) => Typed.OrThrow(TryOf(hour, minute, second));

    /// <summary><c>CivilTime.Of(9, 30, 15, 250_000_000)</c> is <c>09:30:15.25</c>.</summary>
    /// <exception cref="ArgumentOutOfRangeException">A field is out of range.</exception>
    public static CivilTime Of(int hour, int minute, int second, int nanosecond) => Typed.OrThrow(TryOf(hour, minute, second, nanosecond));

    /// <summary>A time, or <c>invalid</c> (<c>field-out-of-range</c>).</summary>
    public static Outcome<CivilTime> TryOf(int hour, int minute) => Make(hour, minute, null, null);

    /// <summary>A time, or <c>invalid</c> (<c>field-out-of-range</c>), or <c>unsupported</c> (<c>leap-second</c>) for second 60.</summary>
    public static Outcome<CivilTime> TryOf(int hour, int minute, int second) => Make(hour, minute, second, null);

    /// <summary>A time, or <c>invalid</c> (<c>field-out-of-range</c>), or <c>unsupported</c> (<c>leap-second</c>) for second 60.</summary>
    public static Outcome<CivilTime> TryOf(int hour, int minute, int second, int nanosecond) => Make(hour, minute, second, nanosecond);

    /// <summary>Parses text such as <c>09:30</c> or <c>09:30:15.25</c>.</summary>
    public static Outcome<CivilTime> Parse(string text) => Typed.Parse<CivilTime>(text, "a time");

    /// <summary>Reads a UTV JSON <c>time</c>.</summary>
    public static Outcome<CivilTime> FromJson(JsonElement json) => Typed.FromJson<CivilTime>(json, "a time");

    /// <summary>Reads UTV JSON text.</summary>
    public static Outcome<CivilTime> FromJson(string json) => Typed.FromJson<CivilTime>(json, "a time");

    /// <summary>A .NET <see cref="TimeOnly"/> as a time with seconds (always possible).</summary>
    public static CivilTime FromTimeOnly(TimeOnly time) => Typed.Trusted<CivilTime>(Interop.FromTimeOnly(time));

    /// <summary>As a .NET <see cref="TimeOnly"/>: <c>unsupported</c> (<c>out-of-native-range</c>) below its 100 ns tick.</summary>
    public Outcome<TimeOnly> ToTimeOnly() => Typed.ToOutcome(Interop.ToTimeOnly(ToJson()), "toTimeOnly");

    /// <inheritdoc/>
    public JsonElement ToJson() => Values.ToElement(Values.Civil("time", Fields(new JsonObject())));

    /// <summary>The text form: <c>09:30</c>, <c>09:30:00</c>, <c>09:30:15.25</c>.</summary>
    public override string ToString() => Typed.Text(this);

    /// <summary>Order within a day; a missing second counts as 0.</summary>
    public int CompareTo(CivilTime? other) => other is null ? 1 : (int)TypedWire.ParseOrdering(Calendar.Compare(ToJson(), other.ToJson()).GetString());

    /// <summary>Earlier in the day.</summary>
    public static bool operator <(CivilTime left, CivilTime right) => left.CompareTo(right) < 0;

    /// <summary>Later in the day.</summary>
    public static bool operator >(CivilTime left, CivilTime right) => left.CompareTo(right) > 0;

    /// <summary>Earlier or at the same time.</summary>
    public static bool operator <=(CivilTime left, CivilTime right) => left.CompareTo(right) <= 0;

    /// <summary>Later or at the same time.</summary>
    public static bool operator >=(CivilTime left, CivilTime right) => left.CompareTo(right) >= 0;

    internal JsonObject Fields(JsonObject fields) => Values.AddTime(fields, Hour, Minute, Second, Second is null ? null : Nanosecond);

    private static Outcome<CivilTime> Make(int hour, int minute, int? second, int? nanosecond) =>
        Typed.FromJson<CivilTime>(Values.ToElement(Values.Civil("time", RawFields(hour, minute, second, nanosecond))), "a time");

    /// <summary>Fields as given, so that the JSON layer judges them (a zero nanosecond is still checked, then omitted).</summary>
    internal static JsonObject RawFields(int hour, int minute, int? second, int? nanosecond, JsonObject? into = null)
    {
        JsonObject fields = into ?? new JsonObject();
        fields["hour"] = hour;
        fields["minute"] = minute;
        if (second is not null)
        {
            fields["second"] = second.Value;
        }
        if (nanosecond is not null && (nanosecond.Value != 0 || second is null))
        {
            fields["nano"] = nanosecond.Value;
        }
        return fields;
    }
}
