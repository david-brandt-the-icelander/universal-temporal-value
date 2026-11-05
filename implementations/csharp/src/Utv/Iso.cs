using System;
using System.Globalization;
using System.Numerics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Utv.Internal.Everyday;
using static System.FormattableString;

namespace Utv;

/// <summary>
/// ISO 8601 extended format and RFC 3339 / RFC 9557 text: <see cref="ParseIso"/> and its inverse <see cref="FormatIso"/>.
/// </summary>
public static partial class Iso
{
    private const string ParseOp = "parseIso";
    private const string FormatOp = "formatIso";

    // [0-9] rather than \d (which matches any Unicode digit); \z rather than $ (which matches before a final newline).
    private const string Year = @"(?<y>[+-][0-9]{4,}|[0-9]{4})";
    private const string Time = @"(?<h>[0-9]{2}):(?<mi>[0-9]{2})(?::(?<s>[0-9]{2})(?:(?<sep>[.,])(?<f>[0-9]+))?)?";

    [GeneratedRegex(@"\A" + Year + @"-(?<m>[0-9]{2})\z", RegexOptions.CultureInvariant)]
    private static partial Regex YearMonthPattern();

    [GeneratedRegex(@"\A" + Year + @"-(?<m>[0-9]{2})-(?<d>[0-9]{2})\z", RegexOptions.CultureInvariant)]
    private static partial Regex DatePattern();

    [GeneratedRegex(@"\A" + Time + @"\z", RegexOptions.CultureInvariant)]
    private static partial Regex TimePattern();

    [GeneratedRegex(@"\A" + Year + @"-(?<m>[0-9]{2})-(?<d>[0-9]{2})T" + Time
        + @"(?<off>Z|[+-][0-9]{2}(?::?[0-9]{2})?)?(?<ann>(?:\[[^\[\]]*\])*)\z", RegexOptions.CultureInvariant)]
    private static partial Regex DateTimePattern();

    // Valid ISO 8601 forms outside this profile.
    [GeneratedRegex(@"\A(?:[+-][0-9]{4,}|[0-9]{4})-?W(?<w>[0-9]{2})(?:-?(?<wd>[0-9]))?(?:T.*)?\z", RegexOptions.CultureInvariant | RegexOptions.Singleline)]
    private static partial Regex WeekDatePattern();

    [GeneratedRegex(@"\A(?<y>[+-][0-9]{4,}|[0-9]{4})-?(?<o>[0-9]{3})(?:T.*)?\z", RegexOptions.CultureInvariant | RegexOptions.Singleline)]
    private static partial Regex OrdinalDatePattern();

    [GeneratedRegex(@"\A(?<y>[0-9]{4})(?<m>[0-9]{2})(?<d>[0-9]{2})(?:T[0-9]{2}(?:[0-9]{2}(?:[0-9]{2}(?:[.,][0-9]+)?)?)?(?:Z|[+-][0-9]{2}(?:[0-9]{2})?)?)?\z", RegexOptions.CultureInvariant)]
    private static partial Regex BasicDatePattern();

    [GeneratedRegex(@"\A(?:[+-][0-9]{4,}|[0-9]{4})\z", RegexOptions.CultureInvariant)]
    private static partial Regex YearOnlyPattern();

    [GeneratedRegex(@"\AT?" + Time + @"(?:Z|[+-][0-9]{2}(?::?[0-9]{2})?)?\z", RegexOptions.CultureInvariant)]
    private static partial Regex TimeWithDesignatorOrOffsetPattern();

    // An IANA time zone name: one or more parts separated by '/'.
    [GeneratedRegex(@"\A[A-Za-z_][A-Za-z0-9_+\-.]*(?:/[A-Za-z0-9_+\-.]+)*\z", RegexOptions.CultureInvariant)]
    private static partial Regex ZoneNamePattern();

    [GeneratedRegex(@"\A[+-][0-9]{2}(?::?[0-9]{2})?\z", RegexOptions.CultureInvariant)]
    private static partial Regex OffsetAnnotationPattern();

    [GeneratedRegex(@"\[([^\[\]]*)\]", RegexOptions.CultureInvariant)]
    private static partial Regex AnnotationPattern();

