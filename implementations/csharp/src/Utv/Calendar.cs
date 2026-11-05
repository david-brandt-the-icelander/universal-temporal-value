using System;
using System.Collections.Generic;
using System.Globalization;
using System.Numerics;
using System.Text.Json;
using System.Text.Json.Nodes;
using Utv.Internal.Everyday;
using static System.FormattableString;

namespace Utv;

/// <summary>
/// Everyday calendar operations on UTV values: <see cref="Fields"/>, <see cref="Add"/>, <see cref="Compare"/>,
/// <see cref="DaysBetween"/> and <see cref="PosixSecondsBetween"/>. Proleptic Gregorian calendar, POSIX instants.
/// </summary>
/// <remarks>
/// Nothing here reads the clock, the host time zone or the culture. Semantic outcomes are statuses, never exceptions.
/// If you also import <c>System.Globalization</c>, refer to this class as <c>Utv.Calendar</c>.
/// </remarks>
public static class Calendar
{
    private static readonly HashSet<string> ComponentNames = new(StringComparer.Ordinal)
    {
        "years", "months", "weeks", "days", "hours", "minutes", "seconds",
    };

    /// <summary>
    /// Derived fields of a Gregorian <c>date</c> or <c>datetime</c>: <c>dayOfWeek</c> (Monday = 1),
    /// <c>dayOfYear</c>, <c>isoWeekYear</c> (a decimal string), <c>isoWeek</c>, <c>daysInMonth</c> and <c>isLeapYear</c>.
    /// </summary>
    /// <returns><c>unique</c> with a JSON object; <c>invalid</c> for a date that does not exist; <c>unsupported</c> for other forms and calendars.</returns>
    public static UtvResult Fields(JsonElement value) => Op.Run("fields", () =>
    {
        CivilRead c = RequireCivil(value, "value");
        if (c.Form is not ("date" or "datetime"))
        {
            throw OpFailure.Unsupported("unsupported-form", "/value/form", Invariant($"`fields` applies to `date` and `datetime`, not `{c.Form}`"));
        }
        Values.RequireGregorian(c);
        (BigInteger y, int m, int d) = Values.RequireDate(c);
        if (c.Form == "datetime")
        {
            Values.RequireTime(c);
        }
        BigInteger days = CivilMath.DaysFromCivil(y, m, d);
        (BigInteger weekYear, int week) = CivilMath.IsoWeek(y, m, d);
        return Op.Unique("fields", new JsonObject
        {
            ["dayOfWeek"] = CivilMath.DayOfWeek(days),
            ["dayOfYear"] = CivilMath.DayOfYear(y, m, d),
            ["isoWeekYear"] = Values.FormatYear(weekYear),
            ["isoWeek"] = week,
            ["daysInMonth"] = GregorianCivil.DaysInMonth(y, m),
            ["isLeapYear"] = GregorianCivil.IsLeapYear(y),
        });
    });

    /// <summary>
    /// Adds an amount to a value. Civil values take a calendar period (<see cref="Build.Period"/>): years and months
    /// first, then the month-end rule, then weeks and days, then hours, minutes and seconds as wall-clock arithmetic.
    /// A zone on the value is kept: the result is still a civil intention. Instants take only POSIX seconds
    /// (<see cref="Build.PosixSeconds(long)"/>), exactly.
    /// </summary>
    /// <param name="value">A Gregorian <c>date</c>, <c>year-month</c> or <c>datetime</c>, or a POSIX instant.</param>
    /// <param name="amount">The amount to add; every component may be negative.</param>
    /// <param name="monthEnd">What to do when the day does not exist in the resulting month.</param>
    /// <returns>
    /// <c>unique</c>; <c>none</c> (diagnostic <c>month-end</c>) when the day does not exist and the policy is
    /// <see cref="MonthEndPolicy.Reject"/>; <c>invalid</c> for an invalid input date, a component the form does not
    /// take (hours on a <c>date</c>) or a calendar amount on an instant; <c>unsupported</c> for <c>time</c> values
    /// and SI (<c>elapsed</c>) seconds on a POSIX instant, which need a leap-second table.
    /// </returns>
    public static UtvResult Add(JsonElement value, JsonElement amount, MonthEndPolicy monthEnd = MonthEndPolicy.Reject) => Op.Run("add", () =>
    {
        Values.RequireStructural(value, "value");
        Values.RequireStructural(amount, "amount");
        if (Values.KindOf(amount) != "amount")
        {
            throw OpFailure.Invalid("not-an-amount", "/kind", Invariant($"the second argument of `add` must be an `amount`, not `{Values.KindOf(amount)}`"));
        }
        JsonElement a = amount.GetProperty("value");
        string basis = a.GetProperty("basis").GetString()!;
        return Values.KindOf(value) switch
        {
            "instant" => AddToInstant(value, a, basis),
            "civil" => AddToCivil(Values.ReadCivil(value), a, basis, monthEnd),
            string kind => throw OpFailure.Unsupported("unsupported-kind", "/kind", Invariant($"`add` is not implemented for kind `{kind}`")),
        };
    });

