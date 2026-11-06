//! Everyday date and time tasks in Rust: chrono first, then the same task with UTV.
//!
//! Run it with `cargo run --example everyday --features chrono`.
//!
//! Rust's standard library has no calendar types, so the native side uses chrono, plus
//! chrono-tz for named zones. Each output line is shown as a trailing comment. The snapshot
//! test (`tests/everyday_example.rs`) checks the output against `examples/everyday.expected.txt`.
//! Lines that end in ‡ depend on the host's clock or zone, and the test skips them.

use std::error::Error;
use std::fmt::Display;
use std::time::SystemTime;

use chrono::{DateTime, Datelike, Days, Local, Months, NaiveDate, NaiveTime, TimeDelta, TimeZone, Utc};
use chrono_tz::America::New_York;
use chrono_tz::Europe::Paris;
use utv::{
    Bound, CivilDate, CivilDateTime, CivilTime, ElapsedSeconds, FixedOffsetDateTime, Gap, Instant, Interval, Outcome, Overlap, PosixSeconds,
    ResolutionPolicy, TemporalContext, Value, ZoneRules,
};

pub fn main() -> Result<(), Box<dyn Error>> {
    // UTV never reads the host's tz database: you load the rules and pass them in once.
    let rules = ZoneRules::load(concat!(env!("CARGO_MANIFEST_DIR"), "/../../conformance/rules/demo-zones.json"))?;
    let ctx = TemporalContext::with(rules);

    now();
    a_date()?;
    invalid_dates();
    adding()?;
    days_between(&ctx)?;
    times()?;
    offsets()?;
    time_zones(&ctx)?;
    dst_gap(&ctx)?;
    dst_overlap(&ctx)?;
    timestamps()?;
    day_vs_24_hours(&ctx)?;
    ranges()?;
    round_trip()?;
    Ok(())
}

fn now() {
    println!("1. Now");
    println!("   Now");
    // BEGIN now
    row("chrono", Utc::now(), "chrono reads the clock ‡"); // 2026-11-05 13:30:00.123456789 UTC ‡
    row("chrono", Local::now(), "and the host's zone ‡"); // 2026-11-05 14:30:00.123456789 +01:00 ‡

    // UTV never reads the clock. You read it and pass the reading in.
    let reading = SystemTime::now();
    row("UTV", Instant::from_system_time(reading), "the reading you passed in ‡"); // 2026-11-05T13:30:00.123456789Z ‡
    // END now
}

fn a_date() -> Result<(), Box<dyn Error>> {
    println!("\n2. A date");
    println!("   5 November 2026");
    // BEGIN a-date
    let date = NaiveDate::from_ymd_opt(2026, 11, 5).unwrap();
    row("chrono", date, ""); // 2026-11-05
    row("chrono", date.format("%-d %B %Y"), ""); // 5 November 2026

    let date = CivilDate::of(2026, 11, 5)?;
    let parsed: CivilDate = "2026-11-05".parse()?;
    row("UTV", date, ""); // 2026-11-05
    row("UTV", parsed == date, "parsed from \"2026-11-05\""); // true
    // END a-date

    println!("   Day of week and ISO week");
    // BEGIN weekday-week
    let date = NaiveDate::from_ymd_opt(2026, 11, 5).unwrap();
    row("chrono", format!("{}, ISO week {}", date.weekday(), date.iso_week().week()), ""); // Thu, ISO week 45

    let date = CivilDate::of(2026, 11, 5)?;
    row("UTV", format!("{}, ISO week {}", date.day_of_week(), date.iso_week()), ""); // Thursday, ISO week 45
    // END weekday-week
    Ok(())
}

