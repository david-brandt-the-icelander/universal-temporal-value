//! The typed layer (`docs/library-api.md`): one test per row of the §2 table, plus text and
//! JSON round trips, constructor refusals, outcome helpers and policies.

use std::cmp::Ordering;

use utv::{
    Bound, CivilDate, CivilDateTime, CivilTime, CivilYearMonth, Diagnostics, ElapsedSeconds, FixedOffsetDateTime, Gap, Instant, Interval, MonthEnd,
    Outcome, Overlap, Period, PosixSeconds, ResolutionPolicy, Status, TemporalContext, Truth, Value, Weekday, ZoneRules, ZonedIntent,
};

type Result<T = ()> = std::result::Result<T, Box<dyn std::error::Error>>;

fn ctx() -> TemporalContext {
    let rules = ZoneRules::load(concat!(env!("CARGO_MANIFEST_DIR"), "/../../conformance/rules/demo-zones.json")).expect("demo rules");
    TemporalContext::with(rules)
}

fn date(y: i128, m: u32, d: u32) -> CivilDate {
    CivilDate::of(y, m, d).expect("a real date")
}

fn codes<T>(o: &Outcome<T>) -> Vec<&str> {
    o.diagnostics().codes()
}

// ---------- §2, row by row ----------

#[test]
fn civil_date_total_operations() -> Result {
    let d = date(2026, 11, 5);
    assert_eq!(d.add_days(30), date(2026, 12, 5));
    assert_eq!(d.add_days(-5), date(2026, 10, 31));
    assert_eq!(d.add_weeks(2), date(2026, 11, 19));
    assert_eq!(date(2026, 1, 31).add_months_constrained(1), date(2026, 2, 28));
    assert_eq!(date(2024, 2, 29).add_years_constrained(1), date(2025, 2, 28));
    assert_eq!((d.year(), d.month(), d.day()), (2026, 11, 5));
    assert_eq!(d.day_of_week(), Weekday::Thursday);
    assert_eq!(d.day_of_week().number(), 4);
    assert_eq!((d.day_of_year(), d.iso_week(), d.iso_week_year()), (309, 45, 2026));
    assert_eq!((date(2026, 12, 31).iso_week(), date(2026, 12, 31).iso_week_year()), (53, 2026));
    assert_eq!((date(2024, 12, 30).iso_week(), date(2024, 12, 30).iso_week_year()), (1, 2025));
    assert_eq!((d.days_in_month(), d.is_leap_year(), date(2024, 1, 1).is_leap_year()), (30, false, true));
    assert_eq!(d.days_until(date(2026, 12, 25)), 50);
    assert_eq!(date(2026, 12, 25).days_until(d), -50);
    assert_eq!(d.at(CivilTime::of(9, 30)?).to_string(), "2026-11-05T09:30");
    assert!(d < date(2026, 12, 25));
    assert_eq!(d.cmp(&date(2026, 11, 5)), Ordering::Equal);
    Ok(())
}

#[test]
fn civil_date_partial_operations() -> Result {
    let jan31 = date(2026, 1, 31);
    match jan31.add_months(1) {
        Outcome::None(why) => assert_eq!(why.to_string(), "2026-02 has no day 31 (month-end)"),
        other => panic!("expected none, got {other:?}"),
    }
    assert_eq!(jan31.add_months(2).single()?, date(2026, 3, 31));
    assert_eq!(codes(&date(2024, 2, 29).add_years(1)), ["month-end"]);
    assert_eq!(date(2024, 2, 29).add_years(4).single()?, date(2028, 2, 29));
    assert_eq!(jan31.add(Period { months: 1, days: 1, ..Period::default() }).status(), Status::None);
    assert_eq!(jan31.add_with(Period { months: 1, days: 1, ..Period::default() }, MonthEnd::Constrain).single()?, date(2026, 3, 1));
    let hours = jan31.add(Period::hours(1));
    assert_eq!((hours.status(), codes(&hours)), (Status::Invalid, vec!["kind-mismatch"]));
    Ok(())
}

#[test]
fn civil_year_month_operations() -> Result {
    let nov = CivilYearMonth::of(2026, 11)?;
    assert_eq!(nov.to_string(), "2026-11");
    assert_eq!(nov.add_months(3).to_string(), "2027-02");
    assert_eq!(nov.add_years(-1).to_string(), "2025-11");
    assert!(nov < CivilYearMonth::of(2026, 12)?);
    assert_eq!((nov.year(), nov.month()), (2026, 11));
    Ok(())
}

