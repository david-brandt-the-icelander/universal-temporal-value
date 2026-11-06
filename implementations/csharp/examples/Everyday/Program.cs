// Everyday date/time tasks, done twice: with .NET's own types, then with UTV.
//
//   dotnet run --project examples/Everyday
//
// Each Row(...) prints the answer and a note; its output is the trailing comment. Rows marked ‡ depend on
// this machine (its clock or its time zone database). Every other line is checked against everyday.expected.txt.

using System;
using System.Globalization;
using System.IO;
using Utv;
using DateOutcome = Utv.Outcome<Utv.CivilDate>;       // short names for the cases: DateOutcome.None(var why)
using InstantOutcome = Utv.Outcome<Utv.Instant>;

CultureInfo inv = CultureInfo.InvariantCulture;

// UTV's zone rules are data you pass in (conformance/rules/demo-zones.json: three zones, 2026-2027 only).
// .NET's come from this machine's time zone database.
var ctx = TemporalContext.With(ZoneRules.Load(Path.Combine(AppContext.BaseDirectory, "demo-zones.json")));
TimeZoneInfo newYork = TimeZoneInfo.FindSystemTimeZoneById("America/New_York");
TimeZoneInfo paris = TimeZoneInfo.FindSystemTimeZoneById("Europe/Paris");

Console.WriteLine("Everyday date/time tasks: .NET and UTV, side by side");

Section("1. Now");
Title("Now");
// BEGIN now
DateTime localNow = DateTime.Now;                                   // reads the clock and this machine's zone
Row(".NET", localNow.ToString("O", inv), $"Kind = {localNow.Kind}", host: true);   // 2026-11-05T22:04:32.1234567+00:00  Kind = Local ‡

Instant now = Instant.FromDateTimeOffset(DateTimeOffset.UtcNow);    // UTV never reads the clock: you pass the reading in
Row("UTV", now.ToString(), "the reading you passed in", host: true);              // 2026-11-05T22:04:32.1234567Z  the reading you passed in ‡
Row("UTV", CivilDateTime.FromDateTime(localNow).ToString(), "Kind = Local is refused");   // unsupported (local-zone)  Kind = Local is refused
// END now

Section("2. A date");
Title("5 November 2026");
// BEGIN a-date
var nov5 = new DateOnly(2026, 11, 5);
Row(".NET", nov5.ToString("O", inv));                               // 2026-11-05
Row(".NET", (DateOnly.Parse("2026-11-05", inv) == nov5).ToString(), "parsed, then ==");   // True  parsed, then ==

CivilDate date = CivilDate.Of(2026, 11, 5);
Row("UTV", date.ToString());                                        // 2026-11-05
Row("UTV", (CivilDate.Parse("2026-11-05").Single() == date).ToString(), "parsed, then ==");   // True  parsed, then ==
// END a-date

Title("Day of week and ISO week");
// BEGIN weekday-week
int nativeWeek = ISOWeek.GetWeekOfYear(nov5.ToDateTime(TimeOnly.MinValue));
Row(".NET", $"{nov5.DayOfWeek}, ISO week {nativeWeek}", "DayOfWeek: Sunday = 0");          // Thursday, ISO week 45  DayOfWeek: Sunday = 0
Row("UTV", $"{date.DayOfWeek}, ISO week {date.IsoWeek}", "Weekday: Monday = 1 ... Sunday = 7");   // Thursday, ISO week 45  Weekday: Monday = 1 ... Sunday = 7
// END weekday-week

Section("3. Invalid dates");
Title("30 February 2026");
// BEGIN feb-30
try
{
    Row(".NET", new DateOnly(2026, 2, 30).ToString("O", inv));
}
catch (ArgumentOutOfRangeException e)
{
    Row(".NET", "throws", e.GetType().Name);                        // throws  ArgumentOutOfRangeException
}
Row(".NET", DateOnly.TryParse("2026-02-30", inv, out _).ToString(), "TryParse");   // False  TryParse

