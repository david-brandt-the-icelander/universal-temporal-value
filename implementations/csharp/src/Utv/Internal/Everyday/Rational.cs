using System;
using System.Globalization;
using System.Numerics;

namespace Utv.Internal.Everyday;

/// <summary>An exact rational number, always reduced, with a positive denominator.</summary>
internal readonly struct Rational : IComparable<Rational>, IEquatable<Rational>
{
    public static readonly BigInteger NanosPerSecond = 1_000_000_000;

    public Rational(BigInteger numerator, BigInteger denominator)
    {
        if (denominator.IsZero)
        {
            throw new ArgumentOutOfRangeException(nameof(denominator), "denominator must not be zero");
        }
        if (denominator.Sign < 0)
        {
            numerator = -numerator;
            denominator = -denominator;
        }
        BigInteger g = BigInteger.GreatestCommonDivisor(numerator, denominator);
        if (!g.IsZero && !g.IsOne)
        {
            numerator /= g;
            denominator /= g;
        }
        Numerator = numerator;
        _denominatorMinusOne = denominator - 1;
    }

    // Stored minus one so that default(Rational) is 0/1.
    private readonly BigInteger _denominatorMinusOne;

    public BigInteger Numerator { get; }

    public BigInteger Denominator => _denominatorMinusOne + 1;

    public bool IsInteger => Denominator.IsOne;

    public static Rational FromInteger(BigInteger value) => new(value, BigInteger.One);

    /// <summary><paramref name="seconds"/> plus <paramref name="nano"/> nanoseconds.</summary>
    public static Rational FromSecondsAndNano(BigInteger seconds, long nano) =>
        new(seconds * NanosPerSecond + nano, NanosPerSecond);

    /// <summary>The largest integer not greater than this value.</summary>
    public BigInteger Floor() => MathUtil.FloorDiv(Numerator, Denominator);

    /// <summary>This value minus its floor, in [0, 1).</summary>
    public Rational FractionalPart() => new(MathUtil.FloorMod(Numerator, Denominator), Denominator);

    /// <summary>The fractional part as whole nanoseconds, if it is an exact number of them.</summary>
    public bool TryGetNanoOfSecond(out int nano)
    {
        Rational frac = FractionalPart();
        BigInteger scaled = frac.Numerator * NanosPerSecond;
        if (!(scaled % frac.Denominator).IsZero)
        {
            nano = 0;
            return false;
        }
        nano = (int)(scaled / frac.Denominator);
        return true;
    }

    public static Rational operator +(Rational a, Rational b) =>
        new(a.Numerator * b.Denominator + b.Numerator * a.Denominator, a.Denominator * b.Denominator);

    public static Rational operator -(Rational a, Rational b) =>
        new(a.Numerator * b.Denominator - b.Numerator * a.Denominator, a.Denominator * b.Denominator);

    public static Rational operator -(Rational a) => new(-a.Numerator, a.Denominator);

    public static bool operator <(Rational a, Rational b) => a.CompareTo(b) < 0;

    public static bool operator >(Rational a, Rational b) => a.CompareTo(b) > 0;

    public static bool operator <=(Rational a, Rational b) => a.CompareTo(b) <= 0;

    public static bool operator >=(Rational a, Rational b) => a.CompareTo(b) >= 0;

    public static bool operator ==(Rational a, Rational b) => a.Equals(b);

    public static bool operator !=(Rational a, Rational b) => !a.Equals(b);

    public int CompareTo(Rational other) =>
        (Numerator * other.Denominator).CompareTo(other.Numerator * Denominator);

    public bool Equals(Rational other) => Numerator == other.Numerator && Denominator == other.Denominator;

    public override bool Equals(object? obj) => obj is Rational r && Equals(r);

    public override int GetHashCode() => HashCode.Combine(Numerator, Denominator);

    public override string ToString() =>
        Numerator.ToString(CultureInfo.InvariantCulture) + "/" + Denominator.ToString(CultureInfo.InvariantCulture);
}

internal static class MathUtil
{
    public static BigInteger FloorDiv(BigInteger a, BigInteger b)
    {
        BigInteger q = BigInteger.DivRem(a, b, out BigInteger r);
        if (!r.IsZero && (r.Sign < 0) != (b.Sign < 0))
        {
            q -= 1;
        }
        return q;
    }

    public static BigInteger FloorMod(BigInteger a, BigInteger b) => a - FloorDiv(a, b) * b;

    public static long FloorDiv(long a, long b)
    {
        long q = a / b;
        if (a % b != 0 && (a < 0) != (b < 0))
        {
            q--;
        }
        return q;
    }

    public static long FloorMod(long a, long b) => a - FloorDiv(a, b) * b;
}
