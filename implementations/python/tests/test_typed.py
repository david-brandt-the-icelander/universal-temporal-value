"""The typed layer (docs/library-api.md): at least one test per row of the §2 table, outcomes,
policies, the context, text and JSON round trips, and constructors that reject impossible values."""

import datetime as dt
import json
import unittest
from fractions import Fraction
from pathlib import Path

import utv
from utv import (
    Bound, CivilDate, CivilDateTime, CivilTime, CivilYearMonth, ElapsedSeconds, FixedOffsetDateTime, Gap, Instant,
    Intent, Interval, Invalid, MonthEnd, Multiple, NoneOf, Ordering, Overlap, OutcomeError, Period, PosixSeconds,
    ResolutionPolicy, TemporalContext, Truth, Unique, Unsupported, Weekday, ZonedIntent, ZoneRules,
)

ROOT = Path(__file__).resolve().parents[3]
CTX = TemporalContext(ZoneRules.load(ROOT / "conformance" / "rules" / "demo-zones.json"))
NY = "America/New_York"


def codes(outcome):
    return [d.code for d in outcome.diagnostics]


class CivilDateRow(unittest.TestCase):
    def test_total(self):
        d = CivilDate.of(2026, 11, 5)
        self.assertEqual(d.add_days(30), CivilDate(2026, 12, 5))
        self.assertEqual(d.add_weeks(-1), CivilDate(2026, 10, 29))
        self.assertEqual(CivilDate(2026, 1, 31).add_months(1, MonthEnd.CONSTRAIN), CivilDate(2026, 2, 28))
        self.assertEqual(CivilDate(2024, 2, 29).add_years(1, "constrain"), CivilDate(2025, 2, 28))
        self.assertEqual((d.year, d.month, d.day), (2026, 11, 5))
        self.assertIs(d.day_of_week, Weekday.THURSDAY)
        self.assertEqual(str(d.day_of_week), "Thursday")
        self.assertEqual((d.day_of_year, d.iso_week, d.iso_week_year, d.days_in_month, d.is_leap_year),
                         (309, 45, 2026, 30, False))
        self.assertEqual(CivilDate(2021, 1, 3).iso_week_year, 2020)
        self.assertEqual(d.days_until(CivilDate(2026, 12, 25)), 50)
        self.assertEqual(d.at(CivilTime(9, 30)), CivilDateTime.of(2026, 11, 5, 9, 30))
        self.assertTrue(d < CivilDate(2026, 12, 25) and d <= d and d >= d and not d > d)

    def test_partial(self):
        jan31 = CivilDate(2026, 1, 31)
        none = jan31.add_months(1)
        self.assertIsInstance(none, NoneOf)
        self.assertEqual(codes(none), ["month-end"])
        self.assertEqual(jan31.add_months(2), Unique(CivilDate(2026, 3, 31)))
        self.assertIsInstance(CivilDate(2024, 2, 29).add_years(1), NoneOf)
        self.assertEqual(jan31.add(Period(months=1, days=1)).status, "none")
        self.assertEqual(jan31.add(Period(months=1), MonthEnd.CONSTRAIN).single(), CivilDate(2026, 2, 28))
        hours = jan31.add(Period(hours=1))
        self.assertIsInstance(hours, Invalid)
        self.assertEqual(codes(hours), ["kind-mismatch"])


