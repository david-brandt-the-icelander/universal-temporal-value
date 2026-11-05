//! Everyday date/time tasks, side by side: what you write today in Rust, and the same with UTV.
//!
//! Run with `cargo run --example everyday --features chrono`.
//!
//! Rust's standard library has no calendar types: `std::time` only has `SystemTime` (a clock
//! reading) and `Duration`. So the native side uses the de-facto standard crate `chrono`, plus
//! `chrono-tz` for named zones. Native outputs are printed and never checked: they depend on the
//! host clock and zone, and on the copy of tzdb compiled into chrono-tz. UTV outputs are
//! deterministic, so this example checks each one and exits non-zero if any differs.

use std::time::{SystemTime, UNIX_EPOCH};

use chrono::{DateTime, Datelike, Days, Local, Months, NaiveDate, NaiveTime, TimeDelta, TimeZone, Utc};
use chrono_tz::Tz;
use serde_json::{json, Value};
use utv::build::{date, date_time, in_zone, instant_with_nanos, interval, interval_with, period, posix_seconds, time, Endpoint, Period};
use utv::interop::chrono::{from_fixed_offset, from_local, from_naive_date, to_fixed_offset, to_naive_date, OffsetIntent};
use utv::{add, compare, contains, days_between, fields, format_iso, interop, overlaps, parse_iso, posix_seconds_between, resolve, to_zone, Rules, UtvResult};

fn main() {
    let mismatches = run();
    println!();
    if mismatches == 0 {
        println!("Every UTV outcome as expected.");
    } else {
        println!("{mismatches} UTV outcomes differ from what this example expects.");
        std::process::exit(1);
    }
}

/// Runs every section and returns the number of UTV outcomes that differ from the expected ones.
pub fn run() -> usize {
    // The library never reads files or the host tzdb: the example loads the rules and passes them in.
    let path = concat!(env!("CARGO_MANIFEST_DIR"), "/../../conformance/rules/demo-zones.json");
    let text = std::fs::read_to_string(path).unwrap_or_else(|e| panic!("{path}: {e}"));
    let rules = Rules::parse(&text).unwrap_or_else(|e| panic!("{path}: {e}"));

    let mut t = Tour::default();
    now(&mut t);
    a_date(&mut t);
    invalid_dates(&mut t);
    adding(&mut t);
    days_between_dates(&mut t);
    times(&mut t);
    offsets(&mut t);
    time_zones(&mut t, &rules);
    dst_gap(&mut t, &rules);
    dst_overlap(&mut t, &rules);
    timestamps(&mut t);
    durations_vs_periods(&mut t, &rules);
    ranges(&mut t);
    round_trip(&mut t);
    t.mismatches
}

// ---------- 1 ----------

fn now(t: &mut Tour) {
    t.section("1. Now");
    let reading = SystemTime::now();
    let since_epoch = reading.duration_since(UNIX_EPOCH).map_or_else(|e| format!("-{:?}", e.duration()), |d| format!("{d:?}"));
    t.native("SystemTime::now().duration_since(UNIX_EPOCH)", since_epoch);
    t.native("Utc::now()", Utc::now());
    t.native("Local::now()", Local::now());
    t.note("Local::now() used this host's zone (TZ or /etc/localtime); every reading depends on the host clock.");

    // The library never reads the clock. The example reads it and converts the reading explicitly.
    let r = interop::from_system_time(reading);
    t.utv_status("interop::from_system_time(reading)", &r, "unique");
    t.note("Deliberate: utv has no now(). You pass the reading in, so the clock dependency is visible and testable.");
}

// ---------- 2 ----------

fn a_date(t: &mut Tour) {
    t.section("2. A date");
    let d = NaiveDate::from_ymd_opt(2026, 11, 5).expect("exists");
    t.native("NaiveDate::from_ymd_opt(2026, 11, 5)", d);
    t.native("d.format(\"%d %B %Y\")", d.format("%d %B %Y"));
    let p: NaiveDate = "2026-11-05".parse().expect("parses");
    t.native("p.weekday(), p.iso_week()", format!("{}, {:?}", p.weekday(), p.iso_week()));

    let d = date(2026, 11, 5);
    t.utv("format_iso(&date(2026, 11, 5))", &format_iso(&d), "unique 2026-11-05");
    let p = parse_iso("2026-11-05");
    t.utv("parse_iso(\"2026-11-05\")", &p, "unique 2026-11-05");
    t.utv(
        "fields(&p)",
        &fields(p.value().expect("unique")),
        r#"unique {"dayOfWeek":4,"dayOfYear":309,"isoWeekYear":"2026","isoWeek":45,"daysInMonth":30,"isLeapYear":false}"#,
    );
}