fn invalid_dates() {
    println!("\n3. Invalid dates");
    println!("   30 February 2026");
    // BEGIN feb-30
    row("chrono", format!("{:?}", NaiveDate::from_ymd_opt(2026, 2, 30)), ""); // None
    row("chrono", format!("{:?}", "2026-02-30".parse::<NaiveDate>()), ""); // Err(ParseError(OutOfRange))

    match CivilDate::of(2026, 2, 30) {
        Ok(date) => row("UTV", date, ""),
        Err(why) => row("UTV", "no such date", why), // 2026-02 has no day 30 (day-out-of-range)
    }
    row("UTV", CivilDate::parse("2026-02-30"), ""); // invalid (day-out-of-range)
    // END feb-30
}

fn adding() -> Result<(), Box<dyn Error>> {
    println!("\n4. Adding");
    println!("   5 Nov + 30 days");
    // BEGIN add-days
    let nov5 = NaiveDate::from_ymd_opt(2026, 11, 5).unwrap();
    row("chrono", nov5 + Days::new(30), ""); // 2026-12-05

    let nov5 = CivilDate::of(2026, 11, 5)?;
    row("UTV", nov5.add_days(30), ""); // 2026-12-05
    // END add-days

    println!("   31 Jan 2026 + 1 month");
    // BEGIN month-end
    let jan31 = NaiveDate::from_ymd_opt(2026, 1, 31).unwrap();
    row("chrono", jan31 + Months::new(1), "clamped silently"); // 2026-02-28

    let jan31 = CivilDate::of(2026, 1, 31)?;
    match jan31.add_months(1) {
        Outcome::Unique(date, _) => row("UTV", date, ""),
        Outcome::None(why) => row("UTV", "no such date", why), // 2026-02 has no day 31 (month-end)
        other => row("UTV", other, ""),
    }
    row("UTV", jan31.add_months_constrained(1), "clamped, because you asked"); // 2026-02-28
    // END month-end
    Ok(())
}

fn days_between(ctx: &TemporalContext) -> Result<(), Box<dyn Error>> {
    println!("\n5. Days between");
    println!("   5 Nov → 25 Dec");
    // BEGIN days-between
    let nov5 = NaiveDate::from_ymd_opt(2026, 11, 5).unwrap();
    let dec25 = NaiveDate::from_ymd_opt(2026, 12, 25).unwrap();
    row("chrono", (dec25 - nov5).num_days(), ""); // 50

    let nov5 = CivilDate::of(2026, 11, 5)?;
    let dec25 = CivilDate::of(2026, 12, 25)?;
    row("UTV", nov5.days_until(dec25), ""); // 50
    // END days-between

    println!("   Which comes first");
    // BEGIN compare-dates
    let nov5 = NaiveDate::from_ymd_opt(2026, 11, 5).unwrap();
    let dec25 = NaiveDate::from_ymd_opt(2026, 12, 25).unwrap();
    row("chrono", format!("{:?}", nov5.cmp(&dec25)), ""); // Less
    let paris = Paris.with_ymd_and_hms(2027, 6, 1, 9, 0, 0).unwrap();
    let new_york = New_York.with_ymd_and_hms(2027, 6, 1, 3, 0, 0).unwrap();
    row("chrono", format!("{:?}", paris.cmp(&new_york)), "09:00 in Paris vs 03:00 in New York"); // Equal

    let nov5 = CivilDate::of(2026, 11, 5)?;
    let dec25 = CivilDate::of(2026, 12, 25)?;
    row("UTV", format!("{:?}", nov5.cmp(&dec25)), ""); // Less
    // Zoned values have no `<`: their order depends on zone rules, so the context answers.
    let paris = CivilDateTime::of(2027, 6, 1, 9, 0)?.in_zone("Europe/Paris");
    let new_york = CivilDateTime::of(2027, 6, 1, 3, 0)?.in_zone("America/New_York");
    match ctx.compare(&paris, &new_york) {
        Outcome::Unique(order, _) => row("UTV", format!("{order:?}"), "by the rules you loaded"), // Equal
        other => row("UTV", other.status(), other.diagnostics()),
    }
    // END compare-dates
    Ok(())
}

