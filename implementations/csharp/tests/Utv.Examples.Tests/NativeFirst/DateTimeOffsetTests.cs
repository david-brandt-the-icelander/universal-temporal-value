using System;
using System.Globalization;
using Xunit;

namespace Utv.Examples.Tests.NativeFirst;

/// <summary>
/// What .NET's <c>System.DateTimeOffset</c> does, compared with UTV's <c>Instant</c> and
/// <c>FixedOffsetDateTime</c>.
/// </summary>
public class DateTimeOffsetTests
{
    #region UtcNow

    /// <summary>
    /// Verifies that DateTimeOffset.UtcNow returns a reading that UTV takes as an input without changing it.
    /// Compared with: UTV never reads the clock; <c>Instant.FromDateTimeOffset</c> is how the reading gets in.
    /// </summary>
    /// <remarks>
    /// This test reads the real clock on purpose: the round trip does not depend on the time read, so the result is the
    /// same on every run. If it ever flakes, that is a finding about the conversion, not noise.
    /// </remarks>
    [Fact]
    public void Test_UtcNow_SurvivesTheTripThroughUtv_WhenReadAtAnyTime()
    {
        // WHEN
        DateTimeOffset actualReading = DateTimeOffset.UtcNow;

        // EXPECTATIONS: the same reading after UTV has held it as an Instant
        DateTimeOffset expectedReading = Instant.FromDateTimeOffset(actualReading).ToDateTimeOffset().Single();

        // THEN
        Assert.True(expectedReading.EqualsExact(actualReading));
    }

    #endregion

    #region Constructor

    /// <summary>
    /// Verifies that the DateTimeOffset constructor pins the wall time to the offset when given one.
    /// Compared with: <c>CivilDateTime.Of(2026, 11, 5, 14, 30).AtOffset(3600)</c> is the same value, and its
    /// <c>ToInstant()</c> is the same moment.
    /// </summary>
    [Fact]
    public void Test_Constructor_PinsTheWallTimeToTheOffset_WhenGivenAnOffset()
    {
        // GIVEN: 14:30 on 5 November 2026, one hour east of UTC
        TimeSpan givenOffset = TimeSpan.FromHours(1);

        // WHEN
        DateTimeOffset actualDateTime = new(2026, 11, 5, 14, 30, 0, givenOffset);

        // EXPECTATIONS: the moment UTV computes for the same wall time and offset
        DateTimeOffset expectedInstant = CivilDateTime.Of(2026, 11, 5, 14, 30).AtOffset(3600).ToInstant().ToDateTimeOffset().Single();

        // THEN
        Assert.Equal(expectedInstant.UtcDateTime, actualDateTime.UtcDateTime);
    }

    #endregion

    #region Parse

    /// <summary>
    /// Verifies that DateTimeOffset.Parse gives the same value for text ending in Z and text ending in +00:00.
    /// Compared with: <c>Temporal.Parse</c> returns an <c>Instant</c> for Z and a <c>FixedOffsetDateTime</c> for
    /// +00:00; the two texts mean different things.
    /// </summary>
    [Fact]
    public void Test_Parse_CannotTellZFromPlusZero_WhenBothNameTheSameMoment()
    {
        // GIVEN: 14:30 UTC, written with Z
        string givenText = "2026-11-05T14:30:00Z";

        // SETUP: the invariant culture, so this machine's locale cannot change the answer
        CultureInfo envCulture = CultureInfo.InvariantCulture;

        // WHEN
        DateTimeOffset actualValue = DateTimeOffset.Parse(givenText, envCulture);

        // EXPECTATIONS: exactly the value .NET parses from "+00:00", which UTV keeps apart as a FixedOffsetDateTime
        DateTimeOffset expectedValue = DateTimeOffset.Parse("2026-11-05T14:30:00+00:00", envCulture);

        // THEN
        Assert.True(expectedValue.EqualsExact(actualValue));
    }

    #endregion

    #region FromUnixTimeSeconds and ToUnixTimeSeconds

    /// <summary>
    /// Verifies that DateTimeOffset.FromUnixTimeSeconds returns the Unix epoch when given zero seconds.
    /// Compared with: <c>Instant.FromUnixSeconds(0)</c> is the same moment.
    /// </summary>
    [Fact]
    public void Test_FromUnixTimeSeconds_ReturnsTheEpoch_WhenGivenZero()
    {
        // GIVEN: zero seconds since 1970-01-01T00:00:00Z
        long givenSeconds = 0;

        // WHEN
        DateTimeOffset actualEpoch = DateTimeOffset.FromUnixTimeSeconds(givenSeconds);

        // EXPECTATIONS: the moment UTV returns for the same count
        DateTimeOffset expectedEpoch = Instant.FromUnixSeconds(0).ToDateTimeOffset().Single();

        // THEN
        Assert.True(expectedEpoch.EqualsExact(actualEpoch));
    }

    /// <summary>
    /// Verifies that DateTimeOffset.ToUnixTimeSeconds drops the fraction of a second when there is one.
    /// Compared with: <c>Instant.UnixSeconds</c> is the same floor, and <c>Instant.SubSecond</c> keeps the rest exactly.
    /// </summary>
    [Fact]
    public void Test_ToUnixTimeSeconds_DropsTheFraction_WhenTheMomentHasAQuarterSecond()
    {
        // SYSTEM UNDER TEST: 13:30:00.25 on 5 November 2026
        DateTimeOffset sut = DateTimeOffset.FromUnixTimeMilliseconds(1_793_885_400_250);

        // WHEN
        long actualSeconds = sut.ToUnixTimeSeconds();

        // EXPECTATIONS: the whole seconds UTV reports, without its quarter second
        long expectedSeconds = (long)Instant.FromUnixSeconds(1_793_885_400, 250_000_000).UnixSeconds;

        // THEN
        Assert.Equal(expectedSeconds, actualSeconds);
    }

