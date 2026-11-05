using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Numerics;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using static System.FormattableString;

namespace Utv.Internal.Everyday;

/// <summary>
/// A semantic outcome raised inside an operation and turned into a result at the operation boundary.
/// Never escapes the library.
/// </summary>
internal sealed class OpFailure : Exception
{
    public OpFailure(UtvStatus status, IReadOnlyList<Diagnostic> diagnostics)
        : base(diagnostics.Count > 0 ? diagnostics[0].Message : status.ToWireString())
    {
        Status = status;
        Diagnostics = diagnostics;
    }

    public UtvStatus Status { get; }

    public IReadOnlyList<Diagnostic> Diagnostics { get; }

    public static OpFailure Invalid(string code, string path, string message) =>
        new(UtvStatus.Invalid, new[] { Diagnostic.Error(code, path, message) });

    public static OpFailure Unsupported(string code, string path, string message) =>
        new(UtvStatus.Unsupported, new[] { Diagnostic.Error(code, path, message) });
}

/// <summary>Kinds of zone a civil value can carry.</summary>
internal enum ZoneKind
{
    Floating,
    Iana,
    FixedOffset,
    Other,
}

/// <summary>A structurally valid <c>civil</c> value, read for the everyday operations.</summary>
internal sealed class CivilRead
{
    public required JsonElement Envelope { get; init; }
    public required string Form { get; init; }
    public required bool Gregorian { get; init; }
    public required string CalendarText { get; init; }
    public BigInteger? Year { get; init; }
    public int? Month { get; init; }
    public int? Day { get; init; }
    public int? Hour { get; init; }
    public int? Minute { get; init; }
    public int? Second { get; init; }
    public int? Nano { get; init; }
    public required ZoneKind Zone { get; init; }
    public string? ZoneType { get; init; }
    public string? ZoneId { get; init; }
    public int ZoneSeconds { get; init; }
    public int? OffsetAssertion { get; init; }
    public JsonElement? ResolutionPolicy { get; init; }
}

/// <summary>Reading and writing UTV JSON values for the everyday operations.</summary>
internal static class Values
{
    public const string PosixSystem = "utv.timescale";
    public const string PosixId = "unix-posix";
    public const string PosixVersion = "1";
    public const int MaxOffsetSeconds = 18 * 3600;

    private static readonly JsonWriterOptions WriterOptions = new() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    // ---------------------------------------------------------------- reading

    /// <summary>Checks an argument structurally, using the 0.1 validator.</summary>
    public static void RequireStructural(JsonElement value, string argument)
    {
        if (value.ValueKind != JsonValueKind.Object)
        {
            throw OpFailure.Invalid("malformed-value", "", Invariant($"{argument} must be a UTV JSON object"));
        }
        UtvResult structural = UtvValidator.ValidateStructural(value.GetRawText());
        if (structural.Status != UtvStatus.Valid)
        {
            throw new OpFailure(structural.Status, WithMalformed(structural));
        }
    }

    /// <summary>
    /// Structural diagnostics, led by <c>malformed-value</c> when the JSON has the wrong shape (library-api.md §5);
    /// an unsupported specification version is not malformed.
    /// </summary>
    public static IReadOnlyList<Diagnostic> WithMalformed(UtvResult structural)
    {
        if (structural.Status != UtvStatus.Invalid)
        {
            return structural.Diagnostics;
        }
        var all = new List<Diagnostic> { Diagnostic.Error("malformed-value", "", "the JSON is not a well-formed UTV value") };
        all.AddRange(structural.Diagnostics);
        return all;
    }

    public static string KindOf(JsonElement value) => value.GetProperty("kind").GetString()!;

