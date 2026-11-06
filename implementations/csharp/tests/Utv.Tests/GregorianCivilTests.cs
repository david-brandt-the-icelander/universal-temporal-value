using System.Numerics;
using System.Text.Json;
using Xunit;

namespace Utv.Tests;

public class GregorianCivilTests
{
    private static string Civil(string fieldsJson, string form = "date", string calendar = "gregory", string chronology = "proleptic") =>
        "{\"schema\":\"utv/1.0\",\"kind\":\"civil\",\"value\":{\"form\":" + JsonSerializer.Serialize(form)
        + ",\"fields\":" + fieldsJson
        + ",\"calendar\":{\"id\":" + JsonSerializer.Serialize(calendar) + ",\"chronology\":" + JsonSerializer.Serialize(chronology) + "}}}";

    private static string Date(string year, int month, int day) =>
        Civil("{\"year\":" + JsonSerializer.Serialize(year) + ",\"month\":" + month + ",\"day\":" + day + "}");

    [Theory]
    [InlineData("2000", true)]
    [InlineData("2024", true)]
    [InlineData("0", true)]
    [InlineData("-4", true)]
    [InlineData("-400", true)]
    [InlineData("-100", false)]
    [InlineData("1900", false)]
    [InlineData("2026", false)]
    [InlineData("-1", false)]
    [InlineData("123456789012345678800", true)]
    [InlineData("123456789012345678900", false)]
    public void LeapRule(string year, bool leap)
    {
        Assert.Equal(leap, GregorianCivil.IsLeapYear(BigInteger.Parse(year, System.Globalization.CultureInfo.InvariantCulture)));
    }

    [Fact]
    public void DaysInMonth()
    {
        Assert.Equal(28, GregorianCivil.DaysInMonth(2026, 2));
        Assert.Equal(29, GregorianCivil.DaysInMonth(2024, 2));
        Assert.Equal(29, GregorianCivil.DaysInMonth(null, 2));
        Assert.Equal(30, GregorianCivil.DaysInMonth(2026, 4));
        Assert.Equal(31, GregorianCivil.DaysInMonth(2026, 12));
        Assert.Throws<System.ArgumentOutOfRangeException>(() => GregorianCivil.DaysInMonth(2026, 13));
    }

    [Theory]
    [InlineData("2026", 11, 4, UtvStatus.Valid)]
    [InlineData("2026", 1, 31, UtvStatus.Valid)]
    [InlineData("2026", 12, 31, UtvStatus.Valid)]
    [InlineData("2024", 2, 29, UtvStatus.Valid)]
    [InlineData("2000", 2, 29, UtvStatus.Valid)]
    [InlineData("0", 2, 29, UtvStatus.Valid)]
    [InlineData("-4", 2, 29, UtvStatus.Valid)]
    [InlineData("123456789012345678800", 2, 29, UtvStatus.Valid)]
    [InlineData("2026", 2, 30, UtvStatus.Invalid)]
    [InlineData("2024", 2, 30, UtvStatus.Invalid)]
    [InlineData("2026", 2, 29, UtvStatus.Invalid)]
    [InlineData("1900", 2, 29, UtvStatus.Invalid)]
    [InlineData("-1", 2, 29, UtvStatus.Invalid)]
    [InlineData("123456789012345678900", 2, 29, UtvStatus.Invalid)]
    [InlineData("2026", 4, 31, UtvStatus.Invalid)]
    [InlineData("2026", 13, 1, UtvStatus.Invalid)]
    [InlineData("2026", 0, 1, UtvStatus.Invalid)]
    [InlineData("2026", 1, 0, UtvStatus.Invalid)]
    [InlineData("2026", 1, 32, UtvStatus.Invalid)]
    [InlineData("2026", -1, 1, UtvStatus.Invalid)]
    public void Dates(string year, int month, int day, UtvStatus expected)
    {
        UtvResult result = GregorianCivil.Validate(Date(year, month, day));
        Assert.True(expected == result.Status, $"{year}-{month}-{day}: {result.Status.ToWireString()}: {Fixtures.Describe(result)}");
        Assert.Equal("validate", result.Operation);
    }

