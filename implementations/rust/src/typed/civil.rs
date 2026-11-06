//! Civil (wall-calendar) values: [`CivilDate`], [`CivilYearMonth`], [`CivilTime`],
//! [`CivilDateTime`], [`FixedOffsetDateTime`] and [`ZonedIntent`], plus [`Weekday`].

use std::cmp::Ordering;
use std::fmt;

use serde_json::Value as Json;

use super::amount::Period;
use super::context::{MonthEnd, ResolutionPolicy, TemporalContext};
use super::instant::Instant;
use super::outcome::{outcome, total, Diagnostics, Outcome};
use super::{kind_mismatch, no_typed_type, only_keys, rejected, typed_value, Typed};
use crate::model::{kind, read_civil, Civil, Form, Zone};
use crate::{Rules, UtvResult};

/// How a civil value is tied to the timeline.
#[derive(PartialEq)]
enum Shape {
    Plain,
    Fixed,
    Zoned,
}

/// Read a civil value of one form and shape with the JSON layer's reader, then make sure the
/// typed operations can compute with it (day numbers and local seconds within `i128`).
///
/// Another kind, or another everyday form or shape, is `kind-mismatch`; a valid civil value that
/// no typed type holds (a zoned date, a `resolutionPolicy`, another zone intent) is
/// `unsupported-operand`.
fn read_as(json: &Json, form: Form, shape: Shape, expected: &str) -> Result<Civil, UtvResult> {
    if kind(json) != "civil" {
        return kind_mismatch(expected, json);
    }
    let mut c = read_civil(json)?;
    let v = &json["value"];
    let got = match (v["zone"]["type"].as_str(), v["zoneIntent"].as_str()) {
        (None | Some("floating"), None) => Some(Shape::Plain),
        (Some("fixed-offset"), None | Some("fixed-offset")) => Some(Shape::Fixed),
        (Some("iana"), None | Some("follow-zone-id")) => Some(Shape::Zoned),
        _ => None,
    };
    let held = matches!((c.form, &got), (Form::DateTime, Some(_)) | (_, Some(Shape::Plain)));
    if !held {
        return no_typed_type(format!("a {} with this zone and zone intent", c.form.as_str()));
    }
    only_keys(json, &["schema", "kind", "value"])?;
    only_keys(v, &["form", "fields", "calendar", "zone", "zoneIntent", "offsetAssertion"])?;
    if c.offset_assertion.is_some() && got != Some(Shape::Zoned) {
        return no_typed_type("an asserted offset without a named zone");
    }
    if c.form != form || got != Some(shape) {
        return kind_mismatch(expected, json);
    }
    if c.nano.is_some() && c.second.is_none() {
        c.second = Some(0);
    }
    if form.has_date() {
        c.day_number()?;
    }
    if form == Form::DateTime {
        c.local_seconds()?;
    }
    Ok(c)
}

fn of<T: Typed>(c: Civil) -> Result<T, Diagnostics> {
    T::read(&c.to_json()).map_err(rejected)
}

fn add(json: &Json, period: Period, month_end: MonthEnd) -> UtvResult {
    crate::ops::add(json, &period.json(), Some(&month_end.json()))
}

/// A day of the week, ISO 8601 numbering (Monday = 1).
#[derive(Debug, Clone, Copy, PartialEq, Eq, Hash, PartialOrd, Ord)]
pub enum Weekday {
    Monday,
    Tuesday,
    Wednesday,
    Thursday,
    Friday,
    Saturday,
    Sunday,
}

impl Weekday {
    const ALL: [Weekday; 7] = [Weekday::Monday, Weekday::Tuesday, Weekday::Wednesday, Weekday::Thursday, Weekday::Friday, Weekday::Saturday, Weekday::Sunday];

    /// 1 (Monday) to 7 (Sunday).
    pub fn number(self) -> u32 {
        self as u32 + 1
    }

    fn of_number(n: u64) -> Weekday {
        Weekday::ALL[(n as usize).clamp(1, 7) - 1]
    }
}

impl fmt::Display for Weekday {
    fn fmt(&self, f: &mut fmt::Formatter<'_>) -> fmt::Result {
        f.pad(match self {
            Weekday::Monday => "Monday",
            Weekday::Tuesday => "Tuesday",
            Weekday::Wednesday => "Wednesday",
            Weekday::Thursday => "Thursday",
            Weekday::Friday => "Friday",
            Weekday::Saturday => "Saturday",
            Weekday::Sunday => "Sunday",
        })
    }
}

