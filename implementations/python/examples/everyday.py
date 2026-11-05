"""Everyday date/time tasks: Python's datetime next to UTV.

Run from implementations/python:

    PYTHONPATH=src python3 examples/everyday.py

Each task does the ordinary thing with datetime and zoneinfo, then with UTV.
The comments show what each row prints. Rows marked ‡ depend on this machine
(the clock, its tz database or the Python version); tests/test_examples.py
checks every other line against examples/everyday.expected.txt.
"""

import calendar
from datetime import date, datetime, time, timedelta, timezone
from pathlib import Path
from time import time_ns
from zoneinfo import ZoneInfo, ZoneInfoNotFoundError

from utv import (
    Bound, CivilDate, CivilDateTime, CivilTime, ElapsedSeconds, Gap, Instant, Intent, Interval, Invalid,
    MonthEnd, Multiple, NoneOf, Overlap, PosixSeconds, ResolutionPolicy, TemporalContext, Unique, ZoneRules,
    from_native, parse, to_native,
)

# UTV's zone rules: a small pinned file (New York, Paris, Reykjavik; 2026-2027), named once.
ctx = TemporalContext(ZoneRules.load(Path(__file__).parents[3] / "conformance/rules/demo-zones.json"))

# Python's zone rules: the host's tz database. Without one, the Python rows that need it are skipped.
try:
    PARIS: ZoneInfo | None = ZoneInfo("Europe/Paris")
    NEW_YORK: ZoneInfo | None = ZoneInfo("America/New_York")
except ZoneInfoNotFoundError:
    PARIS = NEW_YORK = None


def row(who: str, value: object, note: str = "", host: bool = False) -> None:
    """Print one aligned output row; a long value puts its note on the next line.
    host=True marks a row that depends on this machine."""
    text, mark = str(value), " ‡" if host else ""
    if len(text) > 33 and note:
        print(f"     {who:<7}{text}{mark}")
        who, text = "", ""
    print(f"     {who:<7}{text:<33} {note}".rstrip() + mark)


print("Everyday date/time tasks: Python's datetime and UTV   (‡ depends on this machine)")

print("\n1. Now")
print("   Now")
# BEGIN now
now = datetime.now(timezone.utc)
row("Python", now.isoformat(timespec="seconds"), "datetime.now(timezone.utc)", host=True)
# 2026-11-05T13:30:00+00:00   datetime.now(timezone.utc) ‡
stamp = Instant.from_unix_nanoseconds(time_ns())  # UTV never reads the clock: you pass the reading in
row("UTV", stamp, "from time_ns()", host=True)  # 2026-11-05T13:30:00.123456789Z   from time_ns() ‡
# END now

print("\n2. A date")
print("   5 November 2026")
# BEGIN a-date
d = date(2026, 11, 5)
row("Python", d)  # 2026-11-05
row("Python", repr(d))  # datetime.date(2026, 11, 5)
day = CivilDate.of(2026, 11, 5)
row("UTV", day)  # 2026-11-05
row("UTV", repr(day))  # CivilDate(2026-11-05)
# END a-date
print("   Day of week and ISO week")
# BEGIN weekday-week
d = date(2026, 11, 5)
row("Python", f"{d:%A}, ISO week {d.isocalendar().week}", "%A follows the locale")
# Thursday, ISO week 45   %A follows the locale
day = CivilDate.of(2026, 11, 5)
row("UTV", f"{day.day_of_week}, ISO week {day.iso_week}", f"day {day.day_of_year} of the year")
# Thursday, ISO week 45   day 309 of the year
# END weekday-week

print("\n3. Invalid dates")
print("   30 February 2026")
# BEGIN feb-30
try:
    date(2026, 2, 30)
except ValueError:
    row("Python", "ValueError", "raised by date()")  # ValueError   raised by date()
try:
    CivilDate.of(2026, 2, 30)
except ValueError as error:
    row("UTV", "ValueError", str(error))  # ValueError   invalid (day-out-of-range): day 30 is not in 1..28
match CivilDate.parse("2026-02-30"):  # text from outside: an Outcome, never an exception
    case Unique(value=parsed):
        row("UTV", parsed)
    case Invalid(diagnostics=why):
        row("UTV", "invalid", f"{why[0].message} ({why[0].code})")
        # invalid   day 30 is not in 1..28 (day-out-of-range)
    case other:
        row("UTV", other)
# END feb-30

