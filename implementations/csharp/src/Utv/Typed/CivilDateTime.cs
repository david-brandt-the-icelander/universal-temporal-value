using System;
using System.Diagnostics;
using System.Numerics;
using System.Text.Json;
using System.Text.Json.Nodes;
using Utv.Internal.Everyday;
using static System.FormattableString;

namespace Utv;

/// <summary>
/// A date and a time of day with no zone (a "floating" wall time): <c>2026-11-05T14:30</c>. It names no instant
/// until you give it a zone (<see cref="InZone"/>) or an offset (<see cref="AtOffset"/>).
/// </summary>
[DebuggerDisplay("CivilDateTime({ToString(),nq})")]
public sealed record CivilDateTime : ITemporalValue, IComparable<CivilDateTime>
{
    internal CivilDateTime(CivilDate date, CivilTime time)
    {
        Date = date;
        Time = time;
    }

    /// <summary>The calendar day.</summary>
    public CivilDate Date { get; }

    /// <summary>The time of day.</summary>
    public CivilTime Time { get; }

    /// <summary><c>CivilDateTime.Of(2026, 11, 5, 14, 30)</c> is <c>2026-11-05T14:30</c>.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The date doesn't exist or a time field is out of range.</exception>
    public static CivilDateTime Of(BigInteger year, int month, int day, int hour, int minute) =>
        Typed.OrThrow(Make(year, month, day, hour, minute, null, null));

    /// <summary>A date-time with seconds.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The date doesn't exist or a time field is out of range.</exception>
    public static CivilDateTime Of(BigInteger year, int month, int day, int hour, int minute, int second) =>
        Typed.OrThrow(Make(year, month, day, hour, minute, second, null));

    /// <summary>A date-time with seconds and nanoseconds.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The date doesn't exist or a time field is out of range.</exception>
    public static CivilDateTime Of(BigInteger year, int month, int day, int hour, int minute, int second, int nanosecond) =>
        Typed.OrThrow(Make(year, month, day, hour, minute, second, nanosecond));

    /// <summary>A date-time, or <c>invalid</c> (<c>day-out-of-range</c>, <c>field-out-of-range</c>, …).</summary>
    public static Outcome<CivilDateTime> TryOf(BigInteger year, int month, int day, int hour, int minute) =>
        Make(year, month, day, hour, minute, null, null);

    /// <summary>A date-time with seconds, or <c>invalid</c>, or <c>unsupported</c> (<c>leap-second</c>) for second 60.</summary>
    public static Outcome<CivilDateTime> TryOf(BigInteger year, int month, int day, int hour, int minute, int second) =>
        Make(year, month, day, hour, minute, second, null);

    /// <summary>A date-time with seconds and nanoseconds, or an outcome saying why not.</summary>
    public static Outcome<CivilDateTime> TryOf(BigInteger year, int month, int day, int hour, int minute, int second, int nanosecond) =>
        Make(year, month, day, hour, minute, second, nanosecond);

    /// <summary>Parses text such as <c>2026-11-05T14:30</c>.</summary>
    public static Outcome<CivilDateTime> Parse(string text) => Typed.Parse<CivilDateTime>(text, "a date-time without a zone");

    /// <summary>Reads a UTV JSON <c>datetime</c> with no zone.</summary>
    public static Outcome<CivilDateTime> FromJson(JsonElement json) => Typed.FromJson<CivilDateTime>(json, "a date-time without a zone");

    /// <summary>Reads UTV JSON text.</summary>
    public static Outcome<CivilDateTime> FromJson(string json) => Typed.FromJson<CivilDateTime>(json, "a date-time without a zone");

    /// <summary>
    /// A .NET <see cref="System.DateTime"/> of kind <see cref="DateTimeKind.Unspecified"/> as a wall time.
    /// <see cref="DateTimeKind.Local"/> is <c>unsupported</c> (<c>local-zone</c>): it is never read with the host's zone.
    /// <see cref="DateTimeKind.Utc"/> is <c>invalid</c> (<c>intent-required</c>): it is an instant; use <see cref="Instant.FromDateTime"/>.
    /// </summary>
    public static Outcome<CivilDateTime> FromDateTime(System.DateTime value)
    {
        if (value.Kind == DateTimeKind.Utc)
        {
            return Typed.Failure<CivilDateTime>("fromDateTime", UtvStatus.Invalid, new[]
            {
                Diagnostic.Error("intent-required", "", "a DateTimeKind.Utc value is an instant, not a wall time; use Instant.FromDateTime, or say which wall time you mean"),
            });
        }
        return Typed.ToOutcome<CivilDateTime>(Interop.FromDateTime(value));
    }

    /// <summary>
    /// <paramref name="days"/> days later on the wall clock (earlier if negative). Wall-clock arithmetic never fails;
    /// the same holds for <see cref="AddWeeks"/>, <see cref="AddHours"/>, <see cref="AddMinutes"/> and <see cref="AddSeconds"/>.
    /// </summary>
    public CivilDateTime AddDays(long days) => Add(Build.Period(days: days));

    /// <summary><paramref name="weeks"/> weeks later.</summary>
    public CivilDateTime AddWeeks(long weeks) => Add(Build.Period(weeks: weeks));

