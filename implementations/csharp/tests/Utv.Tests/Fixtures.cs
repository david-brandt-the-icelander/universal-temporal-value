using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;

namespace Utv.Tests;

/// <summary>Access to the shared conformance material at the repository root.</summary>
internal static class Fixtures
{
    public static string RepositoryRoot { get; } = typeof(Fixtures).Assembly
        .GetCustomAttributes<AssemblyMetadataAttribute>()
        .Single(a => a.Key == "UtvRepositoryRoot").Value
        ?? throw new InvalidOperationException("UtvRepositoryRoot metadata is missing");

    public static string PathOf(params string[] parts) => Path.Combine(new[] { RepositoryRoot }.Concat(parts).ToArray());

    /// <summary>The named examples of a conformance file, each as its raw JSON text.</summary>
    public static IReadOnlyDictionary<string, string> Examples(string fileName)
    {
        string text = File.ReadAllText(PathOf("conformance", "examples", fileName));
        using JsonDocument doc = JsonDocument.Parse(text);
        return doc.RootElement.EnumerateObject().ToDictionary(p => p.Name, p => p.Value.GetRawText(), StringComparer.Ordinal);
    }

    public static IEnumerable<object[]> ExampleNames(string fileName) =>
        Examples(fileName).Keys.Select(name => new object[] { name });

    public static string Describe(UtvResult result) =>
        string.Join("; ", result.Diagnostics.Select(d => $"{d.Code} at '{d.Path}': {d.Message}"));
}
