using System;
using System.Text.Json;
using Xunit;

namespace Utv.Tests;

public class InteropTests
{
    [Fact]
    public void DateOnlyRoundTrips()
    {
        JsonElement date = Interop.FromDateOnly(new DateOnly(2026, 11, 5));
        Assert.Equal("2026-11-05", Iso.FormatIso(date).GetString());
        NativeResult<DateOnly> back = Interop.ToDateOnly(date);
        Assert.True(back.IsUnique);
        Assert.False(back.IsLossy);
        Assert.Equal(new DateOnly(2026, 11, 5), back.Value);
    }

    [Fact]
    public void TimeOnlyRoundTripsWithSubSecondTicks()
    {
        var time = new TimeOnly(9, 30, 15).Add(TimeSpan.FromTicks(2_500_000)); // .25 s
        JsonElement utv = Interop.FromTimeOnly(time);
        Assert.Equal("09:30:15.25", Iso.FormatIso(utv).GetString());
        Assert.Equal(time, Interop.ToTimeOnly(utv).Value);
    }

    [Fact]
    public void UnspecifiedDateTimeIsFloating()
    {
        var dt = new DateTime(2026, 11, 5, 14, 30, 0, DateTimeKind.Unspecified);
        UtvResult r = Interop.FromDateTime(dt);
        Assert.Equal(UtvStatus.Unique, r.Status);
        Assert.Equal("2026-11-05T14:30:00", Iso.FormatIso(r.Value).GetString());
        Assert.Equal(UtvStatus.Symbolic, Zones.Resolve(r.Value, DemoRules.Value).Status);
        NativeResult<DateTime> back = Interop.ToDateTime(r.Value);
        Assert.Equal(dt, back.Value);
        Assert.Equal(DateTimeKind.Unspecified, back.Value.Kind);
    }

    [Fact]
    public void UtcDateTimeIsAnInstant()
    {
        var dt = new DateTime(2026, 11, 5, 13, 30, 0, DateTimeKind.Utc).AddTicks(5);
        UtvResult r = Interop.FromDateTime(dt);
        Assert.Equal("instant", r.Value.GetProperty("kind").GetString());
        Assert.Equal("1793885400", r.Value.GetProperty("value").GetProperty("ticks").GetString());
        Assert.Equal("2026-11-05T13:30:00.0000005Z", Iso.FormatIso(r.Value).GetString());
        NativeResult<DateTime> back = Interop.ToDateTime(r.Value);
        Assert.Equal(dt, back.Value);
        Assert.Equal(DateTimeKind.Utc, back.Value.Kind);
    }

    [Fact]
    public void InstantsBeforeTheEpochFloor()
    {
        var dt = new DateTime(1969, 12, 31, 23, 59, 59, DateTimeKind.Utc).AddTicks(5_000_000);
        JsonElement v = Interop.FromDateTime(dt).Value;
        Assert.Equal("-1", v.GetProperty("value").GetProperty("ticks").GetString());
        Assert.Equal("1969-12-31T23:59:59.5Z", Iso.FormatIso(v).GetString());
        Assert.Equal(dt, Interop.ToDateTime(v).Value);
    }

    [Fact]
    public void LocalDateTimeIsRefused()
    {
        UtvResult r = Interop.FromDateTime(new DateTime(2026, 11, 5, 14, 30, 0, DateTimeKind.Local));
        Assert.Equal(UtvStatus.Unsupported, r.Status);
        Assert.Empty(r.Candidates);
        Assert.Equal("local-zone", Assert.Single(r.Diagnostics).Code);
        Assert.Throws<InvalidOperationException>(() => r.Value);
    }

    [Fact]
    public void DateTimeOffsetIntentIsExplicit()
    {
        var dto = new DateTimeOffset(2026, 11, 5, 14, 30, 0, TimeSpan.FromHours(1));
        JsonElement instant = Interop.FromDateTimeOffset(dto, DateTimeOffsetIntent.Instant);
        JsonElement offset = Interop.FromDateTimeOffset(dto, DateTimeOffsetIntent.OffsetDateTime);
        Assert.Equal("2026-11-05T13:30:00Z", Iso.FormatIso(instant).GetString());
        Assert.Equal("2026-11-05T14:30:00+01:00", Iso.FormatIso(offset).GetString());
        Assert.Equal("equal", Calendar.Compare(offset, Iso.ParseIso("2026-11-05T13:30:00+00:00").Value).GetString());

        NativeResult<DateTimeOffset> fromOffset = Interop.ToDateTimeOffset(offset);
        Assert.Equal(dto, fromOffset.Value);
        Assert.Equal(TimeSpan.FromHours(1), fromOffset.Value.Offset);
        Assert.False(fromOffset.IsLossy);

        NativeResult<DateTimeOffset> fromInstant = Interop.ToDateTimeOffset(instant);
        Assert.Equal(dto.UtcDateTime, fromInstant.Value.UtcDateTime);
        Assert.Equal(TimeSpan.Zero, fromInstant.Value.Offset);
        Assert.Contains(fromInstant.Diagnostics, d => d.Code == "offset-not-known");

        Assert.Throws<ArgumentOutOfRangeException>(() => Interop.FromDateTimeOffset(dto, (DateTimeOffsetIntent)7));
    }

