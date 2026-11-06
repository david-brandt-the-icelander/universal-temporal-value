using System;
using Xunit;

namespace Utv.Examples.Tests.UtvFirst;

/// <summary>
/// How to use <c>Instant</c>, an exact moment on the POSIX timescale with no calendar and no zone, compared with
/// .NET's <c>DateTimeOffset</c> and <c>TimeSpan</c>.
/// </summary>
public class InstantTests
{
    #region FromDateTimeOffset

    // BEGIN now
    /// <summary>
    /// Verifies that Instant.FromDateTimeOffset takes a clock reading as an input when the program reads the clock.
    /// Compared with: <c>DateTimeOffset.UtcNow</c> reads the clock itself; UTV never does, so tests can fix "now".
    /// </summary>
    [Fact]
    public void Test_FromDateTimeOffset_ReturnsTheSameMoment_WhenGivenAClockReading()
    {
        // GIVEN: a clock reading, as DateTimeOffset.UtcNow would return it
        DateTimeOffset givenReading = new(2026, 11, 5, 13, 30, 0, 123, TimeSpan.Zero);

        // WHEN
        Instant actualNow = Instant.FromDateTimeOffset(givenReading);

        // EXPECTATIONS: the same moment, counted in milliseconds since 1970 as .NET counts it
        Instant expectedNow = Instant.FromUnixNanoseconds(givenReading.ToUnixTimeMilliseconds() * 1_000_000);

        // THEN
        Assert.Equal(expectedNow, actualNow);
    }
    // END now

    #endregion

    #region FromUnixSeconds

    /// <summary>
    /// Verifies that Instant.FromUnixSeconds returns the Unix epoch when given zero seconds.
    /// Compared with: <c>DateTimeOffset.FromUnixTimeSeconds(0)</c> is the same moment.
    /// </summary>
    [Fact]
    public void Test_FromUnixSeconds_ReturnsTheEpoch_WhenGivenZero()
    {
        // GIVEN: zero seconds since 1970-01-01T00:00:00Z
        long givenSeconds = 0;

        // WHEN
        Instant actualInstant = Instant.FromUnixSeconds(givenSeconds);

        // EXPECTATIONS: the moment .NET returns for the same count, written in UTC
        Instant expectedInstant = Instant.FromDateTimeOffset(DateTimeOffset.FromUnixTimeSeconds(0));
        string expectedText = "1970-01-01T00:00:00Z";

        // THEN
        Assert.Equal(expectedInstant, actualInstant);
        Assert.Equal(expectedText, actualInstant.ToString());
    }

    /// <summary>
    /// Verifies that Instant.FromUnixSeconds keeps all nine fractional digits when given nanoseconds.
    /// Compared with: <c>DateTimeOffset</c> counts 100-nanosecond ticks, so it keeps only seven of them.
    /// </summary>
    [Fact]
    public void Test_FromUnixSeconds_KeepsEveryNanosecond_WhenGivenNanoseconds()
    {
        // GIVEN: a moment on 4 November 2026, to the nanosecond
        long givenSeconds = 1_793_795_400;
        long givenNanoseconds = 123_456_789;

        // WHEN
        Instant actualInstant = Instant.FromUnixSeconds(givenSeconds, givenNanoseconds);

        // EXPECTATIONS: all nine digits, where .NET's text stops at seven
        string expectedText = "2026-11-04T12:30:00.123456789Z";
        int expectedNanoseconds = 123_456_789;

        // THEN
        Assert.Equal(expectedText, actualInstant.ToString());
        Assert.Equal(expectedNanoseconds, actualInstant.Nanoseconds);
    }