class OtherCivilRows(unittest.TestCase):
    def test_year_month(self):
        nov = CivilYearMonth.of(2026, 11)
        self.assertEqual(nov.add_months(2), CivilYearMonth(2027, 1))
        self.assertEqual(nov.add_years(-1), CivilYearMonth(2025, 11))
        self.assertLess(nov, CivilYearMonth(2026, 12))
        self.assertEqual(str(nov), "2026-11")

    def test_time(self):
        short, long = CivilTime.of(9, 30), CivilTime.of(9, 30, 0)
        self.assertNotEqual(short, long)  # representation equality: 09:30 is not 09:30:00
        self.assertTrue(short <= long and short >= long and not short < long)
        self.assertLess(short, CivilTime(9, 30, 0, 1))
        self.assertEqual(str(CivilTime(9, 30, 15, 250_000_000)), "09:30:15.25")
        self.assertFalse(hasattr(short, "add_hours"))  # no arithmetic: wrapping past midnight loses the day

    def test_datetime_total(self):
        late = CivilDateTime.of(2026, 11, 5, 23, 30)
        self.assertEqual(late.add_hours(2), CivilDateTime.of(2026, 11, 6, 1, 30))
        self.assertEqual(late.add_minutes(-31), CivilDateTime.of(2026, 11, 5, 22, 59))
        self.assertEqual(late.add_seconds(30), CivilDateTime.of(2026, 11, 5, 23, 30, 30))
        self.assertEqual(late.add_days(1).add_weeks(1), CivilDateTime.of(2026, 11, 13, 23, 30))
        self.assertEqual(CivilDateTime.of(2026, 1, 31, 9, 0).add_months(1, MonthEnd.CONSTRAIN),
                         CivilDateTime.of(2026, 2, 28, 9, 0))
        self.assertEqual((late.date, late.time), (CivilDate(2026, 11, 5), CivilTime(23, 30)))
        self.assertEqual((late.year, late.hour, late.second, late.nanosecond), (2026, 23, None, 0))
        self.assertEqual(late.in_zone("Europe/Paris"), ZonedIntent(late, "Europe/Paris"))
        self.assertEqual(late.at_offset(3600), FixedOffsetDateTime(late, 3600))
        self.assertLess(late, late.add_seconds(1))

    def test_datetime_partial(self):
        jan31 = CivilDateTime.of(2026, 1, 31, 9, 0)
        self.assertEqual(codes(jan31.add_months(1)), ["month-end"])
        self.assertEqual(jan31.add(Period(days=1, hours=1)).single(), CivilDateTime.of(2026, 2, 1, 10, 0))

    def test_fixed_offset(self):
        a = CivilDateTime.of(2026, 11, 5, 14, 30).at_offset(3600)
        b = CivilDateTime.of(2026, 11, 5, 13, 30).at_offset(0)
        self.assertEqual(a.to_instant(), Instant.from_unix_seconds(1793885400))
        self.assertEqual((a.date_time, a.offset_seconds), (CivilDateTime.of(2026, 11, 5, 14, 30), 3600))
        self.assertNotEqual(a, b)  # different values ...
        self.assertTrue(a <= b and a >= b)  # ... at the same instant
        self.assertLess(b, CivilDateTime.of(2026, 11, 5, 13, 31).at_offset(0))


class ZonedRow(unittest.TestCase):
    def test_total(self):
        asserted = ZonedIntent.parse("2026-03-28T09:00+01:00[Europe/Paris]").single()
        self.assertEqual(asserted.asserted_offset_seconds, 3600)
        next_day = asserted.add_days(1)
        self.assertEqual(str(next_day), "2026-03-29T09:00[Europe/Paris]")  # the asserted offset is dropped
        self.assertIsNone(next_day.asserted_offset_seconds)
        z = CivilDateTime.of(2026, 11, 5, 23, 30).in_zone(NY)
        self.assertEqual(z.add_hours(2).date_time, CivilDateTime.of(2026, 11, 6, 1, 30))
        self.assertEqual((z.zone_id, z.date_time.day), (NY, 5))

    def test_partial(self):
        meeting = CivilDateTime.of(2027, 6, 1, 9, 0).in_zone("Europe/Paris")
        self.assertEqual(meeting.resolve(CTX), Unique(Instant.from_unix_seconds(1811833200)))
        self.assertEqual(meeting.resolve(CTX), CTX.resolve(meeting))
        self.assertEqual(codes(CivilDateTime.of(2026, 1, 31, 9, 0).in_zone(NY).add_months(1)), ["month-end"])
        with self.assertRaises(TypeError):
            meeting < meeting  # noqa: B015 - no comparison operators on zoned values

    def test_resolve_policies(self):
        gap = CivilDateTime.of(2026, 3, 8, 2, 30).in_zone(NY)
        self.assertEqual(codes(gap.resolve(CTX)), ["gap"])
        shifted = gap.resolve(CTX, ResolutionPolicy(gap=Gap.SHIFT_FORWARD)).single()
        self.assertEqual(str(shifted), "2026-03-08T07:30:00Z")
        overlap = CivilDateTime.of(2026, 11, 1, 1, 30).in_zone(NY)
        both = overlap.resolve(CTX)
        self.assertIsInstance(both, Multiple)
        self.assertEqual([str(i) for i in both.values], ["2026-11-01T05:30:00Z", "2026-11-01T06:30:00Z"])
        self.assertEqual(codes(both), ["ambiguous"])
        self.assertEqual(overlap.resolve(CTX, ResolutionPolicy(overlap="latest")).single(), both.values[1])
        self.assertEqual(codes(overlap.resolve(CTX, ResolutionPolicy.ALL_CANDIDATES)), [])
        mismatch = ZonedIntent(CivilDateTime.of(2027, 6, 1, 9, 0), "Europe/Paris", 3600).resolve(CTX)
        self.assertEqual((mismatch.status, codes(mismatch)), ("invalid", ["offset-mismatch"]))
        self.assertEqual(codes(CivilDateTime.of(2026, 7, 1, 9, 0).in_zone("Mars/Olympus").resolve(CTX)), ["unknown-zone"])
        self.assertEqual(codes(CivilDateTime.of(2030, 1, 1, 9, 0).in_zone(NY).resolve(CTX)), ["outside-rule-window"])