print("\n4. Adding")
print("   5 Nov + 30 days")
# BEGIN add-days
row("Python", date(2026, 11, 5) + timedelta(days=30))  # 2026-12-05
row("UTV", CivilDate.of(2026, 11, 5).add_days(30))  # 2026-12-05
# END add-days
print("   31 Jan 2026 + 1 month")
# BEGIN month-end
jan31 = date(2026, 1, 31)
try:
    jan31.replace(month=2)
except ValueError:
    row("Python", "ValueError", "date.replace(month=2); there is no add-months")
    # ValueError   date.replace(month=2); there is no add-months
last_day = calendar.monthrange(2026, 2)[1]  # what people write by hand: clamp to the month end
row("Python", jan31.replace(month=2, day=min(jan31.day, last_day)), "clamped by hand")
# 2026-02-28   clamped by hand

utv_jan31 = CivilDate.of(2026, 1, 31)
match utv_jan31.add_months(1):  # Outcome[CivilDate]
    case Unique(value=feb):
        row("UTV", feb)
    case NoneOf(diagnostics=why):  # this runs
        row("UTV", "no such date", f"{why[0].message} ({why[0].code})")
        # no such date   2026-02 has no day 31 (month-end)
    case other:
        row("UTV", other)
row("UTV", utv_jan31.add_months(1, MonthEnd.CONSTRAIN), "with MonthEnd.CONSTRAIN")
# 2026-02-28   with MonthEnd.CONSTRAIN
# END month-end

print("\n5. Days between")
print("   5 Nov → 25 Dec")
# BEGIN days-between
row("Python", (date(2026, 12, 25) - date(2026, 11, 5)).days)  # 50
row("UTV", CivilDate.of(2026, 11, 5).days_until(CivilDate.of(2026, 12, 25)))  # 50
# END days-between
print("   Which comes first")
# BEGIN compare-dates
row("Python", date(2026, 11, 5) < date(2026, 12, 25))  # True
row("UTV", CivilDate.of(2026, 11, 5) < CivilDate.of(2026, 12, 25))  # True
if PARIS and NEW_YORK:
    paris_9am, new_york_9am = datetime(2026, 11, 5, 9, tzinfo=PARIS), datetime(2026, 11, 5, 9, tzinfo=NEW_YORK)
    row("Python", paris_9am < new_york_9am, "09:00 Paris < 09:00 New York", host=True)
    # True   09:00 Paris < 09:00 New York ‡
utv_paris = CivilDateTime.of(2026, 11, 5, 9, 0).in_zone("Europe/Paris")
utv_new_york = CivilDateTime.of(2026, 11, 5, 9, 0).in_zone("America/New_York")
row("UTV", ctx.compare(utv_paris, utv_new_york), "zoned values have no <; the order needs rules")
# before   zoned values have no <; the order needs rules
# END compare-dates

print("\n6. Times and date-times")
print("   09:30")
# BEGIN time-of-day
row("Python", time(9, 30))  # 09:30:00
row("UTV", CivilTime.of(9, 30), "as written; 09:30:00 is a different value")
# 09:30   as written; 09:30:00 is a different value
# END time-of-day
print("   23:30 + 2 hours")
# BEGIN datetime-plus-hours
row("Python", datetime(2026, 11, 5, 23, 30) + timedelta(hours=2))  # 2026-11-06 01:30:00
row("UTV", CivilDateTime.of(2026, 11, 5, 23, 30).add_hours(2))  # 2026-11-06T01:30
# END datetime-plus-hours

print("\n7. Parsing with offsets")
print("   Z vs +00:00")
# BEGIN parse-offsets
zero = datetime.fromisoformat("2026-11-05T14:30:00+00:00")
row("Python", repr(zero.tzinfo), "from +00:00")  # datetime.timezone.utc   from +00:00
try:
    z = datetime.fromisoformat("2026-11-05T14:30:00Z")
    row("Python", repr(z.tzinfo), "from Z: the same object", host=True)
    # datetime.timezone.utc   from Z: the same object ‡
except ValueError:
    row("Python", "ValueError", "from Z (Python 3.10)", host=True)
utc = parse("2026-11-05T14:30:00Z").single()  # UTC is known, the local offset isn't
offset_zero = parse("2026-11-05T14:30:00+00:00").single()  # the local offset is 0
row("UTV", utc, type(utc).__name__)  # 2026-11-05T14:30:00Z   Instant
row("UTV", offset_zero, type(offset_zero).__name__)  # 2026-11-05T14:30:00+00:00   FixedOffsetDateTime
# END parse-offsets

