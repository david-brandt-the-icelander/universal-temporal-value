using Xunit;

namespace Utv.Examples.Tests.UtvFirst;

/// <summary>
/// How to use <c>TemporalContext</c>, which holds the zone rules that zoned operations need, compared with .NET,
/// where every zoned operation reads this machine's tz database.
/// </summary>
public class TemporalContextTests
{
    #region Compare

    // BEGIN compare-dates
    /// <summary>
    /// Verifies that TemporalContext.Compare returns both orders when both wall times fall in the same overlap.
    /// Compared with: two <c>DateTime</c> values of Kind Unspecified compare by wall time and always say "earlier".
    /// </summary>
    [Fact]
    public void Test_Compare_ReturnsMultiple_WhenBothWallTimesFallInAnOverlap()
    {
        // GIVEN: a call at 01:30 and a stand-up at 01:45, on the night New York's clocks go back from 02:00 to 01:00
        ZonedIntent givenCall = CivilDateTime.Of(2026, 11, 1, 1, 30).InZone("America/New_York");
        ZonedIntent givenStandup = CivilDateTime.Of(2026, 11, 1, 1, 45).InZone("America/New_York");

        // SETUP: the pinned demo zone rules, never this machine's tz database
        ZoneRules envRules = Fixtures.DemoZoneRules();

        // SYSTEM UNDER TEST
        TemporalContext sut = TemporalContext.With(envRules);

        // WHEN
        Outcome<Ordering> actualOutcome = sut.Compare(givenCall, givenStandup);

        // EXPECTATIONS: either could come first (01:30 standard time is after 01:45 daylight time)
        UtvStatus expectedStatus = UtvStatus.Multiple;
        string expectedCode = "ambiguous";

        // THEN
        Assert.Equal(expectedStatus, actualOutcome.Status);
        Assert.Contains(expectedCode, actualOutcome.Diagnostics.Codes);
    }
    // END compare-dates

    #endregion
}