switch (CivilDate.TryOf(2026, 2, 30))                               // CivilDate.Of(2026, 2, 30) throws, like DateOnly
{
    case DateOutcome.Unique(var feb30):
        Row("UTV", feb30.ToString());
        break;
    case DateOutcome.Invalid(var why):
        Row("UTV", "invalid", why.ToString());                      // invalid  day 30 is not in 1..28 for 2026-02 (day-out-of-range)
        break;
}
Row("UTV", CivilDate.Parse("2026-02-30").ToString(), "Parse");      // invalid (day-out-of-range)  Parse
// END feb-30

Section("4. Adding");
Title("5 Nov + 30 days");
// BEGIN add-days
Row(".NET", nov5.AddDays(30).ToString("O", inv));                   // 2026-12-05
Row("UTV", date.AddDays(30).ToString(), "a plain CivilDate: can't fail");   // 2026-12-05  a plain CivilDate: can't fail
// END add-days

Title("31 Jan 2026 + 1 month");
// BEGIN month-end
var jan31 = new DateOnly(2026, 1, 31);
Row(".NET", jan31.AddMonths(1).ToString("O", inv), "clamped silently");   // 2026-02-28  clamped silently

var utvJan31 = CivilDate.Of(2026, 1, 31);
DateOutcome nextMonth = utvJan31.AddMonths(1);                      // an Outcome: the answer may not exist
switch (nextMonth)
{
    case DateOutcome.Unique(var d):
        Row("UTV", d.ToString());
        break;
    case DateOutcome.None(var why):
        Row("UTV", "no such date", why.ToString());                 // no such date  2026-02 has no day 31 (month-end)
        break;
    default:
        Row("UTV", nextMonth.ToString());
        break;
}
Row("UTV", utvJan31.AddMonths(1, MonthEnd.Constrain).ToString(), "with MonthEnd.Constrain");   // 2026-02-28  with MonthEnd.Constrain
// END month-end

Section("5. Days between");
Title("5 Nov → 25 Dec");
// BEGIN days-between
var dec25 = new DateOnly(2026, 12, 25);
Row(".NET", (dec25.DayNumber - nov5.DayNumber).ToString(inv), "DayNumber difference");   // 50  DayNumber difference

CivilDate christmas = CivilDate.Of(2026, 12, 25);
Row("UTV", date.DaysUntil(christmas).ToString(inv));                // 50
// END days-between

Title("Which comes first");
// BEGIN compare-dates
Row(".NET", (nov5 < dec25).ToString(), "nov5 < dec25");             // True  nov5 < dec25
Row("UTV", (date < christmas).ToString(), "date < christmas");      // True  date < christmas

ZonedIntent call = CivilDateTime.Of(2026, 11, 1, 1, 30).InZone("America/New_York");
ZonedIntent standup = CivilDateTime.Of(2026, 11, 1, 1, 45).InZone("America/New_York");
Outcome<Ordering> order = ctx.Compare(call, standup);               // zoned values have no <: the rules decide
Row("UTV", order.ToString(), "01:30 vs 01:45, 1 Nov, New York");    // multiple (ambiguous)  01:30 vs 01:45, 1 Nov, New York
// END compare-dates

Section("6. Times and date-times");
Title("09:30");
// BEGIN time-of-day
Row(".NET", new TimeOnly(9, 30).ToString("O", inv), "seconds always there");   // 09:30:00.0000000  seconds always there
Row("UTV", CivilTime.Of(9, 30).ToString(), "no seconds given, none shown");     // 09:30  no seconds given, none shown
Row("UTV", CivilTime.Of(9, 30, 0).ToString());                      // 09:30:00
// END time-of-day

Title("23:30 + 2 hours");
// BEGIN datetime-plus-hours
Row(".NET", new TimeOnly(23, 30).AddHours(2).ToString("HH:mm", inv), "TimeOnly wraps, the day is lost");   // 01:30  TimeOnly wraps, the day is lost
Row(".NET", new DateTime(2026, 11, 5, 23, 30, 0).AddHours(2).ToString("s", inv));   // 2026-11-06T01:30:00

