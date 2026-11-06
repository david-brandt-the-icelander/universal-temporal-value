using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using static System.FormattableString;

namespace Utv.Internal;

/// <summary>
/// A small JSON Schema (draft 2020-12) interpreter covering exactly the keywords that
/// <c>schema/utv-1.0.schema.json</c> uses. Loading fails if the schema uses any other keyword,
/// so a schema change can never be silently ignored.
/// </summary>
internal sealed class SchemaInterpreter
{
    public const string ResourceName = "Utv.utv-1.0.schema.json";

    /// <summary>Keywords that are evaluated.</summary>
    internal static readonly IReadOnlySet<string> AssertionKeywords = new HashSet<string>(StringComparer.Ordinal)
    {
        "$ref", "type", "const", "enum", "minLength", "pattern", "items", "minItems",
        "required", "properties", "additionalProperties", "oneOf", "anyOf", "not",
    };

    /// <summary>Keywords that carry no assertion (2020-12 treats <c>format</c> as an annotation by default).</summary>
    internal static readonly IReadOnlySet<string> AnnotationKeywords = new HashSet<string>(StringComparer.Ordinal)
    {
        "$schema", "$id", "$defs", "title", "description", "format",
    };

    private const string ValueRef = "#/$defs/value";

    private static readonly Lazy<SchemaInterpreter> Shared = new(Load);

    private readonly JObject _root;
    private readonly Dictionary<string, Regex> _patterns = new(StringComparer.Ordinal);
    private readonly Dictionary<string, JNode> _refs = new(StringComparer.Ordinal);

    private SchemaInterpreter(JObject root)
    {
        _root = root;
        Prepare(root, "#");
    }

    public static SchemaInterpreter Instance => Shared.Value;

    public JObject Root => _root;

    /// <summary>The embedded schema text, exactly as built from the repository file.</summary>
    public static string ReadEmbeddedText()
    {
        using Stream stream = typeof(SchemaInterpreter).Assembly.GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException("embedded schema resource " + ResourceName + " is missing");
        using var reader = new StreamReader(stream, new UTF8Encoding(false, true));
        return reader.ReadToEnd();
    }

    private static SchemaInterpreter Load()
    {
        JNode root = StrictJson.Parse(ReadEmbeddedText());
        return root is JObject obj
            ? new SchemaInterpreter(obj)
            : throw new InvalidOperationException("embedded schema is not a JSON object");
    }

    /// <summary>Checks a UTV value (top level or nested) by dispatching on its <c>kind</c>.</summary>
    /// <remarks>
    /// Equivalent to evaluating <c>#/$defs/value</c> (a <c>oneOf</c> over the 26 per-kind schemas, each
    /// pinning <c>kind</c> with <c>const</c>), but reports the failures of the one applicable branch.
    /// A test checks that the schema keeps that shape.
    /// </remarks>
    public void CheckUtvValue(JNode instance, string path, List<Diagnostic> diagnostics)
    {
        if (instance is not JObject obj)
        {
            diagnostics.Add(Diagnostic.Error("not-an-object", path, "a UTV value must be a JSON object"));
            return;
        }
        JNode? kind = obj.Get("kind");
        if (kind is null)
        {
            diagnostics.Add(Diagnostic.Error("missing-property", path + "/kind", "required property `kind` is missing"));
            return;
        }
        if (kind is not JString kindName || !UtvValidator.IsCoreKind(kindName.Value))
        {
            diagnostics.Add(Diagnostic.Error("unknown-kind", path + "/kind", Invariant($"{kind.ToJsonText()} is not a UTV 1.0 core kind")));
            return;
        }
        Check(instance, Resolve("#/$defs/" + kindName.Value + "Value"), path, diagnostics);
    }

    public JNode Resolve(string reference) =>
        _refs.TryGetValue(reference, out JNode? target)
            ? target
            : throw new InvalidOperationException("unresolvable $ref " + reference);

    public bool IsValid(JNode instance, JNode schema)
    {
        var scratch = new List<Diagnostic>();
        Check(instance, schema, "", scratch);
        return scratch.Count == 0;
    }

