//! ISO 8601 extended format and RFC 3339 / RFC 9557 text: [`parse_iso`] and [`format_iso`].
//!
//! ```
//! use utv::{parse_iso, format_iso, Status};
//! let r = parse_iso("2026-11-05T14:30:00+00:00"); // an offset of 0, kept as an offset
//! assert_eq!(format_iso(r.value().unwrap()).as_str(), Some("2026-11-05T14:30:00+00:00"));
//! let z = parse_iso("2026-11-05T14:30:00Z"); // UTC known, local offset not: an instant
//! assert_eq!(z.value().unwrap()["kind"], "instant");
//! assert_eq!(parse_iso("2026-02-30").status, Status::Invalid);
//! ```

use std::sync::OnceLock;

use regex::Regex;
use serde_json::Value;

use crate::model::{done, fail, instant_json, kind, read_civil, read_instant, Civil, Form, Zone, MAX_OFFSET, R};
use crate::num::Q;
use crate::{Status, UtvResult};

const YEAR: &str = r"([+-][0-9]{4,}|[0-9]{4})";

fn re(cell: &'static OnceLock<Regex>, pattern: &str) -> &'static Regex {
    cell.get_or_init(|| Regex::new(pattern).expect("valid pattern"))
}

fn re_date() -> &'static Regex {
    static R: OnceLock<Regex> = OnceLock::new();
    re(&R, &format!("^{YEAR}-([0-9]{{2}})-([0-9]{{2}})$"))
}
fn re_year_month() -> &'static Regex {
    static R: OnceLock<Regex> = OnceLock::new();
    re(&R, &format!("^{YEAR}-([0-9]{{2}})$"))
}
fn re_time() -> &'static Regex {
    static R: OnceLock<Regex> = OnceLock::new();
    re(&R, r"^([0-9]{2}):([0-9]{2})(?::([0-9]{2})(?:\.([0-9]+))?)?$")
}
fn re_date_time() -> &'static Regex {
    static R: OnceLock<Regex> = OnceLock::new();
    re(&R, &format!(r"^{YEAR}-([0-9]{{2}})-([0-9]{{2}})T([0-9]{{2}}):([0-9]{{2}})(?::([0-9]{{2}})(?:\.([0-9]+))?)?(Z|[+-][0-9]{{2}}:[0-9]{{2}})?((?:\[[^\[\]]*\])*)$"))
}
/// Valid ISO 8601 / RFC 9557 forms outside this profile: year only, week dates, ordinal
/// dates, basic format, comma fractions.
fn re_other_iso() -> &'static Regex {
    static R: OnceLock<Regex> = OnceLock::new();
    re(
        &R,
        &format!(
            r"^(?:{YEAR}|{YEAR}-W[0-9]{{2}}(?:-[1-7])?(?:T.*)?|{YEAR}-[0-9]{{3}}(?:T.*)?|[+-]?[0-9]{{8}}(?:T[0-9]{{2,6}}(?:[.,][0-9]+)?(?:Z|[+-][0-9]{{2}}(?::?[0-9]{{2}})?)?)?|.*[0-9]{{2}}:[0-9]{{2}}:[0-9]{{2}},[0-9]+.*)$"
        ),
    )
}
fn re_zone_id() -> &'static Regex {
    static R: OnceLock<Regex> = OnceLock::new();
    re(&R, r"^[A-Za-z][A-Za-z0-9_+\-]*(?:/[A-Za-z0-9_+\-]+)*$")
}
fn re_annotation() -> &'static Regex {
    static R: OnceLock<Regex> = OnceLock::new();
    re(&R, r"^!?(?:[a-z_][a-z0-9_\-]*=[A-Za-z0-9\-]+|[+-][0-9]{2}:[0-9]{2}|[A-Za-z][A-Za-z0-9_+\-]*(?:/[A-Za-z0-9_+\-]+)*)$")
}

fn invalid<T>(msg: impl Into<String>) -> R<T> {
    fail(Status::Invalid, "syntax", "", msg)
}

fn bad_field<T>(code: &str, msg: impl Into<String>) -> R<T> {
    fail(Status::Invalid, code, "", msg)
}

fn unsupported<T>(code: &str, msg: impl Into<String>) -> R<T> {
    fail(Status::Unsupported, code, "", msg)
}

fn num(s: &str) -> u32 {
    s.parse().unwrap_or(u32::MAX)
}

