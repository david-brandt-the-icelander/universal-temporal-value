using System.Text.Json;
using Xunit;

namespace Utv.Tests;

public class EnvelopeTests
{
    private const string Posix =
        """{"schema":"utv/1.0","kind":"instant","value":{"system":{"system":"utv.timescale","id":"unix-posix","version":"1"},"ticks":"0"}}""";

    private static UtvResult Check(string json, UtvStatus expected)
    {
        UtvResult result = UtvValidator.ValidateStructural(json);
        Assert.True(result.Status == expected, $"expected {expected.ToWireString()}, got {result.Status.ToWireString()}: {Fixtures.Describe(result)}");
        Assert.Equal("validate", result.Operation);
        return result;
    }

    private static string WithSchema(string schemaJson) =>
        Posix.Replace("\"schema\":\"utv/1.0\"", "\"schema\":" + schemaJson, System.StringComparison.Ordinal);

    [Fact]
    public void BaselineIsValid()
    {
        UtvResult result = Check(Posix, UtvStatus.Valid);
        Assert.Empty(result.Diagnostics);
    }

    [Theory]
    [InlineData("utv/1.1")]
    [InlineData("utv/1.3")]
    [InlineData("utv/1.12")]
    [InlineData("utv/2.0")]
    [InlineData("utv/0.9")]
    [InlineData("utv/0.0")]
    [InlineData("utv/10.3")]
    [InlineData("utv/123456789012345678901234567890.0")]
    public void OtherWellFormedVersionsAreUnsupported(string version)
    {
        UtvResult result = Check(WithSchema(JsonSerializer.Serialize(version)), UtvStatus.Unsupported);
        Diagnostic d = Assert.Single(result.Diagnostics);
        Assert.Equal("unsupported-version", d.Code);
        Assert.Equal("/schema", d.Path);
    }

    [Fact]
    public void UnsupportedVersionIsReportedBeforeUnknownVocabulary()
    {
        // A newer version may add kinds and properties; the reader must not call them invalid.
        Check("""{"schema":"utv/1.1","kind":"future-kind","value":{},"newProperty":true}""", UtvStatus.Unsupported);
    }

    [Theory]
    [InlineData("\"utv/4\"")]
    [InlineData("\"utv/2\"")]
    [InlineData("\"utv/1\"")]
    [InlineData("\"utv/1.01\"")]
    [InlineData("\"utv/01.0\"")]
    [InlineData("\"utv/1.0 \"")]
    [InlineData("\" utv/1.0\"")]
    [InlineData("\"utv/1.0\\n\"")]
    [InlineData("\"utv/1.1\\n\"")]
    [InlineData("\"UTV/1.0\"")]
    [InlineData("\"utv/1.0.0\"")]
    [InlineData("\"utv/-1.0\"")]
    [InlineData("\"utv/\u0661.0\"")]
    [InlineData("\"utv-result/1.0\"")]
    [InlineData("\"\"")]
    [InlineData("1")]
    [InlineData("1.0")]
    [InlineData("null")]
    [InlineData("[\"utv/1.0\"]")]
    public void MalformedVersionsAreInvalid(string schemaJson)
    {
        UtvResult result = Check(WithSchema(schemaJson), UtvStatus.Invalid);
        Assert.Equal("unknown-schema", Assert.Single(result.Diagnostics).Code);
    }

    [Fact]
    public void MissingVersionIsInvalid()
    {
        UtvResult result = Check("""{"kind":"instant","value":{}}""", UtvStatus.Invalid);
        Assert.Equal("/schema", Assert.Single(result.Diagnostics).Path);
    }

    [Fact]
    public void UnknownTopLevelPropertyIsInvalid()
    {
        UtvResult result = Check(Posix.Replace("\"value\":", "\"zzz\":1,\"value\":", System.StringComparison.Ordinal), UtvStatus.Invalid);
        Diagnostic d = Assert.Single(result.Diagnostics);
        Assert.Equal(("unknown-property", "/zzz"), (d.Code, d.Path));
    }

