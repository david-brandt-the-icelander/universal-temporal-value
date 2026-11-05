using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Utv;
using CultureInfo = System.Globalization.CultureInfo;
using ISOWeek = System.Globalization.ISOWeek;

namespace Utv.Examples;

/// <summary>
/// Everyday date/time tasks, side by side: what you write today with .NET's own types, and the same task with UTV.
/// Native outputs are printed as they come (they can depend on the host's clock and time zone database).
/// UTV outputs are deterministic and are checked; a mismatch makes <see cref="Run"/> return 1.
/// </summary>
internal sealed class EverydayTour
{
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    private readonly TextWriter _out;
    private readonly ZoneRules _rules;
    private readonly Func<string, TimeZoneInfo?> _findZone;
    private int _checked;
    private int _mismatches;

    private EverydayTour(TextWriter output, ZoneRules rules, Func<string, TimeZoneInfo?> findZone)
    {
        _out = output;
        _rules = rules;
        _findZone = findZone;
    }

    /// <summary>Runs every section and returns 0 when every UTV outcome is as expected, otherwise 1.</summary>
    /// <param name="output">Where to print.</param>
    /// <param name="rulesPath">Path of <c>conformance/rules/demo-zones.json</c>.</param>
    /// <param name="findZone">Looks up a host time zone for the native side; null if unavailable. Defaults to the host's tz database.</param>
    public static int Run(TextWriter output, string rulesPath, Func<string, TimeZoneInfo?>? findZone = null)
    {
        var tour = new EverydayTour(output, ZoneRules.Parse(File.ReadAllText(rulesPath)), findZone ?? FindHostZone);
        tour.Now();
        tour.ADate();
        tour.InvalidDates();
        tour.Adding();
        tour.DaysBetween();
        tour.TimesAndDateTimes();
        tour.ParsingWithOffsets();
        tour.TimeZones();
        tour.DstGap();
        tour.DstOverlap();
        tour.Timestamps();
        tour.DurationsVsPeriods();
        tour.RangesSection();
        tour.RoundTrip();
        output.WriteLine();
        output.WriteLine(tour._mismatches == 0
            ? $"{tour._checked} UTV outcomes checked, all as expected."
            : $"{tour._mismatches} of {tour._checked} UTV outcomes were not as expected.");
        return tour._mismatches == 0 ? 0 : 1;
    }

    // ------------------------------------------------------------------ sections

    private void Now()
    {
        Section(1, "Now");
        DateTime local = DateTime.Now;                 // reads the clock and the host's zone
        DateTimeOffset utcNow = DateTimeOffset.UtcNow; // reads the clock
        Native("DateTime.Now", $"{local.ToString("o", Inv)} (Kind={local.Kind})");
        Native("DateTimeOffset.UtcNow", utcNow.ToString("o", Inv));

        // UTV never reads the clock. The example reads it (above) and passes the reading in, by intent.
        Check("Interop.FromDateTime(DateTime.Now)", Interop.FromDateTime(local), "unsupported (local-zone)");
        JsonElement now = Interop.FromDateTimeOffset(utcNow, DateTimeOffsetIntent.Instant);
        CheckStatus("Interop.FromDateTimeOffset(utcNow, Instant)", Iso.FormatIso(now), UtvStatus.Unique);
        Note("the library has no Now(); the clock reading is an explicit input, and a Kind=Local value is refused, never read with the host zone");
    }

    private void ADate()
    {
        Section(2, "A date");
        var date = new DateOnly(2026, 11, 5);
        Native("new DateOnly(2026, 11, 5)", date.ToString("yyyy-MM-dd", Inv));
        DateOnly parsed = DateOnly.ParseExact("2026-11-05", "yyyy-MM-dd", Inv);
        Native("DateOnly.ParseExact(\"2026-11-05\")", parsed.ToString("yyyy-MM-dd", Inv));
        Native("DayOfWeek, ISOWeek.GetWeekOfYear", $"{parsed.DayOfWeek}, week {ISOWeek.GetWeekOfYear(parsed.ToDateTime(TimeOnly.MinValue))}");

        JsonElement d = Build.Date(2026, 11, 5);
        Check("FormatIso(Build.Date(2026, 11, 5))", Iso.FormatIso(d), "unique 2026-11-05");
        Check("ParseIso(\"2026-11-05\")", Iso.ParseIso("2026-11-05"), "unique 2026-11-05");
        Check("Calendar.Fields(date)", Calendar.Fields(d),
            """unique {"dayOfWeek":4,"dayOfYear":309,"isoWeekYear":"2026","isoWeek":45,"daysInMonth":30,"isLeapYear":false}""");
        Note("same answers; UTV numbers days of week ISO-style (Monday = 1, so Thursday = 4) where .NET's DayOfWeek has Sunday = 0");
    }

