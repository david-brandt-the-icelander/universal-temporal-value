using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace Utv;

/// <summary>
/// A <c>utv-result/1.0</c> evaluation result (spec/RESULT-CONTRACT.md). Immutable.
/// </summary>
/// <remarks>
/// This increment always reports an empty manifest, no assumptions and a <c>lossless</c> loss report,
/// which is correct for the validation operations it implements: they depend on no external rules.
/// </remarks>
public sealed class UtvResult
{
    /// <summary>The result-contract wire identifier, <c>utv-result/1.0</c>.</summary>
    public const string ResultSchema = "utv-result/1.0";

    /// <summary>Creates a result.</summary>
    /// <param name="operation">The operation name, for example <c>validate</c>.</param>
    /// <param name="status">The outcome.</param>
    /// <param name="diagnostics">Diagnostics, in report order.</param>
    /// <param name="candidates">Candidate UTV envelopes. They are cloned, so they outlive their source document.</param>
    /// <exception cref="ArgumentException">
    /// The candidates break a status invariant: <see cref="UtvStatus.None"/> must have none and
    /// <see cref="UtvStatus.Unique"/> exactly one.
    /// </exception>
    public UtvResult(
        string operation,
        UtvStatus status,
        IEnumerable<Diagnostic>? diagnostics = null,
        IEnumerable<JsonElement>? candidates = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(operation);
        if (!Enum.IsDefined(status))
        {
            throw new ArgumentOutOfRangeException(nameof(status), status, "not a UTV result status");
        }
        Operation = operation;
        Status = status;
        Diagnostics = new ReadOnlyCollection<Diagnostic>((diagnostics ?? Array.Empty<Diagnostic>()).ToArray());
        Candidates = new ReadOnlyCollection<JsonElement>((candidates ?? Array.Empty<JsonElement>()).Select(c => c.Clone()).ToArray());
        if (Diagnostics.Any(d => d is null))
        {
            throw new ArgumentException("diagnostics must not contain null", nameof(diagnostics));
        }
        if (status == UtvStatus.None && Candidates.Count != 0)
        {
            throw new ArgumentException("a `none` result has no candidates", nameof(candidates));
        }
        if (status == UtvStatus.Unique && Candidates.Count != 1)
        {
            throw new ArgumentException("a `unique` result has exactly one candidate", nameof(candidates));
        }
    }

    /// <summary>Always <c>utv-result/1.0</c>.</summary>
    public string Schema => ResultSchema;

    /// <summary>The operation that produced this result, for example <c>validate</c>.</summary>
    public string Operation { get; }

    /// <summary>The outcome.</summary>
    public UtvStatus Status { get; }

    /// <summary>Diagnostics, in report order.</summary>
    public IReadOnlyList<Diagnostic> Diagnostics { get; }

    /// <summary>Candidate UTV envelopes (empty for validation).</summary>
    public IReadOnlyList<JsonElement> Candidates { get; }

    /// <summary>Serializes the result as a compact <c>utv-result/1.0</c> JSON object.</summary>
    public string ToJson()
    {
        using var buffer = new MemoryStream();
        using (var w = new Utf8JsonWriter(buffer, new JsonWriterOptions { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping }))
        {
            w.WriteStartObject();
            w.WriteString("schema", ResultSchema);
            w.WriteString("operation", Operation);
            w.WriteString("status", Status.ToWireString());
            w.WriteStartArray("candidates");
            foreach (JsonElement candidate in Candidates)
            {
                candidate.WriteTo(w);
            }
            w.WriteEndArray();
            w.WriteStartObject("manifest");
            w.WriteStartArray("dependencies");
            w.WriteEndArray();
            w.WriteEndObject();
            w.WriteStartArray("assumptions");
            w.WriteEndArray();
            w.WriteStartObject("loss");
            w.WriteString("classification", "lossless");
            w.WriteStartArray("fields");
            w.WriteEndArray();
            w.WriteEndObject();
            w.WriteStartArray("diagnostics");
            foreach (Diagnostic d in Diagnostics)
            {
                w.WriteStartObject();
                w.WriteString("code", d.Code);
                w.WriteString("severity", d.Severity.ToWireString());
                w.WriteString("path", d.Path);
                w.WriteString("message", d.Message);
                w.WriteEndObject();
            }
            w.WriteEndArray();
            w.WriteEndObject();
        }
        return Encoding.UTF8.GetString(buffer.ToArray());
    }

    internal static UtvResult Validate(UtvStatus status, IEnumerable<Diagnostic>? diagnostics = null) =>
        new("validate", status, diagnostics);

    internal static UtvResult Validate(UtvStatus status, Diagnostic diagnostic) =>
        new("validate", status, new[] { diagnostic });
}
