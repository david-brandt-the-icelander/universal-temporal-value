import json
import unittest

from utv import Status, is_leap_year, validate_gregorian_date


def civil(fields, form="date", cal=("gregory", "proleptic")):
    return json.dumps({"schema": "utv/1.0", "kind": "civil", "value": {
        "form": form, "fields": fields, "calendar": {"id": cal[0], "chronology": cal[1]}}})


class Gregorian(unittest.TestCase):
    def status(self, text):
        return validate_gregorian_date(text).status

    def test_leap_rule(self):
        self.assertTrue(is_leap_year(2000))
        self.assertTrue(is_leap_year(2024))
        self.assertTrue(is_leap_year(0))
        self.assertTrue(is_leap_year(-4))
        self.assertFalse(is_leap_year(1900))
        self.assertFalse(is_leap_year(2026))

    def test_dates(self):
        self.assertEqual(self.status(civil({"year": "2026", "month": 11, "day": 4})), Status.VALID)
        self.assertEqual(self.status(civil({"year": "2024", "month": 2, "day": 29})), Status.VALID)
        self.assertEqual(self.status(civil({"year": "2000", "month": 2, "day": 29})), Status.VALID)
        self.assertEqual(self.status(civil({"year": "2026", "month": 2, "day": 30})), Status.INVALID)
        self.assertEqual(self.status(civil({"year": "2026", "month": 2, "day": 29})), Status.INVALID)
        self.assertEqual(self.status(civil({"year": "1900", "month": 2, "day": 29})), Status.INVALID)
        self.assertEqual(self.status(civil({"year": "2026", "month": 4, "day": 31})), Status.INVALID)
        self.assertEqual(self.status(civil({"year": "2026", "month": 13, "day": 1})), Status.INVALID)
        self.assertEqual(self.status(civil({"year": "2026", "month": 1, "day": 0})), Status.INVALID)
        self.assertEqual(self.status(civil({"month": 2, "day": 29}, form="month-day")), Status.VALID)
        self.assertEqual(self.status(civil({"month": 2, "day": 30}, form="month-day")), Status.INVALID)
        self.assertEqual(self.status(civil({"year": "123456789012345678800", "month": 2, "day": 29})), Status.VALID)
        self.assertEqual(self.status(civil({"year": "123456789012345678900", "month": 2, "day": 29})), Status.INVALID)

    def test_unsupported(self):
        self.assertEqual(self.status(civil({"year": "2026", "month": 2, "day": 30}, cal=("julian", "proleptic"))),
                         Status.UNSUPPORTED)
        inst = json.dumps({"schema": "utv/1.0", "kind": "instant", "value": {
            "system": {"system": "utv.timescale", "id": "unix-posix", "version": "1"}, "ticks": "0"}})
        self.assertEqual(self.status(inst), Status.UNSUPPORTED)

    def test_structural_error_first(self):
        self.assertEqual(self.status('{"schema":"utv/1.0"}'), Status.INVALID)


if __name__ == "__main__":
    unittest.main()
