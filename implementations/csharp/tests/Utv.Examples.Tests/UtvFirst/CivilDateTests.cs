using System;
using System.Globalization;
using System.Numerics;
using Xunit;

namespace Utv.Examples.Tests.UtvFirst;

/// <summary>
/// How to use <c>CivilDate</c>, a day in the Gregorian calendar with no time and no zone, compared with .NET's
/// <c>DateOnly</c>. Where the two agree, the expected value is computed with <c>DateOnly</c>.
/// </summary>
public class CivilDateTests
{
    #region Of

    // BEGIN a-date
    /// <summary>
    /// Verifies that CivilDate.Of creates the date when the fields name a real day.
    /// Compared with: <c>new DateOnly(2026, 11, 5)</c> names the same day.
    /// </summary>
    [Fact]
    public void Test_Of_CreatesTheDate_WhenTheFieldsNameARealDay()
    {
        // GIVEN: 5 November 2026
        int givenYear = 2026;
        int givenMonth = 11;
        int givenDay = 5;

        // WHEN
        CivilDate actualDate = CivilDate.Of(givenYear, givenMonth, givenDay);

        // EXPECTATIONS: the same day as .NET's DateOnly
        CivilDate expectedDate = CivilDate.FromDateOnly(new DateOnly(2026, 11, 5));
        string expectedText = "2026-11-05";

        // THEN
        Assert.Equal(expectedDate, actualDate);
        Assert.Equal(expectedText, actualDate.ToString());
    }
    // END a-date

    /// <summary>
    /// Verifies that CivilDate.Of accepts 29 February when the year is a leap year.
    /// Compared with: <c>new DateOnly(2024, 2, 29)</c> accepts it too.
    /// </summary>
    [Fact]
    public void Test_Of_AcceptsTheLeapDay_WhenTheYearIsALeapYear()
    {
        // GIVEN: 29 February 2024, a year divisible by 4
        int givenYear = 2024;
        int givenMonth = 2;
        int givenDay = 29;

        // WHEN
        CivilDate actualDate = CivilDate.Of(givenYear, givenMonth, givenDay);

        // EXPECTATIONS: the same day as .NET's DateOnly
        CivilDate expectedDate = CivilDate.FromDateOnly(new DateOnly(2024, 2, 29));

        // THEN
        Assert.Equal(expectedDate, actualDate);
    }

    /// <summary>
    /// Verifies that CivilDate.Of accepts 29 February when the year is a century divisible by 400.
    /// Compared with: <c>new DateOnly(2000, 2, 29)</c> accepts it too.
    /// </summary>
    [Fact]
    public void Test_Of_AcceptsTheLeapDay_WhenTheCenturyIsDivisibleBy400()
    {
        // GIVEN: 29 February 2000, a century year divisible by 400
        int givenYear = 2000;
        int givenMonth = 2;
        int givenDay = 29;

        // WHEN
        CivilDate actualDate = CivilDate.Of(givenYear, givenMonth, givenDay);

        // EXPECTATIONS: the same day as .NET's DateOnly
        CivilDate expectedDate = CivilDate.FromDateOnly(new DateOnly(2000, 2, 29));

        // THEN
        Assert.Equal(expectedDate, actualDate);
    }

    /// <summary>
    /// Verifies that CivilDate.Of throws when the day does not exist in the month.
    /// Compared with: <c>new DateOnly(2026, 2, 30)</c> throws the same exception type.
    /// </summary>
    [Fact]
    public void Test_Of_Throws_WhenTheDayDoesNotExist()
    {
        // GIVEN: 30 February 2026
        int givenYear = 2026;
        int givenMonth = 2;
        int givenDay = 30;

        // WHEN
        Exception? actualException = Record.Exception(() => CivilDate.Of(givenYear, givenMonth, givenDay));

        // EXPECTATIONS: the exception .NET's DateOnly throws for the same fields
        Type expectedExceptionType = typeof(ArgumentOutOfRangeException);

        // THEN
        Assert.IsType(expectedExceptionType, actualException);
    }

