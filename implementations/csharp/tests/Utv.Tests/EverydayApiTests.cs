using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using Xunit;

namespace Utv.Tests;

internal static class DemoRules
{
    public static readonly Lazy<ZoneRules> Lazy = new(() => ZoneRules.Parse(File.ReadAllText(Fixtures.PathOf("conformance", "rules", "demo-zones.json"))));

    public static ZoneRules Value => Lazy.Value;
}

public class EverydayApiTests
{
    [Fact]
    public void BuildersProduceStructurallyValidValues()
    {
        JsonElement date = Build.Date(2026, 11, 5);
        JsonElement[] values =
        {
            date,
            Build.Date(2026, 2, 30), // well-formed, but no such day
            Build.YearMonth(2026, 11),
            Build.Time(9, 30),
            Build.Time(9, 30, 15, 250_000_000),
            Build.DateTime(2026, 11, 5, 23, 30),
            Build.InZone(Build.DateTime(2027, 6, 1, 9, 0), "Europe/Paris"),
            Build.WithOffset(Build.DateTime(2026, 11, 5, 14, 30, 0), 3600),
            Build.Instant(1793885400, 500_000_000),
            Build.Period(years: 1, days: -3),
            Build.PosixSeconds(86400),
            Build.PosixSeconds(-3, 6),
            Build.Interval(date, Build.Date(2026, 12, 1)),
            Build.Interval(IntervalBound.Included(date), IntervalBound.Unknown),
            Build.Interval(IntervalBound.Unbounded, IntervalBound.Excluded(Build.Instant(0))),
        };
        foreach (JsonElement v in values)
        {
            UtvResult r = UtvValidator.ValidateStructural(v.GetRawText());
            Assert.True(r.Status == UtvStatus.Valid, v.GetRawText() + ": " + Fixtures.Describe(r));
        }
        Assert.Equal("""{"numerator":"-1","denominator":"2"}""", values[11].GetProperty("value").GetProperty("seconds").GetRawText());
        Assert.Equal("""{"numerator":"1","denominator":"2"}""", values[8].GetProperty("value").GetProperty("subTick").GetRawText());
        Assert.Equal("""{"years":"1","days":"-3"}""", values[9].GetProperty("value").GetProperty("components").GetRawText());
        Assert.False(values[3].GetProperty("value").GetProperty("fields").TryGetProperty("second", out _));
        Assert.Equal(UtvStatus.Invalid, Calendar.Fields(values[1]).Status);
    }

    [Fact]
    public void BuilderMisuseThrows()
    {
        Assert.Throws<ArgumentException>(() => Build.InZone(Build.Instant(0), "Europe/Paris"));
        Assert.Throws<ArgumentException>(() => Build.InZone(Build.DateTime(2026, 1, 1, 0, 0), ""));
        Assert.Throws<ArgumentException>(() => Build.Interval(Build.Date(2026, 1, 1), Build.Instant(0)));
        Assert.Throws<ArgumentException>(() => Build.Interval(IntervalBound.Unbounded, IntervalBound.Unknown));
        Assert.Throws<ArgumentException>(() => Build.Interval(Build.Period(days: 1), Build.Period(days: 2)));
    }

    [Fact]
    public void InZoneReplacesAnOffsetAndItsAssertion()
    {
        JsonElement zoned = Build.InZone(Iso.ParseIso("2027-06-01T09:00+02:00[Europe/Paris]").Value, "America/New_York");
        Assert.Equal("2027-06-01T09:00[America/New_York]", Iso.FormatIso(zoned).GetString());
        Assert.Equal("2027-06-01T09:00+05:30", Iso.FormatIso(Build.WithOffset(zoned, 19800)).GetString());
    }

    [Fact]
    public void ResultAccessors()
    {
        UtvResult days = Calendar.DaysBetween(Build.Date(2026, 11, 5), Build.Date(2026, 12, 25));
        Assert.True(days.IsUnique);
        Assert.Equal(50, days.GetInt64());
        Assert.Throws<InvalidOperationException>(() => days.GetString());
        Assert.True(days.TryGetValue(out JsonElement v));
        Assert.Equal(JsonValueKind.Number, v.ValueKind);

        UtvResult none = Calendar.Add(Build.Date(2026, 1, 31), Build.Period(months: 1));
        Assert.Equal(UtvStatus.None, none.Status);
        Assert.False(none.TryGetValue(out _));
        Assert.Throws<InvalidOperationException>(() => none.Value);
        Assert.Equal("month-end", Assert.Single(none.Diagnostics).Code);

        using JsonDocument json = JsonDocument.Parse(Iso.FormatIso(Build.Date(2026, 11, 5)).ToJson());
        Assert.Equal("formatIso", json.RootElement.GetProperty("operation").GetString());
        Assert.Equal("2026-11-05", json.RootElement.GetProperty("candidates")[0].GetString());
    }

