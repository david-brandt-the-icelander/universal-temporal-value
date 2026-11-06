using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Utv.Examples;
using Xunit;

namespace Utv.Tests;

/// <summary>Every expectation in the shared catalog <c>examples/traditional.json</c> holds for this library.</summary>
public class TraditionalCatalogTests
{
    private static readonly IReadOnlyList<CatalogEntry> Entries =
        TraditionalCatalog.Load(Fixtures.PathOf("examples", "traditional.json"));

    public static IEnumerable<object[]> EntryIds() => Entries.Select(e => new object[] { e.Id });

    [Theory]
    [MemberData(nameof(EntryIds))]
    public void EntryHasExpectedOutcomes(string id)
    {
        CatalogEntry entry = Entries.Single(e => e.Id == id);
        foreach (CheckOutcome outcome in TraditionalCatalog.Check(entry))
        {
            Assert.True(outcome.Matches, $"{id}: {outcome.Check} expected {outcome.Expected}, got {outcome.Actual}");
        }
    }

    [Fact]
    public void EntryIdsAreUnique()
    {
        Assert.NotEmpty(Entries);
        Assert.Equal(Entries.Count, Entries.Select(e => e.Id).Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public void TourReportsNoMismatches()
    {
        using var output = new StringWriter();
        Assert.Equal(0, TraditionalCatalog.Tour(Entries, output));
        string[] lines = output.ToString().TrimEnd().Split('\n');
        Assert.Equal($"{Entries.Count} examples, every outcome as expected.", lines[^1].TrimEnd('\r'));
    }

    [Fact]
    public void TourReportsAMismatch()
    {
        CatalogEntry wrong = Entries.Single(e => e.Id == "february-30") with { ExpectGregorian = "valid" };
        using var output = new StringWriter();
        Assert.Equal(1, TraditionalCatalog.Tour(new[] { wrong }, output));
        string text = output.ToString();
        Assert.Contains("  MISMATCH: gregorian expected valid, got invalid", text, StringComparison.Ordinal);
        Assert.EndsWith("1 mismatches.", text.TrimEnd(), StringComparison.Ordinal);
    }
}
