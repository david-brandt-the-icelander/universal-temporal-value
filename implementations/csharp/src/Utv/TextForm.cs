using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Utv.Internal.Everyday;
using static System.FormattableString;

namespace Utv;

/// <summary>
/// The UTV text form (docs/text-form.md, profile <c>utv-text/1</c>) at the JSON layer: <see cref="Parse"/> and
/// <see cref="Format"/>. It covers the ISO 8601 / RFC 9557 points of <see cref="Iso"/>, ISO 8601 periods
/// (<c>P1M</c>, <c>-P1DT2H</c>) and bracket intervals (<c>[2026-11-01, 2026-12-01)</c>, <c>[2026-11-01, ..)</c>,
/// <c>[2026-11-01, ?)</c>). EDTF slash intervals (<c>2026-11-01/2026-11-30</c>) are accepted as input.
/// </summary>
/// <remarks>
/// The text form is defined only where it is lossless: <see cref="Format"/> gives <c>unsupported</c>
/// (<c>no-text-form</c>) for any value whose text would not parse back to the same JSON.
/// </remarks>
public static partial class TextForm
{
    private const string ParseOp = "parseText";
    private const string FormatOp = "formatText";

    private const string Digits = "[0-9]+";
    private const string Decimal = "[0-9]+(?:[.,][0-9]+)?";

    private static string PeriodGrammar(string n) =>
        @"\A(?<neg>-)?P(?:(?<y>" + n + ")Y)?(?:(?<mo>" + n + ")M)?(?:(?<w>" + n + ")W)?(?:(?<d>" + n + ")D)?"
        + @"(?<t>T(?:(?<h>" + n + ")H)?(?:(?<mi>" + n + ")M)?(?:(?<s>" + n + @")S)?)?\z";

    private static readonly Regex PeriodPattern = new(PeriodGrammar(Digits), RegexOptions.CultureInvariant);
    private static readonly Regex DecimalPeriodPattern = new(PeriodGrammar(Decimal), RegexOptions.CultureInvariant);

    // EDTF level 1/2 qualified or unspecified dates: 1347~, 1347?, 2026-11%, 134X, 2026-1X.
    [GeneratedRegex(@"\A[+-]?[0-9X]{4,}(?:-[0-9X]{2}){0,2}[~?%]?\z", RegexOptions.CultureInvariant)]
    private static partial Regex EdtfQualifiedPattern();

    private static readonly (string Group, string Name, char Designator, bool Time)[] PeriodParts =
    {
        ("y", "years", 'Y', false),
        ("mo", "months", 'M', false),
        ("w", "weeks", 'W', false),
        ("d", "days", 'D', false),
        ("h", "hours", 'H', true),
        ("mi", "minutes", 'M', true),
        ("s", "seconds", 'S', true),
    };

    /// <summary>Parses text-form input into a UTV value (operation <c>parseText</c>).</summary>
    /// <returns>
    /// <c>unique</c> with the value; <c>invalid</c> for text outside the grammar (<c>syntax</c>), out-of-range
    /// fields, reversed intervals (<c>reversed-interval</c>) or intervals mixing kinds of point
    /// (<c>kind-mismatch</c>); <c>unsupported</c> for valid notation outside the profile, such as decimal period
    /// components and EDTF qualifiers (<c>unsupported-form</c>).
    /// </returns>
    public static UtvResult Parse(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        return Op.Run(ParseOp, () => Op.Unique(ParseOp, ParseNode(text)));
    }

    /// <summary>Formats a UTV value as text-form text (operation <c>formatText</c>).</summary>
    /// <returns>
    /// <c>unique</c> with the text as a JSON string (read it with <see cref="UtvResult.GetString"/>);
    /// <c>invalid</c> for a value that names no real date or time; <c>unsupported</c> with <c>no-text-form</c> for
    /// values the text form cannot carry losslessly (POSIX or SI second counts, periods with mixed signs,
    /// <c>start-amount</c> intervals, recurrences, values with extra properties, and so on).
    /// </returns>
    public static UtvResult Format(JsonElement value) => Op.Run(FormatOp, () =>
    {
        Values.RequireStructural(value, "value");
        string text = FormatValue(value);
        // Defined only where lossless: the text must read back as the same value.
        JsonNode back;
        try
        {
            back = ParseNode(text);
        }
        catch (OpFailure)
        {
            throw NoTextForm("", Invariant($"`{text}` would not read back as this value"));
        }
        if (!Values.JsonEquals(Canonical(JsonNode.Parse(value.GetRawText())!), Canonical(back)))
        {
            throw NoTextForm("", Invariant($"`{text}` would lose part of this value; write it as JSON"));
        }
        return Op.Unique(FormatOp, text);
    });