class InstantRow(unittest.TestCase):
    def test_total(self):
        t = Instant.from_unix_seconds(1793885400, 123_456_789)
        self.assertEqual(t, Instant.from_unix_nanoseconds(1793885400_123456789))
        self.assertEqual((t.unix_seconds, t.nanoseconds), (1793885400, 123_456_789))
        self.assertEqual(t.add(PosixSeconds.of(86400)), Instant.from_unix_seconds(1793971800, 123_456_789))
        self.assertEqual(Instant.from_unix_seconds(1793885400).until(t), PosixSeconds.of(123456789, 10**9))
        self.assertEqual(str(Instant.from_unix_seconds(1793885400).at_offset(-18000)), "2026-11-05T08:30:00-05:00")
        self.assertLess(Instant.from_unix_seconds(-1), Instant.from_unix_seconds(0))
        self.assertEqual(Instant.from_unix_seconds(-1, 500_000_000).unix_seconds, -1)
        third = Instant(Fraction(-2, 3))  # -0.666… s: floor is -1 s, then 1/3 s
        self.assertEqual((third.unix_seconds, third.sub_second, third.nanoseconds), (-1, Fraction(1, 3), 333_333_333))
        self.assertEqual(t.sub_second, Fraction(123_456_789, 10**9))

    def test_partial(self):
        t = Instant.from_unix_seconds(1811833200)
        self.assertEqual(str(t.in_zone(NY, CTX).single()), "2027-06-01T03:00:00-04:00[America/New_York]")
        self.assertEqual(t.in_zone(NY, CTX), CTX.in_zone(t, NY))
        self.assertIsInstance(Instant.from_unix_seconds(0).in_zone(NY, CTX), Unsupported)
        self.assertEqual(codes(t.add(ElapsedSeconds.of(1))), ["needs-leap-seconds"])


class AmountRows(unittest.TestCase):
    def test_period(self):
        p = Period(years=1, months=2, days=-3)
        self.assertEqual((p.years, p.months, p.weeks, p.days), (1, 2, 0, -3))
        self.assertEqual(p.negate(), Period(years=-1, months=-2, days=3))
        self.assertEqual(Period.of_months(1), Period.of(months=1))
        self.assertEqual((Period.of_days(30), Period.of_weeks(2), Period.of_years(1)),
                         (Period(days=30), Period(weeks=2), Period(years=1)))
        self.assertEqual(str(Period.of(days=1, hours=2).negate()), "-P1DT2H")
        self.assertEqual(str(Period()), "P0D")
        self.assertTrue(str(p).startswith("{"))  # mixed signs have no text form: printed as JSON

    def test_seconds(self):
        self.assertEqual(str(PosixSeconds.of(14403, 4)), "3600.75 s (POSIX)")
        self.assertEqual(str(PosixSeconds.of(1, 3)), "1/3 s (POSIX)")
        self.assertEqual(repr(ElapsedSeconds.of(86400)), "ElapsedSeconds(86400 s (SI))")
        self.assertEqual(PosixSeconds.of(3).negate(), PosixSeconds(Fraction(-3)))
        self.assertEqual(ElapsedSeconds.of(1, 2).negate().seconds, Fraction(-1, 2))
        self.assertNotEqual(PosixSeconds.of(1), ElapsedSeconds.of(1))


