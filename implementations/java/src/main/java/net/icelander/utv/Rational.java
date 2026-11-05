package net.icelander.utv;

import java.math.BigInteger;

/** An exact rational number in lowest terms with a positive denominator. */
final class Rational implements Comparable<Rational> {
    static final Rational ZERO = new Rational(BigInteger.ZERO, BigInteger.ONE);
    static final BigInteger BILLION = BigInteger.valueOf(1_000_000_000L);

    final BigInteger num, den;

    private Rational(BigInteger num, BigInteger den) {
        this.num = num;
        this.den = den;
    }

    static Rational of(BigInteger num, BigInteger den) {
        if (den.signum() == 0) throw new ArithmeticException("zero denominator");
        if (den.signum() < 0) { num = num.negate(); den = den.negate(); }
        BigInteger g = num.gcd(den);
        if (g.signum() != 0 && !g.equals(BigInteger.ONE)) { num = num.divide(g); den = den.divide(g); }
        return new Rational(num, den);
    }

    static Rational of(BigInteger n) { return new Rational(n, BigInteger.ONE); }
    static Rational of(long n) { return of(BigInteger.valueOf(n)); }

    Rational add(Rational o) { return of(num.multiply(o.den).add(o.num.multiply(den)), den.multiply(o.den)); }
    Rational subtract(Rational o) { return add(o.negate()); }
    Rational negate() { return new Rational(num.negate(), den); }
    Rational multiply(BigInteger k) { return of(num.multiply(k), den); }

    /** Largest integer not greater than this. */
    BigInteger floor() {
        BigInteger[] qr = num.divideAndRemainder(den);
        return qr[1].signum() < 0 ? qr[0].subtract(BigInteger.ONE) : qr[0];
    }

    /** This minus its floor, in [0, 1). */
    Rational fraction() { return subtract(of(floor())); }

    boolean isZero() { return num.signum() == 0; }
    boolean isInteger() { return den.equals(BigInteger.ONE); }

    @Override public int compareTo(Rational o) { return num.multiply(o.den).compareTo(o.num.multiply(den)); }
    @Override public boolean equals(Object o) { return o instanceof Rational r && num.equals(r.num) && den.equals(r.den); }
    @Override public int hashCode() { return num.hashCode() * 31 + den.hashCode(); }
    @Override public String toString() { return num + "/" + den; }
}