    public void Check(JNode instance, JNode schema, string path, List<Diagnostic> diagnostics)
    {
        if (schema is JBool b)
        {
            if (!b.Value)
            {
                diagnostics.Add(Diagnostic.Error("schema-violation", path, "no value is allowed here"));
            }
            return;
        }
        var s = (JObject)schema;

        if (s.Get("$ref") is JString reference)
        {
            if (string.Equals(reference.Value, ValueRef, StringComparison.Ordinal))
            {
                CheckUtvValue(instance, path, diagnostics);
            }
            else
            {
                Check(instance, Resolve(reference.Value), path, diagnostics);
            }
        }

        if (s.Get("type") is JNode type)
        {
            IEnumerable<string> types = type is JArray list
                ? list.Items.Cast<JString>().Select(t => t.Value)
                : new[] { ((JString)type).Value };
            string[] allowed = types.ToArray();
            if (!allowed.Any(t => HasType(instance, t)))
            {
                diagnostics.Add(Diagnostic.Error("wrong-type", path, Invariant($"expected {string.Join(" or ", allowed)}, found {instance.TypeName}")));
                return;
            }
        }

        if (s.Get("const") is JNode constant && !JNode.JsonEquals(instance, constant))
        {
            diagnostics.Add(Diagnostic.Error("const-mismatch", path, Invariant($"expected {constant.ToJsonText()}")));
        }

        if (s.Get("enum") is JArray options && !options.Items.Any(o => JNode.JsonEquals(instance, o)))
        {
            diagnostics.Add(Diagnostic.Error("enum-mismatch", path, Invariant($"expected one of {options.ToJsonText()}")));
        }

        if (instance is JString str)
        {
            if (s.Get("minLength") is JNumber minLength && minLength.TryGetInt32(out int min) && str.CodePointCount < min)
            {
                diagnostics.Add(Diagnostic.Error("too-short", path, Invariant($"string is shorter than {min} characters")));
            }
            if (s.Get("pattern") is JString pattern && !_patterns[pattern.Value].IsMatch(str.Value))
            {
                diagnostics.Add(Diagnostic.Error("pattern-mismatch", path, Invariant($"does not match {pattern.Value}")));
            }
        }

        if (instance is JArray array)
        {
            if (s.Get("minItems") is JNumber minItems && minItems.TryGetInt32(out int min) && array.Items.Count < min)
            {
                diagnostics.Add(Diagnostic.Error("too-few-items", path, Invariant($"fewer than {min} items")));
            }
            if (s.Get("items") is JNode itemSchema)
            {
                for (int i = 0; i < array.Items.Count; i++)
                {
                    Check(array.Items[i], itemSchema, Invariant($"{path}/{i}"), diagnostics);
                }
            }
        }

        if (instance is JObject obj)
        {
            if (s.Get("required") is JArray required)
            {
                foreach (JString name in required.Items.Cast<JString>())
                {
                    if (!obj.Contains(name.Value))
                    {
                        diagnostics.Add(Diagnostic.Error("missing-property", path + "/" + StrictJson.PointerToken(name.Value), Invariant($"required property `{name.Value}` is missing")));
                    }
                }
            }
            var properties = s.Get("properties") as JObject;
            JNode? additional = s.Get("additionalProperties");
            foreach (KeyValuePair<string, JNode> member in obj.Members)
            {
                string memberPath = path + "/" + StrictJson.PointerToken(member.Key);
                if (properties is not null && properties.TryGet(member.Key, out JNode? propertySchema))
                {
                    Check(member.Value, propertySchema, memberPath, diagnostics);
                }
                else if (additional is JBool { Value: false })
                {
                    diagnostics.Add(Diagnostic.Error("unknown-property", memberPath, Invariant($"unknown property `{member.Key}`")));
                }
                else if (additional is JObject)
                {
                    Check(member.Value, additional, memberPath, diagnostics);
                }
            }
        }

        if (s.Get("anyOf") is JArray anyOf && !anyOf.Items.Any(branch => IsValid(instance, branch)))
        {
            diagnostics.Add(Diagnostic.Error("schema-violation", path, "matches none of the allowed alternatives (anyOf)"));
        }

        if (s.Get("oneOf") is JArray oneOf)
        {
            int matched = oneOf.Items.Count(branch => IsValid(instance, branch));
            if (matched != 1)
            {
                diagnostics.Add(Diagnostic.Error("schema-violation", path, Invariant($"must match exactly one alternative (oneOf), matched {matched}")));
            }
        }

        if (s.Get("not") is JNode forbidden && IsValid(instance, forbidden))
        {
            diagnostics.Add(Diagnostic.Error("schema-violation", path, "matches a forbidden form (not)"));
        }
    }

