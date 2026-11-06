using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Numerics;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using static System.FormattableString;

namespace Utv.Internal;

/// <summary>An immutable JSON value tree with duplicate-free objects and exact numbers.</summary>
internal abstract class JNode
{
    /// <summary>The JSON Schema type name of this node (<c>integer</c> is reported as <c>number</c>).</summary>
    public abstract string TypeName { get; }

    /// <summary>Compact JSON text, for messages.</summary>
    public string ToJsonText()
    {
        using var buffer = new MemoryStream();
        using (var w = new Utf8JsonWriter(buffer, new JsonWriterOptions { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping }))
        {
            Write(w);
        }
        return Encoding.UTF8.GetString(buffer.ToArray());
    }

    internal abstract void Write(Utf8JsonWriter w);

    /// <summary>JSON equality: objects ignore member order, numbers compare mathematically.</summary>
    public static bool JsonEquals(JNode a, JNode b)
    {
        switch (a)
        {
            case JNull:
                return b is JNull;
            case JBool ab:
                return b is JBool bb && ab.Value == bb.Value;
            case JString sa:
                return b is JString sb && string.Equals(sa.Value, sb.Value, StringComparison.Ordinal);
            case JNumber na:
                return b is JNumber nb && na.Mantissa == nb.Mantissa && na.Exponent == nb.Exponent;
            case JArray xa:
                if (b is not JArray xb || xa.Items.Count != xb.Items.Count)
                {
                    return false;
                }
                for (int i = 0; i < xa.Items.Count; i++)
                {
                    if (!JsonEquals(xa.Items[i], xb.Items[i]))
                    {
                        return false;
                    }
                }
                return true;
            case JObject oa:
                if (b is not JObject ob || oa.Count != ob.Count)
                {
                    return false;
                }
                foreach (KeyValuePair<string, JNode> member in oa.Members)
                {
                    if (!ob.TryGet(member.Key, out JNode? other) || !JsonEquals(member.Value, other))
                    {
                        return false;
                    }
                }
                return true;
            default:
                return false;
        }
    }
}

internal sealed class JNull : JNode
{
    public static readonly JNull Instance = new();
    private JNull() { }
    public override string TypeName => "null";
    internal override void Write(Utf8JsonWriter w) => w.WriteNullValue();
}

internal sealed class JBool : JNode
{
    public static readonly JBool True = new(true);
    public static readonly JBool False = new(false);
    private JBool(bool value) => Value = value;
    public bool Value { get; }
    public override string TypeName => "boolean";
    internal override void Write(Utf8JsonWriter w) => w.WriteBooleanValue(Value);
}

internal sealed class JString : JNode
{
    public JString(string value) => Value = value;
    public string Value { get; }
    public override string TypeName => "string";

    /// <summary>Length in Unicode code points, as JSON Schema <c>minLength</c> counts it.</summary>
    public int CodePointCount
    {
        get
        {
            int n = 0;
            for (int i = 0; i < Value.Length; i++)
            {
                if (!char.IsLowSurrogate(Value[i]) || i == 0 || !char.IsHighSurrogate(Value[i - 1]))
                {
                    n++;
                }
            }
            return n;
        }
    }

    internal override void Write(Utf8JsonWriter w) => w.WriteStringValue(Value);
}

/// <summary>
/// An exact JSON number, <c>Mantissa × 10^Exponent</c>, normalized so the mantissa has no
/// trailing zeros (zero is <c>0 × 10^0</c>). Never passes through floating point.
/// </summary>
internal sealed class JNumber : JNode
{
    public JNumber(string raw)
    {
        Raw = raw;
        int i = 0;
        bool negative = raw[i] == '-';
        if (negative)
        {
            i++;
        }
        var digits = new StringBuilder();
        while (i < raw.Length && char.IsAsciiDigit(raw[i]))
        {
            digits.Append(raw[i++]);
        }
        int fractionDigits = 0;
        if (i < raw.Length && raw[i] == '.')
        {
            i++;
            while (i < raw.Length && char.IsAsciiDigit(raw[i]))
            {
                digits.Append(raw[i++]);
                fractionDigits++;
            }
        }
        BigInteger exponent = BigInteger.Zero;
        if (i < raw.Length && (raw[i] == 'e' || raw[i] == 'E'))
        {
            exponent = BigInteger.Parse(raw.AsSpan(i + 1), NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture);
        }
        exponent -= fractionDigits;
        string significant = digits.ToString().TrimStart('0');
        int trimmed = significant.Length;
        significant = significant.TrimEnd('0');
        if (significant.Length == 0)
        {
            Mantissa = BigInteger.Zero;
            Exponent = BigInteger.Zero;
            return;
        }
        exponent += trimmed - significant.Length;
        BigInteger mantissa = BigInteger.Parse(significant, NumberStyles.None, CultureInfo.InvariantCulture);
        Mantissa = negative ? -mantissa : mantissa;
        Exponent = exponent;
    }

    /// <summary>The literal as written in the input.</summary>
    public string Raw { get; }
    public BigInteger Mantissa { get; }
    public BigInteger Exponent { get; }

    /// <summary>True when the value has no fractional part (JSON Schema <c>integer</c>; <c>1.0</c> counts).</summary>
    public bool IsInteger => Exponent.Sign >= 0;

    public override string TypeName => "number";