    /// <summary>
    /// Orders <paramref name="a"/> relative to <paramref name="b"/>: <c>"before"</c>, <c>"equal"</c> or <c>"after"</c>.
    /// </summary>
    /// <returns>
    /// <c>unique</c> with the answer for two unzoned civil values of the same form, two POSIX instants, or two
    /// fixed-offset date-times (compared as instants). <c>unsupported</c> for anything with a named zone (resolve it
    /// first: the order depends on zone rules) and for different kinds or forms.
    /// </returns>
    public static UtvResult Compare(JsonElement a, JsonElement b) => Op.Run("compare", () =>
    {
        Values.RequireStructural(a, "a");
        Values.RequireStructural(b, "b");
        string ka = Values.KindOf(a);
        string kb = Values.KindOf(b);
        int order;
        if (ka == "instant" && kb == "instant")
        {
            order = Values.ReadPosixInstant(a, "a").CompareTo(Values.ReadPosixInstant(b, "b"));
        }
        else if (ka == "civil" && kb == "civil")
        {
            order = CompareCivil(Values.ReadCivil(a), Values.ReadCivil(b));
        }
        else
        {
            throw OpFailure.Unsupported("incomparable", "", Invariant($"cannot compare `{ka}` with `{kb}`"));
        }
        return Op.Unique("compare", order < 0 ? "before" : order > 0 ? "after" : "equal");
    });

    /// <summary>The number of days from Gregorian date <paramref name="a"/> to <paramref name="b"/> (b − a).</summary>
    /// <returns><c>unique</c> with a JSON integer; <c>invalid</c> for a date that does not exist; <c>unsupported</c> for anything but two Gregorian <c>date</c> values.</returns>
    public static UtvResult DaysBetween(JsonElement a, JsonElement b) => Op.Run("daysBetween", () =>
    {
        var zones = new ZoneAgreement();
        BigInteger da = DayNumber(a, "a", zones);
        BigInteger db = DayNumber(b, "b", zones);
        return new UtvResult("daysBetween", UtvStatus.Unique, null, new[] { Values.ToElement(db - da) });
    });

    /// <summary>The POSIX seconds from instant <paramref name="a"/> to <paramref name="b"/> (b − a), exactly, as a <see cref="Build.PosixSeconds(long)"/> amount.</summary>
    /// <returns><c>unique</c>; <c>unsupported</c> for anything but two POSIX instants.</returns>
    public static UtvResult PosixSecondsBetween(JsonElement a, JsonElement b) => Op.Run("posixSecondsBetween", () =>
    {
        Rational ta = RequireInstant(a, "a");
        Rational tb = RequireInstant(b, "b");
        return Op.Unique("posixSecondsBetween", Values.PosixSeconds(tb - ta));
    });

    // ------------------------------------------------------------------ helpers

    internal static CivilRead RequireCivil(JsonElement value, string argument)
    {
        Values.RequireStructural(value, argument);
        string kind = Values.KindOf(value);
        if (kind != "civil")
        {
            throw OpFailure.Unsupported("unsupported-kind", "/kind", Invariant($"{argument}: expected a `civil` value, not `{kind}`"));
        }
        return Values.ReadCivil(value);
    }

