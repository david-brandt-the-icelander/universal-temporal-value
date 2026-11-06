using System.Text.Json;
using Xunit;

namespace Utv.Examples.Tests.NativeFirst.Text.Json;

/// <summary>
/// What .NET's <c>System.Text.Json.JsonDocument</c> does with temporal JSON, compared with UTV's
/// <c>UtvValidator.ValidateStructural</c>.
/// </summary>
public class JsonDocumentTests
{
    #region Parse

    /// <summary>
    /// Verifies that JsonDocument.Parse accepts duplicate keys and GetProperty reads the last one.
    /// Compared with: <c>UtvValidator.ValidateStructural</c> rejects the same text as invalid (duplicate-key): two
    /// months in one date is a contradiction, not something to settle by taking the last.
    /// </summary>
    [Fact]
    public void Test_Parse_KeepsTheLastValue_WhenAKeyAppearsTwice()
    {
        // GIVEN: a UTV date with two different months
        string givenJson = """
            {"schema":"utv/1.0","kind":"civil","value":{"form":"date","fields":{"year":"2026","month":11,"month":12,"day":5},"calendar":{"id":"gregory","chronology":"proleptic"}}}
            """;

        // WHEN
        using JsonDocument actualDocument = JsonDocument.Parse(givenJson);
        int actualMonth = actualDocument.RootElement.GetProperty("value").GetProperty("fields").GetProperty("month").GetInt32();

        // EXPECTATIONS: the second month, December
        int expectedMonth = 12;

        // THEN
        Assert.Equal(expectedMonth, actualMonth);
    }

    #endregion
}