CivilDateTime lateEvening = CivilDateTime.Of(2026, 11, 5, 23, 30);  // CivilTime has no AddHours at all
Row("UTV", lateEvening.AddHours(2).ToString());                     // 2026-11-06T01:30
// END datetime-plus-hours

Section("7. Parsing with offsets");
Title("Z vs +00:00");
// BEGIN parse-offsets
foreach (string text in new[] { "2026-11-05T14:30:00Z", "2026-11-05T14:30:00+00:00" })
{
    DateTimeOffset parsed = DateTimeOffset.Parse(text, inv);
    Row(".NET", parsed.ToString("O", inv), "a DateTimeOffset");     // 2026-11-05T14:30:00.0000000+00:00  a DateTimeOffset (twice)

    ITemporalValue value = Temporal.Parse(text).Single();
    string kind = value switch
    {
        Instant => "an Instant: UTC, no local offset",
        FixedOffsetDateTime => "a wall time at offset +00:00",
        _ => value.GetType().Name,
    };
    Row("UTV", value.ToString(), kind);                             // 2026-11-05T14:30:00Z  an Instant: UTC, no local offset
}                                                                   // 2026-11-05T14:30:00+00:00  a wall time at offset +00:00
// END parse-offsets

Section("8. Time zones");
Title("09:00 in Paris, seen from New York");
// BEGIN meeting-in-paris
var nineInParis = new DateTime(2027, 6, 1, 9, 0, 0);                // Kind = Unspecified
DateTime meetingUtc = TimeZoneInfo.ConvertTimeToUtc(nineInParis, paris);
Row(".NET", TimeZoneInfo.ConvertTimeFromUtc(meetingUtc, newYork).ToString("s", inv), "this machine's tz database", host: true);   // 2027-06-01T03:00:00  this machine's tz database ‡

ZonedIntent meeting = CivilDateTime.Of(2027, 6, 1, 9, 0).InZone("Europe/Paris");
Row("UTV", meeting.ToString(), "an intention, not an instant");     // 2027-06-01T09:00[Europe/Paris]  an intention, not an instant
Instant start = meeting.Resolve(ctx).Single();                      // Single() throws unless there is exactly one instant
Row("UTV", start.InZone("America/New_York", ctx).Single().ToString(), "rules passed in");   // 2027-06-01T03:00:00-04:00[America/New_York]  rules passed in
// END meeting-in-paris

Section("9. DST gap");
Title("02:30 on 8 March 2026 in New York");
// BEGIN dst-gap
var skipped = new DateTime(2026, 3, 8, 2, 30, 0);
Row(".NET", newYork.IsInvalidTime(skipped).ToString(), "IsInvalidTime", host: true);   // True  IsInvalidTime ‡
try
{
    Row(".NET", TimeZoneInfo.ConvertTimeToUtc(skipped, newYork).ToString("s", inv), host: true);
}
catch (ArgumentException e)
{
    Row(".NET", "throws", e.GetType().Name, host: true);            // throws  ArgumentException ‡
}

ZonedIntent springForward = CivilDateTime.Of(2026, 3, 8, 2, 30).InZone("America/New_York");
InstantOutcome resolved = springForward.Resolve(ctx);
switch (resolved)
{
    case InstantOutcome.Unique(var at):
        Row("UTV", at.ToString());
        break;
    case InstantOutcome.None(var why):
        Row("UTV", "no such time", why.ToString());                 // no such time  clocks jump from 02:00 to 03:00 in America/New_York (gap)
        break;
    default:
        Row("UTV", resolved.ToString());
        break;
}
Instant shifted = springForward.Resolve(ctx, Gap.ShiftForward).Single();
Row("UTV", shifted.InZone("America/New_York", ctx).Single().ToString(), "Gap.ShiftForward");   // 2026-03-08T03:30:00-04:00[America/New_York]  Gap.ShiftForward
// END dst-gap