    internal static Rational RequireInstant(JsonElement value, string argument)
    {
        Values.RequireStructural(value, argument);
        string kind = Values.KindOf(value);
        if (kind != "instant")
        {
            throw OpFailure.Unsupported("unsupported-kind", "/kind", Invariant($"{argument}: expected an `instant`, not `{kind}`"));
        }
        return Values.ReadPosixInstant(value, argument);
    }

    /// <summary>A Gregorian date as a day number; its zone must agree with the other dates of the operation.</summary>
    internal static BigInteger DayNumber(JsonElement value, string argument, ZoneAgreement zones)
    {
        CivilRead c = RequireCivil(value, argument);
        if (c.Form != "date")
        {
            throw OpFailure.Unsupported("unsupported-form", "/value/form", Invariant($"{argument}: expected a `date`, not `{c.Form}`"));
        }
        Values.RequireGregorian(c);
        zones.Add(c, argument);
        (BigInteger y, int m, int d) = Values.RequireDate(c);
        return CivilMath.DaysFromCivil(y, m, d);
    }

    /// <summary>Wall time of a Gregorian <c>datetime</c> read as if it were UTC, in exact seconds.</summary>
    internal static Rational WallSeconds(CivilRead c)
    {
        (BigInteger y, int m, int d) = Values.RequireDate(c);
        (int h, int mi, int s, int nano) = Values.RequireTime(c);
        BigInteger seconds = CivilMath.DaysFromCivil(y, m, d) * CivilMath.SecondsPerDay + h * 3600 + mi * 60 + s;
        return Rational.FromSecondsAndNano(seconds, nano);
    }

    private static int CompareCivil(CivilRead a, CivilRead b)
    {
        if (a.Zone is ZoneKind.Iana or ZoneKind.Other || b.Zone is ZoneKind.Iana or ZoneKind.Other)
        {
            throw OpFailure.Unsupported("resolve-first", "/value/zone",
                "ordering values in a named zone depends on zone rules; `resolve` them to instants first");
        }
        if (a.Zone != b.Zone)
        {
            throw OpFailure.Unsupported("incomparable", "/value/zone", "cannot compare a fixed-offset value with an unzoned one");
        }
        if (a.Form != b.Form)
        {
            throw OpFailure.Unsupported("incomparable", "/value/form", Invariant($"cannot compare a `{a.Form}` with a `{b.Form}`"));
        }
        Values.RequireGregorian(a);
        Values.RequireGregorian(b);
        if (a.Zone == ZoneKind.FixedOffset)
        {
            if (a.Form != "datetime")
            {
                throw OpFailure.Unsupported("unsupported-form", "/value/form", Invariant($"cannot compare fixed-offset `{a.Form}` values"));
            }
            Rational ta = WallSeconds(a) - Rational.FromInteger(a.ZoneSeconds);
            Rational tb = WallSeconds(b) - Rational.FromInteger(b.ZoneSeconds);
            return ta.CompareTo(tb);
        }
        return a.Form switch
        {
            "date" => CompareDates(Values.RequireDate(a), Values.RequireDate(b)),
            "year-month" => CompareYearMonths(Values.RequireYearMonth(a), Values.RequireYearMonth(b)),
            "time" => Values.RequireTime(a).CompareTo(Values.RequireTime(b)),
            "datetime" => WallSeconds(a).CompareTo(WallSeconds(b)),
            _ => throw OpFailure.Unsupported("unsupported-form", "/value/form", Invariant($"cannot compare `{a.Form}` values")),
        };
    }

    private static int CompareDates((BigInteger Y, int M, int D) a, (BigInteger Y, int M, int D) b)
    {
        int c = a.Y.CompareTo(b.Y);
        return c != 0 ? c : (a.M, a.D).CompareTo((b.M, b.D));
    }

    private static int CompareYearMonths((BigInteger Y, int M) a, (BigInteger Y, int M) b)
    {
        int c = a.Y.CompareTo(b.Y);
        return c != 0 ? c : a.M.CompareTo(b.M);
    }

