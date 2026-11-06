using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text.Json;
using Xunit;
using DateOutcome = Utv.Outcome<Utv.CivilDate>;
using InstantOutcome = Utv.Outcome<Utv.Instant>;

namespace Utv.Tests;

/// <summary>The typed layer of docs/library-api.md: one test or more per row of §2, plus §1, §3 and §4.</summary>
public class TypedApiTests
{
    private static readonly TemporalContext Ctx = TemporalContext.With(DemoRules.Value);

    public static IEnumerable<object[]> Samples() => new ITemporalValue[]
    {
        CivilDate.Of(2026, 11, 5),
        CivilDate.Of(BigInteger.Parse("275760"), 9, 13),
        CivilDate.Of(-500, 3, 1),
        CivilYearMonth.Of(2026, 11),
        CivilTime.Of(9, 30),
        CivilTime.Of(9, 30, 0),
        CivilTime.Of(9, 30, 15, 250_000_000),
        CivilDateTime.Of(2026, 11, 5, 14, 30),
        CivilDateTime.Of(2026, 11, 5, 14, 30).AtOffset(3600),
        CivilDateTime.Of(2027, 6, 1, 9, 0).InZone("Europe/Paris"),
        ZonedIntent.Parse("2027-06-01T09:00+02:00[Europe/Paris]").Single(),
        Instant.FromUnixSeconds(1_793_885_400),
        Instant.FromUnixSeconds(0, 500_000_000),
        Period.Months(1),
        Period.Of(years: 1, months: 2, days: 10, hours: 2, minutes: 30),
        Period.Days(1).Negate(),
        Period.Zero,
        Interval.HalfOpen(CivilDate.Of(2026, 11, 1), CivilDate.Of(2026, 12, 1)),
        Interval.Of(Bound.Included(CivilDate.Of(2026, 11, 1)), Bound.Unbounded),
        Interval.Of(Bound.Unknown, Bound.Excluded(CivilDate.Of(2026, 12, 1))),
        Interval.Closed(Instant.FromUnixSeconds(0), Instant.FromUnixSeconds(60)),
    }.Select(v => new object[] { v });

    // ------------------------------------------------------------------ §1 rules for every type

    [Theory]
    [MemberData(nameof(Samples))]
    public void ToStringRoundTripsThroughParse(ITemporalValue value)
    {
        ITemporalValue back = Temporal.Parse(value.ToString()).Single();
        Assert.Equal(value, back);
        Assert.Equal(value.GetHashCode(), back.GetHashCode());
    }

    [Theory]
    [MemberData(nameof(Samples))]
    public void FromJsonOfToJsonIsTheSameValue(ITemporalValue value)
    {
        Assert.Equal(value, Temporal.FromJson(value.ToJson()).Single());
        Assert.Equal(value, Temporal.FromJson(value.ToJson().GetRawText()).Single());
        Assert.Equal(UtvStatus.Valid, UtvValidator.ValidateStructural(value.ToJson().GetRawText()).Status);
    }

    [Fact]
    public void SecondCountsRoundTripThroughJsonAndDisplayAsDecimals()
    {
        foreach (ITemporalValue v in new ITemporalValue[] { PosixSeconds.Of(14403, 4), PosixSeconds.Of(1, 3), ElapsedSeconds.Of(86_400), PosixSeconds.Of(-1, 2) })
        {
            Assert.Equal(v, Temporal.FromJson(v.ToJson()).Single());
        }
        Assert.Equal("3600.75 s (POSIX)", PosixSeconds.Of(14403, 4).ToString());
        Assert.Equal("1/3 s (POSIX)", PosixSeconds.Of(1, 3).ToString());
        Assert.Equal("-0.5 s (POSIX)", PosixSeconds.Of(-1, 2).ToString());
        Assert.Equal("86400 s (SI)", ElapsedSeconds.Of(86_400).ToString());
        Assert.Equal(PosixSeconds.Of(1, 2), PosixSeconds.Of(2, 4));
        Assert.Equal(PosixSeconds.Of(-1, 2), PosixSeconds.Of(1, 2).Negate());
    }