    private void InvalidDates()
    {
        Section(3, "Invalid dates");
        try
        {
            Native("new DateOnly(2026, 2, 30)", new DateOnly(2026, 2, 30).ToString("yyyy-MM-dd", Inv));
        }
        catch (ArgumentOutOfRangeException e)
        {
            Native("new DateOnly(2026, 2, 30)", $"throws {e.GetType().Name}");
        }
        Native("DateOnly.TryParseExact(\"2026-02-30\")", DateOnly.TryParseExact("2026-02-30", "yyyy-MM-dd", Inv, System.Globalization.DateTimeStyles.None, out _).ToString());

        Check("ParseIso(\"2026-02-30\")", Iso.ParseIso("2026-02-30"), "invalid (day-out-of-range)");
        JsonElement feb30 = Build.Date(2026, 2, 30); // builders build shapes; they don't judge them
        Check("Calendar.Fields(Build.Date(2026, 2, 30))", Calendar.Fields(feb30), "invalid (day-out-of-range)");
        Note(".NET throws (constructor) or returns false (TryParse); UTV returns status invalid with a diagnostic saying why");
    }

    private void Adding()
    {
        Section(4, "Adding");
        var nov5 = new DateOnly(2026, 11, 5);
        Native("nov5.AddDays(30)", nov5.AddDays(30).ToString("yyyy-MM-dd", Inv));
        Native("nov5.AddMonths(1)", nov5.AddMonths(1).ToString("yyyy-MM-dd", Inv));
        Native("new DateOnly(2026, 1, 31).AddMonths(1)", new DateOnly(2026, 1, 31).AddMonths(1).ToString("yyyy-MM-dd", Inv));

        JsonElement d = Build.Date(2026, 11, 5);
        Check("Calendar.Add(nov5, Period(days: 30))", Calendar.Add(d, Build.Period(days: 30)), "unique 2026-12-05");
        Check("Calendar.Add(nov5, Period(months: 1))", Calendar.Add(d, Build.Period(months: 1)), "unique 2026-12-05");
        JsonElement jan31 = Build.Date(2026, 1, 31);
        Check("Calendar.Add(jan31, Period(months: 1))", Calendar.Add(jan31, Build.Period(months: 1)), "none (month-end)");
        Check("... MonthEndPolicy.Constrain", Calendar.Add(jan31, Build.Period(months: 1), MonthEndPolicy.Constrain), "unique 2026-02-28");
        Note(".NET's AddMonths clamps 31 January to 28 February silently; UTV says there is no 31 February (none) until you choose to clamp");
    }

    private void DaysBetween()
    {
        Section(5, "Days between");
        var nov5 = new DateOnly(2026, 11, 5);
        var dec25 = new DateOnly(2026, 12, 25);
        Native("dec25.DayNumber - nov5.DayNumber", (dec25.DayNumber - nov5.DayNumber).ToString(Inv));
        Native("nov5 < dec25", (nov5 < dec25).ToString());

        JsonElement a = Build.Date(2026, 11, 5);
        JsonElement b = Build.Date(2026, 12, 25);
        Check("Calendar.DaysBetween(nov5, dec25)", Calendar.DaysBetween(a, b), "unique 50");
        Check("Calendar.Compare(nov5, dec25)", Calendar.Compare(a, b), "unique before");
    }