    public static CivilRead ReadCivil(JsonElement envelope)
    {
        JsonElement value = envelope.GetProperty("value");
        JsonElement calendar = value.GetProperty("calendar");
        string calendarId = calendar.GetProperty("id").GetString()!;
        string chronology = calendar.GetProperty("chronology").GetString()!;
        JsonElement fields = value.GetProperty("fields");

        ZoneKind zoneKind = ZoneKind.Floating;
        string? zoneType = null;
        string? zoneId = null;
        int zoneSeconds = 0;
        if (value.TryGetProperty("zone", out JsonElement zone))
        {
            zoneType = zone.GetProperty("type").GetString()!;
            switch (zoneType)
            {
                case "floating":
                    break;
                case "iana":
                    zoneKind = ZoneKind.Iana;
                    zoneId = zone.GetProperty("id").GetString();
                    break;
                case "fixed-offset":
                    zoneKind = ZoneKind.FixedOffset;
                    zoneSeconds = IntOrFail(zone.GetProperty("seconds"), "/value/zone/seconds");
                    if (Math.Abs(zoneSeconds) > MaxOffsetSeconds)
                    {
                        throw OpFailure.Invalid("offset-out-of-range", "/value/zone/seconds",
                            Invariant($"offset {zoneSeconds} s is beyond ±18:00"));
                    }
                    break;
                default:
                    zoneKind = ZoneKind.Other;
                    break;
            }
        }

        return new CivilRead
        {
            Envelope = envelope,
            Form = value.GetProperty("form").GetString()!,
            Gregorian = calendarId == "gregory" && chronology == "proleptic",
            CalendarText = Invariant($"`{calendarId}` ({chronology})"),
            Year = fields.TryGetProperty("year", out JsonElement y)
                ? BigInteger.Parse(y.GetString()!, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture)
                : null,
            Month = OptionalInt(fields, "month"),
            Day = OptionalInt(fields, "day"),
            Hour = OptionalInt(fields, "hour"),
            Minute = OptionalInt(fields, "minute"),
            Second = OptionalInt(fields, "second"),
            Nano = OptionalInt(fields, "nano"),
            Zone = zoneKind,
            ZoneType = zoneType,
            ZoneId = zoneId,
            ZoneSeconds = zoneSeconds,
            OffsetAssertion = value.TryGetProperty("offsetAssertion", out JsonElement oa)
                ? IntOrFail(oa, "/value/offsetAssertion")
                : null,
            ResolutionPolicy = value.TryGetProperty("resolutionPolicy", out JsonElement rp) ? rp : null,
        };
    }

    private static int? OptionalInt(JsonElement fields, string name) =>
        fields.TryGetProperty(name, out JsonElement e) ? IntOrFail(e, "/value/fields/" + name) : null;

    /// <summary>A JSON integer (including forms such as <c>2.0</c>) that fits in an <see cref="int"/>.</summary>
    public static int IntOrFail(JsonElement e, string path)
    {
        if (e.ValueKind == JsonValueKind.Number)
        {
            if (e.TryGetInt32(out int i))
            {
                return i;
            }
            if (e.TryGetDecimal(out decimal d) && decimal.Truncate(d) == d && d >= int.MinValue && d <= int.MaxValue)
            {
                return (int)d;
            }
        }
        throw OpFailure.Invalid("malformed-value", path, Invariant($"{e.GetRawText()} is not an integer in range"));
    }

    public static void RequireGregorian(CivilRead c, string path = "/value/calendar")
    {
        if (!c.Gregorian)
        {
            throw OpFailure.Unsupported("unsupported-calendar", path,
                Invariant($"calendar {c.CalendarText} is not implemented; only proleptic `gregory` is"));
        }
    }

    /// <summary>Year, month and day are present and name a real proleptic Gregorian date.</summary>
    public static (BigInteger Year, int Month, int Day) RequireDate(CivilRead c)
    {
        if (c.Year is null || c.Month is null || c.Day is null)
        {
            throw OpFailure.Invalid("malformed-value", "/value/fields",
                Invariant($"a `{c.Form}` needs `year`, `month` and `day`"));
        }
        RequireMonth(c.Month.Value);
        int limit = GregorianCivil.DaysInMonth(c.Year.Value, c.Month.Value);
        if (c.Day < 1 || c.Day > limit)
        {
            throw OpFailure.Invalid("day-out-of-range", "/value/fields/day",
                Invariant($"day {c.Day} is not in 1..{limit} for {FormatYear(c.Year.Value)}-{c.Month.Value:00}"));
        }
        return (c.Year.Value, c.Month.Value, c.Day.Value);
    }

