using System;
using System.Diagnostics;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using Utv.Internal.Everyday;
using static System.FormattableString;

namespace Utv;

/// <summary>
/// A wall time at a fixed UTC offset: <c>2026-11-05T14:30+01:00</c>. It names exactly one instant without any zone
/// rules, so it converts with <see cref="ToInstant"/> and compares by instant.
/// </summary>
/// <remarks>Equality is representation equality: <c>14:30+01:00</c> and <c>13:30+00:00</c> are different values
/// that compare as equal (<see cref="CompareTo"/> returns 0).</remarks>
[DebuggerDisplay("FixedOffsetDateTime({ToString(),nq})")]
public sealed record FixedOffsetDateTime : ITemporalValue, IComparable<FixedOffsetDateTime>
{
    internal const int MaxOffsetSeconds = 18 * 3600;

    internal FixedOffsetDateTime(CivilDateTime dateTime, int offsetSeconds)
    {
        DateTime = dateTime;
        OffsetSeconds = offsetSeconds;
    }

    /// <summary>The wall time.</summary>
    public CivilDateTime DateTime { get; }

    /// <summary>The offset from UTC in seconds, east positive: 3600 is +01:00.</summary>
    public int OffsetSeconds { get; }

    /// <summary>Parses text such as <c>2026-11-05T14:30+01:00</c>.</summary>
    public static Outcome<FixedOffsetDateTime> Parse(string text) => Typed.Parse<FixedOffsetDateTime>(text, "a date-time with an offset");

    /// <summary>Reads a UTV JSON <c>datetime</c> with a <c>fixed-offset</c> zone.</summary>
    public static Outcome<FixedOffsetDateTime> FromJson(JsonElement json) => Typed.FromJson<FixedOffsetDateTime>(json, "a date-time with an offset");

    /// <summary>Reads UTV JSON text.</summary>
    public static Outcome<FixedOffsetDateTime> FromJson(string json) => Typed.FromJson<FixedOffsetDateTime>(json, "a date-time with an offset");

    /// <summary>A .NET <see cref="DateTimeOffset"/> as the wall time and offset it shows (lossless).</summary>
    public static FixedOffsetDateTime FromDateTimeOffset(DateTimeOffset value) =>
        Typed.Trusted<FixedOffsetDateTime>(Interop.FromDateTimeOffset(value, DateTimeOffsetIntent.OffsetDateTime));

    /// <summary>The instant this names (exact; no rules needed).</summary>
    public Instant ToInstant() => Typed.Total(Zones.Resolve(ToJson(), ZoneRules.Empty), Typed.Trusted<Instant>);

    /// <summary>As a .NET <see cref="DateTimeOffset"/>: <c>unsupported</c> (<c>out-of-native-range</c>) beyond its years, ticks or ±14:00.</summary>
    public Outcome<DateTimeOffset> ToDateTimeOffset() => Typed.ToOutcome(Interop.ToDateTimeOffset(ToJson()), "toDateTimeOffset");

    /// <inheritdoc/>
    public JsonElement ToJson()
    {
        JsonObject node = DateTime.Node();
        JsonObject value = node["value"]!.AsObject();
        value["zone"] = new JsonObject { ["type"] = "fixed-offset", ["seconds"] = OffsetSeconds };
        value["zoneIntent"] = "fixed-offset";
        return Values.ToElement(node);
    }

    /// <summary>The text form, for example <c>2026-11-05T14:30+01:00</c>.</summary>
    public override string ToString() => Typed.Text(this);

    /// <summary>Order of the instants named.</summary>
    public int CompareTo(FixedOffsetDateTime? other) => other is null ? 1 : (int)TypedWire.ParseOrdering(Calendar.Compare(ToJson(), other.ToJson()).GetString());

    /// <summary>Earlier instant.</summary>
    public static bool operator <(FixedOffsetDateTime left, FixedOffsetDateTime right) => left.CompareTo(right) < 0;

    /// <summary>Later instant.</summary>
    public static bool operator >(FixedOffsetDateTime left, FixedOffsetDateTime right) => left.CompareTo(right) > 0;

    /// <summary>Earlier or the same instant.</summary>
    public static bool operator <=(FixedOffsetDateTime left, FixedOffsetDateTime right) => left.CompareTo(right) <= 0;

    /// <summary>Later or the same instant.</summary>
    public static bool operator >=(FixedOffsetDateTime left, FixedOffsetDateTime right) => left.CompareTo(right) >= 0;

    internal static void CheckOffset(int offsetSeconds)
    {
        if (Math.Abs(offsetSeconds) > MaxOffsetSeconds)
        {
            throw new ArgumentOutOfRangeException(nameof(offsetSeconds), offsetSeconds, "an offset is at most ±18:00 (64800 s)");
        }
    }
}