Section("10. DST overlap");
Title("01:30 on 1 November 2026 in New York");
// BEGIN dst-overlap
var repeated = new DateTime(2026, 11, 1, 1, 30, 0);
Row(".NET", newYork.IsAmbiguousTime(repeated).ToString(), "IsAmbiguousTime", host: true);   // True  IsAmbiguousTime ‡
Row(".NET", TimeZoneInfo.ConvertTimeToUtc(repeated, newYork).ToString("s", inv) + "Z", "picks one silently", host: true);   // 2026-11-01T06:30:00Z  picks one silently ‡

ZonedIntent fallBack = CivilDateTime.Of(2026, 11, 1, 1, 30).InZone("America/New_York");
InstantOutcome twice = fallBack.Resolve(ctx);
switch (twice)
{
    case InstantOutcome.Unique(var at):
        Row("UTV", at.ToString());
        break;
    case InstantOutcome.Multiple(var both):
        Row("UTV", $"{both.Count} instants", $"{string.Join(", ", twice.Diagnostics.Codes)}: clocks go back an hour");   // 2 instants  ambiguous: clocks go back an hour
        foreach (Instant at in both)
        {
            Row("UTV", at.InZone("America/New_York", ctx).Single().ToString());   // 2026-11-01T01:30:00-04:00[America/New_York], then -05:00
        }
        break;
    default:
        Row("UTV", twice.ToString());
        break;
}
Row("UTV", fallBack.Resolve(ctx, Overlap.Earliest).Single().ToString(), "Overlap.Earliest");   // 2026-11-01T05:30:00Z  Overlap.Earliest
// END dst-overlap

Section("11. Timestamps");
Title("Exact fractions and differences");
// BEGIN timestamps
DateTimeOffset a = DateTimeOffset.FromUnixTimeMilliseconds(1_793_885_400_250);
DateTimeOffset b = DateTimeOffset.FromUnixTimeSeconds(1_793_889_001);
Row(".NET", a.ToUnixTimeSeconds().ToString(inv), "ToUnixTimeSeconds drops the .25");   // 1793885400  ToUnixTimeSeconds drops the .25
Row(".NET", (b - a).TotalSeconds.ToString(inv), "a double");        // 3600.75  a double

Instant ua = Instant.FromUnixSeconds(1_793_885_400, 250_000_000);
Instant ub = Instant.FromUnixSeconds(1_793_889_001);
Row("UTV", ua.ToString());                                          // 2026-11-05T13:30:00.25Z
Row("UTV", ua.Until(ub).ToString(), "an exact fraction, 14403/4");  // 3600.75 s (POSIX)  an exact fraction, 14403/4
// END timestamps

Section("12. Durations vs periods");
Title("One day vs 86,400 seconds across DST");
// BEGIN day-vs-24h
var noonEst = new DateTimeOffset(2026, 3, 7, 12, 0, 0, TimeSpan.FromHours(-5));   // noon in New York, the day before DST
DateTimeOffset dayLater = noonEst.AddDays(1);
Row(".NET", dayLater.ToString("yyyy-MM-ddTHH:mmzzz", inv), "AddDays keeps -05:00");   // 2026-03-08T12:00-05:00  AddDays keeps -05:00
Row(".NET", TimeZoneInfo.ConvertTime(dayLater, newYork).ToString("yyyy-MM-ddTHH:mmzzz", inv), "so 13:00 in New York", host: true);   // 2026-03-08T13:00-04:00  so 13:00 in New York ‡

ZonedIntent noon = CivilDateTime.Of(2026, 3, 7, 12, 0).InZone("America/New_York");
ZonedIntent nextNoon = noon.AddDays(1);                             // a calendar day: same wall time, next date
Row("UTV", nextNoon.ToString(), "a calendar day later");            // 2026-03-08T12:00[America/New_York]  a calendar day later
Instant noonInstant = noon.Resolve(ctx).Single();
Row("UTV", noonInstant.Until(nextNoon.Resolve(ctx).Single()).ToString(), "that day lasts 23 hours");   // 82800 s (POSIX)  that day lasts 23 hours
Row("UTV", (noonInstant + PosixSeconds.Of(86_400)).InZone("America/New_York", ctx).Single().ToString(), "86,400 s later");   // 2026-03-08T13:00:00-04:00[America/New_York]  86,400 s later
Row("UTV", noonInstant.Add(ElapsedSeconds.Of(86_400)).ToString(), "SI seconds need leap seconds");   // unsupported (needs-leap-seconds)  SI seconds need leap seconds
// END day-vs-24h