    [Fact]
    public void ConstructorsRejectInvalidValues()
    {
        Assert.Equal("day", Assert.Throws<ArgumentOutOfRangeException>(() => CivilDate.Of(2026, 2, 30)).ParamName);
        Assert.Equal("month", Assert.Throws<ArgumentOutOfRangeException>(() => CivilDate.Of(2026, 13, 1)).ParamName);
        Assert.Throws<ArgumentOutOfRangeException>(() => CivilYearMonth.Of(2026, 0));
        Assert.Equal("hour", Assert.Throws<ArgumentOutOfRangeException>(() => CivilTime.Of(25, 0)).ParamName);
        Assert.Throws<ArgumentOutOfRangeException>(() => CivilTime.Of(9, 60));
        Assert.Throws<ArgumentOutOfRangeException>(() => CivilTime.Of(9, 30, 60));
        Assert.Equal("nanosecond", Assert.Throws<ArgumentOutOfRangeException>(() => CivilTime.Of(9, 30, 0, 1_000_000_000)).ParamName);
        Assert.Throws<ArgumentOutOfRangeException>(() => CivilDateTime.Of(2026, 2, 29, 9, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => CivilDateTime.Of(2026, 1, 1, 0, 0).AtOffset(18 * 3600 + 1));
        Assert.Throws<ArgumentException>(() => CivilDateTime.Of(2026, 1, 1, 0, 0).InZone("not a zone"));
        Assert.Throws<ArgumentOutOfRangeException>(() => Instant.FromUnixSeconds(0, 1_000_000_000));
        Assert.Throws<ArgumentOutOfRangeException>(() => PosixSeconds.Of(1, 0));

        Assert.Equal("day-out-of-range", Assert.IsType<DateOutcome.Invalid>(CivilDate.TryOf(2026, 2, 30)).Diagnostics.Codes.Single());
        Assert.True(CivilDate.TryOf(2024, 2, 29).TryGetUnique(out _));
        Assert.True(CivilTime.TryOf(9, 30, 60).Diagnostics.Has("leap-second"));
        Assert.True(CivilTime.TryOf(24, 0).Diagnostics.Has("field-out-of-range"));
    }

    [Fact]
    public void IntervalsAreValidByConstruction()
    {
        CivilDate nov1 = CivilDate.Of(2026, 11, 1);
        CivilDate dec1 = CivilDate.Of(2026, 12, 1);
        Assert.Contains("reversed-interval", Assert.Throws<ArgumentException>(() => Interval.HalfOpen(dec1, nov1)).Message, StringComparison.Ordinal);
        Assert.Contains("unsupported-form", Assert.Throws<ArgumentException>(() => Interval.Of<CivilDate>(Bound.Unbounded, Bound.Unknown)).Message, StringComparison.Ordinal);
        Assert.NotNull(Interval.HalfOpen(nov1, nov1)); // empty, but not reversed
        Assert.True(Interval<CivilDate>.Parse("[2026-12-01, 2026-11-01)").Diagnostics.Has("reversed-interval"));
        Assert.True(Interval<CivilDate>.Parse("(.., ..)").Diagnostics.Has("unsupported-form"));
    }

    [Fact]
    public void ParseNeverThrowsAndChecksTheType()
    {
        Assert.Equal(UtvStatus.Invalid, CivilDate.Parse("next Tuesday").Status);
        Assert.True(CivilDate.Parse("next Tuesday").Diagnostics.Has("syntax"));
        Outcome<CivilDate> instead = CivilDate.Parse("2026-11-05T13:30:00Z");
        Assert.Equal(UtvStatus.Invalid, instead.Status);
        Assert.True(instead.Diagnostics.Has("kind-mismatch"));
        Assert.True(Temporal.Parse("1347~").Diagnostics.Has("unsupported-form"));
        Assert.IsType<Instant>(Temporal.Parse("2026-11-05T14:30:00Z").Single());
        Assert.IsType<FixedOffsetDateTime>(Temporal.Parse("2026-11-05T14:30:00+00:00").Single());
        Assert.IsType<Interval<CivilDate>>(Temporal.Parse("2026-11-01/2026-11-30").Single());
        Assert.Equal("[2026-11-01, 2026-11-30]", Temporal.Parse("2026-11-01/2026-11-30").Single().ToString());
    }

    [Fact]
    public void FromJsonRejectsWrongKindsAndValuesNoTypeHolds()
    {
        Outcome<Instant> wrongKind = Instant.FromJson(CivilDate.Of(2026, 11, 5).ToJson());
        Assert.True(wrongKind.Diagnostics.Has("kind-mismatch"));

        JsonElement zonedDate = Build.InZone(Build.Date(2026, 11, 5), "Europe/Paris");
        Outcome<ITemporalValue> noType = Temporal.FromJson(zonedDate);
        Assert.Equal(UtvStatus.Unsupported, noType.Status);
        Assert.True(noType.Diagnostics.Has("unsupported-operand"));

        string withPolicy = """{"schema":"utv/1.0","kind":"civil","value":{"form":"datetime","fields":{"year":"2026","month":11,"day":5,"hour":9,"minute":0},"calendar":{"id":"gregory","chronology":"proleptic"},"resolutionPolicy":{"gap":"reject"}}}""";
        Assert.True(Temporal.FromJson(withPolicy).Diagnostics.Has("unsupported-operand"));

        Outcome<ITemporalValue> malformed = Temporal.FromJson("""{"schema":"utv/1.0","kind":"civil","value":{"form":"date"}}""");
        Assert.Equal(UtvStatus.Invalid, malformed.Status);
        Assert.True(malformed.Diagnostics.Has("malformed-value"));
        Assert.True(Temporal.FromJson("""{"a":1,"a":2}""").Diagnostics.Has("malformed-value"));

        // An explicit floating zone and an explicit start-end construction are equivalent spellings.
        string floating = """{"schema":"utv/1.0","kind":"civil","value":{"form":"date","fields":{"year":"2026","month":11,"day":5},"calendar":{"id":"gregory","chronology":"proleptic"},"zone":{"type":"floating"}}}""";
        Assert.Equal(CivilDate.Of(2026, 11, 5), CivilDate.FromJson(floating).Single());
    }

    [Fact]
    public void EqualityIsRepresentationEquality()
    {
        Assert.NotEqual(CivilTime.Of(9, 30), CivilTime.Of(9, 30, 0));
        Assert.Equal(0, CivilTime.Of(9, 30).CompareTo(CivilTime.Of(9, 30, 0)));
        FixedOffsetDateTime paris = CivilDateTime.Of(2026, 11, 5, 14, 30).AtOffset(3600);
        FixedOffsetDateTime london = CivilDateTime.Of(2026, 11, 5, 13, 30).AtOffset(0);
        Assert.NotEqual(paris, london);
        Assert.Equal(0, paris.CompareTo(london));
        Assert.Single(new HashSet<CivilDate> { CivilDate.Of(2026, 1, 1), CivilDate.Parse("2026-01-01").Single() });
    }

    [Fact]
    public void DebuggerDisplayShowsTheTypeAndText()
    {
        var attribute = (System.Diagnostics.DebuggerDisplayAttribute)Attribute.GetCustomAttribute(typeof(CivilDate), typeof(System.Diagnostics.DebuggerDisplayAttribute))!;
        Assert.Equal("CivilDate({ToString(),nq})", attribute.Value);
        foreach (Type t in new[] { typeof(CivilYearMonth), typeof(CivilTime), typeof(CivilDateTime), typeof(FixedOffsetDateTime), typeof(ZonedIntent), typeof(Instant), typeof(Period), typeof(PosixSeconds), typeof(ElapsedSeconds), typeof(Interval<>) })
        {
            Assert.NotNull(Attribute.GetCustomAttribute(t, typeof(System.Diagnostics.DebuggerDisplayAttribute)));
        }
    }

    // ------------------------------------------------------------------ §2 CivilDate

    [Fact]
    public void CivilDateTotalOperations()
    {
        CivilDate nov5 = CivilDate.Of(2026, 11, 5);
        Assert.Equal(CivilDate.Of(2026, 12, 5), nov5.AddDays(30));
        Assert.Equal(CivilDate.Of(2026, 10, 29), nov5.AddWeeks(-1));
        Assert.Equal(CivilDate.Of(2026, 2, 28), CivilDate.Of(2026, 1, 31).AddMonths(1, MonthEnd.Constrain));
        Assert.Equal(CivilDate.Of(2025, 2, 28), CivilDate.Of(2024, 2, 29).AddYears(1, MonthEnd.Constrain));
        Assert.Equal((BigInteger)2026, nov5.Year);
        Assert.Equal((11, 5), (nov5.Month, nov5.Day));
        Assert.Equal(Weekday.Thursday, nov5.DayOfWeek);
        Assert.Equal(309, nov5.DayOfYear);
        Assert.Equal(45, nov5.IsoWeek);
        Assert.Equal((BigInteger)2026, nov5.IsoWeekYear);
        Assert.Equal(53, CivilDate.Of(2026, 12, 31).IsoWeek);
        Assert.Equal((BigInteger)2026, CivilDate.Of(2027, 1, 1).IsoWeekYear);
        Assert.Equal(30, nov5.DaysInMonth);
        Assert.False(nov5.IsLeapYear);
        Assert.True(CivilDate.Of(2024, 1, 1).IsLeapYear);
        Assert.Equal(50, nov5.DaysUntil(CivilDate.Of(2026, 12, 25)));
        Assert.Equal(-50, CivilDate.Of(2026, 12, 25).DaysUntil(nov5));
        Assert.Equal(CivilDateTime.Of(2026, 11, 5, 9, 30), nov5.At(CivilTime.Of(9, 30)));
        Assert.True(nov5 < CivilDate.Of(2026, 12, 25));
        Assert.True(nov5 >= CivilDate.Of(2026, 11, 5));
        Assert.Equal(new[] { CivilDate.Of(-1, 1, 1), nov5 }, new[] { nov5, CivilDate.Of(-1, 1, 1) }.Order());
    }

    [Fact]
    public void CivilDatePartialOperations()
    {
        CivilDate jan31 = CivilDate.Of(2026, 1, 31);
        DateOutcome none = jan31.AddMonths(1);
        Assert.Equal("month-end", Assert.IsType<DateOutcome.None>(none).Diagnostics.Codes.Single());
        Assert.Equal("none (month-end)", none.ToString());
        Assert.Equal(CivilDate.Of(2026, 3, 31), jan31.AddMonths(2).Single());
        Assert.Equal(UtvStatus.None, CivilDate.Of(2024, 2, 29).AddYears(1).Status);
        Assert.Equal(CivilDate.Of(2026, 3, 3), jan31.Add(Period.Of(months: 1, days: 3), MonthEnd.Constrain).Single());
        Assert.Equal(UtvStatus.None, jan31.Add(Period.Months(1)).Status);
        Outcome<CivilDate> hours = jan31.Add(Period.Hours(1));
        Assert.Equal(UtvStatus.Invalid, hours.Status);
        Assert.True(hours.Diagnostics.Has("kind-mismatch"));
    }

    [Fact]
    public void PolicyOverloadsTakeOnlyConstrain()
    {
        // The rejecting form is the overload without a policy, which returns an Outcome (library-api.md §2).
        Assert.Throws<ArgumentException>(() => CivilDate.Of(2026, 1, 15).AddMonths(1, MonthEnd.Reject));
        Assert.Throws<ArgumentException>(() => CivilDate.Of(2026, 1, 15).AddYears(1, MonthEnd.Reject));
        Assert.Throws<ArgumentException>(() => CivilDateTime.Of(2026, 1, 15, 9, 0).AddMonths(1, MonthEnd.Reject));
    }

    // ------------------------------------------------------------------ §2 other types

    [Fact]
    public void CivilYearMonthOperations()
    {
        CivilYearMonth nov = CivilYearMonth.Of(2026, 11);
        Assert.Equal(CivilYearMonth.Of(2027, 2), nov.AddMonths(3));
        Assert.Equal(CivilYearMonth.Of(2024, 11), nov.AddYears(-2));
        Assert.True(nov < CivilYearMonth.Of(2026, 12));
        Assert.Equal("2026-11", nov.ToString());
    }

    [Fact]
    public void CivilTimeComparesButHasNoArithmetic()
    {
        Assert.True(CivilTime.Of(9, 30) < CivilTime.Of(9, 30, 1));
        Assert.True(CivilTime.Of(23, 59) > CivilTime.Of(0, 0));
        Assert.Null(CivilTime.Of(9, 30).Second);
        Assert.Equal(250_000_000, CivilTime.Of(9, 30, 15, 250_000_000).Nanosecond);
        Assert.DoesNotContain(typeof(CivilTime).GetMethods(), m => m.Name.StartsWith("Add", StringComparison.Ordinal));
    }

    [Fact]
    public void CivilDateTimeOperations()
    {
        CivilDateTime late = CivilDateTime.Of(2026, 11, 5, 23, 30);
        Assert.Equal(CivilDateTime.Of(2026, 11, 6, 1, 30), late.AddHours(2));
        Assert.Equal(CivilDateTime.Of(2026, 11, 5, 23, 0), late.AddMinutes(-30));
        Assert.Equal(CivilDateTime.Of(2026, 11, 5, 23, 30, 1), late.AddSeconds(1));
        Assert.Equal(CivilDateTime.Of(2026, 11, 12, 23, 30), late.AddWeeks(1));
        Assert.Equal(CivilDateTime.Of(2026, 11, 4, 23, 30), late.AddDays(-1));
        Assert.Equal(CivilDateTime.Of(2026, 2, 28, 9, 0), CivilDateTime.Of(2026, 1, 31, 9, 0).AddMonths(1, MonthEnd.Constrain));
        Assert.Equal(UtvStatus.None, CivilDateTime.Of(2026, 1, 31, 9, 0).AddMonths(1).Status);
        Assert.Equal(CivilDateTime.Of(2026, 11, 6, 1, 30), late.Add(Period.Of(hours: 2)).Single());
        Assert.Equal(CivilDate.Of(2026, 11, 5), late.Date);
        Assert.Equal(CivilTime.Of(23, 30), late.Time);
        Assert.Equal("2026-11-05T23:30[Europe/Paris]", late.InZone("Europe/Paris").ToString());
        Assert.Equal("2026-11-05T23:30+05:30", late.AtOffset(19_800).ToString());
        Assert.True(late > CivilDateTime.Of(2026, 11, 5, 23, 29, 59));
    }

    [Fact]
    public void FixedOffsetDateTimeOperations()
    {
        FixedOffsetDateTime t = CivilDateTime.Of(2026, 11, 5, 14, 30).AtOffset(3600);
        Assert.Equal(Instant.FromUnixSeconds(1_793_885_400), t.ToInstant());
        Assert.Equal(3600, t.OffsetSeconds);
        Assert.Equal(CivilDateTime.Of(2026, 11, 5, 14, 30), t.DateTime);
        Assert.True(t < CivilDateTime.Of(2026, 11, 5, 14, 30).AtOffset(0));
    }

    [Fact]
    public void ZonedIntentOperations()
    {
        ZonedIntent asserted = ZonedIntent.Parse("2026-03-28T09:00+01:00[Europe/Paris]").Single();
        Assert.Equal(3600, asserted.AssertedOffsetSeconds);
        ZonedIntent next = asserted.AddDays(1);
        Assert.Equal("2026-03-29T09:00[Europe/Paris]", next.ToString()); // the assertion is dropped
        Assert.Null(next.AssertedOffsetSeconds);
        Assert.Equal("Europe/Paris", next.ZoneId);
        Assert.Equal("2026-03-29T09:00[Europe/Paris]", asserted.AddWeeks(0).AddHours(24).ToString());
        Assert.Equal("2026-03-28T09:01[Europe/Paris]", asserted.AddMinutes(1).ToString());
        Assert.Equal("2026-03-28T09:00:30[Europe/Paris]", asserted.AddSeconds(30).ToString());
        Assert.Equal(UtvStatus.None, CivilDateTime.Of(2026, 1, 31, 9, 0).InZone("Europe/Paris").AddMonths(1).Status);
        Assert.Equal(Instant.FromUnixSeconds(1_774_767_600), next.Resolve(Ctx).Single()); // 07:00Z
        Assert.DoesNotContain(typeof(ZonedIntent).GetMethods(), m => m.Name is "op_LessThan" or "CompareTo");
    }

    [Fact]
    public void ResolveGapsAndOverlaps()
    {
        ZonedIntent gap = CivilDateTime.Of(2026, 3, 8, 2, 30).InZone("America/New_York");
        InstantOutcome none = gap.Resolve(Ctx);
        Assert.IsType<InstantOutcome.None>(none);
        Assert.True(none.Diagnostics.Has("gap"));
        Assert.Equal("2026-03-08T07:30:00Z", gap.Resolve(Ctx, Gap.ShiftForward).Single().ToString());
        Assert.Equal(UtvStatus.None, gap.Resolve(Ctx, ResolutionPolicy.AllCandidates).Status);

        ZonedIntent overlap = CivilDateTime.Of(2026, 11, 1, 1, 30).InZone("America/New_York");
        var both = Assert.IsType<InstantOutcome.Multiple>(overlap.Resolve(Ctx));
        Assert.Equal(new[] { "2026-11-01T05:30:00Z", "2026-11-01T06:30:00Z" }, both.Values.Select(i => i.ToString()));
        Assert.True(both.Diagnostics.Has("ambiguous"));
        Assert.Empty(overlap.Resolve(Ctx, ResolutionPolicy.AllCandidates).Diagnostics);
        Assert.Equal("2026-11-01T06:30:00Z", overlap.Resolve(Ctx, Overlap.Latest).Single().ToString());
        Assert.Equal("2026-11-01T05:30:00Z", overlap.Resolve(Ctx, new ResolutionPolicy(Gap.Reject, Overlap.Earliest)).Single().ToString());
        Assert.Equal(overlap.Resolve(Ctx), Ctx.Resolve(overlap));

        Assert.True(CivilDateTime.Of(2026, 6, 1, 9, 0).InZone("Asia/Tokyo").Resolve(Ctx).Diagnostics.Has("unknown-zone"));
        Assert.True(CivilDateTime.Of(2030, 6, 1, 9, 0).InZone("Europe/Paris").Resolve(Ctx).Diagnostics.Has("outside-rule-window"));
        Assert.True(ZonedIntent.Parse("2026-07-04T12:00-05:00[America/New_York]").Single().Resolve(Ctx).Diagnostics.Has("offset-mismatch"));
    }

    [Fact]
    public void ContextComparesZonedValues()
    {
        ZonedIntent call = CivilDateTime.Of(2026, 11, 1, 1, 30).InZone("America/New_York");
        ZonedIntent standup = CivilDateTime.Of(2026, 11, 1, 1, 45).InZone("America/New_York");
        var order = Assert.IsType<Outcome<Ordering>.Multiple>(Ctx.Compare(call, standup));
        Assert.Equal(new[] { Ordering.Before, Ordering.After }, order.Values);
        Assert.True(order.Diagnostics.Has("ambiguous"));

        ZonedIntent paris = CivilDateTime.Of(2027, 6, 1, 9, 0).InZone("Europe/Paris");
        ZonedIntent newYork = CivilDateTime.Of(2027, 6, 1, 9, 0).InZone("America/New_York");
        Assert.Equal(Ordering.Before, Ctx.Compare(paris, newYork).Single());
        Assert.Equal(Ordering.Equal, Ctx.Compare(paris, Instant.Parse("2027-06-01T07:00:00Z").Single()).Single());
        Assert.Equal(Ordering.After, Ctx.Compare(Instant.Parse("2027-06-01T08:00:00Z").Single(), paris).Single());
        Assert.True(Ctx.Compare(CivilDateTime.Of(2026, 3, 8, 2, 30).InZone("America/New_York"), paris).Diagnostics.Has("gap"));
    }

    [Fact]
    public void InstantOperations()
    {
        Instant a = Instant.FromUnixSeconds(1_793_885_400, 250_000_000);
        Instant b = Instant.FromUnixSeconds(1_793_889_001);
        Assert.Equal(PosixSeconds.Of(14403, 4), a.Until(b));
        Assert.Equal(b, a.Add(PosixSeconds.Of(14403, 4)));
        Assert.Equal(b, a + PosixSeconds.Of(14403, 4));
        Assert.Equal((BigInteger)1_793_885_400, a.UnixSeconds);
        Assert.Equal(250_000_000, a.Nanoseconds);
        Assert.Equal(PosixSeconds.Of(1, 4), a.SubSecond);
        Assert.Equal(a, Instant.FromUnixNanoseconds(1_793_885_400_250_000_000));
        Assert.Equal(Instant.FromUnixSeconds(-1, 500_000_000), Instant.FromUnixNanoseconds(-500_000_000));

        Instant third = Instant.FromUnixSeconds(0).Add(PosixSeconds.Of(1, 3));
        Assert.Equal(333_333_333, third.Nanoseconds); // floor
        Assert.Equal(PosixSeconds.Of(1, 3), third.SubSecond); // exact

        Assert.Equal("2026-11-05T14:30:00.25+01:00", a.AtOffset(3600).ToString());
        Assert.Equal("2026-11-05T08:30:00.25-05:00[America/New_York]", a.InZone("America/New_York", Ctx).Single().ToString());
        Assert.True(a.InZone("Asia/Tokyo", Ctx).Diagnostics.Has("unknown-zone"));
        Assert.True(Instant.FromUnixSeconds(0).InZone("Europe/Paris", Ctx).Diagnostics.Has("outside-rule-window"));
        Outcome<Instant> si = a.Add(ElapsedSeconds.Of(1));
        Assert.Equal(UtvStatus.Unsupported, si.Status);
        Assert.True(si.Diagnostics.Has("needs-leap-seconds"));
        Assert.True(a < b);
    }

    [Fact]
    public void AmountsHaveComponentsNegationAndEquality()
    {
        Period p = Period.Of(years: 1, days: -3);
        Assert.Equal(new PeriodComponents(1, 0, 0, -3, 0, 0, 0), p.Components);
        Assert.Equal(Period.Of(years: -1, days: 3), p.Negate());
        Assert.Equal(Period.Days(30), Period.Of(days: 30));
        Assert.Equal("P1M", Period.Months(1).ToString());
        Assert.Equal("PT1M", Period.Minutes(1).ToString());
        Assert.Equal("-P1W", Period.Weeks(-1).ToString());
        Assert.Equal("P0D", Period.Zero.ToString());
        Assert.StartsWith("{", p.ToString(), StringComparison.Ordinal); // mixed signs: no text form, so JSON
        Assert.Equal((BigInteger)14403, PosixSeconds.Of(14403, 4).Numerator);
        Assert.Equal((BigInteger)4, PosixSeconds.Of(14403, 4).Denominator);
        Assert.Equal(ElapsedSeconds.Of(-5), ElapsedSeconds.Of(5).Negate());
    }

    [Fact]
    public void IntervalOperations()
    {
        Interval<CivilDate> november = Interval.HalfOpen(CivilDate.Of(2026, 11, 1), CivilDate.Of(2026, 12, 1));
        Assert.Equal(Truth.Definitely, november.Contains(CivilDate.Of(2026, 11, 15)));
        Assert.Equal(Truth.Impossible, november.Contains(CivilDate.Of(2026, 12, 1)));
        Interval<CivilDate> openEnd = Interval.Of(Bound.Included(CivilDate.Of(2026, 11, 1)), Bound.Unknown);
        Assert.Equal(Truth.Possibly, openEnd.Contains(CivilDate.Of(2027, 6, 1)));
        Assert.Equal(Truth.Impossible, Interval.Closed(CivilDate.Of(2026, 11, 1), CivilDate.Of(2026, 11, 30))
            .Overlaps(Interval.HalfOpen(CivilDate.Of(2026, 12, 1), CivilDate.Of(2027, 1, 1))));
        Assert.Equal(Truth.Possibly, openEnd.Overlaps(Interval.HalfOpen(CivilDate.Of(2027, 1, 1), CivilDate.Of(2027, 2, 1))));
        Assert.Equal(BoundState.Unknown, openEnd.End.State);
        Assert.Null(openEnd.End.Point);
        Assert.Equal(CivilDate.Of(2026, 11, 1), openEnd.Start.Point);

        Interval<Instant> hour = Interval.HalfOpen(Instant.FromUnixSeconds(0), Instant.FromUnixSeconds(3600));
        Assert.Equal(Truth.Definitely, hour.Contains(Instant.FromUnixSeconds(0)));
        Assert.Equal(Truth.Impossible, hour.Contains(Instant.FromUnixSeconds(3600)));
        Assert.Equal("[1970-01-01T00:00:00Z, 1970-01-01T01:00:00Z)", hour.ToString());
    }

    // ------------------------------------------------------------------ §3 outcomes

    [Fact]
    public void OutcomeHelpersNeverPickSilently()
    {
        DateOutcome none = CivilDate.Of(2026, 1, 31).AddMonths(1);
        DateOutcome unique = CivilDate.Of(2026, 1, 15).AddMonths(1);
        Assert.False(none.TryGetUnique(out _));
        Assert.True(unique.TryGetUnique(out CivilDate? d));
        Assert.Equal(CivilDate.Of(2026, 2, 15), d);
        CivilDate fallback = CivilDate.Of(2000, 1, 1);
        Assert.Equal(fallback, none.UniqueOr(fallback));
        Assert.Equal(CivilDate.Of(2026, 2, 15), unique.UniqueOr(fallback));
        OutcomeException e = Assert.Throws<OutcomeException>(() => none.Single());
        Assert.Equal(UtvStatus.None, e.Status);
        Assert.True(e.Diagnostics.Has("month-end"));
        Assert.Equal("2026-02-15", unique.ToString());
        Assert.Equal(UtvStatus.Unique, unique.Status);

        InstantOutcome multiple = CivilDateTime.Of(2026, 11, 1, 1, 30).InZone("America/New_York").Resolve(Ctx);
        Assert.Throws<OutcomeException>(() => multiple.Single());
        Assert.Equal("multiple (ambiguous)", multiple.ToString());

        string text = multiple switch
        {
            InstantOutcome.Unique(var one) => one.ToString(),
            InstantOutcome.Multiple(var all) => string.Join(" or ", all),
            InstantOutcome.None(var why) => why.ToString(),
            _ => "?",
        };
        Assert.Equal("2026-11-01T05:30:00Z or 2026-11-01T06:30:00Z", text);
    }

    [Fact]
    public void OutcomesBridgeToTheJsonLayer()
    {
        UtvResult none = CivilDate.Of(2026, 1, 31).AddMonths(1).ToResult();
        Assert.Equal("add", none.Operation);
        Assert.Equal(UtvStatus.None, none.Status);
        Assert.Equal("month-end", none.Diagnostics.Single().Code);
        UtvResult unique = CivilDate.Of(2026, 1, 15).AddMonths(1).ToResult();
        Assert.Equal("2026-02-15", Iso.FormatIso(unique.Value).GetString());
        UtvResult multiple = CivilDateTime.Of(2026, 11, 1, 1, 30).InZone("America/New_York").Resolve(Ctx).ToResult();
        Assert.Equal(2, multiple.Candidates.Count);
        Assert.Equal("before", new Outcome<Ordering>.Unique(Ordering.Before).ToResult().GetString());
        Assert.Throws<NotSupportedException>(() => CivilDate.Of(2026, 1, 1).ToDateOnly().ToResult());
    }

    [Fact]
    public void OutcomeEqualityComparesCasesValuesAndDiagnostics()
    {
        Assert.Equal(CivilDate.Of(2026, 1, 31).AddMonths(1), CivilDate.Of(2026, 1, 31).AddMonths(1));
        Assert.Equal(CivilDate.Parse("2026-01-01"), CivilDate.TryOf(2026, 1, 1));
        Assert.NotEqual(CivilDate.TryOf(2026, 1, 1), CivilDate.TryOf(2026, 1, 2));
        InstantOutcome m = CivilDateTime.Of(2026, 11, 1, 1, 30).InZone("America/New_York").Resolve(Ctx);
        Assert.Equal(m, CivilDateTime.Of(2026, 11, 1, 1, 30).InZone("America/New_York").Resolve(Ctx));
    }

    // ------------------------------------------------------------------ interop

    [Fact]
    public void NativeInterop()
    {
        var lunch = new DateTimeOffset(2026, 11, 5, 14, 30, 0, TimeSpan.FromHours(1));
        FixedOffsetDateTime f = FixedOffsetDateTime.FromDateTimeOffset(lunch);
        Assert.Equal("2026-11-05T14:30:00+01:00", f.ToString());
        Assert.True(f.ToDateTimeOffset().Single().EqualsExact(lunch));
        Assert.Equal("2026-11-05T13:30:00Z", Instant.FromDateTimeOffset(lunch).ToString());
        Assert.Equal(lunch, Instant.FromDateTimeOffset(lunch).ToDateTimeOffset().Single());

        Assert.True(CivilDateTime.FromDateTime(new DateTime(2026, 1, 1, 9, 0, 0, DateTimeKind.Local)).Diagnostics.Has("local-zone"));
        Assert.True(CivilDateTime.FromDateTime(new DateTime(2026, 1, 1, 9, 0, 0, DateTimeKind.Utc)).Diagnostics.Has("intent-required"));
        Assert.Equal("2026-01-01T09:00:00", CivilDateTime.FromDateTime(new DateTime(2026, 1, 1, 9, 0, 0)).Single().ToString());
        Assert.True(Instant.FromDateTime(new DateTime(2026, 1, 1, 9, 0, 0, DateTimeKind.Local)).Diagnostics.Has("local-zone"));
        Assert.True(Instant.FromDateTime(new DateTime(2026, 1, 1, 9, 0, 0)).Diagnostics.Has("intent-required"));
        Assert.Equal("2026-01-01T09:00:00Z", Instant.FromDateTime(new DateTime(2026, 1, 1, 9, 0, 0, DateTimeKind.Utc)).Single().ToString());

        Assert.Equal(new DateOnly(2026, 11, 5), CivilDate.FromDateOnly(new DateOnly(2026, 11, 5)).ToDateOnly().Single());
        Assert.True(CivilDate.Of(10_000, 1, 1).ToDateOnly().Diagnostics.Has("out-of-native-range"));
        Assert.Equal(new TimeOnly(9, 30), CivilTime.FromTimeOnly(new TimeOnly(9, 30)).ToTimeOnly().Single());
        Assert.Equal(new DateTime(2026, 11, 5, 9, 30, 0), CivilDateTime.Of(2026, 11, 5, 9, 30).ToDateTime().Single());
        Assert.Equal(PosixSeconds.Of(3, 2), PosixSeconds.FromTimeSpan(TimeSpan.FromMilliseconds(1500)));
        Assert.Equal(TimeSpan.FromMilliseconds(1500), PosixSeconds.Of(3, 2).ToTimeSpan().Single());
        Assert.True(PosixSeconds.Of(1, 3).ToTimeSpan().Diagnostics.Has("out-of-native-range"));
    }

    [Fact]
    public void ContextHasNoDefaultAndLoadsRulesFromAPath()
    {
        Assert.Throws<ArgumentNullException>(() => TemporalContext.With(null!));
        ZoneRules rules = ZoneRules.Load(Fixtures.PathOf("conformance", "rules", "demo-zones.json"));
        Assert.Equal("demo-zones", TemporalContext.With(rules).Rules.RuleSetId);
        Assert.Empty(typeof(TemporalContext).GetProperties(System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public));
    }
}