    private static bool HasType(JNode instance, string type) => type switch
    {
        "object" => instance is JObject,
        "array" => instance is JArray,
        "string" => instance is JString,
        "boolean" => instance is JBool,
        "null" => instance is JNull,
        "number" => instance is JNumber,
        "integer" => instance is JNumber { IsInteger: true },
        _ => throw new InvalidOperationException("unknown JSON Schema type " + type),
    };

    /// <summary>
    /// Walks every subschema once at load time: rejects unknown keywords, resolves every <c>$ref</c>
    /// and compiles every <c>pattern</c>.
    /// </summary>
    private void Prepare(JNode schema, string location)
    {
        if (schema is JBool)
        {
            return;
        }
        if (schema is not JObject s)
        {
            throw new InvalidOperationException("schema at " + location + " is neither an object nor a boolean");
        }
        foreach (KeyValuePair<string, JNode> keyword in s.Members)
        {
            string at = location + "/" + StrictJson.PointerToken(keyword.Key);
            switch (keyword.Key)
            {
                case "$defs":
                case "properties":
                    foreach (KeyValuePair<string, JNode> sub in ((JObject)keyword.Value).Members)
                    {
                        Prepare(sub.Value, at + "/" + StrictJson.PointerToken(sub.Key));
                    }
                    break;
                case "items":
                case "additionalProperties":
                case "not":
                    Prepare(keyword.Value, at);
                    break;
                case "oneOf":
                case "anyOf":
                    var branches = (JArray)keyword.Value;
                    for (int i = 0; i < branches.Items.Count; i++)
                    {
                        Prepare(branches.Items[i], Invariant($"{at}/{i}"));
                    }
                    break;
                case "$ref":
                    string reference = ((JString)keyword.Value).Value;
                    _refs[reference] = ResolvePointer(reference);
                    break;
                case "pattern":
                    string pattern = ((JString)keyword.Value).Value;
                    if (!_patterns.ContainsKey(pattern))
                    {
                        _patterns[pattern] = CompilePattern(pattern);
                    }
                    break;
                case "type":
                    IEnumerable<JNode> types = keyword.Value is JArray list ? list.Items : new[] { keyword.Value };
                    foreach (JNode t in types)
                    {
                        HasType(JNull.Instance, ((JString)t).Value);
                    }
                    break;
                default:
                    if (!AssertionKeywords.Contains(keyword.Key) && !AnnotationKeywords.Contains(keyword.Key))
                    {
                        throw new InvalidOperationException("the structural schema uses unsupported keyword " + at);
                    }
                    break;
            }
        }
    }

    private JNode ResolvePointer(string reference)
    {
        if (!reference.StartsWith('#'))
        {
            throw new InvalidOperationException("only local $ref values are supported: " + reference);
        }
        JNode node = _root;
        string pointer = reference.Substring(1);
        if (pointer.Length == 0)
        {
            return node;
        }
        foreach (string rawToken in pointer.Substring(1).Split('/'))
        {
            string token = rawToken.Replace("~1", "/", StringComparison.Ordinal).Replace("~0", "~", StringComparison.Ordinal);
            node = node is JObject obj && obj.TryGet(token, out JNode? child)
                ? child
                : throw new InvalidOperationException("unresolvable $ref " + reference);
        }
        return node;
    }

    /// <summary>
    /// Compiles an ECMA-262 pattern for .NET. The schema's patterns use only <c>^</c>, <c>$</c>, groups,
    /// alternation, ASCII ranges and quantifiers, which mean the same in both dialects except <c>$</c>:
    /// .NET also matches it before a final newline, so it is rewritten to <c>\z</c>.
    /// </summary>
    internal static Regex CompilePattern(string pattern)
    {
        var translated = new StringBuilder(pattern.Length + 4);
        bool inClass = false;
        for (int i = 0; i < pattern.Length; i++)
        {
            char c = pattern[i];
            if (c == '\\' && i + 1 < pattern.Length)
            {
                translated.Append(c).Append(pattern[++i]);
                continue;
            }
            if (inClass)
            {
                inClass = c != ']';
            }
            else if (c == '[')
            {
                inClass = true;
            }
            else if (c == '$')
            {
                translated.Append("\\z");
                continue;
            }
            translated.Append(c);
        }
        // NonBacktracking guarantees matching time linear in the input length.
        return new Regex(translated.ToString(), RegexOptions.CultureInvariant | RegexOptions.NonBacktracking);
    }
}