    /// <summary>
    /// Parses ISO 8601 extended format and RFC 3339 / RFC 9557 text into a UTV value.
    /// </summary>
    /// <returns>
    /// <c>unique</c> with the value; <c>invalid</c> for text outside the grammar or out-of-range fields
    /// (30 February, minute 60, an offset beyond ±18:00); <c>unsupported</c> for valid ISO/RFC forms this
    /// profile does not handle (second 60, <c>24:00</c>, week and ordinal dates, basic format, <c>Z[zone]</c>,
    /// critical annotations).
    /// </returns>
    public static UtvResult ParseIso(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        return Op.Run(ParseOp, () => Op.Unique(ParseOp, Parse(text)));
    }

    /// <summary>
    /// Formats a UTV value as ISO 8601 / RFC 3339 / RFC 9557 text: the inverse of <see cref="ParseIso"/>.
    /// </summary>
    /// <returns>
    /// <c>unique</c> with the text as a JSON string (read it with <see cref="UtvResult.GetString"/>);
    /// <c>invalid</c> for a value that names no real date or time; <c>unsupported</c> for anything with no
    /// ISO text in this profile (amounts, intervals, other calendars, fractions finer than a nanosecond).
    /// </returns>
    public static UtvResult FormatIso(JsonElement value) =>
        Op.Run(FormatOp, () =>
        {
            Values.RequireStructural(value, "value");
            return Op.Unique(FormatOp, Format(value));
        });

    // ------------------------------------------------------------------ parsing

    private static JsonNode Parse(string text)
    {
        Match m;
        if ((m = DatePattern().Match(text)).Success)
        {
            (BigInteger y, int mo, int d) = ParseDate(m);
            return Values.Civil("date", Values.DateFields(y, mo, d));
        }
        if ((m = YearMonthPattern().Match(text)).Success)
        {
            BigInteger y = ParseYear(m.Groups["y"].Value);
            int mo = ParseMonth(m);
            return Values.Civil("year-month", new JsonObject { ["year"] = Values.FormatYear(y), ["month"] = mo });
        }
        if ((m = TimePattern().Match(text)).Success)
        {
            (int h, int mi, int? s, int? nano) = ParseTime(m);
            CheckTimeSupported(h, s);
            return Values.Civil("time",Values.AddTime(new JsonObject(), h, mi, s, nano));
        }
        if ((m = DateTimePattern().Match(text)).Success)
        {
            return ParseDateTime(m);
        }
        throw Unrecognized(text);
    }