#[test]
fn civil_time_compares_with_a_missing_second_as_zero() -> Result {
    let (a, b) = (CivilTime::of(9, 30)?, CivilTime::of_hms(9, 30, 0)?);
    assert_ne!(a, b, "09:30 and 09:30:00 are different texts");
    assert!(CivilTime::of_hms(9, 29, 59)? < a);
    assert!(a < CivilTime::of_hms(9, 30, 1)?);
    assert!(a < b, "a tie in time breaks by representation, so Ord agrees with Eq");
    assert_eq!(CivilTime::of_hms_nano(9, 30, 15, 250_000_000)?.to_string(), "09:30:15.25");
    assert_eq!((a.hour(), a.minute(), a.second(), b.second(), a.nanosecond()), (9, 30, None, Some(0), 0));
    Ok(())
}

#[test]
fn civil_date_time_operations() -> Result {
    let late = CivilDateTime::of(2026, 11, 5, 23, 30)?;
    assert_eq!(late.add_hours(2).to_string(), "2026-11-06T01:30");
    assert_eq!(late.add_minutes(45).to_string(), "2026-11-06T00:15");
    assert_eq!(late.add_seconds(30).to_string(), "2026-11-05T23:30:30");
    assert_eq!(late.add_days(1).to_string(), "2026-11-06T23:30");
    assert_eq!(late.add_weeks(-1).to_string(), "2026-10-29T23:30");
    let jan31 = CivilDateTime::of(2026, 1, 31, 9, 0)?;
    assert_eq!(codes(&jan31.add_months(1)), ["month-end"]);
    assert_eq!(jan31.add_months_constrained(1).to_string(), "2026-02-28T09:00");
    assert_eq!(jan31.add(Period { days: 1, hours: 2, ..Period::default() }).single()?.to_string(), "2026-02-01T11:00");
    assert_eq!((late.date(), late.time()), (date(2026, 11, 5), CivilTime::of(23, 30)?));
    assert_eq!(late.in_zone("Europe/Paris").to_string(), "2026-11-05T23:30[Europe/Paris]");
    assert_eq!(late.at_offset(3600)?.to_string(), "2026-11-05T23:30+01:00");
    assert_eq!(late.at_offset(19 * 3600).unwrap_err().codes(), ["offset-out-of-range"]);
    assert!(late < late.add_seconds(1));
    Ok(())
}

#[test]
fn fixed_offset_date_times_compare_by_instant() -> Result {
    let paris = CivilDateTime::of(2026, 11, 5, 14, 30)?.at_offset(3600)?;
    let london = CivilDateTime::of(2026, 11, 5, 13, 30)?.at_offset(0)?;
    assert_eq!(paris.to_instant(), london.to_instant());
    assert_eq!(paris.to_instant().to_string(), "2026-11-05T13:30:00Z");
    assert_ne!(paris, london);
    assert!(london < paris, "same instant: the smaller offset first");
    assert!(paris < CivilDateTime::of(2026, 11, 5, 13, 31)?.at_offset(0)?);
    assert_eq!((paris.date_time().to_string(), paris.offset_seconds()), ("2026-11-05T14:30".to_owned(), 3600));
    Ok(())
}

