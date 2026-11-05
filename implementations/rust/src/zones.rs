//! Zone rules and the conversions between civil date-times and instants: [`Rules`],
//! [`resolve`], [`to_zone`] and [`to_offset`].
//!
//! Rules are always an explicit input. The library never reads the host's tz database; load a
//! rule bundle in the format of `conformance/rules/demo-zones.json` with [`Rules::parse`].
//! Outside the bundle's validity window, or for zones it doesn't list, the answer is
//! `unsupported`: nothing is extrapolated.

use std::collections::BTreeMap;

use serde_json::Value;

use crate::model::{done, fail, instant_json, kind, read_civil, read_instant, Civil, Form, Zone, MAX_OFFSET, R};
use crate::num::Q;
use crate::ops::note;
use crate::{Status, UtvResult};

#[derive(Debug, Clone, PartialEq, Eq)]
struct ZoneRules {
    initial_offset: i64,
    /// `(at, offset)`, ascending by `at` (POSIX seconds).
    transitions: Vec<(i128, i64)>,
}

impl ZoneRules {
    /// The offset in force at `t`: the last transition at or before `t`.
    fn offset_at(&self, t: Q) -> i64 {
        self.transitions.iter().take_while(|(at, _)| Q::int(*at).cmp(t).is_some_and(|o| o.is_le())).last().map_or(self.initial_offset, |(_, o)| *o)
    }
}

/// A parsed zone rule bundle (the format of `conformance/rules/demo-zones.json`).
#[derive(Debug, Clone, PartialEq, Eq)]
pub struct Rules {
    /// The bundle's `ruleSet` reference, as given.
    pub rule_set: Value,
    /// Inclusive start of validity, POSIX seconds.
    pub valid_from: i128,
    /// Exclusive end of validity, POSIX seconds.
    pub valid_until: i128,
    zones: BTreeMap<String, ZoneRules>,
}

impl Rules {
    /// Parse a rule bundle from JSON text.
    pub fn parse(text: &str) -> Result<Rules, String> {
        Rules::from_json(&crate::parse_strict(text)?)
    }

    /// Read a rule bundle from an already parsed JSON value.
    pub fn from_json(v: &Value) -> Result<Rules, String> {
        let int = |x: &Value, what: &str| -> Result<i128, String> {
            match x {
                Value::String(s) => s.parse::<i128>().map_err(|e| format!("{what}: {e}")),
                Value::Number(n) => n.as_i64().map(i128::from).ok_or_else(|| format!("{what}: not an integer")),
                _ => Err(format!("{what}: missing")),
            }
        };
        let offset = |x: &Value, what: &str| -> Result<i64, String> {
            x.as_i64().filter(|o| o.abs() <= 26 * 3600).ok_or_else(|| format!("{what}: offset must be an integer number of seconds"))
        };
        let mut zones = BTreeMap::new();
        for (id, z) in v["zones"].as_object().ok_or("`zones` must be an object")? {
            let mut transitions = Vec::new();
            for (i, t) in z["transitions"].as_array().map(Vec::as_slice).unwrap_or_default().iter().enumerate() {
                let what = format!("zones.{id}.transitions[{i}]");
                transitions.push((int(&t["at"], &what)?, offset(&t["offset"], &what)?));
            }
            if transitions.windows(2).any(|w| w[0].0 >= w[1].0) {
                return Err(format!("zones.{id}: transitions must be strictly ascending"));
            }
            zones.insert(id.clone(), ZoneRules { initial_offset: offset(&z["initialOffset"], &format!("zones.{id}.initialOffset"))?, transitions });
        }
        Ok(Rules { rule_set: v["ruleSet"].clone(), valid_from: int(&v["validFrom"], "validFrom")?, valid_until: int(&v["validUntil"], "validUntil")?, zones })
    }

    /// The zone ids this bundle has rules for.
    pub fn zone_ids(&self) -> impl Iterator<Item = &str> {
        self.zones.keys().map(String::as_str)
    }

    fn zone(&self, id: &str) -> R<&ZoneRules> {
        self.zones.get(id).map_or_else(|| fail(Status::Unsupported, "zone.unknown", "/value/zone/id", format!("the rules have no zone `{id}`")), Ok)
    }

    fn within(&self, t: Q) -> bool {
        Q::int(self.valid_from).cmp(t).is_some_and(|o| o.is_le()) && t.cmp(Q::int(self.valid_until)).is_some_and(|o| o.is_lt())
    }
}

fn window_err<T>() -> R<T> {
    fail(Status::Unsupported, "rules.window", "", "outside the rules' validity window; zone rules are never extrapolated")
}

