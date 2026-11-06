using System;
using System.Globalization;
using Xunit;

namespace Utv.Examples.Tests.NativeFirst;

/// <summary>
/// What .NET's <c>System.TimeOnly</c> does, compared with UTV's <c>CivilTime</c>.
/// </summary>
public class TimeOnlyTests
{
    #region ToString

    /// <summary>
    /// Verifies that TimeOnly.ToString with the round-trip format writes seconds and ticks even when none were given.
    /// Compared with: <c>CivilTime.Of(9, 30)</c> remembers that no seconds were given, and writes "09:30".
    /// </summary>
    [Fact]
    public void Test_ToString_WritesSecondsAndTicks_WhenNoSecondsWereGiven()
    {
        // GIVEN: the round-trip format
        string givenFormat = "O";

        // SETUP: the invariant culture, so this machine's locale cannot change the answer
        CultureInfo envCulture = CultureInfo.InvariantCulture;

        // SYSTEM UNDER TEST: half past nine, to the minute
        TimeOnly sut = new(9, 30);

        // WHEN
        string actualText = sut.ToString(givenFormat, envCulture);

        // EXPECTATIONS: UTV's text for the same time, padded with zero seconds and seven zero digits
        string expectedText = CivilTime.Of(9, 30).ToString() + ":00.0000000";

        // THEN
        Assert.Equal(expectedText, actualText);
    }

    #endregion

    #region AddHours

    /// <summary>
    /// Verifies that TimeOnly.AddHours wraps around midnight and loses the day when the hours pass midnight.
    /// Compared with: <c>CivilTime</c> has no arithmetic at all; <c>CivilDateTime.AddHours</c> carries into the next
    /// day and keeps the date.
    /// </summary>
    [Fact]
    public void Test_AddHours_WrapsAndLosesTheDay_WhenTheHoursPassMidnight()
    {
        // GIVEN: two hours, past midnight
        double givenHours = 2;

        // SYSTEM UNDER TEST: 23:30
        TimeOnly sut = new(23, 30);

        // WHEN
        TimeOnly actualTime = sut.AddHours(givenHours);

        // EXPECTATIONS: the time of day UTV reaches, without the next date it also reaches
        TimeOnly expectedTime = CivilDateTime.Of(2026, 11, 5, 23, 30).AddHours(2).Time.ToTimeOnly().Single();

        // THEN
        Assert.Equal(expectedTime, actualTime);
    }

    #endregion
}
