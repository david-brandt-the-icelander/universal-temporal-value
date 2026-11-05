using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text.Json;
using System.Text.Json.Nodes;
using Utv.Internal.Everyday;
using static System.FormattableString;

namespace Utv;

/// <summary>
/// One typed UTV value: <see cref="CivilDate"/>, <see cref="CivilYearMonth"/>, <see cref="CivilTime"/>,
/// <see cref="CivilDateTime"/>, <see cref="FixedOffsetDateTime"/>, <see cref="ZonedIntent"/>, <see cref="Instant"/>,
/// <see cref="Period"/>, <see cref="PosixSeconds"/>, <see cref="ElapsedSeconds"/> or an <see cref="Interval{T}"/>.
/// Each is exactly one UTV JSON value. <c>ToString()</c> gives its text form.
/// </summary>
public interface ITemporalValue
{
    /// <summary>The text form (docs/text-form.md), or a display string for second counts.</summary>
    string ToString();

    /// <summary>The UTV JSON value (the wire format).</summary>
    JsonElement ToJson();
}

/// <summary>A value an <see cref="Interval{T}"/> can hold: <see cref="CivilDate"/> or <see cref="Instant"/>.</summary>
public interface IIntervalPoint : ITemporalValue
{
    /// <summary>The interval domain: <c>civil/gregory</c> or <c>physical</c>.</summary>
    internal string Domain { get; }
}

/// <summary>
/// Entry points for values of any type: <see cref="Parse"/> and <see cref="FromJson(JsonElement)"/>.
/// (The contract's <c>Utv.Parse</c>: in C#, <c>Utv</c> is the namespace.)
/// </summary>
public static class Temporal
{
    /// <summary>
    /// Parses any text-form value (docs/text-form.md): a date, time, date-time, offset or zoned date-time, instant,
    /// period or interval. Never throws for bad text; match on the result's type
    /// (<c>value switch { Instant i =&gt; …, FixedOffsetDateTime f =&gt; …, … }</c>).
    /// </summary>
    public static Outcome<ITemporalValue> Parse(string text) => Typed.Parse<ITemporalValue>(text, "a value");

    /// <summary>Reads any UTV JSON value the typed layer holds; other values are <c>unsupported</c>.</summary>
    public static Outcome<ITemporalValue> FromJson(JsonElement json) => Typed.FromJson<ITemporalValue>(json, "a value");

    /// <summary>Reads UTV JSON text (strictly: duplicate keys, comments and the like are <c>invalid</c>).</summary>
    public static Outcome<ITemporalValue> FromJson(string json) => Typed.FromJson<ITemporalValue>(json, "a value");
}

/// <summary>The bridge between the JSON layer and the typed values.</summary>
internal static class Typed
{
    // ------------------------------------------------------------------ JSON → typed

    public static Outcome<T> FromJson<T>(JsonElement json, string expected)
        where T : ITemporalValue =>
        Catch<T>("fromJson", () => As<T>(Read(json), expected));

    public static Outcome<T> FromJson<T>(string json, string expected)
        where T : ITemporalValue
    {
        ArgumentNullException.ThrowIfNull(json);
        UtvResult structural = UtvValidator.ValidateStructural(json);
        if (structural.Status != UtvStatus.Valid)
        {
            return Failure<T>("fromJson", structural.Status, Values.WithMalformed(structural));
        }
        using JsonDocument doc = JsonDocument.Parse(json);
        return FromJson<T>(doc.RootElement, expected);
    }

    public static Outcome<T> Parse<T>(string text, string expected)
        where T : ITemporalValue
    {
        ArgumentNullException.ThrowIfNull(text);
        return Catch<T>("parseText", () => As<T>(Read(Values.ToElement(TextForm.ParseNode(text))), expected));
    }

    /// <summary>
    /// A JSON value as a typed value. Throws <see cref="OpFailure"/> for values that are invalid, or that the typed
    /// layer can't hold without losing something.
    /// </summary>
    public static ITemporalValue Read(JsonElement json)
    {
        Values.RequireStructural(json, "value");
        ITemporalValue value = ReadCore(json);
        if (!Values.JsonEquals(TextForm.Canonical(JsonNode.Parse(json.GetRawText())!), TextForm.Canonical(JsonNode.Parse(value.ToJson().GetRawText())!)))
        {
            throw OpFailure.Unsupported("unsupported-operand", "",
                Invariant($"this value has properties a {value.GetType().Name} does not hold; use the JSON layer for it"));
        }
        return value;
    }

    /// <summary>A value the JSON layer produced, as <typeparamref name="T"/>.</summary>
    public static T Trusted<T>(JsonElement json)
        where T : ITemporalValue =>
        ReadCore(json) is T t ? t : throw new InvalidOperationException(Invariant($"expected a {typeof(T).Name}: {json.GetRawText()}"));