    [Theory]
    [InlineData("2026-11-05 14:30", "invalid")]
    [InlineData("2026-11-05T14:30Z", "unique")]
    [InlineData("2026-11-05T14:30:00,5", "unsupported")]
    [InlineData("2026-11-05T14:30:00.1234567891", "unsupported")]
    [InlineData("2026-11-05T14:30+0100", "unsupported")]
    [InlineData("2026-11-05T14:30+01:60", "invalid")]
    [InlineData("2026-11-05T14:30Z[Europe/Paris]", "unsupported")]
    [InlineData("2026-11-05T14:30-00:00[Europe/Paris]", "unsupported")]
    [InlineData("2026-11-05T14:30[Europe/Paris][u-ca=gregory]", "unsupported")]
    [InlineData("2026-11-05T14:30[Europe/Paris][America/New_York]", "unsupported")]
    [InlineData("2026-11-05T14:30[]", "unsupported")]
    [InlineData("2026-11-05T14:30[+01:00]", "unsupported")]
    [InlineData("2026-11-05T24:30", "invalid")]
    [InlineData("2026-11-05T25:00", "invalid")]
    [InlineData("24:00", "unsupported")]
    [InlineData("23:59:61", "invalid")]
    [InlineData("2026-W54-1", "invalid")]
    [InlineData("2026-366", "invalid")]
    [InlineData("2024-366", "unsupported")]
    [InlineData("20260230", "invalid")]
    [InlineData("2026", "unsupported")]
    [InlineData("T09:30", "unsupported")]
    [InlineData("09:30+01:00", "unsupported")]
    [InlineData("२०२६-११-०५", "invalid")] // non-ASCII digits
    [InlineData("2026-11-05\n", "invalid")]
    [InlineData("", "invalid")]
    [InlineData("-0000-01-01", "unique")]
    [InlineData("+2026-11-05", "unique")]
    public void ParseEdgeCases(string text, string status)
    {
        Assert.Equal(status, Iso.ParseIso(text).Status.ToWireString());
    }

    [Theory]
    [InlineData("2026-11-05")]
    [InlineData("2026-11")]
    [InlineData("09:30")]
    [InlineData("09:30:00")]
    [InlineData("09:30:15.000000001")]
    [InlineData("2026-11-05T14:30:05.12")]
    [InlineData("2026-11-05T14:30-05:00")]
    [InlineData("2026-11-05T14:30+00:00")]
    [InlineData("2027-06-01T09:00+02:00[Europe/Paris]")]
    [InlineData("2027-06-01T09:00[Etc/GMT+5]")]
    [InlineData("1970-01-01T00:00:00.000000001Z")]
    [InlineData("+275760-09-13")]
    [InlineData("-0500-03-01")]
    [InlineData("0000-01-01")]
    public void FormatInvertsParse(string text)
    {
        Assert.Equal(text, Iso.FormatIso(Iso.ParseIso(text).Value).GetString());
    }

    [Fact]
    public void FormatRejectsWhatHasNoText()
    {
        Assert.Equal(UtvStatus.Invalid, Iso.FormatIso(Build.Date(2026, 2, 30)).Status);
        Assert.Equal(UtvStatus.Invalid, Iso.FormatIso(Build.Time(25, 0)).Status);
        Assert.Equal("1970-01-01T00:00:00Z", Iso.FormatIso(Build.Instant(0)).GetString());
        Assert.Equal(UtvStatus.Unsupported, Iso.FormatIso(Build.WithOffset(Build.DateTime(2026, 1, 1, 0, 0), 3601)).Status);
        Assert.Equal(UtvStatus.Unsupported, Iso.FormatIso(Build.Interval(Build.Date(2026, 1, 1), Build.Date(2026, 2, 1))).Status);
        using JsonDocument third = JsonDocument.Parse("""{"schema":"utv/1.0","kind":"instant","value":{"system":{"system":"utv.timescale","id":"unix-posix","version":"1"},"ticks":"0","subTick":{"numerator":"1","denominator":"3"}}}""");
        Assert.Equal(UtvStatus.Unsupported, Iso.FormatIso(third.RootElement).Status);
        using JsonDocument broken = JsonDocument.Parse("""{"schema":"utv/1.0","kind":"civil","value":{"form":"date"}}""");
        Assert.Equal(UtvStatus.Invalid, Iso.FormatIso(broken.RootElement).Status);
        using JsonDocument future = JsonDocument.Parse("""{"schema":"utv/2.0","kind":"civil","value":{}}""");
        Assert.Equal(UtvStatus.Unsupported, Iso.FormatIso(future.RootElement).Status);
    }