#[test]
fn zoned_intent_operations() -> Result {
    let ctx = ctx();
    let paris: ZonedIntent = "2026-03-28T09:00+01:00[Europe/Paris]".parse()?;
    assert_eq!((paris.zone_id(), paris.asserted_offset_seconds()), ("Europe/Paris", Some(3600)));
    let next = paris.add_days(1);
    assert_eq!(next.to_string(), "2026-03-29T09:00[Europe/Paris]", "add drops the asserted offset");
    assert_eq!(next.resolve(&ctx).single()?.to_string(), "2026-03-29T07:00:00Z");
    assert_eq!(paris.add_hours(24).to_string(), "2026-03-29T09:00[Europe/Paris]");
    assert_eq!(paris.add_weeks(1).add_minutes(30).add_seconds(5).to_string(), "2026-04-04T09:30:05[Europe/Paris]");
    assert_eq!(paris.date_time(), CivilDateTime::of(2026, 3, 28, 9, 0)?);
    assert_eq!(codes(&CivilDateTime::of(2026, 1, 31, 9, 0)?.in_zone("Europe/Paris").add_months(1)), ["month-end"]);

    let gap = CivilDateTime::of(2026, 3, 8, 2, 30)?.in_zone("America/New_York");
    assert_eq!(codes(&gap.resolve(&ctx)), ["gap"]);
    let shifted = gap.resolve_with(&ctx, ResolutionPolicy::new(Gap::ShiftForward, Overlap::Reject)).single()?;
    assert_eq!(shifted.to_string(), "2026-03-08T07:30:00Z");
    let overlap = CivilDateTime::of(2026, 11, 1, 1, 30)?.in_zone("America/New_York");
    match overlap.resolve(&ctx) {
        Outcome::Multiple(both, why) => {
            assert_eq!(both.iter().map(Instant::to_string).collect::<Vec<_>>(), ["2026-11-01T05:30:00Z", "2026-11-01T06:30:00Z"]);
            assert!(why.contains("ambiguous"));
        }
        other => panic!("expected multiple, got {other:?}"),
    }
    assert!(overlap.resolve_with(&ctx, ResolutionPolicy::ALL_CANDIDATES).diagnostics().is_empty());
    let latest = overlap.resolve_with(&ctx, ResolutionPolicy::new(Gap::Reject, Overlap::Latest)).single()?;
    assert_eq!(latest.to_string(), "2026-11-01T06:30:00Z");
    let unknown = CivilDateTime::of(2026, 11, 1, 1, 30)?.in_zone("Mars/Olympus_Mons");
    assert_eq!((unknown.resolve(&ctx).status(), codes(&unknown.resolve(&ctx))), (Status::Unsupported, vec!["unknown-zone"]));
    let mismatch: ZonedIntent = "2026-03-08T02:30-05:00[America/New_York]".parse()?;
    assert_eq!(codes(&mismatch.resolve(&ctx)), ["offset-mismatch"]);
    Ok(())
}

#[test]
fn zoned_values_compare_only_through_the_context() -> Result {
    let ctx = ctx();
    let paris = CivilDateTime::of(2027, 6, 1, 9, 0)?.in_zone("Europe/Paris");
    let new_york = CivilDateTime::of(2027, 6, 1, 3, 0)?.in_zone("America/New_York");
    assert_eq!(ctx.compare(&paris, &new_york).single()?, Ordering::Equal);
    assert_eq!(ctx.compare(&new_york, &paris.add_hours(1)).single()?, Ordering::Less);
    // 01:30 and 01:45 on the night the clocks go back: the order depends on which 01:30 is meant.
    let a = CivilDateTime::of(2026, 11, 1, 1, 30)?.in_zone("America/New_York");
    let b = CivilDateTime::of(2026, 11, 1, 1, 45)?.in_zone("America/New_York");
    match ctx.compare(&a, &b) {
        Outcome::Multiple(orders, why) => {
            assert_eq!(orders, [Ordering::Less, Ordering::Greater]);
            assert!(why.contains("ambiguous"));
        }
        other => panic!("expected multiple, got {other:?}"),
    }
    let gap = CivilDateTime::of(2026, 3, 8, 2, 30)?.in_zone("America/New_York");
    assert_eq!(codes(&ctx.compare(&gap, &a)), ["gap"]);
    Ok(())
}

#[test]
fn instant_operations() -> Result {
    let ctx = ctx();
    let a = Instant::from_unix_seconds_nanos(1_793_885_400, 250_000_000);
    assert_eq!(a.to_string(), "2026-11-05T13:30:00.25Z");
    assert_eq!((a.unix_seconds(), a.nanoseconds()), (1_793_885_400, 250_000_000));
    assert_eq!(Instant::from_unix_nanoseconds(1_793_885_400_250_000_000), a);
    assert_eq!(Instant::from_unix_seconds(-1).to_string(), "1969-12-31T23:59:59Z");
    let b = a.add(PosixSeconds::of_ratio(14403, 4)?);
    assert_eq!(b.to_string(), "2026-11-05T14:30:01Z");
    assert_eq!(a.until(b).to_string(), "3600.75 s (POSIX)");
    assert_eq!(b.until(a), PosixSeconds::of_ratio(14403, 4)?.negate());
    assert!(a < b);
    assert_eq!(a.at_offset(-5 * 3600)?.to_string(), "2026-11-05T08:30:00.25-05:00");
    assert_eq!(a.in_zone("Europe/Paris", &ctx).single()?.to_string(), "2026-11-05T14:30:00.25+01:00[Europe/Paris]");
    assert_eq!(codes(&Instant::from_unix_seconds(0).in_zone("Europe/Paris", &ctx)), ["outside-rule-window"]);
    let si = a.add_elapsed(ElapsedSeconds::of(86_400));
    assert_eq!((si.status(), codes(&si)), (Status::Unsupported, vec!["needs-leap-seconds"]));
    let reading = std::time::UNIX_EPOCH + std::time::Duration::new(1_793_885_400, 250_000_000);
    assert_eq!(Instant::from_system_time(reading), a);
    assert_eq!(std::time::SystemTime::try_from(a)?, reading);
    Ok(())
}