// ---------- CivilDate ----------

/// A calendar date in the proleptic Gregorian calendar: `2026-11-05`.
///
/// ```
/// use utv::{CivilDate, Weekday};
/// let d = CivilDate::of(2026, 11, 5)?;
/// assert_eq!(d.to_string(), "2026-11-05");
/// assert_eq!((d.day_of_week(), d.iso_week()), (Weekday::Thursday, 45));
/// assert_eq!(d.add_days(30).to_string(), "2026-12-05");
/// assert!(CivilDate::of(2026, 2, 30).is_err());
/// # Ok::<(), utv::Diagnostics>(())
/// ```
#[derive(Clone, Copy, PartialEq, Eq, Hash, PartialOrd, Ord)]
pub struct CivilDate {
    year: i128,
    month: u8,
    day: u8,
}

typed_value!(CivilDate);

impl Typed for CivilDate {
    fn read(json: &Json) -> Result<Self, UtvResult> {
        let c = read_as(json, Form::Date, Shape::Plain, "a civil date")?;
        Ok(CivilDate { year: c.year, month: c.month as u8, day: c.day as u8 })
    }
    fn json(&self) -> Json {
        self.civil(Form::Date).to_json()
    }
}

impl CivilDate {
    /// The date, or `Err` with `month-out-of-range`, `day-out-of-range` (30 February) or
    /// `implementation-limit` (a year this library can't compute with).
    pub fn of(year: i128, month: u32, day: u32) -> Result<CivilDate, Diagnostics> {
        let mut c = Civil::new(Form::Date);
        (c.year, c.month, c.day) = (year, month, day);
        of(c)
    }

    fn civil(&self, form: Form) -> Civil {
        let mut c = Civil::new(form);
        (c.year, c.month, c.day) = (self.year, self.month as u32, self.day as u32);
        c
    }

    pub fn year(self) -> i128 {
        self.year
    }
    pub fn month(self) -> u32 {
        self.month as u32
    }
    pub fn day(self) -> u32 {
        self.day as u32
    }

    fn fields(self) -> Json {
        total(crate::ops::fields(&self.json()), |j| Ok(j.clone()))
    }
    pub fn day_of_week(self) -> Weekday {
        Weekday::of_number(self.fields()["dayOfWeek"].as_u64().unwrap_or(1))
    }
    pub fn day_of_year(self) -> u32 {
        self.fields()["dayOfYear"].as_u64().unwrap_or(0) as u32
    }
    /// The ISO 8601 week number, 1 to 53.
    pub fn iso_week(self) -> u32 {
        self.fields()["isoWeek"].as_u64().unwrap_or(0) as u32
    }
    /// The year the ISO week belongs to (2024-12-30 is in week 1 of 2025).
    pub fn iso_week_year(self) -> i128 {
        self.fields()["isoWeekYear"].as_str().and_then(|s| s.parse().ok()).unwrap_or(self.year)
    }
    pub fn days_in_month(self) -> u32 {
        self.fields()["daysInMonth"].as_u64().unwrap_or(0) as u32
    }
    pub fn is_leap_year(self) -> bool {
        self.fields()["isLeapYear"].as_bool().unwrap_or(false)
    }

    pub fn add_days(self, n: i64) -> CivilDate {
        total(add(&self.json(), Period::days(n), MonthEnd::Reject), CivilDate::read)
    }
    pub fn add_weeks(self, n: i64) -> CivilDate {
        total(add(&self.json(), Period::weeks(n), MonthEnd::Reject), CivilDate::read)
    }
    /// `none` (`month-end`) when the day doesn't exist in the resulting month: 31 January + 1 month.
    pub fn add_months(self, n: i64) -> Outcome<CivilDate> {
        outcome(add(&self.json(), Period::months(n), MonthEnd::Reject), CivilDate::read)
    }
    /// Months, clamping to the month's last day ([`MonthEnd::Constrain`]): 31 January + 1 month is 28 February.
    pub fn add_months_constrained(self, n: i64) -> CivilDate {
        total(add(&self.json(), Period::months(n), MonthEnd::Constrain), CivilDate::read)
    }
    /// `none` (`month-end`) for 29 February + 1 year.
    pub fn add_years(self, n: i64) -> Outcome<CivilDate> {
        outcome(add(&self.json(), Period::years(n), MonthEnd::Reject), CivilDate::read)
    }
    /// Years, clamping 29 February to 28 February ([`MonthEnd::Constrain`]).
    pub fn add_years_constrained(self, n: i64) -> CivilDate {
        total(add(&self.json(), Period::years(n), MonthEnd::Constrain), CivilDate::read)
    }
    /// Years and months, then weeks and days. `none` (`month-end`) when the day doesn't exist
    /// after the months; `invalid` (`kind-mismatch`) if the period has hours, minutes or seconds.
    pub fn add(&self, period: Period) -> Outcome<CivilDate> {
        self.add_with(period, MonthEnd::Reject)
    }
    /// [`CivilDate::add`] with an explicit month-end policy.
    pub fn add_with(&self, period: Period, month_end: MonthEnd) -> Outcome<CivilDate> {
        outcome(add(&self.json(), period, month_end), CivilDate::read)
    }