    /// <summary>
    /// Verifies that CivilDate.Of creates the date when the year is beyond 9999.
    /// Compared with: <c>DateOnly</c> ends at 9999-12-31 and cannot hold it.
    /// </summary>
    [Fact]
    public void Test_Of_CreatesTheDate_WhenTheYearIsBeyond9999()
    {
        // GIVEN: 13 September 275760, the last day a JavaScript Date can hold
        BigInteger givenYear = 275_760;
        int givenMonth = 9;
        int givenDay = 13;

        // WHEN
        CivilDate actualDate = CivilDate.Of(givenYear, givenMonth, givenDay);

        // EXPECTATIONS: an expanded year, written with a sign
        string expectedText = "+275760-09-13";

        // THEN
        Assert.Equal(expectedText, actualDate.ToString());
    }

    /// <summary>
    /// Verifies that CivilDate.Of creates the date when the year is before year 1.
    /// Compared with: <c>DateOnly</c> starts at 0001-01-01 and cannot hold it.
    /// </summary>
    [Fact]
    public void Test_Of_CreatesTheDate_WhenTheYearIsNegative()
    {
        // GIVEN: 1 March of year -500 in the proleptic Gregorian calendar
        int givenYear = -500;
        int givenMonth = 3;
        int givenDay = 1;

        // WHEN
        CivilDate actualDate = CivilDate.Of(givenYear, givenMonth, givenDay);

        // EXPECTATIONS: a signed, four-digit year
        string expectedText = "-0500-03-01";

        // THEN
        Assert.Equal(expectedText, actualDate.ToString());
    }

    #endregion

    #region TryOf

    // BEGIN feb-30
    /// <summary>
    /// Verifies that CivilDate.TryOf returns invalid when the day is 30 February.
    /// Compared with: <c>new DateOnly(2026, 2, 30)</c> throws; TryOf reports the problem as a value instead.
    /// </summary>
    [Fact]
    public void Test_TryOf_ReturnsInvalid_WhenTheDayIs30February()
    {
        // GIVEN: 30 February 2026
        int givenYear = 2026;
        int givenMonth = 2;
        int givenDay = 30;

        // WHEN
        Outcome<CivilDate> actualOutcome = CivilDate.TryOf(givenYear, givenMonth, givenDay);

        // EXPECTATIONS: invalid, because February 2026 has 28 days
        UtvStatus expectedStatus = UtvStatus.Invalid;
        string expectedCode = "day-out-of-range";

        // THEN
        Assert.Equal(expectedStatus, actualOutcome.Status);
        Assert.Contains(expectedCode, actualOutcome.Diagnostics.Codes);
    }
    // END feb-30

    /// <summary>
    /// Verifies that CivilDate.TryOf returns invalid when 29 February falls in a common year.
    /// Compared with: <c>DateTime.IsLeapYear(2026)</c> is false and <c>new DateOnly(2026, 2, 29)</c> throws.
    /// </summary>
    [Fact]
    public void Test_TryOf_ReturnsInvalid_When29FebruaryFallsInACommonYear()
    {
        // GIVEN: 29 February 2026, a year not divisible by 4
        int givenYear = 2026;
        int givenMonth = 2;
        int givenDay = 29;

        // WHEN
        Outcome<CivilDate> actualOutcome = CivilDate.TryOf(givenYear, givenMonth, givenDay);

        // EXPECTATIONS: invalid, rather than rolled over to 1 March
        UtvStatus expectedStatus = UtvStatus.Invalid;
        string expectedCode = "day-out-of-range";

        // THEN
        Assert.Equal(expectedStatus, actualOutcome.Status);
        Assert.Contains(expectedCode, actualOutcome.Diagnostics.Codes);
    }

    /// <summary>
    /// Verifies that CivilDate.TryOf returns invalid when 29 February falls in a century not divisible by 400.
    /// Compared with: <c>DateTime.IsLeapYear(1900)</c> is false too.
    /// </summary>
    [Fact]
    public void Test_TryOf_ReturnsInvalid_When29FebruaryFallsInACenturyNotDivisibleBy400()
    {
        // GIVEN: 29 February 1900, divisible by 100 but not by 400
        int givenYear = 1900;
        int givenMonth = 2;
        int givenDay = 29;

        // WHEN
        Outcome<CivilDate> actualOutcome = CivilDate.TryOf(givenYear, givenMonth, givenDay);

        // EXPECTATIONS: invalid, because 1900 was not a leap year
        UtvStatus expectedStatus = UtvStatus.Invalid;
        string expectedCode = "day-out-of-range";

        // THEN
        Assert.Equal(expectedStatus, actualOutcome.Status);
        Assert.Contains(expectedCode, actualOutcome.Diagnostics.Codes);
    }

