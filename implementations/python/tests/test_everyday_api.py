"""Builders, the result type and purity of the everyday API (the shared fixtures are in test_everyday_fixtures)."""

import io
import re
import tokenize
import unittest
from fractions import Fraction
from pathlib import Path

import utv
from utv import UNBOUNDED, Status

SRC = Path(__file__).resolve().parents[1] / "src" / "utv"


class Builders(unittest.TestCase):
    def test_time_keeps_only_given_fields(self):
        self.assertEqual(utv.time(9, 30)["value"]["fields"], {"hour": 9, "minute": 30})
        self.assertEqual(utv.time(9, 30, 0, 0)["value"]["fields"], {"hour": 9, "minute": 30, "second": 0})

    def test_instant_sub_tick_is_reduced(self):
        self.assertEqual(utv.instant(0, 500_000_000)["value"]["subTick"], {"numerator": "1", "denominator": "2"})
        self.assertNotIn("subTick", utv.instant(5)["value"])
        self.assertEqual(utv.instant(-1, 0), utv.parse_iso("1969-12-31T23:59:59Z").value)

    def test_period_and_posix_seconds(self):
        self.assertEqual(utv.period(months=1, days=-2)["value"]["components"], {"months": "1", "days": "-2"})
        self.assertEqual(utv.posix_seconds(Fraction(-3, 2))["value"]["seconds"], {"numerator": "-3", "denominator": "2"})

    def test_zone_builders(self):
        dt = utv.date_time(2027, 6, 1, 9, 0)
        self.assertEqual(utv.format_iso(utv.in_zone(dt, "Europe/Paris")).value, "2027-06-01T09:00[Europe/Paris]")
        self.assertEqual(utv.format_iso(utv.with_offset(dt, 0)).value, "2027-06-01T09:00+00:00")
        self.assertNotIn("zone", dt["value"])

    def test_interval_domains(self):
        self.assertEqual(utv.interval(utv.date(2026, 11, 1), UNBOUNDED)["value"]["domain"], "civil/gregory")
        self.assertEqual(utv.interval(UNBOUNDED, utv.instant(0))["value"]["domain"], "physical")
        self.assertEqual(utv.interval(utv.date(2026, 11, 1), utv.date(2026, 11, 30), True, True)["value"]["end"]["state"], "included")
        with self.assertRaises(ValueError):
            utv.interval(UNBOUNDED, UNBOUNDED)

    def test_builders_reject_wrong_types(self):
        with self.assertRaises(TypeError):
            utv.date("2026", 11, 5)
        with self.assertRaises(TypeError):
            utv.time(9.5, 0)

    def test_round_trip_through_text(self):
        for text in ["2026-11-05", "2026-11", "09:30:15.25", "-0500-03-01", "+275760-09-13",
                     "2026-11-05T14:30:00.000000001-05:30", "2027-06-01T09:00+02:00[Europe/Paris]", "1970-01-01T00:00:00.5Z"]:
            with self.subTest(text=text):
                self.assertEqual(utv.format_iso(utv.parse_iso(text).value).value, text)


class ResultType(unittest.TestCase):
    def test_candidates_and_value(self):
        r = utv.parse_iso("2026-11-05")
        self.assertEqual(r.value, utv.date(2026, 11, 5))
        self.assertEqual(r.to_dict()["candidates"], [utv.date(2026, 11, 5)])
        self.assertEqual(r.to_dict()["operation"], "parseIso")
        self.assertIsNone(utv.parse_iso("2026-02-30").value)
        self.assertNotIn("candidates", utv.parse_iso("2026-02-30").to_dict())

    def test_malformed_input_is_invalid_not_an_exception(self):
        for bad in [None, {}, {"schema": "utv/1.0", "kind": "civil"}, utv.date(2026, 11, 5) | {"extra": 1}]:
            with self.subTest(bad=bad):
                self.assertEqual(utv.fields(bad).status, Status.INVALID)
        self.assertEqual(utv.parse_iso(42).status, Status.INVALID)

    def test_resolve_records_rules_in_manifest(self):
        rules = utv.load_rules(Path(__file__).resolve().parents[3] / "conformance" / "rules" / "demo-zones.json")
        r = utv.resolve(utv.in_zone(utv.date_time(2026, 7, 4, 12, 0), "America/New_York"), rules)
        self.assertEqual(r.manifest["dependencies"], [{"system": "utv.demo", "id": "demo-zones", "version": "2026-11-05"}])
        self.assertEqual(r.loss["classification"], "information-losing")

    def test_add_drops_a_stale_offset_assertion(self):
        zoned = utv.parse_iso("2026-03-28T09:00+01:00[Europe/Paris]").value
        r = utv.add(zoned, utv.period(days=1))
        self.assertEqual(utv.format_iso(r.value).value, "2026-03-29T09:00[Europe/Paris]")
        self.assertEqual(r.diagnostics[0].code, "offset-assertion-dropped")

    def test_dates_must_share_a_zone(self):
        paris = utv.in_zone(utv.date(2026, 11, 1), "Europe/Paris")
        november = utv.interval(utv.date(2026, 11, 1), utv.date(2026, 12, 1))
        self.assertEqual(utv.contains(november, paris).status, Status.UNSUPPORTED)
        self.assertEqual(utv.overlaps(november, utv.interval(paris, UNBOUNDED)).status, Status.UNSUPPORTED)
        in_paris = utv.interval(paris, UNBOUNDED)
        self.assertEqual(utv.contains(in_paris, utv.in_zone(utv.date(2026, 11, 5), "Europe/Paris")).value, "definitely")
        self.assertEqual(utv.days_between(paris, utv.in_zone(utv.date(2026, 11, 5), "Europe/Paris")).value, 4)

    def test_offset_assertion_in_a_gap_is_invalid(self):
        rules = utv.load_rules(Path(__file__).resolve().parents[3] / "conformance" / "rules" / "demo-zones.json")
        asserted = utv.parse_iso("2026-03-08T02:30-05:00[America/New_York]").value
        r = utv.resolve(asserted, rules, {"gap": "shift-forward-by-gap", "overlap": "reject"})
        self.assertEqual((r.status, r.diagnostics[0].code), (Status.INVALID, "offset-mismatch"))


class Purity(unittest.TestCase):
    """The library never reads the clock, the host time zone or the locale."""

    # Code tokens are joined with spaces, so patterns allow whitespace between tokens.
    FORBIDDEN = [r"\.\s*now\s*\(", r"\.\s*today\s*\(", r"\btime\s*\.\s*time\s*\(", r"\btime_ns\b",
                 r"\.\s*astimezone\s*\(\s*\)", r"\blocaltime\b", r"\bimport\s+locale\b", r"\benviron\b",
                 r"\bfromtimestamp\b", r"\butcnow\b", r"\bZoneInfo\s*\("]

    @staticmethod
    def code_only(path):
        """The source with strings and comments removed (messages may mention ZoneInfo(...))."""
        skip = {tokenize.STRING, tokenize.COMMENT} | {getattr(tokenize, "FSTRING_MIDDLE", tokenize.STRING)}
        tokens = tokenize.generate_tokens(io.StringIO(path.read_text(encoding="utf-8")).readline)
        return " ".join(t.string for t in tokens if t.type not in skip)

    def test_no_hidden_environment(self):
        for path in SRC.glob("*.py"):
            text = self.code_only(path)
            for pattern in self.FORBIDDEN:
                with self.subTest(file=path.name, pattern=pattern):
                    self.assertIsNone(re.search(pattern, text))


if __name__ == "__main__":
    unittest.main()
