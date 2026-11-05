//! Exact arithmetic: reduced `i128` rationals and proleptic Gregorian day numbers.
//!
//! Every operation is checked. An overflow gives `None`, which the operations report as
//! `unsupported` (diagnostic `range`) rather than wrapping or rounding.

use std::cmp::Ordering;

/// A reduced rational number with a positive denominator.
#[derive(Debug, Clone, Copy, PartialEq, Eq)]
pub(crate) struct Q {
    pub n: i128,
    pub d: i128,
}

fn gcd(mut a: i128, mut b: i128) -> i128 {
    a = a.abs();
    b = b.abs();
    while b != 0 {
        let t = a % b;
        a = b;
        b = t;
    }
    a
}

impl Q {
    pub fn new(n: i128, d: i128) -> Option<Q> {
        if d == 0 {
            return None;
        }
        let g = gcd(n, d).max(1);
        let (mut n, mut d) = (n / g, d / g);
        if d < 0 {
            n = n.checked_neg()?;
            d = d.checked_neg()?;
        }
        Some(Q { n, d })
    }

    pub fn int(n: i128) -> Q {
        Q { n, d: 1 }
    }

    pub fn add(self, o: Q) -> Option<Q> {
        let g = gcd(self.d, o.d);
        let l = (self.d / g).checked_mul(o.d)?;
        let a = self.n.checked_mul(l / self.d)?;
        let b = o.n.checked_mul(l / o.d)?;
        Q::new(a.checked_add(b)?, l)
    }

    pub fn neg(self) -> Option<Q> {
        Some(Q { n: self.n.checked_neg()?, d: self.d })
    }

    pub fn sub(self, o: Q) -> Option<Q> {
        self.add(o.neg()?)
    }

    pub fn cmp(self, o: Q) -> Option<Ordering> {
        Some(self.n.checked_mul(o.d)?.cmp(&o.n.checked_mul(self.d)?))
    }

    pub fn floor(self) -> i128 {
        self.n.div_euclid(self.d)
    }

    /// The fractional part, in `[0, 1)`.
    pub fn frac(self) -> Q {
        Q { n: self.n.rem_euclid(self.d), d: self.d }
    }

    /// The fractional part as a whole number of nanoseconds, if it is one.
    pub fn frac_nanos(self) -> Option<u32> {
        let f = self.frac();
        let scaled = f.n.checked_mul(1_000_000_000)?;
        if scaled % f.d != 0 {
            return None;
        }
        Some((scaled / f.d) as u32)
    }

    pub fn from_secs_nanos(secs: i128, nano: u32) -> Option<Q> {
        Q::int(secs).add(Q::new(nano as i128, 1_000_000_000)?)
    }
}

/// Proleptic Gregorian leap year.
pub(crate) fn is_leap(year: i128) -> bool {
    year.rem_euclid(4) == 0 && (year.rem_euclid(100) != 0 || year.rem_euclid(400) == 0)
}

pub(crate) fn days_in_month(year: i128, month: u32) -> u32 {
    match month {
        1 | 3 | 5 | 7 | 8 | 10 | 12 => 31,
        4 | 6 | 9 | 11 => 30,
        _ => {
            if is_leap(year) {
                29
            } else {
                28
            }
        }
    }
}

/// Days from 1970-01-01 to a proleptic Gregorian date (H. Hinnant's algorithm, checked).
pub(crate) fn days_from_civil(y: i128, m: u32, d: u32) -> Option<i128> {
    let y = if m <= 2 { y.checked_sub(1)? } else { y };
    let era = y.div_euclid(400);
    let yoe = y.rem_euclid(400);
    let mp = (m as i128 + 9) % 12;
    let doy = (153 * mp + 2) / 5 + d as i128 - 1;
    let doe = yoe * 365 + yoe / 4 - yoe / 100 + doy;
    era.checked_mul(146_097)?.checked_add(doe)?.checked_sub(719_468)
}

/// The proleptic Gregorian date of a day number (days from 1970-01-01).
pub(crate) fn civil_from_days(z: i128) -> Option<(i128, u32, u32)> {
    let z = z.checked_add(719_468)?;
    let era = z.div_euclid(146_097);
    let doe = z.rem_euclid(146_097);
    let yoe = (doe - doe / 1460 + doe / 36_524 - doe / 146_096) / 365;
    let doy = doe - (365 * yoe + yoe / 4 - yoe / 100);
    let mp = (5 * doy + 2) / 153;
    let d = (doy - (153 * mp + 2) / 5 + 1) as u32;
    let m = if mp < 10 { mp + 3 } else { mp - 9 } as u32;
    let y = era.checked_mul(400)?.checked_add(yoe)?;
    Some((if m <= 2 { y.checked_add(1)? } else { y }, m, d))
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn day_numbers_round_trip() {
        assert_eq!(days_from_civil(1970, 1, 1), Some(0));
        assert_eq!(days_from_civil(2026, 11, 5), Some(20_762));
        for z in [-1_000_000i128, -719_468, -1, 0, 1, 20_762, 3_000_000] {
            let (y, m, d) = civil_from_days(z).unwrap();
            assert_eq!(days_from_civil(y, m, d), Some(z));
        }
        let huge = 10i128.pow(30);
        let z = days_from_civil(huge, 2, 29).unwrap();
        assert_eq!(civil_from_days(z), Some((huge, 2, 29)));
        assert_eq!(days_from_civil(i128::MAX / 2, 1, 1), None);
    }

    #[test]
    fn rationals() {
        let a = Q::new(3, 4).unwrap();
        let b = Q::new(1, 2).unwrap();
        assert_eq!(a.add(b), Q::new(5, 4));
        assert_eq!(Q::new(-3, 2).unwrap().floor(), -2);
        assert_eq!(Q::new(-3, 2).unwrap().frac(), Q::new(1, 2).unwrap());
        assert_eq!(Q::new(1, 200_000_000).unwrap().frac_nanos(), Some(5));
        assert_eq!(Q::new(1, 3).unwrap().frac_nanos(), None);
    }
}