    public static (BigInteger Year, int Month) RequireYearMonth(CivilRead c)
    {
        if (c.Year is null || c.Month is null)
        {
            throw OpFailure.Invalid("malformed-value", "/value/fields", "a `year-month` needs `year` and `month`");
        }
        RequireMonth(c.Month.Value);
        return (c.Year.Value, c.Month.Value);
    }

    private static void RequireMonth(int month)
    {
        if (month is < 1 or > 12)
        {
            throw OpFailure.Invalid("month-out-of-range", "/value/fields/month", Invariant($"month {month} is not in 1..12"));
        }
    }

    /// <summary>
    /// Hour and minute are present; all time fields are in range. A <c>second</c> of 60 is
    /// <c>unsupported</c> (it needs a leap-second table).
    /// </summary>
    public static (int Hour, int Minute, int Second, int Nano) RequireTime(CivilRead c)
    {
        if (c.Hour is null || c.Minute is null)
        {
            throw OpFailure.Invalid("malformed-value", "/value/fields", Invariant($"a `{c.Form}` needs `hour` and `minute`"));
        }
        if (c.Hour is < 0 or > 23)
        {
            throw OpFailure.Invalid("field-out-of-range", "/value/fields/hour", Invariant($"hour {c.Hour} is not in 0..23"));
        }
        if (c.Minute is < 0 or > 59)
        {
            throw OpFailure.Invalid("field-out-of-range", "/value/fields/minute", Invariant($"minute {c.Minute} is not in 0..59"));
        }
        if (c.Second is < 0 or > 60)
        {
            throw OpFailure.Invalid("field-out-of-range", "/value/fields/second", Invariant($"second {c.Second} is not in 0..60"));
        }
        if (c.Nano is < 0 or > 999_999_999)
        {
            throw OpFailure.Invalid("field-out-of-range", "/value/fields/nano", Invariant($"nano {c.Nano} is not in 0..999999999"));
        }
        if (c.Nano is not null && c.Second is null)
        {
            throw OpFailure.Invalid("malformed-value", "/value/fields/second", "`nano` needs `second`");
        }
        if (c.Second == 60)
        {
            throw OpFailure.Unsupported("leap-second", "/value/fields/second",
                "second 60 needs a leap-second table, which this library does not ship");
        }
        return (c.Hour.Value, c.Minute.Value, c.Second ?? 0, c.Nano ?? 0);
    }

    /// <summary>A POSIX instant as an exact number of seconds since 1970-01-01T00:00:00Z.</summary>
    public static Rational ReadPosixInstant(JsonElement envelope, string argument)
    {
        JsonElement value = envelope.GetProperty("value");
        RequirePosix(value.GetProperty("system"), "/value/system", argument);
        if (!value.TryGetProperty("ticks", out JsonElement ticks))
        {
            throw OpFailure.Unsupported("unsupported-operand", "/value/coordinate",
                Invariant($"{argument}: instants given as a `coordinate` are not implemented; use `ticks`"));
        }
        Rational t = Rational.FromInteger(BigInteger.Parse(ticks.GetString()!, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture));
        if (value.TryGetProperty("subTick", out JsonElement sub))
        {
            t += ReadRational(sub);
        }
        return t;
    }

    public static bool IsPosix(JsonElement ruleRef) =>
        ruleRef.GetProperty("system").GetString() == PosixSystem
        && ruleRef.GetProperty("id").GetString() == PosixId
        && ruleRef.GetProperty("version").GetString() == PosixVersion;