class IntervalRow(unittest.TestCase):
    def test_contains_and_overlaps(self):
        nov = Interval.half_open(CivilDate(2026, 11, 1), CivilDate(2026, 12, 1))
        dec = Interval.half_open(CivilDate(2026, 12, 1), CivilDate(2027, 1, 1))
        self.assertIs(nov.contains(CivilDate(2026, 11, 30)), Truth.DEFINITELY)
        self.assertIs(nov.contains(CivilDate(2026, 12, 1)), Truth.IMPOSSIBLE)
        self.assertIs(nov.overlaps(dec), Truth.IMPOSSIBLE)
        self.assertIs(Interval.closed(CivilDate(2026, 11, 1), CivilDate(2026, 12, 1)).overlaps(dec), Truth.DEFINITELY)
        unknown = Interval.of(Bound.included(CivilDate(2026, 11, 1)), Bound.UNKNOWN)
        self.assertIs(unknown.contains(CivilDate(2027, 6, 1)), Truth.POSSIBLY)
        self.assertEqual((nov.start, nov.end), (Bound.included(CivilDate(2026, 11, 1)), Bound.excluded(CivilDate(2026, 12, 1))))
        hours = Interval.half_open(Instant.from_unix_seconds(0), Instant.from_unix_seconds(3600))
        self.assertIs(hours.contains(Instant.from_unix_seconds(3599)), Truth.DEFINITELY)
        self.assertEqual(str(Interval.of(Bound.UNBOUNDED, Bound.excluded(CivilDate(2026, 12, 1)))), "(.., 2026-12-01)")
        self.assertEqual(repr(Bound.UNKNOWN), "Bound.UNKNOWN")

    def test_construction_is_checked(self):
        with self.assertRaises(ValueError):
            Interval.half_open(CivilDate(2026, 12, 1), CivilDate(2026, 11, 1))
        self.assertEqual(codes(Interval.try_of(Bound.included(CivilDate(2026, 12, 1)), Bound.excluded(CivilDate(2026, 11, 1)))),
                         ["reversed-interval"])
        with self.assertRaises(ValueError):
            Interval.half_open(CivilDate(2026, 12, 1), Instant.from_unix_seconds(0))  # type: ignore[type-var]
        with self.assertRaises(ValueError) as caught:
            Interval.of(Bound.UNBOUNDED, Bound.UNKNOWN)
        self.assertEqual([d.code for d in caught.exception.diagnostics], ["unsupported-form"])  # type: ignore[attr-defined]
        with self.assertRaises(TypeError):
            Bound.included("2026-11-01")  # type: ignore[type-var]