    [Fact]
    public void ZonedValueToDateTimeOffsetReportsLoss()
    {
        JsonElement zoned = Iso.ParseIso("2027-06-01T09:00+02:00[Europe/Paris]").Value;
        NativeResult<DateTimeOffset> r = Interop.ToDateTimeOffset(zoned);
        Assert.True(r.IsUnique);
        Assert.True(r.IsLossy);
        Assert.Equal(new DateTimeOffset(2027, 6, 1, 9, 0, 0, TimeSpan.FromHours(2)), r.Value);
        Assert.Contains(r.Diagnostics, d => d.Code == "zone-dropped");

        NativeResult<DateTimeOffset> noAssertion = Interop.ToDateTimeOffset(Iso.ParseIso("2027-06-01T09:00[Europe/Paris]").Value);
        Assert.Equal(UtvStatus.Unsupported, noAssertion.Status);
        Assert.False(noAssertion.TryGetValue(out _));
        Assert.Equal(UtvStatus.Unsupported, Interop.ToDateTimeOffset(Build.DateTime(2026, 11, 5, 9, 0)).Status);
        Assert.Equal(UtvStatus.Unsupported, Interop.ToDateTime(zoned).Status);
    }

    [Theory]
    [InlineData("+10000-01-01")]
    [InlineData("0000-12-31")]
    [InlineData("-0500-03-01")]
    public void YearsOutsideDotNetAreUnsupported(string text)
    {
        NativeResult<DateOnly> r = Interop.ToDateOnly(Iso.ParseIso(text).Value);
        Assert.Equal(UtvStatus.Unsupported, r.Status);
        Assert.Equal("out-of-native-range", Assert.Single(r.Diagnostics).Code);
        Assert.Throws<InvalidOperationException>(() => r.Value);
    }

    [Fact]
    public void SubTickPrecisionIsUnsupported()
    {
        Assert.Equal(UtvStatus.Unsupported, Interop.ToTimeOnly(Build.Time(9, 30, 0, 50)).Status);
        Assert.Equal(UtvStatus.Unsupported, Interop.ToDateTime(Build.DateTime(2026, 11, 5, 9, 30, 0, 123_456_789)).Status);
        Assert.Equal(UtvStatus.Unsupported, Interop.ToDateTime(Build.Instant(0, 1)).Status);
        Assert.Equal(new TimeOnly(9, 30).Add(TimeSpan.FromTicks(1)), Interop.ToTimeOnly(Build.Time(9, 30, 0, 100)).Value);
    }

    [Fact]
    public void InstantsOutsideDotNetAreUnsupported()
    {
        Assert.Equal(UtvStatus.Unsupported, Interop.ToDateTime(Build.Instant(253402300800)).Status); // 10000-01-01
        Assert.Equal(UtvStatus.Unique, Interop.ToDateTime(Build.Instant(253402300799)).Status);
        Assert.Equal(UtvStatus.Unsupported, Interop.ToDateTime(Build.Instant(-62135596801)).Status);
    }

    [Fact]
    public void InvalidValuesAreInvalid()
    {
        Assert.Equal(UtvStatus.Invalid, Interop.ToDateOnly(Build.Date(2026, 2, 30)).Status);
        Assert.Equal(UtvStatus.Unsupported, Interop.ToDateOnly(Build.Time(9, 0)).Status);
    }

    [Fact]
    public void TimeSpanIsPosixSeconds()
    {
        JsonElement amount = Interop.PosixSecondsFromTimeSpan(TimeSpan.FromMilliseconds(1500));
        JsonElement seconds = amount.GetProperty("value").GetProperty("seconds");
        Assert.Equal("3", seconds.GetProperty("numerator").GetString());
        Assert.Equal("2", seconds.GetProperty("denominator").GetString());
        Assert.Equal(TimeSpan.FromMilliseconds(1500), Interop.ToTimeSpan(amount).Value);
        Assert.Equal(UtvStatus.Unsupported, Interop.ToTimeSpan(Build.Period(days: 1)).Status);
        Assert.Equal(UtvStatus.Unsupported, Interop.ToTimeSpan(Build.PosixSeconds(1, 3)).Status);
        Assert.Equal(TimeSpan.FromDays(1), Interop.ToTimeSpan(Build.PosixSeconds(86400)).Value);
    }
}