// ---------- 3 ----------

fn invalid_dates(t: &mut Tour) {
    t.section("3. Invalid dates");
    t.native("NaiveDate::from_ymd_opt(2026, 2, 30)", format!("{:?}", NaiveDate::from_ymd_opt(2026, 2, 30)));
    t.native("\"2026-02-30\".parse::<NaiveDate>()", format!("{:?}", "2026-02-30".parse::<NaiveDate>()));
    t.note("chrono neither panics nor rolls over: the constructor gives None and parse gives Err(OutOfRange).");

    let feb30 = date(2026, 2, 30); // a well-formed value of a date that doesn't exist
    t.utv("fields(&date(2026, 2, 30))", &fields(&feb30), "invalid");
    t.utv("parse_iso(\"2026-02-30\")", &parse_iso("2026-02-30"), "invalid");
    t.note("Same refusal, but as a result with a diagnostic saying which field is wrong.");
}

// ---------- 4 ----------

fn adding(t: &mut Tour) {
    t.section("4. Adding");
    let d = NaiveDate::from_ymd_opt(2026, 11, 5).expect("exists");
    let jan31 = NaiveDate::from_ymd_opt(2026, 1, 31).expect("exists");
    t.native("d + Days::new(30)", d + Days::new(30));
    t.native("d + Months::new(1)", d + Months::new(1));
    t.native("jan31 + Months::new(1)", jan31 + Months::new(1));
    t.note("chrono clamped 31 January + 1 month to 28 February without saying so.");

    let d = date(2026, 11, 5);
    let jan31 = date(2026, 1, 31);
    let month = period(Period::months(1));
    t.utv("add(&d, &period(Period::days(30)), None)", &add(&d, &period(Period::days(30)), None), "unique 2026-12-05");
    t.utv("add(&d, &month, None)", &add(&d, &month, None), "unique 2026-12-05");
    t.utv("add(&jan31, &month, None)", &add(&jan31, &month, None), "none");
    let constrain = json!({"monthEnd": "constrain"});
    t.utv("add(&jan31, &month, Some(&constrain))", &add(&jan31, &month, Some(&constrain)), "unique 2026-02-28");
    t.note("UTV's default is `none` (there is no 31 February); clamping is an explicit policy.");
}

// ---------- 5 ----------

fn days_between_dates(t: &mut Tour) {
    t.section("5. Days between");
    let (a, b) = (NaiveDate::from_ymd_opt(2026, 11, 5).expect("exists"), NaiveDate::from_ymd_opt(2026, 12, 25).expect("exists"));
    t.native("(b - a).num_days()", (b - a).num_days());
    t.native("a < b", a < b);

    let (a, b) = (date(2026, 11, 5), date(2026, 12, 25));
    t.utv("days_between(&a, &b)", &days_between(&a, &b), "unique 50");
    t.utv("compare(&a, &b)", &compare(&a, &b), "unique before");
    t.note("Same answers. UTV's compare refuses values whose order depends on zone rules instead of guessing.");
}

// ---------- 6 ----------

fn times(t: &mut Tour) {
    t.section("6. Times and date-times");
    t.native("NaiveTime::from_hms_opt(9, 30, 0)", NaiveTime::from_hms_opt(9, 30, 0).expect("exists"));
    let dt = NaiveDate::from_ymd_opt(2026, 11, 5).and_then(|d| d.and_hms_opt(23, 30, 0)).expect("exists");
    t.native("dt + TimeDelta::hours(2)", dt + TimeDelta::hours(2));

    t.utv("format_iso(&time(9, 30))", &format_iso(&time(9, 30)), "unique 09:30");
    let dt = date_time(2026, 11, 5, 23, 30);
    t.utv("add(&dt, &period(Period::hours(2)), None)", &add(&dt, &period(Period::hours(2)), None), "unique 2026-11-06T01:30");
    t.note("UTV keeps \"no seconds given\" (09:30, not 09:30:00); both carry into the next day.");
}

