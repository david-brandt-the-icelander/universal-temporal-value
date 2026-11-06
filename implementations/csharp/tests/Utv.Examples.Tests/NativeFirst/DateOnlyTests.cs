using System;
using System.Globalization;
using Xunit;

namespace Utv.Examples.Tests.NativeFirst;

/// <summary>
/// What .NET's <c>System.DateOnly</c> does, compared with UTV's <c>CivilDate</c>. Where the two agree, the expected
/// value is computed with UTV.
/// </summary>
public class DateOnlyTests
{
    #region Constructor

    /// <summary>
    /// Verifies that the DateOnly constructor creates the date when the fields name a real day.
    /// Compared with: <c>CivilDate.Of(2026, 11, 5)</c> names the same day.
    /// </summary>
    [Fact]
    public void Test_Constructor_CreatesTheDate_WhenTheFieldsNameARealDay()
    {
        // GIVEN: 5 November 2026
        int givenYear = 2026;
        int givenMonth = 11;
        int givenDay = 5;

        // WHEN
        DateOnly actualDate = new(givenYear, givenMonth, givenDay);

        // EXPECTATIONS: the same day as UTV's CivilDate
        DateOnly expectedDate = CivilDate.Of(2026, 11, 5).ToDateOnly().Single();

        // THEN
        Assert.Equal(expectedDate, actualDate);
    }

    /// <summary>
    /// Verifies that the DateOnly constructor throws when the day does not exist in the month.
    /// Compared with: <c>CivilDate.Of</c> throws the same exception type; <c>CivilDate.TryOf</c> returns invalid instead.
    /// </summary>
    [Fact]
    public void Test_Constructor_Throws_WhenTheDayDoesNotExist()
    {
        // GIVEN: 30 February 2026
        int givenYear = 2026;
        int givenMonth = 2;
        int givenDay = 30;

        // WHEN
        Exception? actualException = Record.Exception(() => new DateOnly(givenYear, givenMonth, givenDay));

        // EXPECTATIONS: the exception UTV's CivilDate.Of throws for the same fields
        Type expectedExceptionType = typeof(ArgumentOutOfRangeException);

        // THEN
        Assert.IsType(expectedExceptionType, actualException);
    }

    /// <summary>
    /// Verifies that the DateOnly constructor throws when the month is 13.
    /// Compared with: <c>CivilDate.TryOf(2026, 13, 1)</c> returns invalid with month-out-of-range.
    /// </summary>
    [Fact]
    public void Test_Constructor_Throws_WhenTheMonthIs13()
    {
        // GIVEN: the first day of a thirteenth month
        int givenYear = 2026;
        int givenMonth = 13;
        int givenDay = 1;

        // WHEN
        Exception? actualException = Record.Exception(() => new DateOnly(givenYear, givenMonth, givenDay));

        // EXPECTATIONS
        Type expectedExceptionType = typeof(ArgumentOutOfRangeException);

        // THEN
        Assert.IsType(expectedExceptionType, actualException);
    }

    /// <summary>
    /// Verifies that the DateOnly constructor throws when the year is beyond 9999.
    /// Compared with: <c>CivilDate.Of(275760, 9, 13)</c> holds the date; years are arbitrary precision.
    /// </summary>
    [Fact]
    public void Test_Constructor_Throws_WhenTheYearIsBeyond9999()
    {
        // GIVEN: 13 September 275760
        int givenYear = 275_760;
        int givenMonth = 9;
        int givenDay = 13;

        // WHEN
        Exception? actualException = Record.Exception(() => new DateOnly(givenYear, givenMonth, givenDay));

        // EXPECTATIONS
        Type expectedExceptionType = typeof(ArgumentOutOfRangeException);

        // THEN
        Assert.IsType(expectedExceptionType, actualException);
    }

