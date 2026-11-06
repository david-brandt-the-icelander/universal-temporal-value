using System;
using Xunit;

namespace Utv.Examples.Tests.UtvFirst;

/// <summary>
/// How to use <c>ZonedIntent</c>, a wall time in a named zone that stays an intention until it is resolved against
/// explicit zone rules, compared with .NET's <c>TimeZoneInfo</c>, which reads this machine's tz database.
/// </summary>
public class ZonedIntentTests
{
    #region Resolve

    // BEGIN meeting-in-paris
    /// <summary>
    /// Verifies that ZonedIntent.Resolve returns the one instant when the wall time happens exactly once.
    /// Compared with: <c>TimeZoneInfo.ConvertTimeToUtc</c> finds the same instant in this machine's tz database.
    /// </summary>
    [Fact]
    public void Test_Resolve_ReturnsTheInstant_WhenTheWallTimeHappensOnce()
    {
        // SETUP: the pinned demo zone rules, never this machine's tz database
        TemporalContext envContext = TemporalContext.With(Fixtures.DemoZoneRules());

        // SYSTEM UNDER TEST: a meeting at 09:00 on 1 June 2027 in Paris
        ZonedIntent sut = CivilDateTime.Of(2027, 6, 1, 9, 0).InZone("Europe/Paris");

        // WHEN
        Outcome<Instant> actualOutcome = sut.Resolve(envContext);

        // EXPECTATIONS: the instant .NET finds for the same wall time
        TimeZoneInfo tmpParis = TimeZoneInfo.FindSystemTimeZoneById("Europe/Paris");
        DateTime tmpUtc = TimeZoneInfo.ConvertTimeToUtc(new DateTime(2027, 6, 1, 9, 0, 0), tmpParis);
        Instant expectedInstant = Instant.FromDateTimeOffset(new DateTimeOffset(tmpUtc));

        // THEN
        Assert.Equal(expectedInstant, actualOutcome.Single());
    }
    // END meeting-in-paris

    // BEGIN dst-gap
    /// <summary>
    /// Verifies that ZonedIntent.Resolve returns none when the wall time falls in a daylight-saving gap.
    /// Compared with: <c>TimeZoneInfo.ConvertTimeToUtc</c> throws an ArgumentException for the same wall time.
    /// </summary>
    [Fact]
    public void Test_Resolve_ReturnsNone_WhenTheWallTimeFallsInAGap()
    {
        // SETUP: the pinned demo zone rules, never this machine's tz database
        TemporalContext envContext = TemporalContext.With(Fixtures.DemoZoneRules());

        // SYSTEM UNDER TEST: 02:30 on 8 March 2026 in New York, when clocks jump from 02:00 to 03:00
        ZonedIntent sut = CivilDateTime.Of(2026, 3, 8, 2, 30).InZone("America/New_York");

        // WHEN
        Outcome<Instant> actualOutcome = sut.Resolve(envContext);

        // EXPECTATIONS: no such instant, explained by the gap diagnostic
        UtvStatus expectedStatus = UtvStatus.None;
        string expectedCode = "gap";

        // THEN
        Assert.Equal(expectedStatus, actualOutcome.Status);
        Assert.Contains(expectedCode, actualOutcome.Diagnostics.Codes);
    }

    /// <summary>
    /// Verifies that ZonedIntent.Resolve with Gap.ShiftForward moves the wall time forward by the length of the gap.
    /// Compared with: .NET has no such policy; it throws instead (see TimeZoneInfoTests).
    /// </summary>
    [Fact]
    public void Test_Resolve_ShiftsForwardByTheGap_WhenShiftForwardIsRequested()
    {
        // GIVEN: the policy that moves a skipped wall time forward by the gap
        Gap givenPolicy = Gap.ShiftForward;

        // SETUP: the pinned demo zone rules, never this machine's tz database
        TemporalContext envContext = TemporalContext.With(Fixtures.DemoZoneRules());

        // SYSTEM UNDER TEST: 02:30 on 8 March 2026 in New York, when clocks jump from 02:00 to 03:00
        ZonedIntent sut = CivilDateTime.Of(2026, 3, 8, 2, 30).InZone("America/New_York");

        // WHEN
        Outcome<Instant> actualOutcome = sut.Resolve(envContext, givenPolicy);

        // EXPECTATIONS: 03:30 daylight time, four hours behind UTC
        Instant expectedInstant = Instant.FromDateTimeOffset(new DateTimeOffset(2026, 3, 8, 3, 30, 0, TimeSpan.FromHours(-4)));

        // THEN
        Assert.Equal(expectedInstant, actualOutcome.Single());
    }
    // END dst-gap

