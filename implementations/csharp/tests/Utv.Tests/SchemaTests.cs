using System.IO;
using System.Linq;
using Utv.Internal;
using Xunit;

namespace Utv.Tests;

public class SchemaTests
{
    [Fact]
    public void EmbeddedSchemaIsTheRepositoryFile()
    {
        string repository = File.ReadAllText(Fixtures.PathOf("schema", "utv-1.0.schema.json"));
        Assert.Equal(repository, SchemaInterpreter.ReadEmbeddedText());
    }

    [Fact]
    public void InterpreterLoadsTheSchemaAndKnowsEveryKeyword()
    {
        // Loading throws if the schema uses a keyword the interpreter does not implement.
        Assert.NotNull(SchemaInterpreter.Instance.Root);
    }

    [Fact]
    public void ValueDispatchIsEquivalentToTheSchemaOneOf()
    {
        // The interpreter dispatches #/$defs/value (and the root) on `kind`. That is only equivalent to the
        // schema's oneOf if both list exactly the 26 `<kind>Value` schemas and each pins `kind` with const.
        JObject root = SchemaInterpreter.Instance.Root;
        var defs = (JObject)root.Get("$defs")!;
        string[] expected = UtvValidator.CoreKinds.Select(k => "#/$defs/" + k + "Value").ToArray();
        foreach (JObject holder in new[] { root, (JObject)defs.Get("value")! })
        {
            string[] refs = ((JArray)holder.Get("oneOf")!).Items
                .Select(b => ((JString)((JObject)b).Get("$ref")!).Value).ToArray();
            Assert.Equal(expected.OrderBy(x => x, System.StringComparer.Ordinal), refs.OrderBy(x => x, System.StringComparer.Ordinal));
        }
        foreach (string kind in UtvValidator.CoreKinds)
        {
            var kindSchema = (JObject)defs.Get(kind + "Value")!;
            var kindProperty = (JObject)((JObject)kindSchema.Get("properties")!).Get("kind")!;
            Assert.Equal(kind, ((JString)kindProperty.Get("const")!).Value);
            Assert.Contains(((JArray)kindSchema.Get("required")!).Items, n => ((JString)n).Value == "kind");
        }
    }

    [Fact]
    public void CoreKindsAreTheTwentySix()
    {
        Assert.Equal(26, UtvValidator.CoreKinds.Count);
        Assert.Equal(26, UtvValidator.CoreKinds.Distinct().Count());
        Assert.True(UtvValidator.IsCoreKind("logical-clock"));
        Assert.False(UtvValidator.IsCoreKind("Instant"));
    }

    [Theory]
    [InlineData("^(0|-?[1-9][0-9]*)$", "0", true)]
    [InlineData("^(0|-?[1-9][0-9]*)$", "-12", true)]
    [InlineData("^(0|-?[1-9][0-9]*)$", "-0", false)]
    [InlineData("^(0|-?[1-9][0-9]*)$", "012", false)]
    [InlineData("^(0|-?[1-9][0-9]*)$", "0\n", false)]
    [InlineData("^(0|-?[1-9][0-9]*)$", "1\n2", false)]
    [InlineData("^(0|-?[1-9][0-9]*)$", "\u0661", false)]
    [InlineData("^[0-9a-f]{64}$", "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef", true)]
    [InlineData("^[0-9a-f]{64}$", "0123456789ABCDEF0123456789abcdef0123456789abcdef0123456789abcdef", false)]
    [InlineData("a[$]", "a$", true)]
    [InlineData(@"a\$", "a$", true)]
    public void PatternsFollowEcmaScriptAnchoring(string pattern, string input, bool matches)
    {
        Assert.Equal(matches, SchemaInterpreter.CompilePattern(pattern).IsMatch(input));
    }
}
