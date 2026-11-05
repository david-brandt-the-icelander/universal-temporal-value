using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.RegularExpressions;
using Utv.Internal;
using static System.FormattableString;

namespace Utv;

/// <summary>
/// Envelope and structural validation of UTV JSON text against <c>schema/utv-1.0.schema.json</c>.
/// </summary>
/// <remarks>
/// Structural validity says a value is well-formed. It says nothing about semantic validity or
/// resolvability, which are separate checks. Nothing here reads the clock, time zone or locale.
/// </remarks>
public static partial class UtvValidator
{
    /// <summary>The only specification version this reader implements, <c>utv/1.0</c>.</summary>
    public const string SpecVersion = "utv/1.0";

    private static readonly string[] CoreKindList =
    {
        "instant", "civil", "amount", "interval", "recurrence", "uncertain", "set", "clock",
        "observation", "logical-clock", "relative", "relation", "assertion", "event-history",
        "location-intent", "constraint", "calendar-rule", "temporal-reference", "ordinal-position",
        "event-relative", "selection", "sample", "series", "temporal-graph", "sequence", "pattern",
    };

    private static readonly HashSet<string> CoreKindSet = new(CoreKindList, StringComparer.Ordinal);

    private static readonly HashSet<string> EnvelopeProperties = new(StringComparer.Ordinal)
    {
        "schema", "kind", "value", "provenance", "extensions",
    };

    /// <summary>The 26 core kinds of UTV 1.0, in specification order.</summary>
    public static IReadOnlyList<string> CoreKinds { get; } = new ReadOnlyCollection<string>(CoreKindList);

    /// <summary>True if <paramref name="kind"/> is one of the 26 core kinds (exact, case-sensitive).</summary>
    public static bool IsCoreKind(string kind) => CoreKindSet.Contains(kind);

    /// <summary>
    /// True if <paramref name="identifier"/> is a well-formed wire identifier
    /// <c>utv/MAJOR.MINOR</c> (no leading zeros, no surrounding whitespace).
    /// </summary>
    public static bool IsWellFormedVersion(string identifier) => WellFormedVersion().IsMatch(identifier);

    // \z rather than $: .NET's $ also matches before a trailing newline.
    [GeneratedRegex(@"\Autv/(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\z", RegexOptions.CultureInvariant)]
    private static partial Regex WellFormedVersion();

    /// <summary>
    /// Parses one UTV JSON document and checks it structurally.
    /// </summary>
    /// <returns>
    /// A <c>validate</c> result: <see cref="UtvStatus.Valid"/>; <see cref="UtvStatus.Invalid"/> for malformed
    /// JSON, duplicate keys, a missing or malformed version identifier, unknown kinds or properties, or any
    /// other schema violation; <see cref="UtvStatus.Unsupported"/> for a well-formed version other than
    /// <c>utv/1.0</c> (for example <c>utv/1.3</c> or <c>utv/2.0</c>).
    /// </returns>
    public static UtvResult ValidateStructural(string json) => ValidateStructural(json, out _);

    internal static UtvResult ValidateStructural(string json, out JObject? document)
    {
        ArgumentNullException.ThrowIfNull(json);
        document = null;
        JNode root;
        try
        {
            root = StrictJson.Parse(json);
        }
        catch (StrictJsonException e)
        {
            return UtvResult.Validate(UtvStatus.Invalid, Diagnostic.Error(e.Code, e.Path, e.Message));
        }

        if (root is not JObject obj)
        {
            return UtvResult.Validate(UtvStatus.Invalid, Diagnostic.Error("not-an-object", "", "a UTV value must be a JSON object"));
        }

        // Version first: a newer version may legitimately use vocabulary this reader does not know.
        JNode? schema = obj.Get("schema");
        if (schema is not JString { Value: SpecVersion })
        {
            if (schema is JString other && IsWellFormedVersion(other.Value))
            {
                return UtvResult.Validate(UtvStatus.Unsupported, Diagnostic.Error(
                    "unsupported-version", "/schema",
                    Invariant($"specification version `{other.Value}` is not implemented; this reader implements {SpecVersion}")));
            }
            string why = schema switch
            {
                null => "the `schema` property is missing",
                JString s => Invariant($"`{s.Value}` is not a well-formed identifier utv/MAJOR.MINOR"),
                _ => Invariant($"`schema` must be a string, found {schema.TypeName}"),
            };
            return UtvResult.Validate(UtvStatus.Invalid, Diagnostic.Error("unknown-schema", "/schema", why));
        }

        var diagnostics = new List<Diagnostic>();
        foreach (KeyValuePair<string, JNode> member in obj.Members.Where(m => !EnvelopeProperties.Contains(m.Key)))
        {
            diagnostics.Add(Diagnostic.Error("unknown-property", "/" + StrictJson.PointerToken(member.Key), Invariant($"unknown property `{member.Key}`")));
        }
        int envelopeErrors = diagnostics.Count;
        SchemaInterpreter.Instance.CheckUtvValue(obj, "", diagnostics);
        if (envelopeErrors > 0)
        {
            // The per-kind schema reports the same unknown top-level properties again (identical records); keep one copy.
            diagnostics = diagnostics.Distinct().ToList();
        }

        if (diagnostics.Count > 0)
        {
            return UtvResult.Validate(UtvStatus.Invalid, diagnostics);
        }
        document = obj;
        return UtvResult.Validate(UtvStatus.Valid);
    }
}
