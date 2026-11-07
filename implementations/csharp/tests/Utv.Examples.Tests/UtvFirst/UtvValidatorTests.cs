using System.Linq;
using Xunit;

namespace Utv.Examples.Tests.UtvFirst;

/// <summary>
/// How to use <c>UtvValidator.ValidateStructural</c>, which checks that JSON is well-formed UTV before anything
/// interprets it, compared with .NET, which has no temporal values beyond the everyday types: the values here are
/// written as UTV JSON because .NET has nothing to write them with.
/// </summary>
public class UtvValidatorTests
{
    #region ValidateStructural

    /// <summary>
    /// Verifies that UtvValidator.ValidateStructural accepts a value that is only a year.
    /// Compared with: <c>new DateOnly(1347, 1, 1)</c>, the usual stand-in, claims a day nobody knows.
    /// </summary>
    [Fact]
    public void Test_ValidateStructural_ReturnsValid_WhenTheValueIsOnlyAYear()
    {
        // GIVEN: the year 1347, with no month or day
        string givenJson = """
            { "schema": "utv/1.0", "kind": "civil",
              "value": { "form": "year", "fields": { "year": "1347" },
                         "calendar": { "id": "gregory", "chronology": "proleptic" } } }
            """;

        // WHEN
        UtvResult actualResult = UtvValidator.ValidateStructural(givenJson);

        // EXPECTATIONS
        UtvStatus expectedStatus = UtvStatus.Valid;

        // THEN
        Assert.Equal(expectedStatus, actualResult.Status);
    }

    /// <summary>
    /// Verifies that UtvValidator.ValidateStructural accepts a leap-second label, leaving its meaning to a later check.
    /// Compared with: <c>new DateTime(2016, 12, 31, 23, 59, 60)</c> throws before anything can be stored.
    /// </summary>
    [Fact]
    public void Test_ValidateStructural_ReturnsValid_WhenTheSecondIsALeapSecond()
    {
        // GIVEN: 23:59:60 UTC on 31 December 2016, a real leap second
        string givenJson = """
            { "schema": "utv/1.0", "kind": "civil",
              "value": { "form": "datetime",
                         "fields": { "year": "2016", "month": 12, "day": 31, "hour": 23, "minute": 59, "second": 60 },
                         "calendar": { "id": "gregory", "chronology": "proleptic" },
                         "zone": { "type": "fixed-offset", "seconds": 0 }, "zoneIntent": "fixed-offset" } }
            """;

        // WHEN
        UtvResult actualResult = UtvValidator.ValidateStructural(givenJson);

        // EXPECTATIONS: well-formed; confirming the label needs a leap-second table
        UtvStatus expectedStatus = UtvStatus.Valid;

        // THEN
        Assert.Equal(expectedStatus, actualResult.Status);
    }

    /// <summary>
    /// Verifies that UtvValidator.ValidateStructural accepts an interval stored as a start plus an amount.
    /// Compared with: a .NET (start, start + TimeSpan) pair stores a computed end, and forgets how it was written.
    /// </summary>
    [Fact]
    public void Test_ValidateStructural_ReturnsValid_WhenTheIntervalIsAStartPlusAnAmount()
    {
        // GIVEN: a two-hour meeting starting at 12:30 UTC on 4 November 2026
        string givenJson = """
            { "schema": "utv/1.0", "kind": "interval",
              "value": { "domain": "physical", "construction": "start-amount",
                         "start": { "state": "included",
                                    "point": { "schema": "utv/1.0", "kind": "instant",
                                               "value": { "system": { "system": "utv.timescale", "id": "unix-posix", "version": "1" },
                                                          "ticks": "1793795400" } } },
                         "amount": { "schema": "utv/1.0", "kind": "amount",
                                     "value": { "basis": "elapsed", "seconds": { "numerator": "7200", "denominator": "1" } } } } }
            """;

        // WHEN
        UtvResult actualResult = UtvValidator.ValidateStructural(givenJson);

        // EXPECTATIONS
        UtvStatus expectedStatus = UtvStatus.Valid;

        // THEN
        Assert.Equal(expectedStatus, actualResult.Status);
    }