// ---------- 7 ----------

fn offsets(t: &mut Tour) {
    t.section("7. Parsing with offsets");
    for s in ["2026-11-05T14:30:00+01:00", "2026-11-05T14:30:00Z", "2026-11-05T14:30:00+00:00"] {
        t.native(&format!("DateTime::parse_from_rfc3339({s:?})"), DateTime::parse_from_rfc3339(s).map_or_else(|e| e.to_string(), |d| d.to_string()));
    }
    t.note("chrono gives the same DateTime<FixedOffset> (+00:00) for `Z` and `+00:00`: the difference is gone.");

    for (s, want, kind) in [
        ("2026-11-05T14:30:00+01:00", "unique 2026-11-05T14:30:00+01:00", "civil"),
        ("2026-11-05T14:30:00Z", "unique 2026-11-05T14:30:00Z", "instant"),
        ("2026-11-05T14:30:00+00:00", "unique 2026-11-05T14:30:00+00:00", "civil"),
    ] {
        let r = parse_iso(s);
        t.utv(&format!("parse_iso({s:?})"), &r, want);
        t.expect_eq("  kind", r.value().map_or("", |v| v["kind"].as_str().unwrap_or("")), kind);
    }
    t.note("`Z` is an instant (UTC known, local offset unknown); `+00:00` is a wall time at offset 0.");
}

// ---------- 8 ----------

fn zone(name: &str) -> Option<Tz> {
    name.parse::<Tz>().ok()
}

fn time_zones(t: &mut Tour, rules: &Rules) {
    t.section("8. Time zones");
    match (zone("Europe/Paris"), zone("America/New_York")) {
        (Some(paris), Some(new_york)) => {
            let m = paris.with_ymd_and_hms(2027, 6, 1, 9, 0, 0);
            t.native("Paris.with_ymd_and_hms(2027, 6, 1, 9, 0, 0)", format!("{m:?}"));
            if let Some(m) = m.single() {
                t.native("m.with_timezone(&New_York)", m.with_timezone(&new_york));
            }
        }
        _ => t.native("Europe/Paris, America/New_York", "zone not available in this chrono-tz build"),
    }
    t.note("chrono-tz uses the tzdb release compiled into the crate, not the host's zone files.");

    let meeting = in_zone(date_time(2027, 6, 1, 9, 0), "Europe/Paris");
    t.utv("in_zone(date_time(2027, 6, 1, 9, 0), \"Europe/Paris\")", &format_iso(&meeting), "unique 2027-06-01T09:00[Europe/Paris]");
    let at = resolve(&meeting, rules, None);
    t.utv("resolve(&meeting, &rules, None)", &at, "unique 2027-06-01T07:00:00Z");
    if let Some(at) = at.value() {
        t.utv("to_zone(&at, \"America/New_York\", &rules)", &to_zone(at, "America/New_York", rules), "unique 2027-06-01T03:00:00-04:00[America/New_York]");
    }
    t.note("UTV only uses the rules passed in (demo-zones: three zones, 2026-2027); anything else is `unsupported`.");
}

// ---------- 9 ----------

fn dst_gap(t: &mut Tour, rules: &Rules) {
    t.section("9. DST gap");
    match zone("America/New_York") {
        Some(ny) => t.native("New_York.with_ymd_and_hms(2026, 3, 8, 2, 30, 0)", format!("{:?}", ny.with_ymd_and_hms(2026, 3, 8, 2, 30, 0))),
        None => t.native("America/New_York", "zone not available in this chrono-tz build"),
    }
    t.note("chrono answers LocalResult::None too, and leaves choosing a replacement time to you.");

    let gap = in_zone(date_time(2026, 3, 8, 2, 30), "America/New_York");
    t.utv("resolve(&gap, &rules, None)", &resolve(&gap, rules, None), "none");
    let shift = json!({"gap": "shift-forward-by-gap", "overlap": "reject"});
    let r = resolve(&gap, rules, Some(&shift));
    t.utv("resolve(&gap, &rules, Some(&shift_forward))", &r, "unique 2026-03-08T07:30:00Z");
    if let Some(at) = r.value() {
        t.utv("to_zone(&at, \"America/New_York\", &rules)", &to_zone(at, "America/New_York", rules), "unique 2026-03-08T03:30:00-04:00[America/New_York]");
    }
    t.note("02:30 doesn't exist that day; the named policy `shift-forward-by-gap` gives 03:30 EDT.");
}

