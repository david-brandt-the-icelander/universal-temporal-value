using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Numerics;
using System.Text.Json;
using System.Text.Json.Nodes;
using Utv.Internal.Everyday;
using static System.FormattableString;

namespace Utv;

/// <summary>What a <see cref="DateTimeOffset"/> means, chosen by the caller: nothing is chosen silently.</summary>
public enum DateTimeOffsetIntent
{
    /// <summary>A point on the timeline; the offset is discarded (a UTV instant knows UTC, not a local offset).</summary>
    Instant,
    /// <summary>A wall-clock date-time at a fixed offset; both are kept (<see cref="Build.WithOffset"/>).</summary>
    OffsetDateTime,
}

/// <summary>
/// The outcome of converting a UTV value to a .NET type: a status, the value when <see cref="UtvStatus.Unique"/>,
/// diagnostics, and whether information was lost.
/// </summary>
/// <typeparam name="T">The .NET type.</typeparam>
public sealed class NativeResult<T>
    where T : struct
{
    private readonly T _value;

    internal NativeResult(UtvStatus status, T value, bool isLossy, IEnumerable<Diagnostic> diagnostics)
    {
        Status = status;
        _value = value;
        IsLossy = isLossy;
        Diagnostics = new ReadOnlyCollection<Diagnostic>(diagnostics.ToArray());
    }

    /// <summary><see cref="UtvStatus.Unique"/> when converted; <see cref="UtvStatus.Unsupported"/> when the .NET type cannot hold the value; <see cref="UtvStatus.Invalid"/> for an invalid value.</summary>
    public UtvStatus Status { get; }

    /// <summary>True when the status is <see cref="UtvStatus.Unique"/>.</summary>
    public bool IsUnique => Status == UtvStatus.Unique;

    /// <summary>True when the conversion succeeded but dropped information (a diagnostic says what).</summary>
    public bool IsLossy { get; }

    /// <summary>Diagnostics: why it failed, or what was lost.</summary>
    public IReadOnlyList<Diagnostic> Diagnostics { get; }

    /// <summary>The converted value.</summary>
    /// <exception cref="InvalidOperationException">The status is not <c>unique</c>.</exception>
    public T Value => IsUnique
        ? _value
        : throw new InvalidOperationException("conversion gave `" + Status.ToWireString() + "`, so there is no value; check Status first");

    /// <summary>The converted value, if the status is <see cref="UtvStatus.Unique"/>.</summary>
    public bool TryGetValue(out T value)
    {
        value = _value;
        return IsUnique;
    }
}

/// <summary>
/// Explicit adapters between UTV values and .NET's <see cref="DateOnly"/>, <see cref="TimeOnly"/>,
/// <see cref="DateTime"/>, <see cref="DateTimeOffset"/> and <see cref="TimeSpan"/>.
/// </summary>
/// <remarks>
/// The caller names the intent. A <see cref="DateTimeKind.Local"/> value is refused: it is never read with the
/// host's time zone. Conversions back to .NET report what does not fit (years outside 1..9999, nanoseconds that
/// are not a multiple of 100, a zone that .NET would reduce to an offset). Nothing here reads the clock;
/// pass <c>DateTimeOffset.UtcNow</c> in yourself.
/// </remarks>
public static class Interop
{
    private const long TicksPerSecond = TimeSpan.TicksPerSecond;
    private static readonly long UnixEpochTicks = DateTime.UnixEpoch.Ticks;

    // ------------------------------------------------------------------ .NET → UTV

    /// <summary>A <see cref="DateOnly"/> as a Gregorian <c>date</c>.</summary>
    public static JsonElement FromDateOnly(DateOnly date) => Build.Date(date.Year, date.Month, date.Day);

    /// <summary>A <see cref="TimeOnly"/> as a <c>time</c> with seconds (and <c>nano</c> when non-zero).</summary>
    public static JsonElement FromTimeOnly(TimeOnly time) =>
        Build.Time(time.Hour, time.Minute, time.Second, NanoOf(time.Ticks));

