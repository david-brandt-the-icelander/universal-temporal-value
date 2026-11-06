//! Typed policies and [`TemporalContext`], which holds the zone rules once.

use std::cmp::Ordering;

use serde_json::{json, Value as Json};

use super::civil::ZonedIntent;
use super::instant::Instant;
use super::outcome::{failure, outcome, Diagnostics, Outcome};
use super::Typed;
use crate::{Rules, UtvResult};

/// The zone rule bundle a [`TemporalContext`] holds: `ZoneRules::load(path)` or
/// `ZoneRules::parse(text)`. The same type as the JSON layer's [`Rules`].
pub type ZoneRules = Rules;

/// What month arithmetic does when the day doesn't exist in the resulting month.
#[derive(Debug, Clone, Copy, PartialEq, Eq, Hash, Default)]
pub enum MonthEnd {
    /// No answer (`none`, diagnostic `month-end`). The default.
    #[default]
    Reject,
    /// Clamp to the last day of the month.
    Constrain,
}

impl MonthEnd {
    pub(crate) fn json(self) -> Json {
        json!({"monthEnd": match self { MonthEnd::Reject => "reject", MonthEnd::Constrain => "constrain" }})
    }
}

/// What resolving a local time in a gap (it never happened) does.
#[derive(Debug, Clone, Copy, PartialEq, Eq, Hash, Default)]
pub enum Gap {
    /// No answer (`none`, diagnostic `gap`). The default.
    #[default]
    Reject,
    /// No answer either: the value stays an unresolved intention (`none`, diagnostic `gap`).
    PreserveUnresolved,
    /// Shift forward by the gap's length: 02:30 in a one-hour spring-forward gap gives 03:30.
    ShiftForward,
}

/// What resolving a local time in an overlap (it happened more than once) does.
#[derive(Debug, Clone, Copy, PartialEq, Eq, Hash, Default)]
pub enum Overlap {
    /// Every candidate, as `multiple`, with diagnostic `ambiguous`. The default.
    #[default]
    Reject,
    /// Every candidate, as `multiple`, without a diagnostic.
    All,
    /// The earlier instant.
    Earliest,
    /// The later instant.
    Latest,
}

/// How [`ZonedIntent::resolve_with`] treats gaps and overlaps.
#[derive(Debug, Clone, Copy, PartialEq, Eq, Hash, Default)]
pub struct ResolutionPolicy {
    pub gap: Gap,
    pub overlap: Overlap,
}

impl ResolutionPolicy {
    /// Reject gaps and overlaps (the default): `none` for a gap, `multiple` + `ambiguous` for an overlap.
    pub const STRICT: ResolutionPolicy = ResolutionPolicy { gap: Gap::Reject, overlap: Overlap::Reject };
    /// Every candidate there is: `none` for a gap, `multiple` for an overlap, no diagnostics.
    pub const ALL_CANDIDATES: ResolutionPolicy = ResolutionPolicy { gap: Gap::PreserveUnresolved, overlap: Overlap::All };

    pub const fn new(gap: Gap, overlap: Overlap) -> ResolutionPolicy {
        ResolutionPolicy { gap, overlap }
    }

    pub(crate) fn json(self) -> Json {
        let gap = match self.gap {
            Gap::Reject => "reject",
            Gap::PreserveUnresolved => "preserve-unresolved",
            Gap::ShiftForward => "shift-forward-by-gap",
        };
        let overlap = match self.overlap {
            Overlap::Reject => "reject",
            Overlap::All => "all",
            Overlap::Earliest => "earliest",
            Overlap::Latest => "latest",
        };
        json!({"gap": gap, "overlap": overlap})
    }
}

/// The zone rules, held once. There is no default or global context: the library never reads
/// the host's time zone database, so you load the rules and say which ones you mean.
///
/// ```
/// use utv::{CivilDateTime, TemporalContext, ZoneRules};
/// let rules = ZoneRules::load(concat!(env!("CARGO_MANIFEST_DIR"), "/../../conformance/rules/demo-zones.json"))?;
/// let ctx = TemporalContext::with(rules);
/// let meeting = CivilDateTime::of(2027, 6, 1, 9, 0)?.in_zone("Europe/Paris");
/// assert_eq!(ctx.resolve(&meeting).single()?.to_string(), "2027-06-01T07:00:00Z");
/// # Ok::<(), Box<dyn std::error::Error>>(())
/// ```
#[derive(Debug, Clone, PartialEq, Eq)]
pub struct TemporalContext {
    rules: Rules,
}

impl TemporalContext {
    pub fn with(rules: ZoneRules) -> TemporalContext {
        TemporalContext { rules }
    }

    pub fn rules(&self) -> &ZoneRules {
        &self.rules
    }

    /// The instant(s) of a zoned local time, under [`ResolutionPolicy::STRICT`].
    pub fn resolve(&self, zoned: &ZonedIntent) -> Outcome<Instant> {
        self.resolve_with(zoned, ResolutionPolicy::STRICT)
    }

    /// The instant(s) of a zoned local time under `policy`: `unique`; `none` in a gap (unless
    /// [`Gap::ShiftForward`]); `multiple` in an overlap (unless [`Overlap::Earliest`] or
    /// [`Overlap::Latest`]); `invalid` (`offset-mismatch`) if an asserted offset contradicts the
    /// rules; `unsupported` for zones or times the rules don't cover.
    pub fn resolve_with(&self, zoned: &ZonedIntent, policy: ResolutionPolicy) -> Outcome<Instant> {
        outcome(crate::zones::resolve(&zoned.json(), &self.rules, Some(&policy.json())), Instant::read)
    }

    /// The wall time of `instant` in zone `zone_id`, with the offset in force asserted.
    /// `unsupported` for zones the rules don't list and instants outside their window.
    pub fn in_zone(&self, instant: &Instant, zone_id: &str) -> Outcome<ZonedIntent> {
        outcome(crate::zones::to_zone(&instant.json(), zone_id, &self.rules), ZonedIntent::read)
    }

    /// The order of two zoned values: both are resolved strictly, then compared. `multiple`
    /// (with the orders, ascending) when the order depends on which side of an overlap is meant;
    /// `none` or `unsupported` when either can't be resolved.
    pub fn compare(&self, a: &ZonedIntent, b: &ZonedIntent) -> Outcome<Ordering> {
        let (ra, rb) = (self.resolve(a), self.resolve(b));
        let mut notes: Vec<_> = ra.diagnostics().iter().chain(rb.diagnostics()).cloned().collect();
        notes.dedup();
        let mut r = UtvResult::new(crate::Status::Unique, vec![]).op("compare");
        r.diagnostics = notes;
        let candidates = |o: Outcome<Instant>| -> Result<Vec<Instant>, Outcome<Ordering>> {
            match o {
                Outcome::Unique(v, _) => Ok(vec![v]),
                Outcome::Multiple(vs, _) => Ok(vs),
                other => Err(failure(other.to_result().op("compare"))),
            }
        };
        let (xs, ys) = match (candidates(ra), candidates(rb)) {
            (Ok(xs), Ok(ys)) => (xs, ys),
            (Err(e), _) | (_, Err(e)) => return e,
        };
        let mut orders: Vec<Ordering> = xs.iter().flat_map(|x| ys.iter().map(move |y| x.cmp(y))).collect();
        orders.sort();
        orders.dedup();
        let d = Diagnostics::of(&r);
        match orders.as_slice() {
            [one] => Outcome::Unique(*one, d),
            _ => Outcome::Multiple(orders, d),
        }
    }
}