// ---------- 10 ----------

fn dst_overlap(t: &mut Tour, rules: &Rules) {
    t.section("10. DST overlap");
    match zone("America/New_York") {
        Some(ny) => {
            let r = ny.with_ymd_and_hms(2026, 11, 1, 1, 30, 0);
            t.native("New_York.with_ymd_and_hms(2026, 11, 1, 1, 30, 0)", format!("{r:?}"));
            t.native(".earliest()", format!("{:?}", r.earliest()));
        }
        None => t.native("America/New_York", "zone not available in this chrono-tz build"),
    }
    t.note("chrono reports both (LocalResult::Ambiguous); .single() would give None, .earliest() the EDT one.");

    let overlap = in_zone(date_time(2026, 11, 1, 1, 30), "America/New_York");
    t.utv("resolve(&overlap, &rules, None)", &resolve(&overlap, rules, None), "multiple 2026-11-01T05:30:00Z, 2026-11-01T06:30:00Z");
    let earliest = json!({"gap": "reject", "overlap": "earliest"});
    t.utv("resolve(&overlap, &rules, Some(&earliest))", &resolve(&overlap, rules, Some(&earliest)), "unique 2026-11-01T05:30:00Z");
}

// ---------- 11 ----------

fn timestamps(t: &mut Tour) {
    t.section("11. Timestamps");
    let a = DateTime::from_timestamp(1_793_885_400, 250_000_000).expect("in range");
    let b = DateTime::from_timestamp(1_793_971_800, 500_000_000).expect("in range");
    t.native("DateTime::from_timestamp(1_793_885_400, 250_000_000)", a);
    t.native("a.timestamp(), a.timestamp_subsec_nanos()", format!("{}, {}", a.timestamp(), a.timestamp_subsec_nanos()));
    t.native("a.timestamp_millis() as f64 / 1000.0", a.timestamp_millis() as f64 / 1000.0);
    t.native("b - a", b - a);

    let (a, b) = (instant_with_nanos(1_793_885_400, 250_000_000), instant_with_nanos(1_793_971_800, 500_000_000));
    t.utv("format_iso(&instant_with_nanos(1_793_885_400, 250_000_000))", &format_iso(&a), "unique 2026-11-05T13:30:00.25Z");
    t.utv("posix_seconds_between(&a, &b)", &posix_seconds_between(&a, &b), "unique 345601/4 POSIX s");
    t.note("UTV keeps the fraction as an exact rational (0.25 s is 1/4); no floating point anywhere.");
}

// ---------- 12 ----------

fn durations_vs_periods(t: &mut Tour, rules: &Rules) {
    t.section("12. Durations vs periods");
    t.native("TimeDelta::days(1).num_seconds()", TimeDelta::days(1).num_seconds());
    if let Some(ny) = zone("America/New_York") {
        if let Some(noon) = ny.with_ymd_and_hms(2026, 10, 31, 12, 0, 0).single() {
            t.native("noon_oct31_ny + TimeDelta::days(1)", noon + TimeDelta::days(1));
            t.native("noon_oct31_ny + Days::new(1)", noon.checked_add_days(Days::new(1)).map_or("None".into(), |d| d.to_string()));
        }
    } else {
        t.native("America/New_York", "zone not available in this chrono-tz build");
    }
    t.note("chrono has both: TimeDelta is exact seconds (11:00 after the clocks go back), Days is calendar days (12:00).");

    let day = period(Period::days(1));
    let secs = posix_seconds(86_400);
    t.utv("period(Period::days(1))", &UtvResult::unique(day.clone()), "unique period {\"days\":\"1\"}");
    t.utv("posix_seconds(86_400)", &UtvResult::unique(secs.clone()), "unique 86400 POSIX s");
    let noon = in_zone(date_time(2026, 10, 31, 12, 0), "America/New_York");
    let next = add(&noon, &day, None);
    t.utv("add(&noon, &period(days 1), None)", &next, "unique 2026-11-01T12:00[America/New_York]");
    if let Some(next) = next.value() {
        t.utv("resolve(&that, &rules, None)", &resolve(next, rules, None), "unique 2026-11-01T17:00:00Z");
    }
    let start = resolve(&noon, rules, None);
    if let Some(start) = start.value() {
        let later = add(start, &secs, None);
        t.utv("add(&resolve(&noon), &posix_seconds(86_400), None)", &later, "unique 2026-11-01T16:00:00Z");
        if let Some(later) = later.value() {
            t.utv("to_zone(&that, \"America/New_York\", &rules)", &to_zone(later, "America/New_York", rules), "unique 2026-11-01T11:00:00-05:00[America/New_York]");
        }
        // SI seconds differ from POSIX seconds across a leap second; this library has no leap-second table.
        let elapsed = json!({"schema": "utv/1.0", "kind": "amount", "value": {"basis": "elapsed", "seconds": {"numerator": "86400", "denominator": "1"}}});
        t.utv("add(&instant, &elapsed_si_86400, None)", &add(start, &elapsed, None), "unsupported");
    }
}