    private static UtvResult AddToInstant(JsonElement instant, JsonElement amount, string basis)
    {
        Rational t = Values.ReadPosixInstant(instant, "value");
        switch (basis)
        {
            case "clock-ticks":
                if (!amount.TryGetProperty("rules", out JsonElement rules) || !Values.IsPosix(rules))
                {
                    throw OpFailure.Unsupported("unsupported-timescale", "/value/rules", "only POSIX clock-tick amounts can be added to a POSIX instant");
                }
                if (!amount.TryGetProperty("seconds", out JsonElement seconds))
                {
                    throw OpFailure.Invalid("missing-property", "/value/seconds", "a clock-tick amount needs `seconds`");
                }
                return Op.Unique("add", Values.Instant(t + Values.ReadRational(seconds)));
            case "elapsed":
                throw OpFailure.Unsupported("needs-leap-seconds", "/value/basis",
                    "adding SI (elapsed) seconds to a POSIX instant needs a leap-second table, which this library does not ship; use POSIX seconds");
            case "calendar":
                throw OpFailure.Invalid("calendar-on-instant", "/value/basis",
                    "a calendar amount has no meaning on an instant; convert to a civil date-time in a zone first");
            default:
                throw OpFailure.Unsupported("unsupported-amount", "/value/basis", Invariant($"`{basis}` amounts are not implemented"));
        }
    }

    private static UtvResult AddToCivil(CivilRead c, JsonElement amount, string basis, MonthEndPolicy monthEnd)
    {
        if (basis != "calendar")
        {
            throw OpFailure.Invalid("wrong-amount", "/value/basis",
                Invariant($"a `{basis}` amount cannot be added to a civil value; use a calendar period"));
        }
        Dictionary<string, BigInteger> parts = ReadComponents(amount);
        BigInteger P(string name) => parts.TryGetValue(name, out BigInteger v) ? v : BigInteger.Zero;
        bool hasTime = !P("hours").IsZero || !P("minutes").IsZero || !P("seconds").IsZero;
        bool hasDays = !P("weeks").IsZero || !P("days").IsZero;

        if (c.Form == "time")
        {
            throw OpFailure.Unsupported("time-wraps", "/value/form", "adding to a time of day wraps past midnight and loses the day; use a `datetime`");
        }
        if (c.Form is not ("date" or "year-month" or "datetime"))
        {
            throw OpFailure.Unsupported("unsupported-form", "/value/form", Invariant($"`add` is not implemented for `{c.Form}`"));
        }
        Values.RequireGregorian(c);

        BigInteger totalMonths = 12 * P("years") + P("months");
        if (c.Form == "year-month")
        {
            (BigInteger y0, int m0) = Values.RequireYearMonth(c);
            if (hasTime || hasDays)
            {
                throw OpFailure.Invalid("wrong-component", "/value/components", "a `year-month` accepts only years and months");
            }
            (BigInteger ny, int nm) = AddMonths(y0, m0, totalMonths);
            return Op.Unique("add", WithFields(c, new JsonObject { ["year"] = Values.FormatYear(ny), ["month"] = nm }, out var notes), notes);
        }

        (BigInteger y, int m, int d) = Values.RequireDate(c);
        (int h, int mi, int s, int nano) = (0, 0, 0, 0);
        if (c.Form == "datetime")
        {
            (h, mi, s, nano) = Values.RequireTime(c);
        }
        else if (hasTime)
        {
            throw OpFailure.Invalid("wrong-component", "/value/components", "a `date` does not accept hours, minutes or seconds; use a `datetime`");
        }

        (BigInteger year, int month) = AddMonths(y, m, totalMonths);
        int limit = GregorianCivil.DaysInMonth(year, month);
        int day = d;
        if (d > limit)
        {
            if (monthEnd == MonthEndPolicy.Reject)
            {
                return new UtvResult("add", UtvStatus.None, new[]
                {
                    new Diagnostic("month-end", DiagnosticSeverity.Warning, "/value/fields/day",
                        Invariant($"day {d} does not exist in {Values.FormatYear(year)}-{month:00}; pass MonthEndPolicy.Constrain to clamp to day {limit}")),
                });
            }
            day = limit;
        }

        BigInteger days = CivilMath.DaysFromCivil(year, month, day) + 7 * P("weeks") + P("days");
        if (c.Form == "date")
        {
            (BigInteger ry, int rm, int rd) = CivilMath.CivilFromDays(days);
            return Op.Unique("add", WithFields(c, Values.DateFields(ry, rm, rd), out var notes), notes);
        }

        BigInteger secondOfDay = h * 3600 + mi * 60 + s + 3600 * P("hours") + 60 * P("minutes") + P("seconds");
        days += MathUtil.FloorDiv(secondOfDay, CivilMath.SecondsPerDay);
        int sod = (int)MathUtil.FloorMod(secondOfDay, CivilMath.SecondsPerDay);
        (BigInteger yy, int mm, int dd) = CivilMath.CivilFromDays(days);
        int second = sod % 60;
        JsonObject fields = Values.AddTime(Values.DateFields(yy, mm, dd), sod / 3600, sod / 60 % 60,
            c.Second is not null || second != 0 ? second : null, c.Nano);
        return Op.Unique("add", WithFields(c, fields, out var dtNotes), dtNotes);
    }