#[test]
fn amounts() -> Result {
    let p = Period::of(1, 2, 0, 10, 2, 30, 0);
    assert_eq!(p.to_string(), "P1Y2M10DT2H30M");
    assert_eq!(p.negate().to_string(), "-P1Y2M10DT2H30M");
    assert_eq!((p.years, p.months, p.days, p.hours, p.minutes), (1, 2, 10, 2, 30));
    assert_eq!(Period::default().to_string(), "P0D");
    assert!(Period::default().is_zero());
    assert_eq!(Period::months(1), Period::months(1));
    assert_ne!(Period::months(1), Period::minutes(1));
    let mixed = Period { months: 1, days: -1, ..Period::default() };
    assert!(mixed.to_string().starts_with('{'), "no text form: shown as JSON");
    let s = PosixSeconds::of(86_400);
    assert_eq!((s.numerator(), s.denominator(), s.to_string()), (86_400, 1, "86400 s (POSIX)".to_owned()));
    assert_eq!(PosixSeconds::of_ratio(2, 6)?.to_string(), "1/3 s (POSIX)");
    assert_eq!(ElapsedSeconds::of(86_400).negate().to_string(), "-86400 s (SI)");
    assert_ne!(PosixSeconds::of(1).to_json(), ElapsedSeconds::of(1).to_json());
    Ok(())
}

#[test]
fn intervals_answer_with_three_valued_truth() -> Result {
    let (nov1, dec1) = (date(2026, 11, 1), date(2026, 12, 1));
    let november = Interval::half_open(nov1, dec1)?;
    assert_eq!(november.to_string(), "[2026-11-01, 2026-12-01)");
    assert_eq!(november.contains(&date(2026, 11, 30)), Truth::Definitely);
    assert_eq!(november.contains(&dec1), Truth::Impossible);
    assert_eq!(Interval::closed(nov1, date(2026, 11, 30))?.overlaps(&Interval::of(Bound::Included(dec1), Bound::Unbounded)?), Truth::Impossible);
    let unknown_end = Interval::of(Bound::Included(nov1), Bound::Unknown)?;
    assert_eq!(unknown_end.to_string(), "[2026-11-01, ?)");
    assert_eq!(unknown_end.contains(&date(2027, 6, 1)), Truth::Possibly);
    assert_eq!(unknown_end.contains(&date(2026, 10, 1)), Truth::Impossible);
    assert_eq!(unknown_end.overlaps(&november), Truth::Possibly, "the unknown end might be the start");
    assert_eq!((november.start(), november.end()), (&Bound::Included(nov1), &Bound::Excluded(dec1)));
    let a = Instant::from_unix_seconds(0);
    let hour = Interval::half_open(a, a.add(PosixSeconds::of(3600)))?;
    assert_eq!(hour.to_string(), "[1970-01-01T00:00:00Z, 1970-01-01T01:00:00Z)");
    assert_eq!(hour.contains(&a), Truth::Definitely);
    assert_eq!(Interval::half_open(dec1, nov1).unwrap_err().codes(), ["reversed-interval"]);
    Ok(())
}

// ---------- text and JSON ----------