/// Turn a civil date-time into instant(s).
///
/// `policy` is `{"gap": …, "overlap": …}`; without it, the value's own `resolutionPolicy` is
/// used, and failing that `{"gap":"reject","overlap":"reject"}`.
///
/// - unzoned (floating) date-time: `symbolic`, there is no zone to resolve against yet;
/// - fixed offset: `unique`, no rules needed;
/// - an `offsetAssertion` keeps only the candidate with that offset, before any policy applies;
///   if none has it (including a wall time in a gap), the value contradicts the rules: `invalid`,
///   diagnostic `offset-mismatch`;
/// - named zone: one candidate is `unique`. A gap gives `none` (diagnostic `gap`) under `reject`
///   or `preserve-unresolved`, or the instant shifted forward by the gap under
///   `shift-forward-by-gap`. An overlap gives `multiple` (both instants, ascending; `reject`
///   adds diagnostic `ambiguous`) under `reject` or `all`, or one instant under
///   `earliest` / `latest`;
/// - other forms, second 60, unknown zones, times outside the rules' window and other policy
///   values: `unsupported`.
pub fn resolve(value: &Value, rules: &Rules, policy: Option<&Value>) -> UtvResult {
    done("resolve", (|| {
        if kind(value) == "civil" && value["value"]["form"] != "datetime" {
            crate::model::structural(value)?;
            return fail(Status::Unsupported, "form", "/value/form", "only `datetime` values resolve to instants");
        }
        let c = read_civil(value)?;
        if c.form != Form::DateTime {
            return fail(Status::Unsupported, "form", "/value/form", "only `datetime` values resolve to instants");
        }
        let l = c.local_seconds()?;
        let id = match &c.zone {
            Zone::Floating => {
                let mut r = UtvResult::fail(Status::Symbolic, "floating", "/value/zone", "no zone is bound, so there is nothing to resolve yet");
                r.diagnostics[0].severity = crate::Severity::Info;
                return Ok(r);
            }
            Zone::Fixed(o) => {
                if c.offset_assertion.is_some_and(|a| a != *o) {
                    return fail(Status::Invalid, "offset-mismatch", "/value/offsetAssertion", "offsetAssertion contradicts the fixed offset");
                }
                return Ok(UtvResult::unique(instant_json(sub(l, *o)?)));
            }
            Zone::Other(t) => return fail(Status::Unsupported, "zone.type", "/value/zone/type", format!("zone type `{t}` is not implemented")),
            Zone::Iana(id) => id,
        };
        let zone = rules.zone(id)?;
        let day = Q::int(86_400);
        let (lo, hi) = (l.sub(day), l.add(day));
        let (Some(lo), Some(hi)) = (lo, hi) else { return window_err() };
        if lo.cmp(Q::int(rules.valid_from)).is_none_or(|o| o.is_lt()) || hi.cmp(Q::int(rules.valid_until)).is_none_or(|o| o.is_ge()) {
            return window_err();
        }
        let policy = policy.or_else(|| value["value"].get("resolutionPolicy"));
        let pick = |key: &str| policy.and_then(|p| p.get(key)).and_then(Value::as_str).unwrap_or("reject").to_owned();
        let (gap, overlap) = (pick("gap"), pick("overlap"));
        if !matches!(gap.as_str(), "reject" | "preserve-unresolved" | "shift-forward-by-gap") {
            return fail(Status::Unsupported, "policy", "/policy/gap", format!("gap policy `{gap}` is not implemented"));
        }
        if !matches!(overlap.as_str(), "reject" | "all" | "earliest" | "latest") {
            return fail(Status::Unsupported, "policy", "/policy/overlap", format!("overlap policy `{overlap}` is not implemented"));
        }

        // Offsets in force within a day of L; t = L − o is a candidate when the offset at t is o.
        let mut offsets = vec![zone.offset_at(lo)];
        offsets.extend(zone.transitions.iter().filter(|(at, _)| in_range(*at, lo, hi)).map(|(_, o)| *o));
        offsets.sort();
        offsets.dedup();
        let mut found = Vec::new();
        for o in offsets {
            let t = sub(l, o)?;
            if zone.offset_at(t) == o {
                found.push((t, o));
            }
        }
        found.sort_by(|a, b| a.0.cmp(b.0).unwrap_or(std::cmp::Ordering::Equal));
        found.dedup();
        // An asserted offset keeps only the candidate with that offset, before any policy applies.
        // A value whose assertion matches no candidate (including a wall time in a gap)
        // contradicts the rules.
        if let Some(asserted) = c.offset_assertion {
            found.retain(|(_, o)| *o == asserted);
            if found.is_empty() {
                return fail(Status::Invalid, "offset-mismatch", "/value/offsetAssertion", format!("offset {asserted} s is not in force in {id} at {}", wall(&c)));
            }
        }
        let found: Vec<Q> = found.into_iter().map(|(t, _)| t).collect();

        match found.as_slice() {
            [t] => Ok(UtvResult::unique(instant_json(*t))),
            [] => {
                if gap == "shift-forward-by-gap" {
                    let before = offset_before_gap(zone, l)?;
                    let mut r = UtvResult::unique(instant_json(sub(l, before)?));
                    r.diagnostics.push(note("gap", "the wall time falls in a gap; shifted forward by the gap's length"));
                    return Ok(r);
                }
                fail(Status::None, "gap", "/value/fields", format!("{} doesn't exist in {id}: it falls in a gap of the zone rules", wall(&c)))
            }
            many => {
                let instants: Vec<Value> = many.iter().map(|t| instant_json(*t)).collect();
                match overlap.as_str() {
                    "earliest" => Ok(UtvResult::unique(instants[0].clone())),
                    "latest" => Ok(UtvResult::unique(instants[instants.len() - 1].clone())),
                    _ => {
                        let mut r = UtvResult::new(Status::Multiple, instants);
                        if overlap == "reject" {
                            let mut d = UtvResult::fail(Status::Multiple, "ambiguous", "/value/fields", format!("{} happens {} times in {id}: the zone's clocks went back", wall(&c), many.len())).diagnostics;
                            d[0].severity = crate::Severity::Warning;
                            r.diagnostics = d;
                        }
                        Ok(r)
                    }
                }
            }
        }
    })())
}

