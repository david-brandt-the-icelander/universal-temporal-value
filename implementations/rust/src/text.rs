//! The UTV text form (profile `utv-text/1`, `docs/text-form.md`): [`parse_text`] and
//! [`format_text`].
//!
//! The text form is ISO 8601 / RFC 9557 for points (exactly what [`parse_iso`] and
//! [`format_iso`] handle), ISO 8601 durations for periods (`P1M`, `-P1DT2H`), and bracket
//! notation for intervals (`[2026-11-01, 2026-12-01)`, `[2026-11-01, ..)`, `[2026-11-01, ?)`).
//! EDTF slash intervals (`2026-11-01/2026-11-30`) are accepted on input and read as closed.
//! It is defined only where it is lossless, so `parse_text(format_text(v)) == v`.
//!
//! ```
//! use utv::{format_text, parse_text, Status};
//! let november = parse_text("[2026-11-01, 2026-12-01)");
//! assert_eq!(format_text(november.value().unwrap()).as_str(), Some("[2026-11-01, 2026-12-01)"));
//! assert_eq!(parse_text("2026-11-01/2026-11-30").status, Status::Unique); // EDTF: closed
//! assert_eq!(format_text(&utv::posix_seconds(60)).status, Status::Unsupported); // no text form
//! ```

use std::sync::OnceLock;

use regex::Regex;
use serde_json::{json, Map, Value};

use crate::build::{interval_with, Endpoint};
use crate::iso::{format_iso, parse_iso};
use crate::model::{done, fail, kind, read_amount, structural, Amount, R};
use crate::{Status, UtvResult};

fn re(cell: &'static OnceLock<Regex>, pattern: &str) -> &'static Regex {
    cell.get_or_init(|| Regex::new(pattern).expect("valid pattern"))
}

fn re_period() -> &'static Regex {
    static R: OnceLock<Regex> = OnceLock::new();
    re(&R, r"^(-)?P(?:([0-9]+)Y)?(?:([0-9]+)M)?(?:([0-9]+)W)?(?:([0-9]+)D)?(?:T(?:([0-9]+)H)?(?:([0-9]+)M)?(?:([0-9]+)S)?)?$")
}

/// A period with a decimal fraction somewhere: valid ISO 8601, outside this profile.
fn re_period_fraction() -> &'static Regex {
    static R: OnceLock<Regex> = OnceLock::new();
    re(&R, r"^-?P(?:[0-9]+(?:[.,][0-9]+)?[YMWD])*(?:T(?:[0-9]+(?:[.,][0-9]+)?[HMS])+)?$")
}

/// EDTF level 1/2 qualifiers and unspecified digits (`1347~`, `1347?`, `2004-06-1X`).
fn re_edtf_qualified() -> &'static Regex {
    static R: OnceLock<Regex> = OnceLock::new();
    re(&R, r"^[~?%]?[+-]?[0-9X]{4,}(?:-[~?%]?[0-9X]{2}(?:-[~?%]?[0-9X]{2})?)?[~?%]?$")
}

fn syntax<T>(msg: impl Into<String>) -> R<T> {
    fail(Status::Invalid, "syntax", "", msg)
}

fn no_text_form<T>(msg: impl Into<String>) -> R<T> {
    fail(Status::Unsupported, "no-text-form", "", msg)
}

// ---------- parsing ----------

/// Parse UTV text: a point (ISO 8601 / RFC 3339 / RFC 9557, as [`parse_iso`]), a period
/// (`P1Y2M10DT2H30M`, `-P1M`), an interval in bracket notation (`[a, b)`, `(.., b)`, `[a, ?)`)
/// or an EDTF interval (`a/b`, `a/..`, `../b`, `a/`, `/b`; closed where it has points).
///
/// Bad text is `invalid` (`syntax`, or the field's own code); valid notation outside this
/// profile is `unsupported` (`unsupported-form`). Reversed intervals are `invalid`
/// (`reversed-interval`), and intervals mixing kinds or forms are `invalid` (`kind-mismatch`).
pub fn parse_text(text: &str) -> UtvResult {
    done("parseText", parse(text).map(UtvResult::unique))
}

