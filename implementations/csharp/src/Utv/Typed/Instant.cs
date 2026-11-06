using System;
using System.Diagnostics;
using System.Numerics;
using System.Text.Json;
using Utv.Internal.Everyday;

namespace Utv;

/// <summary>
/// A point on the POSIX timeline: <c>2026-11-05T13:30:00Z</c>. It knows UTC but no local offset or zone, and keeps
/// fractions of a second exactly.
/// </summary>
[DebuggerDisplay("Instant({ToString(),nq})")]
public sealed record Instant : IIntervalPoint, IComparable<Instant>
{
    private readonly Rational _seconds;

    internal Instant(Rational seconds)
    {
        _seconds = seconds;
    }

    /// <summary>Whole POSIX seconds since 1970-01-01T00:00:00Z (the floor, so negative before 1970).</summary>
    public BigInteger UnixSeconds => _seconds.Floor();

    /// <summary>
    /// The nanoseconds after <see cref="UnixSeconds"/>, 0 to 999,999,999: the floor of the sub-second part. A fraction
    /// finer than a nanosecond (from exact arithmetic such as adding 1/3 s) is kept exactly in <see cref="SubSecond"/>.
    /// </summary>
    public int Nanoseconds
    {
        get
        {
            Rational fraction = _seconds.FractionalPart();
            return (int)(fraction.Numerator * Rational.NanosPerSecond / fraction.Denominator);
        }
    }

    /// <summary>The exact fraction of a second after <see cref="UnixSeconds"/>, in [0, 1), as a reduced rational.</summary>
    public PosixSeconds SubSecond => new(_seconds.FractionalPart());

    string IIntervalPoint.Domain => "physical";

    internal Rational Seconds => _seconds;

    /// <summary><paramref name="seconds"/> POSIX seconds plus <paramref name="nanoseconds"/> since the Unix epoch.</summary>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="nanoseconds"/> is not 0 to 999,999,999.</exception>
    public static Instant FromUnixSeconds(long seconds, long nanoseconds = 0)
    {
        if (nanoseconds is < 0 or > 999_999_999)
        {
            throw new ArgumentOutOfRangeException(nameof(nanoseconds), nanoseconds, "nanoseconds are 0 to 999,999,999");
        }
        return Typed.Trusted<Instant>(Build.Instant(seconds, nanoseconds));
    }

    /// <summary>Nanoseconds since the Unix epoch, for example from a clock reading you took yourself.</summary>
    public static Instant FromUnixNanoseconds(long nanoseconds) =>
        Typed.Trusted<Instant>(Build.Instant(MathUtil.FloorDiv(nanoseconds, 1_000_000_000), MathUtil.FloorMod(nanoseconds, 1_000_000_000)));

    /// <summary>
    /// A .NET <see cref="DateTimeOffset"/> as the instant it names; its offset is not kept (an instant knows UTC, not a
    /// local offset). To keep the offset, use <see cref="FixedOffsetDateTime.FromDateTimeOffset"/>.
    /// </summary>
    public static Instant FromDateTimeOffset(DateTimeOffset value) =>
        Typed.Trusted<Instant>(Interop.FromDateTimeOffset(value, DateTimeOffsetIntent.Instant));

    /// <summary>
    /// A .NET <see cref="DateTime"/> of kind <see cref="DateTimeKind.Utc"/> as an instant. <see cref="DateTimeKind.Local"/>
    /// is <c>unsupported</c> (<c>local-zone</c>); <see cref="DateTimeKind.Unspecified"/> is <c>invalid</c>
    /// (<c>intent-required</c>): it is a wall time, so use <see cref="CivilDateTime.FromDateTime"/>.
    /// </summary>
    public static Outcome<Instant> FromDateTime(DateTime value)
    {
        if (value.Kind == DateTimeKind.Unspecified)
        {
            return Typed.Failure<Instant>("fromDateTime", UtvStatus.Invalid, new[]
            {
                Diagnostic.Error("intent-required", "", "a DateTimeKind.Unspecified value is a wall time, not an instant; use CivilDateTime.FromDateTime and give it a zone or offset"),
            });
        }
        return Typed.ToOutcome<Instant>(Interop.FromDateTime(value));
    }

