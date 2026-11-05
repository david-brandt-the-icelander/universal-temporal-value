using System;
using System.Globalization;
using System.Numerics;
using System.Text.Json;
using System.Text.Json.Nodes;
using Utv.Internal.Everyday;

namespace Utv;

/// <summary>
/// Builders for the canonical UTV values of the everyday API (docs/everyday-api.md, "Canonical shapes").
/// Each returns a UTV JSON value as a <see cref="JsonElement"/>.
/// </summary>
/// <remarks>
/// Builders produce shapes; they do not judge them. <c>Build.Date(2026, 2, 30)</c> is a well-formed value
/// that names no real day: <see cref="Calendar.Fields"/> or <see cref="Iso.FormatIso"/> on it give <c>invalid</c>.
/// Builders throw only for misuse, such as passing a non-civil value to <see cref="InZone"/>.
/// </remarks>
public static class Build
{
    /// <summary>A proleptic Gregorian <c>date</c>.</summary>
    public static JsonElement Date(long year, int month, int day) =>
        Values.ToElement(Values.Civil("date", Values.DateFields(year, month, day)));

    /// <summary>A proleptic Gregorian <c>year-month</c>.</summary>
    public static JsonElement YearMonth(long year, int month) =>
        Values.ToElement(Values.Civil("year-month", new JsonObject { ["year"] = Values.FormatYear(year), ["month"] = month }));

    /// <summary>A <c>time</c> of day without seconds, for example 09:30.</summary>
    public static JsonElement Time(int hour, int minute) => TimeOf(hour, minute, null, null);

    /// <summary>A <c>time</c> of day with seconds.</summary>
    public static JsonElement Time(int hour, int minute, int second) => TimeOf(hour, minute, second, null);

    /// <summary>A <c>time</c> of day with seconds and nanoseconds (<c>nano</c> is omitted when 0).</summary>
    public static JsonElement Time(int hour, int minute, int second, int nano) => TimeOf(hour, minute, second, nano);

    /// <summary>An unzoned ("floating") local <c>datetime</c> without seconds.</summary>
    public static JsonElement DateTime(long year, int month, int day, int hour, int minute) =>
        DateTimeOf(year, month, day, hour, minute, null, null);

    /// <summary>An unzoned ("floating") local <c>datetime</c> with seconds.</summary>
    public static JsonElement DateTime(long year, int month, int day, int hour, int minute, int second) =>
        DateTimeOf(year, month, day, hour, minute, second, null);

    /// <summary>An unzoned ("floating") local <c>datetime</c> with seconds and nanoseconds (<c>nano</c> is omitted when 0).</summary>
    public static JsonElement DateTime(long year, int month, int day, int hour, int minute, int second, int nano) =>
        DateTimeOf(year, month, day, hour, minute, second, nano);

    /// <summary>
    /// The same civil value, following the IANA zone <paramref name="zoneId"/>: adds
    /// <c>"zone":{"type":"iana","id":…}</c> and <c>"zoneIntent":"follow-zone-id"</c>.
    /// Any previous zone and <c>offsetAssertion</c> are replaced.
    /// </summary>
    /// <exception cref="ArgumentException"><paramref name="dateTime"/> is not a <c>civil</c> value, or the zone id is empty.</exception>
    public static JsonElement InZone(JsonElement dateTime, string zoneId)
    {
        ArgumentException.ThrowIfNullOrEmpty(zoneId);
        return Rezone(dateTime, new JsonObject { ["type"] = "iana", ["id"] = zoneId }, "follow-zone-id");
    }

    /// <summary>
    /// The same civil value at a fixed UTC offset in seconds east of Greenwich: adds
    /// <c>"zone":{"type":"fixed-offset","seconds":…}</c> and <c>"zoneIntent":"fixed-offset"</c>.
    /// </summary>
    /// <exception cref="ArgumentException"><paramref name="dateTime"/> is not a <c>civil</c> value.</exception>
    public static JsonElement WithOffset(JsonElement dateTime, int offsetSeconds) =>
        Rezone(dateTime, new JsonObject { ["type"] = "fixed-offset", ["seconds"] = offsetSeconds }, "fixed-offset");

