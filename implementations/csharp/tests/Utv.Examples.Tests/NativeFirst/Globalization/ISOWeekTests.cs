using System;
using System.Globalization;
using Xunit;

namespace Utv.Examples.Tests.NativeFirst.Globalization;

/// <summary>
/// What .NET's <c>System.Globalization.ISOWeek</c> does, compared with UTV's <c>CivilDate.IsoWeek</c>.
/// </summary>
public class ISOWeekTests
{
    #region GetWeekOfYear

    /// <summary>
    /// Verifies that ISOWeek.GetWeekOfYear returns the ISO 8601 week number of the date.
    /// Compared with: <c>CivilDate.IsoWeek</c> gives the same week, without first inventing a time of day.
    /// </summary>
    [Fact]
    public void Test_GetWeekOfYear_Returns45_WhenTheDateIs5November2026()
    {
        // GIVEN: midnight at the start of 5 November 2026
        DateTime givenDate = new(2026, 11, 5);

        // WHEN
        int actualWeek = ISOWeek.GetWeekOfYear(givenDate);

        // EXPECTATIONS: the week UTV computes for the same day
        int expectedWeek = CivilDate.Of(2026, 11, 5).IsoWeek;

        // THEN
        Assert.Equal(expectedWeek, actualWeek);
    }

    #endregion
}