    /// <summary>
    /// Verifies that CivilDate.TryOf returns invalid when the month is 13.
    /// Compared with: <c>new DateOnly(2026, 13, 1)</c> throws.
    /// </summary>
    [Fact]
    public void Test_TryOf_ReturnsInvalid_WhenTheMonthIs13()
    {
        // GIVEN: the first day of a thirteenth month
        int givenYear = 2026;
        int givenMonth = 13;
        int givenDay = 1;

        // WHEN
        Outcome<CivilDate> actualOutcome = CivilDate.TryOf(givenYear, givenMonth, givenDay);

        // EXPECTATIONS: invalid, because a Gregorian year has 12 months
        UtvStatus expectedStatus = UtvStatus.Invalid;
        string expectedCode = "month-out-of-range";

        // THEN
        Assert.Equal(expectedStatus, actualOutcome.Status);
        Assert.Contains(expectedCode, actualOutcome.Diagnostics.Codes);
    }

    #endregion

    #region Parse

    /// <summary>
    /// Verifies that CivilDate.Parse returns the date when the text is an ISO 8601 calendar date.
    /// Compared with: <c>DateOnly.Parse("2026-11-05", CultureInfo.InvariantCulture)</c> reads the same day.
    /// </summary>
    [Fact]
    public void Test_Parse_ReturnsTheDate_WhenTheTextIsAnIsoDate()
    {
        // GIVEN: the ISO 8601 text of 5 November 2026
        string givenText = "2026-11-05";

        // WHEN
        Outcome<CivilDate> actualOutcome = CivilDate.Parse(givenText);

        // EXPECTATIONS: the day .NET parses from the same text
        CivilDate expectedDate = CivilDate.FromDateOnly(DateOnly.Parse("2026-11-05", CultureInfo.InvariantCulture));

        // THEN
        Assert.Equal(expectedDate, actualOutcome.Single());
    }

    /// <summary>
    /// Verifies that CivilDate.Parse returns invalid, with the reason, when the text names a day that does not exist.
    /// Compared with: <c>DateOnly.TryParse</c> returns false and gives no reason.
    /// </summary>
    [Fact]
    public void Test_Parse_ReturnsInvalid_WhenTheTextNamesADayThatDoesNotExist()
    {
        // GIVEN: well-formed text for 30 February 2026
        string givenText = "2026-02-30";

        // WHEN
        Outcome<CivilDate> actualOutcome = CivilDate.Parse(givenText);

        // EXPECTATIONS: invalid, and the diagnostic says which field is wrong
        UtvStatus expectedStatus = UtvStatus.Invalid;
        string expectedCode = "day-out-of-range";

        // THEN
        Assert.Equal(expectedStatus, actualOutcome.Status);
        Assert.Contains(expectedCode, actualOutcome.Diagnostics.Codes);
    }

    #endregion

    #region DayOfWeek and IsoWeek

    // BEGIN weekday-week
    /// <summary>
    /// Verifies that CivilDate.DayOfWeek returns the weekday of the date.
    /// Compared with: <c>DateOnly.DayOfWeek</c> names the same day, but numbers Sunday 0 where ISO numbers it 7.
    /// </summary>
    [Fact]
    public void Test_DayOfWeek_ReturnsThursday_WhenTheDateIs5November2026()
    {
        // SYSTEM UNDER TEST: 5 November 2026
        CivilDate sut = CivilDate.Of(2026, 11, 5);

        // WHEN
        Weekday actualWeekday = sut.DayOfWeek;

        // EXPECTATIONS: the weekday .NET names for the same day
        string expectedName = new DateOnly(2026, 11, 5).DayOfWeek.ToString();

        // THEN
        Assert.Equal(expectedName, actualWeekday.ToString());
    }

    /// <summary>
    /// Verifies that CivilDate.IsoWeek returns the ISO 8601 week number of the date.
    /// Compared with: <c>ISOWeek.GetWeekOfYear</c> needs a <c>DateTime</c>, so a time of day is invented first.
    /// </summary>
    [Fact]
    public void Test_IsoWeek_Returns45_WhenTheDateIs5November2026()
    {
        // SYSTEM UNDER TEST: 5 November 2026
        CivilDate sut = CivilDate.Of(2026, 11, 5);

        // WHEN
        int actualWeek = sut.IsoWeek;

        // EXPECTATIONS: the ISO week .NET computes for midnight of the same day
        int expectedWeek = ISOWeek.GetWeekOfYear(new DateTime(2026, 11, 5));

        // THEN
        Assert.Equal(expectedWeek, actualWeek);
    }
    // END weekday-week