    private static JsonNode ParseDateTime(Match m)
    {
        // Every `invalid` check comes before every `unsupported` one.
        (BigInteger y, int mo, int d) = ParseDate(m);
        (int h, int mi, int? s, int? nano) = ParseTime(m);

        string off = m.Groups["off"].Value;
        bool utc = off == "Z" || off == "-00:00";
        int? offsetSeconds = null;
        if (off.Length > 0 && off != "Z")
        {
            if (off.Length != 6 || off[3] != ':')
            {
                throw OpFailure.Unsupported("basic-format", "", Invariant($"offset `{off}` is not in extended `±hh:mm` form"));
            }
            int oh = Int(off.AsSpan(1, 2));
            int om = Int(off.AsSpan(4, 2));
            if (om > 59)
            {
                throw OpFailure.Invalid("offset-out-of-range", "", Invariant($"offset minutes {om} are not in 0..59"));
            }
            int total = (oh * 3600 + om * 60) * (off[0] == '-' ? -1 : 1);
            if (Math.Abs(total) > Values.MaxOffsetSeconds)
            {
                throw OpFailure.Invalid("offset-out-of-range", "", Invariant($"offset `{off}` is beyond ±18:00"));
            }
            offsetSeconds = total;
        }

        string? zone = null;
        bool unsupportedAnnotation = false;
        foreach (Match a in AnnotationPattern().Matches(m.Groups["ann"].Value))
        {
            string body = a.Groups[1].Value;
            if (body.StartsWith('!') || body.Contains('=', StringComparison.Ordinal) || OffsetAnnotationPattern().IsMatch(body))
            {
                unsupportedAnnotation = true;
                continue;
            }
            if (zone is not null || !ZoneNamePattern().IsMatch(body))
            {
                // Only a single [Area/Location] annotation is handled (everyday-api.md, Clarifications 3).
                unsupportedAnnotation = true;
                continue;
            }
            zone = body;
        }

        CheckTimeSupported(h, s);
        if (unsupportedAnnotation)
        {
            throw OpFailure.Unsupported("unsupported-annotation", "",
                "only a single `[Area/Location]` annotation is handled; critical (`[!…]`), key-value (`[u-ca=…]`), offset and further annotations are not");
        }
        if (utc && zone is not null)
        {
            throw OpFailure.Unsupported("utc-with-zone", "",
                Invariant($"`{off}` followed by `[{zone}]` is not handled by this profile"));
        }

        if (utc)
        {
            // RFC 3339 / 9557: UTC is known, the local offset is not; nothing civil is kept.
            BigInteger seconds = CivilMath.DaysFromCivil(y, mo, d) * CivilMath.SecondsPerDay + h * 3600 + mi * 60 + (s ?? 0);
            return Values.Instant(Rational.FromSecondsAndNano(seconds, nano ?? 0));
        }

        JsonObject node = Values.Civil("datetime", Values.AddTime(Values.DateFields(y, mo, d), h, mi, s, nano));
        JsonObject value = node["value"]!.AsObject();
        if (zone is not null)
        {
            value["zone"] = new JsonObject { ["type"] = "iana", ["id"] = zone };
            value["zoneIntent"] = "follow-zone-id";
            if (offsetSeconds is not null)
            {
                value["offsetAssertion"] = offsetSeconds.Value;
            }
        }
        else if (offsetSeconds is not null)
        {
            value["zone"] = new JsonObject { ["type"] = "fixed-offset", ["seconds"] = offsetSeconds.Value };
            value["zoneIntent"] = "fixed-offset";
        }
        return node;
    }

    private static (BigInteger, int, int) ParseDate(Match m)
    {
        BigInteger y = ParseYear(m.Groups["y"].Value);
        int mo = ParseMonth(m);
        int d = Int(m.Groups["d"].ValueSpan);
        int limit = GregorianCivil.DaysInMonth(y, mo);
        if (d < 1 || d > limit)
        {
            throw OpFailure.Invalid("day-out-of-range", "", Invariant($"day {d} is not in 1..{limit} for {Values.FormatYear(y)}-{mo:00}"));
        }
        return (y, mo, d);
    }

    private static int ParseMonth(Match m)
    {
        int mo = Int(m.Groups["m"].ValueSpan);
        if (mo is < 1 or > 12)
        {
            throw OpFailure.Invalid("month-out-of-range", "", Invariant($"month {mo} is not in 1..12"));
        }
        return mo;
    }

    /// <summary>Range-checks the time; 24:00 and second 60 pass here and are reported by <see cref="CheckTimeSupported"/>.</summary>
    private static (int, int, int?, int?) ParseTime(Match m)
    {
        int h = Int(m.Groups["h"].ValueSpan);
        int mi = Int(m.Groups["mi"].ValueSpan);
        int? s = m.Groups["s"].Success ? Int(m.Groups["s"].ValueSpan) : null;
        string f = m.Groups["f"].Value;
        if (mi > 59)
        {
            throw OpFailure.Invalid("minute-out-of-range", "", Invariant($"minute {mi} is not in 0..59"));
        }
        if (s > 60)
        {
            throw OpFailure.Invalid("second-out-of-range", "", Invariant($"second {s} is not in 0..60"));
        }
        if (h > 24 || (h == 24 && (mi != 0 || s is > 0 || f.Trim('0').Length > 0)))
        {
            throw OpFailure.Invalid("hour-out-of-range", "", Invariant($"hour {h} is not in 0..23 (24 only as 24:00)"));
        }
        if (m.Groups["sep"].Value == ",")
        {
            CheckTimeSupported(h, s);
            throw OpFailure.Unsupported("comma-fraction", "", "a decimal comma is valid ISO 8601 but this profile reads only `.`");
        }
        if (f.Length > 9)
        {
            CheckTimeSupported(h, s);
            throw OpFailure.Unsupported("sub-nanosecond", "", Invariant($"a {f.Length}-digit fraction is finer than a nanosecond"));
        }
        int? nano = f.Length == 0 ? null : Int(f.PadRight(9, '0'));
        return (h, mi, s, nano);
    }