fn values() -> Result<Vec<Value>> {
    let ctx = ctx();
    let nov = date(2026, 11, 5);
    Ok(vec![
        Value::Date(nov),
        Value::Date(date(275_760, 9, 13)),
        Value::Date(date(-500, 3, 1)),
        Value::YearMonth(CivilYearMonth::of(2026, 11)?),
        Value::Time(CivilTime::of(9, 30)?),
        Value::Time(CivilTime::of_hms_nano(9, 30, 15, 250_000_000)?),
        Value::DateTime(CivilDateTime::of_hms(2026, 11, 5, 14, 30, 0)?),
        Value::FixedOffset(CivilDateTime::of(2026, 11, 5, 14, 30)?.at_offset(-3600)?),
        Value::Zoned(CivilDateTime::of(2027, 6, 1, 9, 0)?.in_zone("Europe/Paris")),
        Value::Zoned(Instant::from_unix_seconds(1_793_885_400).in_zone("America/New_York", &ctx).single()?),
        Value::Instant(Instant::from_unix_seconds_nanos(0, 500_000_000)),
        Value::Period(Period::of(1, 2, 3, 4, 5, 6, 7)),
        Value::Period(Period::days(-1)),
        Value::DateInterval(Interval::of(Bound::Unbounded, Bound::Excluded(nov))?),
        Value::DateInterval(Interval::of(Bound::Excluded(nov), Bound::Unknown)?),
        Value::InstantInterval(Interval::closed(Instant::from_unix_seconds(0), Instant::from_unix_seconds(60))?),
    ])
}

#[test]
fn to_string_round_trips_through_parse() -> Result {
    for v in values()? {
        let text = v.to_string();
        assert_eq!(utv::parse(&text).single()?, v, "{text}");
        assert_eq!(text.parse::<Value>()?, v);
    }
    assert_eq!(CivilDate::parse("2026-11-05").single()?, date(2026, 11, 5));
    assert_eq!("2026-11-05".parse::<CivilDate>()?, date(2026, 11, 5));
    assert_eq!("[2026-11-01, 2026-12-01)".parse::<Interval<CivilDate>>()?, Interval::half_open(date(2026, 11, 1), date(2026, 12, 1))?);
    assert_eq!("2026-11-01/2026-11-30".parse::<Interval<CivilDate>>()?.to_string(), "[2026-11-01, 2026-11-30]");
    assert_eq!("P1M".parse::<Period>()?, Period::months(1));
    assert_eq!("PT1M".parse::<Period>()?, Period::minutes(1));
    Ok(())
}

#[test]
fn from_json_of_to_json_is_the_same_value() -> Result {
    for v in values()? {
        assert_eq!(Value::from_json(&v.to_json()).single()?, v, "{v}");
    }
    let d = date(2026, 11, 5);
    assert_eq!(CivilDate::from_json(&d.to_json()).single()?, d);
    assert_eq!(d.to_json(), utv::build::date(2026, 11, 5), "the same JSON as the 0.2 builders");
    assert_eq!(PosixSeconds::from_json(&utv::build::posix_seconds(5)).single()?, PosixSeconds::of(5));
    assert_eq!(ElapsedSeconds::from_json(&utv::build::elapsed_seconds(5)).single()?, ElapsedSeconds::of(5));
    Ok(())
}

#[test]
fn wrong_kinds_and_bad_text_are_outcomes() {
    let o = CivilDate::from_json(&utv::build::instant(0));
    assert_eq!((o.status(), codes(&o)), (Status::Invalid, vec!["kind-mismatch"]));
    let o = Instant::from_json(&utv::build::date(2026, 11, 5));
    assert_eq!(codes(&o), ["kind-mismatch"]);
    let o = CivilDateTime::from_json(&utv::build::in_zone(utv::build::date_time(2026, 11, 5, 9, 0), "Europe/Paris"));
    assert_eq!(codes(&o), ["kind-mismatch"]);
    let o = CivilDate::from_json(&utv::build::date(2026, 2, 30));
    assert_eq!(codes(&o), ["day-out-of-range"]);
    assert_eq!(codes(&CivilDate::parse("2026-11-05T09:00")), ["kind-mismatch"]);
    assert_eq!(codes(&CivilDate::parse("2026-02-30")), ["day-out-of-range"]);
    assert_eq!(codes(&utv::parse("next Tuesday")), ["syntax"]);
    assert_eq!(codes(&utv::parse("2026-W45-4")), ["unsupported-form"]);
    assert_eq!(utv::parse("2026-02-30").to_string(), "invalid (day-out-of-range)");
    assert!("2026-02-30".parse::<CivilDate>().unwrap_err().to_string().starts_with("invalid: 2026-02 has no day 30"));
}