fn parse(text: &str) -> R<Value> {
    if text.starts_with('[') || text.starts_with('(') {
        return bracket_interval(text);
    }
    if text.starts_with('P') || text.starts_with("-P") {
        return period(text);
    }
    if let Some(slash) = slash_outside_brackets(text) {
        return edtf_interval(&text[..slash], &text[slash + 1..]);
    }
    point(text)
}

/// A single point: the ISO 8601 / RFC 9557 grammar of `parse_iso`, after refusing EDTF
/// qualifiers (they need a mapping to UTV's `uncertain` kind, which isn't defined yet).
fn point(text: &str) -> R<Value> {
    if re_edtf_qualified().is_match(text) && text.contains(['~', '?', '%', 'X']) {
        return fail(Status::Unsupported, "unsupported-form", "", format!("`{text}` uses EDTF qualifiers, which this profile doesn't map yet"));
    }
    let r = parse_iso(text);
    match r.value() {
        Some(v) => Ok(v.clone()),
        None => Err(r),
    }
}

fn period(text: &str) -> R<Value> {
    let Some(m) = re_period().captures(text) else {
        if re_period_fraction().is_match(text) && text.contains(['.', ',']) {
            return fail(Status::Unsupported, "unsupported-form", "", "period components with decimal fractions are not handled: calendar components are whole numbers");
        }
        return syntax(format!("`{text}` is not an ISO 8601 period (P[nY][nM][nW][nD][T[nH][nM][nS]])"));
    };
    let has = |i: usize| m.get(i).is_some();
    if !(2..=8).any(has) {
        return syntax("a period needs at least one component");
    }
    if text.contains('T') && !(6..=8).any(has) {
        return syntax("`T` must be followed by hours, minutes or seconds");
    }
    let negative = m.get(1).is_some();
    let mut components = Map::new();
    for (i, name) in [(2, "years"), (3, "months"), (4, "weeks"), (5, "days"), (6, "hours"), (7, "minutes"), (8, "seconds")] {
        let Some(digits) = m.get(i) else { continue };
        let Ok(n) = digits.as_str().parse::<i128>() else {
            return fail(Status::Unsupported, "implementation-limit", "", "period component outside the i128 range");
        };
        if n != 0 {
            components.insert(name.into(), Value::String(if negative { format!("-{n}") } else { n.to_string() }));
        }
    }
    Ok(json!({"schema": crate::SPEC_VERSION, "kind": "amount", "value": {"basis": "calendar", "components": components}}))
}

fn slash_outside_brackets(text: &str) -> Option<usize> {
    let mut depth = 0i32;
    for (i, ch) in text.char_indices() {
        match ch {
            '[' => depth += 1,
            ']' => depth -= 1,
            '/' if depth == 0 => return Some(i),
            _ => {}
        }
    }
    None
}

/// One end of a written interval, before the points are checked against each other.
enum End {
    Point(Value, bool),
    Unbounded,
    Unknown,
}

fn bracket_interval(text: &str) -> R<Value> {
    let (open, rest) = text.split_at(1);
    let Some(close) = rest.chars().last().filter(|c| *c == ')' || *c == ']') else {
        return syntax("an interval ends with `)` or `]`");
    };
    let inner = &rest[..rest.len() - 1];
    let Some((a, b)) = inner.split_once(',') else {
        return syntax("an interval has two endpoints separated by `, `");
    };
    let (a, b) = (a, b.trim_start());
    if a.is_empty() || b.is_empty() || a.ends_with(char::is_whitespace) || b.contains(',') {
        return syntax("an interval is written `[start, end)`");
    }
    let end_of = |token: &str, open_bracket: bool, at: &str| -> R<End> {
        match token {
            ".." | "?" if !open_bracket => syntax(format!("an {} {at} must sit on an open bracket", if token == ".." { "unbounded" } else { "unknown" })),
            ".." => Ok(End::Unbounded),
            "?" => Ok(End::Unknown),
            point_text => Ok(End::Point(point(point_text)?, !open_bracket)),
        }
    };
    let start = end_of(a, open == "(", "start")?;
    let end = end_of(b, close == ')', "end")?;
    interval(start, end)
}

fn edtf_interval(a: &str, b: &str) -> R<Value> {
    let end_of = |token: &str| -> R<End> {
        match token {
            "" => Ok(End::Unknown),
            ".." => Ok(End::Unbounded),
            p => Ok(End::Point(point(p)?, true)),
        }
    };
    if a.is_empty() && b.is_empty() {
        return syntax("`/` alone is not an interval");
    }
    interval(end_of(a)?, end_of(b)?)
}