    private static void CheckTimeSupported(int h, int? s)
    {
        if (h == 24)
        {
            throw OpFailure.Unsupported("end-of-day", "", "24:00 needs an end-of-day policy, which this profile does not have");
        }
        if (s == 60)
        {
            throw OpFailure.Unsupported("leap-second", "", "second 60 needs a leap-second table, which this library does not ship");
        }
    }

    private static OpFailure Unrecognized(string text)
    {
        if (WeekDatePattern().Match(text) is { Success: true } w)
        {
            int week = Int(w.Groups["w"].ValueSpan);
            bool dayOk = !w.Groups["wd"].Success || Int(w.Groups["wd"].ValueSpan) is >= 1 and <= 7;
            return week is >= 1 and <= 53 && dayOk
                ? OpFailure.Unsupported("week-date", "", "ISO week dates are valid ISO 8601 but not handled by this profile")
                : OpFailure.Invalid("not-iso", "", Invariant($"`{text}` is not a valid week date"));
        }
        if (OrdinalDatePattern().Match(text) is { Success: true } o)
        {
            int ordinal = Int(o.Groups["o"].ValueSpan);
            int days = GregorianCivil.IsLeapYear(ParseYear(o.Groups["y"].Value)) ? 366 : 365;
            return ordinal >= 1 && ordinal <= days
                ? OpFailure.Unsupported("ordinal-date", "", "ISO ordinal dates are valid ISO 8601 but not handled by this profile")
                : OpFailure.Invalid("not-iso", "", Invariant($"`{text}` is not a valid ordinal date"));
        }
        if (BasicDatePattern().Match(text) is { Success: true } b)
        {
            int mo = Int(b.Groups["m"].ValueSpan);
            int d = Int(b.Groups["d"].ValueSpan);
            return CivilMath.IsValidDate(ParseYear(b.Groups["y"].Value), mo, d)
                ? OpFailure.Unsupported("basic-format", "", "ISO 8601 basic format is valid but this profile reads only extended format")
                : OpFailure.Invalid("not-iso", "", Invariant($"`{text}` is not a valid basic-format date"));
        }
        if (YearOnlyPattern().IsMatch(text))
        {
            return OpFailure.Unsupported("reduced-precision", "", "a year on its own is valid ISO 8601 but not handled by this profile");
        }
        if (TimeWithDesignatorOrOffsetPattern().Match(text) is { Success: true } t)
        {
            // Range-check first so that `T25:00` stays invalid.
            try
            {
                ParseTime(t);
            }
            catch (OpFailure failure) when (failure.Status == UtvStatus.Invalid)
            {
                return failure;
            }
            catch (OpFailure)
            {
                // Unsupported for another reason; report the form below.
            }
            return OpFailure.Unsupported("time-with-offset", "",
                "a time of day with `T` or with an offset is valid ISO 8601 but not handled by this profile");
        }
        return OpFailure.Invalid("not-iso", "", Invariant($"`{text}` is not ISO 8601 extended format or RFC 3339 / RFC 9557"));
    }

    private static BigInteger ParseYear(string text)
    {
        // A sign is followed by four or more digits; `-0000` is year 0.
        return BigInteger.Parse(text, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture);
    }

    private static int Int(ReadOnlySpan<char> digits) => int.Parse(digits, NumberStyles.None, CultureInfo.InvariantCulture);

    // ------------------------------------------------------------------ formatting