    /// <summary>
    /// A POSIX <c>instant</c>: <paramref name="epochSeconds"/> since 1970-01-01T00:00:00Z plus
    /// <paramref name="nano"/> nanoseconds (kept as a reduced <c>subTick</c> fraction).
    /// </summary>
    public static JsonElement Instant(long epochSeconds, long nano = 0) =>
        Values.ToElement(Values.Instant(Rational.FromSecondsAndNano(epochSeconds, nano)));

    /// <summary>
    /// A calendar <c>amount</c> (a period). Only non-zero components are written; each may be negative.
    /// Use named arguments: <c>Build.Period(months: 1)</c>.
    /// </summary>
    public static JsonElement Period(long years = 0, long months = 0, long weeks = 0, long days = 0, long hours = 0, long minutes = 0, long seconds = 0)
    {
        var components = new JsonObject();
        void Put(string name, long n)
        {
            if (n != 0)
            {
                components[name] = n.ToString(CultureInfo.InvariantCulture);
            }
        }
        Put("years", years);
        Put("months", months);
        Put("weeks", weeks);
        Put("days", days);
        Put("hours", hours);
        Put("minutes", minutes);
        Put("seconds", seconds);
        return Values.ToElement(new JsonObject
        {
            ["schema"] = UtvValidator.SpecVersion,
            ["kind"] = "amount",
            ["value"] = new JsonObject { ["basis"] = "calendar", ["components"] = components },
        });
    }

    /// <summary>
    /// An amount of POSIX seconds (<c>basis: clock-ticks</c>). POSIX days are always 86400 of these;
    /// they are not SI seconds across a leap second.
    /// </summary>
    public static JsonElement PosixSeconds(long seconds) =>
        Values.ToElement(Values.PosixSeconds(Rational.FromInteger(seconds)));

    /// <summary>An exact fractional amount of POSIX seconds, <paramref name="numerator"/>/<paramref name="denominator"/>, stored reduced.</summary>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="denominator"/> is zero.</exception>
    public static JsonElement PosixSeconds(BigInteger numerator, BigInteger denominator) =>
        Values.ToElement(Values.PosixSeconds(new Rational(numerator, denominator)));

    /// <summary>
    /// An interval between two points of the same domain, half-open <c>[start, end)</c> by default.
    /// The domain is <c>civil/gregory</c> for civil points and <c>physical</c> for instants.
    /// </summary>
    /// <exception cref="ArgumentException">A point is neither a civil value nor an instant, or the two differ.</exception>
    public static JsonElement Interval(JsonElement start, JsonElement end, bool startIncluded = true, bool endIncluded = false) =>
        Interval(
            startIncluded ? IntervalBound.Included(start) : IntervalBound.Excluded(start),
            endIncluded ? IntervalBound.Included(end) : IntervalBound.Excluded(end));

    /// <summary>
    /// An interval from two bounds, each of which may be <see cref="IntervalBound.Unbounded"/> (open-ended)
    /// or <see cref="IntervalBound.Unknown"/> (exists, but not known).
    /// </summary>
    /// <exception cref="ArgumentException">
    /// Neither bound holds a point (the domain cannot be inferred), a point is neither a civil value nor an
    /// instant, or the two points are of different domains.
    /// </exception>
    public static JsonElement Interval(IntervalBound start, IntervalBound end)
    {
        string? domain = DomainOf(start, nameof(start));
        string? endDomain = DomainOf(end, nameof(end));
        if (domain is not null && endDomain is not null && domain != endDomain)
        {
            throw new ArgumentException("both points must be civil values or both instants", nameof(end));
        }
        domain ??= endDomain ?? throw new ArgumentException("at least one bound must hold a point, so the domain is known", nameof(start));
        return Values.ToElement(new JsonObject
        {
            ["schema"] = UtvValidator.SpecVersion,
            ["kind"] = "interval",
            ["value"] = new JsonObject { ["domain"] = domain, ["start"] = start.ToNode(), ["end"] = end.ToNode() },
        });
    }