/// The kind of a point plus, for civil values, its form and zone type: the points of one
/// interval must agree on all three.
fn shape(v: &Value) -> (String, String, String) {
    let inner = &v["value"];
    (kind(v).to_owned(), inner["form"].as_str().unwrap_or("").to_owned(), inner["zone"]["type"].as_str().unwrap_or("").to_owned())
}

fn interval(start: End, end: End) -> R<Value> {
    if !matches!(start, End::Point(..)) && !matches!(end, End::Point(..)) {
        return fail(Status::Unsupported, "unsupported-form", "", "an interval without a point has no domain");
    }
    if let (End::Point(a, _), End::Point(b, _)) = (&start, &end) {
        if shape(a) != shape(b) {
            return fail(Status::Invalid, "kind-mismatch", "", "both endpoints of an interval must be of the same kind and form");
        }
        if crate::ops::compare(a, b).as_str() == Some("after") {
            return fail(Status::Invalid, "reversed-interval", "", "the interval ends before it starts");
        }
    }
    let endpoint = |e: End| match e {
        End::Point(p, true) => Endpoint::Included(p),
        End::Point(p, false) => Endpoint::Excluded(p),
        End::Unbounded => Endpoint::Unbounded,
        End::Unknown => Endpoint::Unknown,
    };
    Ok(interval_with(endpoint(start), endpoint(end)))
}

// ---------- formatting ----------

/// Format a UTV value as text: the inverse of [`parse_text`].
///
/// Points print as [`format_iso`] does, calendar periods as ISO 8601 durations, and start-end
/// intervals in bracket notation. Values the text form can't express losslessly (exact second
/// counts, mixed-sign periods, start-amount intervals, other kinds, extra properties such as
/// `provenance` or a `resolutionPolicy`) are `unsupported` with diagnostic `no-text-form`.
pub fn format_text(value: &Value) -> UtvResult {
    done("formatText", format(value).map(|s| UtvResult::unique(Value::String(s))))
}

fn only_keys(v: &Value, allowed: &[&str]) -> R<()> {
    match v.as_object().and_then(|o| o.keys().find(|k| !allowed.contains(&k.as_str()))) {
        Some(extra) => no_text_form(format!("the text form has no place for `{extra}`")),
        None => Ok(()),
    }
}