    /// The days from `self` to `other` (negative if `other` is earlier).
    pub fn days_until(self, other: CivilDate) -> i64 {
        total(crate::ops::days_between(&self.json(), &other.json()), |j| j.as_i64().ok_or_else(|| UtvResult::new(crate::Status::Error, vec![])))
    }

    /// This date at a time of day.
    pub fn at(self, time: CivilTime) -> CivilDateTime {
        CivilDateTime { date: self, time }
    }
}

// ---------- CivilYearMonth ----------

/// A year and month: `2026-11`.
#[derive(Clone, Copy, PartialEq, Eq, Hash, PartialOrd, Ord)]
pub struct CivilYearMonth {
    year: i128,
    month: u8,
}

typed_value!(CivilYearMonth);

impl Typed for CivilYearMonth {
    fn read(json: &Json) -> Result<Self, UtvResult> {
        let c = read_as(json, Form::YearMonth, Shape::Plain, "a civil year-month")?;
        Ok(CivilYearMonth { year: c.year, month: c.month as u8 })
    }
    fn json(&self) -> Json {
        let mut c = Civil::new(Form::YearMonth);
        (c.year, c.month) = (self.year, self.month as u32);
        c.to_json()
    }
}

impl CivilYearMonth {
    pub fn of(year: i128, month: u32) -> Result<CivilYearMonth, Diagnostics> {
        let mut c = Civil::new(Form::YearMonth);
        (c.year, c.month) = (year, month);
        of(c)
    }
    pub fn year(self) -> i128 {
        self.year
    }
    pub fn month(self) -> u32 {
        self.month as u32
    }
    pub fn add_months(self, n: i64) -> CivilYearMonth {
        total(add(&self.json(), Period::months(n), MonthEnd::Reject), CivilYearMonth::read)
    }
    pub fn add_years(self, n: i64) -> CivilYearMonth {
        total(add(&self.json(), Period::years(n), MonthEnd::Reject), CivilYearMonth::read)
    }
}

// ---------- CivilTime ----------

/// A time of day: `09:30`, `09:30:15.25`. It remembers whether a second was given; comparisons
/// treat a missing second as 0. There is no arithmetic: wrapping past midnight loses the day.
#[derive(Clone, Copy, PartialEq, Eq, Hash)]
pub struct CivilTime {
    hour: u8,
    minute: u8,
    second: Option<u8>,
    nano: u32,
}

typed_value!(CivilTime);

impl Typed for CivilTime {
    fn read(json: &Json) -> Result<Self, UtvResult> {
        Ok(CivilTime::of_civil(&read_as(json, Form::Time, Shape::Plain, "a civil time")?))
    }
    fn json(&self) -> Json {
        let mut c = Civil::new(Form::Time);
        self.set(&mut c);
        c.to_json()
    }
}

impl CivilTime {
    /// Hours and minutes, no second: `09:30`. `Err` (`field-out-of-range`) for hour 24 and beyond.
    pub fn of(hour: u32, minute: u32) -> Result<CivilTime, Diagnostics> {
        CivilTime::of_fields(hour, minute, None, 0)
    }
    /// With a second: `09:30:00`. Second 60 is `Err` (`leap-second`): it needs a leap-second table.
    pub fn of_hms(hour: u32, minute: u32, second: u32) -> Result<CivilTime, Diagnostics> {
        CivilTime::of_fields(hour, minute, Some(second), 0)
    }
    /// With a second and nanoseconds: `09:30:15.25`.
    pub fn of_hms_nano(hour: u32, minute: u32, second: u32, nano: u32) -> Result<CivilTime, Diagnostics> {
        CivilTime::of_fields(hour, minute, Some(second), nano)
    }