    /// <summary>
    /// Verifies that the DateOnly constructor throws when the year is before year 1.
    /// Compared with: <c>CivilDate.Of(-500, 3, 1)</c> holds the date in the proleptic Gregorian calendar.
    /// </summary>
    [Fact]
    public void Test_Constructor_Throws_WhenTheYearIsNegative()
    {
        // GIVEN: 1 March of year -500
        int givenYear = -500;
        int givenMonth = 3;
        int givenDay = 1;

        // WHEN
        Exception? actualException = Record.Exception(() => new DateOnly(givenYear, givenMonth, givenDay));

        // EXPECTATIONS
        Type expectedExceptionType = typeof(ArgumentOutOfRangeException);

        // THEN
        Assert.IsType(expectedExceptionType, actualException);
    }

    #endregion

    #region MaxValue

    /// <summary>
    /// Verifies that DateOnly.MaxValue is the last day of year 9999.
    /// Compared with: <c>CivilDate.Of(10_000, 1, 1).ToDateOnly()</c> is unsupported for exactly this reason.
    /// </summary>
    [Fact]
    public void Test_MaxValue_IsTheLastDayOf9999_WhenRead()
    {
        // WHEN
        DateOnly actualMaxValue = DateOnly.MaxValue;

        // EXPECTATIONS: 31 December 9999, as UTV converts it
        DateOnly expectedMaxValue = CivilDate.Of(9999, 12, 31).ToDateOnly().Single();

        // THEN
        Assert.Equal(expectedMaxValue, actualMaxValue);
    }

    #endregion

    #region Parse and TryParse

    /// <summary>
    /// Verifies that DateOnly.Parse reads the date when the text is an ISO 8601 calendar date.
    /// Compared with: <c>CivilDate.Parse("2026-11-05")</c> reads the same day, and never depends on a culture.
    /// </summary>
    [Fact]
    public void Test_Parse_ReadsTheDate_WhenTheTextIsAnIsoDate()
    {
        // GIVEN: the ISO 8601 text of 5 November 2026
        string givenText = "2026-11-05";

        // SETUP: the invariant culture, so this machine's locale cannot change the answer
        CultureInfo envCulture = CultureInfo.InvariantCulture;

        // WHEN
        DateOnly actualDate = DateOnly.Parse(givenText, envCulture);

        // EXPECTATIONS: the day UTV parses from the same text
        DateOnly expectedDate = CivilDate.Parse("2026-11-05").Single().ToDateOnly().Single();

        // THEN
        Assert.Equal(expectedDate, actualDate);
    }

    /// <summary>
    /// Verifies that DateOnly.TryParse returns false, with no reason, when the text names a day that does not exist.
    /// Compared with: <c>CivilDate.Parse("2026-02-30")</c> returns invalid with day-out-of-range.
    /// </summary>
    [Fact]
    public void Test_TryParse_ReturnsFalse_WhenTheTextNamesADayThatDoesNotExist()
    {
        // GIVEN: well-formed text for 30 February 2026
        string givenText = "2026-02-30";

        // SETUP: the invariant culture, so this machine's locale cannot change the answer
        CultureInfo envCulture = CultureInfo.InvariantCulture;

        // WHEN
        bool actualParsed = DateOnly.TryParse(givenText, envCulture, out _);

        // EXPECTATIONS: no date, as UTV reports too
        bool expectedParsed = CivilDate.Parse("2026-02-30").Status == UtvStatus.Unique;

        // THEN
        Assert.Equal(expectedParsed, actualParsed);
    }

    #endregion

    #region DayOfWeek

    /// <summary>
    /// Verifies that DateOnly.DayOfWeek numbers Sunday 0 when the date is a Sunday.
    /// Compared with: UTV's <c>Weekday</c> follows ISO 8601, Monday 1 to Sunday 7, so Sunday is 7.
    /// </summary>
    [Fact]
    public void Test_DayOfWeek_NumbersSundayZero_WhenTheDateIsASunday()
    {
        // SYSTEM UNDER TEST: Sunday 8 November 2026
        DateOnly sut = new(2026, 11, 8);

        // WHEN
        DayOfWeek actualDayOfWeek = sut.DayOfWeek;

        // EXPECTATIONS: the day UTV names, numbered 0 where UTV numbers it 7
        string expectedName = CivilDate.Of(2026, 11, 8).DayOfWeek.ToString();
        int expectedNumber = 0;

        // THEN
        Assert.Equal(expectedName, actualDayOfWeek.ToString());
        Assert.Equal(expectedNumber, (int)actualDayOfWeek);
    }