    /// <summary>
    /// A <see cref="DateTime"/>, by its <see cref="DateTime.Kind"/>: <see cref="DateTimeKind.Unspecified"/> is an
    /// unzoned ("floating") <c>datetime</c>; <see cref="DateTimeKind.Utc"/> is an <c>instant</c>;
    /// <see cref="DateTimeKind.Local"/> is refused with <c>unsupported</c>, since it depends on the host's zone.
    /// </summary>
    public static UtvResult FromDateTime(DateTime value) => value.Kind switch
    {
        DateTimeKind.Unspecified => new UtvResult("fromDateTime", UtvStatus.Unique, null, new[] { FloatingOf(value) }),
        DateTimeKind.Utc => new UtvResult("fromDateTime", UtvStatus.Unique, null, new[] { InstantOf(value.Ticks - UnixEpochTicks) }),
        _ => Op.Status("fromDateTime", UtvStatus.Unsupported, "local-zone", "",
            "a DateTimeKind.Local value depends on the host's time zone, which this library never reads; "
            + "convert it yourself with ToUniversalTime() or use a DateTimeOffset, and name the intent"),
    };

    /// <summary>A <see cref="DateTimeOffset"/>, as the caller intends: an <c>instant</c> or a fixed-offset <c>datetime</c>.</summary>
    public static JsonElement FromDateTimeOffset(DateTimeOffset value, DateTimeOffsetIntent intent) => intent switch
    {
        DateTimeOffsetIntent.Instant => InstantOf(value.UtcTicks - UnixEpochTicks),
        DateTimeOffsetIntent.OffsetDateTime => Build.WithOffset(FloatingOf(value.DateTime), (int)(value.Offset.Ticks / TicksPerSecond)),
        _ => throw new ArgumentOutOfRangeException(nameof(intent), intent, "not a DateTimeOffset intent"),
    };

    /// <summary>A <see cref="TimeSpan"/> as an amount of POSIX seconds (<see cref="Build.PosixSeconds(BigInteger, BigInteger)"/>), for arithmetic on instants.</summary>
    public static JsonElement PosixSecondsFromTimeSpan(TimeSpan value) =>
        Build.PosixSeconds(value.Ticks, TicksPerSecond);

    // ------------------------------------------------------------------ UTV → .NET

    /// <summary>A Gregorian <c>date</c> as a <see cref="DateOnly"/>; <c>unsupported</c> outside years 1..9999.</summary>
    public static NativeResult<DateOnly> ToDateOnly(JsonElement value) => Convert<DateOnly>(() =>
    {
        CivilRead c = RequireForm(value, "date");
        (BigInteger y, int m, int d) = Values.RequireDate(c);
        return Ok(new DateOnly(RequireNativeYear(y), m, d));
    });

    /// <summary>A <c>time</c> as a <see cref="TimeOnly"/>; <c>unsupported</c> for nanoseconds that are not a multiple of 100.</summary>
    public static NativeResult<TimeOnly> ToTimeOnly(JsonElement value) => Convert<TimeOnly>(() =>
    {
        CivilRead c = RequireForm(value, "time");
        (int h, int mi, int s, int nano) = Values.RequireTime(c);
        return Ok(new TimeOnly(new TimeOnly(h, mi, s).Ticks + RequireTicksOfNano(nano)));
    });

    /// <summary>
    /// An unzoned <c>datetime</c> as a <see cref="DateTimeKind.Unspecified"/> <see cref="DateTime"/>, or an
    /// <c>instant</c> as a <see cref="DateTimeKind.Utc"/> one. A zoned or fixed-offset <c>datetime</c> is
    /// <c>unsupported</c>: a <see cref="DateTime"/> cannot hold the zone; use <see cref="ToDateTimeOffset"/>.
    /// </summary>
    public static NativeResult<DateTime> ToDateTime(JsonElement value) => Convert<DateTime>(() =>
    {
        Values.RequireStructural(value, "value");
        if (Values.KindOf(value) == "instant")
        {
            return Ok(new DateTime(InstantTicks(value), DateTimeKind.Utc));
        }
        CivilRead c = RequireForm(value, "datetime");
        if (c.Zone != ZoneKind.Floating)
        {
            throw OpFailure.Unsupported("unsupported-operand", "/value/zone",
                "a DateTime cannot hold a zone or offset; use ToDateTimeOffset, or Zones.Resolve to an instant first");
        }
        return Ok(new DateTime(WallTicks(c), DateTimeKind.Unspecified));
    });