class Outcomes(unittest.TestCase):
    def test_helpers(self):
        jan30, none = CivilDate(2026, 1, 30).add_months(1), CivilDate(2026, 1, 31).add_months(1)
        self.assertEqual(none.unique_or(CivilDate(1, 1, 1)), CivilDate(1, 1, 1))
        self.assertEqual(Unique(5).unique_or(0), 5)
        with self.assertRaises(OutcomeError) as caught:
            none.single()
        self.assertIsInstance(caught.exception, ValueError)
        self.assertEqual((caught.exception.status, [d.code for d in caught.exception.diagnostics]), ("none", ["month-end"]))
        self.assertEqual(str(none), "none (month-end)")
        self.assertEqual(none.codes, ("month-end",))
        self.assertEqual(str(CivilDateTime.of(2026, 11, 1, 1, 30).in_zone(NY).resolve(CTX)), "multiple (ambiguous)")
        self.assertIsInstance(jan30, NoneOf)  # 30 Jan + 1 month: no 30 February either

    def test_match(self):
        match CivilDate(2026, 1, 31).add_months(1):
            case Unique(value=d):
                self.fail(f"unexpected {d}")
            case NoneOf(diagnostics=why):
                self.assertEqual(why[0].code, "month-end")
            case _:
                self.fail("unexpected case")

    def test_to_result_is_the_json_layer_envelope(self):
        res = CivilDateTime.of(2026, 7, 4, 12, 0).in_zone(NY).resolve(CTX).to_result()
        self.assertEqual(res.operation, "resolve")
        self.assertEqual(res.manifest["dependencies"][0]["id"], "demo-zones")
        self.assertEqual(res.value, Instant.from_unix_seconds(1783180800).to_json())
        self.assertEqual(Unique(Truth.POSSIBLY).to_result().candidates, ["possibly"])

    def test_context_compare(self):
        paris = CivilDateTime.of(2026, 11, 5, 9, 0).in_zone("Europe/Paris")
        new_york = CivilDateTime.of(2026, 11, 5, 9, 0).in_zone(NY)
        self.assertEqual(CTX.compare(paris, new_york), Unique(Ordering.BEFORE))
        self.assertEqual(CTX.compare(new_york, paris).single(), Ordering.AFTER)
        overlap = CivilDateTime.of(2026, 11, 1, 1, 30).in_zone(NY)
        between = Instant.from_unix_seconds(1793511000 + 1800)  # 06:00Z: after the first 01:30, before the second
        order = CTX.compare(overlap, between)
        self.assertIsInstance(order, Multiple)
        self.assertEqual(order.values, (Ordering.BEFORE, Ordering.AFTER))
        self.assertEqual(CTX.compare(overlap, overlap).values, (Ordering.BEFORE, Ordering.EQUAL, Ordering.AFTER))
        fixed = CivilDateTime.of(2026, 11, 5, 9, 0).at_offset(3600)
        self.assertEqual(CTX.compare(paris, fixed).single(), Ordering.EQUAL)
        gap = CivilDateTime.of(2026, 3, 8, 2, 30).in_zone(NY)
        self.assertEqual(codes(CTX.compare(gap, paris)), ["gap"])

    def test_policies_accept_strings(self):
        self.assertEqual(ResolutionPolicy("shift-forward-by-gap", "earliest"), ResolutionPolicy(Gap.SHIFT_FORWARD, Overlap.EARLIEST))
        self.assertEqual(ResolutionPolicy.STRICT, ResolutionPolicy())
        with self.assertRaises(ValueError):
            ResolutionPolicy(gap="guess")  # type: ignore[arg-type]
        with self.assertRaises(ValueError):
            CivilDate(2026, 1, 31).add_months(1, "clamp")  # type: ignore[call-overload]

    def test_policy_overloads_take_only_constrain(self):
        """The rejecting form is the call without a policy (library-api §2, "Policy overloads")."""
        dt_ = CivilDateTime.of(2026, 1, 31, 9, 0)
        for call in (lambda: CivilDate(2026, 1, 31).add_months(1, MonthEnd.REJECT),  # type: ignore[call-overload]
                     lambda: CivilDate(2024, 2, 29).add_years(1, "reject"),  # type: ignore[call-overload]
                     lambda: dt_.add_months(1, MonthEnd.REJECT),  # type: ignore[call-overload]
                     lambda: dt_.in_zone(NY).add_months(1, "reject")):  # type: ignore[call-overload]
            with self.assertRaises(ValueError):
                call()
        self.assertEqual(dt_.in_zone(NY).add_months(1, "constrain").date_time, CivilDateTime.of(2026, 2, 28, 9, 0))
        self.assertEqual(codes(CivilDate(2026, 1, 31).add(Period(months=1), MonthEnd.REJECT)), ["month-end"])

    def test_context_needs_rules(self):
        with self.assertRaises(TypeError):
            TemporalContext("conformance/rules/demo-zones.json")  # type: ignore[arg-type]
        self.assertEqual(TemporalContext.with_rules(CTX.rules), CTX)
        text = (ROOT / "conformance" / "rules" / "demo-zones.json").read_text(encoding="utf-8")
        self.assertEqual(ZoneRules.parse(text), CTX.rules)