    private void TimesAndDateTimes()
    {
        Section(6, "Times and date-times");
        Native("new TimeOnly(9, 30)", new TimeOnly(9, 30).ToString("HH:mm", Inv));
        Native("new DateTime(2026, 11, 5, 23, 30, 0).AddHours(2)", new DateTime(2026, 11, 5, 23, 30, 0).AddHours(2).ToString("yyyy-MM-ddTHH:mm", Inv));
        Native("new TimeOnly(23, 30).AddHours(2)", new TimeOnly(23, 30).AddHours(2).ToString("HH:mm", Inv));

        Check("FormatIso(Build.Time(9, 30))", Iso.FormatIso(Build.Time(9, 30)), "unique 09:30");
        Check("Calendar.Add(2026-11-05T23:30, Period(hours: 2))",
            Calendar.Add(Build.DateTime(2026, 11, 5, 23, 30), Build.Period(hours: 2)), "unique 2026-11-06T01:30");
        Check("Calendar.Add(Build.Time(23, 30), Period(hours: 2))",
            Calendar.Add(Build.Time(23, 30), Build.Period(hours: 2)), "unsupported (time-wraps)");
        Note("TimeOnly.AddHours wraps past midnight silently; UTV refuses, since the day is lost; add to a date-time instead");
    }

    private void ParsingWithOffsets()
    {
        Section(7, "Parsing with offsets");
        foreach (string text in new[] { "2026-11-05T14:30:00+01:00", "2026-11-05T14:30:00Z", "2026-11-05T14:30:00+00:00" })
        {
            DateTimeOffset dto = DateTimeOffset.Parse(text, Inv);
            Native($"DateTimeOffset.Parse(\"{text}\")", $"{dto.ToString("o", Inv)} (Offset {dto.Offset})");
        }
        DateTimeOffset z = DateTimeOffset.Parse("2026-11-05T14:30:00Z", Inv);
        DateTimeOffset zero = DateTimeOffset.Parse("2026-11-05T14:30:00+00:00", Inv);
        Native("z.EqualsExact(plusZero)", z.EqualsExact(zero).ToString());

        Check("ParseIso(\"…+01:00\")", Iso.ParseIso("2026-11-05T14:30:00+01:00"), "unique 2026-11-05T14:30:00+01:00");
        UtvResult utc = Iso.ParseIso("2026-11-05T14:30:00Z");
        UtvResult plusZero = Iso.ParseIso("2026-11-05T14:30:00+00:00");
        Check("ParseIso(\"…Z\")", utc, "unique 2026-11-05T14:30:00Z");
        Check("ParseIso(\"…+00:00\")", plusZero, "unique 2026-11-05T14:30:00+00:00");
        _out.WriteLine($"  UTV:    kinds: Z → {utc.Value.GetProperty("kind")}, +00:00 → {plusZero.Value.GetProperty("kind")} with a fixed offset of 0");
        Check("Calendar.Compare(Z value, +00:00 value)", Calendar.Compare(utc.Value, plusZero.Value), "unsupported (incomparable)");
        Note(".NET turns both Z and +00:00 into the same DateTimeOffset; UTV keeps Z (UTC known, local offset unknown: an instant) apart from +00:00 (a wall time at offset 0)");
    }

    private void TimeZones()
    {
        Section(8, "Time zones");
        TimeZoneInfo? paris = Zone("Europe/Paris");
        TimeZoneInfo? newYork = Zone("America/New_York");
        if (paris is not null && newYork is not null)
        {
            var meeting = new DateTime(2027, 6, 1, 9, 0, 0, DateTimeKind.Unspecified);
            DateTime utc = TimeZoneInfo.ConvertTimeToUtc(meeting, paris);
            DateTime inNewYork = TimeZoneInfo.ConvertTimeFromUtc(utc, newYork);
            Native("ConvertTimeToUtc(09:00, Paris)", utc.ToString("yyyy-MM-ddTHH:mm:ssZ", Inv));
            Native("ConvertTimeFromUtc(utc, New York)", inNewYork.ToString("yyyy-MM-ddTHH:mm", Inv) + " (host tz database)");
        }

        JsonElement meetingUtv = Build.InZone(Build.DateTime(2027, 6, 1, 9, 0), "Europe/Paris");
        Check("FormatIso(Build.InZone(…09:00, \"Europe/Paris\"))", Iso.FormatIso(meetingUtv), "unique 2027-06-01T09:00[Europe/Paris]");
        UtvResult instant = Zones.Resolve(meetingUtv, _rules);
        Check("Zones.Resolve(meeting, demoRules)", instant, "unique 2027-06-01T07:00:00Z");
        Check("Zones.ToZone(instant, \"America/New_York\", demoRules)",
            Zones.ToZone(instant.Value, "America/New_York", _rules), "unique 2027-06-01T03:00:00-04:00[America/New_York]");
        Note("the meeting stays \"09:00 in Paris\" until resolved; the rules are data passed in (demo-zones.json), not the host's database");
    }