    [Fact]
    public void AddCarriesZoneAndDropsOffsetAssertion()
    {
        JsonElement start = Iso.ParseIso("2026-03-28T09:00+01:00[Europe/Paris]").Value;
        UtvResult r = Calendar.Add(start, Build.Period(days: 1));
        Assert.Equal("2026-03-29T09:00[Europe/Paris]", Iso.FormatIso(r.Value).GetString());
        Assert.Equal("offset-assertion-dropped", Assert.Single(r.Diagnostics).Code);
        Assert.Equal("2026-03-29T07:00:00Z", Iso.FormatIso(Zones.Resolve(r.Value, DemoRules.Value).Value).GetString());
    }

    [Fact]
    public void AddWallClockArithmetic()
    {
        JsonElement dt = Build.DateTime(2026, 12, 31, 23, 59, 30);
        Assert.Equal("2027-01-01T00:00:15", Iso.FormatIso(Calendar.Add(dt, Build.Period(seconds: 45)).Value).GetString());
        Assert.Equal("2026-12-31T23:59:30", Iso.FormatIso(Calendar.Add(dt, Build.Period(days: 1, hours: -24)).Value).GetString());
        Assert.Equal("2026-11-05T00:00:01", Iso.FormatIso(Calendar.Add(Build.DateTime(2026, 11, 5, 0, 0), Build.Period(seconds: 1)).Value).GetString());
        Assert.Equal("2024-02-29", Iso.FormatIso(Calendar.Add(Build.Date(2028, 2, 29), Build.Period(years: -4)).Value).GetString());
        Assert.Equal(UtvStatus.Invalid, Calendar.Add(dt, Build.PosixSeconds(1)).Status);
        Assert.Equal(UtvStatus.Invalid, Calendar.Add(dt, Build.Date(2026, 1, 1)).Status);
    }

    [Fact]
    public void ResolveChecksAnOffsetAssertion()
    {
        ZoneRules rules = DemoRules.Value;
        // The second 01:30 of the New York overlap, as toZone writes it.
        JsonElement second = Zones.ToZone(Build.Instant(1793514600), "America/New_York", rules).Value;
        Assert.Equal("2026-11-01T01:30:00-05:00[America/New_York]", Iso.FormatIso(second).GetString());
        UtvResult back = Zones.Resolve(second, rules);
        Assert.Equal(UtvStatus.Unique, back.Status);
        Assert.Equal("1793514600", back.Value.GetProperty("value").GetProperty("ticks").GetString());

        UtvResult wrong = Zones.Resolve(Iso.ParseIso("2026-07-04T12:00-05:00[America/New_York]").Value, rules);
        Assert.Equal(UtvStatus.Invalid, wrong.Status);
        Assert.Equal("offset-mismatch", Assert.Single(wrong.Diagnostics).Code);
    }

