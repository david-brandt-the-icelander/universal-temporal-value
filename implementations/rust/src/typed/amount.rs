//! Amounts: [`Period`] (calendar), [`PosixSeconds`] and [`ElapsedSeconds`] (exact seconds).

use std::fmt;

use serde_json::{json, Map, Value as Json};

use super::outcome::{Diagnostics, Outcome};
use super::{kind_mismatch, rejected, Typed};
use crate::model::{fail, kind, posix_seconds_json, rational_json, read_amount, Amount};
use crate::num::Q;
use crate::{Status, UtvResult};

/// A calendar amount: years, months, weeks, days and wall-clock hours, minutes and seconds.
/// Each may be negative. Its text form is an ISO 8601 duration: `P1M` is a month, `PT1M` a
/// minute, `-P1DT2H` a negative day and two hours.
///
/// A period is not a duration: one day is not 86,400 seconds across a DST change. For exact
/// seconds use [`PosixSeconds`].
///
/// ```
/// use utv::Period;
/// assert_eq!(Period::months(1).to_string(), "P1M");
/// let p = Period { years: 1, days: 10, ..Period::default() };
/// assert_eq!(p.to_string(), "P1Y10D");
/// assert_eq!(p.negate().to_string(), "-P1Y10D");
/// ```
#[derive(Clone, Copy, Default, PartialEq, Eq, Hash)]
pub struct Period {
    pub years: i64,
    pub months: i64,
    pub weeks: i64,
    pub days: i64,
    pub hours: i64,
    pub minutes: i64,
    pub seconds: i64,
}

super::typed_value!(Period);

impl Typed for Period {
    fn read(json: &Json) -> Result<Self, UtvResult> {
        if kind(json) != "amount" || json["value"]["basis"] != "calendar" {
            return kind_mismatch("a calendar amount (period)", json);
        }
        let Amount::Calendar(c) = read_amount(json)? else { return kind_mismatch("a calendar amount (period)", json) };
        super::only_keys(json, &["schema", "kind", "value"])?;
        super::only_keys(&json["value"], &["basis", "components"])?;
        let fit = |n: i128| i64::try_from(n).map_or_else(|_| fail(Status::Unsupported, "implementation-limit", "/value/components", "period component outside the i64 range"), Ok);
        Ok(Period {
            years: fit(c.years)?,
            months: fit(c.months)?,
            weeks: fit(c.weeks)?,
            days: fit(c.days)?,
            hours: fit(c.hours)?,
            minutes: fit(c.minutes)?,
            seconds: fit(c.seconds)?,
        })
    }
    /// `{"basis":"calendar","components":{…}}`: only non-zero components, as decimal strings.
    fn json(&self) -> Json {
        let mut c = Map::new();
        for (name, n) in self.components() {
            if n != 0 {
                c.insert(name.into(), Json::String(n.to_string()));
            }
        }
        json!({"schema": crate::SPEC_VERSION, "kind": "amount", "value": {"basis": "calendar", "components": c}})
    }
}

impl Period {
    /// All seven components at once, in ISO order.
    #[allow(clippy::too_many_arguments)]
    pub const fn of(years: i64, months: i64, weeks: i64, days: i64, hours: i64, minutes: i64, seconds: i64) -> Period {
        Period { years, months, weeks, days, hours, minutes, seconds }
    }
    pub const fn years(n: i64) -> Period {
        Period::of(n, 0, 0, 0, 0, 0, 0)
    }
    pub const fn months(n: i64) -> Period {
        Period::of(0, n, 0, 0, 0, 0, 0)
    }
    pub const fn weeks(n: i64) -> Period {
        Period::of(0, 0, n, 0, 0, 0, 0)
    }
    pub const fn days(n: i64) -> Period {
        Period::of(0, 0, 0, n, 0, 0, 0)
    }
    pub const fn hours(n: i64) -> Period {
        Period::of(0, 0, 0, 0, n, 0, 0)
    }
    pub const fn minutes(n: i64) -> Period {
        Period::of(0, 0, 0, 0, 0, n, 0)
    }
    pub const fn seconds(n: i64) -> Period {
        Period::of(0, 0, 0, 0, 0, 0, n)
    }