    private static (BigInteger Year, int Month) AddMonths(BigInteger year, int month, BigInteger months)
    {
        BigInteger index = year * 12 + (month - 1) + months;
        return (MathUtil.FloorDiv(index, 12), (int)MathUtil.FloorMod(index, 12) + 1);
    }

    private static Dictionary<string, BigInteger> ReadComponents(JsonElement amount)
    {
        var parts = new Dictionary<string, BigInteger>(StringComparer.Ordinal);
        if (!amount.TryGetProperty("components", out JsonElement components))
        {
            throw OpFailure.Invalid("missing-property", "/value/components", "a calendar amount needs `components`");
        }
        foreach (JsonProperty p in components.EnumerateObject())
        {
            string path = "/value/components/" + p.Name;
            if (!ComponentNames.Contains(p.Name))
            {
                throw OpFailure.Unsupported("unsupported-component", path, Invariant($"component `{p.Name}` is not implemented"));
            }
            string text = p.Value.ValueKind switch
            {
                JsonValueKind.String => p.Value.GetString()!,
                JsonValueKind.Number => p.Value.GetRawText(),
                _ => throw OpFailure.Invalid("wrong-type", path, "a component is a decimal string"),
            };
            if (!BigInteger.TryParse(text, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out BigInteger n))
            {
                throw decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out _)
                    ? OpFailure.Unsupported("fractional-component", path, Invariant($"fractional component `{text}` is not implemented"))
                    : OpFailure.Invalid("wrong-type", path, Invariant($"`{text}` is not a decimal integer"));
            }
            parts[p.Name] = n;
        }
        return parts;
    }

    /// <summary>The input value with new fields. A zone is kept; an <c>offsetAssertion</c> is dropped, since it described the old wall time.</summary>
    private static JsonObject WithFields(CivilRead c, JsonObject fields, out Diagnostic[] notes)
    {
        JsonObject copy = Values.Mutable(c.Envelope);
        copy.Remove("provenance");
        JsonObject value = copy["value"]!.AsObject();
        value["fields"] = fields;
        notes = Array.Empty<Diagnostic>();
        if (value.Remove("offsetAssertion"))
        {
            notes = new[]
            {
                new Diagnostic("offset-assertion-dropped", DiagnosticSeverity.Info, "/value/offsetAssertion",
                    "the asserted offset described the original wall time, so it was not carried over; the zone was"),
            };
        }
        return copy;
    }
}

/// <summary>
/// Dates in one <c>daysBetween</c>, <c>contains</c> or <c>overlaps</c> must be all unzoned or all in the same zone
/// (everyday-api.md, Clarifications 4).
/// </summary>
internal sealed class ZoneAgreement
{
    private string? _first;

    public void Add(CivilRead c, string argument)
    {
        string key = c.Zone switch
        {
            ZoneKind.Floating => "unzoned",
            ZoneKind.Iana => "zone " + c.ZoneId,
            ZoneKind.FixedOffset => "offset " + c.ZoneSeconds.ToString(CultureInfo.InvariantCulture),
            _ => "zone type " + c.ZoneType,
        };
        _first ??= key;
        if (_first != key)
        {
            throw OpFailure.Unsupported("mixed-zones", "/value/zone",
                Invariant($"{argument}: dates must be all unzoned or all in the same zone; found {_first} and {key}"));
        }
    }
}