print("\n8. Time zones")
print("   09:00 in Paris, seen from New York")
# BEGIN meeting-in-paris
if PARIS and NEW_YORK:
    meeting = datetime(2027, 6, 1, 9, 0, tzinfo=PARIS)
    row("Python", meeting.astimezone(NEW_YORK), "host tz database", host=True)
    # 2027-06-01 03:00:00-04:00   host tz database ‡
utv_meeting = CivilDateTime.of(2027, 6, 1, 9, 0).in_zone("Europe/Paris")
starts = utv_meeting.resolve(ctx).single()  # 09:00 happens exactly once that day in Paris
row("UTV", starts.in_zone("America/New_York", ctx).single(), "demo-zones rules")
# 2027-06-01T03:00:00-04:00[America/New_York]
#                                   demo-zones rules
# END meeting-in-paris

print("\n9. DST gap")
print("   02:30 on 8 March 2026 in New York")
# BEGIN dst-gap
if NEW_YORK:
    wall = datetime(2026, 3, 8, 2, 30, tzinfo=NEW_YORK)
    row("Python", wall, "accepted without a word", host=True)
    # 2026-03-08 02:30:00-05:00   accepted without a word ‡
    row("Python", wall.astimezone(timezone.utc).astimezone(NEW_YORK), "after a trip through UTC", host=True)
    # 2026-03-08 03:30:00-04:00   after a trip through UTC ‡
gap = CivilDateTime.of(2026, 3, 8, 2, 30).in_zone("America/New_York")
resolved = gap.resolve(ctx)  # Outcome[Instant]
match resolved:
    case Unique(value=instant):
        row("UTV", instant)
    case NoneOf(diagnostics=why):  # this runs: clocks jump from 02:00 to 03:00
        row("UTV", "no such time", f"{why[0].message} ({why[0].code})")
        # no such time   wall time skipped in America/New_York (gap)
    case _:
        row("UTV", resolved)
shifted = gap.resolve(ctx, ResolutionPolicy(gap=Gap.SHIFT_FORWARD)).single()
row("UTV", shifted.in_zone("America/New_York", ctx).single(), "with Gap.SHIFT_FORWARD")
# 2026-03-08T03:30:00-04:00[America/New_York]
#                                   with Gap.SHIFT_FORWARD
# END dst-gap

print("\n10. DST overlap")
print("   01:30 on 1 November 2026 in New York")
# BEGIN dst-overlap
if NEW_YORK:
    wall = datetime(2026, 11, 1, 1, 30, tzinfo=NEW_YORK)
    row("Python", wall, "fold=0 (the default): EDT, no warning", host=True)
    # 2026-11-01 01:30:00-04:00   fold=0 (the default): EDT, no warning ‡
    row("Python", wall.replace(fold=1), "fold=1: EST", host=True)  # 2026-11-01 01:30:00-05:00   fold=1: EST ‡
overlap = CivilDateTime.of(2026, 11, 1, 1, 30).in_zone("America/New_York")
resolved = overlap.resolve(ctx)  # Outcome[Instant]
match resolved:
    case Unique(value=instant):
        row("UTV", instant)
    case Multiple(values=instants):  # this runs: clocks go back from 02:00 to 01:00
        for n, instant in enumerate(instants, 1):
            row("UTV", instant.in_zone("America/New_York", ctx).single(), f"{n} of {len(instants)}")
        # 2026-11-01T01:30:00-04:00[America/New_York]
        #                                   1 of 2
        # 2026-11-01T01:30:00-05:00[America/New_York]
        #                                   2 of 2
    case _:
        row("UTV", resolved)
earliest = overlap.resolve(ctx, ResolutionPolicy(overlap=Overlap.EARLIEST)).single()
row("UTV", earliest, "with Overlap.EARLIEST")  # 2026-11-01T05:30:00Z   with Overlap.EARLIEST
# END dst-overlap

print("\n11. Timestamps")
print("   Exact fractions and differences")
# BEGIN timestamps
stamp_float = datetime.fromtimestamp(1793885400.123456789, tz=timezone.utc)
row("Python", stamp_float.isoformat(), "a float timestamp")  # 2026-11-05T13:30:00.123457+00:00   a float ...
row("Python", timedelta(seconds=1) / 3)  # 0:00:00.333333
precise = Instant.from_unix_seconds(1793885400, 123_456_789)
row("UTV", precise)  # 2026-11-05T13:30:00.123456789Z
row("UTV", Instant.from_unix_seconds(1793885400).until(precise))  # 0.123456789 s (POSIX)
row("UTV", PosixSeconds.of(1, 3), "exact; it has no decimal form")  # 1/3 s (POSIX)   exact; it has no ...
# END timestamps