/// <summary>
/// A wall time in a named IANA zone, following that zone's rules: <c>2027-06-01T09:00[Europe/Paris]</c>. It is an
/// intention ("09:00 in Paris"), not an instant: resolving it needs rules (<see cref="Resolve(TemporalContext, ResolutionPolicy?)"/>),
/// and the answer can be none (a DST gap) or several (an overlap).
/// </summary>
/// <remarks>
/// It has no comparison operators, because its order depends on zone rules: use
/// <see cref="TemporalContext.Compare(ZonedIntent, ZonedIntent)"/>. An asserted offset
/// (<c>2027-06-01T09:00+02:00[Europe/Paris]</c>, as <see cref="Instant.InZone"/> writes it) picks one side of an overlap
/// when resolving, and makes the value <c>invalid</c> if the rules disagree.
/// </remarks>
[DebuggerDisplay("ZonedIntent({ToString(),nq})")]
public sealed record ZonedIntent : ITemporalValue
{
    internal ZonedIntent(CivilDateTime dateTime, string zoneId, int? assertedOffsetSeconds)
    {
        DateTime = dateTime;
        ZoneId = zoneId;
        AssertedOffsetSeconds = assertedOffsetSeconds;
    }

    /// <summary>The wall time.</summary>
    public CivilDateTime DateTime { get; }

    /// <summary>The IANA zone, for example <c>Europe/Paris</c>.</summary>
    public string ZoneId { get; }

    /// <summary>The offset the value asserts (seconds east of UTC), or null when it asserts none.</summary>
    public int? AssertedOffsetSeconds { get; }

    /// <summary>Parses text such as <c>2027-06-01T09:00[Europe/Paris]</c> or <c>2027-06-01T09:00+02:00[Europe/Paris]</c>.</summary>
    public static Outcome<ZonedIntent> Parse(string text) => Typed.Parse<ZonedIntent>(text, "a zoned date-time");

    /// <summary>Reads a UTV JSON <c>datetime</c> with an IANA zone and <c>follow-zone-id</c>.</summary>
    public static Outcome<ZonedIntent> FromJson(JsonElement json) => Typed.FromJson<ZonedIntent>(json, "a zoned date-time");

    /// <summary>Reads UTV JSON text.</summary>
    public static Outcome<ZonedIntent> FromJson(string json) => Typed.FromJson<ZonedIntent>(json, "a zoned date-time");

    /// <summary>
    /// <paramref name="days"/> days later on the wall clock, in the same zone: 09:00 in Paris stays 09:00 in Paris,
    /// whatever the DST rules. An asserted offset is dropped (it may no longer hold). Never fails; the same holds for
    /// <see cref="AddWeeks"/>, <see cref="AddHours"/>, <see cref="AddMinutes"/> and <see cref="AddSeconds"/>.
    /// </summary>
    public ZonedIntent AddDays(long days) => Add(Build.Period(days: days));

    /// <summary><paramref name="weeks"/> weeks later on the wall clock.</summary>
    public ZonedIntent AddWeeks(long weeks) => Add(Build.Period(weeks: weeks));

    /// <summary><paramref name="hours"/> hours later on the wall clock (not elapsed time: resolve first for that).</summary>
    public ZonedIntent AddHours(long hours) => Add(Build.Period(hours: hours));

    /// <summary><paramref name="minutes"/> minutes later on the wall clock.</summary>
    public ZonedIntent AddMinutes(long minutes) => Add(Build.Period(minutes: minutes));

    /// <summary><paramref name="seconds"/> seconds later on the wall clock.</summary>
    public ZonedIntent AddSeconds(long seconds) => Add(Build.Period(seconds: seconds));

    /// <summary><paramref name="months"/> months later: <c>none</c> (<c>month-end</c>) if the day doesn't exist.</summary>
    public Outcome<ZonedIntent> AddMonths(long months) => Typed.ToOutcome<ZonedIntent>(Calendar.Add(ToJson(), Build.Period(months: months)));

    /// <summary>
    /// The instant(s) this wall time names under the rules of <paramref name="context"/>. With the default
    /// <see cref="ResolutionPolicy.Strict"/>: <c>none</c> (<c>gap</c>) when the clocks skip it, <c>multiple</c>
    /// (<c>ambiguous</c>) when it happens twice, <c>unsupported</c> for zones or dates outside the rules.
    /// </summary>
    public Outcome<Instant> Resolve(TemporalContext context, ResolutionPolicy? policy = null)
    {
        ArgumentNullException.ThrowIfNull(context);
        return context.Resolve(this, policy);
    }

    /// <summary>Resolves with a gap policy, for example <see cref="Gap.ShiftForward"/> (overlaps still rejected).</summary>
    public Outcome<Instant> Resolve(TemporalContext context, Gap gap) => Resolve(context, new ResolutionPolicy(gap, Overlap.Reject));

    /// <summary>Resolves with an overlap policy, for example <see cref="Overlap.Earliest"/> (gaps still rejected).</summary>
    public Outcome<Instant> Resolve(TemporalContext context, Overlap overlap) => Resolve(context, new ResolutionPolicy(Gap.Reject, overlap));

    /// <inheritdoc/>
    public JsonElement ToJson()
    {
        JsonObject node = DateTime.Node();
        JsonObject value = node["value"]!.AsObject();
        value["zone"] = new JsonObject { ["type"] = "iana", ["id"] = ZoneId };
        value["zoneIntent"] = "follow-zone-id";
        if (AssertedOffsetSeconds is int asserted)
        {
            value["offsetAssertion"] = asserted;
        }
        return Values.ToElement(node);
    }