    fn of_fields(hour: u32, minute: u32, second: Option<u32>, nano: u32) -> Result<CivilTime, Diagnostics> {
        let mut c = Civil::new(Form::Time);
        (c.hour, c.minute, c.second, c.nano) = (hour, minute, second, Some(nano));
        of(c)
    }

    fn of_civil(c: &Civil) -> CivilTime {
        CivilTime { hour: c.hour as u8, minute: c.minute as u8, second: c.second.map(|s| s as u8), nano: c.nano.unwrap_or(0) }
    }

    fn set(&self, c: &mut Civil) {
        (c.hour, c.minute, c.second) = (self.hour as u32, self.minute as u32, self.second.map(u32::from));
        c.nano = Some(self.nano).filter(|n| *n != 0);
    }

    pub fn hour(self) -> u32 {
        self.hour as u32
    }
    pub fn minute(self) -> u32 {
        self.minute as u32
    }
    /// The second, if one was given.
    pub fn second(self) -> Option<u32> {
        self.second.map(u32::from)
    }
    pub fn nanosecond(self) -> u32 {
        self.nano
    }

    /// Time order with a missing second as 0, then (so that `Ord` agrees with `Eq`) a time
    /// without a second before the same time with one: `09:30` < `09:30:00` < `09:30:01`.
    fn key(&self) -> (u8, u8, u8, u32, bool) {
        (self.hour, self.minute, self.second.unwrap_or(0), self.nano, self.second.is_some())
    }
}

impl Ord for CivilTime {
    fn cmp(&self, other: &Self) -> Ordering {
        self.key().cmp(&other.key())
    }
}

impl PartialOrd for CivilTime {
    fn partial_cmp(&self, other: &Self) -> Option<Ordering> {
        Some(self.cmp(other))
    }
}

// ---------- CivilDateTime ----------

/// A date and time of day with no zone: `2026-11-05T14:30`. It is a wall-clock reading that
/// isn't tied to the timeline until you say where ([`CivilDateTime::in_zone`],
/// [`CivilDateTime::at_offset`]).
#[derive(Clone, Copy, PartialEq, Eq, Hash, PartialOrd, Ord)]
pub struct CivilDateTime {
    date: CivilDate,
    time: CivilTime,
}

typed_value!(CivilDateTime);

impl Typed for CivilDateTime {
    fn read(json: &Json) -> Result<Self, UtvResult> {
        Ok(CivilDateTime::of_civil(&read_as(json, Form::DateTime, Shape::Plain, "a civil date-time without a zone")?))
    }
    fn json(&self) -> Json {
        self.civil().to_json()
    }
}

impl CivilDateTime {
    /// `2026-11-05T14:30`, without seconds.
    pub fn of(year: i128, month: u32, day: u32, hour: u32, minute: u32) -> Result<CivilDateTime, Diagnostics> {
        CivilDateTime::of_fields(year, month, day, hour, minute, None, 0)
    }
    /// With a second: `2026-11-05T14:30:00`.
    pub fn of_hms(year: i128, month: u32, day: u32, hour: u32, minute: u32, second: u32) -> Result<CivilDateTime, Diagnostics> {
        CivilDateTime::of_fields(year, month, day, hour, minute, Some(second), 0)
    }
    /// With a second and nanoseconds.
    #[allow(clippy::too_many_arguments)]
    pub fn of_hms_nano(year: i128, month: u32, day: u32, hour: u32, minute: u32, second: u32, nano: u32) -> Result<CivilDateTime, Diagnostics> {
        CivilDateTime::of_fields(year, month, day, hour, minute, Some(second), nano)
    }

    #[allow(clippy::too_many_arguments)]
    fn of_fields(year: i128, month: u32, day: u32, hour: u32, minute: u32, second: Option<u32>, nano: u32) -> Result<CivilDateTime, Diagnostics> {
        let mut c = Civil::new(Form::DateTime);
        (c.year, c.month, c.day, c.hour, c.minute, c.second, c.nano) = (year, month, day, hour, minute, second, Some(nano));
        of(c)
    }