    private void DstGap()
    {
        Section(9, "DST gap");
        var wall = new DateTime(2026, 3, 8, 2, 30, 0, DateTimeKind.Unspecified);
        if (Zone("America/New_York") is TimeZoneInfo newYork)
        {
            Native("newYork.IsInvalidTime(02:30)", newYork.IsInvalidTime(wall).ToString());
            try
            {
                Native("ConvertTimeToUtc(02:30, New York)", TimeZoneInfo.ConvertTimeToUtc(wall, newYork).ToString("o", Inv));
            }
            catch (ArgumentException e)
            {
                Native("ConvertTimeToUtc(02:30, New York)", $"throws {e.GetType().Name}");
            }
        }

        JsonElement local = Build.InZone(Build.DateTime(2026, 3, 8, 2, 30), "America/New_York");
        Check("Zones.Resolve(02:30 New York)", Zones.Resolve(local, _rules), "none (gap)");
        UtvResult shifted = Zones.Resolve(local, _rules, new ResolutionPolicy(Gap: GapPolicy.ShiftForwardByGap));
        Check("... GapPolicy.ShiftForwardByGap", shifted, "unique 2026-03-08T07:30:00Z");
        Check("Zones.ToZone(shifted, \"America/New_York\")",
            Zones.ToZone(shifted.Value, "America/New_York", _rules), "unique 2026-03-08T03:30:00-04:00[America/New_York]");
        Note("02:30 does not exist that night; .NET throws on conversion (host tz database), UTV answers none, then shifts only when asked");
    }

    private void DstOverlap()
    {
        Section(10, "DST overlap");
        var wall = new DateTime(2026, 11, 1, 1, 30, 0, DateTimeKind.Unspecified);
        if (Zone("America/New_York") is TimeZoneInfo newYork)
        {
            Native("newYork.IsAmbiguousTime(01:30)", newYork.IsAmbiguousTime(wall).ToString());
            Native("GetAmbiguousTimeOffsets(01:30)", string.Join(", ", newYork.GetAmbiguousTimeOffsets(wall).Select(o => o.ToString())));
            Native("ConvertTimeToUtc(01:30, New York)", TimeZoneInfo.ConvertTimeToUtc(wall, newYork).ToString("yyyy-MM-ddTHH:mm:ssZ", Inv) + " (picks one silently)");
        }

        JsonElement local = Build.InZone(Build.DateTime(2026, 11, 1, 1, 30), "America/New_York");
        Check("Zones.Resolve(01:30 New York)", Zones.Resolve(local, _rules),
            "multiple 2026-11-01T05:30:00Z, 2026-11-01T06:30:00Z (ambiguous)");
        Check("... OverlapPolicy.Earliest", Zones.Resolve(local, _rules, new ResolutionPolicy(Overlap: OverlapPolicy.Earliest)),
            "unique 2026-11-01T05:30:00Z");
        Note(".NET's ConvertTimeToUtc picks standard time for an ambiguous time; UTV returns both instants until a policy picks one");
    }

    private void Timestamps()
    {
        Section(11, "Timestamps");
        DateTimeOffset a = DateTimeOffset.FromUnixTimeMilliseconds(1_793_885_400_250);
        DateTimeOffset b = DateTimeOffset.FromUnixTimeSeconds(1_793_889_001);
        Native("FromUnixTimeMilliseconds(1793885400250)", a.ToString("yyyy-MM-ddTHH:mm:ss.FFFFFFFZ", Inv));
        Native("a.ToUnixTimeSeconds()", a.ToUnixTimeSeconds().ToString(Inv) + " (fraction truncated)");
        Native("(b - a).TotalSeconds", (b - a).TotalSeconds.ToString(Inv) + " (a double)");

        JsonElement ua = Build.Instant(1_793_885_400, 250_000_000);
        JsonElement ub = Build.Instant(1_793_889_001);
        Check("FormatIso(Build.Instant(1793885400, 250000000))", Iso.FormatIso(ua), "unique 2026-11-05T13:30:00.25Z");
        Check("Calendar.PosixSecondsBetween(a, b)", Calendar.PosixSecondsBetween(ua, ub), "unique 14403/4 POSIX s");
        Note("UTV keeps the fraction exactly, as a reduced rational (3600.75 s = 14403/4)");
    }