    /// <summary>
    /// Verifies that UtvValidator.ValidateStructural accepts a union of two separate date ranges.
    /// Compared with: .NET has no range type, so two ranges are a list of hand-made pairs.
    /// </summary>
    [Fact]
    public void Test_ValidateStructural_ReturnsValid_WhenTheValueIsAUnionOfRanges()
    {
        // GIVEN: 1–5 November and 20–25 November 2026, as one value
        string givenJson = """
            { "schema": "utv/1.0", "kind": "set",
              "value": { "operation": "union", "members": [
                { "schema": "utv/1.0", "kind": "interval",
                  "value": { "domain": "civil/gregory",
                             "start": { "state": "included", "point": { "schema": "utv/1.0", "kind": "civil",
                                        "value": { "form": "date", "fields": { "year": "2026", "month": 11, "day": 1 },
                                                   "calendar": { "id": "gregory", "chronology": "proleptic" } } } },
                             "end": { "state": "excluded", "point": { "schema": "utv/1.0", "kind": "civil",
                                      "value": { "form": "date", "fields": { "year": "2026", "month": 11, "day": 6 },
                                                 "calendar": { "id": "gregory", "chronology": "proleptic" } } } } } },
                { "schema": "utv/1.0", "kind": "interval",
                  "value": { "domain": "civil/gregory",
                             "start": { "state": "included", "point": { "schema": "utv/1.0", "kind": "civil",
                                        "value": { "form": "date", "fields": { "year": "2026", "month": 11, "day": 20 },
                                                   "calendar": { "id": "gregory", "chronology": "proleptic" } } } },
                             "end": { "state": "excluded", "point": { "schema": "utv/1.0", "kind": "civil",
                                      "value": { "form": "date", "fields": { "year": "2026", "month": 11, "day": 26 },
                                                 "calendar": { "id": "gregory", "chronology": "proleptic" } } } } } } ] } }
            """;

        // WHEN
        UtvResult actualResult = UtvValidator.ValidateStructural(givenJson);

        // EXPECTATIONS
        UtvStatus expectedStatus = UtvStatus.Valid;

        // THEN
        Assert.Equal(expectedStatus, actualResult.Status);
    }

    /// <summary>
    /// Verifies that UtvValidator.ValidateStructural accepts a weekly recurrence anchored in a named zone.
    /// Compared with: .NET has no recurrence type; a list of computed <c>DateTime</c>s goes stale when zone rules change.
    /// </summary>
    [Fact]
    public void Test_ValidateStructural_ReturnsValid_WhenTheValueIsAWeeklyRecurrence()
    {
        // GIVEN: every Tuesday at 09:00 in Paris, from 3 November 2026, as an RFC 5545 rule
        string givenJson = """
            { "schema": "utv/1.0", "kind": "recurrence",
              "value": { "intent": { "schema": "utv/1.0", "kind": "civil",
                                     "value": { "form": "datetime",
                                                "fields": { "year": "2026", "month": 11, "day": 3, "hour": 9, "minute": 0 },
                                                "calendar": { "id": "gregory", "chronology": "proleptic" },
                                                "zone": { "type": "iana", "id": "Europe/Paris" }, "zoneIntent": "follow-zone-id" } },
                         "algorithm": { "system": "rfc", "id": "5545", "version": "1" },
                         "rule": "FREQ=WEEKLY;BYDAY=TU" } }
            """;

        // WHEN
        UtvResult actualResult = UtvValidator.ValidateStructural(givenJson);

        // EXPECTATIONS
        UtvStatus expectedStatus = UtvStatus.Valid;

        // THEN
        Assert.Equal(expectedStatus, actualResult.Status);
    }

    /// <summary>
    /// Verifies that UtvValidator.ValidateStructural accepts a year known only to lie within bounds.
    /// Compared with: .NET can only store one date, such as 1347-01-01, and hope nobody asks.
    /// </summary>
    [Fact]
    public void Test_ValidateStructural_ReturnsValid_WhenTheYearIsUncertain()
    {
        // GIVEN: about 1347, somewhere between 1340 and 1355
        string givenJson = """
            { "schema": "utv/1.0", "kind": "uncertain",
              "value": { "target": { "schema": "utv/1.0", "kind": "civil",
                                     "value": { "form": "year", "fields": { "year": "1347" },
                                                "calendar": { "id": "gregory", "chronology": "proleptic" } } },
                         "model": "bounds",
                         "parameters": { "earliest": { "year": "1340" }, "latest": { "year": "1355" },
                                         "interpretation": "possible range, not 95% confidence" } } }
            """;

        // WHEN
        UtvResult actualResult = UtvValidator.ValidateStructural(givenJson);

        // EXPECTATIONS
        UtvStatus expectedStatus = UtvStatus.Valid;

        // THEN
        Assert.Equal(expectedStatus, actualResult.Status);
    }