    /// `(name, value)` for each component, in ISO order: years, months, weeks, days, hours,
    /// minutes, seconds.
    pub fn components(&self) -> [(&'static str, i64); 7] {
        [
            ("years", self.years),
            ("months", self.months),
            ("weeks", self.weeks),
            ("days", self.days),
            ("hours", self.hours),
            ("minutes", self.minutes),
            ("seconds", self.seconds),
        ]
    }

    /// Every component negated.
    ///
    /// # Panics
    ///
    /// If a component is `i64::MIN`.
    pub fn negate(self) -> Period {
        let n = |x: i64| x.checked_neg().expect("utv: period component overflow");
        Period::of(n(self.years), n(self.months), n(self.weeks), n(self.days), n(self.hours), n(self.minutes), n(self.seconds))
    }

    pub fn is_zero(&self) -> bool {
        *self == Period::default()
    }
}

/// `3600.75`, or `1/3` when the decimal doesn't terminate.
fn display_seconds(q: Q) -> String {
    let (sign, abs) = if q.n < 0 { ("-", Q { n: q.n.unsigned_abs() as i128, d: q.d }) } else { ("", q) };
    let mut d = abs.d;
    for p in [2, 5] {
        while d % p == 0 {
            d /= p;
        }
    }
    if d != 1 {
        return format!("{sign}{}/{}", abs.n, abs.d);
    }
    let mut digits = String::new();
    let (mut n, den) = (abs.frac().n, abs.d);
    while n != 0 {
        let Some(scaled) = n.checked_mul(10) else { return format!("{sign}{}/{}", abs.n, abs.d) };
        n = scaled;
        digits.push(char::from(b'0' + (n / den) as u8));
        n %= den;
    }
    let frac = if digits.is_empty() { String::new() } else { format!(".{digits}") };
    format!("{sign}{}{frac}", abs.floor())
}

fn read_seconds(json: &Json, basis: &str, what: &str) -> Result<Q, UtvResult> {
    if kind(json) != "amount" || json["value"]["basis"] != basis {
        return kind_mismatch(what, json);
    }
    let amount = read_amount(json)?;
    super::only_keys(json, &["schema", "kind", "value"])?;
    super::only_keys(&json["value"], &["basis", "rules", "seconds"])?;
    match amount {
        Amount::PosixSeconds(q) | Amount::Elapsed(Some(q)) => Ok(q),
        _ => fail(Status::Invalid, "malformed-value", "/value/seconds", "an amount of seconds needs `seconds`"),
    }
}

fn ratio(numerator: i128, denominator: i128) -> Result<Q, Diagnostics> {
    Q::new(numerator, denominator).ok_or_else(|| rejected(UtvResult::fail(Status::Invalid, "field-out-of-range", "/value/seconds/denominator", "the denominator must not be 0")))
}

macro_rules! seconds_amount {
    ($t:ident, $basis:literal, $label:literal, $what:literal) => {
        impl Typed for $t {
            fn read(json: &Json) -> Result<Self, UtvResult> {
                Ok($t { q: read_seconds(json, $basis, $what)? })
            }
            fn json(&self) -> Json {
                self.to_json_value()
            }
        }

        impl $t {
            /// A whole number of seconds.
            pub fn of(seconds: i64) -> $t {
                $t { q: Q::int(seconds as i128) }
            }
            /// An exact fraction of seconds. `Err` (`field-out-of-range`) for a zero denominator.
            pub fn of_ratio(numerator: i128, denominator: i128) -> Result<$t, Diagnostics> {
                Ok($t { q: ratio(numerator, denominator)? })
            }
            /// The numerator of the reduced fraction.
            pub fn numerator(self) -> i128 {
                self.q.n
            }
            /// The denominator of the reduced fraction (always positive).
            pub fn denominator(self) -> i128 {
                self.q.d
            }
            /// # Panics
            ///
            /// If the numerator is `i128::MIN`.
            pub fn negate(self) -> $t {
                $t { q: self.q.neg().expect("utv: seconds overflow") }
            }
            /// The UTV JSON value.
            pub fn to_json(&self) -> Json {
                self.to_json_value()
            }
            /// Read a UTV JSON value: `invalid` (`kind-mismatch`) for anything else.
            pub fn from_json(json: &Json) -> Outcome<$t> {
                super::from_json(json)
            }
        }

        impl fmt::Display for $t {
            /// A display string, not a text form (exact seconds have none): `3600.75 s (POSIX)`,
            /// `1/3 s (POSIX)`.
            fn fmt(&self, f: &mut fmt::Formatter<'_>) -> fmt::Result {
                f.pad(&format!("{} s ({})", display_seconds(self.q), $label))
            }
        }

        impl fmt::Debug for $t {
            fn fmt(&self, f: &mut fmt::Formatter<'_>) -> fmt::Result {
                super::debug(f, stringify!($t), &self.to_string())
            }
        }

        impl crate::ToJson for $t {
            fn to_json(&self) -> Json {
                self.to_json_value()
            }
        }
    };
}

/// An exact number of POSIX seconds (`basis: clock-ticks`): what `Instant::until` answers and
/// `Instant::add` takes. POSIX seconds are not SI seconds across a leap second.
///
/// It has no text form, only a display string: `3600.75 s (POSIX)`, or `1/3 s (POSIX)`.
#[derive(Clone, Copy, PartialEq, Eq, Hash)]
pub struct PosixSeconds {
    q: Q,
}

seconds_amount!(PosixSeconds, "clock-ticks", "POSIX", "an amount of POSIX seconds");

impl PosixSeconds {
    pub(crate) fn of_q(q: Q) -> PosixSeconds {
        PosixSeconds { q }
    }

    fn to_json_value(self) -> Json {
        posix_seconds_json(self.q)
    }
}

/// An exact number of SI seconds (`basis: elapsed`), as a physical clock measures them. Adding
/// it to a POSIX instant needs a leap-second table.
///
/// It has no text form, only a display string: `86400 s (SI)`.
#[derive(Clone, Copy, PartialEq, Eq, Hash)]
pub struct ElapsedSeconds {
    q: Q,
}

seconds_amount!(ElapsedSeconds, "elapsed", "SI", "an amount of SI (elapsed) seconds");

impl ElapsedSeconds {
    fn to_json_value(self) -> Json {
        json!({"schema": crate::SPEC_VERSION, "kind": "amount", "value": {"basis": "elapsed", "seconds": rational_json(self.q)}})
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn display_strings() {
        assert_eq!(PosixSeconds::of_ratio(14403, 4).unwrap().to_string(), "3600.75 s (POSIX)");
        assert_eq!(PosixSeconds::of_ratio(1, 3).unwrap().to_string(), "1/3 s (POSIX)");
        assert_eq!(PosixSeconds::of_ratio(-1, 2).unwrap().to_string(), "-0.5 s (POSIX)");
        assert_eq!(ElapsedSeconds::of(86_400).to_string(), "86400 s (SI)");
        assert!(PosixSeconds::of_ratio(1, 0).is_err());
    }
}