    // ------------------------------------------------------------------ parsing

    internal static JsonNode ParseNode(string text)
    {
        if (text.Length > 0 && text[0] is '[' or '(')
        {
            return ParseBracketInterval(text);
        }
        if (text.StartsWith('P') || text.StartsWith("-P", StringComparison.Ordinal))
        {
            return ParsePeriod(text);
        }
        if (EdtfQualifiedPattern().IsMatch(text) && (text.Contains('X', StringComparison.Ordinal) || text[^1] is '~' or '?' or '%'))
        {
            throw OpFailure.Unsupported("unsupported-form", "",
                "EDTF qualifiers and unspecified digits (`1347~`, `1347?`, `134X`) are not mapped to UTV `uncertain` values yet");
        }
        int slash = text.IndexOf('/', StringComparison.Ordinal);
        int bracket = text.IndexOf('[', StringComparison.Ordinal);
        if (slash >= 0 && (bracket < 0 || slash < bracket))
        {
            return ParseEdtfInterval(text[..slash], text[(slash + 1)..]);
        }
        return Iso.Parse(text);
    }

    private static JsonNode ParsePeriod(string text)
    {
        Match m = PeriodPattern.Match(text);
        if (!m.Success)
        {
            if (DecimalPeriodPattern.Match(text) is { Success: true } d && HasComponent(d))
            {
                throw OpFailure.Unsupported("unsupported-form", "",
                    Invariant($"`{text}` has a decimal component; calendar components are whole numbers"));
            }
            throw Syntax(text, "an ISO 8601 period such as `P1M`, `P30D` or `PT2H` (hours, minutes and seconds follow `T`)");
        }
        if (!HasComponent(m) || (m.Groups["t"].Success && !PeriodParts.Any(p => p.Time && m.Groups[p.Group].Success)))
        {
            throw Syntax(text, "a period with at least one component (`P0D` is the empty period)");
        }
        bool negative = m.Groups["neg"].Success;
        var components = new JsonObject();
        foreach ((string group, string name, _, _) in PeriodParts)
        {
            if (m.Groups[group].Success)
            {
                BigInteger n = BigInteger.Parse(m.Groups[group].ValueSpan, NumberStyles.None, CultureInfo.InvariantCulture);
                if (!n.IsZero)
                {
                    components[name] = (negative ? -n : n).ToString(CultureInfo.InvariantCulture);
                }
            }
        }
        if (negative && components.Count == 0)
        {
            throw Syntax(text, "a negative period with a non-zero component");
        }
        return new JsonObject
        {
            ["schema"] = UtvValidator.SpecVersion,
            ["kind"] = "amount",
            ["value"] = new JsonObject { ["basis"] = "calendar", ["components"] = components },
        };

        static bool HasComponent(Match match) => PeriodParts.Any(p => match.Groups[p.Group].Success);
    }

    private static JsonNode ParseBracketInterval(string text)
    {
        char open = text[0];
        char close = text[^1];
        if (text.Length < 2 || close is not (']' or ')'))
        {
            throw Syntax(text, "an interval such as `[2026-11-01, 2026-12-01)`");
        }
        string[] parts = text[1..^1].Split(',');
        if (parts.Length != 2)
        {
            throw Syntax(text, "two endpoints separated by `, `");
        }
        string first = parts[0];
        string second = parts[1].TrimStart(' ');
        if (first.Length == 0 || second.Length == 0 || char.IsWhiteSpace(first[0]) || char.IsWhiteSpace(first[^1]) || char.IsWhiteSpace(second[^1]))
        {
            throw Syntax(text, "an interval such as `[2026-11-01, 2026-12-01)`, with optional whitespace only after the comma");
        }
        return Interval(text,
            Endpoint(first, included: open == '[', isStart: true, text),
            Endpoint(second, included: close == ']', isStart: false, text));
    }