    /// <summary>
    /// Verifies that Instant.FromUnixSeconds keeps a fraction of a second when given one.
    /// Compared with: <c>DateTimeOffset.ToUnixTimeSeconds</c> drops the fraction when counting back.
    /// </summary>
    [Fact]
    public void Test_FromUnixSeconds_KeepsTheFraction_WhenGivenAQuarterSecond()
    {
        // GIVEN: 13:30:00.25 on 5 November 2026
        long givenSeconds = 1_793_885_400;
        long givenNanoseconds = 250_000_000;

        // WHEN
        Instant actualInstant = Instant.FromUnixSeconds(givenSeconds, givenNanoseconds);

        // EXPECTATIONS: the moment .NET holds, with the quarter second written out
        Instant expectedInstant = Instant.FromDateTimeOffset(DateTimeOffset.FromUnixTimeMilliseconds(1_793_885_400_250));
        string expectedText = "2026-11-05T13:30:00.25Z";

        // THEN
        Assert.Equal(expectedInstant, actualInstant);
        Assert.Equal(expectedText, actualInstant.ToString());
    }

    #endregion

    #region Until

    // BEGIN timestamps
    /// <summary>
    /// Verifies that Instant.Until returns the exact number of seconds, as a fraction, between two instants.
    /// Compared with: subtracting two <c>DateTimeOffset</c> values gives a <c>TimeSpan</c> whose TotalSeconds is a double.
    /// </summary>
    [Fact]
    public void Test_Until_ReturnsAnExactFraction_WhenTheInstantsAreAFractionOfASecondApart()
    {
        // GIVEN: one hour, 0.75 seconds later
        Instant givenLater = Instant.FromUnixSeconds(1_793_889_001);

        // SYSTEM UNDER TEST: 13:30:00.25 on 5 November 2026
        Instant sut = Instant.FromUnixSeconds(1_793_885_400, 250_000_000);

        // WHEN
        PosixSeconds actualSeconds = sut.Until(givenLater);

        // EXPECTATIONS: exactly 14403/4 seconds, the number .NET approximates as a double
        PosixSeconds expectedSeconds = PosixSeconds.Of(14_403, 4);
        double expectedApproximation =
            (DateTimeOffset.FromUnixTimeSeconds(1_793_889_001) - DateTimeOffset.FromUnixTimeMilliseconds(1_793_885_400_250)).TotalSeconds;

        // THEN
        Assert.Equal(expectedSeconds, actualSeconds);
        Assert.Equal(expectedApproximation, (double)actualSeconds.Numerator / (double)actualSeconds.Denominator);
    }
    // END timestamps

    /// <summary>
    /// Verifies that Instant.Until returns 23 hours when the instants are noon on both sides of a spring-forward night.
    /// Compared with: .NET agrees once both noons are converted with <c>TimeZoneInfo</c>; the calendar day is 23 hours long.
    /// </summary>
    [Fact]
    public void Test_Until_Returns23Hours_WhenTheDayBetweenLosesAnHourToDst()
    {
        // GIVEN: noon on 8 March 2026 in New York, daylight time
        Instant givenNextNoon = Instant.FromDateTimeOffset(new DateTimeOffset(2026, 3, 8, 12, 0, 0, TimeSpan.FromHours(-4)));

        // SYSTEM UNDER TEST: noon on 7 March 2026 in New York, standard time
        Instant sut = Instant.FromDateTimeOffset(new DateTimeOffset(2026, 3, 7, 12, 0, 0, TimeSpan.FromHours(-5)));

        // WHEN
        PosixSeconds actualSeconds = sut.Until(givenNextNoon);

        // EXPECTATIONS: 82,800 seconds, as .NET's TimeSpan counts them
        TimeSpan tmpNative = new DateTimeOffset(2026, 3, 8, 12, 0, 0, TimeSpan.FromHours(-4)) - new DateTimeOffset(2026, 3, 7, 12, 0, 0, TimeSpan.FromHours(-5));
        PosixSeconds expectedSeconds = PosixSeconds.FromTimeSpan(tmpNative);

        // THEN
        Assert.Equal(expectedSeconds, actualSeconds);
    }

    #endregion

    #region Add

