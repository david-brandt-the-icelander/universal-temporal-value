using System;
using Xunit;

namespace Utv.Examples.Tests.NativeFirst;

/// <summary>
/// What .NET's <c>System.TimeSpan</c> does, compared with UTV's three kinds of amount: <c>Period</c>,
/// <c>PosixSeconds</c> and <c>ElapsedSeconds</c>.
/// </summary>
public class TimeSpanTests
{
    #region FromDays

    /// <summary>
    /// Verifies that TimeSpan.FromDays(1) equals 86,400 seconds.
    /// Compared with: UTV keeps <c>Period.Days(1)</c>, <c>PosixSeconds.Of(86_400)</c> and
    /// <c>ElapsedSeconds.Of(86_400)</c> apart, because a calendar day can be 23 or 25 hours long.
    /// </summary>
    [Fact]
    public void Test_FromDays_Equals86400Seconds_WhenGivenOneDay()
    {
        // GIVEN: one day
        double givenDays = 1;

        // WHEN
        TimeSpan actualDay = TimeSpan.FromDays(givenDays);

        // EXPECTATIONS: the POSIX seconds UTV would convert this TimeSpan into
        TimeSpan expectedDay = PosixSeconds.Of(86_400).ToTimeSpan().Single();

        // THEN
        Assert.Equal(expectedDay, actualDay);
    }

    #endregion

    #region FromMinutes

    /// <summary>
    /// Verifies that TimeSpan.FromMinutes counts 60 seconds to the minute.
    /// Compared with: <c>ElapsedSeconds.Of(5_400)</c> holds the same number of seconds as an exact SI amount.
    /// </summary>
    [Fact]
    public void Test_FromMinutes_CountsSixtySecondsAMinute_WhenGivenNinetyMinutes()
    {
        // GIVEN: 90 minutes
        double givenMinutes = 90;

        // WHEN
        TimeSpan actualDuration = TimeSpan.FromMinutes(givenMinutes);

        // EXPECTATIONS: the seconds UTV holds for the same duration
        ElapsedSeconds tmpElapsed = ElapsedSeconds.Of(5_400);
        double expectedSeconds = (double)tmpElapsed.Numerator / (double)tmpElapsed.Denominator;

        // THEN
        Assert.Equal(expectedSeconds, actualDuration.TotalSeconds);
    }

    #endregion
}