    // BEGIN dst-overlap
    /// <summary>
    /// Verifies that ZonedIntent.Resolve returns both instants when the wall time happens twice.
    /// Compared with: <c>TimeZoneInfo.ConvertTimeToUtc</c> silently picks one of them, the standard-time one.
    /// </summary>
    [Fact]
    public void Test_Resolve_ReturnsBothInstants_WhenTheWallTimeFallsInAnOverlap()
    {
        // SETUP: the pinned demo zone rules, never this machine's tz database
        TemporalContext envContext = TemporalContext.With(Fixtures.DemoZoneRules());

        // SYSTEM UNDER TEST: 01:30 on 1 November 2026 in New York, when clocks go back from 02:00 to 01:00
        ZonedIntent sut = CivilDateTime.Of(2026, 11, 1, 1, 30).InZone("America/New_York");

        // WHEN
        Outcome<Instant> actualOutcome = sut.Resolve(envContext);

        // EXPECTATIONS: 01:30 daylight time, then 01:30 standard time; .NET's single answer is one of them
        Instant[] expectedInstants =
        {
            Instant.FromDateTimeOffset(new DateTimeOffset(2026, 11, 1, 1, 30, 0, TimeSpan.FromHours(-4))),
            Instant.FromDateTimeOffset(new DateTimeOffset(2026, 11, 1, 1, 30, 0, TimeSpan.FromHours(-5))),
        };
        string expectedCode = "ambiguous";
        TimeZoneInfo tmpNewYork = TimeZoneInfo.FindSystemTimeZoneById("America/New_York");
        DateTime tmpUtc = TimeZoneInfo.ConvertTimeToUtc(new DateTime(2026, 11, 1, 1, 30, 0), tmpNewYork);
        Instant expectedNativePick = Instant.FromDateTimeOffset(new DateTimeOffset(tmpUtc));

        // THEN
        Outcome<Instant>.Multiple actualMultiple = Assert.IsType<Outcome<Instant>.Multiple>(actualOutcome);
        Assert.Equal(expectedInstants, actualMultiple.Values);
        Assert.Contains(expectedCode, actualOutcome.Diagnostics.Codes);
        Assert.Contains(expectedNativePick, actualMultiple.Values);
    }

    /// <summary>
    /// Verifies that ZonedIntent.Resolve with Overlap.Earliest returns the first of the two instants.
    /// Compared with: <c>TimeZoneInfo.ConvertTimeToUtc</c> picks the later one, and does not say so.
    /// </summary>
    [Fact]
    public void Test_Resolve_ReturnsTheEarlierInstant_WhenEarliestIsRequested()
    {
        // GIVEN: the policy that picks the earlier of two instants
        Overlap givenPolicy = Overlap.Earliest;

        // SETUP: the pinned demo zone rules, never this machine's tz database
        TemporalContext envContext = TemporalContext.With(Fixtures.DemoZoneRules());

        // SYSTEM UNDER TEST: 01:30 on 1 November 2026 in New York, when clocks go back from 02:00 to 01:00
        ZonedIntent sut = CivilDateTime.Of(2026, 11, 1, 1, 30).InZone("America/New_York");

        // WHEN
        Outcome<Instant> actualOutcome = sut.Resolve(envContext, givenPolicy);

        // EXPECTATIONS: 01:30 daylight time, the first time the clock shows it
        Instant expectedInstant = Instant.FromDateTimeOffset(new DateTimeOffset(2026, 11, 1, 1, 30, 0, TimeSpan.FromHours(-4)));

        // THEN
        Assert.Equal(expectedInstant, actualOutcome.Single());
    }
    // END dst-overlap

    #endregion

    #region AddDays

    // BEGIN day-vs-24h
    /// <summary>
    /// Verifies that ZonedIntent.AddDays keeps the wall time when the day crosses a daylight-saving change.
    /// Compared with: <c>DateTimeOffset.AddDays</c> keeps the offset instead, so the wall time moves to 13:00.
    /// </summary>
    [Fact]
    public void Test_AddDays_KeepsTheWallTime_WhenTheDayCrossesADstChange()
    {
        // GIVEN: one calendar day
        int givenDays = 1;

        // SYSTEM UNDER TEST: noon on 7 March 2026 in New York, the day before clocks spring forward
        ZonedIntent sut = CivilDateTime.Of(2026, 3, 7, 12, 0).InZone("America/New_York");

        // WHEN
        ZonedIntent actualNextNoon = sut.AddDays(givenDays);

        // EXPECTATIONS: noon again, on the next date
        ZonedIntent expectedNextNoon = CivilDateTime.Of(2026, 3, 8, 12, 0).InZone("America/New_York");

        // THEN
        Assert.Equal(expectedNextNoon, actualNextNoon);
    }
    // END day-vs-24h

    #endregion
}
