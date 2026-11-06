//! [`Interval`] and [`Bound`]: ranges of dates or instants with explicit endpoint states.

use std::fmt;

use serde_json::Value as Json;

use super::civil::CivilDate;
use super::instant::Instant;
use super::outcome::{total, Diagnostics, Outcome, Truth};
use super::{kind_mismatch, rejected, Typed};
use crate::build::{interval_with, Endpoint};
use crate::model::{fail, kind, structural};
use crate::{Status, UtvResult};

mod sealed {
    pub trait Sealed {}
    impl Sealed for super::CivilDate {}
    impl Sealed for super::Instant {}
}

/// The points an [`Interval`] can have: [`CivilDate`] (domain `civil/gregory`) and
/// [`Instant`] (domain `physical`).
#[allow(private_bounds)]
pub trait IntervalPoint: sealed::Sealed + Typed + Clone + Ord + fmt::Display + fmt::Debug {
    #[doc(hidden)]
    const DOMAIN: &'static str;
}

impl IntervalPoint for CivilDate {
    const DOMAIN: &'static str = "civil/gregory";
}

impl IntervalPoint for Instant {
    const DOMAIN: &'static str = "physical";
}

/// One end of an interval.
#[derive(Debug, Clone, Copy, PartialEq, Eq, Hash)]
pub enum Bound<T> {
    /// The point belongs to the interval: `[` or `]`.
    Included(T),
    /// The point doesn't: `(` or `)`.
    Excluded(T),
    /// There is no end on this side: `..`.
    Unbounded,
    /// There is an end, but nobody knows it (yet): `?`.
    Unknown,
}

impl<T> Bound<T> {
    fn point(&self) -> Option<&T> {
        match self {
            Bound::Included(p) | Bound::Excluded(p) => Some(p),
            _ => None,
        }
    }
}

/// A range of dates or instants: `[2026-11-01, 2026-12-01)`, `[2026-11-01, ..)` (no end),
/// `[2026-11-01, ?)` (an end nobody knows yet).
///
/// [`Interval::contains`] and [`Interval::overlaps`] answer with a [`Truth`]: an unknown end
/// can leave the answer `Possibly`.
///
/// ```
/// use utv::{Bound, CivilDate, Interval, Truth};
/// let nov1 = CivilDate::of(2026, 11, 1)?;
/// let november = Interval::half_open(nov1, CivilDate::of(2026, 12, 1)?)?;
/// assert_eq!(november.to_string(), "[2026-11-01, 2026-12-01)");
/// assert_eq!(november.contains(&CivilDate::of(2026, 11, 30)?), Truth::Definitely);
/// let open_ended = Interval::of(Bound::Included(nov1), Bound::Unknown)?;
/// assert_eq!(open_ended.contains(&CivilDate::of(2027, 6, 1)?), Truth::Possibly);
/// # Ok::<(), utv::Diagnostics>(())
/// ```
#[derive(Clone, PartialEq, Eq, Hash)]
pub struct Interval<T> {
    start: Bound<T>,
    end: Bound<T>,
}

impl<T: IntervalPoint> Interval<T> {
    /// `[start, end)`. `Err` (`reversed-interval`) if `end` is before `start`.
    pub fn half_open(start: T, end: T) -> Result<Interval<T>, Diagnostics> {
        Interval::of(Bound::Included(start), Bound::Excluded(end))
    }

    /// `[start, end]`. `Err` (`reversed-interval`) if `end` is before `start`.
    pub fn closed(start: T, end: T) -> Result<Interval<T>, Diagnostics> {
        Interval::of(Bound::Included(start), Bound::Included(end))
    }

    /// Any endpoints. `Err` with `reversed-interval` if both are points and `end` is before
    /// `start`, or with `unsupported-form` if neither is a point (nothing would fix the domain).
    pub fn of(start: Bound<T>, end: Bound<T>) -> Result<Interval<T>, Diagnostics> {
        Interval::checked(start, end).map_err(rejected)
    }

    fn checked(start: Bound<T>, end: Bound<T>) -> Result<Interval<T>, UtvResult> {
        if start.point().is_none() && end.point().is_none() {
            return fail(Status::Unsupported, "unsupported-form", "", "an interval needs at least one point: nothing else fixes its domain");
        }
        if let (Some(a), Some(b)) = (start.point(), end.point()) {
            if b < a {
                return fail(Status::Invalid, "reversed-interval", "", format!("the interval ends ({b}) before it starts ({a})"));
            }
        }
        Ok(Interval { start, end })
    }

    pub fn start(&self) -> &Bound<T> {
        &self.start
    }

