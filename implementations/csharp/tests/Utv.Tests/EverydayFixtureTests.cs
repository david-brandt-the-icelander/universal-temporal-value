using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Xunit;

namespace Utv.Tests;

/// <summary>
/// Runs every fixture of the shared file <c>conformance/operations/everyday.json</c> (read from the repository,
/// never copied) against the everyday API. Results are compared as JSON, ignoring key order.
/// </summary>
public class EverydayFixtureTests
{
    private static readonly Lazy<FixtureFile> File = new(FixtureFile.Load);

    public static IEnumerable<object[]> FixtureIds() => File.Value.Fixtures.Select(f => new object[] { f.Id });

    [Theory]
    [MemberData(nameof(FixtureIds))]
    public void FixtureHolds(string id)
    {
        string? mismatch = File.Value.Check(File.Value.Fixtures.Single(f => f.Id == id));
        Assert.True(mismatch is null, $"{id}: {mismatch}");
    }

    [Fact]
    public void EveryFixturePasses()
    {
        IReadOnlyList<Fixture> fixtures = File.Value.Fixtures;
        Assert.NotEmpty(fixtures);
        Assert.Equal(fixtures.Count, fixtures.Select(f => f.Id).Distinct(StringComparer.Ordinal).Count());
        string[] failures = fixtures
            .Select(f => (f.Id, Mismatch: File.Value.Check(f)))
            .Where(x => x.Mismatch is not null)
            .Select(x => $"{x.Id}: {x.Mismatch}")
            .ToArray();
        Assert.True(failures.Length == 0, $"{failures.Length}/{fixtures.Count} fixtures failed:\n" + string.Join("\n", failures));
    }

    [Fact]
    public void ExpectedValuesAreStructurallyValid()
    {
        foreach (Fixture f in File.Value.Fixtures)
        {
            foreach (JsonElement e in f.ExpectedCandidates.Where(e => e.ValueKind == JsonValueKind.Object && e.TryGetProperty("schema", out _)))
            {
                UtvResult r = UtvValidator.ValidateStructural(e.GetRawText());
                Assert.True(r.Status == UtvStatus.Valid, $"{f.Id}: {Fixtures.Describe(r)}");
            }
        }
    }

    [Fact]
    public void JsonComparisonIgnoresKeyOrderOnly()
    {
        using JsonDocument a = JsonDocument.Parse("""{"a":1,"b":[1,{"c":"x"}]}""");
        using JsonDocument b = JsonDocument.Parse("""{"b":[1,{"c":"x"}],"a":1.0}""");
        using JsonDocument c = JsonDocument.Parse("""{"b":[{"c":"x"},1],"a":1}""");
        using JsonDocument d = JsonDocument.Parse("""{"a":1,"b":[1,{"c":"x"}],"e":null}""");
        Assert.True(JsonEquality.Equal(a.RootElement, b.RootElement));
        Assert.False(JsonEquality.Equal(a.RootElement, c.RootElement));
        Assert.False(JsonEquality.Equal(a.RootElement, d.RootElement));
    }

    internal sealed record Fixture(string Id, string Op, JsonElement[] Args, string Status, JsonElement[] ExpectedCandidates);

    internal sealed class FixtureFile
    {
        private readonly Dictionary<string, ZoneRules> _rules;

        private FixtureFile(IReadOnlyList<Fixture> fixtures, Dictionary<string, ZoneRules> rules)
        {
            Fixtures = fixtures;
            _rules = rules;
        }

        public IReadOnlyList<Fixture> Fixtures { get; }

        public static FixtureFile Load()
        {
            using JsonDocument doc = JsonDocument.Parse(System.IO.File.ReadAllText(Utv.Tests.Fixtures.PathOf("conformance", "operations", "everyday.json")));
            JsonElement root = doc.RootElement;
            var rules = new Dictionary<string, ZoneRules>(StringComparer.Ordinal);
            foreach (JsonProperty r in root.GetProperty("rules").EnumerateObject())
            {
                string path = Utv.Tests.Fixtures.PathOf(r.Value.GetString()!.Split('/'));
                rules[r.Name] = ZoneRules.Parse(System.IO.File.ReadAllText(path));
            }
            Fixture[] fixtures = root.GetProperty("fixtures").EnumerateArray().Select(f =>
            {
                JsonElement expect = f.GetProperty("expect");
                JsonElement[] candidates = expect.TryGetProperty("result", out JsonElement one)
                    ? new[] { one.Clone() }
                    : expect.TryGetProperty("results", out JsonElement many)
                        ? many.EnumerateArray().Select(e => e.Clone()).ToArray()
                        : Array.Empty<JsonElement>();
                return new Fixture(
                    f.GetProperty("id").GetString()!,
                    f.GetProperty("op").GetString()!,
                    f.GetProperty("args").EnumerateArray().Select(a => a.Clone()).ToArray(),
                    expect.GetProperty("status").GetString()!,
                    candidates);
            }).ToArray();
            return new FixtureFile(fixtures, rules);
        }