    /// <summary>Parses text such as <c>2026-11-05T13:30:00Z</c>.</summary>
    public static Outcome<Instant> Parse(string text) => Typed.Parse<Instant>(text, "an instant");

    /// <summary>Reads a UTV JSON POSIX <c>instant</c>.</summary>
    public static Outcome<Instant> FromJson(JsonElement json) => Typed.FromJson<Instant>(json, "an instant");

    /// <summary>Reads UTV JSON text.</summary>
    public static Outcome<Instant> FromJson(string json) => Typed.FromJson<Instant>(json, "an instant");

    /// <summary>Exactly <paramref name="seconds"/> POSIX seconds later.</summary>
    public Instant Add(PosixSeconds seconds)
    {
        ArgumentNullException.ThrowIfNull(seconds);
        return Typed.Total(Calendar.Add(ToJson(), seconds.ToJson()), Typed.Trusted<Instant>);
    }

    /// <summary>
    /// SI seconds on the POSIX timeline: <c>unsupported</c> (<c>needs-leap-seconds</c>), because a leap-second table
    /// is needed and none is shipped. Use <see cref="PosixSeconds"/>.
    /// </summary>
    public Outcome<Instant> Add(ElapsedSeconds seconds)
    {
        ArgumentNullException.ThrowIfNull(seconds);
        return Typed.ToOutcome<Instant>(Calendar.Add(ToJson(), seconds.ToJson()));
    }

    /// <summary>The exact POSIX seconds from this instant to <paramref name="other"/> (negative if it is earlier).</summary>
    public PosixSeconds Until(Instant other)
    {
        ArgumentNullException.ThrowIfNull(other);
        return Typed.Total(Calendar.PosixSecondsBetween(ToJson(), other.ToJson()), Typed.Trusted<PosixSeconds>);
    }

    /// <summary>The wall time at a fixed offset (seconds east of UTC); no rules needed.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The offset is beyond ±18:00.</exception>
    public FixedOffsetDateTime AtOffset(int offsetSeconds)
    {
        FixedOffsetDateTime.CheckOffset(offsetSeconds);
        return Typed.Total(Zones.ToOffset(ToJson(), offsetSeconds), Typed.Trusted<FixedOffsetDateTime>);
    }

    /// <summary>The wall time in a zone, with the offset in force asserted; see <see cref="TemporalContext.InZone"/>.</summary>
    public Outcome<ZonedIntent> InZone(string zoneId, TemporalContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return context.InZone(this, zoneId);
    }

    /// <summary>As a .NET <see cref="DateTimeOffset"/> at +00:00: <c>unsupported</c> (<c>out-of-native-range</c>) if it doesn't fit.</summary>
    public Outcome<DateTimeOffset> ToDateTimeOffset() => Typed.ToOutcome(Interop.ToDateTimeOffset(ToJson()), "toDateTimeOffset");

    /// <inheritdoc/>
    public JsonElement ToJson() => Values.ToElement(Values.Instant(_seconds));

    /// <summary>The text form, for example <c>2026-11-05T13:30:00.25Z</c>.</summary>
    public override string ToString() => Typed.Text(this);

    /// <summary>Timeline order.</summary>
    public int CompareTo(Instant? other) => other is null ? 1 : (int)TypedWire.ParseOrdering(Calendar.Compare(ToJson(), other.ToJson()).GetString());

    /// <summary>Exactly <paramref name="seconds"/> POSIX seconds later.</summary>
    public static Instant operator +(Instant instant, PosixSeconds seconds)
    {
        ArgumentNullException.ThrowIfNull(instant);
        return instant.Add(seconds);
    }

    /// <summary>Earlier.</summary>
    public static bool operator <(Instant left, Instant right) => left.CompareTo(right) < 0;

    /// <summary>Later.</summary>
    public static bool operator >(Instant left, Instant right) => left.CompareTo(right) > 0;

    /// <summary>Earlier or the same.</summary>
    public static bool operator <=(Instant left, Instant right) => left.CompareTo(right) <= 0;

    /// <summary>Later or the same.</summary>
    public static bool operator >=(Instant left, Instant right) => left.CompareTo(right) >= 0;
}