Section("13. Ranges");
Title("November, open-ended and unknown ends");
// BEGIN ranges
DateOnly first = new(2026, 11, 1), end = new(2026, 12, 1), day = new(2026, 11, 15);
Row(".NET", (day >= first && day < end).ToString(), "bounds written by hand");   // True  bounds written by hand

Interval<CivilDate> november = Interval.HalfOpen(CivilDate.Of(2026, 11, 1), CivilDate.Of(2026, 12, 1));
Row("UTV", november.ToString());                                    // [2026-11-01, 2026-12-01)
Row("UTV", november.Contains(CivilDate.Of(2026, 11, 15)).ToString(), "15 Nov");   // Definitely  15 Nov
Row("UTV", november.Contains(CivilDate.Of(2026, 12, 1)).ToString(), "1 Dec");     // Impossible  1 Dec

var fromNovember = Interval.Of(Bound.Included(CivilDate.Of(2026, 11, 1)), Bound.Unbounded);
Row("UTV", fromNovember.ToString(), "no end");                      // [2026-11-01, ..)  no end
Row("UTV", fromNovember.Contains(CivilDate.Of(3000, 1, 1)).ToString(), "1 Jan 3000");   // Definitely  1 Jan 3000

var endNotDecided = Interval.Of(Bound.Included(CivilDate.Of(2026, 11, 1)), Bound.Unknown);
Row("UTV", endNotDecided.ToString(), "an end nobody knows yet");    // [2026-11-01, ?)  an end nobody knows yet
Row("UTV", endNotDecided.Contains(CivilDate.Of(2027, 6, 1)).ToString(), "1 Jun 2027");   // Possibly  1 Jun 2027
// END ranges

Section("14. Round trip");
Title("Native → UTV → native");
// BEGIN round-trip
var lunch = new DateTimeOffset(2026, 11, 5, 14, 30, 0, TimeSpan.FromHours(1));
FixedOffsetDateTime utvLunch = FixedOffsetDateTime.FromDateTimeOffset(lunch);   // the target type names the intent
Row("UTV", utvLunch.ToString(), "from a DateTimeOffset");           // 2026-11-05T14:30:00+01:00  from a DateTimeOffset
DateTimeOffset lunchAgain = utvLunch.ToDateTimeOffset().Single();
Row(".NET", lunchAgain.EqualsExact(lunch).ToString(), "and back: EqualsExact");   // True  and back: EqualsExact

Row("UTV", Instant.FromDateTimeOffset(lunch).ToString(), "as an Instant instead");   // 2026-11-05T13:30:00Z  as an Instant instead
Row("UTV", CivilDate.Of(10_000, 1, 1).ToDateOnly().ToString(), "DateOnly ends at 9999");   // unsupported (out-of-native-range)  DateOnly ends at 9999
// END round-trip

// ---------------------------------------------------------------- printing only

static void Section(string title) => Console.WriteLine(Environment.NewLine + title);

static void Title(string title) => Console.WriteLine("   " + title);

// One aligned output row: who answered, the answer, a note. A wide answer puts its note on the next line.
// Rows that depend on this machine end in ‡.
static void Row(string who, string text, string note = "", bool host = false)
{
    string mark = host ? " ‡" : "";
    string lead = "     " + who.PadRight(7);
    if (text.Length < 25 || note.Length == 0)
    {
        Console.WriteLine((lead + text.PadRight(26) + note).TrimEnd() + mark);
        return;
    }
    Console.WriteLine(lead + text + mark);
    Console.WriteLine(new string(' ', lead.Length + 26) + note + mark);
}
