using System;
using Xunit;

namespace Utv.Examples.Tests.UtvFirst;

/// <summary>
/// How to use <c>CivilYearMonth</c>, a month of a Gregorian year with no day, compared with .NET, which has no
/// year-month type and invents day 1 of the month with <c>DateOnly</c>.
/// </summary>
public class CivilYearMonthTests
{
    #region Of

    /// <summary>
    /// Verifies that CivilYearMonth.Of creates a month without inventing a day when given a year and a month.
    /// Compared with: <c>new DateOnly(2026, 11, 1)</c> has the same year and month, plus a day nobody asked for.
    /// </summary>
    [Fact]
    public void Test_Of_CreatesAMonthWithoutADay_WhenGivenAYearAndAMonth()
    {
        // GIVEN: November 2026
        int givenYear = 2026;
        int givenMonth = 11;

        // WHEN
        CivilYearMonth actualMonth = CivilYearMonth.Of(givenYear, givenMonth);

        // EXPECTATIONS: the year and month of .NET's stand-in, and text with no day in it
        DateOnly expectedStandIn = new(2026, 11, 1);
        string expectedText = "2026-11";

        // THEN
        Assert.Equal(expectedStandIn.Year, (int)actualMonth.Year);
        Assert.Equal(expectedStandIn.Month, actualMonth.Month);
        Assert.Equal(expectedText, actualMonth.ToString());
    }

    #endregion

    #region AddMonths

    /// <summary>
    /// Verifies that CivilYearMonth.AddMonths returns the later month, carrying into the next year.
    /// Compared with: <c>DateOnly.AddMonths</c> on the stand-in day 1 lands in the same month.
    /// </summary>
    [Fact]
    public void Test_AddMonths_ReturnsTheLaterMonth_WhenTheMonthsCrossAYearEnd()
    {
        // GIVEN: three months, past December
        int givenMonths = 3;

        // SYSTEM UNDER TEST: November 2026
        CivilYearMonth sut = CivilYearMonth.Of(2026, 11);

        // WHEN
        CivilYearMonth actualMonth = sut.AddMonths(givenMonths);

        // EXPECTATIONS: the month .NET lands in from the stand-in day
        DateOnly tmpStandIn = new DateOnly(2026, 11, 1).AddMonths(3);
        CivilYearMonth expectedMonth = CivilYearMonth.Of(tmpStandIn.Year, tmpStandIn.Month);

        // THEN
        Assert.Equal(expectedMonth, actualMonth);
    }

    #endregion
}