#[test]
fn constructors_reject_invalid_values() {
    fn code<T>(r: std::result::Result<T, Diagnostics>) -> String {
        r.map(drop).unwrap_err().codes().join(",")
    }
    assert_eq!(code(CivilDate::of(2026, 2, 30)), "day-out-of-range");
    assert_eq!(code(CivilDate::of(2026, 2, 29)), "day-out-of-range");
    assert_eq!(code(CivilDate::of(2026, 13, 1)), "month-out-of-range");
    assert_eq!(code(CivilDate::of(2026, 0, 1)), "month-out-of-range");
    assert_eq!(code(CivilYearMonth::of(2026, 13)), "month-out-of-range");
    assert_eq!(code(CivilTime::of(24, 0)), "field-out-of-range");
    assert_eq!(code(CivilTime::of(9, 60)), "field-out-of-range");
    assert_eq!(code(CivilTime::of_hms(23, 59, 60)), "leap-second");
    assert_eq!(code(CivilTime::of_hms_nano(9, 30, 0, 1_000_000_000)), "field-out-of-range");
    assert_eq!(code(CivilDateTime::of(2026, 2, 29, 9, 0)), "day-out-of-range");
    assert_eq!(code(CivilDate::of(i128::MAX, 1, 1)), "implementation-limit");
    assert_eq!(code(PosixSeconds::of_ratio(1, 0)), "field-out-of-range");
    assert_eq!(CivilDate::of(2026, 2, 30).unwrap_err().to_string(), "2026-02 has no day 30 (day-out-of-range)");
    assert!(CivilDate::of(2024, 2, 29).is_ok());
    assert!(CivilDate::of(0, 1, 1).is_ok());
    assert!(CivilDate::of(10i128.pow(20), 12, 31).is_ok());
}

// ---------- outcome helpers ----------

#[test]
fn outcome_helpers_never_pick_an_answer_silently() -> Result {
    let ctx = ctx();
    let jan31 = date(2026, 1, 31);
    assert_eq!(jan31.add_months(2).unique(), Some(date(2026, 3, 31)));
    assert_eq!(jan31.add_months(1).unique(), None);
    assert_eq!(jan31.add_months(1).unique_or(jan31), jan31);
    assert_eq!(jan31.add_months(2).as_unique(), Some(&date(2026, 3, 31)));
    assert!(jan31.add_months(2).is_unique());
    let err = jan31.add_months(1).single().unwrap_err();
    assert_eq!((err.status, err.diagnostics.codes()), (Status::None, vec!["month-end"]));
    assert_eq!(err.to_string(), "none: 2026-02 has no day 31 (month-end)");
    assert_eq!(jan31.add_months(1).status().as_str(), "none");
    assert_eq!(jan31.add_months(1).to_string(), "none (month-end)");
    assert_eq!(jan31.add_months(2).to_string(), "2026-03-31");
    let overlap = CivilDateTime::of(2026, 11, 1, 1, 30)?.in_zone("America/New_York").resolve(&ctx);
    let first = Instant::from_unix_seconds(0);
    assert_eq!(overlap.clone().unique_or(first), first, "multiple is not unique");
    assert_eq!(overlap.status(), Status::Multiple);
    assert_eq!(overlap.to_string(), "multiple (ambiguous)");
    assert_eq!(overlap.clone().single().unwrap_err().status, Status::Multiple);
    let r = overlap.to_result();
    assert_eq!((r.operation.as_str(), r.status, r.candidates.len()), ("resolve", Status::Multiple, 2));
    assert_eq!(r.to_json()["schema"], "utv-result/1.0");
    assert_eq!(jan31.add_months(1).to_result().to_json()["diagnostics"][0]["code"], "month-end");
    assert_eq!(jan31.add_months(2).map(|d| d.day()).single()?, 31);
    Ok(())
}

#[test]
fn debug_shows_the_type_and_the_text() -> Result {
    assert_eq!(format!("{:?}", date(2026, 11, 5)), "CivilDate(2026-11-05)");
    assert_eq!(format!("{:?}", Instant::from_unix_seconds(0)), "Instant(1970-01-01T00:00:00Z)");
    assert_eq!(format!("{:?}", PosixSeconds::of(60)), "PosixSeconds(60 s (POSIX))");
    assert_eq!(format!("{:?}", Value::Period(Period::months(1))), "Period(P1M)");
    assert_eq!(format!("{:?}", date(2026, 1, 31).add_months(2)), "Unique(CivilDate(2026-03-31))");
    assert_eq!(format!("{:<12}|", date(2026, 11, 5)), "2026-11-05  |", "Display honours width");
    let _: FixedOffsetDateTime = "2026-11-05T14:30+01:00".parse()?;
    Ok(())
}

