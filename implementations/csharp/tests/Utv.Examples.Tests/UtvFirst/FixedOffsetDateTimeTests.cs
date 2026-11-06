using System;
using Xunit;

namespace Utv.Examples.Tests.UtvFirst;

/// <summary>
/// How to use <c>FixedOffsetDateTime</c>, a wall time pinned to a UTC offset (but to no zone), compared with .NET's
/// <c>DateTimeOffset</c>.
/// </summary>
public class FixedOffsetDateTimeTests
{
    #region FromDateTimeOffset

    /// <summary>
    /// Verifies that FixedOffsetDateTime.FromDateTimeOffset keeps the wall time and the offset of the native value.
    /// Compared with: the <c>DateTimeOffset</c> it was made from has the same wall time and offset.
    /// </summary>
    [Fact]
    public void Test_FromDateTimeOffset_KeepsTheWallTimeAndOffset_WhenGivenANativeValue()
    {
        // GIVEN: lunch at 14:30 on 5 November 2026, one hour east of UTC
        DateTimeOffset givenLunch = new(2026, 11, 5, 14, 30, 0, TimeSpan.FromHours(1));

        // WHEN
        FixedOffsetDateTime actualLunch = FixedOffsetDateTime.FromDateTimeOffset(givenLunch);

        // EXPECTATIONS: the same wall time and offset as the native value
        CivilDateTime expectedWallTime = CivilDateTime.Of(2026, 11, 5, 14, 30, 0);
        int expectedOffsetSeconds = 3600;

        // THEN
        Assert.Equal(expectedWallTime, actualLunch.DateTime);
        Assert.Equal(expectedOffsetSeconds, actualLunch.OffsetSeconds);
    }

    #endregion

    #region ToDateTimeOffset

    // BEGIN round-trip
    /// <summary>
    /// Verifies that FixedOffsetDateTime.ToDateTimeOffset gives back exactly the native value it was made from.
    /// Compared with: <c>DateTimeOffset.EqualsExact</c> checks the instant and the offset.
    /// </summary>
    [Fact]
    public void Test_ToDateTimeOffset_ReturnsTheOriginal_WhenTheValueCameFromADateTimeOffset()
    {
        // GIVEN: lunch at 14:30 on 5 November 2026, one hour east of UTC
        DateTimeOffset givenLunch = new(2026, 11, 5, 14, 30, 0, TimeSpan.FromHours(1));

        // SYSTEM UNDER TEST: the same lunch as a UTV value
        FixedOffsetDateTime sut = FixedOffsetDateTime.FromDateTimeOffset(givenLunch);

        // WHEN
        Outcome<DateTimeOffset> actualOutcome = sut.ToDateTimeOffset();

        // EXPECTATIONS: the very same native value
        DateTimeOffset expectedLunch = new(2026, 11, 5, 14, 30, 0, TimeSpan.FromHours(1));

        // THEN
        Assert.True(expectedLunch.EqualsExact(actualOutcome.Single()));
    }
    // END round-trip

    #endregion

    #region ToInstant

    /// <summary>
    /// Verifies that FixedOffsetDateTime.ToInstant returns the instant the wall time and offset describe.
    /// Compared with: <c>DateTimeOffset.UtcDateTime</c> names the same moment.
    /// </summary>
    [Fact]
    public void Test_ToInstant_ReturnsTheUtcMoment_WhenTheOffsetIsKnown()
    {
        // SYSTEM UNDER TEST: 14:30 on 5 November 2026, one hour east of UTC
        FixedOffsetDateTime sut = CivilDateTime.Of(2026, 11, 5, 14, 30).AtOffset(3600);

        // WHEN
        Instant actualInstant = sut.ToInstant();

        // EXPECTATIONS: the moment .NET computes for the same value
        Instant expectedInstant = Instant.FromDateTimeOffset(new DateTimeOffset(2026, 11, 5, 14, 30, 0, TimeSpan.FromHours(1)));

        // THEN
        Assert.Equal(expectedInstant, actualInstant);
    }

    #endregion
}
