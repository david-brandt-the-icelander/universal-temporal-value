using System;
using System.IO;
using Utv.Examples;
using Xunit;

namespace Utv.Tests;

/// <summary>Runs the Everyday example. Only UTV outcomes are checked; native output depends on the host.</summary>
public class EverydayExampleTests
{
    private static readonly string RulesPath = Fixtures.PathOf("conformance", "rules", "demo-zones.json");

    [Fact]
    public void ExampleRunsWithEveryUtvOutcomeAsExpected()
    {
        using var output = new StringWriter();
        Assert.Equal(0, EverydayTour.Run(output, RulesPath));
        string text = output.ToString();
        Assert.DoesNotContain("MISMATCH", text, StringComparison.Ordinal);
        Assert.Contains("UTV outcomes checked, all as expected.", text, StringComparison.Ordinal);
        for (int section = 1; section <= 14; section++)
        {
            Assert.Contains($"== {section}. ", text, StringComparison.Ordinal);
        }
        Assert.Contains("Zones.Resolve(02:30 New York) → none (gap)", text, StringComparison.Ordinal);
        Assert.Contains("multiple 2026-11-01T05:30:00Z, 2026-11-01T06:30:00Z (ambiguous)", text, StringComparison.Ordinal);
    }

    [Fact]
    public void ExampleRunsWithoutAHostTimeZoneDatabase()
    {
        using var output = new StringWriter();
        Assert.Equal(0, EverydayTour.Run(output, RulesPath, _ => null));
        Assert.Contains("FindSystemTimeZoneById(\"America/New_York\") → not available on this host", output.ToString(), StringComparison.Ordinal);
    }
}
