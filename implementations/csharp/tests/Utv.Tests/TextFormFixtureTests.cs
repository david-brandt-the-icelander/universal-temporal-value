using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Xunit;

namespace Utv.Tests;

/// <summary>
/// Runs every fixture of the shared file <c>conformance/operations/text-form.json</c> (read from the repository)
/// against the JSON-level <see cref="TextForm.Parse"/> and <see cref="TextForm.Format"/>.
/// </summary>
public class TextFormFixtureTests
{
    private static readonly Lazy<JsonElement[]> All = new(() =>
    {
        using JsonDocument doc = JsonDocument.Parse(System.IO.File.ReadAllText(Fixtures.PathOf("conformance", "operations", "text-form.json")));
        return doc.RootElement.GetProperty("fixtures").EnumerateArray().Select(f => f.Clone()).ToArray();
    });

    public static IEnumerable<object[]> FixtureIds() => All.Value.Select(f => new object[] { f.GetProperty("id").GetString()! });

    [Theory]
    [MemberData(nameof(FixtureIds))]
    public void FixtureHolds(string id)
    {
        string? mismatch = Check(All.Value.Single(f => f.GetProperty("id").GetString() == id));
        Assert.True(mismatch is null, $"{id}: {mismatch}");
    }

    [Fact]
    public void EveryFixturePasses()
    {
        Assert.NotEmpty(All.Value);
        string[] failures = All.Value
            .Select(f => (Id: f.GetProperty("id").GetString(), Mismatch: Check(f)))
            .Where(x => x.Mismatch is not null)
            .Select(x => $"{x.Id}: {x.Mismatch}")
            .ToArray();
        Assert.True(failures.Length == 0, $"{failures.Length}/{All.Value.Length} text-form fixtures failed:\n" + string.Join("\n", failures));
    }

    /// <summary>Null if the fixture holds, otherwise a description of the mismatch.</summary>
    private static string? Check(JsonElement f)
    {
        string direction = f.GetProperty("direction").GetString()!;
        JsonElement? expect = f.TryGetProperty("expect", out JsonElement e) ? e : null;
        string? problem = null;
        if (direction is "both" or "parse")
        {
            UtvResult parsed = TextForm.Parse(f.GetProperty("text").GetString()!);
            problem = expect is JsonElement x
                ? Compare(parsed, x, x.TryGetProperty("value", out JsonElement v) ? v : null)
                : Compare(parsed, null, f.GetProperty("value"));
        }
        if (problem is null && direction is "both" or "format")
        {
            UtvResult formatted = TextForm.Format(f.GetProperty("value"));
            if (expect is JsonElement x)
            {
                problem = Compare(formatted, x, x.TryGetProperty("text", out JsonElement t) ? t : null);
            }
            else
            {
                problem = formatted.IsUnique && formatted.GetString() == f.GetProperty("text").GetString()
                    ? null
                    : $"format: expected `{f.GetProperty("text").GetString()}`, got {Describe(formatted)}";
            }
        }
        return problem;
    }

    private static string? Compare(UtvResult actual, JsonElement? expect, JsonElement? value)
    {
        string status = expect is JsonElement x ? x.GetProperty("status").GetString()! : "unique";
        if (actual.Status.ToWireString() != status)
        {
            return $"{actual.Operation}: expected status {status}, got {Describe(actual)}";
        }
        if (value is JsonElement v && !JsonEquality.Equal(actual.Value, v))
        {
            return $"{actual.Operation}: expected {v.GetRawText()}, got {actual.Value.GetRawText()}";
        }
        if (expect is JsonElement y && y.TryGetProperty("diagnostics", out JsonElement codes))
        {
            string[] missing = codes.EnumerateArray().Select(c => c.GetString()!)
                .Where(code => !actual.Diagnostics.Any(d => d.Code == code)).ToArray();
            if (missing.Length > 0)
            {
                return $"{actual.Operation}: expected diagnostic code(s) {string.Join(", ", missing)}, got {Describe(actual)}";
            }
        }
        return null;
    }

    private static string Describe(UtvResult r) =>
        $"{r.Status.ToWireString()} [{string.Join(", ", r.Candidates.Select(c => c.GetRawText()))}] ({string.Join(", ", r.Diagnostics.Select(d => d.Code))})";
}