    private static ITemporalValue ReadCore(JsonElement json)
    {
        string kind = Values.KindOf(json);
        switch (kind)
        {
            case "civil":
                return ReadCivil(Values.ReadCivil(json));
            case "instant":
                return new Instant(Values.ReadPosixInstant(json, "value"));
            case "amount":
                return ReadAmount(json.GetProperty("value"));
            case "interval":
                return ReadInterval(json.GetProperty("value"));
            default:
                throw OpFailure.Unsupported("unsupported-operand", "/kind", Invariant($"the typed layer has no type for kind `{kind}`; use the JSON layer"));
        }
    }

    private static ITemporalValue ReadCivil(CivilRead c)
    {
        Values.RequireGregorian(c);
        if (c.Form != "datetime" && c.Zone != ZoneKind.Floating)
        {
            throw OpFailure.Unsupported("unsupported-operand", "/value/zone", Invariant($"the typed layer has no type for a zoned `{c.Form}`; use the JSON layer"));
        }
        switch (c.Form)
        {
            case "date":
                (BigInteger y, int m, int d) = Values.RequireDate(c);
                return new CivilDate(y, m, d);
            case "year-month":
                (BigInteger year, int month) = Values.RequireYearMonth(c);
                return new CivilYearMonth(year, month);
            case "time":
                (int h, int mi, _, int nano) = Values.RequireTime(c);
                return new CivilTime(h, mi, c.Second, nano);
            case "datetime":
                (BigInteger dy, int dm, int dd) = Values.RequireDate(c);
                (int th, int tmi, _, int tnano) = Values.RequireTime(c);
                var dateTime = new CivilDateTime(new CivilDate(dy, dm, dd), new CivilTime(th, tmi, c.Second, tnano));
                return c.Zone switch
                {
                    ZoneKind.Floating => dateTime,
                    ZoneKind.FixedOffset => new FixedOffsetDateTime(dateTime, c.ZoneSeconds),
                    ZoneKind.Iana => new ZonedIntent(dateTime, c.ZoneId!, c.OffsetAssertion),
                    _ => throw OpFailure.Unsupported("unsupported-operand", "/value/zone/type", Invariant($"zone type `{c.ZoneType}` has no typed value")),
                };
            default:
                throw OpFailure.Unsupported("unsupported-operand", "/value/form", Invariant($"the typed layer has no type for form `{c.Form}`; use the JSON layer"));
        }
    }

    private static ITemporalValue ReadAmount(JsonElement amount)
    {
        string basis = amount.GetProperty("basis").GetString()!;
        switch (basis)
        {
            case "calendar":
                Dictionary<string, BigInteger> parts = Calendar.ReadComponents(amount);
                long Part(string name)
                {
                    if (!parts.TryGetValue(name, out BigInteger n))
                    {
                        return 0;
                    }
                    return n >= long.MinValue && n <= long.MaxValue
                        ? (long)n
                        : throw OpFailure.Unsupported("implementation-limit", "/value/components/" + name, Invariant($"{n} {name} is beyond a 64-bit Period component"));
                }
                return new Period(Part("years"), Part("months"), Part("weeks"), Part("days"), Part("hours"), Part("minutes"), Part("seconds"));
            case "clock-ticks" when amount.TryGetProperty("rules", out JsonElement rules) && Values.IsPosix(rules) && amount.TryGetProperty("seconds", out JsonElement posix):
                return new PosixSeconds(Values.ReadRational(posix));
            case "elapsed" when amount.TryGetProperty("seconds", out JsonElement si):
                return new ElapsedSeconds(Values.ReadRational(si));
            default:
                throw OpFailure.Unsupported("unsupported-operand", "/value/basis", Invariant($"the typed layer has no type for this `{basis}` amount; use the JSON layer"));
        }
    }

    private static ITemporalValue ReadInterval(JsonElement interval)
    {
        string domain = interval.GetProperty("domain").GetString()!;
        if (interval.TryGetProperty("construction", out JsonElement construction) && construction.GetString() != "start-end")
        {
            throw OpFailure.Unsupported("unsupported-operand", "/value/construction", "the typed layer holds only `start-end` intervals");
        }
        return domain switch
        {
            "civil/gregory" => ReadInterval<CivilDate>(interval, "a date"),
            "physical" => ReadInterval<Instant>(interval, "an instant"),
            _ => throw OpFailure.Unsupported("unsupported-operand", "/value/domain", Invariant($"the typed layer has no intervals over `{domain}`")),
        };
    }