    [Fact]
    public void ResolvePolicies()
    {
        ZoneRules rules = DemoRules.Value;
        JsonElement overlap = Build.InZone(Build.DateTime(2026, 11, 1, 1, 30), "America/New_York");
        UtvResult all = Zones.Resolve(overlap, rules, new ResolutionPolicy(Overlap: OverlapPolicy.All));
        Assert.Equal(UtvStatus.Multiple, all.Status);
        Assert.Equal(2, all.Candidates.Count);
        Assert.Empty(all.Diagnostics);
        Assert.Equal("ambiguous", Assert.Single(Zones.Resolve(overlap, rules).Diagnostics).Code);
        Assert.Equal(UtvStatus.Unsupported, Zones.Resolve(overlap, rules, new ResolutionPolicy(Overlap: OverlapPolicy.OccurrenceIndex)).Status);

        JsonElement gap = Build.InZone(Build.DateTime(2026, 3, 29, 2, 30), "Europe/Paris");
        Assert.Equal("gap", Assert.Single(Zones.Resolve(gap, rules).Diagnostics).Code);
        UtvResult shifted = Zones.Resolve(gap, rules, new ResolutionPolicy(Gap: GapPolicy.ShiftForwardByGap));
        Assert.Equal("2026-03-29T03:30:00+02:00[Europe/Paris]", Iso.FormatIso(Zones.ToZone(shifted.Value, "Europe/Paris", rules).Value).GetString());
        Assert.Equal(UtvStatus.Unsupported, Zones.Resolve(gap, rules, new ResolutionPolicy(Gap: GapPolicy.NextValidBoundary)).Status);

        Assert.Equal(UtvStatus.Invalid, Zones.Resolve(Build.InZone(Build.DateTime(2026, 2, 30, 9, 0), "Europe/Paris"), rules).Status);
        Assert.Throws<ArgumentNullException>(() => Zones.Resolve(gap, null!));
    }

    [Fact]
    public void ZoneRulesLoader()
    {
        ZoneRules rules = DemoRules.Value;
        Assert.Equal("demo-zones", rules.RuleSetId);
        Assert.Equal(new[] { "America/New_York", "Atlantic/Reykjavik", "Europe/Paris" }, rules.ZoneIds);
        Assert.True(rules.Contains("Europe/Paris"));
        Assert.False(rules.Contains("europe/paris"));
        Assert.Throws<FormatException>(() => ZoneRules.Parse("{"));
        Assert.Throws<FormatException>(() => ZoneRules.Parse("""{"ruleSet":{"system":"s","id":"i","version":"v"},"validFrom":"1","validUntil":"0","zones":{}}"""));
        Assert.Throws<FormatException>(() => ZoneRules.Parse("""{"validFrom":"0"}"""));
    }

    [Fact]
    public void PolicyWireStrings()
    {
        foreach (GapPolicy g in Enum.GetValues<GapPolicy>())
        {
            Assert.True(PolicyWire.TryParseGap(g.ToWireString(), out GapPolicy back));
            Assert.Equal(g, back);
        }
        foreach (OverlapPolicy o in Enum.GetValues<OverlapPolicy>())
        {
            Assert.True(PolicyWire.TryParseOverlap(o.ToWireString(), out OverlapPolicy back));
            Assert.Equal(o, back);
        }
        using JsonDocument bad = JsonDocument.Parse("""{"gap":"sideways"}""");
        Assert.False(ResolutionPolicy.TryParse(bad.RootElement, out _));
        using JsonDocument partial = JsonDocument.Parse("""{"overlap":"latest"}""");
        Assert.True(ResolutionPolicy.TryParse(partial.RootElement, out ResolutionPolicy p));
        Assert.Equal(new ResolutionPolicy(GapPolicy.Reject, OverlapPolicy.Latest), p);
    }

    [Fact]
    public void RangesEdgeCases()
    {
        JsonElement oct1 = Build.Date(2026, 10, 1);
        JsonElement nov1 = Build.Date(2026, 11, 1);
        JsonElement nov2 = Build.Date(2026, 11, 2);
        JsonElement dec1 = Build.Date(2026, 12, 1);
        // [1 Dec, 1 Nov) is provably empty, so it overlaps nothing.
        Assert.Equal("impossible", Ranges.Overlaps(Build.Interval(dec1, nov1), Build.Interval(oct1, Build.Date(2027, 1, 1))).GetString());
        Assert.Equal("impossible", Ranges.Contains(Build.Interval(nov1, nov1), nov1).GetString());
        Assert.Equal("definitely", Ranges.Contains(Build.Interval(nov1, nov1, endIncluded: true), nov1).GetString());
        // (1 Nov, 1 Dec) starts on 2 Nov; [1 Oct, 2 Nov) ends before it.
        Assert.Equal("impossible", Ranges.Overlaps(Build.Interval(nov1, dec1, startIncluded: false), Build.Interval(oct1, nov2)).GetString());
        Assert.Equal("definitely", Ranges.Overlaps(Build.Interval(nov1, dec1, startIncluded: false), Build.Interval(oct1, nov2, endIncluded: true)).GetString());
        Assert.Equal("possibly", Ranges.Overlaps(Build.Interval(IntervalBound.Unknown, IntervalBound.Excluded(dec1)), Build.Interval(oct1, nov2)).GetString());
        Assert.Equal(UtvStatus.Unsupported, Ranges.Overlaps(Build.Interval(nov1, dec1), Build.Interval(Build.Instant(0), Build.Instant(1))).Status);
        Assert.Equal(UtvStatus.Invalid, Ranges.Contains(Build.Interval(nov1, dec1), Build.Date(2026, 2, 30)).Status);
    }

