using System;
using Xunit;

namespace Utv.Examples.Tests.UtvFirst;

/// <summary>
/// How to use <c>CivilTime</c>, a wall-clock time of day with no date and no zone, compared with .NET's
/// <c>TimeOnly</c>.
/// </summary>
public class CivilTimeTests
{
    #region Of

    // BEGIN time-of-day
    /// <summary>
    /// Verifies that CivilTime.Of keeps the time to the minute when no seconds are given.
    /// Compared with: <c>new TimeOnly(9, 30)</c> always has seconds, and prints them.
    /// </summary>
    [Fact]
    public void Test_Of_WritesNoSeconds_WhenNoSecondsAreGiven()
    {
        // GIVEN: half past nine, to the minute
        int givenHour = 9;
        int givenMinute = 30;

        // WHEN
        CivilTime actualTime = CivilTime.Of(givenHour, givenMinute);

        // EXPECTATIONS: the same hour and minute as .NET, and no seconds in the text
        TimeOnly expectedTime = new(9, 30);
        string expectedText = "09:30";

        // THEN
        Assert.Equal(expectedTime.Hour, actualTime.Hour);
        Assert.Equal(expectedTime.Minute, actualTime.Minute);
        Assert.Equal(expectedText, actualTime.ToString());
    }
    // END time-of-day

    /// <summary>
    /// Verifies that CivilTime.Of keeps the seconds when they are given.
    /// Compared with: <c>new TimeOnly(9, 30, 0)</c> is the same time; UTV also remembers that a second was given.
    /// </summary>
    [Fact]
    public void Test_Of_WritesTheSeconds_WhenSecondsAreGiven()
    {
        // GIVEN: half past nine, to the second
        int givenHour = 9;
        int givenMinute = 30;
        int givenSecond = 0;

        // WHEN
        CivilTime actualTime = CivilTime.Of(givenHour, givenMinute, givenSecond);

        // EXPECTATIONS: the time .NET holds, with its seconds written out
        CivilTime expectedTime = CivilTime.FromTimeOnly(new TimeOnly(9, 30, 0));
        string expectedText = "09:30:00";

        // THEN
        Assert.Equal(expectedTime, actualTime);
        Assert.Equal(expectedText, actualTime.ToString());
    }

    #endregion
}