    [Fact]
    public void ProvenanceAndExtensionsAreAllowed()
    {
        Check(Posix.Replace("\"value\":", "\"provenance\":{},\"extensions\":{},\"value\":", System.StringComparison.Ordinal), UtvStatus.Valid);
    }

    [Theory]
    [InlineData("\"moment\"")]
    [InlineData("\"Instant\"")]
    [InlineData("\"\"")]
    [InlineData("1")]
    [InlineData("null")]
    public void KindsOutsideTheCoreAreInvalid(string kindJson)
    {
        UtvResult result = Check(Posix.Replace("\"instant\"", kindJson, System.StringComparison.Ordinal), UtvStatus.Invalid);
        Assert.Equal(("unknown-kind", "/kind"), (result.Diagnostics[0].Code, result.Diagnostics[0].Path));
    }

    [Fact]
    public void MissingKindAndValueAreInvalid()
    {
        Check("""{"schema":"utv/1.0","value":{}}""", UtvStatus.Invalid);
        UtvResult result = Check("""{"schema":"utv/1.0","kind":"instant"}""", UtvStatus.Invalid);
        Assert.Contains(result.Diagnostics, d => d.Code == "missing-property" && d.Path == "/value");
    }

    [Theory]
    [InlineData("""{"schema":"utv/1.0","schema":"utv/1.0","kind":"instant","value":{}}""", "/schema")]
    [InlineData("""{"schema":"utv/1.0","kind":"instant","value":{"system":{"system":"a","id":"b","id":"b","version":"1"},"ticks":"0"}}""", "/value/system/id")]
    [InlineData("""{"schema":"utv/1.0","kind":"instant","value":{},"provenance":{"x":[{"a":1,"\u0061":2}]}}""", "/provenance/x/0/a")]
    [InlineData("""{"schema":"utv/2.0","schema":"utv/2.0"}""", "/schema")]
    public void DuplicateKeysAreInvalidAtEveryDepth(string json, string path)
    {
        UtvResult result = Check(json, UtvStatus.Invalid);
        Diagnostic d = Assert.Single(result.Diagnostics);
        Assert.Equal("duplicate-key", d.Code);
        Assert.Equal(path, d.Path);
    }

    [Theory]
    [InlineData("")]
    [InlineData("{")]
    [InlineData("[]")]
    [InlineData("\"utv/1.0\"")]
    [InlineData("null")]
    [InlineData("NaN")]
    [InlineData("{\"schema\":\"utv/1.0\",}")]
    [InlineData("{\"schema\":\"utv/1.0\"} {}")]
    [InlineData("// comment\n{}")]
    [InlineData("{'schema':'utv/1.0'}")]
    [InlineData("{\"schema\":\"\\ud800\"}")]
    [InlineData("{\"schema\":\"utv/1.0\",\"kind\":\"instant\",\"value\":{\"ticks\":01}}")]
    public void MalformedJsonIsInvalid(string json)
    {
        Check(json, UtvStatus.Invalid);
    }

    [Fact]
    public void ExcessiveNestingIsInvalidNotACrash()
    {
        string deep = new string('[', 10_000) + new string(']', 10_000);
        Check("{\"schema\":\"utv/1.0\",\"kind\":\"instant\",\"value\":" + deep + "}", UtvStatus.Invalid);
    }

    [Theory]
    [InlineData("\"ticks\":\"-0\"", "pattern-mismatch")]
    [InlineData("\"ticks\":\"007\"", "pattern-mismatch")]
    [InlineData("\"ticks\":\"0\\n\"", "pattern-mismatch")]
    [InlineData("\"ticks\":0", "wrong-type")]
    [InlineData("\"ticks\":\"0\",\"coordinate\":{\"numerator\":\"1\",\"denominator\":\"2\"}", "schema-violation")]
    [InlineData("\"coordinate\":{\"numerator\":\"1\",\"denominator\":\"0\"}", "pattern-mismatch")]
    [InlineData("\"ticks\":\"0\",\"quality\":{\"precision\":1}", "wrong-type")]
    public void NestedStructureIsChecked(string valueMembers, string code)
    {
        string json = """{"schema":"utv/1.0","kind":"instant","value":{"system":{"system":"utv.timescale","id":"unix-posix","version":"1"},""" + valueMembers + "}}";
        UtvResult result = Check(json, UtvStatus.Invalid);
        Assert.Contains(result.Diagnostics, d => d.Code == code);
    }

