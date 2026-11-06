/** Exact rationals on `bigint`, always kept in lowest terms with a positive denominator. Internal. */
export interface Rational { n: bigint; d: bigint }

const abs = (x: bigint) => (x < 0n ? -x : x);
function gcd(a: bigint, b: bigint): bigint {
  a = abs(a); b = abs(b);
  while (b !== 0n) [a, b] = [b, a % b];
  return a;
}

export function rat(n: bigint, d: bigint = 1n): Rational {
  if (d === 0n) throw new RangeError("zero denominator");
  if (d < 0n) { n = -n; d = -d; }
  const g = gcd(n, d) || 1n;
  return { n: n / g, d: d / g };
}

export const add = (a: Rational, b: Rational) => rat(a.n * b.d + b.n * a.d, a.d * b.d);
export const sub = (a: Rational, b: Rational) => rat(a.n * b.d - b.n * a.d, a.d * b.d);
export const cmp = (a: Rational, b: Rational): -1 | 0 | 1 => {
  const x = a.n * b.d, y = b.n * a.d;
  return x < y ? -1 : x > y ? 1 : 0;
};

/** Floor division on bigint (rounds toward negative infinity). */
export function floorDiv(a: bigint, b: bigint): bigint {
  const q = a / b;
  return (a % b !== 0n && (a < 0n) !== (b < 0n)) ? q - 1n : q;
}
export const mod = (a: bigint, b: bigint) => a - floorDiv(a, b) * b;

/** Splits into an integer floor and a fractional part in [0, 1). */
export function split(r: Rational): { whole: bigint; frac: Rational } {
  const whole = floorDiv(r.n, r.d);
  return { whole, frac: rat(r.n - whole * r.d, r.d) };
}

/** A fraction in [0, 1) as nanoseconds, or undefined when it is not a whole number of nanoseconds. */
export function toNano(frac: Rational): number | undefined {
  const scaled = frac.n * 1_000_000_000n;
  return scaled % frac.d === 0n ? Number(scaled / frac.d) : undefined;
}

export const INT = /^-?(0|[1-9][0-9]*)$/;