    [Fact]
    public void DatesMustAgreeOnTheirZone()
    {
        JsonElement paris5 = Build.InZone(Build.Date(2026, 11, 5), "Europe/Paris");
        JsonElement paris6 = Build.InZone(Build.Date(2026, 11, 6), "Europe/Paris");
        JsonElement plain6 = Build.Date(2026, 11, 6);
        Assert.Equal(1, Calendar.DaysBetween(paris5, paris6).GetInt64());
        UtvResult mixed = Calendar.DaysBetween(paris5, plain6);
        Assert.Equal(UtvStatus.Unsupported, mixed.Status);
        Assert.Equal("different-zones", Assert.Single(mixed.Diagnostics).Code);
        Assert.Equal("definitely", Ranges.Contains(Build.Interval(paris5, Build.InZone(Build.Date(2026, 12, 1), "Europe/Paris")), paris6).GetString());
        Assert.Equal(UtvStatus.Unsupported, Ranges.Contains(Build.Interval(Build.Date(2026, 11, 1), Build.Date(2026, 12, 1)), paris6).Status);
        Assert.Equal(UtvStatus.Unsupported, Ranges.Overlaps(Build.Interval(IntervalBound.Included(paris5), IntervalBound.Unbounded), Build.Interval(IntervalBound.Included(plain6), IntervalBound.Unbounded)).Status);
    }

    [Fact]
    public void OperationsDoNotDependOnTheCurrentCulture()
    {
        string invariant = Run(CultureInfo.InvariantCulture);
        var odd = (CultureInfo)CultureInfo.InvariantCulture.Clone();
        odd.NumberFormat.NegativeSign = "~";
        odd.NumberFormat.NumberDecimalSeparator = ",";
        odd.NumberFormat.NumberGroupSeparator = "'";
        Assert.Equal(invariant, Run(odd));
        Assert.DoesNotContain("~", invariant, StringComparison.Ordinal);

        static string Run(CultureInfo culture)
        {
            CultureInfo saved = CultureInfo.CurrentCulture;
            try
            {
                CultureInfo.CurrentCulture = culture;
                return string.Join("|",
                    Iso.FormatIso(Iso.ParseIso("-0500-03-01").Value).ToJson(),
                    Iso.FormatIso(Build.Instant(-1, 500_000_000)).ToJson(),
                    Calendar.DaysBetween(Build.Date(2026, 12, 25), Build.Date(2026, 11, 5)).ToJson(),
                    Calendar.PosixSecondsBetween(Build.Instant(1, 500_000_000), Build.Instant(0)).ToJson(),
                    Calendar.Add(Build.Date(2026, 1, 31), Build.Period(months: -1)).ToJson(),
                    Zones.ToOffset(Build.Instant(1793885400), -18000).ToJson());
            }
            finally
            {
                CultureInfo.CurrentCulture = saved;
            }
        }
    }

    [Fact]
    public void LibraryNeverReadsClockZoneOrCulture()
    {
        string src = Fixtures.PathOf("implementations", "csharp", "src", "Utv");
        var forbidden = new Regex(@"\b(DateTime(Offset)?\.(Now|UtcNow|Today)|TimeZoneInfo\.(Local|FindSystemTimeZoneById|ConvertTime)|CurrentCulture|CurrentUICulture|Stopwatch|Environment\.TickCount|TimeProvider)\b");
        foreach (string file in Directory.EnumerateFiles(src, "*.cs", SearchOption.AllDirectories).Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal)))
        {
            foreach (string line in File.ReadLines(file).Where(l => !l.TrimStart().StartsWith("//", StringComparison.Ordinal)))
            {
                Assert.False(forbidden.IsMatch(line), $"{Path.GetFileName(file)}: {line.Trim()}");
            }
        }
    }
}