    /// <summary>
    /// Verifies that UtvValidator.ValidateStructural returns unsupported when the value is from a newer version.
    /// Compared with: a .NET deserializer would fail with a parse error, or read it as if nothing had changed.
    /// </summary>
    [Fact]
    public void Test_ValidateStructural_ReturnsUnsupported_WhenTheVersionIsNewer()
    {
        // GIVEN: a well-formed date labelled with a future specification version
        string givenJson = """
            { "schema": "utv/2.0", "kind": "civil",
              "value": { "form": "date", "fields": { "year": "2026", "month": 11, "day": 5 },
                         "calendar": { "id": "gregory", "chronology": "proleptic" } } }
            """;

        // WHEN
        UtvResult actualResult = UtvValidator.ValidateStructural(givenJson);

        // EXPECTATIONS: unsupported, not invalid: the value may be fine, this library just doesn't read it
        UtvStatus expectedStatus = UtvStatus.Unsupported;

        // THEN
        Assert.Equal(expectedStatus, actualResult.Status);
    }

    /// <summary>
    /// Verifies that UtvValidator.ValidateStructural returns invalid when the version label is malformed.
    /// </summary>
    [Fact]
    public void Test_ValidateStructural_ReturnsInvalid_WhenTheVersionLabelIsMalformed()
    {
        // GIVEN: a date labelled "utv/4", an unreleased draft label
        string givenJson = """
            { "schema": "utv/4", "kind": "civil",
              "value": { "form": "date", "fields": { "year": "2026", "month": 11, "day": 5 },
                         "calendar": { "id": "gregory", "chronology": "proleptic" } } }
            """;

        // WHEN
        UtvResult actualResult = UtvValidator.ValidateStructural(givenJson);

        // EXPECTATIONS
        UtvStatus expectedStatus = UtvStatus.Invalid;

        // THEN
        Assert.Equal(expectedStatus, actualResult.Status);
    }

    /// <summary>
    /// Verifies that UtvValidator.ValidateStructural returns invalid when a key appears twice.
    /// Compared with: <c>JsonDocument.Parse</c> accepts the text, and <c>GetProperty</c> reads the last month.
    /// </summary>
    [Fact]
    public void Test_ValidateStructural_ReturnsInvalid_WhenAKeyAppearsTwice()
    {
        // GIVEN: a date with two different months
        string givenJson = """
            {"schema":"utv/1.0","kind":"civil","value":{"form":"date","fields":{"year":"2026","month":11,"month":12,"day":5},"calendar":{"id":"gregory","chronology":"proleptic"}}}
            """;

        // WHEN
        UtvResult actualResult = UtvValidator.ValidateStructural(givenJson);

        // EXPECTATIONS: invalid, rather than whichever month a parser happens to keep
        UtvStatus expectedStatus = UtvStatus.Invalid;
        string expectedCode = "duplicate-key";

        // THEN
        Assert.Equal(expectedStatus, actualResult.Status);
        Assert.Contains(expectedCode, actualResult.Diagnostics.Select(d => d.Code));
    }

    /// <summary>
    /// Verifies that UtvValidator.ValidateStructural returns invalid when the kind is unknown.
    /// </summary>
    [Fact]
    public void Test_ValidateStructural_ReturnsInvalid_WhenTheKindIsUnknown()
    {
        // GIVEN: a value of a kind the specification does not define
        string givenJson = """
            { "schema": "utv/1.0", "kind": "moment", "value": {} }
            """;

        // WHEN
        UtvResult actualResult = UtvValidator.ValidateStructural(givenJson);

        // EXPECTATIONS: fail closed: an unknown kind is rejected, not ignored
        UtvStatus expectedStatus = UtvStatus.Invalid;

        // THEN
        Assert.Equal(expectedStatus, actualResult.Status);
    }

    #endregion
}