    private static string? DomainOf(IntervalBound bound, string parameter)
    {
        if (bound.Point is not JsonElement point)
        {
            return null;
        }
        string? kind = point.ValueKind == JsonValueKind.Object && point.TryGetProperty("kind", out JsonElement k) ? k.GetString() : null;
        return kind switch
        {
            "civil" => "civil/gregory",
            "instant" => "physical",
            _ => throw new ArgumentException("an interval point must be a civil value or an instant", parameter),
        };
    }

    private static JsonElement TimeOf(int hour, int minute, int? second, int? nano) =>
        Values.ToElement(Values.Civil("time", Values.AddTime(new JsonObject(), hour, minute, second, nano)));

    private static JsonElement DateTimeOf(long year, int month, int day, int hour, int minute, int? second, int? nano) =>
        Values.ToElement(Values.Civil("datetime", Values.AddTime(Values.DateFields(year, month, day), hour, minute, second, nano)));

    private static JsonElement Rezone(JsonElement civil, JsonObject zone, string intent)
    {
        if (civil.ValueKind != JsonValueKind.Object
            || !civil.TryGetProperty("kind", out JsonElement kind) || kind.GetString() != "civil"
            || !civil.TryGetProperty("value", out JsonElement value) || value.ValueKind != JsonValueKind.Object)
        {
            throw new ArgumentException("expected a UTV civil value", nameof(civil));
        }
        JsonObject copy = Values.Mutable(civil);
        JsonObject v = copy["value"]!.AsObject();
        v.Remove("offsetAssertion");
        v["zone"] = zone;
        v["zoneIntent"] = intent;
        return Values.ToElement(copy);
    }
}

/// <summary>One end of an interval: a point that is included or excluded, or an unbounded or unknown end.</summary>
public readonly struct IntervalBound : IEquatable<IntervalBound>
{
    private IntervalBound(string state, JsonElement? point)
    {
        _state = state;
        Point = point?.Clone();
    }

    // Null only in default(IntervalBound), which is unbounded.
    private readonly string? _state;

    /// <summary><c>included</c>, <c>excluded</c>, <c>unbounded</c> or <c>unknown</c>.</summary>
    public string State => _state ?? "unbounded";

    /// <summary>The point, for <c>included</c> and <c>excluded</c> bounds.</summary>
    public JsonElement? Point { get; }

    /// <summary>The bound includes <paramref name="point"/> (≤).</summary>
    public static IntervalBound Included(JsonElement point) => new("included", point);

    /// <summary>The bound excludes <paramref name="point"/> (&lt;).</summary>
    public static IntervalBound Excluded(JsonElement point) => new("excluded", point);

    /// <summary>No bound: the interval is open-ended on this side.</summary>
    public static IntervalBound Unbounded => new("unbounded", null);

    /// <summary>A bound exists but is not known, for example an end date not yet decided.</summary>
    public static IntervalBound Unknown => new("unknown", null);

    /// <inheritdoc/>
    public bool Equals(IntervalBound other) =>
        State == other.State
        && (Point is null) == (other.Point is null)
        && (Point is null || Point.Value.GetRawText() == other.Point!.Value.GetRawText());

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is IntervalBound b && Equals(b);

    /// <inheritdoc/>
    public override int GetHashCode() => HashCode.Combine(State, Point?.GetRawText());

    /// <summary>Equality by state and point text.</summary>
    public static bool operator ==(IntervalBound left, IntervalBound right) => left.Equals(right);

    /// <summary>Inequality by state and point text.</summary>
    public static bool operator !=(IntervalBound left, IntervalBound right) => !left.Equals(right);

    internal JsonObject ToNode()
    {
        var node = new JsonObject { ["state"] = State };
        if (Point is JsonElement p)
        {
            node["point"] = Values.Mutable(p);
        }
        return node;
    }
}