fn times() -> Result<(), Box<dyn Error>> {
    println!("\n6. Times and date-times");
    println!("   09:30");
    // BEGIN time-of-day
    row("chrono", NaiveTime::from_hms_opt(9, 30, 0).unwrap(), "always has seconds"); // 09:30:00

    row("UTV", CivilTime::of(9, 30)?, "no seconds given, none shown"); // 09:30
    row("UTV", CivilTime::of_hms(9, 30, 0)?, ""); // 09:30:00
    // END time-of-day

    println!("   23:30 + 2 hours");
    // BEGIN datetime-plus-hours
    let late = NaiveDate::from_ymd_opt(2026, 11, 5).unwrap().and_hms_opt(23, 30, 0).unwrap();
    row("chrono", late + TimeDelta::hours(2), ""); // 2026-11-06 01:30:00

    let late = CivilDateTime::of(2026, 11, 5, 23, 30)?;
    row("UTV", late.add_hours(2), "wall-clock hours, into the next day"); // 2026-11-06T01:30
    // END datetime-plus-hours
    Ok(())
}

fn offsets() -> Result<(), Box<dyn Error>> {
    println!("\n7. Parsing with offsets");
    println!("   Z vs +00:00");
    // BEGIN parse-offsets
    let utc = DateTime::parse_from_rfc3339("2026-11-05T14:30:00Z")?;
    let zero = DateTime::parse_from_rfc3339("2026-11-05T14:30:00+00:00")?;
    row("chrono", utc, "from ...Z"); // 2026-11-05 14:30:00 +00:00
    row("chrono", zero, "from ...+00:00: the same value"); // 2026-11-05 14:30:00 +00:00

    for text in ["2026-11-05T14:30:00Z", "2026-11-05T14:30:00+00:00"] {
        match utv::parse(text).single()? {
            Value::Instant(at) => row("UTV", at, "an instant: UTC known, local offset not"), // 2026-11-05T14:30:00Z
            Value::FixedOffset(wall) => row("UTV", wall, "a wall time at offset 0"), // 2026-11-05T14:30:00+00:00
            other => row("UTV", other, ""),
        }
    }
    // END parse-offsets
    Ok(())
}

fn time_zones(ctx: &TemporalContext) -> Result<(), Box<dyn Error>> {
    println!("\n8. Time zones");
    println!("   09:00 in Paris, seen from New York");
    // BEGIN meeting-in-paris
    let meeting = Paris.with_ymd_and_hms(2027, 6, 1, 9, 0, 0).unwrap();
    row("chrono", meeting.with_timezone(&New_York), "tzdb compiled into chrono-tz"); // 2027-06-01 03:00:00 EDT

    let meeting = CivilDateTime::of(2027, 6, 1, 9, 0)?.in_zone("Europe/Paris");
    let starts = meeting.resolve(ctx).single()?;
    row("UTV", &meeting, ""); // 2027-06-01T09:00[Europe/Paris]
    row("UTV", starts, ""); // 2027-06-01T07:00:00Z
    row("UTV", starts.in_zone("America/New_York", ctx).single()?, ""); // 2027-06-01T03:00:00-04:00[America/New_York]
    // END meeting-in-paris
    Ok(())
}

fn dst_gap(ctx: &TemporalContext) -> Result<(), Box<dyn Error>> {
    println!("\n9. DST gap");
    println!("   02:30 on 8 March 2026 in New York");
    // BEGIN dst-gap
    let local = New_York.with_ymd_and_hms(2026, 3, 8, 2, 30, 0);
    row("chrono", format!("{local:?}"), "you choose what to do"); // None

    let local = CivilDateTime::of(2026, 3, 8, 2, 30)?.in_zone("America/New_York");
    match local.resolve(ctx) {
        Outcome::Unique(at, _) => row("UTV", at, ""),
        Outcome::None(why) => row("UTV", "no such time", why), // 2026-03-08T02:30 is skipped in America/New_York (gap)
        other => row("UTV", other, ""),
    }
    let shift = ResolutionPolicy::new(Gap::ShiftForward, Overlap::Reject);
    let at = local.resolve_with(ctx, shift).single()?;
    row("UTV", at.in_zone("America/New_York", ctx).single()?, "Gap::ShiftForward"); // 2026-03-08T03:30:00-04:00[America/New_York]
    // END dst-gap
    Ok(())
}