    /// <summary>EDTF / ISO 8601-2 <c>a/b</c>: endpoints are included; <c>..</c> is open, an empty side unknown.</summary>
    private static JsonNode ParseEdtfInterval(string start, string end)
    {
        string text = start + "/" + end;
        if (end.Contains('/', StringComparison.Ordinal))
        {
            throw Syntax(text, "one `/` between the two ends");
        }
        return Interval(text, Edtf(start), Edtf(end));

        static (string State, JsonNode? Point) Edtf(string side) => side switch
        {
            "" => ("unknown", null),
            ".." => ("unbounded", null),
            _ => ("included", Iso.Parse(side)),
        };
    }

    private static (string State, JsonNode? Point) Endpoint(string token, bool included, bool isStart, string text)
    {
        if (token is ".." or "?")
        {
            if (included)
            {
                throw Syntax(text, Invariant($"`{token}` on an open bracket: `{(isStart ? "(" : ")")}`, since an unbounded or unknown end can't be included"));
            }
            return (token == ".." ? "unbounded" : "unknown", null);
        }
        return (included ? "included" : "excluded", Iso.Parse(token));
    }

    private static JsonNode Interval(string text, (string State, JsonNode? Point) start, (string State, JsonNode? Point) end)
    {
        JsonNode? point = start.Point ?? end.Point;
        if (point is null)
        {
            throw OpFailure.Unsupported("unsupported-form", "",
                Invariant($"`{text}` has no endpoint, so its domain (civil or physical) is unknown; write it as JSON"));
        }
        if (start.Point is not null && end.Point is not null)
        {
            if (Shape(start.Point) != Shape(end.Point))
            {
                throw OpFailure.Invalid("kind-mismatch", "",
                    Invariant($"`{text}` mixes a {Shape(start.Point)} with a {Shape(end.Point)}; both ends must be the same kind of value"));
            }
            UtvResult order = Calendar.Compare(Values.ToElement(start.Point), Values.ToElement(end.Point));
            if (order.IsUnique && order.GetString() == "after")
            {
                throw OpFailure.Invalid("reversed-interval", "", Invariant($"`{text}` ends before it starts"));
            }
        }
        return new JsonObject
        {
            ["schema"] = UtvValidator.SpecVersion,
            ["kind"] = "interval",
            ["value"] = new JsonObject
            {
                ["domain"] = (string)point["kind"]! == "instant" ? "physical" : "civil/gregory",
                ["start"] = Bound(start),
                ["end"] = Bound(end),
            },
        };

        static JsonObject Bound((string State, JsonNode? Point) b)
        {
            var node = new JsonObject { ["state"] = b.State };
            if (b.Point is not null)
            {
                node["point"] = b.Point;
            }
            return node;
        }
    }

    /// <summary>A point's kind and form, and its zone type: two ends of an interval must agree on all three.</summary>
    private static string Shape(JsonNode point)
    {
        string kind = (string)point["kind"]!;
        if (kind != "civil")
        {
            return kind == "instant" ? "an instant" : kind;
        }
        JsonNode value = point["value"]!;
        string zone = value["zone"]?["type"]?.GetValue<string>() switch
        {
            "iana" => "zoned ",
            "fixed-offset" => "fixed-offset ",
            _ => "",
        };
        return zone + (string)value["form"]!;
    }

    // ------------------------------------------------------------------ formatting

    private static string FormatValue(JsonElement value)
    {
        string kind = Values.KindOf(value);
        switch (kind)
        {
            case "civil":
            case "instant":
                return FormatPoint(value);
            case "amount":
                JsonElement amount = value.GetProperty("value");
                string basis = amount.GetProperty("basis").GetString()!;
                if (basis != "calendar")
                {
                    throw NoTextForm("/value/basis", Invariant($"a `{basis}` amount has no text form; no standard notation carries its basis"));
                }
                return FormatPeriod(amount);
            case "interval":
                return FormatInterval(value.GetProperty("value"));
            default:
                throw NoTextForm("/kind", Invariant($"kind `{kind}` has no text form; write it as JSON"));
        }
    }

    private static string FormatPoint(JsonElement value)
    {
        try
        {
            return Iso.Format(value);
        }
        catch (OpFailure failure) when (failure.Status == UtvStatus.Unsupported)
        {
            throw new OpFailure(UtvStatus.Unsupported, failure.Diagnostics
                .Select(d => d.Code == "unsupported-operand" ? d with { Code = "no-text-form" } : d)
                .ToArray());
        }
    }