    /// <summary>
    /// A <see cref="DateTimeOffset"/> from an <c>instant</c> (shown at +00:00), a fixed-offset <c>datetime</c>, or a
    /// zoned <c>datetime</c> that carries an <c>offsetAssertion</c> (lossy: the zone is dropped, only its offset
    /// is kept). A zoned value without an asserted offset, or an unzoned one, is <c>unsupported</c>.
    /// </summary>
    public static NativeResult<DateTimeOffset> ToDateTimeOffset(JsonElement value) => Convert<DateTimeOffset>(() =>
    {
        Values.RequireStructural(value, "value");
        if (Values.KindOf(value) == "instant")
        {
            return Ok(new DateTimeOffset(InstantTicks(value), TimeSpan.Zero), notes: new[]
            {
                new Diagnostic("offset-not-known", DiagnosticSeverity.Info, "",
                    "a UTV instant knows UTC but not a local offset; the DateTimeOffset shows it at +00:00"),
            });
        }
        CivilRead c = RequireForm(value, "datetime");
        long wall = WallTicks(c);
        switch (c.Zone)
        {
            case ZoneKind.FixedOffset:
                return Ok(Offset(wall, c.ZoneSeconds, "/value/zone/seconds"));
            case ZoneKind.Iana when c.OffsetAssertion is int asserted:
                return Ok(Offset(wall, asserted, "/value/offsetAssertion"), lossy: true, notes: new[]
                {
                    new Diagnostic("zone-dropped", DiagnosticSeverity.Warning, "/value/zone",
                        Invariant($"DateTimeOffset keeps the offset but not the zone `{c.ZoneId}`; adding time to it will not follow the zone's rules")),
                });
            case ZoneKind.Iana:
                throw OpFailure.Unsupported("needs-zone-rules", "/value/zone",
                    "a zoned date-time without an asserted offset needs zone rules; use Zones.Resolve first");
            default:
                throw OpFailure.Unsupported("needs-zone-rules", "/value/zone",
                    "an unzoned date-time has no offset; use ToDateTime, or bind a zone or offset first");
        }
    });

    /// <summary>
    /// An amount of POSIX seconds as a <see cref="TimeSpan"/>. Calendar periods are <c>unsupported</c> (a day or a
    /// month has no fixed length), as are fractions finer than 100 ns.
    /// </summary>
    public static NativeResult<TimeSpan> ToTimeSpan(JsonElement amount) => Convert<TimeSpan>(() =>
    {
        Values.RequireStructural(amount, "amount");
        if (Values.KindOf(amount) != "amount")
        {
            throw OpFailure.Unsupported("unsupported-operand", "/kind", "expected an `amount`");
        }
        JsonElement v = amount.GetProperty("value");
        string basis = v.GetProperty("basis").GetString()!;
        if (basis != "clock-ticks" || !v.TryGetProperty("rules", out JsonElement rules) || !Values.IsPosix(rules) || !v.TryGetProperty("seconds", out JsonElement s))
        {
            throw OpFailure.Unsupported("unsupported-operand", "/value/basis",
                Invariant($"only POSIX-second amounts convert to TimeSpan; a `{basis}` amount has no fixed length in .NET ticks"));
        }
        Rational seconds = Values.ReadRational(s);
        BigInteger scaled = seconds.Numerator * TicksPerSecond;
        if (!(scaled % seconds.Denominator).IsZero)
        {
            throw OpFailure.Unsupported("out-of-native-range", "/value/seconds", Invariant($"{seconds} s is not a whole number of 100 ns ticks"));
        }
        BigInteger ticks = scaled / seconds.Denominator;
        if (ticks < TimeSpan.MinValue.Ticks || ticks > TimeSpan.MaxValue.Ticks)
        {
            throw OpFailure.Unsupported("out-of-native-range", "/value/seconds", "the amount is beyond TimeSpan's range");
        }
        return Ok(new TimeSpan((long)ticks));
    });

    // ------------------------------------------------------------------ helpers