    /// <summary>The value as an <see cref="int"/>, if it is an integer in range.</summary>
    public bool TryGetInt32(out int value)
    {
        value = 0;
        if (!IsInteger || Exponent > 10)
        {
            return false;
        }
        BigInteger exact = Mantissa * BigInteger.Pow(10, (int)Exponent);
        if (exact < int.MinValue || exact > int.MaxValue)
        {
            return false;
        }
        value = (int)exact;
        return true;
    }

    internal override void Write(Utf8JsonWriter w) => w.WriteRawValue(Raw, skipInputValidation: true);
}

internal sealed class JArray : JNode
{
    public JArray(IReadOnlyList<JNode> items) => Items = items;
    public IReadOnlyList<JNode> Items { get; }
    public override string TypeName => "array";

    internal override void Write(Utf8JsonWriter w)
    {
        w.WriteStartArray();
        foreach (JNode item in Items)
        {
            item.Write(w);
        }
        w.WriteEndArray();
    }
}

internal sealed class JObject : JNode
{
    private readonly List<KeyValuePair<string, JNode>> _members = new();
    private readonly Dictionary<string, JNode> _byName = new(StringComparer.Ordinal);

    /// <summary>Members in input order.</summary>
    public IReadOnlyList<KeyValuePair<string, JNode>> Members => _members;
    public int Count => _members.Count;
    public override string TypeName => "object";

    public bool Contains(string name) => _byName.ContainsKey(name);

    public bool TryGet(string name, [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out JNode? value) =>
        _byName.TryGetValue(name, out value);

    public JNode? Get(string name) => _byName.TryGetValue(name, out JNode? value) ? value : null;

    internal void Add(string name, JNode value)
    {
        _byName.Add(name, value);
        _members.Add(new KeyValuePair<string, JNode>(name, value));
    }

    internal override void Write(Utf8JsonWriter w)
    {
        w.WriteStartObject();
        foreach (KeyValuePair<string, JNode> member in _members)
        {
            w.WritePropertyName(member.Key);
            member.Value.Write(w);
        }
        w.WriteEndObject();
    }
}

/// <summary>Raised by <see cref="StrictJson.Parse"/> for input that is not acceptable JSON.</summary>
internal sealed class StrictJsonException : Exception
{
    public StrictJsonException(string code, string path, string message)
        : base(message)
    {
        Code = code;
        Path = path;
    }

    public string Code { get; }
    public string Path { get; }
}

/// <summary>
/// Strict RFC 8259 parsing on top of <see cref="JsonDocument"/>: no comments, no trailing commas,
/// no NaN/Infinity, no trailing content, no duplicate object keys at any depth, no lone surrogates.
/// </summary>
internal static class StrictJson
{
    /// <summary>Maximum nesting depth of arrays and objects (spec §9: enforce limits on object depth).</summary>
    public const int MaxDepth = 128;

    private static readonly JsonDocumentOptions Options = new()
    {
        AllowTrailingCommas = false,
        CommentHandling = JsonCommentHandling.Disallow,
        MaxDepth = MaxDepth,
    };

    public static JNode Parse(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(text, Options);
        }
        catch (JsonException e)
        {
            throw new StrictJsonException("malformed-json", "", e.Message);
        }
        catch (ArgumentException e)
        {
            // Input text that is not valid UTF-16 (for example an unpaired surrogate char).
            throw new StrictJsonException("malformed-json", "", e.Message);
        }
        using (document)
        {
            return Convert(document.RootElement, "");
        }
    }

    /// <summary>Escapes one JSON Pointer reference token (RFC 6901).</summary>
    public static string PointerToken(string name) =>
        name.Replace("~", "~0", StringComparison.Ordinal).Replace("/", "~1", StringComparison.Ordinal);

    private static JNode Convert(JsonElement element, string path)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                var obj = new JObject();
                foreach (JsonProperty property in element.EnumerateObject())
                {
                    string name = ReadString(() => property.Name, path);
                    string childPath = path + "/" + PointerToken(name);
                    if (obj.Contains(name))
                    {
                        throw new StrictJsonException("duplicate-key", childPath, Invariant($"duplicate JSON key `{name}`"));
                    }
                    obj.Add(name, Convert(property.Value, childPath));
                }
                return obj;
            case JsonValueKind.Array:
                var items = new List<JNode>(element.GetArrayLength());
                int index = 0;
                foreach (JsonElement item in element.EnumerateArray())
                {
                    items.Add(Convert(item, path + "/" + index.ToString(CultureInfo.InvariantCulture)));
                    index++;
                }
                return new JArray(items);
            case JsonValueKind.String:
                return new JString(ReadString(() => element.GetString()!, path));
            case JsonValueKind.Number:
                return new JNumber(element.GetRawText());
            case JsonValueKind.True:
                return JBool.True;
            case JsonValueKind.False:
                return JBool.False;
            case JsonValueKind.Null:
                return JNull.Instance;
            default:
                throw new StrictJsonException("malformed-json", path, "unexpected JSON token");
        }
    }

    private static string ReadString(Func<string> read, string path)
    {
        string value;
        try
        {
            value = read();
        }
        catch (InvalidOperationException e)
        {
            throw new StrictJsonException("malformed-json", path, e.Message);
        }
        // An escaped unpaired surrogate (for example "\ud800") is not a Unicode string (RFC 8259 §8.2).
        for (int i = 0; i < value.Length; i++)
        {
            char c = value[i];
            if (char.IsHighSurrogate(c) && i + 1 < value.Length && char.IsLowSurrogate(value[i + 1]))
            {
                i++;
            }
            else if (char.IsSurrogate(c))
            {
                throw new StrictJsonException("malformed-json", path, "string contains an unpaired surrogate");
            }
        }
        return value;
    }
}
