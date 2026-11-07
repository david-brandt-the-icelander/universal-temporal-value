using System.Linq;
using Xunit;

namespace Utv.Examples.Tests.UtvFirst;

/// <summary>
/// How to use <c>GregorianCivil.Validate</c>, the semantic check of civil values in the proleptic Gregorian calendar
/// at the JSON layer, compared with .NET, where a <c>DateOnly</c> that exists is always valid and any other calendar is
/// converted to Gregorian on the way in.
/// </summary>
public class GregorianCivilTests
{
    #region Validate

    /// <summary>
    /// Verifies that GregorianCivil.Validate returns valid when the JSON names a real Gregorian date.
    /// Compared with: <c>new DateOnly(2026, 11, 5)</c> exists too.
    /// </summary>
    [Fact]
    public void Test_Validate_ReturnsValid_WhenTheDateExists()
    {
        // GIVEN: 5 November 2026 as UTV JSON: fields in a named calendar, with no time and no zone
        string givenJson = """
            { "schema": "utv/1.0", "kind": "civil",
              "value": { "form": "date", "fields": { "year": "2026", "month": 11, "day": 5 },
                         "calendar": { "id": "gregory", "chronology": "proleptic" } } }
            """;

        // WHEN
        UtvResult actualResult = GregorianCivil.Validate(givenJson);

        // EXPECTATIONS
        UtvStatus expectedStatus = UtvStatus.Valid;

        // THEN
        Assert.Equal(expectedStatus, actualResult.Status);
    }

    /// <summary>
    /// Verifies that GregorianCivil.Validate returns invalid when well-formed JSON names a day that does not exist.
    /// Compared with: .NET has one check, in the constructor; UTV keeps structure and meaning as separate checks.
    /// </summary>
    [Fact]
    public void Test_Validate_ReturnsInvalid_WhenWellFormedJsonNamesAMissingDay()
    {
        // GIVEN: 29 February 2026 as UTV JSON: structurally valid, but 2026 is a common year
        string givenJson = """
            { "schema": "utv/1.0", "kind": "civil",
              "value": { "form": "date", "fields": { "year": "2026", "month": 2, "day": 29 },
                         "calendar": { "id": "gregory", "chronology": "proleptic" } } }
            """;

        // WHEN
        UtvResult actualResult = GregorianCivil.Validate(givenJson);

        // EXPECTATIONS: invalid, with the reason, instead of rolling over to 1 March
        UtvStatus expectedStatus = UtvStatus.Invalid;
        string expectedCode = "day-out-of-range";

        // THEN
        Assert.Equal(expectedStatus, actualResult.Status);
        Assert.Contains(expectedCode, actualResult.Diagnostics.Select(d => d.Code));
    }

    /// <summary>
    /// Verifies that GregorianCivil.Validate returns unsupported when the date is in another calendar.
    /// Compared with: <c>HebrewCalendar.ToDateTime</c> converts the date to a Gregorian <c>DateTime</c> straight away,
    /// and the Hebrew fields are gone.
    /// </summary>
    [Fact]
    public void Test_Validate_ReturnsUnsupported_WhenTheCalendarIsNotGregorian()
    {
        // GIVEN: 14 Heshvan 5787 as UTV JSON, in the Hebrew calendar
        string givenJson = """
            { "schema": "utv/1.0", "kind": "civil",
              "value": { "form": "date", "fields": { "year": "5787", "month": 2, "day": 14 },
                         "calendar": { "id": "hebrew", "chronology": "proleptic" } } }
            """;

        // WHEN
        UtvResult actualResult = GregorianCivil.Validate(givenJson);

        // EXPECTATIONS: unsupported, kept as written, rather than guessed or converted
        UtvStatus expectedStatus = UtvStatus.Unsupported;

        // THEN
        Assert.Equal(expectedStatus, actualResult.Status);
    }

    /// <summary>
    /// Verifies that GregorianCivil.Validate returns valid when the value is a year with no month or day.
    /// Compared with: <c>new DateOnly(1347, 1, 1)</c> would claim it was 1 January.
    /// </summary>
    [Fact]
    public void Test_Validate_ReturnsValid_WhenTheValueIsOnlyAYear()
    {
        // GIVEN: the year 1347 as UTV JSON
        string givenJson = """
            { "schema": "utv/1.0", "kind": "civil",
              "value": { "form": "year", "fields": { "year": "1347" },
                         "calendar": { "id": "gregory", "chronology": "proleptic" } } }
            """;

        // WHEN
        UtvResult actualResult = GregorianCivil.Validate(givenJson);

        // EXPECTATIONS
        UtvStatus expectedStatus = UtvStatus.Valid;

        // THEN
        Assert.Equal(expectedStatus, actualResult.Status);
    }

    #endregion
}