    #endregion

    #region AddTicks

    /// <summary>
    /// Verifies that DateTimeOffset keeps only seven fractional digits when given a time to the nanosecond.
    /// Compared with: <c>Instant.FromUnixSeconds(1_793_795_400, 123_456_789)</c> keeps all nine, and its
    /// <c>ToDateTimeOffset()</c> is unsupported rather than silently rounded.
    /// </summary>
    [Fact]
    public void Test_AddTicks_KeepsSevenDigits_WhenGivenNanoseconds()
    {
        // GIVEN: 123,456,789 nanoseconds, as whole 100-nanosecond ticks
        long givenTicks = 123_456_789 / 100;

        // SYSTEM UNDER TEST: 12:30 UTC on 4 November 2026
        DateTimeOffset sut = DateTimeOffset.FromUnixTimeSeconds(1_793_795_400);

        // WHEN
        DateTimeOffset actualMoment = sut.AddTicks(givenTicks);

        // EXPECTATIONS: the moment UTV holds once the last two of its nine digits are dropped
        DateTimeOffset expectedMoment = Instant.FromUnixSeconds(1_793_795_400, 123_456_700).ToDateTimeOffset().Single();

        // THEN
        Assert.True(expectedMoment.EqualsExact(actualMoment));
    }

    #endregion

    #region Subtraction

    /// <summary>
    /// Verifies that subtracting two DateTimeOffsets gives a TimeSpan whose TotalSeconds is a double.
    /// Compared with: <c>Instant.Until</c> returns the exact fraction 14403/4.
    /// </summary>
    [Fact]
    public void Test_Subtraction_ReturnsADouble_WhenTheMomentsAreAFractionOfASecondApart()
    {
        // GIVEN: one hour, 0.75 seconds later
        DateTimeOffset givenLater = DateTimeOffset.FromUnixTimeSeconds(1_793_889_001);

        // SYSTEM UNDER TEST: 13:30:00.25 on 5 November 2026
        DateTimeOffset sut = DateTimeOffset.FromUnixTimeMilliseconds(1_793_885_400_250);

        // WHEN
        double actualSeconds = (givenLater - sut).TotalSeconds;

        // EXPECTATIONS: UTV's exact answer, as the nearest double
        PosixSeconds tmpExact = Instant.FromUnixSeconds(1_793_885_400, 250_000_000).Until(Instant.FromUnixSeconds(1_793_889_001));
        double expectedSeconds = (double)tmpExact.Numerator / (double)tmpExact.Denominator;

        // THEN
        Assert.Equal(expectedSeconds, actualSeconds);
    }

    #endregion

    #region AddDays

    /// <summary>
    /// Verifies that DateTimeOffset.AddDays keeps the offset when the day crosses a daylight-saving change.
    /// Compared with: <c>ZonedIntent.AddDays</c> keeps the wall time, noon, and lets the offset follow the zone.
    /// </summary>
    [Fact]
    public void Test_AddDays_KeepsTheOffset_WhenTheDayCrossesADstChange()
    {
        // GIVEN: one day
        double givenDays = 1;

        // SYSTEM UNDER TEST: noon on 7 March 2026 in New York, standard time, the day before clocks spring forward
        DateTimeOffset sut = new(2026, 3, 7, 12, 0, 0, TimeSpan.FromHours(-5));

        // WHEN
        DateTimeOffset actualDayLater = sut.AddDays(givenDays);

        // EXPECTATIONS: exactly 86,400 seconds later, which UTV places at 13:00 daylight time, not noon
        DateTimeOffset expectedDayLater = Instant.FromDateTimeOffset(sut).Add(PosixSeconds.Of(86_400)).ToDateTimeOffset().Single();
        TimeSpan expectedOffset = TimeSpan.FromHours(-5);

        // THEN
        Assert.Equal(expectedDayLater.UtcDateTime, actualDayLater.UtcDateTime);
        Assert.Equal(expectedOffset, actualDayLater.Offset);
    }

    #endregion

    #region EqualsExact

    /// <summary>
    /// Verifies that DateTimeOffset.EqualsExact is true when the value made a round trip through UTV.
    /// Compared with: <c>FixedOffsetDateTime.FromDateTimeOffset</c> and <c>ToDateTimeOffset</c> lose nothing.
    /// </summary>
    [Fact]
    public void Test_EqualsExact_ReturnsTrue_WhenTheValueMadeARoundTripThroughUtv()
    {
        // GIVEN: the same lunch, after a round trip through UTV's FixedOffsetDateTime
        DateTimeOffset givenRoundTrip = FixedOffsetDateTime
            .FromDateTimeOffset(new DateTimeOffset(2026, 11, 5, 14, 30, 0, TimeSpan.FromHours(1)))
            .ToDateTimeOffset()
            .Single();

        // SYSTEM UNDER TEST: lunch at 14:30 on 5 November 2026, one hour east of UTC
        DateTimeOffset sut = new(2026, 11, 5, 14, 30, 0, TimeSpan.FromHours(1));

        // WHEN
        bool actualEqual = sut.EqualsExact(givenRoundTrip);

        // EXPECTATIONS
        bool expectedEqual = true;

        // THEN
        Assert.Equal(expectedEqual, actualEqual);
    }

    #endregion
}