    public static void RequirePosix(JsonElement ruleRef, string path, string argument)
    {
        if (!IsPosix(ruleRef))
        {
            throw OpFailure.Unsupported("unsupported-operand", path,
                Invariant($"{argument}: only the `{PosixSystem}` `{PosixId}` version `{PosixVersion}` timescale is implemented"));
        }
    }

    public static Rational ReadRational(JsonElement r) => new(
        BigInteger.Parse(r.GetProperty("numerator").GetString()!, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture),
        BigInteger.Parse(r.GetProperty("denominator").GetString()!, NumberStyles.None, CultureInfo.InvariantCulture));

    // ---------------------------------------------------------------- writing

    public static string FormatYear(BigInteger year) => year.ToString(CultureInfo.InvariantCulture);

    public static JsonObject Calendar() => new() { ["id"] = "gregory", ["chronology"] = "proleptic" };

    public static JsonObject PosixRef() => new() { ["system"] = PosixSystem, ["id"] = PosixId, ["version"] = PosixVersion };

    public static JsonObject Civil(string form, JsonObject fields) => new()
    {
        ["schema"] = UtvValidator.SpecVersion,
        ["kind"] = "civil",
        ["value"] = new JsonObject { ["form"] = form, ["fields"] = fields, ["calendar"] = Calendar() },
    };

    public static JsonObject DateFields(BigInteger year, int month, int day) => new()
    {
        ["year"] = FormatYear(year),
        ["month"] = month,
        ["day"] = day,
    };

    /// <summary>Adds time fields: <c>second</c> only if given, <c>nano</c> only if given and non-zero.</summary>
    public static JsonObject AddTime(JsonObject fields, int hour, int minute, int? second, int? nano)
    {
        fields["hour"] = hour;
        fields["minute"] = minute;
        if (second is not null)
        {
            fields["second"] = second.Value;
        }
        if (nano is not null && nano.Value != 0)
        {
            fields["nano"] = nano.Value;
        }
        return fields;
    }

    public static JsonObject Instant(Rational t)
    {
        var value = new JsonObject
        {
            ["system"] = PosixRef(),
            ["ticks"] = t.Floor().ToString(CultureInfo.InvariantCulture),
        };
        Rational frac = t.FractionalPart();
        if (!frac.Numerator.IsZero)
        {
            value["subTick"] = RationalNode(frac);
        }
        return new JsonObject { ["schema"] = UtvValidator.SpecVersion, ["kind"] = "instant", ["value"] = value };
    }

    public static JsonObject PosixSeconds(Rational seconds) => new()
    {
        ["schema"] = UtvValidator.SpecVersion,
        ["kind"] = "amount",
        ["value"] = new JsonObject
        {
            ["basis"] = "clock-ticks",
            ["rules"] = PosixRef(),
            ["seconds"] = RationalNode(seconds),
        },
    };

    public static JsonObject RationalNode(Rational r) => new()
    {
        ["numerator"] = r.Numerator.ToString(CultureInfo.InvariantCulture),
        ["denominator"] = r.Denominator.ToString(CultureInfo.InvariantCulture),
    };

    /// <summary>The wall-clock reading of instant <paramref name="t"/> at <paramref name="offsetSeconds"/>, as datetime fields.</summary>
    public static JsonObject WallFields(Rational t, int offsetSeconds, string path)
    {
        Rational local = t + Rational.FromInteger(offsetSeconds);
        if (!local.TryGetNanoOfSecond(out int nano))
        {
            throw OpFailure.Unsupported("implementation-limit", path,
                Invariant($"the instant's fraction {local.FractionalPart()} is not a whole number of nanoseconds"));
        }
        BigInteger seconds = local.Floor();
        BigInteger days = MathUtil.FloorDiv(seconds, CivilMath.SecondsPerDay);
        int secondOfDay = (int)(seconds - days * CivilMath.SecondsPerDay);
        (BigInteger y, int m, int d) = CivilMath.CivilFromDays(days);
        return AddTime(DateFields(y, m, d), secondOfDay / 3600, secondOfDay / 60 % 60, secondOfDay % 60, nano);
    }