    private readonly record struct Converted<T>(T Value, bool Lossy, Diagnostic[] Notes);

    private static Converted<T> Ok<T>(T value, bool lossy = false, Diagnostic[]? notes = null) =>
        new(value, lossy, notes ?? Array.Empty<Diagnostic>());

    private static NativeResult<T> Convert<T>(Func<Converted<T>> body)
        where T : struct
    {
        try
        {
            Converted<T> c = body();
            return new NativeResult<T>(UtvStatus.Unique, c.Value, c.Lossy, c.Notes);
        }
        catch (OpFailure failure)
        {
            return new NativeResult<T>(failure.Status, default, false, failure.Diagnostics);
        }
    }

    private static CivilRead RequireForm(JsonElement value, string form)
    {
        CivilRead c = Calendar.RequireCivil(value, "value");
        if (c.Form != form)
        {
            throw OpFailure.Unsupported("unsupported-operand", "/value/form", Invariant($"expected a `{form}`, not a `{c.Form}`"));
        }
        Values.RequireGregorian(c);
        return c;
    }

    private static int RequireNativeYear(BigInteger year)
    {
        if (year < 1 || year > 9999)
        {
            throw OpFailure.Unsupported("out-of-native-range", "/value/fields/year",
                Invariant($"year {year} is outside .NET's range 1..9999"));
        }
        return (int)year;
    }

    private static long RequireTicksOfNano(int nano)
    {
        if (nano % 100 != 0)
        {
            throw OpFailure.Unsupported("out-of-native-range", "/value/fields/nano",
                Invariant($"{nano} ns is not a multiple of .NET's 100 ns tick"));
        }
        return nano / 100;
    }

    private static long WallTicks(CivilRead c)
    {
        (BigInteger y, int m, int d) = Values.RequireDate(c);
        (int h, int mi, int s, int nano) = Values.RequireTime(c);
        return new DateTime(RequireNativeYear(y), m, d, h, mi, s, DateTimeKind.Unspecified).Ticks + RequireTicksOfNano(nano);
    }

    private static long InstantTicks(JsonElement instant)
    {
        Rational t = Values.ReadPosixInstant(instant, "value");
        BigInteger scaled = t.Numerator * TicksPerSecond;
        if (!(scaled % t.Denominator).IsZero)
        {
            throw OpFailure.Unsupported("out-of-native-range", "/value/subTick", "the instant is not a whole number of 100 ns ticks");
        }
        BigInteger ticks = scaled / t.Denominator + UnixEpochTicks;
        if (ticks < DateTime.MinValue.Ticks || ticks > DateTime.MaxValue.Ticks)
        {
            throw OpFailure.Unsupported("out-of-native-range", "/value/ticks", "the instant is outside .NET's range (years 1..9999)");
        }
        return (long)ticks;
    }

    private static DateTimeOffset Offset(long wallTicks, int offsetSeconds, string path)
    {
        if (offsetSeconds % 60 != 0 || Math.Abs(offsetSeconds) > 14 * 3600)
        {
            throw OpFailure.Unsupported("out-of-native-range", path,
                Invariant($"DateTimeOffset holds whole-minute offsets within ±14:00, not {offsetSeconds} s"));
        }
        long utc = wallTicks - offsetSeconds * TicksPerSecond;
        if (utc < DateTime.MinValue.Ticks || utc > DateTime.MaxValue.Ticks)
        {
            throw OpFailure.Unsupported("out-of-native-range", "/value", "the instant is outside .NET's range");
        }
        return new DateTimeOffset(wallTicks, TimeSpan.FromSeconds(offsetSeconds));
    }

    private static int NanoOf(long ticks) => (int)(ticks % TicksPerSecond) * 100;

    private static JsonElement FloatingOf(DateTime value) =>
        Build.DateTime(value.Year, value.Month, value.Day, value.Hour, value.Minute, value.Second, NanoOf(value.Ticks));

    private static JsonElement InstantOf(long ticksSinceEpoch) =>
        Build.Instant(MathUtil.FloorDiv(ticksSinceEpoch, TicksPerSecond), MathUtil.FloorMod(ticksSinceEpoch, TicksPerSecond) * 100);
}