// ---------- contract details settled after the first 0.3 library ----------

#[test]
fn valid_values_no_typed_type_holds_are_unsupported() {
    let zoned_date = utv::build::in_zone(utv::build::date(2026, 11, 5), "Europe/Paris");
    for o in [CivilDate::from_json(&zoned_date).map(drop), Value::from_json(&zoned_date).map(drop)] {
        assert_eq!((o.status(), codes(&o)), (Status::Unsupported, vec!["unsupported-operand"]));
    }
    let mut with_policy = utv::build::date_time(2026, 11, 5, 9, 0);
    with_policy["value"]["resolutionPolicy"] = serde_json::json!({"gap": "reject"});
    assert_eq!(codes(&CivilDateTime::from_json(&with_policy)), ["unsupported-operand"]);
    let datetimes = utv::build::interval(utv::build::date_time(2026, 11, 5, 9, 0), utv::build::date_time(2026, 11, 5, 10, 0));
    assert_eq!(codes(&Value::from_json(&datetimes)), ["unsupported-operand"]);
    assert_eq!(codes(&Interval::<CivilDate>::from_json(&datetimes)), ["unsupported-operand"]);
    let recurrence = serde_json::json!({"schema": "utv/1.0", "kind": "recurrence", "value": {"intent": utv::build::date(2026, 11, 3), "algorithm": {"system": "rfc", "id": "5545", "version": "1"}, "rule": "FREQ=WEEKLY"}});
    assert_eq!(codes(&Value::from_json(&recurrence)), ["unsupported-operand"]);
    // Another typed type holds these: wrong kind or form.
    let instants = utv::build::interval(utv::build::instant(0), utv::build::instant(60));
    assert_eq!(codes(&Interval::<CivilDate>::from_json(&instants)), ["kind-mismatch"]);
    assert_eq!(codes(&CivilDateTime::from_json(&utv::build::with_offset(utv::build::date_time(2026, 11, 5, 9, 0), 0))), ["kind-mismatch"]);
}

#[test]
fn malformed_json_is_malformed_value() {
    let mut bad = utv::build::date(2026, 11, 5);
    bad["value"]["fields"]["day"] = serde_json::json!("five");
    assert!(CivilDate::from_json(&bad).diagnostics().contains("malformed-value"));
    assert!(CivilDate::from_json(&serde_json::json!({"kind": "civil"})).diagnostics().contains("malformed-value"));
    assert!(utv::fields(&bad).diagnostics.iter().any(|d| d.code == "malformed-value"));
    let missing = ZoneRules::load("/nonexistent/rules.json").unwrap_err();
    assert_eq!(missing.codes(), ["malformed-value"]);
}

#[test]
fn intervals_need_a_point_and_the_right_order() {
    let no_point = Interval::<CivilDate>::of(Bound::Unbounded, Bound::Unknown).unwrap_err();
    assert_eq!(no_point.codes(), ["unsupported-form"]);
    assert_eq!(codes(&Interval::<CivilDate>::parse("(.., ..)")), ["unsupported-form"]);
    let (a, b) = (Instant::from_unix_seconds(60), Instant::from_unix_seconds(0));
    assert_eq!(Interval::closed(a, b).unwrap_err().codes(), ["reversed-interval"]);
    assert!(Interval::half_open(a, a).is_ok(), "empty, not reversed");
}

#[test]
fn instant_sub_second_is_exact_and_nanoseconds_its_floor() -> Result {
    let third: Instant = utv::Instant::from_json(&serde_json::json!({"schema": "utv/1.0", "kind": "instant", "value": {
        "system": {"system": "utv.timescale", "id": "unix-posix", "version": "1"}, "ticks": "-1", "subTick": {"numerator": "1", "denominator": "3"}}}))
    .single()?;
    assert_eq!((third.unix_seconds(), third.nanoseconds()), (-1, 333_333_333));
    assert_eq!(third.sub_second(), PosixSeconds::of_ratio(1, 3)?);
    assert_eq!(Instant::from_unix_seconds_nanos(5, 250_000_000).sub_second().to_string(), "0.25 s (POSIX)");
    Ok(())
}
