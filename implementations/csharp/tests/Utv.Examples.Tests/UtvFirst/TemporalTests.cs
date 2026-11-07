using System;
using System.Globalization;
using Xunit;

namespace Utv.Examples.Tests.UtvFirst;

/// <summary>
/// How to use <c>Temporal.Parse</c>, which reads any value in the UTV text form and returns the type the text
/// describes, compared with .NET's <c>DateTimeOffset.Parse</c>.
/// </summary>
public class TemporalTests
{
    #region Parse

    // BEGIN parse-offsets
    /// <summary>
    /// Verifies that Temporal.Parse returns an Instant when the text ends in Z.
    /// Compared with: <c>DateTimeOffset.Parse</c> gives an offset of +00:00, the same as for the text "+00:00".
    /// </summary>
    [Fact]
    public void Test_Parse_ReturnsAnInstant_WhenTheTextEndsInZ()
    {
        // GIVEN: 14:30 UTC, written with Z: a moment, with no local wall time implied
        string givenText = "2026-11-05T14:30:00Z";

        // WHEN
        Outcome<ITemporalValue> actualOutcome = Temporal.Parse(givenText);

        // EXPECTATIONS: the moment .NET parses from the same text
        Instant expectedInstant = Instant.FromDateTimeOffset(DateTimeOffset.Parse("2026-11-05T14:30:00Z", CultureInfo.InvariantCulture));

        // THEN
        Instant actualInstant = Assert.IsType<Instant>(actualOutcome.Single());
        Assert.Equal(expectedInstant, actualInstant);
    }

    /// <summary>
    /// Verifies that Temporal.Parse returns a FixedOffsetDateTime when the text ends in +00:00.
    /// Compared with: <c>DateTimeOffset.Parse</c> cannot tell this text from the one ending in Z.
    /// </summary>
    [Fact]
    public void Test_Parse_ReturnsAFixedOffsetDateTime_WhenTheTextEndsInPlusZero()
    {
        // GIVEN: 14:30 at offset +00:00: a wall time somewhere that is at UTC+0 right now
        string givenText = "2026-11-05T14:30:00+00:00";

        // WHEN
        Outcome<ITemporalValue> actualOutcome = Temporal.Parse(givenText);

        // EXPECTATIONS: the value .NET parses from the same text, and the text kept as written
        FixedOffsetDateTime expectedDateTime =
            FixedOffsetDateTime.FromDateTimeOffset(DateTimeOffset.Parse("2026-11-05T14:30:00+00:00", CultureInfo.InvariantCulture));
        string expectedText = "2026-11-05T14:30:00+00:00";

        // THEN
        FixedOffsetDateTime actualDateTime = Assert.IsType<FixedOffsetDateTime>(actualOutcome.Single());
        Assert.Equal(expectedDateTime, actualDateTime);
        Assert.Equal(expectedText, actualDateTime.ToString());
    }
    // END parse-offsets

    #endregion
}
