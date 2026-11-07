using System;
using Xunit;

namespace Utv.Examples.Tests.NativeFirst;

/// <summary>
/// What .NET's <c>System.TimeZoneInfo</c> does, compared with UTV's <c>ZonedIntent</c> and <c>Instant.InZone</c>.
/// </summary>
/// <remarks>
/// <c>TimeZoneInfo</c> reads this machine's tz database, so these tests need one that knows the 2026–2027 rules of
/// New York and Paris. UTV reads only the pinned demo rules it is given.
/// </remarks>
public class TimeZoneInfoTests
{
    #region ConvertTimeToUtc

    /// <summary>
    /// Verifies that TimeZoneInfo.ConvertTimeToUtc returns the instant when the wall time happens exactly once.
    /// Compared with: <c>ZonedIntent.Resolve</c> finds the same instant in the pinned rules.
    /// </summary>
    [Fact]
    public void Test_ConvertTimeToUtc_ReturnsTheInstant_WhenTheWallTimeHappensOnce()
    {
        // GIVEN: a meeting at 09:00 on 1 June 2027
        DateTime givenWallTime = new(2027, 6, 1, 9, 0, 0);

        // SETUP: Paris, from this machine's tz database
        TimeZoneInfo envParis = TimeZoneInfo.FindSystemTimeZoneById("Europe/Paris");

        // WHEN
        DateTime actualUtc = TimeZoneInfo.ConvertTimeToUtc(givenWallTime, envParis);

        // EXPECTATIONS: the instant UTV resolves from the pinned rules
        TemporalContext tmpContext = TemporalContext.With(Fixtures.DemoZoneRules());
        Instant tmpInstant = CivilDateTime.Of(2027, 6, 1, 9, 0).InZone("Europe/Paris").Resolve(tmpContext).Single();
        DateTime expectedUtc = tmpInstant.ToDateTimeOffset().Single().UtcDateTime;

        // THEN
        Assert.Equal(expectedUtc, actualUtc);
    }

    /// <summary>
    /// Verifies that TimeZoneInfo.ConvertTimeToUtc throws when the wall time falls in a daylight-saving gap.
    /// Compared with: <c>ZonedIntent.Resolve</c> returns none (gap) as a value, and <c>Gap.ShiftForward</c> is there
    /// for callers who want 03:30.
    /// </summary>
    [Fact]
    public void Test_ConvertTimeToUtc_Throws_WhenTheWallTimeFallsInAGap()
    {
        // GIVEN: 02:30 on 8 March 2026, when New York's clocks jump from 02:00 to 03:00
        DateTime givenWallTime = new(2026, 3, 8, 2, 30, 0);

        // SETUP: New York, from this machine's tz database
        TimeZoneInfo envNewYork = TimeZoneInfo.FindSystemTimeZoneById("America/New_York");

        // WHEN
        Exception? actualException = Record.Exception(() => TimeZoneInfo.ConvertTimeToUtc(givenWallTime, envNewYork));

        // EXPECTATIONS
        Type expectedExceptionType = typeof(ArgumentException);

        // THEN
        Assert.IsType(expectedExceptionType, actualException);
    }

    /// <summary>
    /// Verifies that TimeZoneInfo.ConvertTimeToUtc picks the standard-time instant, silently, when the wall time
    /// happens twice.
    /// Compared with: <c>ZonedIntent.Resolve</c> returns both instants; this one is what <c>Overlap.Latest</c> picks.
    /// </summary>
    [Fact]
    public void Test_ConvertTimeToUtc_PicksTheLaterInstant_WhenTheWallTimeFallsInAnOverlap()
    {
        // GIVEN: 01:30 on 1 November 2026, when New York's clocks go back from 02:00 to 01:00
        DateTime givenWallTime = new(2026, 11, 1, 1, 30, 0);

        // SETUP: New York, from this machine's tz database
        TimeZoneInfo envNewYork = TimeZoneInfo.FindSystemTimeZoneById("America/New_York");

        // WHEN
        DateTime actualUtc = TimeZoneInfo.ConvertTimeToUtc(givenWallTime, envNewYork);

        // EXPECTATIONS: the instant UTV picks only when asked for the latest
        TemporalContext tmpContext = TemporalContext.With(Fixtures.DemoZoneRules());
        Instant tmpInstant = CivilDateTime.Of(2026, 11, 1, 1, 30).InZone("America/New_York").Resolve(tmpContext, Overlap.Latest).Single();
        DateTime expectedUtc = tmpInstant.ToDateTimeOffset().Single().UtcDateTime;

        // THEN
        Assert.Equal(expectedUtc, actualUtc);
    }