SAMPLES = [
    CivilDate(2026, 11, 5), CivilDate(275760, 9, 13), CivilDate(-500, 3, 1), CivilYearMonth(2026, 11),
    CivilTime(9, 30), CivilTime(9, 30, 15, 250_000_000), CivilDateTime.of(2026, 11, 5, 14, 30),
    CivilDateTime.of(2026, 11, 5, 14, 30).at_offset(-19800), CivilDateTime.of(2027, 6, 1, 9, 0).in_zone("Europe/Paris"),
    ZonedIntent(CivilDateTime.of(2027, 6, 1, 9, 0), "Europe/Paris", 7200), Instant.from_unix_seconds(0, 500_000_000),
    Period(months=1), Period(years=1, months=2, days=10, hours=2, minutes=30), Period(days=-1, hours=-2), Period(),
    Interval.half_open(CivilDate(2026, 11, 1), CivilDate(2026, 12, 1)),
    Interval.of(Bound.included(CivilDate(2026, 11, 1)), Bound.UNKNOWN),
    Interval.of(Bound.UNBOUNDED, Bound.included(Instant.from_unix_seconds(0))),
]


class RoundTrips(unittest.TestCase):
    def test_text_round_trips_through_parse(self):
        for v in SAMPLES:
            with self.subTest(v=repr(v)):
                self.assertEqual(utv.parse(str(v)), Unique(v))
                self.assertEqual(type(v).parse(str(v)), Unique(v))

    def test_json_round_trips(self):
        for v in SAMPLES + [PosixSeconds.of(14403, 4), ElapsedSeconds.of(86400), Period(months=1, days=-1),
                            Instant(Fraction(1, 3))]:
            with self.subTest(v=repr(v)):
                self.assertEqual(type(v).from_json(v.to_json()), Unique(v))
                self.assertEqual(utv.from_json(json.dumps(v.to_json())), Unique(v))
                self.assertEqual(utv.validate_structural(json.dumps(v.to_json())).status, utv.Status.VALID)

    def test_values_are_hashable_and_immutable(self):
        self.assertEqual(len(set(SAMPLES + SAMPLES)), len(SAMPLES))
        with self.assertRaises(AttributeError):
            CivilDate(2026, 11, 5).day = 6  # type: ignore[misc]

    def test_repr(self):
        self.assertEqual(repr(CivilDate(2026, 11, 5)), "CivilDate(2026-11-05)")
        self.assertEqual(repr(Instant.from_unix_seconds(1793885400)), "Instant(2026-11-05T13:30:00Z)")
        self.assertEqual(repr(Unique(CivilDate(2026, 11, 5))), "Unique(value=CivilDate(2026-11-05), diagnostics=())")

    def test_wrong_kind_or_form(self):
        date_json = CivilDate(2026, 11, 5).to_json()
        self.assertEqual(codes(Instant.from_json(date_json)), ["kind-mismatch"])
        self.assertEqual(codes(CivilDate.parse("2026-11")), ["kind-mismatch"])
        self.assertEqual(codes(CivilDate.parse("next Tuesday")), ["syntax"])
        self.assertEqual(codes(CivilDate.from_json(utv.date(2026, 2, 30))), ["day-out-of-range"])
        malformed = utv.from_json({"schema": "utv/1.0"})
        self.assertIsInstance(malformed, Invalid)
        self.assertEqual(codes(malformed)[0], "malformed-value")  # 0.1 structural codes follow it
        self.assertEqual(codes(utv.from_json("{not json"))[0], "malformed-value")

    def test_json_the_typed_layer_cannot_hold_exactly(self):
        zoned_date = utv.in_zone(utv.date(2026, 11, 5), "Europe/Paris")
        with_policy = utv.parse_iso("2027-06-01T09:00[Europe/Paris]").value
        with_policy["value"]["resolutionPolicy"] = {"gap": "reject", "overlap": "earliest"}
        floating_with_policy = utv.date_time(2026, 11, 5, 9, 0)
        floating_with_policy["value"]["resolutionPolicy"] = {"gap": "reject", "overlap": "earliest"}
        for value in (zoned_date, with_policy, floating_with_policy, utv.date(2026, 11, 5) | {"provenance": {}}):
            with self.subTest(value=value):
                self.assertEqual(codes(utv.from_json(value)), ["unsupported-operand"])
        floating = utv.date_time(2026, 11, 5, 9, 0)
        floating["value"]["zone"] = {"type": "floating"}  # means the same as no zone
        self.assertEqual(utv.from_json(floating), Unique(CivilDateTime.of(2026, 11, 5, 9, 0)))