    [Fact]
    public void ImpossibleDatesAreStructurallyValid()
    {
        // Structural and semantic validity are separate checks.
        Assert.Equal(UtvStatus.Valid, UtvValidator.ValidateStructural(Date("2026", 2, 30)).Status);
        UtvResult result = GregorianCivil.Validate(Date("2026", 2, 30));
        Diagnostic d = Assert.Single(result.Diagnostics);
        Assert.Equal(("day-out-of-range", "/value/fields/day"), (d.Code, d.Path));
        Assert.Contains("2026-02", d.Message, System.StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("{\"month\":2,\"day\":29}", "month-day", UtvStatus.Valid)]
    [InlineData("{\"month\":2,\"day\":30}", "month-day", UtvStatus.Invalid)]
    [InlineData("{\"year\":\"2026\"}", "year", UtvStatus.Valid)]
    [InlineData("{\"year\":\"2026\",\"month\":2}", "year-month", UtvStatus.Valid)]
    [InlineData("{\"year\":\"2026\",\"month\":14}", "year-month", UtvStatus.Invalid)]
    [InlineData("{\"day\":31}", "calendar-fields", UtvStatus.Valid)]
    [InlineData("{\"day\":32}", "calendar-fields", UtvStatus.Invalid)]
    [InlineData("{\"year\":\"2026\",\"month\":2.0,\"day\":28.0}", "date", UtvStatus.Valid)]
    [InlineData("{\"year\":\"2026\",\"month\":2,\"day\":1e400}", "date", UtvStatus.Invalid)]
    [InlineData("{\"year\":\"2026\",\"month\":2,\"day\":29,\"hour\":12,\"minute\":0}", "datetime", UtvStatus.Invalid)]
    [InlineData("{\"year\":\"2024\",\"month\":2,\"day\":29,\"hour\":12,\"minute\":0}", "datetime", UtvStatus.Valid)]
    public void PartialAndOtherForms(string fields, string form, UtvStatus expected)
    {
        Assert.Equal(expected, GregorianCivil.Validate(Civil(fields, form)).Status);
    }

    [Theory]
    [InlineData("julian", "proleptic")]
    [InlineData("gregory", "historical-jurisdiction")]
    [InlineData("gregory", "registered")]
    [InlineData("hebrew", "registered")]
    public void OtherCalendarsAreUnsupported(string calendar, string chronology)
    {
        UtvResult result = GregorianCivil.Validate(Civil("{\"year\":\"2026\",\"month\":2,\"day\":30}", calendar: calendar, chronology: chronology));
        Assert.Equal(UtvStatus.Unsupported, result.Status);
        Assert.Equal("unsupported-calendar", Assert.Single(result.Diagnostics).Code);
    }

    [Fact]
    public void OtherKindsAreUnsupported()
    {
        UtvResult result = GregorianCivil.Validate(Fixtures.Examples("valid.json")["posix-instant"]);
        Assert.Equal(UtvStatus.Unsupported, result.Status);
        Assert.Equal("unsupported-kind", Assert.Single(result.Diagnostics).Code);
    }

    [Fact]
    public void StructuralResultComesFirst()
    {
        Assert.Equal(UtvStatus.Invalid, GregorianCivil.Validate("""{"schema":"utv/1.0"}""").Status);
        Assert.Equal(UtvStatus.Invalid, GregorianCivil.Validate("{").Status);
        Assert.Equal(UtvStatus.Unsupported, GregorianCivil.Validate(Date("2026", 2, 30).Replace("utv/1.0", "utv/1.1", System.StringComparison.Ordinal)).Status);
    }

    [Fact]
    public void CivilExamplesWithGregorianDatesAreValid()
    {
        foreach ((string name, string json) in Fixtures.Examples("valid.json"))
        {
            UtvResult result = GregorianCivil.Validate(json);
            Assert.True(result.Status is UtvStatus.Valid or UtvStatus.Unsupported, $"{name}: {Fixtures.Describe(result)}");
        }
    }
}