fn year(s: &str) -> R<i128> {
    if s.starts_with('-') && s[1..].bytes().all(|b| b == b'0') {
        return invalid("year -0 is not allowed");
    }
    s.parse::<i128>().or_else(|_| unsupported("implementation-limit", "year outside the i128 range"))
}

fn nano(frac: Option<&str>) -> R<Option<u32>> {
    match frac {
        None => Ok(None),
        Some(f) if f.len() > 9 => invalid("a fraction has 1 to 9 digits"),
        Some(f) => {
            let n = num(&format!("{f:0<9}"));
            Ok((n != 0).then_some(n))
        }
    }
}

/// Check the time fields shared by `time` and `datetime`. Hour 24 with zero minutes and seconds
/// and second 60 are valid ISO forms that need policies this profile doesn't have.
fn check_time(c: &Civil, had_fraction: bool) -> R<()> {
    if c.hour == 24 && c.minute == 0 && c.second.unwrap_or(0) == 0 && !had_fraction {
        return unsupported("end-of-day", "24:00 needs an end-of-day policy");
    }
    if c.hour > 23 {
        return bad_field("field-out-of-range", format!("hour {} is out of range 0..=23", c.hour));
    }
    if c.minute > 59 {
        return bad_field("field-out-of-range", format!("minute {} is out of range 0..=59", c.minute));
    }
    match c.second {
        Some(60) => unsupported("leap-second", "second 60 needs a leap-second table"),
        Some(s) if s > 60 => bad_field("field-out-of-range", format!("second {s} is out of range 0..=59")),
        _ => Ok(()),
    }
}

fn check_date(c: &Civil) -> R<()> {
    if !(1..=12).contains(&c.month) {
        return bad_field("month-out-of-range", format!("there is no month {}", c.month));
    }
    if c.form.has_date() && (c.day < 1 || c.day > crate::num::days_in_month(c.year, c.month)) {
        return bad_field("day-out-of-range", format!("{} has no day {}", crate::model::year_month_text(c.year, c.month), c.day));
    }
    Ok(())
}

/// Parse ISO 8601 extended / RFC 3339 / RFC 9557 text into a UTV value.
///
/// - `YYYY-MM-DD` → date, `YYYY-MM` → year-month, `hh:mm[:ss[.f]]` → time,
///   `YYYY-MM-DDThh:mm[:ss[.f]]` → unzoned date-time;
/// - `±hh:mm` → fixed-offset date-time (`+00:00` is an offset of 0);
/// - `Z` or `-00:00` → instant (UTC known, local offset not);
/// - `[Area/Location]` → zoned date-time, with `offsetAssertion` when an offset precedes it.
///
/// Out-of-grammar text and non-existent fields are `invalid`; valid ISO/RFC forms this profile
/// doesn't handle (leap seconds, 24:00, week and ordinal dates, basic format, critical
/// annotations, `Z[zone]`) are `unsupported`.
pub fn parse_iso(text: &str) -> UtvResult {
    done("parseIso", parse(text).map(UtvResult::unique))
}

fn parse(text: &str) -> R<Value> {
    if let Some(m) = re_date().captures(text) {
        let mut c = Civil::new(Form::Date);
        (c.year, c.month, c.day) = (year(&m[1])?, num(&m[2]), num(&m[3]));
        check_date(&c)?;
        return Ok(c.to_json());
    }
    if let Some(m) = re_year_month().captures(text) {
        let mut c = Civil::new(Form::YearMonth);
        (c.year, c.month) = (year(&m[1])?, num(&m[2]));
        check_date(&c)?;
        return Ok(c.to_json());
    }
    if let Some(m) = re_time().captures(text) {
        let mut c = Civil::new(Form::Time);
        (c.hour, c.minute, c.second) = (num(&m[1]), num(&m[2]), m.get(3).map(|s| num(s.as_str())));
        let frac = m.get(4).map(|f| f.as_str());
        c.nano = nano(frac)?;
        check_time(&c, frac.is_some_and(|f| num(f) != 0))?;
        return Ok(c.to_json());
    }
    if let Some(m) = re_date_time().captures(text) {
        let mut c = Civil::new(Form::DateTime);
        (c.year, c.month, c.day) = (year(&m[1])?, num(&m[2]), num(&m[3]));
        (c.hour, c.minute, c.second) = (num(&m[4]), num(&m[5]), m.get(6).map(|s| num(s.as_str())));
        let frac = m.get(7).map(|f| f.as_str());
        c.nano = nano(frac)?;
        check_date(&c)?;
        check_time(&c, frac.is_some_and(|f| num(f) != 0))?;
        let offset = m.get(8).map(|o| o.as_str());
        let zone = annotation(m.get(9).map_or("", |a| a.as_str()))?;
        return match (offset, zone) {
            (Some("Z" | "-00:00"), Some(_)) => unsupported("unsupported-annotation", "`Z` or `-00:00` followed by a [zone] is not handled by this profile"),
            (Some("Z" | "-00:00"), None) => {
                let l = c.local_seconds()?;
                Ok(instant_json(l))
            }
            (Some(o), z) => {
                let secs = offset_seconds(o)?;
                match z {
                    Some(id) => {
                        c.zone = Zone::Iana(id);
                        c.offset_assertion = Some(secs);
                    }
                    None => c.zone = Zone::Fixed(secs),
                }
                Ok(c.to_json())
            }
            (None, Some(id)) => {
                c.zone = Zone::Iana(id);
                Ok(c.to_json())
            }
            (None, None) => Ok(c.to_json()),
        };
    }
    if re_other_iso().is_match(text) {
        return unsupported("unsupported-form", format!("`{text}` is a valid ISO 8601 form this profile doesn't handle (week, ordinal, year-only or basic format)"));
    }
    invalid(format!("`{text}` is not ISO 8601 extended / RFC 3339 / RFC 9557 text"))
}

