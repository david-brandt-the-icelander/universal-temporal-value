using System;
using System.Globalization;
using System.Numerics;
using Utv.Internal;
using static System.FormattableString;

namespace Utv;

/// <summary>
/// Semantic validation of the date fields of <c>civil</c> values in the proleptic Gregorian calendar
/// (<c>calendar.id = "gregory"</c>, <c>calendar.chronology = "proleptic"</c>).
/// </summary>
/// <remarks>
/// This is a semantic check, kept separate from structural validation. It judges only
/// <c>year</c>, <c>month</c> and <c>day</c>; zones, offsets and time-of-day fields are not checked.
/// Years use astronomical numbering (year 0 exists and is a leap year) and arbitrary precision.
/// </remarks>
public static class GregorianCivil
{
    private static readonly int[] MonthDays = { 31, 28, 31, 30, 31, 30, 31, 31, 30, 31, 30, 31 };

    /// <summary>The proleptic Gregorian leap rule.</summary>
    public static bool IsLeapYear(BigInteger year) =>
        (year % 4).IsZero && (!(year % 100).IsZero || (year % 400).IsZero);

    /// <summary>
    /// Days in <paramref name="month"/> (1..12). With <paramref name="year"/> unknown (<c>null</c>),
    /// February has at most 29 days.
    /// </summary>
    public static int DaysInMonth(BigInteger? year, int month)
    {
        if (month is < 1 or > 12)
        {
            throw new ArgumentOutOfRangeException(nameof(month), month, "month must be in 1..12");
        }
        if (month == 2 && (year is null || IsLeapYear(year.Value)))
        {
            return 29;
        }
        return MonthDays[month - 1];
    }

    /// <summary>
    /// Structurally validates <paramref name="json"/>, then checks that its date fields name a real
    /// proleptic Gregorian date.
    /// </summary>
    /// <returns>
    /// The structural result if that is not <see cref="UtvStatus.Valid"/>; otherwise
    /// <see cref="UtvStatus.Unsupported"/> for kinds other than <c>civil</c> and for other calendars or
    /// chronologies; <see cref="UtvStatus.Invalid"/> for an impossible date such as February 30 or
    /// February 29 in a common year; and <see cref="UtvStatus.Valid"/> otherwise.
    /// </returns>
    public static UtvResult Validate(string json)
    {
        UtvResult structural = UtvValidator.ValidateStructural(json, out JObject? document);
        if (structural.Status != UtvStatus.Valid || document is null)
        {
            return structural;
        }

        string kind = ((JString)document.Get("kind")!).Value;
        if (!string.Equals(kind, "civil", StringComparison.Ordinal))
        {
            return UtvResult.Validate(UtvStatus.Unsupported, Diagnostic.Error(
                "unsupported-kind", "/kind", Invariant($"Gregorian date validation is not implemented for kind `{kind}`")));
        }

        var value = (JObject)document.Get("value")!;
        var calendar = (JObject)value.Get("calendar")!;
        string calendarId = ((JString)calendar.Get("id")!).Value;
        string chronology = ((JString)calendar.Get("chronology")!).Value;
        if (!string.Equals(calendarId, "gregory", StringComparison.Ordinal) || !string.Equals(chronology, "proleptic", StringComparison.Ordinal))
        {
            return UtvResult.Validate(UtvStatus.Unsupported, Diagnostic.Error(
                "unsupported-calendar", "/value/calendar",
                Invariant($"calendar `{calendarId}` with chronology `{chronology}` is not implemented; only proleptic `gregory` is")));
        }

        var fields = (JObject)value.Get("fields")!;
        BigInteger? year = fields.Get("year") is JString y
            ? BigInteger.Parse(y.Value, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture)
            : null;
        var month = fields.Get("month") as JNumber;
        var day = fields.Get("day") as JNumber;

        int m = 0;
        if (month is not null && (!month.TryGetInt32(out m) || m is < 1 or > 12))
        {
            return UtvResult.Validate(UtvStatus.Invalid, Diagnostic.Error(
                "month-out-of-range", "/value/fields/month", Invariant($"month {month.Raw} is not in 1..12")));
        }
        if (day is not null)
        {
            int limit = month is null ? 31 : DaysInMonth(year, m);
            if (!day.TryGetInt32(out int d) || d < 1 || d > limit)
            {
                string where = month is null
                    ? ""
                    : year is null
                        ? Invariant($" for month {m}")
                        : Invariant($" for {year.Value.ToString(CultureInfo.InvariantCulture)}-{m.ToString("00", CultureInfo.InvariantCulture)}");
                return UtvResult.Validate(UtvStatus.Invalid, Diagnostic.Error(
                    "day-out-of-range", "/value/fields/day", Invariant($"day {day.Raw} is not in 1..{limit}{where}")));
            }
        }
        return UtvResult.Validate(UtvStatus.Valid);
    }
}