class Constructors(unittest.TestCase):
    def test_reject_impossible_values(self):
        cases = [
            (lambda: CivilDate.of(2026, 2, 30), "day-out-of-range"),
            (lambda: CivilDate(2026, 13, 1), "month-out-of-range"),
            (lambda: CivilYearMonth(2026, 0), "month-out-of-range"),
            (lambda: CivilTime.of(25, 0), "field-out-of-range"),
            (lambda: CivilTime.of(9, 60), "field-out-of-range"),
            (lambda: CivilTime.of(23, 59, 60), "leap-second"),
            (lambda: CivilTime.of(24, 0), "end-of-day"),
            (lambda: CivilDateTime.of(2026, 11, 5, 9, 0).at_offset(19 * 3600), "offset-out-of-range"),
            (lambda: CivilDateTime.of(2026, 11, 5, 9, 0).in_zone("not a zone"), "syntax"),
        ]
        for make, code in cases:
            with self.subTest(code=code):
                with self.assertRaises(ValueError) as caught:
                    make()
                self.assertEqual([d.code for d in caught.exception.diagnostics], [code])  # type: ignore[attr-defined]

    def test_try_of(self):
        self.assertEqual(CivilDate.try_of(2026, 11, 5), Unique(CivilDate(2026, 11, 5)))
        self.assertEqual(codes(CivilDate.try_of(2026, 2, 29)), ["day-out-of-range"])
        self.assertIsInstance(CivilTime.try_of(23, 59, 60), Unsupported)
        self.assertIsInstance(CivilDateTime.try_of(2026, 2, 30, 9, 0), Invalid)
        self.assertIsInstance(CivilYearMonth.try_of(2026, 13), Invalid)

    def test_wrong_python_types(self):
        for make in (lambda: CivilDate("2026", 11, 5), lambda: CivilTime(9.5, 0), lambda: Period(days=1.5),
                     lambda: Instant(1.5), lambda: CivilDateTime(CivilDate(2026, 11, 5), "09:30")):
            with self.subTest():
                with self.assertRaises(TypeError):
                    make()


class NativeInterop(unittest.TestCase):
    def test_from_and_to_native(self):
        aware = dt.datetime(2026, 11, 5, 14, 30, tzinfo=dt.timezone(dt.timedelta(hours=1)))
        self.assertEqual(utv.from_native(aware, Intent.INSTANT).single(), Instant.from_unix_seconds(1793885400))
        fixed = utv.from_native(aware, Intent.OFFSET).single()
        self.assertIsInstance(fixed, FixedOffsetDateTime)
        self.assertEqual(utv.to_native(fixed).single(), aware)
        self.assertEqual(utv.from_native(dt.date(2026, 11, 5)), Unique(CivilDate(2026, 11, 5)))
        self.assertEqual(utv.to_native(CivilTime(9, 30)).single(), dt.time(9, 30))
        self.assertEqual(utv.from_native(dt.timedelta(days=1), Intent.PERIOD).single(), Period(days=1))
        self.assertEqual(utv.to_native(PosixSeconds.of(86400)).single(), dt.timedelta(days=1))
        self.assertEqual(codes(utv.from_native(dt.datetime(2026, 11, 5), Intent.INSTANT)), ["local-zone"])
        self.assertEqual(codes(utv.from_native(aware)), ["intent-required"])
        self.assertEqual(codes(utv.to_native(CivilDate(10000, 1, 1))), ["out-of-native-range"])
        lossy = utv.to_native(CivilTime(9, 30, 0, 1), allow_loss=True)
        self.assertEqual((lossy.single(), codes(lossy)), (dt.time(9, 30), ["precision-loss"]))
        self.assertEqual(codes(utv.to_native(Period(days=1))), ["unsupported-operand"])
        with self.assertRaises(TypeError):
            utv.from_native("2026-11-05")  # type: ignore[arg-type]


if __name__ == "__main__":
    unittest.main()