fn offset_seconds(o: &str) -> R<i64> {
    let sign = if o.starts_with('-') { -1 } else { 1 };
    let (h, m) = (num(&o[1..3]) as i64, num(&o[4..6]) as i64);
    if m > 59 || h * 3600 + m * 60 > MAX_OFFSET {
        return bad_field("offset-out-of-range", format!("offset {o} is beyond ±18:00"));
    }
    Ok(sign * (h * 3600 + m * 60))
}

/// The bracketed RFC 9557 suffix: nothing, or exactly one non-critical `[Area/Location]`.
fn annotation(suffix: &str) -> R<Option<String>> {
    if suffix.is_empty() {
        return Ok(None);
    }
    let parts: Vec<&str> = suffix.trim_start_matches('[').trim_end_matches(']').split("][").collect();
    if let Some(bad) = parts.iter().find(|p| !re_annotation().is_match(p)) {
        return invalid(format!("`[{bad}]` is not an RFC 9557 annotation"));
    }
    if parts.iter().any(|p| p.starts_with('!')) {
        return unsupported("unsupported-annotation", "critical annotations ([!…]) are not handled by this profile");
    }
    match parts.as_slice() {
        [zone] if re_zone_id().is_match(zone) => Ok(Some((*zone).to_owned())),
        _ => unsupported("unsupported-annotation", "only a single [Area/Location] annotation is handled by this profile"),
    }
}

// ---------- formatting ----------

pub(crate) fn fmt_year(y: i128) -> String {
    if (0..=9999).contains(&y) {
        format!("{y:04}")
    } else {
        format!("{}{:04}", if y < 0 { '-' } else { '+' }, y.unsigned_abs())
    }
}

fn fmt_fraction(nano: u32) -> String {
    if nano == 0 {
        return String::new();
    }
    format!(".{}", format!("{nano:09}").trim_end_matches('0'))
}

fn fmt_offset(s: i64) -> R<String> {
    if s % 60 != 0 {
        return unsupported("no-text-form", "offsets with seconds can't be written as ±hh:mm");
    }
    let a = s.abs();
    Ok(format!("{}{:02}:{:02}", if s < 0 { '-' } else { '+' }, a / 3600, a / 60 % 60))
}

fn fmt_time(c: &Civil, always_seconds: bool) -> String {
    let mut s = format!("{:02}:{:02}", c.hour, c.minute);
    let nano = c.nano.unwrap_or(0);
    if always_seconds || c.second.is_some() || nano != 0 {
        s += &format!(":{:02}{}", c.second.unwrap_or(0), fmt_fraction(nano));
    }
    s
}

/// Format a UTV value as ISO 8601 / RFC 3339 / RFC 9557 text: the inverse of [`parse_iso`].
///
/// The answer is a `unique` result whose candidate is a JSON string. Instants print as UTC with
/// seconds (`…Z`); offsets as `±hh:mm`; zoned values as `…[Zone]`, after the asserted offset
/// if there is one. Anything else is `unsupported`; a non-existent date is `invalid`.
pub fn format_iso(value: &Value) -> UtvResult {
    done("formatIso", format(value).map(|s| UtvResult::unique(Value::String(s))))
}