fn dst_overlap(ctx: &TemporalContext) -> Result<(), Box<dyn Error>> {
    println!("\n10. DST overlap");
    println!("   01:30 on 1 November 2026 in New York");
    // BEGIN dst-overlap
    let local = New_York.with_ymd_and_hms(2026, 11, 1, 1, 30, 0);
    row("chrono", format!("{local:?}"), ""); // Ambiguous(2026-11-01T01:30:00EDT, 2026-11-01T01:30:00EST)
    row("chrono", local.earliest().unwrap(), ".earliest()"); // 2026-11-01 01:30:00 EDT

    let local = CivilDateTime::of(2026, 11, 1, 1, 30)?.in_zone("America/New_York");
    match local.resolve(ctx) {
        Outcome::Unique(at, _) => row("UTV", at, ""),
        Outcome::Multiple(instants, why) => {
            row("UTV", format!("{} instants", instants.len()), why); // 2 instants  2026-11-01T01:30 occurs twice in America/New_York (ambiguous)
            for at in instants {
                row("UTV", at, ""); // 2026-11-01T05:30:00Z, then 2026-11-01T06:30:00Z
            }
        }
        other => row("UTV", other, ""),
    }
    let earliest = ResolutionPolicy::new(Gap::Reject, Overlap::Earliest);
    row("UTV", local.resolve_with(ctx, earliest).single()?, "Overlap::Earliest"); // 2026-11-01T05:30:00Z
    // END dst-overlap
    Ok(())
}

fn timestamps() -> Result<(), Box<dyn Error>> {
    println!("\n11. Timestamps");
    println!("   Exact fractions and differences");
    // BEGIN timestamps
    let start = DateTime::from_timestamp(1_793_885_400, 250_000_000).unwrap();
    let end = DateTime::from_timestamp(1_793_889_001, 0).unwrap();
    row("chrono", start, ""); // 2026-11-05 13:30:00.250 UTC
    row("chrono", end - start, ""); // PT3600.75S
    row("chrono", TimeDelta::seconds(1) / 3, "rounded to nanoseconds"); // PT0.333333333S

    let start = Instant::from_unix_seconds_nanos(1_793_885_400, 250_000_000);
    let end = Instant::from_unix_seconds(1_793_889_001);
    row("UTV", start, ""); // 2026-11-05T13:30:00.25Z
    row("UTV", start.until(end), ""); // 3600.75 s (POSIX)
    row("UTV", PosixSeconds::of_ratio(1, 3)?, "exact"); // 1/3 s (POSIX)
    // END timestamps
    Ok(())
}

fn day_vs_24_hours(ctx: &TemporalContext) -> Result<(), Box<dyn Error>> {
    println!("\n12. Durations vs periods");
    println!("   One day vs 86,400 seconds across DST");
    // BEGIN day-vs-24h
    let noon = New_York.with_ymd_and_hms(2026, 10, 31, 12, 0, 0).unwrap();
    row("chrono", noon + Days::new(1), "a calendar day"); // 2026-11-01 12:00:00 EST
    row("chrono", noon + TimeDelta::days(1), "86,400 seconds"); // 2026-11-01 11:00:00 EST

    let noon = CivilDateTime::of(2026, 10, 31, 12, 0)?.in_zone("America/New_York");
    row("UTV", noon.add_days(1), "a calendar day"); // 2026-11-01T12:00[America/New_York]
    let start = noon.resolve(ctx).single()?;
    let later = start.add(PosixSeconds::of(86_400));
    row("UTV", later.in_zone("America/New_York", ctx).single()?, "86,400 POSIX seconds"); // 2026-11-01T11:00:00-05:00[America/New_York]
    match start.add_elapsed(ElapsedSeconds::of(86_400)) {
        Outcome::Unique(at, _) => row("UTV", at, "86,400 SI seconds"),
        other => row("UTV", other, "SI seconds need a leap-second table"), // unsupported (needs-leap-seconds)
    }
    // END day-vs-24h
    Ok(())
}