    fn of_civil(c: &Civil) -> CivilDateTime {
        CivilDateTime { date: CivilDate { year: c.year, month: c.month as u8, day: c.day as u8 }, time: CivilTime::of_civil(c) }
    }

    fn civil(&self) -> Civil {
        let mut c = self.date.civil(Form::DateTime);
        self.time.set(&mut c);
        c
    }

    pub fn date(self) -> CivilDate {
        self.date
    }
    pub fn time(self) -> CivilTime {
        self.time
    }

    fn plus(&self, period: Period) -> CivilDateTime {
        total(add(&self.json(), period, MonthEnd::Reject), CivilDateTime::read)
    }
    pub fn add_days(self, n: i64) -> CivilDateTime {
        self.plus(Period::days(n))
    }
    pub fn add_weeks(self, n: i64) -> CivilDateTime {
        self.plus(Period::weeks(n))
    }
    /// Wall-clock hours, carrying into days: 23:30 + 2 hours is 01:30 the next day.
    pub fn add_hours(self, n: i64) -> CivilDateTime {
        self.plus(Period::hours(n))
    }
    pub fn add_minutes(self, n: i64) -> CivilDateTime {
        self.plus(Period::minutes(n))
    }
    pub fn add_seconds(self, n: i64) -> CivilDateTime {
        self.plus(Period::seconds(n))
    }
    /// `none` (`month-end`) when the day doesn't exist in the resulting month.
    pub fn add_months(self, n: i64) -> Outcome<CivilDateTime> {
        outcome(add(&self.json(), Period::months(n), MonthEnd::Reject), CivilDateTime::read)
    }
    /// Months, clamping to the month's last day ([`MonthEnd::Constrain`]).
    pub fn add_months_constrained(self, n: i64) -> CivilDateTime {
        total(add(&self.json(), Period::months(n), MonthEnd::Constrain), CivilDateTime::read)
    }
    /// Any period: years and months, then weeks and days, then wall-clock time.
    pub fn add(&self, period: Period) -> Outcome<CivilDateTime> {
        self.add_with(period, MonthEnd::Reject)
    }
    pub fn add_with(&self, period: Period, month_end: MonthEnd) -> Outcome<CivilDateTime> {
        outcome(add(&self.json(), period, month_end), CivilDateTime::read)
    }

    /// This wall time, meant to follow the named IANA zone's rules: `2027-06-01T09:00[Europe/Paris]`.
    /// Which instant that is depends on the rules: see [`ZonedIntent::resolve`].
    pub fn in_zone(self, zone_id: &str) -> ZonedIntent {
        ZonedIntent { date_time: self, zone: zone_id.to_owned(), asserted: None }
    }

    /// This wall time at a fixed offset from UTC, in seconds east. `Err` (`offset-out-of-range`)
    /// beyond ±18:00.
    pub fn at_offset(self, offset_seconds: i32) -> Result<FixedOffsetDateTime, Diagnostics> {
        let mut c = self.civil();
        c.zone = Zone::Fixed(offset_seconds as i64);
        of(c)
    }
}

// ---------- FixedOffsetDateTime ----------

/// A wall time at a fixed offset from UTC: `2026-11-05T14:30+01:00`. It denotes exactly one
/// instant, so it compares by instant (and, for the same instant, by offset, then by its text).
#[derive(Clone, Copy, PartialEq, Eq, Hash)]
pub struct FixedOffsetDateTime {
    date_time: CivilDateTime,
    offset: i32,
}

typed_value!(FixedOffsetDateTime);

impl Typed for FixedOffsetDateTime {
    fn read(json: &Json) -> Result<Self, UtvResult> {
        let c = read_as(json, Form::DateTime, Shape::Fixed, "a date-time at a fixed offset")?;
        let Zone::Fixed(offset) = c.zone else { return kind_mismatch("a date-time at a fixed offset", json) };
        Ok(FixedOffsetDateTime { date_time: CivilDateTime::of_civil(&c), offset: offset as i32 })
    }
    fn json(&self) -> Json {
        let mut c = self.date_time.civil();
        c.zone = Zone::Fixed(self.offset as i64);
        c.to_json()
    }
}