// ---------- 13 ----------

fn ranges(t: &mut Tour) {
    t.section("13. Ranges");
    let ymd = |y, m, d| NaiveDate::from_ymd_opt(y, m, d).expect("exists");
    let november = ymd(2026, 11, 1)..ymd(2026, 12, 1);
    let december = ymd(2026, 12, 1)..ymd(2027, 1, 1);
    t.native("(nov1..dec1).contains(&nov15)", november.contains(&ymd(2026, 11, 15)));
    t.native("nov.start < dec.end && dec.start < nov.end", november.start < december.end && december.start < november.end);
    t.native("(nov1..).contains(&jan1_3000)", (ymd(2026, 11, 1)..).contains(&ymd(3000, 1, 1)));
    let unknown_end: Option<NaiveDate> = None;
    let x = ymd(2027, 6, 1);
    t.native("end: None; end.is_none_or(|e| x < e)", ymd(2026, 11, 1) <= x && unknown_end.is_none_or(|e| x < e));
    t.note("Rust ranges have no \"end unknown\": with Option you must pick an answer (here: treat it as open).");

    let nov = interval(date(2026, 11, 1), date(2026, 12, 1));
    let dec = interval(date(2026, 12, 1), date(2027, 1, 1));
    t.utv("contains(&nov, &date(2026, 11, 15))", &contains(&nov, &date(2026, 11, 15)), "unique definitely");
    t.utv("overlaps(&nov, &dec)", &overlaps(&nov, &dec), "unique impossible");
    let open = interval_with(Endpoint::Included(date(2026, 11, 1)), Endpoint::Unbounded);
    t.utv("contains(&[nov1, ∞), &date(3000, 1, 1))", &contains(&open, &date(3000, 1, 1)), "unique definitely");
    let unknown = interval_with(Endpoint::Included(date(2026, 11, 1)), Endpoint::Unknown);
    t.utv("contains(&[nov1, ?), &date(2027, 6, 1))", &contains(&unknown, &date(2027, 6, 1)), "unique possibly");
    t.utv("contains(&[nov1, ?), &date(2026, 10, 1))", &contains(&unknown, &date(2026, 10, 1)), "unique impossible");
}

// ---------- 14 ----------

fn round_trip(t: &mut Tour) {
    t.section("14. Round trip");
    let d = NaiveDate::from_ymd_opt(2026, 11, 5).expect("exists");
    t.native("let d = NaiveDate::from_ymd_opt(2026, 11, 5)", d);
    let v = from_naive_date(d);
    t.utv("from_naive_date(d)", &v, "unique 2026-11-05");
    let later = add(v.value().expect("unique"), &period(Period::days(30)), None);
    t.back("to_naive_date(&add(.., days 30))", later.value().map_or_else(|| Err(later.clone()), to_naive_date), "ok 2026-12-05");

    let odt = DateTime::parse_from_rfc3339("2026-11-05T14:30:00+01:00").expect("parses");
    t.native("let odt = DateTime::parse_from_rfc3339(..+01:00)", odt);
    t.utv("from_fixed_offset(odt, OffsetIntent::Instant)", &from_fixed_offset(odt, OffsetIntent::Instant), "unique 2026-11-05T13:30:00Z");
    let as_wall = from_fixed_offset(odt, OffsetIntent::OffsetDateTime);
    t.utv("from_fixed_offset(odt, OffsetIntent::OffsetDateTime)", &as_wall, "unique 2026-11-05T14:30:00+01:00");
    t.back("to_fixed_offset(&that)", to_fixed_offset(as_wall.value().expect("unique")), "ok 2026-11-05 14:30:00 +01:00");

    // Refusals: a host-local value, and conversions chrono couldn't hold exactly.
    t.utv("from_local(Local::now())", &from_local(Local::now()), "unsupported");
    t.back("to_naive_date(&date(275760, 9, 13))", to_naive_date(&date(275760, 9, 13)), "unsupported");
    t.back("to_fixed_offset(&meeting_in_paris)", to_fixed_offset(&in_zone(date_time(2027, 6, 1, 9, 0), "Europe/Paris")), "unsupported");
    t.note("Local depends on the host's zone, so it is refused; chrono's years stop near ±262000, and FixedOffset would drop Paris's rules.");
}

