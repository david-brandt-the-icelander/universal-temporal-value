using System.Numerics;

namespace Utv.Internal.Everyday;

/// <summary>Proleptic Gregorian day arithmetic with arbitrary-precision years (astronomical numbering).</summary>
internal static class CivilMath
{
    public const int SecondsPerDay = 86400;

    /// <summary>Days from 1970-01-01 to the given date (negative before it).</summary>
    public static BigInteger DaysFromCivil(BigInteger year, int month, int day)
    {
        BigInteger y = month <= 2 ? year - 1 : year;
        BigInteger era = MathUtil.FloorDiv(y, 400);
        int yoe = (int)(y - era * 400); // [0, 399]
        int mp = month > 2 ? month - 3 : month + 9; // March = 0
        int doy = (153 * mp + 2) / 5 + day - 1; // [0, 365]
        int doe = yoe * 365 + yoe / 4 - yoe / 100 + doy; // [0, 146096]
        return era * 146097 + doe - 719468;
    }

    /// <summary>The date <paramref name="days"/> days after 1970-01-01.</summary>
    public static (BigInteger Year, int Month, int Day) CivilFromDays(BigInteger days)
    {
        BigInteger z = days + 719468;
        BigInteger era = MathUtil.FloorDiv(z, 146097);
        int doe = (int)(z - era * 146097); // [0, 146096]
        int yoe = (doe - doe / 1460 + doe / 36524 - doe / 146096) / 365; // [0, 399]
        int doy = doe - (365 * yoe + yoe / 4 - yoe / 100); // [0, 365]
        int mp = (5 * doy + 2) / 153; // [0, 11]
        int day = doy - (153 * mp + 2) / 5 + 1;
        int month = mp < 10 ? mp + 3 : mp - 9;
        BigInteger year = yoe + era * 400 + (month <= 2 ? 1 : 0);
        return (year, month, day);
    }

    /// <summary>ISO day of week, Monday = 1 … Sunday = 7.</summary>
    public static int DayOfWeek(BigInteger days) => (int)MathUtil.FloorMod(days + 3, 7) + 1;

    public static int DayOfYear(BigInteger year, int month, int day) =>
        (int)(DaysFromCivil(year, month, day) - DaysFromCivil(year, 1, 1)) + 1;

    /// <summary>ISO 8601 week-numbering year and week of the given date.</summary>
    public static (BigInteger WeekYear, int Week) IsoWeek(BigInteger year, int month, int day)
    {
        BigInteger days = DaysFromCivil(year, month, day);
        BigInteger thursday = days - (DayOfWeek(days) - 4);
        (BigInteger weekYear, int tm, int td) = CivilFromDays(thursday);
        int week = (DayOfYear(weekYear, tm, td) - 1) / 7 + 1;
        return (weekYear, week);
    }

    public static bool IsValidDate(BigInteger year, int month, int day) =>
        month is >= 1 and <= 12 && day >= 1 && day <= GregorianCivil.DaysInMonth(year, month);
}