fn format(value: &Value) -> R<String> {
    match kind(value) {
        "instant" => {
            let q = read_instant(value)?;
            if !terminates(q.frac().d) {
                return unsupported("no-text-form", "the fraction of a second has no finite decimal form");
            }
            let mut c = Civil::new(Form::DateTime);
            c.set_from_seconds(Q::int(q.floor()))?;
            let frac = decimal_fraction(q.frac())?;
            Ok(format!("{}-{:02}-{:02}T{:02}:{:02}:{:02}{frac}Z", fmt_year(c.year), c.month, c.day, c.hour, c.minute, c.second.unwrap_or(0)))
        }
        "civil" => {
            let c = read_civil(value)?;
            let date = format!("{}-{:02}", fmt_year(c.year), c.month);
            Ok(match c.form {
                Form::YearMonth => date,
                Form::Date => format!("{date}-{:02}", c.day),
                Form::Time => fmt_time(&c, false),
                Form::DateTime => {
                    let mut s = format!("{date}-{:02}T{}", c.day, fmt_time(&c, false));
                    match &c.zone {
                        Zone::Floating => {}
                        Zone::Fixed(o) => s += &fmt_offset(*o)?,
                        Zone::Iana(id) => {
                            if let Some(a) = c.offset_assertion {
                                s += &fmt_offset(a)?;
                            }
                            s += &format!("[{id}]");
                        }
                        Zone::Other(t) => return unsupported("unsupported-operand", format!("zone type `{t}` has no ISO 8601 form")),
                    }
                    s
                }
            })
        }
        k => {
            crate::model::structural(value)?;
            unsupported("unsupported-operand", format!("kind `{k}` has no ISO 8601 form in this profile"))
        }
    }
}

fn terminates(mut d: i128) -> bool {
    while d % 2 == 0 {
        d /= 2;
    }
    while d % 5 == 0 {
        d /= 5;
    }
    d == 1
}

/// `.ddd` with the fewest digits, for a fraction in `[0, 1)` whose decimal form terminates.
fn decimal_fraction(f: Q) -> R<String> {
    if f.n == 0 {
        return Ok(String::new());
    }
    let mut digits = String::from(".");
    let (mut n, d) = (f.n, f.d);
    while n != 0 {
        n = match n.checked_mul(10) {
            Some(n) => n,
            None => return crate::model::range_err("/value/subTick"),
        };
        digits.push(char::from(b'0' + (n / d) as u8));
        n %= d;
    }
    Ok(digits)
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn round_trips() {
        for t in ["2026-11-05", "2026-11", "09:30", "09:30:15.25", "2026-11-05T14:30", "2026-11-05T14:30:00+01:00", "2026-11-05T14:30-05:00", "2027-06-01T09:00[Europe/Paris]", "2027-06-01T09:00+02:00[Europe/Paris]", "+275760-09-13", "-0500-03-01", "0000-01-01", "1970-01-01T00:00:00.5Z", "1969-12-31T23:59:59Z"] {
            let v = parse_iso(t);
            assert_eq!(v.status, Status::Unique, "{t}: {v:?}");
            assert_eq!(format_iso(v.value().unwrap()).as_str(), Some(t), "{t}");
        }
    }

    #[test]
    fn outcomes() {
        for (t, s) in [
            ("2026-11-05 14:30", Status::Invalid),
            ("2026-11-05T14:30z", Status::Invalid),
            ("2026-11-05T24:30", Status::Invalid),
            ("2026-11-05T14:30:00.1234567890", Status::Invalid),
            ("-0000-01-01", Status::Invalid),
            ("2026-11-05T14:30Z[Europe/Paris]", Status::Unsupported),
            ("2026-11-05T14:30[u-ca=hebrew]", Status::Unsupported),
            ("2026", Status::Unsupported),
            ("20261105T1430Z", Status::Unsupported),
            ("24:00", Status::Unsupported),
        ] {
            assert_eq!(parse_iso(t).status, s, "{t}");
        }
    }

    #[test]
    fn non_terminating_fraction_is_unsupported() {
        let v = serde_json::json!({"schema":"utv/1.0","kind":"instant","value":{"system":crate::model::posix(),"ticks":"0","subTick":{"numerator":"1","denominator":"3"}}});
        assert_eq!(format_iso(&v).status, Status::Unsupported);
    }
}