// ---------- printing and checking ----------

#[derive(Default)]
struct Tour {
    mismatches: usize,
}

const W: usize = 52;

/// A candidate as text: ISO 8601 where `format_iso` has a form, amounts compactly, else JSON.
fn show(c: &Value) -> String {
    if let Some(s) = c.as_str() {
        return s.to_owned();
    }
    let v = &c["value"];
    match (c["kind"].as_str(), v["basis"].as_str()) {
        (Some("amount"), Some("clock-ticks")) => {
            let (n, d) = (v["seconds"]["numerator"].as_str().unwrap_or("?"), v["seconds"]["denominator"].as_str().unwrap_or("?"));
            return if d == "1" { format!("{n} POSIX s") } else { format!("{n}/{d} POSIX s") };
        }
        (Some("amount"), Some("calendar")) => return format!("period {}", v["components"]),
        _ => {}
    }
    format_iso(c).as_str().map_or_else(|| c.to_string(), str::to_owned)
}

fn summary(r: &UtvResult) -> String {
    let shown: Vec<String> = r.candidates.iter().map(show).collect();
    if shown.is_empty() {
        r.status.as_str().to_owned()
    } else {
        format!("{} {}", r.status.as_str(), shown.join(", "))
    }
}

impl Tour {
    fn section(&self, title: &str) {
        println!("\n== {title} ==");
    }

    fn native(&self, code: &str, out: impl std::fmt::Display) {
        println!("  native  {code:<W$} -> {out}");
    }

    fn note(&self, text: &str) {
        println!("  note    {text}");
    }

    fn mismatch(&mut self, got: &str, expected: &str) {
        println!("  MISMATCH: expected {expected}, got {got}");
        self.mismatches += 1;
    }

    /// A UTV result: its status and candidates, plus the first diagnostic when there are none.
    fn utv(&mut self, code: &str, r: &UtvResult, expected: &str) {
        let got = summary(r);
        match r.diagnostics.first() {
            Some(d) if r.candidates.is_empty() => println!("  utv     {code:<W$} -> {got} ({}: {})", d.code, d.message),
            _ => println!("  utv     {code:<W$} -> {got}"),
        }
        if got != expected {
            self.mismatch(&got, expected);
        }
    }

    /// A UTV result whose candidate varies between runs (the clock): only the status is checked.
    fn utv_status(&mut self, code: &str, r: &UtvResult, expected: &str) {
        println!("  utv     {code:<W$} -> {}", summary(r));
        if r.status.as_str() != expected {
            self.mismatch(r.status.as_str(), expected);
        }
    }

    /// A conversion back to a native type: `ok <value>`, or the refusal's status and reason.
    fn back<T: std::fmt::Display>(&mut self, code: &str, r: Result<T, UtvResult>, expected: &str) {
        let got = match &r {
            Ok(v) => format!("ok {v}"),
            Err(e) => e.status.as_str().to_owned(),
        };
        match &r {
            Err(e) if !e.diagnostics.is_empty() => println!("  utv     {code:<W$} -> {got} ({}: {})", e.diagnostics[0].code, e.diagnostics[0].message),
            _ => println!("  utv     {code:<W$} -> {got}"),
        }
        if got != expected {
            self.mismatch(&got, expected);
        }
    }

    fn expect_eq(&mut self, what: &str, got: &str, expected: &str) {
        println!("  utv     {what:<W$} -> {got}");
        if got != expected {
            self.mismatch(got, expected);
        }
    }
}
