using System;
using System.Globalization;
using Xunit;

namespace Utv.Examples.Tests.NativeFirst.Globalization;

/// <summary>
/// What .NET's <c>System.Globalization.HebrewCalendar</c> does, compared with UTV, which keeps a Hebrew date's own
/// fields and reports <c>unsupported</c> from its Gregorian check rather than converting.
/// </summary>
public class HebrewCalendarTests
{
    #region ToDateTime

    /// <summary>
    /// Verifies that HebrewCalendar.ToDateTime converts a Hebrew date to a Gregorian DateTime.
    /// Compared with: UTV stores the Hebrew fields as written, and <c>GregorianCivil.Validate</c> returns unsupported
    /// (unsupported-calendar) instead of guessing; the Gregorian date is the same day only for callers with the rules.
    /// </summary>
    [Fact]
    public void Test_ToDateTime_ReturnsAGregorianDate_WhenGivenAHebrewDate()
    {
        // GIVEN: 14 Heshvan 5787
        int givenYear = 5787;
        int givenMonth = 2;
        int givenDay = 14;

        // SYSTEM UNDER TEST
        HebrewCalendar sut = new();

        // WHEN
        DateTime actualDate = sut.ToDateTime(givenYear, givenMonth, givenDay, 0, 0, 0, 0);

        // EXPECTATIONS: 25 October 2026, with nothing left to say that it was ever a Hebrew date
        DateTime expectedDate = new(2026, 10, 25);

        // THEN
        Assert.Equal(expectedDate, actualDate);
    }

    #endregion
}