    #endregion

    #region IsLeapYear

    /// <summary>
    /// Verifies that CivilDate.IsLeapYear follows the Gregorian leap-year rules for every kind of year.
    /// Compared with: <c>DateTime.IsLeapYear</c> gives the same answers.
    /// </summary>
    [Theory]
    [InlineData(2024)] // divisible by 4: leap
    [InlineData(2026)] // not divisible by 4: common
    [InlineData(1900)] // divisible by 100 but not 400: common
    [InlineData(2000)] // divisible by 400: leap
    public void Test_IsLeapYear_AgreesWithDotNet_WhenGivenAnyKindOfYear(int givenYear)
    {
        // SYSTEM UNDER TEST: 1 January of the given year
        CivilDate sut = CivilDate.Of(givenYear, 1, 1);

        // WHEN
        bool actualIsLeapYear = sut.IsLeapYear;

        // EXPECTATIONS: .NET's answer for the same year
        bool expectedIsLeapYear = DateTime.IsLeapYear(givenYear);

        // THEN
        Assert.Equal(expectedIsLeapYear, actualIsLeapYear);
    }

    #endregion

    #region AddDays

    // BEGIN add-days
    /// <summary>
    /// Verifies that CivilDate.AddDays returns the date that many days later, carrying into the next month.
    /// Compared with: <c>DateOnly.AddDays</c> gives the same date.
    /// </summary>
    [Fact]
    public void Test_AddDays_ReturnsTheLaterDate_WhenTheDaysCrossAMonthEnd()
    {
        // GIVEN: 30 days, more than remain in November
        int givenDays = 30;

        // SYSTEM UNDER TEST: 5 November 2026
        CivilDate sut = CivilDate.Of(2026, 11, 5);

        // WHEN
        CivilDate actualDate = sut.AddDays(givenDays);

        // EXPECTATIONS: the date .NET computes for the same addition
        CivilDate expectedDate = CivilDate.FromDateOnly(new DateOnly(2026, 11, 5).AddDays(30));

        // THEN
        Assert.Equal(expectedDate, actualDate);
    }
    // END add-days

    #endregion

    #region AddMonths

    // BEGIN month-end
    /// <summary>
    /// Verifies that CivilDate.AddMonths returns none when the day is missing from the target month.
    /// Compared with: <c>DateOnly.AddMonths</c> clamps 31 January to 28 February without saying so.
    /// </summary>
    [Fact]
    public void Test_AddMonths_ReturnsNone_WhenTheDayIsMissingFromTheTargetMonth()
    {
        // GIVEN: one month, into a February that has no day 31
        int givenMonths = 1;

        // SYSTEM UNDER TEST: 31 January 2026
        CivilDate sut = CivilDate.Of(2026, 1, 31);

        // WHEN
        Outcome<CivilDate> actualOutcome = sut.AddMonths(givenMonths);

        // EXPECTATIONS: no such date, explained by the month-end diagnostic
        UtvStatus expectedStatus = UtvStatus.None;
        string expectedCode = "month-end";

        // THEN
        Assert.Equal(expectedStatus, actualOutcome.Status);
        Assert.Contains(expectedCode, actualOutcome.Diagnostics.Codes);
    }

    /// <summary>
    /// Verifies that CivilDate.AddMonths with MonthEnd.Constrain returns the last day of the target month when the
    /// day is missing from it.
    /// Compared with: <c>DateOnly.AddMonths</c> gives the same date, but without being asked.
    /// </summary>
    [Fact]
    public void Test_AddMonths_ReturnsTheLastDayOfTheMonth_WhenConstrainIsRequested()
    {
        // GIVEN: one month, constrained to the end of the target month
        int givenMonths = 1;
        MonthEnd givenPolicy = MonthEnd.Constrain;

        // SYSTEM UNDER TEST: 31 January 2026
        CivilDate sut = CivilDate.Of(2026, 1, 31);

        // WHEN
        CivilDate actualDate = sut.AddMonths(givenMonths, givenPolicy);

        // EXPECTATIONS: the date .NET clamps to
        CivilDate expectedDate = CivilDate.FromDateOnly(new DateOnly(2026, 1, 31).AddMonths(1));

        // THEN
        Assert.Equal(expectedDate, actualDate);
    }
    // END month-end