impl FixedOffsetDateTime {
    pub fn date_time(self) -> CivilDateTime {
        self.date_time
    }
    /// Seconds east of UTC.
    pub fn offset_seconds(self) -> i32 {
        self.offset
    }
    /// The instant this wall time denotes. No rules are needed for a fixed offset.
    pub fn to_instant(self) -> Instant {
        total(crate::zones::resolve(&self.json(), &Rules::empty(), None), Instant::read)
    }
}

impl Ord for FixedOffsetDateTime {
    fn cmp(&self, other: &Self) -> Ordering {
        self.to_instant().cmp(&other.to_instant()).then(self.offset.cmp(&other.offset)).then(self.date_time.cmp(&other.date_time))
    }
}

impl PartialOrd for FixedOffsetDateTime {
    fn partial_cmp(&self, other: &Self) -> Option<Ordering> {
        Some(self.cmp(other))
    }
}

// ---------- ZonedIntent ----------

/// A wall time meant to follow a named zone's rules: `2027-06-01T09:00[Europe/Paris]`, maybe
/// with the offset someone asserted, `2027-06-01T09:00+02:00[Europe/Paris]`.
///
/// It is an intention, not an instant: which instant it is (one, two in an overlap, none in a
/// gap) depends on zone rules, so there are no comparison operators. Use
/// [`TemporalContext::compare`], or [`ZonedIntent::resolve`] first.
#[derive(Clone, PartialEq, Eq, Hash)]
pub struct ZonedIntent {
    date_time: CivilDateTime,
    zone: String,
    asserted: Option<i32>,
}

typed_value!(ZonedIntent);

impl Typed for ZonedIntent {
    fn read(json: &Json) -> Result<Self, UtvResult> {
        let c = read_as(json, Form::DateTime, Shape::Zoned, "a zoned date-time")?;
        let Zone::Iana(zone) = &c.zone else { return kind_mismatch("a zoned date-time", json) };
        Ok(ZonedIntent { date_time: CivilDateTime::of_civil(&c), zone: zone.clone(), asserted: c.offset_assertion.map(|o| o as i32) })
    }
    fn json(&self) -> Json {
        let mut c = self.date_time.civil();
        c.zone = Zone::Iana(self.zone.clone());
        c.offset_assertion = self.asserted.map(i64::from);
        c.to_json()
    }
}

impl ZonedIntent {
    pub fn date_time(&self) -> CivilDateTime {
        self.date_time
    }
    pub fn zone_id(&self) -> &str {
        &self.zone
    }
    /// The offset someone asserted (`+02:00` in `09:00+02:00[Europe/Paris]`), in seconds east.
    pub fn asserted_offset_seconds(&self) -> Option<i32> {
        self.asserted
    }

    /// Wall-clock arithmetic in the same zone. It drops an asserted offset, which described the
    /// old wall time; resolve the result to learn the new one.
    fn plus(&self, period: Period) -> ZonedIntent {
        total(add(&self.json(), period, MonthEnd::Reject), ZonedIntent::read)
    }
    /// The same wall time `n` days later: 12:00 stays 12:00 across a DST change.
    pub fn add_days(&self, n: i64) -> ZonedIntent {
        self.plus(Period::days(n))
    }
    pub fn add_weeks(&self, n: i64) -> ZonedIntent {
        self.plus(Period::weeks(n))
    }
    /// Wall-clock hours (not elapsed time): add `PosixSeconds` to the resolved instant for that.
    pub fn add_hours(&self, n: i64) -> ZonedIntent {
        self.plus(Period::hours(n))
    }
    pub fn add_minutes(&self, n: i64) -> ZonedIntent {
        self.plus(Period::minutes(n))
    }
    pub fn add_seconds(&self, n: i64) -> ZonedIntent {
        self.plus(Period::seconds(n))
    }
    /// `none` (`month-end`) when the day doesn't exist in the resulting month.
    pub fn add_months(&self, n: i64) -> Outcome<ZonedIntent> {
        outcome(add(&self.json(), Period::months(n), MonthEnd::Reject), ZonedIntent::read)
    }

    /// The instant(s) under [`ResolutionPolicy::STRICT`]: see [`TemporalContext::resolve_with`].
    pub fn resolve(&self, ctx: &TemporalContext) -> Outcome<Instant> {
        ctx.resolve(self)
    }
    /// The instant(s) under `policy`.
    pub fn resolve_with(&self, ctx: &TemporalContext, policy: ResolutionPolicy) -> Outcome<Instant> {
        ctx.resolve_with(self, policy)
    }
}