        /// <summary>Null if the fixture holds, otherwise a description of the mismatch.</summary>
        public string? Check(Fixture f)
        {
            UtvResult result;
            try
            {
                result = Run(f);
            }
            catch (Exception e)
            {
                return $"threw {e.GetType().Name}: {e.Message}";
            }
            string status = result.Status.ToWireString();
            string actual = "[" + string.Join(",", result.Candidates.Select(c => c.GetRawText())) + "]";
            if (status != f.Status)
            {
                return $"expected status {f.Status}, got {status} {actual} ({Utv.Tests.Fixtures.Describe(result)})";
            }
            if (result.Candidates.Count != f.ExpectedCandidates.Length
                || !result.Candidates.Zip(f.ExpectedCandidates).All(p => JsonEquality.Equal(p.First, p.Second)))
            {
                string expected = "[" + string.Join(",", f.ExpectedCandidates.Select(c => c.GetRawText())) + "]";
                return $"expected candidates {expected}, got {actual}";
            }
            return null;
        }

        private UtvResult Run(Fixture f)
        {
            JsonElement[] a = f.Args;
            return f.Op switch
            {
                "parseIso" => Iso.ParseIso(a[0].GetString()!),
                "formatIso" => Iso.FormatIso(a[0]),
                "fields" => Calendar.Fields(a[0]),
                "add" => a.Length > 2 ? Calendar.Add(a[0], a[1], MonthEnd(a[2])) : Calendar.Add(a[0], a[1]),
                "compare" => Calendar.Compare(a[0], a[1]),
                "daysBetween" => Calendar.DaysBetween(a[0], a[1]),
                "posixSecondsBetween" => Calendar.PosixSecondsBetween(a[0], a[1]),
                "contains" => Ranges.Contains(a[0], a[1]),
                "overlaps" => Ranges.Overlaps(a[0], a[1]),
                "resolve" => Zones.Resolve(a[0], Rules(a[1]), a.Length > 2 ? Policy(a[2]) : null),
                "toZone" => Zones.ToZone(a[0], a[1].GetString()!, Rules(a[2])),
                "toOffset" => Zones.ToOffset(a[0], a[1].GetInt32()),
                _ => throw new InvalidOperationException($"unknown op `{f.Op}`"),
            };
        }

        private ZoneRules Rules(JsonElement name) => _rules[name.GetString()!];

        private static MonthEndPolicy MonthEnd(JsonElement policy) =>
            PolicyWire.TryParseMonthEnd(policy.GetProperty("monthEnd").GetString(), out MonthEndPolicy p)
                ? p
                : throw new InvalidOperationException($"bad monthEnd policy {policy.GetRawText()}");

        private static ResolutionPolicy Policy(JsonElement policy) =>
            ResolutionPolicy.TryParse(policy, out ResolutionPolicy p)
                ? p
                : throw new InvalidOperationException($"bad resolution policy {policy.GetRawText()}");
    }
}

/// <summary>JSON equality ignoring object key order; numbers compare by value.</summary>
internal static class JsonEquality
{
    public static bool Equal(JsonElement a, JsonElement b)
    {
        if (a.ValueKind != b.ValueKind)
        {
            return false;
        }
        switch (a.ValueKind)
        {
            case JsonValueKind.Object:
                var pa = a.EnumerateObject().ToDictionary(p => p.Name, p => p.Value, StringComparer.Ordinal);
                var pb = b.EnumerateObject().ToDictionary(p => p.Name, p => p.Value, StringComparer.Ordinal);
                return pa.Count == pb.Count && pa.All(kv => pb.TryGetValue(kv.Key, out JsonElement v) && Equal(kv.Value, v));
            case JsonValueKind.Array:
                return a.GetArrayLength() == b.GetArrayLength() && a.EnumerateArray().Zip(b.EnumerateArray()).All(p => Equal(p.First, p.Second));
            case JsonValueKind.String:
                return a.GetString() == b.GetString();
            case JsonValueKind.Number:
                return a.TryGetDecimal(out decimal da) && b.TryGetDecimal(out decimal db) ? da == db : a.GetRawText() == b.GetRawText();
            default:
                return true; // true, false, null: same kind is same value
        }
    }
}