fn wall(c: &Civil) -> String {
    let mut c = c.clone();
    c.zone = Zone::Floating;
    crate::iso::format_iso(&c.to_json()).as_str().unwrap_or("this wall time").to_owned()
}

fn in_range(at: i128, lo: Q, hi: Q) -> bool {
    let at = Q::int(at);
    lo.cmp(at).is_some_and(|o| o.is_lt()) && at.cmp(hi).is_some_and(|o| o.is_le())
}

fn sub(l: Q, offset: i64) -> R<Q> {
    l.sub(Q::int(offset as i128)).map_or_else(|| crate::model::range_err(""), Ok)
}

/// The offset in force just before the forward transition whose gap contains wall time `l`.
fn offset_before_gap(zone: &ZoneRules, l: Q) -> R<i64> {
    let mut prev = zone.initial_offset;
    for (at, o) in &zone.transitions {
        if *o > prev {
            let start = sub(Q::int(*at), -prev)?;
            let end = sub(Q::int(*at), -*o)?;
            if start.cmp(l).is_some_and(|x| x.is_le()) && l.cmp(end).is_some_and(|x| x.is_lt()) {
                return Ok(prev);
            }
        }
        prev = *o;
    }
    fail(Status::Error, "gap", "", "no transition explains this gap")
}

/// The civil date-time of an instant in a named zone, with `"offsetAssertion"` set to the offset
/// in force. `unsupported` for zones the rules don't list and instants outside their window.
pub fn to_zone(instant: &Value, zone_id: &str, rules: &Rules) -> UtvResult {
    done("toZone", (|| {
        let t = read_instant(instant)?;
        let zone = rules.zone(zone_id)?;
        if !rules.within(t) {
            return window_err();
        }
        let offset = zone.offset_at(t);
        let mut c = civil_at(t, offset)?;
        c.zone = Zone::Iana(zone_id.to_owned());
        c.offset_assertion = Some(offset);
        Ok(UtvResult::unique(c.to_json()))
    })())
}

/// The civil date-time of an instant at a fixed offset (seconds east of UTC, at most ±18:00).
pub fn to_offset(instant: &Value, offset_seconds: i64) -> UtvResult {
    done("toOffset", (|| {
        let t = read_instant(instant)?;
        if offset_seconds.abs() > MAX_OFFSET {
            return fail(Status::Invalid, "zone.offset", "/offset", "offset beyond ±18:00");
        }
        let mut c = civil_at(t, offset_seconds)?;
        c.zone = Zone::Fixed(offset_seconds);
        Ok(UtvResult::unique(c.to_json()))
    })())
}

fn civil_at(t: Q, offset: i64) -> R<Civil> {
    let mut c = Civil::new(Form::DateTime);
    c.set_from_seconds(sub(t, -offset)?)?;
    Ok(c)
}