    #endregion

    #region AddDays

    /// <summary>
    /// Verifies that DateOnly.AddDays returns the date that many days later, carrying into the next month.
    /// Compared with: <c>CivilDate.AddDays</c> gives the same date.
    /// </summary>
    [Fact]
    public void Test_AddDays_ReturnsTheLaterDate_WhenTheDaysCrossAMonthEnd()
    {
        // GIVEN: 30 days, more than remain in November
        int givenDays = 30;

        // SYSTEM UNDER TEST: 5 November 2026
        DateOnly sut = new(2026, 11, 5);

        // WHEN
        DateOnly actualDate = sut.AddDays(givenDays);

        // EXPECTATIONS: the date UTV computes for the same addition
        DateOnly expectedDate = CivilDate.Of(2026, 11, 5).AddDays(30).ToDateOnly().Single();

        // THEN
        Assert.Equal(expectedDate, actualDate);
    }

    #endregion

    #region AddMonths

    /// <summary>
    /// Verifies that DateOnly.AddMonths clamps to the last day of the month when the day is missing from it.
    /// Compared with: UTV returns none for <c>CivilDate.AddMonths(1)</c>, and gives this date only when asked with
    /// <c>MonthEnd.Constrain</c>.
    /// </summary>
    [Fact]
    public void Test_AddMonths_ClampsToTheLastDay_WhenTheDayIsMissingFromTheTargetMonth()
    {
        // GIVEN: one month, into a February that has no day 31
        int givenMonths = 1;

        // SYSTEM UNDER TEST: 31 January 2026
        DateOnly sut = new(2026, 1, 31);

        // WHEN
        DateOnly actualDate = sut.AddMonths(givenMonths);

        // EXPECTATIONS: the date UTV gives only with the explicit Constrain policy
        DateOnly expectedDate = CivilDate.Of(2026, 1, 31).AddMonths(1, MonthEnd.Constrain).ToDateOnly().Single();

        // THEN
        Assert.Equal(expectedDate, actualDate);
    }

    #endregion

    #region DayNumber

    /// <summary>
    /// Verifies that the difference of two DateOnly.DayNumber values is the number of days between the dates.
    /// Compared with: <c>CivilDate.DaysUntil</c> returns the same count directly.
    /// </summary>
    [Fact]
    public void Test_DayNumber_DiffersByTheDayCount_WhenSubtracted()
    {
        // GIVEN: Christmas Day 2026
        DateOnly givenChristmas = new(2026, 12, 25);

        // SYSTEM UNDER TEST: 5 November 2026
        DateOnly sut = new(2026, 11, 5);

        // WHEN
        int actualDays = givenChristmas.DayNumber - sut.DayNumber;

        // EXPECTATIONS: the count UTV computes
        long expectedDays = CivilDate.Of(2026, 11, 5).DaysUntil(CivilDate.Of(2026, 12, 25));

        // THEN
        Assert.Equal(expectedDays, actualDays);
    }

    #endregion

    #region Comparison operators

    /// <summary>
    /// Verifies that DateOnly's less-than operator is true when the left date comes first.
    /// Compared with: <c>CivilDate</c>'s operator gives the same answer.
    /// </summary>
    [Fact]
    public void Test_LessThan_ReturnsTrue_WhenTheLeftDateComesFirst()
    {
        // GIVEN: Christmas Day 2026
        DateOnly givenChristmas = new(2026, 12, 25);

        // SYSTEM UNDER TEST: 5 November 2026
        DateOnly sut = new(2026, 11, 5);

        // WHEN
        bool actualComesFirst = sut < givenChristmas;

        // EXPECTATIONS: UTV's answer for the same two days
        bool expectedComesFirst = CivilDate.Of(2026, 11, 5) < CivilDate.Of(2026, 12, 25);

        // THEN
        Assert.Equal(expectedComesFirst, actualComesFirst);
    }

    #endregion
}