    /// <summary>
    /// Verifies that Instant.Add with POSIX seconds moves the instant by exactly that many seconds.
    /// Compared with: <c>DateTimeOffset.AddSeconds(86_400)</c> lands on the same moment: 13:00 in New York, not noon.
    /// </summary>
    [Fact]
    public void Test_Add_MovesByExactlyThoseSeconds_WhenGivenPosixSeconds()
    {
        // GIVEN: 86,400 POSIX seconds, one nominal day
        PosixSeconds givenSeconds = PosixSeconds.Of(86_400);

        // SYSTEM UNDER TEST: noon on 7 March 2026 in New York, the day before clocks spring forward
        Instant sut = Instant.FromDateTimeOffset(new DateTimeOffset(2026, 3, 7, 12, 0, 0, TimeSpan.FromHours(-5)));

        // WHEN
        Instant actualInstant = sut.Add(givenSeconds);

        // EXPECTATIONS: the moment .NET computes for the same addition
        Instant expectedInstant = Instant.FromDateTimeOffset(new DateTimeOffset(2026, 3, 7, 12, 0, 0, TimeSpan.FromHours(-5)).AddSeconds(86_400));

        // THEN
        Assert.Equal(expectedInstant, actualInstant);
    }

    // BEGIN day-vs-24h
    /// <summary>
    /// Verifies that Instant.Add with SI seconds returns unsupported when no leap-second table is available.
    /// Compared with: <c>TimeSpan</c> has one kind of second, so .NET cannot tell SI seconds from POSIX seconds.
    /// </summary>
    [Fact]
    public void Test_Add_ReturnsUnsupported_WhenGivenSiSecondsWithoutALeapSecondTable()
    {
        // GIVEN: 86,400 SI seconds, as a physicist measures a day
        ElapsedSeconds givenSeconds = ElapsedSeconds.Of(86_400);

        // SYSTEM UNDER TEST: noon on 7 March 2026 in New York
        Instant sut = Instant.FromDateTimeOffset(new DateTimeOffset(2026, 3, 7, 12, 0, 0, TimeSpan.FromHours(-5)));

        // WHEN
        Outcome<Instant> actualOutcome = sut.Add(givenSeconds);

        // EXPECTATIONS: unsupported, because SI seconds on a POSIX timescale need leap seconds
        UtvStatus expectedStatus = UtvStatus.Unsupported;
        string expectedCode = "needs-leap-seconds";

        // THEN
        Assert.Equal(expectedStatus, actualOutcome.Status);
        Assert.Contains(expectedCode, actualOutcome.Diagnostics.Codes);
    }
    // END day-vs-24h

    #endregion

    #region InZone

    // BEGIN meeting-in-paris
    /// <summary>
    /// Verifies that Instant.InZone returns the wall time and offset in the zone when the zone has rules for it.
    /// Compared with: <c>TimeZoneInfo.ConvertTimeFromUtc</c> gives the same wall time from this machine's tz database.
    /// </summary>
    [Fact]
    public void Test_InZone_ReturnsTheLocalWallTime_WhenTheZoneHasRules()
    {
        // GIVEN: New York
        string givenZoneId = "America/New_York";

        // SETUP: the pinned demo zone rules, never this machine's tz database
        TemporalContext envContext = TemporalContext.With(Fixtures.DemoZoneRules());

        // SYSTEM UNDER TEST: 07:00 UTC on 1 June 2027, when the meeting starts in Paris
        Instant sut = Instant.FromDateTimeOffset(new DateTimeOffset(2027, 6, 1, 7, 0, 0, TimeSpan.Zero));

        // WHEN
        Outcome<ZonedIntent> actualOutcome = sut.InZone(givenZoneId, envContext);

        // EXPECTATIONS: the wall time .NET computes, with the offset UTV asserts alongside it
        TimeZoneInfo tmpNewYork = TimeZoneInfo.FindSystemTimeZoneById("America/New_York");
        DateTime tmpWallTime = TimeZoneInfo.ConvertTimeFromUtc(new DateTime(2027, 6, 1, 7, 0, 0, DateTimeKind.Utc), tmpNewYork);
        CivilDateTime expectedWallTime = CivilDateTime.FromDateTime(tmpWallTime).Single();
        string expectedText = "2027-06-01T03:00:00-04:00[America/New_York]";

        // THEN
        Assert.Equal(expectedWallTime, actualOutcome.Single().DateTime);
        Assert.Equal(expectedText, actualOutcome.Single().ToString());
    }
    // END meeting-in-paris

    #endregion
}
