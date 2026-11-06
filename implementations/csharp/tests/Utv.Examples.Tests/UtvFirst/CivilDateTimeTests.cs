using System;
using Xunit;

namespace Utv.Examples.Tests.UtvFirst;

/// <summary>
/// How to use <c>CivilDateTime</c>, a wall-clock date and time with no zone, compared with .NET's <c>DateTime</c>
/// (whose <c>Kind</c> can quietly tie it to this machine's zone).
/// </summary>
public class CivilDateTimeTests
{
    #region Of

    /// <summary>
    /// Verifies that CivilDateTime.Of creates a wall-clock date-time with no zone when given a date and a time.
    /// Compared with: <c>new DateTime(2026, 11, 5, 14, 30, 0)</c> is the same wall time, of Kind Unspecified.
    /// </summary>
    [Fact]
    public void Test_Of_CreatesAWallClockDateTime_WhenGivenADateAndATime()
    {
        // GIVEN: 14:30 on 5 November 2026, in no particular zone
        int givenYear = 2026;
        int givenMonth = 11;
        int givenDay = 5;
        int givenHour = 14;
        int givenMinute = 30;

        // WHEN
        CivilDateTime actualDateTime = CivilDateTime.Of(givenYear, givenMonth, givenDay, givenHour, givenMinute);

        // EXPECTATIONS: the wall time .NET holds, written without an offset or a zone
        DateTime expectedDateTime = new(2026, 11, 5, 14, 30, 0);
        string expectedText = "2026-11-05T14:30";

        // THEN
        Assert.Equal(expectedDateTime, actualDateTime.ToDateTime().Single());
        Assert.Equal(expectedText, actualDateTime.ToString());
    }

    #endregion

    #region TryOf

    /// <summary>
    /// Verifies that CivilDateTime.TryOf returns unsupported when the second is a leap second, 60.
    /// Compared with: <c>new DateTime(2016, 12, 31, 23, 59, 60)</c> throws, as if the second could never exist.
    /// </summary>
    [Fact]
    public void Test_TryOf_ReturnsUnsupported_WhenTheSecondIsALeapSecond()
    {
        // GIVEN: 23:59:60 on 31 December 2016, a real leap second
        int givenYear = 2016;
        int givenMonth = 12;
        int givenDay = 31;
        int givenHour = 23;
        int givenMinute = 59;
        int givenSecond = 60;

        // WHEN
        Outcome<CivilDateTime> actualOutcome =
            CivilDateTime.TryOf(givenYear, givenMonth, givenDay, givenHour, givenMinute, givenSecond);

        // EXPECTATIONS: unsupported, not invalid: judging it needs a leap-second table
        UtvStatus expectedStatus = UtvStatus.Unsupported;
        string expectedCode = "leap-second";

        // THEN
        Assert.Equal(expectedStatus, actualOutcome.Status);
        Assert.Contains(expectedCode, actualOutcome.Diagnostics.Codes);
    }

    #endregion

    #region AddHours

    // BEGIN datetime-plus-hours
    /// <summary>
    /// Verifies that CivilDateTime.AddHours carries into the next day when the hours pass midnight.
    /// Compared with: <c>DateTime.AddHours</c> gives the same wall time; <c>TimeOnly.AddHours</c> would lose the day.
    /// </summary>
    [Fact]
    public void Test_AddHours_CarriesIntoTheNextDay_WhenTheHoursPassMidnight()
    {
        // GIVEN: two hours, past midnight
        int givenHours = 2;

        // SYSTEM UNDER TEST: 23:30 on 5 November 2026
        CivilDateTime sut = CivilDateTime.Of(2026, 11, 5, 23, 30);

        // WHEN
        CivilDateTime actualDateTime = sut.AddHours(givenHours);

        // EXPECTATIONS: the wall time .NET computes for the same addition
        DateTime expectedDateTime = new DateTime(2026, 11, 5, 23, 30, 0).AddHours(2);

        // THEN
        Assert.Equal(expectedDateTime, actualDateTime.ToDateTime().Single());
    }
    // END datetime-plus-hours

    #endregion

    #region FromDateTime

    // BEGIN now
    /// <summary>
    /// Verifies that CivilDateTime.FromDateTime returns unsupported when the DateTime's Kind is Local.
    /// Compared with: <c>DateTime.Now</c> returns Kind Local, a wall time that only means something on this machine.
    /// </summary>
    [Fact]
    public void Test_FromDateTime_ReturnsUnsupported_WhenTheKindIsLocal()
    {
        // GIVEN: a reading shaped like DateTime.Now's, tied to this machine's zone
        DateTime givenLocalTime = new(2026, 11, 5, 14, 30, 0, DateTimeKind.Local);

        // WHEN
        Outcome<CivilDateTime> actualOutcome = CivilDateTime.FromDateTime(givenLocalTime);

        // EXPECTATIONS: refused, because the host's zone is hidden input
        UtvStatus expectedStatus = UtvStatus.Unsupported;
        string expectedCode = "local-zone";

        // THEN
        Assert.Equal(expectedStatus, actualOutcome.Status);
        Assert.Contains(expectedCode, actualOutcome.Diagnostics.Codes);
    }
    // END now

    #endregion

    #region InZone

    /// <summary>
    /// Verifies that CivilDateTime.InZone keeps the wall time and the zone name as an intention.
    /// Compared with: .NET can only convert with <c>TimeZoneInfo</c>, which turns the intention into an instant
    /// using this machine's rules of today.
    /// </summary>
    [Fact]
    public void Test_InZone_KeepsTheWallTimeAndTheZone_WhenGivenAZoneId()
    {
        // GIVEN: the Paris time zone
        string givenZoneId = "Europe/Paris";

        // SYSTEM UNDER TEST: 09:00 on 1 June 2027
        CivilDateTime sut = CivilDateTime.Of(2027, 6, 1, 9, 0);

        // WHEN
        ZonedIntent actualMeeting = sut.InZone(givenZoneId);

        // EXPECTATIONS: the wall time and the zone, with no offset decided yet
        string expectedText = "2027-06-01T09:00[Europe/Paris]";

        // THEN
        Assert.Equal(expectedText, actualMeeting.ToString());
    }

    #endregion

    #region AtOffset

    /// <summary>
    /// Verifies that CivilDateTime.AtOffset pins the wall time to a fixed UTC offset.
    /// Compared with: <c>new DateTimeOffset(2026, 11, 5, 14, 30, 0, TimeSpan.FromHours(1))</c> is the same value.
    /// </summary>
    [Fact]
    public void Test_AtOffset_PinsTheWallTimeToTheOffset_WhenGivenOffsetSeconds()
    {
        // GIVEN: one hour east of UTC
        int givenOffsetSeconds = 3600;

        // SYSTEM UNDER TEST: 14:30 on 5 November 2026
        CivilDateTime sut = CivilDateTime.Of(2026, 11, 5, 14, 30);

        // WHEN
        FixedOffsetDateTime actualDateTime = sut.AtOffset(givenOffsetSeconds);

        // EXPECTATIONS: the value .NET's DateTimeOffset holds
        DateTimeOffset expectedDateTime = new(2026, 11, 5, 14, 30, 0, TimeSpan.FromHours(1));

        // THEN
        Assert.True(expectedDateTime.EqualsExact(actualDateTime.ToDateTimeOffset().Single()));
    }

    #endregion
}