    private void DurationsVsPeriods()
    {
        Section(12, "Durations vs periods");
        Native("TimeSpan.FromDays(1)", TimeSpan.FromDays(1).ToString());
        var beforeDst = new DateTimeOffset(2026, 3, 7, 12, 0, 0, TimeSpan.FromHours(-5));
        Native("new DateTimeOffset(2026-03-07T12:00-05:00).AddDays(1)", beforeDst.AddDays(1).ToString("yyyy-MM-ddTHH:mmzzz", Inv) + " (offset kept, though New York is at -04:00 by then)");

        JsonElement noon = Build.InZone(Build.DateTime(2026, 3, 7, 12, 0), "America/New_York");
        UtvResult nextDay = Calendar.Add(noon, Build.Period(days: 1));
        Check("Calendar.Add(noon NY, Period(days: 1))", nextDay, "unique 2026-03-08T12:00[America/New_York]");
        Check("Zones.Resolve(that)", Zones.Resolve(nextDay.Value, _rules), "unique 2026-03-08T16:00:00Z");
        JsonElement noonInstant = Zones.Resolve(noon, _rules).Value;
        Check("Calendar.Add(noon instant, PosixSeconds(86400))", Calendar.Add(noonInstant, Build.PosixSeconds(86400)), "unique 2026-03-08T17:00:00Z");
        using JsonDocument si = JsonDocument.Parse("""{"schema":"utv/1.0","kind":"amount","value":{"basis":"elapsed","seconds":{"numerator":"86400","denominator":"1"}}}""");
        Check("Calendar.Add(noon instant, 86400 SI seconds)", Calendar.Add(noonInstant, si.RootElement), "unsupported (needs-leap-seconds)");
        Note("a calendar day lasts 23 hours that day (DST starts in New York), while 86400 POSIX seconds is always 24 hours; SI seconds need a leap-second table");
    }

    private void RangesSection()
    {
        Section(13, "Ranges");
        var start = new DateOnly(2026, 11, 1);
        var end = new DateOnly(2026, 12, 1);
        var day = new DateOnly(2026, 11, 15);
        Native("day >= start && day < end", (day >= start && day < end).ToString() + " (no range type; you write the bounds by hand)");

        JsonElement november = Build.Interval(Build.Date(2026, 11, 1), Build.Date(2026, 12, 1));
        JsonElement december = Build.Interval(Build.Date(2026, 12, 1), Build.Date(2027, 1, 1));
        Check("Ranges.Contains(november, 15 Nov)", Ranges.Contains(november, Build.Date(2026, 11, 15)), "unique definitely");
        Check("Ranges.Contains(november, 1 Dec)", Ranges.Contains(november, Build.Date(2026, 12, 1)), "unique impossible");
        Check("Ranges.Overlaps(november, december)", Ranges.Overlaps(november, december), "unique impossible");
        JsonElement fromNov = Build.Interval(IntervalBound.Included(Build.Date(2026, 11, 1)), IntervalBound.Unbounded);
        Check("Ranges.Contains([1 Nov, ∞), 1 Jan 3000)", Ranges.Contains(fromNov, Build.Date(3000, 1, 1)), "unique definitely");
        JsonElement endUnknown = Build.Interval(IntervalBound.Included(Build.Date(2026, 11, 1)), IntervalBound.Unknown);
        Check("Ranges.Contains([1 Nov, ?), 1 Jun 2027)", Ranges.Contains(endUnknown, Build.Date(2027, 6, 1)), "unique possibly");
        Note("an unknown end is not an open end: UTV answers possibly, not true or false");
    }

