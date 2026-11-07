using Xunit;

namespace Utv.Examples.Tests;

/// <summary>Tests of the shared test data in <see cref="Fixtures"/>.</summary>
public class FixturesTests
{
    /// <summary>
    /// Verifies that Fixtures.DemoZoneRules loads the three demo zones when called.
    /// </summary>
    [Fact]
    public void Test_DemoZoneRules_ContainsTheThreeDemoZones_WhenLoaded()
    {
        // WHEN
        ZoneRules actualRules = Fixtures.DemoZoneRules();

        // EXPECTATIONS
        string[] expectedZoneIds = { "America/New_York", "Atlantic/Reykjavik", "Europe/Paris" };

        // THEN
        Assert.Equal(expectedZoneIds, actualRules.ZoneIds);
    }
}