fn ranges() -> Result<(), Box<dyn Error>> {
    println!("\n13. Ranges");
    println!("   November, open-ended and unknown ends");
    // BEGIN ranges
    let nov1 = NaiveDate::from_ymd_opt(2026, 11, 1).unwrap();
    let november = nov1..NaiveDate::from_ymd_opt(2026, 12, 1).unwrap();
    row("std", november.contains(&NaiveDate::from_ymd_opt(2026, 11, 15).unwrap()), "in 2026-11-01..2026-12-01"); // true
    row("std", (nov1..).contains(&NaiveDate::from_ymd_opt(3000, 1, 1).unwrap()), "in 2026-11-01.."); // true
    // An end nobody knows yet has no Range: an Option<NaiveDate> end makes you pick an answer.

    let nov1 = CivilDate::of(2026, 11, 1)?;
    let november = Interval::half_open(nov1, CivilDate::of(2026, 12, 1)?)?;
    row("UTV", november.contains(&CivilDate::of(2026, 11, 15)?), format!("in {november}")); // definitely  in [2026-11-01, 2026-12-01)
    let open_ended = Interval::of(Bound::Included(nov1), Bound::Unbounded)?;
    row("UTV", open_ended.contains(&CivilDate::of(3000, 1, 1)?), format!("in {open_ended}")); // definitely  in [2026-11-01, ..)
    let unknown_end = Interval::of(Bound::Included(nov1), Bound::Unknown)?;
    row("UTV", unknown_end.contains(&CivilDate::of(2027, 6, 1)?), format!("in {unknown_end}")); // possibly  in [2026-11-01, ?)
    // END ranges
    Ok(())
}

fn round_trip() -> Result<(), Box<dyn Error>> {
    println!("\n14. Round trip");
    println!("   Native → UTV → native");
    // BEGIN round-trip
    let date = NaiveDate::from_ymd_opt(2026, 11, 5).unwrap();
    let later = CivilDate::from(date).add_days(30);
    row("UTV", later, "from a NaiveDate, plus 30 days"); // 2026-12-05
    row("chrono", NaiveDate::try_from(later)?, "and back"); // 2026-12-05
    match NaiveDate::try_from(CivilDate::of(275_760, 9, 13)?) {
        Ok(date) => row("chrono", date, ""),
        Err(why) => row("chrono", "refused", why), // year 275760 is outside chrono's range (out-of-native-range)
    }

    // A DateTime<FixedOffset> can mean two things; the type you convert to says which.
    let written = DateTime::parse_from_rfc3339("2026-11-05T14:30:00+01:00")?;
    row("UTV", Instant::try_from(written)?, "as an instant"); // 2026-11-05T13:30:00Z
    row("UTV", FixedOffsetDateTime::try_from(written)?, "as a wall time at +01:00"); // 2026-11-05T14:30:00+01:00
    // There is no conversion from DateTime<Local>: it depends on the host's zone.
    // END round-trip
    Ok(())
}

/// Prints one output row, aligned: who answered, the answer, and a note. A note that doesn't
/// fit after the answer goes on the next line, so rows stay within 100 columns. Host-dependent
/// rows end in ‡.
fn row(who: &str, text: impl Display, note: impl Display) {
    let (text, note) = (text.to_string(), note.to_string());
    let note_column = if 41 + note.chars().count() <= 100 { 41 } else { 13 };
    if note.is_empty() || (text.chars().count() <= 26 && note_column == 41) {
        println!("{}", format!("     {who:<8}{text:<26}  {note}").trim_end());
    } else {
        let host = if note.ends_with('‡') { " ‡" } else { "" };
        println!("     {who:<8}{text}{host}");
        println!("{:note_column$}{note}", "");
    }
}