    private void RoundTrip()
    {
        Section(14, "Round trip");
        var dto = new DateTimeOffset(2026, 11, 5, 14, 30, 0, TimeSpan.FromHours(1));
        JsonElement utv = Interop.FromDateTimeOffset(dto, DateTimeOffsetIntent.OffsetDateTime);
        Check("Interop.FromDateTimeOffset(dto, OffsetDateTime)", Iso.FormatIso(utv), "unique 2026-11-05T14:30:00+01:00");
        NativeResult<DateTimeOffset> back = Interop.ToDateTimeOffset(utv);
        Native("Interop.ToDateTimeOffset(…).Value.EqualsExact(dto)", back.Value.EqualsExact(dto).ToString());
        Expect("round trip", back.IsUnique && !back.IsLossy && back.Value.EqualsExact(dto), "unique, lossless, EqualsExact");

        var localKind = new DateTime(2026, 11, 5, 9, 0, 0, DateTimeKind.Local);
        Check("Interop.FromDateTime(Kind=Local)", Interop.FromDateTime(localKind), "unsupported (local-zone)");

        NativeResult<DateTimeOffset> zoned = Interop.ToDateTimeOffset(Iso.ParseIso("2027-06-01T09:00+02:00[Europe/Paris]").Value);
        Native("Interop.ToDateTimeOffset(…+02:00[Europe/Paris])", $"{zoned.Value.ToString("o", Inv)} IsLossy={zoned.IsLossy} ({string.Join(", ", zoned.Diagnostics.Select(d => d.Code))})");
        Expect("zoned → DateTimeOffset is lossy", zoned.IsUnique && zoned.IsLossy, "unique, lossy (zone-dropped)");
        NativeResult<DateOnly> farFuture = Interop.ToDateOnly(Iso.ParseIso("+10000-01-01").Value);
        Expect("Interop.ToDateOnly(+10000-01-01)", farFuture.Status == UtvStatus.Unsupported, "unsupported (out-of-native-range)");
        Note("a Kind=Local DateTime is refused rather than read with the host zone; conversions that drop information say so");
    }

    // ------------------------------------------------------------------ printing and checking

    private void Section(int number, string title)
    {
        if (number > 1)
        {
            _out.WriteLine();
        }
        _out.WriteLine($"== {number}. {title} ==");
    }

    private void Native(string code, string result) => _out.WriteLine($"  native: {code} → {result}");

    private void Note(string text) => _out.WriteLine($"  note:   {text}");

    private void Check(string code, UtvResult result, string expected)
    {
        string actual = Describe(result);
        _out.WriteLine($"  UTV:    {code} → {actual}");
        Count(actual == expected, expected);
    }

    private void CheckStatus(string code, UtvResult result, UtvStatus expected)
    {
        string actual = Describe(result);
        _out.WriteLine($"  UTV:    {code} → {actual}");
        Count(result.Status == expected, expected.ToWireString());
    }

    private void Expect(string what, bool holds, string expected)
    {
        _out.WriteLine($"  UTV:    {what} → {(holds ? expected : "NOT " + expected)}");
        Count(holds, expected);
    }

    private void Count(bool ok, string expected)
    {
        _checked++;
        if (!ok)
        {
            _mismatches++;
            _out.WriteLine($"  MISMATCH: expected {expected}");
        }
    }

    /// <summary>The status, then each candidate (ISO text where there is one), then diagnostic codes for non-answers and warnings.</summary>
    private static string Describe(UtvResult result)
    {
        string text = result.Status.ToWireString();
        if (result.Candidates.Count > 0)
        {
            text += " " + string.Join(", ", result.Candidates.Select(Text));
        }
        string[] codes = result.Diagnostics.Where(d => d.Severity != DiagnosticSeverity.Info).Select(d => d.Code).ToArray();
        return codes.Length > 0 ? $"{text} ({string.Join(", ", codes)})" : text;
    }

    private static string Text(JsonElement candidate)
    {
        if (candidate.ValueKind == JsonValueKind.String)
        {
            return candidate.GetString()!;
        }
        if (candidate.ValueKind == JsonValueKind.Object && candidate.TryGetProperty("kind", out JsonElement kind))
        {
            if (kind.GetString() == "amount" && candidate.GetProperty("value").TryGetProperty("seconds", out JsonElement s))
            {
                string n = s.GetProperty("numerator").GetString()!;
                string d = s.GetProperty("denominator").GetString()!;
                return (d == "1" ? n : $"{n}/{d}") + " POSIX s";
            }
            UtvResult iso = Iso.FormatIso(candidate);
            return iso.IsUnique ? iso.GetString() : candidate.GetRawText();
        }
        return candidate.GetRawText();
    }

    private TimeZoneInfo? Zone(string id)
    {
        TimeZoneInfo? zone = _findZone(id);
        if (zone is null)
        {
            Native($"FindSystemTimeZoneById(\"{id}\")", "not available on this host; skipping the native side");
        }
        return zone;
    }

    private static TimeZoneInfo? FindHostZone(string id)
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(id);
        }
        catch (Exception e) when (e is TimeZoneNotFoundException or InvalidTimeZoneException or System.Security.SecurityException)
        {
            return null;
        }
    }
}
