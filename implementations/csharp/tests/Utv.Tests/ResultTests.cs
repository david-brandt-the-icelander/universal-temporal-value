using System;
using System.Linq;
using System.Text.Json;
using Xunit;

namespace Utv.Tests;

public class ResultTests
{
    [Fact]
    public void StatusWireStrings()
    {
        string[] expected =
        {
            "invalid", "unsupported", "indeterminate", "none", "unique", "multiple",
            "range", "symbolic", "resource-limit", "error", "valid",
        };
        UtvStatus[] all = Enum.GetValues<UtvStatus>();
        Assert.Equal(11, all.Length);
        Assert.Equal(expected, all.Select(s => s.ToWireString()).ToArray());
        foreach (UtvStatus status in all)
        {
            Assert.True(UtvStatusExtensions.TryParseWireString(status.ToWireString(), out UtvStatus parsed));
            Assert.Equal(status, parsed);
        }
        Assert.False(UtvStatusExtensions.TryParseWireString("Valid", out _));
        Assert.False(UtvStatusExtensions.TryParseWireString("ResourceLimit", out _));
        Assert.False(UtvStatusExtensions.TryParseWireString(null, out _));
    }

    [Fact]
    public void ToJsonEmitsTheResultEnvelope()
    {
        UtvResult result = UtvValidator.ValidateStructural("""{"schema":"utv/1.0","kind":"moment","value":{}}""");
        using JsonDocument doc = JsonDocument.Parse(result.ToJson());
        JsonElement root = doc.RootElement;
        Assert.Equal(
            new[] { "schema", "operation", "status", "candidates", "manifest", "assumptions", "loss", "diagnostics" },
            root.EnumerateObject().Select(p => p.Name).ToArray());
        Assert.Equal("utv-result/1.0", root.GetProperty("schema").GetString());
        Assert.Equal("validate", root.GetProperty("operation").GetString());
        Assert.Equal("invalid", root.GetProperty("status").GetString());
        Assert.Equal(0, root.GetProperty("candidates").GetArrayLength());
        Assert.Equal(0, root.GetProperty("manifest").GetProperty("dependencies").GetArrayLength());
        Assert.Equal(0, root.GetProperty("assumptions").GetArrayLength());
        Assert.Equal("lossless", root.GetProperty("loss").GetProperty("classification").GetString());
        JsonElement diagnostic = Assert.Single(root.GetProperty("diagnostics").EnumerateArray());
        Assert.Equal("unknown-kind", diagnostic.GetProperty("code").GetString());
        Assert.Equal("error", diagnostic.GetProperty("severity").GetString());
        Assert.Equal("/kind", diagnostic.GetProperty("path").GetString());
        Assert.Contains("moment", diagnostic.GetProperty("message").GetString(), StringComparison.Ordinal);
        Assert.Equal(UtvResult.ResultSchema, result.Schema);
    }

    [Fact]
    public void CandidatesAreClonedAndSerialized()
    {
        string posix = Fixtures.Examples("valid.json")["posix-instant"];
        UtvResult result;
        using (JsonDocument doc = JsonDocument.Parse(posix))
        {
            result = new UtvResult("resolve", UtvStatus.Unique, candidates: new[] { doc.RootElement });
        }
        // The source document is disposed; the candidate must still be readable.
        Assert.Equal("instant", result.Candidates[0].GetProperty("kind").GetString());
        using JsonDocument json = JsonDocument.Parse(result.ToJson());
        Assert.Equal("unique", json.RootElement.GetProperty("status").GetString());
        Assert.Equal("utv/1.0", json.RootElement.GetProperty("candidates")[0].GetProperty("schema").GetString());
    }

    [Fact]
    public void StatusInvariantsAreEnforced()
    {
        using JsonDocument doc = JsonDocument.Parse("""{"schema":"utv/1.0"}""");
        Assert.Throws<ArgumentException>(() => new UtvResult("resolve", UtvStatus.None, candidates: new[] { doc.RootElement }));
        Assert.Throws<ArgumentException>(() => new UtvResult("resolve", UtvStatus.Unique));
        Assert.Throws<ArgumentException>(() => new UtvResult("", UtvStatus.Valid));
        Assert.Throws<ArgumentOutOfRangeException>(() => new UtvResult("validate", (UtvStatus)99));
        Assert.Empty(new UtvResult("resolve", UtvStatus.None).Candidates);
    }

    [Fact]
    public void DiagnosticIsAValueRecord()
    {
        var a = new Diagnostic("c", DiagnosticSeverity.Warning, "/x", "m");
        Assert.Equal(a, new Diagnostic("c", DiagnosticSeverity.Warning, "/x", "m"));
        Assert.Equal("warning", a.Severity.ToWireString());
        Assert.Equal("info", DiagnosticSeverity.Info.ToWireString());
    }
}
