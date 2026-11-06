using System;

namespace Utv;

/// <summary>Severity of a <see cref="Diagnostic"/>.</summary>
public enum DiagnosticSeverity
{
    /// <summary><c>info</c></summary>
    Info,
    /// <summary><c>warning</c></summary>
    Warning,
    /// <summary><c>error</c></summary>
    Error,
}

/// <summary>Wire-string conversions for <see cref="DiagnosticSeverity"/>.</summary>
public static class DiagnosticSeverityExtensions
{
    /// <summary>The severity as it appears on the wire: <c>error</c>, <c>warning</c> or <c>info</c>.</summary>
    public static string ToWireString(this DiagnosticSeverity severity) => severity switch
    {
        DiagnosticSeverity.Info => "info",
        DiagnosticSeverity.Warning => "warning",
        DiagnosticSeverity.Error => "error",
        _ => throw new ArgumentOutOfRangeException(nameof(severity), severity, "not a diagnostic severity"),
    };
}

/// <summary>
/// One result diagnostic, <c>{code, severity, path, message}</c> (spec/RESULT-CONTRACT.md).
/// </summary>
/// <param name="Code">A stable machine-readable code, for example <c>unknown-property</c>.</param>
/// <param name="Severity">How serious the finding is.</param>
/// <param name="Path">An RFC 6901 JSON Pointer into the input; the empty string is the whole document.</param>
/// <param name="Message">Human-readable explanation (English, culture-invariant).</param>
public sealed record Diagnostic(string Code, DiagnosticSeverity Severity, string Path, string Message)
{
    /// <summary>The message followed by the code, for example <c>2026-02 has no day 31 (month-end)</c>.</summary>
    public override string ToString() => Message.Length == 0 ? Code : Message + " (" + Code + ")";

    internal static Diagnostic Error(string code, string path, string message) =>
        new(code, DiagnosticSeverity.Error, path, message);
}
