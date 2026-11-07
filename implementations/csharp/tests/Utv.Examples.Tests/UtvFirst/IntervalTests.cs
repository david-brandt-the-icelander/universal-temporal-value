using System;
using Xunit;

namespace Utv.Examples.Tests.UtvFirst;

/// <summary>
/// How to use <c>Interval</c>, a range of dates or instants whose ends say whether they are included, unbounded or
/// unknown, compared with .NET, which has no date range type: bounds are compared by hand and a missing end is
/// <c>null</c>, whatever the reason.
/// </summary>
public class IntervalTests
{
    #region HalfOpen

    // BEGIN ranges
    /// <summary>
    /// Verifies that a half-open Interval definitely contains a day between its ends.
    /// Compared with: <c>day &gt;= first &amp;&amp; day &lt; end</c> on <c>DateOnly</c> values says the same.
    /// </summary>
    [Fact]
    public void Test_HalfOpen_DefinitelyContainsTheDay_WhenTheDayIsBetweenTheEnds()
    {
        // GIVEN: 15 November 2026
        CivilDate givenDay = CivilDate.Of(2026, 11, 15);

        // SYSTEM UNDER TEST: November 2026, from the 1st up to but not including 1 December
        Interval<CivilDate> sut = Interval.HalfOpen(CivilDate.Of(2026, 11, 1), CivilDate.Of(2026, 12, 1));

        // WHEN
        Truth actualContains = sut.Contains(givenDay);

        // EXPECTATIONS: the answer .NET's hand-written bounds give
        DateOnly tmpDay = new(2026, 11, 15);
        bool tmpNativeContains = tmpDay >= new DateOnly(2026, 11, 1) && tmpDay < new DateOnly(2026, 12, 1);
        Truth expectedContains = tmpNativeContains ? Truth.Definitely : Truth.Impossible;

        // THEN
        Assert.Equal(expectedContains, actualContains);
    }

    /// <summary>
    /// Verifies that a half-open Interval cannot contain its excluded end.
    /// Compared with: the hand-written <c>day &lt; end</c> excludes it too, if nobody writes <c>&lt;=</c> by mistake.
    /// </summary>
    [Fact]
    public void Test_HalfOpen_CannotContainTheDay_WhenTheDayIsTheExcludedEnd()
    {
        // GIVEN: 1 December 2026, the end of the interval
        CivilDate givenDay = CivilDate.Of(2026, 12, 1);

        // SYSTEM UNDER TEST: November 2026, from the 1st up to but not including 1 December
        Interval<CivilDate> sut = Interval.HalfOpen(CivilDate.Of(2026, 11, 1), CivilDate.Of(2026, 12, 1));

        // WHEN
        Truth actualContains = sut.Contains(givenDay);

        // EXPECTATIONS: impossible, and written with the end's bracket open
        Truth expectedContains = Truth.Impossible;
        string expectedText = "[2026-11-01, 2026-12-01)";

        // THEN
        Assert.Equal(expectedContains, actualContains);
        Assert.Equal(expectedText, sut.ToString());
    }
    // END ranges

    #endregion

    #region Closed

    /// <summary>
    /// Verifies that a closed Interval definitely contains its last day.
    /// Compared with: <c>day &gt;= first &amp;&amp; day &lt;= last</c> on <c>DateOnly</c> values says the same.
    /// </summary>
    [Fact]
    public void Test_Closed_DefinitelyContainsTheDay_WhenTheDayIsTheIncludedEnd()
    {
        // GIVEN: 30 November 2026, the last day of the interval
        CivilDate givenDay = CivilDate.Of(2026, 11, 30);

        // SYSTEM UNDER TEST: November 2026, from the 1st to the 30th, both included
        Interval<CivilDate> sut = Interval.Closed(CivilDate.Of(2026, 11, 1), CivilDate.Of(2026, 11, 30));

        // WHEN
        Truth actualContains = sut.Contains(givenDay);

        // EXPECTATIONS: the answer .NET's hand-written bounds give
        DateOnly tmpDay = new(2026, 11, 30);
        bool tmpNativeContains = tmpDay >= new DateOnly(2026, 11, 1) && tmpDay <= new DateOnly(2026, 11, 30);
        Truth expectedContains = tmpNativeContains ? Truth.Definitely : Truth.Impossible;

        // THEN
        Assert.Equal(expectedContains, actualContains);
    }

    #endregion

    #region Of

    /// <summary>
    /// Verifies that an Interval with an unbounded end definitely contains any later day.
    /// Compared with: a <c>DateOnly?</c> end of <c>null</c> means "no end" here, by convention only.
    /// </summary>
    [Fact]
    public void Test_Of_DefinitelyContainsALaterDay_WhenTheEndIsUnbounded()
    {
        // GIVEN: 1 January 3000
        CivilDate givenDay = CivilDate.Of(3000, 1, 1);

        // SYSTEM UNDER TEST: from 1 November 2026 on, with no end at all
        Interval<CivilDate> sut = Interval.Of(Bound.Included(CivilDate.Of(2026, 11, 1)), Bound.Unbounded);

        // WHEN
        Truth actualContains = sut.Contains(givenDay);

        // EXPECTATIONS: definitely, and written with ".." for the missing end
        Truth expectedContains = Truth.Definitely;
        string expectedText = "[2026-11-01, ..)";

        // THEN
        Assert.Equal(expectedContains, actualContains);
        Assert.Equal(expectedText, sut.ToString());
    }

    // BEGIN ranges
    /// <summary>
    /// Verifies that an Interval with an unknown end only possibly contains a later day.
    /// Compared with: the same <c>null</c> end would be read as "no end", and the answer would wrongly be "yes".
    /// </summary>
    [Fact]
    public void Test_Of_PossiblyContainsALaterDay_WhenTheEndIsUnknown()
    {
        // GIVEN: 1 June 2027
        CivilDate givenDay = CivilDate.Of(2027, 6, 1);

        // SYSTEM UNDER TEST: from 1 November 2026, until an end nobody knows yet
        Interval<CivilDate> sut = Interval.Of(Bound.Included(CivilDate.Of(2026, 11, 1)), Bound.Unknown);

        // WHEN
        Truth actualContains = sut.Contains(givenDay);

        // EXPECTATIONS: possibly, and written with "?" for the unknown end
        Truth expectedContains = Truth.Possibly;
        string expectedText = "[2026-11-01, ?)";

        // THEN
        Assert.Equal(expectedContains, actualContains);
        Assert.Equal(expectedText, sut.ToString());
    }
    // END ranges

    #endregion
}
