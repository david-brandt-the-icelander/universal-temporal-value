using System;
using Xunit;

namespace Utv.Examples.Tests.UtvFirst;

/// <summary>
/// How to use <c>Period</c>, an amount of calendar components such as days and months, compared with .NET's
/// <c>TimeSpan</c>, which only counts ticks and so cannot tell a calendar day from 86,400 seconds.
/// </summary>
public class PeriodTests
{
    #region Days

    /// <summary>
    /// Verifies that Period.Days creates a calendar day, which is not a number of seconds.
    /// Compared with: <c>TimeSpan.FromDays(1)</c> equals <c>TimeSpan.FromSeconds(86_400)</c>, so the difference is lost.
    /// </summary>
    [Fact]
    public void Test_Days_IsNotEqualTo86400Seconds_WhenGivenOneDay()
    {
        // GIVEN: one day
        long givenDays = 1;

        // WHEN
        Period actualPeriod = Period.Days(givenDays);

        // EXPECTATIONS: a calendar component, written in ISO 8601, and a different value from 86,400 seconds
        string expectedText = "P1D";
        PosixSeconds expectedOtherAmount = PosixSeconds.FromTimeSpan(TimeSpan.FromDays(1));

        // THEN
        Assert.Equal(expectedText, actualPeriod.ToString());
        Assert.NotEqual<ITemporalValue>(expectedOtherAmount, actualPeriod);
    }

    #endregion

    #region Months

    /// <summary>
    /// Verifies that Period.Months creates a calendar month, distinct from a minute.
    /// Compared with: <c>TimeSpan</c> has no months at all; ISO 8601 writes P1M for a month and PT1M for a minute.
    /// </summary>
    [Fact]
    public void Test_Months_WritesP1M_WhenGivenOneMonth()
    {
        // GIVEN: one month
        long givenMonths = 1;

        // WHEN
        Period actualPeriod = Period.Months(givenMonths);

        // EXPECTATIONS: P1M, never PT1M
        string expectedText = "P1M";
        string expectedMinuteText = Period.Minutes(1).ToString();

        // THEN
        Assert.Equal(expectedText, actualPeriod.ToString());
        Assert.NotEqual(expectedMinuteText, actualPeriod.ToString());
    }

    #endregion
}