print("\n12. Durations vs periods")
print("   One day vs 86,400 seconds across DST")
# BEGIN day-vs-24h
if NEW_YORK:
    noon = datetime(2026, 10, 31, 12, 0, tzinfo=NEW_YORK)
    next_noon = noon + timedelta(days=1)
    row("Python", next_noon, "noon + timedelta(days=1)", host=True)
    # 2026-11-01 12:00:00-05:00   noon + timedelta(days=1) ‡
    row("Python", next_noon - noon, "subtracted as wall times", host=True)
    # 1 day, 0:00:00   subtracted as wall times ‡
    elapsed = next_noon.astimezone(timezone.utc) - noon.astimezone(timezone.utc)
    row("Python", elapsed, "subtracted in UTC", host=True)  # 1 day, 1:00:00   subtracted in UTC ‡
utv_noon = CivilDateTime.of(2026, 10, 31, 12, 0).in_zone("America/New_York")
utv_next_noon = utv_noon.add_days(1)  # a calendar day: 12:00 again
row("UTV", utv_next_noon, "add_days(1)")
# 2026-11-01T12:00[America/New_York]
#                                   add_days(1)
start, end = utv_noon.resolve(ctx).single(), utv_next_noon.resolve(ctx).single()
row("UTV", start.until(end), "elapsed: 25 hours")  # 90000 s (POSIX)   elapsed: 25 hours
later = start.add(PosixSeconds.of(86400))
row("UTV", later.in_zone("America/New_York", ctx).single(), "+ 86,400 POSIX seconds")
# 2026-11-01T11:00:00-05:00[America/New_York]
#                                   + 86,400 POSIX seconds
row("UTV", start.add(ElapsedSeconds.of(86400)), "+ 86,400 SI seconds")
# unsupported (needs-leap-seconds)   + 86,400 SI seconds
# END day-vs-24h

print("\n13. Ranges")
print("   November, open-ended and unknown ends")
# BEGIN ranges
first, last = date(2026, 11, 1), date(2026, 12, 1)
row("Python", first <= date(2026, 11, 15) < last, "no range type: compare by hand")
# True   no range type: compare by hand
lease_end = None
row("Python", lease_end is None or date(2027, 6, 1) < lease_end, "end=None: no end, or not known yet?")
# True   end=None: no end, or not known yet?
november = Interval.half_open(CivilDate.of(2026, 11, 1), CivilDate.of(2026, 12, 1))
row("UTV", november, f"15 Nov: {november.contains(CivilDate.of(2026, 11, 15))}")
# [2026-11-01, 2026-12-01)   15 Nov: definitely
row("UTV", november, f"1 Dec: {november.contains(CivilDate.of(2026, 12, 1))}")
# [2026-11-01, 2026-12-01)   1 Dec: impossible
open_ended = Interval.of(Bound.included(CivilDate.of(2026, 11, 1)), Bound.UNBOUNDED)
row("UTV", open_ended, f"1 Jan 3000: {open_ended.contains(CivilDate.of(3000, 1, 1))}")
# [2026-11-01, ..)   1 Jan 3000: definitely
unknown_end = Interval.of(Bound.included(CivilDate.of(2026, 11, 1)), Bound.UNKNOWN)
row("UTV", unknown_end, f"1 Jun 2027: {unknown_end.contains(CivilDate.of(2027, 6, 1))}")
# [2026-11-01, ?)   1 Jun 2027: possibly
# END ranges

print("\n14. Round trip")
print("   Native → UTV → native")
# BEGIN round-trip
original = datetime(2026, 11, 5, 14, 30, tzinfo=timezone(timedelta(hours=1)))
fixed = from_native(original, Intent.OFFSET).single()  # you name the intent: an offset date-time
row("UTV", fixed, type(fixed).__name__)  # 2026-11-05T14:30:00+01:00   FixedOffsetDateTime
back = to_native(fixed).single()
row("Python", back, f"equal to the original: {back == original}")
# 2026-11-05 14:30:00+01:00   equal to the original: True
naive = datetime(2026, 11, 5, 14, 30)  # no zone, like datetime.now()
row("UTV", from_native(naive, Intent.INSTANT), "never read as local time")
# unsupported (local-zone)   never read as local time
row("UTV", to_native(CivilDate.of(10000, 1, 1)), "datetime.date stops at 9999")
# unsupported (out-of-native-range)   datetime.date stops at 9999
# END round-trip