    /// <summary>The text form, for example <c>2027-06-01T09:00[Europe/Paris]</c>.</summary>
    public override string ToString() => Typed.Text(this);

    private ZonedIntent Add(JsonElement period) => Typed.Total(Calendar.Add(ToJson(), period), Typed.Trusted<ZonedIntent>);
}

/// <summary>
/// Zone rules held once, so that operations needing them don't each take them: <c>TemporalContext.With(rules)</c>.
/// There is no default or ambient context, and nothing reads the host's time zone database.
/// </summary>
public sealed class TemporalContext
{
    private TemporalContext(ZoneRules rules)
    {
        Rules = rules;
    }

    /// <summary>The zone rules.</summary>
    public ZoneRules Rules { get; }

    /// <summary>A context with these rules, for example <c>TemporalContext.With(ZoneRules.Load(path))</c>.</summary>
    public static TemporalContext With(ZoneRules rules)
    {
        ArgumentNullException.ThrowIfNull(rules);
        return new TemporalContext(rules);
    }

    /// <summary>The instant(s) a zoned wall time names; see <see cref="ZonedIntent.Resolve(TemporalContext, ResolutionPolicy?)"/>.</summary>
    public Outcome<Instant> Resolve(ZonedIntent zoned, ResolutionPolicy? policy = null)
    {
        ArgumentNullException.ThrowIfNull(zoned);
        return Typed.ToOutcome<Instant>(Zones.Resolve(zoned.ToJson(), Rules, policy ?? ResolutionPolicy.Strict));
    }

    /// <summary>
    /// The wall time of <paramref name="instant"/> in zone <paramref name="zoneId"/>, with the offset in force asserted:
    /// <c>2027-06-01T03:00:00-04:00[America/New_York]</c>. <c>unsupported</c> for a zone or instant outside the rules.
    /// </summary>
    public Outcome<ZonedIntent> InZone(Instant instant, string zoneId)
    {
        ArgumentNullException.ThrowIfNull(instant);
        ArgumentNullException.ThrowIfNull(zoneId);
        return Typed.ToOutcome<ZonedIntent>(Zones.ToZone(instant.ToJson(), zoneId, Rules));
    }

    /// <summary>
    /// Orders two zoned wall times: both are resolved strictly, then compared as instants. <c>multiple</c>
    /// (<c>ambiguous</c>) when the order depends on which side of an overlap is meant; <c>none</c> (<c>gap</c>) when
    /// a wall time doesn't exist; <c>unsupported</c> outside the rules.
    /// </summary>
    public Outcome<Ordering> Compare(ZonedIntent a, ZonedIntent b)
    {
        ArgumentNullException.ThrowIfNull(a);
        ArgumentNullException.ThrowIfNull(b);
        return Compare(Resolve(a), Resolve(b));
    }

    /// <summary>Orders a zoned wall time and an instant (the wall time is resolved strictly).</summary>
    public Outcome<Ordering> Compare(ZonedIntent a, Instant b)
    {
        ArgumentNullException.ThrowIfNull(a);
        ArgumentNullException.ThrowIfNull(b);
        return Compare(Resolve(a), new Outcome<Instant>.Unique(b));
    }

    /// <summary>Orders an instant and a zoned wall time (the wall time is resolved strictly).</summary>
    public Outcome<Ordering> Compare(Instant a, ZonedIntent b)
    {
        ArgumentNullException.ThrowIfNull(a);
        ArgumentNullException.ThrowIfNull(b);
        return Compare(new Outcome<Instant>.Unique(a), Resolve(b));
    }

    private static Outcome<Ordering> Compare(Outcome<Instant> a, Outcome<Instant> b)
    {
        foreach (Outcome<Instant> side in new[] { a, b })
        {
            if (side.Status is not (UtvStatus.Unique or UtvStatus.Multiple))
            {
                return Typed.Failure<Ordering>("compare", side.Status, side.Diagnostics);
            }
        }
        Instant[] xs = Candidates(a);
        Instant[] ys = Candidates(b);
        Ordering[] orders = xs.SelectMany(x => ys, (x, y) => (Ordering)Math.Sign(x.CompareTo(y))).Distinct().Order().ToArray();
        return orders.Length == 1
            ? new Outcome<Ordering>.Unique(orders[0]) { Operation = "compare" }
            : new Outcome<Ordering>.Multiple(orders, new Diagnostics(new[]
            {
                new Diagnostic("ambiguous", DiagnosticSeverity.Warning, "",
                    Invariant($"the order depends on which side of a DST overlap is meant: {string.Join(" or ", orders.Select(o => o.ToWireString()))}")),
            })) { Operation = "compare" };

        static Instant[] Candidates(Outcome<Instant> o) => o switch
        {
            Outcome<Instant>.Unique(var one) => new[] { one },
            Outcome<Instant>.Multiple(var many) => many.ToArray(),
            _ => Array.Empty<Instant>(),
        };
    }
}