    #endregion

    #region Add

    /// <summary>
    /// Verifies that CivilDate.Add returns none when a one-month period lands on a missing day.
    /// Compared with: .NET has no calendar period type; <c>TimeSpan</c> cannot express a month.
    /// </summary>
    [Fact]
    public void Test_Add_ReturnsNone_WhenAOneMonthPeriodLandsOnAMissingDay()
    {
        // GIVEN: a calendar period of one month
        Period givenPeriod = Period.Months(1);

        // SYSTEM UNDER TEST: 31 January 2026
        CivilDate sut = CivilDate.Of(2026, 1, 31);

        // WHEN
        Outcome<CivilDate> actualOutcome = sut.Add(givenPeriod);

        // EXPECTATIONS: no such date under the default, rejecting month-end policy
        UtvStatus expectedStatus = UtvStatus.None;
        string expectedCode = "month-end";

        // THEN
        Assert.Equal(expectedStatus, actualOutcome.Status);
        Assert.Contains(expectedCode, actualOutcome.Diagnostics.Codes);
    }

    #endregion

    #region DaysUntil

    // BEGIN days-between
    /// <summary>
    /// Verifies that CivilDate.DaysUntil returns the number of days to a later date.
    /// Compared with: the difference of the two <c>DateOnly.DayNumber</c> values is the same.
    /// </summary>
    [Fact]
    public void Test_DaysUntil_ReturnsTheDayCount_WhenTheOtherDateIsLater()
    {
        // GIVEN: Christmas Day 2026
        CivilDate givenChristmas = CivilDate.Of(2026, 12, 25);

        // SYSTEM UNDER TEST: 5 November 2026
        CivilDate sut = CivilDate.Of(2026, 11, 5);

        // WHEN
        long actualDays = sut.DaysUntil(givenChristmas);

        // EXPECTATIONS: the difference of .NET's day numbers
        long expectedDays = new DateOnly(2026, 12, 25).DayNumber - new DateOnly(2026, 11, 5).DayNumber;

        // THEN
        Assert.Equal(expectedDays, actualDays);
    }
    // END days-between

    #endregion

    #region CompareTo

    // BEGIN compare-dates
    /// <summary>
    /// Verifies that CivilDate's less-than operator is true when the left date comes first.
    /// Compared with: <c>DateOnly</c>'s operator gives the same answer. Plain dates always have an order.
    /// </summary>
    [Fact]
    public void Test_LessThan_ReturnsTrue_WhenTheLeftDateComesFirst()
    {
        // GIVEN: Christmas Day 2026
        CivilDate givenChristmas = CivilDate.Of(2026, 12, 25);

        // SYSTEM UNDER TEST: 5 November 2026
        CivilDate sut = CivilDate.Of(2026, 11, 5);

        // WHEN
        bool actualComesFirst = sut < givenChristmas;

        // EXPECTATIONS: .NET's answer for the same two days
        bool expectedComesFirst = new DateOnly(2026, 11, 5) < new DateOnly(2026, 12, 25);

        // THEN
        Assert.Equal(expectedComesFirst, actualComesFirst);
    }
    // END compare-dates

    #endregion

    #region ToDateOnly

    /// <summary>
    /// Verifies that CivilDate.ToDateOnly returns unsupported when the year is beyond 9999.
    /// Compared with: <c>DateOnly.MaxValue</c> is 9999-12-31, so there is no DateOnly to return.
    /// </summary>
    [Fact]
    public void Test_ToDateOnly_ReturnsUnsupported_WhenTheYearIsBeyond9999()
    {
        // SYSTEM UNDER TEST: 1 January 10000
        CivilDate sut = CivilDate.Of(10_000, 1, 1);

        // WHEN
        Outcome<DateOnly> actualOutcome = sut.ToDateOnly();

        // EXPECTATIONS: unsupported, because the native type cannot hold the value
        UtvStatus expectedStatus = UtvStatus.Unsupported;
        string expectedCode = "out-of-native-range";

        // THEN
        Assert.Equal(expectedStatus, actualOutcome.Status);
        Assert.Contains(expectedCode, actualOutcome.Diagnostics.Codes);
    }

    #endregion
}