    private static string Format(JsonElement value)
    {
        string kind = Values.KindOf(value);
        if (kind == "instant")
        {
            Rational t = Values.ReadPosixInstant(value, "value");
            JsonObject f = Values.WallFields(t, 0, "/value/subTick");
            var sb = new StringBuilder();
            AppendDate(sb, BigInteger.Parse((string)f["year"]!, CultureInfo.InvariantCulture), (int)f["month"]!, (int)f["day"]!);
            sb.Append('T');
            AppendTime(sb, (int)f["hour"]!, (int)f["minute"]!, (int)f["second"]!, f["nano"] is JsonNode n ? (int)n : 0);
            return sb.Append('Z').ToString();
        }
        if (kind != "civil")
        {
            throw OpFailure.Unsupported("unsupported-kind", "/kind", Invariant($"kind `{kind}` has no ISO 8601 text in this profile"));
        }

        CivilRead c = Values.ReadCivil(value);
        Values.RequireGregorian(c);
        var text = new StringBuilder();
        switch (c.Form)
        {
            case "date":
            {
                (BigInteger y, int m, int d) = Values.RequireDate(c);
                AppendDate(text, y, m, d);
                break;
            }
            case "year-month":
            {
                (BigInteger y, int m) = Values.RequireYearMonth(c);
                AppendYear(text, y);
                text.Append('-').Append(Two(m));
                break;
            }
            case "time":
            {
                (int h, int mi, int s, int nano) = Values.RequireTime(c);
                AppendTime(text, h, mi, c.Second is null ? null : s, nano);
                break;
            }
            case "datetime":
            {
                (BigInteger y, int m, int d) = Values.RequireDate(c);
                (int h, int mi, int s, int nano) = Values.RequireTime(c);
                AppendDate(text, y, m, d);
                text.Append('T');
                AppendTime(text, h, mi, c.Second is null ? null : s, nano);
                break;
            }
            default:
                throw OpFailure.Unsupported("unsupported-form", "/value/form", Invariant($"form `{c.Form}` has no ISO 8601 text in this profile"));
        }

        if (c.Form is "date" or "year-month" or "time")
        {
            if (c.Zone != ZoneKind.Floating)
            {
                throw OpFailure.Unsupported("unsupported-zone", "/value/zone", Invariant($"a zoned `{c.Form}` has no ISO 8601 text in this profile"));
            }
            return text.ToString();
        }

        switch (c.Zone)
        {
            case ZoneKind.Floating:
                break;
            case ZoneKind.FixedOffset:
                AppendOffset(text, c.ZoneSeconds, "/value/zone/seconds");
                break;
            case ZoneKind.Iana:
                if (c.OffsetAssertion is int asserted)
                {
                    if (Math.Abs(asserted) > Values.MaxOffsetSeconds)
                    {
                        throw OpFailure.Invalid("offset-out-of-range", "/value/offsetAssertion", Invariant($"offset {asserted} s is beyond ±18:00"));
                    }
                    AppendOffset(text, asserted, "/value/offsetAssertion");
                }
                text.Append('[').Append(c.ZoneId).Append(']');
                break;
            default:
                throw OpFailure.Unsupported("unsupported-zone", "/value/zone/type", Invariant($"zone type `{c.ZoneType}` has no ISO 8601 text"));
        }
        return text.ToString();
    }

    private static void AppendYear(StringBuilder sb, BigInteger year)
    {
        if (year >= 0 && year <= 9999)
        {
            sb.Append(year.ToString("0000", CultureInfo.InvariantCulture));
            return;
        }
        sb.Append(year.Sign < 0 ? '-' : '+');
        sb.Append(BigInteger.Abs(year).ToString(CultureInfo.InvariantCulture).PadLeft(4, '0'));
    }

    private static void AppendDate(StringBuilder sb, BigInteger y, int m, int d)
    {
        AppendYear(sb, y);
        sb.Append('-').Append(Two(m)).Append('-').Append(Two(d));
    }

    private static void AppendTime(StringBuilder sb, int h, int mi, int? s, int nano)
    {
        sb.Append(Two(h)).Append(':').Append(Two(mi));
        if (s is null)
        {
            return;
        }
        sb.Append(':').Append(Two(s.Value));
        if (nano != 0)
        {
            sb.Append('.').Append(nano.ToString("000000000", CultureInfo.InvariantCulture).TrimEnd('0'));
        }
    }

    private static void AppendOffset(StringBuilder sb, int seconds, string path)
    {
        if (seconds % 60 != 0)
        {
            throw OpFailure.Unsupported("sub-minute-offset", path, Invariant($"offset {seconds} s has no `±hh:mm` form"));
        }
        int abs = Math.Abs(seconds);
        sb.Append(seconds < 0 ? '-' : '+').Append(Two(abs / 3600)).Append(':').Append(Two(abs / 60 % 60));
    }

    private static string Two(int n) => n.ToString("00", CultureInfo.InvariantCulture);
}
