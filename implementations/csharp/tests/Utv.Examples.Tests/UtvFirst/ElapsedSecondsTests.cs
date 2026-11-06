using System;
using Xunit;

namespace Utv.Examples.Tests.UtvFirst;

/// <summary>
/// How to use <c>ElapsedSeconds</c>, SI seconds as a stopwatch measures them, compared with .NET's <c>TimeSpan</c>,
/// which has one kind of second for every purpose.
/// </summary>
public class ElapsedSecondsTests
{
    #region Of

    /// <summary>
    /// Verifies that ElapsedSeconds.Of holds exactly the elapsed seconds it is given.
    /// Compared with: <c>TimeSpan.FromMinutes(90).TotalSeconds</c> is the same number, as a double.
    /// </summary>
    [Fact]
    public void Test_Of_HoldsTheSeconds_WhenGivenNinetyMinutes()
    {
        // GIVEN: 90 minutes of elapsed time, in seconds
        long givenSeconds = 5_400;

        // WHEN
        ElapsedSeconds actualAmount = ElapsedSeconds.Of(givenSeconds);

        // EXPECTATIONS: the seconds .NET counts in 90 minutes, labelled as SI seconds
        double expectedSeconds = TimeSpan.FromMinutes(90).TotalSeconds;
        string expectedText = "5400 s (SI)";

        // THEN
        Assert.Equal(expectedSeconds, (double)actualAmount.Numerator);
        Assert.Equal(expectedText, actualAmount.ToString());
    }

    /// <summary>
    /// Verifies that ElapsedSeconds.Of creates an amount that is neither a calendar day nor POSIX seconds when given
    /// 86,400 seconds.
    /// Compared with: <c>TimeSpan.FromDays(1)</c> is all three at once.
    /// </summary>
    [Fact]
    public void Test_Of_IsNotACalendarDay_WhenGiven86400Seconds()
    {
        // GIVEN: exactly 86,400 SI seconds
        long givenSeconds = 86_400;

        // WHEN
        ElapsedSeconds actualAmount = ElapsedSeconds.Of(givenSeconds);

        // EXPECTATIONS: different from a calendar day and from 86,400 POSIX seconds
        Period expectedCalendarDay = Period.Days(1);
        PosixSeconds expectedPosixDay = PosixSeconds.Of(86_400);

        // THEN
        Assert.NotEqual<ITemporalValue>(expectedCalendarDay, actualAmount);
        Assert.NotEqual<ITemporalValue>(expectedPosixDay, actualAmount);
    }

    #endregion
}
