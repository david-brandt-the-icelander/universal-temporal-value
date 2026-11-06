using System.Collections.Generic;
using Xunit;

namespace Utv.Tests;

public class ConformanceTests
{
    public static IEnumerable<object[]> ValidNames() => Fixtures.ExampleNames("valid.json");

    public static IEnumerable<object[]> InvalidNames() => Fixtures.ExampleNames("invalid-structural.json");

    [Theory]
    [MemberData(nameof(ValidNames))]
    public void ValidExampleIsValid(string name)
    {
        UtvResult result = UtvValidator.ValidateStructural(Fixtures.Examples("valid.json")[name]);
        Assert.True(result.Status == UtvStatus.Valid, $"{name}: {result.Status.ToWireString()}: {Fixtures.Describe(result)}");
        Assert.Empty(result.Diagnostics);
    }

    [Theory]
    [MemberData(nameof(InvalidNames))]
    public void InvalidStructuralExampleIsInvalid(string name)
    {
        UtvResult result = UtvValidator.ValidateStructural(Fixtures.Examples("invalid-structural.json")[name]);
        Assert.Equal(UtvStatus.Invalid, result.Status);
        Assert.NotEmpty(result.Diagnostics);
    }

    [Fact]
    public void FixtureFilesAreNotEmpty()
    {
        Assert.True(Fixtures.Examples("valid.json").Count >= 31);
        Assert.True(Fixtures.Examples("invalid-structural.json").Count >= 8);
    }

    [Theory]
    [InlineData("unknown-kind", "unknown-kind", "/kind")]
    [InlineData("ordinal-missing-system", "missing-property", "/value/system")]
    [InlineData("unknown-core-property", "unknown-property", "/value/unrecognized")]
    [InlineData("unreleased-draft-schema-identifier", "unknown-schema", "/schema")]
    public void InvalidExamplesReportTheCause(string name, string code, string path)
    {
        UtvResult result = UtvValidator.ValidateStructural(Fixtures.Examples("invalid-structural.json")[name]);
        Assert.Contains(result.Diagnostics, d => d.Code == code && d.Path == path);
    }
}