    private static Interval<T> ReadInterval<T>(JsonElement interval, string expected)
        where T : class, IIntervalPoint
    {
        Bound<T> Side(string name)
        {
            JsonElement bound = interval.GetProperty(name);
            string state = bound.GetProperty("state").GetString()!;
            if (state is "unbounded" or "unknown")
            {
                return new Bound<T>(state == "unbounded" ? BoundState.Unbounded : BoundState.Unknown, null);
            }
            if (ReadCore(bound.GetProperty("point")) is not T point)
            {
                throw OpFailure.Invalid("kind-mismatch", "/value/" + name + "/point", Invariant($"this interval's domain holds {expected} at each end"));
            }
            return new Bound<T>(state == "included" ? BoundState.Included : BoundState.Excluded, point);
        }
        Bound<T> start = Side("start");
        Bound<T> end = Side("end");
        if (start.Point is null && end.Point is null)
        {
            throw OpFailure.Unsupported("unsupported-form", "/value", "an interval with no point has no domain; use the JSON layer");
        }
        if (Interval<T>.IsReversed(start, end))
        {
            throw OpFailure.Invalid("reversed-interval", "/value/end", "the interval ends before it starts");
        }
        return new Interval<T>(start, end);
    }

    private static T As<T>(ITemporalValue value, string expected)
        where T : ITemporalValue =>
        value is T t
            ? t
            : throw OpFailure.Invalid("kind-mismatch", "", Invariant($"expected {expected}, found a {value.GetType().Name} ({value})"));

    // ------------------------------------------------------------------ results → typed

    /// <summary>The answer of an operation that can't fail for valid inputs.</summary>
    public static T Total<T>(UtvResult result, Func<JsonElement, T> read) =>
        result.IsUnique
            ? read(result.Value)
            : throw new InvalidOperationException(Invariant($"`{result.Operation}` gave `{result.Status.ToWireString()}` for valid typed inputs: {string.Join("; ", result.Diagnostics)}"));

    /// <summary>A JSON-layer result as an outcome, each candidate read with <paramref name="read"/>.</summary>
    public static Outcome<T> ToOutcome<T>(UtvResult result, Func<JsonElement, T> read)
    {
        Diagnostics why = Diagnostics.Of(result.Diagnostics);
        return result.Status switch
        {
            UtvStatus.Unique => new Outcome<T>.Unique(read(result.Value), why) { Operation = result.Operation },
            UtvStatus.Multiple => new Outcome<T>.Multiple(result.Candidates.Select(read).ToArray(), why) { Operation = result.Operation },
            _ => Failure<T>(result.Operation, result.Status, why),
        };
    }

    public static Outcome<T> ToOutcome<T>(UtvResult result)
        where T : ITemporalValue => ToOutcome(result, Trusted<T>);

    /// <summary>A .NET interop result as an outcome.</summary>
    public static Outcome<T> ToOutcome<T>(NativeResult<T> result, string operation)
        where T : struct =>
        result.TryGetValue(out T value)
            ? new Outcome<T>.Unique(value, Diagnostics.Of(result.Diagnostics)) { Operation = operation }
            : Failure<T>(operation, result.Status, result.Diagnostics);

    public static Outcome<T> Failure<T>(string operation, UtvStatus status, IEnumerable<Diagnostic> diagnostics)
    {
        Diagnostics why = Diagnostics.Of(diagnostics);
        return status switch
        {
            UtvStatus.None => new Outcome<T>.None(why) { Operation = operation },
            UtvStatus.Invalid => new Outcome<T>.Invalid(why) { Operation = operation },
            _ => new Outcome<T>.Unsupported(why) { Operation = operation },
        };
    }

    private static Outcome<T> Catch<T>(string operation, Func<T> body)
    {
        try
        {
            return new Outcome<T>.Unique(body()) { Operation = operation };
        }
        catch (OpFailure failure)
        {
            return Failure<T>(operation, failure.Status, failure.Diagnostics);
        }
    }

    // ------------------------------------------------------------------ typed → text

    /// <summary>The text form, or, for a value that has none, a compact JSON text.</summary>
    public static string Text(ITemporalValue value)
    {
        JsonElement json = value.ToJson();
        UtvResult text = TextForm.Format(json);
        return text.IsUnique ? text.GetString() : json.GetRawText();
    }

    /// <summary>For constructors: the outcome's value, or an <see cref="ArgumentOutOfRangeException"/> naming the field.</summary>
    public static T OrThrow<T>(Outcome<T> outcome)
    {
        if (outcome.TryGetUnique(out T? value))
        {
            return value;
        }
        Diagnostic? first = outcome.Diagnostics.Count > 0 ? outcome.Diagnostics[0] : null;
        string field = first?.Path.Split('/')[^1] switch
        {
            "nano" => "nanosecond",
            { Length: > 0 } name => name,
            _ => "value",
        };
        throw new ArgumentOutOfRangeException(field, first?.Message ?? outcome.ToString());
    }
}