    /// <summary><paramref name="hours"/> hours later on the wall clock, carrying into days: 23:30 + 2 hours is 01:30 the next day.</summary>
    public CivilDateTime AddHours(long hours) => Add(Build.Period(hours: hours));

    /// <summary><paramref name="minutes"/> minutes later on the wall clock.</summary>
    public CivilDateTime AddMinutes(long minutes) => Add(Build.Period(minutes: minutes));

    /// <summary><paramref name="seconds"/> seconds later on the wall clock.</summary>
    public CivilDateTime AddSeconds(long seconds) => Add(Build.Period(seconds: seconds));

    /// <summary><paramref name="months"/> months later: <c>none</c> (<c>month-end</c>) if the day doesn't exist.</summary>
    public Outcome<CivilDateTime> AddMonths(long months) => Typed.ToOutcome<CivilDateTime>(Calendar.Add(ToJson(), Build.Period(months: months)));

    /// <summary><paramref name="months"/> months later; <see cref="MonthEnd.Constrain"/> clamps a missing day.</summary>
    /// <exception cref="ArgumentException"><paramref name="monthEnd"/> is <see cref="MonthEnd.Reject"/>: use the overload without a policy, which returns an <see cref="Outcome{T}"/>.</exception>
    public CivilDateTime AddMonths(long months, MonthEnd monthEnd)
    {
        if (monthEnd != MonthEnd.Constrain)
        {
            throw new ArgumentException("this overload always answers, so it takes only MonthEnd.Constrain; the overload without a policy returns an Outcome", nameof(monthEnd));
        }
        return Typed.ToOutcome<CivilDateTime>(Calendar.Add(ToJson(), Build.Period(months: months), monthEnd.ToPolicy())).Single();
    }

    /// <summary>Adds a period: <c>none</c> (<c>month-end</c>) if a month step lands on a day that doesn't exist.</summary>
    public Outcome<CivilDateTime> Add(Period period, MonthEnd monthEnd = MonthEnd.Reject)
    {
        ArgumentNullException.ThrowIfNull(period);
        return Typed.ToOutcome<CivilDateTime>(Calendar.Add(ToJson(), period.ToJson(), monthEnd.ToPolicy()));
    }

    /// <summary>This wall time in an IANA zone: an intention such as "09:00 in Paris", resolved later with rules.</summary>
    /// <exception cref="ArgumentException">The zone id is not an IANA name such as <c>Europe/Paris</c>.</exception>
    public ZonedIntent InZone(string zoneId)
    {
        ArgumentException.ThrowIfNullOrEmpty(zoneId);
        if (!Iso.ZoneNamePattern().IsMatch(zoneId))
        {
            throw new ArgumentException(Invariant($"`{zoneId}` is not an IANA zone name such as Europe/Paris"), nameof(zoneId));
        }
        return new ZonedIntent(this, zoneId, null);
    }

    /// <summary>This wall time at a fixed UTC offset (seconds east of Greenwich): an exact instant, no rules needed.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The offset is beyond ±18:00.</exception>
    public FixedOffsetDateTime AtOffset(int offsetSeconds)
    {
        FixedOffsetDateTime.CheckOffset(offsetSeconds);
        return new FixedOffsetDateTime(this, offsetSeconds);
    }

    /// <summary>As a .NET <see cref="System.DateTime"/> of kind <see cref="DateTimeKind.Unspecified"/>; <c>unsupported</c> (<c>out-of-native-range</c>) if it doesn't fit.</summary>
    public Outcome<System.DateTime> ToDateTime() => Typed.ToOutcome(Interop.ToDateTime(ToJson()), "toDateTime");

    /// <inheritdoc/>
    public JsonElement ToJson() => Values.ToElement(Node());

    /// <summary>The text form, for example <c>2026-11-05T14:30</c>.</summary>
    public override string ToString() => Typed.Text(this);

    /// <summary>Wall-clock order; a missing second counts as 0.</summary>
    public int CompareTo(CivilDateTime? other) => other is null ? 1 : (int)TypedWire.ParseOrdering(Calendar.Compare(ToJson(), other.ToJson()).GetString());

    /// <summary>Earlier on the wall clock.</summary>
    public static bool operator <(CivilDateTime left, CivilDateTime right) => left.CompareTo(right) < 0;

    /// <summary>Later on the wall clock.</summary>
    public static bool operator >(CivilDateTime left, CivilDateTime right) => left.CompareTo(right) > 0;

    /// <summary>Earlier or the same.</summary>
    public static bool operator <=(CivilDateTime left, CivilDateTime right) => left.CompareTo(right) <= 0;

    /// <summary>Later or the same.</summary>
    public static bool operator >=(CivilDateTime left, CivilDateTime right) => left.CompareTo(right) >= 0;

    internal JsonObject Node() => Values.Civil("datetime", Time.Fields(Values.DateFields(Date.Year, Date.Month, Date.Day)));

    private CivilDateTime Add(JsonElement period) => Typed.Total(Calendar.Add(ToJson(), period), Typed.Trusted<CivilDateTime>);

    private static Outcome<CivilDateTime> Make(BigInteger year, int month, int day, int hour, int minute, int? second, int? nanosecond) =>
        Typed.FromJson<CivilDateTime>(
            Values.ToElement(Values.Civil("datetime", CivilTime.RawFields(hour, minute, second, nanosecond, Values.DateFields(year, month, day)))),
            "a date-time without a zone");
}