    pub fn end(&self) -> &Bound<T> {
        &self.end
    }

    /// Whether `point` is in the interval: `Definitely`, `Possibly` (an unknown end decides)
    /// or `Impossible`.
    pub fn contains(&self, point: &T) -> Truth {
        truth(crate::ops::contains(&self.json(), &point.json()))
    }

    /// Whether the two intervals share a point. Date intervals are compared as whole days, so
    /// `[2026-11-01, 2026-11-30]` and `[2026-12-01, ..)` don't overlap.
    pub fn overlaps(&self, other: &Interval<T>) -> Truth {
        truth(crate::ops::overlaps(&self.json(), &other.json()))
    }

    /// The UTV JSON value.
    pub fn to_json(&self) -> Json {
        self.json()
    }

    /// Read a UTV JSON interval of this point type: `invalid` (`kind-mismatch`) for anything else.
    pub fn from_json(json: &Json) -> Outcome<Interval<T>> {
        super::from_json(json)
    }

    /// Parse the text form, `[2026-11-01, 2026-12-01)`, or an EDTF interval, `2026-11-01/2026-11-30`.
    pub fn parse(text: &str) -> Outcome<Interval<T>> {
        super::parse_as(text)
    }
}

fn truth(r: UtvResult) -> Truth {
    total(r, |j| j.as_str().and_then(Truth::parse).map_or_else(|| fail(Status::Error, "unexpected", "", "not a truth value"), Ok))
}

impl<T: IntervalPoint> Typed for Interval<T> {
    fn read(json: &Json) -> Result<Self, UtvResult> {
        let what = format!("an interval of {}", if T::DOMAIN == "physical" { "instants" } else { "dates" });
        if kind(json) != "interval" {
            return kind_mismatch(&what, json);
        }
        structural(json)?;
        let v = &json["value"];
        if v.get("construction").is_some_and(|c| c != "start-end") {
            return super::no_typed_type("an interval given by start and amount");
        }
        super::only_keys(json, &["schema", "kind", "value"])?;
        super::only_keys(v, &["domain", "start", "end", "construction"])?;
        match v["domain"].as_str() {
            Some(d) if d == T::DOMAIN => {}
            Some("civil/gregory" | "physical") => return kind_mismatch(&what, json),
            _ => return super::no_typed_type("an interval in this domain"),
        }
        // A point of another kind in the right domain (an interval of date-times, say) is a
        // valid interval no typed type holds.
        let point = |p: &Json| -> Result<T, UtvResult> {
            T::read(p).map_err(|e| if e.diagnostics.iter().any(|d| d.code == "kind-mismatch") { UtvResult::fail(Status::Unsupported, "unsupported-operand", "", "no typed interval holds these points; use the JSON layer") } else { e })
        };
        let bound = |b: &Json| -> Result<Bound<T>, UtvResult> {
            Ok(match b["state"].as_str().unwrap_or("") {
                "included" => Bound::Included(point(&b["point"])?),
                "excluded" => Bound::Excluded(point(&b["point"])?),
                "unbounded" => Bound::Unbounded,
                _ => Bound::Unknown,
            })
        };
        Interval::checked(bound(&v["start"])?, bound(&v["end"])?)
    }

    fn json(&self) -> Json {
        let endpoint = |b: &Bound<T>| match b {
            Bound::Included(p) => Endpoint::Included(p.json()),
            Bound::Excluded(p) => Endpoint::Excluded(p.json()),
            Bound::Unbounded => Endpoint::Unbounded,
            Bound::Unknown => Endpoint::Unknown,
        };
        let mut v = interval_with(endpoint(&self.start), endpoint(&self.end));
        v["value"]["domain"] = T::DOMAIN.into();
        v
    }
}

impl<T: IntervalPoint> fmt::Display for Interval<T> {
    /// The text form: `[2026-11-01, 2026-12-01)`.
    fn fmt(&self, f: &mut fmt::Formatter<'_>) -> fmt::Result {
        f.pad(&super::text_of(&self.json()))
    }
}

impl<T: IntervalPoint> fmt::Debug for Interval<T> {
    fn fmt(&self, f: &mut fmt::Formatter<'_>) -> fmt::Result {
        super::debug(f, "Interval", &self.to_string())
    }
}

impl<T: IntervalPoint> std::str::FromStr for Interval<T> {
    type Err = crate::OutcomeError;
    fn from_str(text: &str) -> Result<Self, Self::Err> {
        Interval::parse(text).single()
    }
}

impl<T: IntervalPoint> crate::ToJson for Interval<T> {
    fn to_json(&self) -> Json {
        self.json()
    }
}