    #endregion

    #region IsInvalidTime

    /// <summary>
    /// Verifies that TimeZoneInfo.IsInvalidTime returns true when the wall time falls in a daylight-saving gap.
    /// Compared with: <c>ZonedIntent.Resolve</c> reports the same thing as status none, with the gap diagnostic.
    /// </summary>
    [Fact]
    public void Test_IsInvalidTime_ReturnsTrue_WhenTheWallTimeFallsInAGap()
    {
        // GIVEN: 02:30 on 8 March 2026, when New York's clocks jump from 02:00 to 03:00
        DateTime givenWallTime = new(2026, 3, 8, 2, 30, 0);

        // SYSTEM UNDER TEST: New York, from this machine's tz database
        TimeZoneInfo sut = TimeZoneInfo.FindSystemTimeZoneById("America/New_York");

        // WHEN
        bool actualInvalid = sut.IsInvalidTime(givenWallTime);

        // EXPECTATIONS: invalid exactly when UTV finds no instant
        TemporalContext tmpContext = TemporalContext.With(Fixtures.DemoZoneRules());
        Outcome<Instant> tmpOutcome = CivilDateTime.Of(2026, 3, 8, 2, 30).InZone("America/New_York").Resolve(tmpContext);
        bool expectedInvalid = tmpOutcome.Status == UtvStatus.None;

        // THEN
        Assert.Equal(expectedInvalid, actualInvalid);
    }

    #endregion

    #region IsAmbiguousTime

    /// <summary>
    /// Verifies that TimeZoneInfo.IsAmbiguousTime returns true when the wall time happens twice.
    /// Compared with: <c>ZonedIntent.Resolve</c> reports the same thing as status multiple, with both instants.
    /// </summary>
    [Fact]
    public void Test_IsAmbiguousTime_ReturnsTrue_WhenTheWallTimeFallsInAnOverlap()
    {
        // GIVEN: 01:30 on 1 November 2026, when New York's clocks go back from 02:00 to 01:00
        DateTime givenWallTime = new(2026, 11, 1, 1, 30, 0);

        // SYSTEM UNDER TEST: New York, from this machine's tz database
        TimeZoneInfo sut = TimeZoneInfo.FindSystemTimeZoneById("America/New_York");

        // WHEN
        bool actualAmbiguous = sut.IsAmbiguousTime(givenWallTime);

        // EXPECTATIONS: ambiguous exactly when UTV finds more than one instant
        TemporalContext tmpContext = TemporalContext.With(Fixtures.DemoZoneRules());
        Outcome<Instant> tmpOutcome = CivilDateTime.Of(2026, 11, 1, 1, 30).InZone("America/New_York").Resolve(tmpContext);
        bool expectedAmbiguous = tmpOutcome.Status == UtvStatus.Multiple;

        // THEN
        Assert.Equal(expectedAmbiguous, actualAmbiguous);
    }

    #endregion

    #region ConvertTimeFromUtc

    /// <summary>
    /// Verifies that TimeZoneInfo.ConvertTimeFromUtc returns the local wall time when given an instant.
    /// Compared with: <c>Instant.InZone</c> gives the same wall time from the pinned rules, with its offset.
    /// </summary>
    [Fact]
    public void Test_ConvertTimeFromUtc_ReturnsTheLocalWallTime_WhenGivenAnInstant()
    {
        // GIVEN: 07:00 UTC on 1 June 2027
        DateTime givenUtc = new(2027, 6, 1, 7, 0, 0, DateTimeKind.Utc);

        // SETUP: New York, from this machine's tz database
        TimeZoneInfo envNewYork = TimeZoneInfo.FindSystemTimeZoneById("America/New_York");

        // WHEN
        DateTime actualWallTime = TimeZoneInfo.ConvertTimeFromUtc(givenUtc, envNewYork);

        // EXPECTATIONS: the wall time UTV computes from the pinned rules
        TemporalContext tmpContext = TemporalContext.With(Fixtures.DemoZoneRules());
        ZonedIntent tmpZoned = Instant.FromUnixSeconds(1_811_833_200).InZone("America/New_York", tmpContext).Single();
        DateTime expectedWallTime = tmpZoned.DateTime.ToDateTime().Single();

        // THEN
        Assert.Equal(expectedWallTime, actualWallTime);
    }

    #endregion
}
