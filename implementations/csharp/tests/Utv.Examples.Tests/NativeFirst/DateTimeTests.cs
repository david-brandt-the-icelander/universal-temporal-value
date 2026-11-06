using System;
using Xunit;

namespace Utv.Examples.Tests.NativeFirst;

/// <summary>
/// What .NET's <c>System.DateTime</c> does, compared with UTV's <c>CivilDate</c> and <c>CivilDateTime</c>.
/// </summary>
public class DateTimeTests
{
    #region Constructor

    /// <summary>
    /// Verifies that the DateTime constructor creates a wall time of Kind Unspecified when given no kind.
    /// Compared with: <c>CivilDateTime.Of(2026, 11, 5, 14, 30)</c> is the same wall time, and has no zone by design.
    /// </summary>
    [Fact]
    public void Test_Constructor_CreatesAnUnspecifiedWallTime_WhenGivenNoKind()
    {
        // GIVEN: 14:30 on 5 November 2026
        int givenYear = 2026;
        int givenMonth = 11;
        int givenDay = 5;
        int givenHour = 14;
        int givenMinute = 30;

        // WHEN
        DateTime actualDateTime = new(givenYear, givenMonth, givenDay, givenHour, givenMinute, 0);

        // EXPECTATIONS: the wall time UTV holds, tied to no zone
        DateTime expectedDateTime = CivilDateTime.Of(2026, 11, 5, 14, 30).ToDateTime().Single();
        DateTimeKind expectedKind = DateTimeKind.Unspecified;

        // THEN
        Assert.Equal(expectedDateTime, actualDateTime);
        Assert.Equal(expectedKind, actualDateTime.Kind);
    }

    /// <summary>
    /// Verifies that the DateTime constructor throws when the second is a leap second, 60.
    /// Compared with: <c>CivilDateTime.TryOf(2016, 12, 31, 23, 59, 60)</c> returns unsupported (leap-second): the
    /// second may well exist, and judging it needs a leap-second table.
    /// </summary>
    [Fact]
    public void Test_Constructor_Throws_WhenTheSecondIsALeapSecond()
    {
        // GIVEN: 23:59:60 on 31 December 2016, a real leap second
        int givenSecond = 60;

        // WHEN
        Exception? actualException = Record.Exception(() => new DateTime(2016, 12, 31, 23, 59, givenSecond));

        // EXPECTATIONS
        Type expectedExceptionType = typeof(ArgumentOutOfRangeException);

        // THEN
        Assert.IsType(expectedExceptionType, actualException);
    }

    #endregion

    #region Now

    /// <summary>
    /// Verifies that DateTime.Now returns a value of Kind Local whatever the time is.
    /// Compared with: <c>CivilDateTime.FromDateTime</c> refuses Kind Local (local-zone), because its meaning depends
    /// on this machine's zone.
    /// </summary>
    /// <remarks>
    /// This test reads the real clock on purpose: the kind does not depend on the time read, so the result is the same
    /// on every run. If it ever flakes, that is a finding about DateTime.Now, not noise.
    /// </remarks>
    [Fact]
    public void Test_Now_ReturnsKindLocal_WhenReadAtAnyTime()
    {
        // WHEN
        DateTime actualNow = DateTime.Now;

        // EXPECTATIONS: a wall time tied to this machine's zone
        DateTimeKind expectedKind = DateTimeKind.Local;

        // THEN
        Assert.Equal(expectedKind, actualNow.Kind);
    }

    #endregion

    #region IsLeapYear

    /// <summary>
    /// Verifies that DateTime.IsLeapYear follows the Gregorian leap-year rules for every kind of year.
    /// Compared with: <c>CivilDate.IsLeapYear</c> gives the same answers.
    /// </summary>
    [Theory]
    [InlineData(2024)] // divisible by 4: leap
    [InlineData(2026)] // not divisible by 4: common
    [InlineData(1900)] // divisible by 100 but not 400: common
    [InlineData(2000)] // divisible by 400: leap
    public void Test_IsLeapYear_AgreesWithUtv_WhenGivenAnyKindOfYear(int givenYear)
    {
        // WHEN
        bool actualIsLeapYear = DateTime.IsLeapYear(givenYear);

        // EXPECTATIONS: UTV's answer for the same year
        bool expectedIsLeapYear = CivilDate.Of(givenYear, 1, 1).IsLeapYear;

        // THEN
        Assert.Equal(expectedIsLeapYear, actualIsLeapYear);
    }

    #endregion

    #region AddHours

    /// <summary>
    /// Verifies that DateTime.AddHours carries into the next day when the hours pass midnight.
    /// Compared with: <c>CivilDateTime.AddHours</c> gives the same wall time.
    /// </summary>
    [Fact]
    public void Test_AddHours_CarriesIntoTheNextDay_WhenTheHoursPassMidnight()
    {
        // GIVEN: two hours, past midnight
        int givenHours = 2;

        // SYSTEM UNDER TEST: 23:30 on 5 November 2026
        DateTime sut = new(2026, 11, 5, 23, 30, 0);

        // WHEN
        DateTime actualDateTime = sut.AddHours(givenHours);

        // EXPECTATIONS: the wall time UTV computes for the same addition
        DateTime expectedDateTime = CivilDateTime.Of(2026, 11, 5, 23, 30).AddHours(2).ToDateTime().Single();

        // THEN
        Assert.Equal(expectedDateTime, actualDateTime);
    }

    #endregion
}
