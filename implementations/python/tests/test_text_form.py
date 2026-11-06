"""Every fixture in the shared conformance/operations/text-form.json (read from the repository, not copied),
through the JSON-level parse_text / format_text."""

import json
import unittest
from fractions import Fraction
from pathlib import Path

import utv
from utv import Status
from utv.text import decimal_text, seconds_display

ROOT = Path(__file__).resolve().parents[3]
FIXTURES = ROOT / "conformance" / "operations" / "text-form.json"


def load():
    with open(FIXTURES, encoding="utf-8") as fh:
        return json.load(fh)["fixtures"]


def canonical(x):
    return json.dumps(x, sort_keys=True, separators=(",", ":"))


class TextFormFixtures(unittest.TestCase):
    def test_fixture_ids(self):
        fixtures = load()
        self.assertTrue(fixtures)
        self.assertEqual(len({f["id"] for f in fixtures}), len(fixtures), "fixture ids must be unique")
        self.assertTrue(all(f["direction"] in ("both", "parse", "format") for f in fixtures))

    def check(self, res, status, value=None, codes=()):
        why = [d.to_dict() for d in res.diagnostics]
        self.assertEqual(res.status.value, status, why)
        if value is not None:
            self.assertEqual(canonical(res.value), canonical(value))
        found = {d.code for d in res.diagnostics}
        for code in codes:
            self.assertIn(code, found, why)

    def test_every_fixture(self):
        for f in load():
            with self.subTest(id=f["id"]):
                expect = f.get("expect", {})
                if f["direction"] in ("both", "parse"):
                    res = utv.parse_text(f["text"])
                    self.assertEqual(res.operation, "parseText")
                    if f["direction"] == "both":
                        self.check(res, "unique", f["value"])
                    else:
                        self.check(res, expect["status"], expect.get("value"), expect.get("diagnostics", []))
                if f["direction"] in ("both", "format"):
                    res = utv.format_text(f["value"])
                    self.assertEqual(res.operation, "formatText")
                    if f["direction"] == "both":
                        self.check(res, "unique", f["text"])
                    else:
                        self.check(res, expect["status"], expect.get("value"), expect.get("diagnostics", []))

    def test_parsed_values_are_valid_utv(self):
        for f in load():
            if f["direction"] != "format":
                res = utv.parse_text(f["text"])
                if res.status is Status.UNIQUE:
                    with self.subTest(id=f["id"]):
                        self.assertEqual(utv.validate_structural(json.dumps(res.value)).status, Status.VALID)


class TextFormEdges(unittest.TestCase):
    def test_wrong_input_type(self):
        self.assertEqual(utv.parse_text(42).diagnostics[0].code, "malformed-value")

    def test_zone_annotation_slash_is_not_an_edtf_interval(self):
        res = utv.parse_text("2027-06-01T09:00[Europe/Paris]")
        self.assertEqual(res.value["value"]["zone"], {"type": "iana", "id": "Europe/Paris"})

    def test_interval_syntax(self):
        for bad in ["[2026-11-01 2026-12-01)", "2026-11-01/2026-11-05/2026-11-09", "[.., 2026-12-01)"]:
            with self.subTest(text=bad):
                self.assertEqual(utv.parse_text(bad).status, Status.INVALID)
        for no_points in ["(.., ?)", "../..", "/"]:
            with self.subTest(text=no_points):
                res = utv.parse_text(no_points)
                self.assertEqual((res.status, res.diagnostics[0].code), (Status.UNSUPPORTED, "unsupported-form"))

    def test_period_round_trips(self):
        for text in ["P1Y", "PT1S", "P1W", "-P2W3DT4H", "P10Y11M12W13DT14H15M16S"]:
            with self.subTest(text=text):
                self.assertEqual(utv.format_text(utv.parse_text(text).value).value, text)
        self.assertEqual(utv.parse_text("P0Y").value, utv.period())
        self.assertEqual(utv.parse_text("PT").status, Status.INVALID)

    def test_instants_with_long_fractions_have_no_text_form(self):
        third = {"schema": "utv/1.0", "kind": "instant",
                 "value": {"system": {"system": "utv.timescale", "id": "unix-posix", "version": "1"},
                           "ticks": "0", "subTick": {"numerator": "1", "denominator": "3"}}}
        res = utv.format_text(third)
        self.assertEqual((res.status, res.diagnostics[0].code), (Status.UNSUPPORTED, "no-text-form"))

    def test_display_strings(self):
        self.assertEqual(decimal_text(Fraction(14403, 4)), "3600.75")
        self.assertEqual(decimal_text(Fraction(-1, 40)), "-0.025")
        self.assertEqual(decimal_text(Fraction(1, 3)), "1/3")
        self.assertEqual(seconds_display(utv.posix_seconds(Fraction(14403, 4))), "3600.75 s (POSIX)")
        self.assertEqual(seconds_display(utv.elapsed_seconds(86400)), "86400 s (SI)")
        self.assertIsNone(seconds_display(utv.period(days=1)))


if __name__ == "__main__":
    unittest.main()