    private static string FormatPeriod(JsonElement amount)
    {
        Dictionary<string, BigInteger> parts = Calendar.ReadComponents(amount);
        BigInteger[] nonZero = parts.Values.Where(v => !v.IsZero).ToArray();
        if (nonZero.Length == 0)
        {
            return "P0D";
        }
        bool negative = nonZero.All(v => v.Sign < 0);
        if (!negative && nonZero.Any(v => v.Sign < 0))
        {
            throw NoTextForm("/value/components", "a period with both positive and negative components has no ISO 8601 text");
        }
        var text = new StringBuilder(negative ? "-P" : "P");
        bool inTime = false;
        foreach ((_, string name, char designator, bool time) in PeriodParts)
        {
            if (!parts.TryGetValue(name, out BigInteger n) || n.IsZero)
            {
                continue;
            }
            if (time && !inTime)
            {
                text.Append('T');
                inTime = true;
            }
            text.Append(BigInteger.Abs(n).ToString(CultureInfo.InvariantCulture)).Append(designator);
        }
        return text.ToString();
    }

    private static string FormatInterval(JsonElement interval)
    {
        if (interval.TryGetProperty("construction", out JsonElement construction) && construction.GetString() != "start-end")
        {
            throw NoTextForm("/value/construction",
                Invariant($"a `{construction.GetString()}` interval has no text form; only `start-end` intervals have one"));
        }
        if (!interval.TryGetProperty("start", out JsonElement start) || !interval.TryGetProperty("end", out JsonElement end))
        {
            throw NoTextForm("/value", "an interval without both ends has no text form");
        }
        (char open, string first) = Side(start, isStart: true);
        (char close, string second) = Side(end, isStart: false);
        return Invariant($"{open}{first}, {second}{close}");

        static (char Bracket, string Text) Side(JsonElement bound, bool isStart)
        {
            string state = bound.GetProperty("state").GetString()!;
            return state switch
            {
                "included" => (isStart ? '[' : ']', FormatPoint(bound.GetProperty("point"))),
                "excluded" => (isStart ? '(' : ')', FormatPoint(bound.GetProperty("point"))),
                "unbounded" => (isStart ? '(' : ')', ".."),
                _ => (isStart ? '(' : ')', "?"),
            };
        }
    }

    // ------------------------------------------------------------------ helpers

    /// <summary>
    /// A copy with equivalent spellings made identical: an explicit floating zone is dropped, an explicit
    /// <c>start-end</c> construction is dropped, and calendar components become canonical decimal strings with zeros
    /// omitted.
    /// </summary>
    internal static JsonNode Canonical(JsonNode node)
    {
        JsonNode copy = node.DeepClone();
        if (copy is not JsonObject o || o["value"] is not JsonObject v)
        {
            return copy;
        }
        switch ((string?)(o["kind"] as JsonValue))
        {
            case "civil":
                if (v["zone"] is JsonObject zone && (string?)(zone["type"] as JsonValue) == "floating")
                {
                    v.Remove("zone");
                }
                break;
            case "interval":
                if ((string?)(v["construction"] as JsonValue) == "start-end")
                {
                    v.Remove("construction");
                }
                foreach (string side in new[] { "start", "end" })
                {
                    if (v[side] is JsonObject bound && bound["point"] is JsonNode point)
                    {
                        bound["point"] = Canonical(point);
                    }
                }
                break;
            case "amount":
                if (v["components"] is JsonObject components)
                {
                    var canonical = new JsonObject();
                    foreach ((string name, JsonNode? n) in components)
                    {
                        string? digits = n is JsonValue jv ? jv.ToJsonString().Trim('"') : null;
                        if (digits is not null && BigInteger.TryParse(digits, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out BigInteger b))
                        {
                            if (!b.IsZero)
                            {
                                canonical[name] = b.ToString(CultureInfo.InvariantCulture);
                            }
                        }
                        else
                        {
                            canonical[name] = n?.DeepClone();
                        }
                    }
                    v["components"] = canonical;
                }
                break;
        }
        return copy;
    }

    private static OpFailure Syntax(string text, string expected) =>
        OpFailure.Invalid("syntax", "", Invariant($"`{text}` is not UTV text; expected {expected}"));

    private static OpFailure NoTextForm(string path, string message) => OpFailure.Unsupported("no-text-form", path, message);
}