fn format(value: &Value) -> R<String> {
    structural(value)?;
    only_keys(value, &["schema", "kind", "value"])?;
    let inner = &value["value"];
    match kind(value) {
        "civil" => {
            only_keys(inner, &["form", "fields", "calendar", "zone", "zoneIntent", "offsetAssertion"])?;
            let asserted = inner.get("offsetAssertion").is_some();
            let lossless = match (inner["form"].as_str(), inner["zone"]["type"].as_str(), inner["zoneIntent"].as_str()) {
                (_, None | Some("floating"), None) => !asserted,
                (Some("datetime"), Some("iana"), Some("follow-zone-id")) => true,
                (Some("datetime"), Some("fixed-offset"), Some("fixed-offset")) => !asserted,
                _ => false,
            };
            if !lossless {
                return no_text_form("this combination of form, zone and zone intent has no text form");
            }
            iso(value)
        }
        "instant" => {
            only_keys(inner, &["system", "ticks", "subTick"])?;
            iso(value)
        }
        "amount" => match read_amount(value)? {
            Amount::Calendar(c) => {
                only_keys(inner, &["basis", "components"])?;
                let parts = [c.years, c.months, c.weeks, c.days, c.hours, c.minutes, c.seconds];
                if parts.iter().any(|n| *n < 0) && parts.iter().any(|n| *n > 0) {
                    return no_text_form("a period with mixed signs has no ISO 8601 form");
                }
                let negative = parts.iter().any(|n| *n < 0);
                let mut s = String::from(if negative { "-P" } else { "P" });
                for (n, unit) in [(c.years, 'Y'), (c.months, 'M'), (c.weeks, 'W'), (c.days, 'D')] {
                    if n != 0 {
                        s += &format!("{}{unit}", n.unsigned_abs());
                    }
                }
                if c.hours != 0 || c.minutes != 0 || c.seconds != 0 {
                    s.push('T');
                    for (n, unit) in [(c.hours, 'H'), (c.minutes, 'M'), (c.seconds, 'S')] {
                        if n != 0 {
                            s += &format!("{}{unit}", n.unsigned_abs());
                        }
                    }
                }
                Ok(if s == "P" { "P0D".into() } else { s })
            }
            _ => no_text_form("exact second counts have no standard notation that carries their basis; they have a display string only"),
        },
        "interval" => {
            only_keys(inner, &["domain", "start", "end", "construction"])?;
            if inner.get("construction").is_some_and(|c| c != "start-end") {
                return no_text_form("only start-end intervals have a text form");
            }
            let end = |b: &Value, is_start: bool| -> R<String> {
                Ok(match b["state"].as_str().unwrap_or("") {
                    "unbounded" => if is_start { "(..".into() } else { "..)".into() },
                    "unknown" => if is_start { "(?".into() } else { "?)".into() },
                    state => {
                        let p = iso(&b["point"])?;
                        let included = state == "included";
                        match (is_start, included) {
                            (true, true) => format!("[{p}"),
                            (true, false) => format!("({p}"),
                            (false, true) => format!("{p}]"),
                            (false, false) => format!("{p})"),
                        }
                    }
                })
            };
            if [&inner["start"], &inner["end"]].iter().all(|b| matches!(b["state"].as_str(), Some("unbounded" | "unknown"))) {
                return no_text_form("an interval without a point has no domain in the text form");
            }
            let text = format!("{}, {}", end(&inner["start"], true)?, end(&inner["end"], false)?);
            let expected_domain = match parse(&text).ok() {
                Some(back) => back["value"]["domain"].clone(),
                None => return no_text_form("this interval doesn't read back from its text"),
            };
            if inner["domain"] != expected_domain {
                return no_text_form("this interval's domain can't be told from its text");
            }
            Ok(text)
        }
        k => no_text_form(format!("kind `{k}` has no text form; write it as JSON")),
    }
}

fn iso(value: &Value) -> R<String> {
    let r = format_iso(value);
    match r.as_str() {
        Some(s) => Ok(s.to_owned()),
        None => Err(r),
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn round_trips() {
        for t in ["2026-11-05", "P1M", "PT1M", "-P1DT2H", "P0D", "[2026-11-01, ..)", "(?, 2026-12-01]", "[2026-11-05T09:00[Europe/Paris], 2026-11-05T10:00[Europe/Paris])"] {
            let v = parse_text(t);
            assert_eq!(v.status, Status::Unique, "{t}: {v:?}");
            assert_eq!(format_text(v.value().unwrap()).as_str(), Some(t), "{t}");
        }
    }

    #[test]
    fn outcomes() {
        for (t, s, code) in [
            ("PT", Status::Invalid, "syntax"),
            ("P1M2Y", Status::Invalid, "syntax"),
            ("P-1M", Status::Invalid, "syntax"),
            ("[.., 2026-12-01)", Status::Invalid, "syntax"),
            ("[2026-11-01 2026-12-01)", Status::Invalid, "syntax"),
            ("/", Status::Invalid, "syntax"),
            ("(.., ?)", Status::Unsupported, "unsupported-form"),
            ("../", Status::Unsupported, "unsupported-form"),
            ("2026-11-0X", Status::Unsupported, "unsupported-form"),
            ("1347?/1350", Status::Unsupported, "unsupported-form"),
            ("2026-11-01/2026-10-01", Status::Invalid, "reversed-interval"),
        ] {
            let r = parse_text(t);
            assert_eq!((r.status, r.diagnostics.first().map(|d| d.code.as_str())), (s, Some(code)), "{t}");
        }
        assert_eq!(parse_text("2026-11-01/2026-11-30").operation, "parseText");
    }

    #[test]
    fn lossy_values_have_no_text_form() {
        let mut v = parse_iso("2026-11-05T09:00[Europe/Paris]").value().unwrap().clone();
        v["value"]["resolutionPolicy"] = json!({"gap": "reject"});
        assert_eq!(format_text(&v).diagnostics[0].code, "no-text-form");
        let no_points = interval_with(Endpoint::Unbounded, Endpoint::Unknown);
        assert_eq!(format_text(&no_points).diagnostics[0].code, "no-text-form");
    }
}