    [Fact]
    public void RuleRefChecksMinLengthAndDigestPattern()
    {
        Check(Posix.Replace("\"id\":\"unix-posix\"", "\"id\":\"\"", System.StringComparison.Ordinal), UtvStatus.Invalid);
        Check(Posix.Replace("\"version\":\"1\"", "\"version\":\"1\",\"sha256\":\"" + new string('a', 64) + "\"", System.StringComparison.Ordinal), UtvStatus.Valid);
        Check(Posix.Replace("\"version\":\"1\"", "\"version\":\"1\",\"sha256\":\"" + new string('a', 63) + "\"", System.StringComparison.Ordinal), UtvStatus.Invalid);
        // minLength counts code points: one astral character is one character.
        Check(Posix.Replace("\"id\":\"unix-posix\"", "\"id\":\"\\ud83d\\ude00\"", System.StringComparison.Ordinal), UtvStatus.Valid);
    }

    [Theory]
    [InlineData("2", UtvStatus.Valid)]
    [InlineData("2.0", UtvStatus.Valid)]
    [InlineData("0.2e1", UtvStatus.Valid)]
    [InlineData("1e400", UtvStatus.Valid)]
    [InlineData("2.5", UtvStatus.Invalid)]
    [InlineData("1e-400", UtvStatus.Invalid)]
    [InlineData("\"2\"", UtvStatus.Invalid)]
    public void IntegerTypeFollowsJsonSchemaSemantics(string month, UtvStatus expected)
    {
        Check("""{"schema":"utv/1.0","kind":"civil","value":{"form":"year-month","fields":{"year":"2026","month":""" + month + """},"calendar":{"id":"gregory","chronology":"proleptic"}}}""", expected);
    }

    [Fact]
    public void NestedValuesMustCarryTheSameVersion()
    {
        string valid = Fixtures.Examples("valid.json")["civil-date-range"];
        Assert.Equal(UtvStatus.Valid, UtvValidator.ValidateStructural(valid).Status);
        int first = valid.IndexOf("utv/1.0", System.StringComparison.Ordinal);
        int nested = valid.IndexOf("utv/1.0", first + 1, System.StringComparison.Ordinal);
        Assert.True(nested > first);
        string mixed = valid.Substring(0, nested) + "utv/1.1" + valid.Substring(nested + "utv/1.0".Length);
        // The nested value sits in an interval boundary (a oneOf), so it is reported at the boundary.
        UtvResult result = Check(mixed, UtvStatus.Invalid);
        Assert.Contains(result.Diagnostics, d => d.Code == "schema-violation" && d.Path.StartsWith("/value/", System.StringComparison.Ordinal));
    }

    [Fact]
    public void DiagnosticPathsEscapeJsonPointerTokens()
    {
        UtvResult result = Check(Posix.Replace("\"value\":", "\"a/b~c\":1,\"value\":", System.StringComparison.Ordinal), UtvStatus.Invalid);
        Assert.Equal("/a~1b~0c", Assert.Single(result.Diagnostics).Path);
    }

    [Fact]
    public void WellFormedVersionPredicate()
    {
        Assert.True(UtvValidator.IsWellFormedVersion("utv/1.0"));
        Assert.True(UtvValidator.IsWellFormedVersion("utv/0.0"));
        Assert.False(UtvValidator.IsWellFormedVersion("utv/1.0\n"));
        Assert.False(UtvValidator.IsWellFormedVersion("utv/1"));
        Assert.Equal(UtvStatus.Invalid, UtvValidator.ValidateStructural("{}").Status);
        Assert.Equal(UtvStatus.Unsupported, UtvValidator.ValidateStructural("""{"schema":"utv/1.3"}""").Status);
        Assert.Throws<System.ArgumentNullException>(() => UtvValidator.ValidateStructural(null!));
    }
}