    public static JsonElement ToElement(JsonNode node)
    {
        using var buffer = new MemoryStream();
        using (var w = new Utf8JsonWriter(buffer, WriterOptions))
        {
            node.WriteTo(w);
        }
        using JsonDocument doc = JsonDocument.Parse(buffer.ToArray());
        return doc.RootElement.Clone();
    }

    public static JsonElement ToElement(string text)
    {
        using var buffer = new MemoryStream();
        using (var w = new Utf8JsonWriter(buffer, WriterOptions))
        {
            w.WriteStringValue(text);
        }
        using JsonDocument doc = JsonDocument.Parse(buffer.ToArray());
        return doc.RootElement.Clone();
    }

    public static JsonElement ToElement(long number)
    {
        using JsonDocument doc = JsonDocument.Parse(number.ToString(CultureInfo.InvariantCulture));
        return doc.RootElement.Clone();
    }

    public static JsonElement ToElement(BigInteger number)
    {
        using JsonDocument doc = JsonDocument.Parse(number.ToString(CultureInfo.InvariantCulture));
        return doc.RootElement.Clone();
    }

    /// <summary>JSON equality ignoring object key order; numbers compare by value.</summary>
    public static bool JsonEquals(JsonNode? a, JsonNode? b) =>
        a is null || b is null ? a is null && b is null : JsonEquals(ToElement(a), ToElement(b));

    /// <summary>JSON equality ignoring object key order; numbers compare by value.</summary>
    public static bool JsonEquals(JsonElement a, JsonElement b)
    {
        if (a.ValueKind != b.ValueKind)
        {
            return false;
        }
        switch (a.ValueKind)
        {
            case JsonValueKind.Object:
                var pa = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
                foreach (JsonProperty p in a.EnumerateObject())
                {
                    pa[p.Name] = p.Value;
                }
                int count = 0;
                foreach (JsonProperty p in b.EnumerateObject())
                {
                    count++;
                    if (!pa.TryGetValue(p.Name, out JsonElement v) || !JsonEquals(v, p.Value))
                    {
                        return false;
                    }
                }
                return count == pa.Count;
            case JsonValueKind.Array:
                if (a.GetArrayLength() != b.GetArrayLength())
                {
                    return false;
                }
                using (var ea = a.EnumerateArray().GetEnumerator())
                using (var eb = b.EnumerateArray().GetEnumerator())
                {
                    while (ea.MoveNext() && eb.MoveNext())
                    {
                        if (!JsonEquals(ea.Current, eb.Current))
                        {
                            return false;
                        }
                    }
                }
                return true;
            case JsonValueKind.String:
                return a.GetString() == b.GetString();
            case JsonValueKind.Number:
                return a.TryGetDecimal(out decimal da) && b.TryGetDecimal(out decimal db) ? da == db : a.GetRawText() == b.GetRawText();
            default:
                return true;
        }
    }

    /// <summary>A mutable copy of a JSON element.</summary>
    public static JsonObject Mutable(JsonElement element) =>
        JsonNode.Parse(element.GetRawText())!.AsObject();
}

/// <summary>Runs an operation body and turns semantic failures into results.</summary>
internal static class Op
{
    public static UtvResult Run(string operation, Func<UtvResult> body)
    {
        try
        {
            return body();
        }
        catch (OpFailure failure)
        {
            return new UtvResult(operation, failure.Status, failure.Diagnostics);
        }
    }

    public static UtvResult Unique(string operation, JsonNode candidate, IEnumerable<Diagnostic>? diagnostics = null) =>
        new(operation, UtvStatus.Unique, diagnostics, new[] { Values.ToElement(candidate) });

    public static UtvResult Unique(string operation, string candidate) =>
        new(operation, UtvStatus.Unique, null, new[] { Values.ToElement(candidate) });

    public static UtvResult Status(string operation, UtvStatus status, string code, string path, string message) =>
        new(operation, status, new[] { Diagnostic.Error(code, path, message) });
}
