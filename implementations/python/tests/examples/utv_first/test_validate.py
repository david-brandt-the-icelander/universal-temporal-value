"""How to use ``utv.validate.validate_structural``, which checks that JSON is well-formed UTV before anything
interprets it, compared with Python, which has no temporal values beyond ``datetime``'s: the values here are written
as UTV JSON because Python has nothing to write them with."""

import unittest

from utv import Status
from utv.validate import validate_structural


class TestValidateStructural(unittest.TestCase):
    def test_returns_valid_when_the_value_is_only_a_year(self):
        """Verifies that validate_structural accepts a value that is only a year.

        Compared with: ``date(1347, 1, 1)``, the usual stand-in, claims a day nobody knows.
        """
        # GIVEN: the year 1347, with no month or day
        given_json = """
            { "schema": "utv/1.0", "kind": "civil",
              "value": { "form": "year", "fields": { "year": "1347" },
                         "calendar": { "id": "gregory", "chronology": "proleptic" } } }
            """

        # WHEN
        actual_result = validate_structural(given_json)

        # EXPECTATIONS
        expected_status = Status.VALID

        # THEN
        self.assertEqual(expected_status, actual_result.status)

    def test_returns_valid_when_the_second_is_a_leap_second(self):
        """Verifies that validate_structural accepts a leap-second label, leaving its meaning to a later check.

        Compared with: ``datetime(2016, 12, 31, 23, 59, 60)`` raises a ValueError before anything can be stored.
        """
        # GIVEN: 23:59:60 UTC on 31 December 2016, a real leap second
        given_json = """
            { "schema": "utv/1.0", "kind": "civil",
              "value": { "form": "datetime",
                         "fields": { "year": "2016", "month": 12, "day": 31, "hour": 23, "minute": 59, "second": 60 },
                         "calendar": { "id": "gregory", "chronology": "proleptic" },
                         "zone": { "type": "fixed-offset", "seconds": 0 }, "zoneIntent": "fixed-offset" } }
            """

        # WHEN
        actual_result = validate_structural(given_json)

        # EXPECTATIONS: well-formed; confirming the label needs a leap-second table
        expected_status = Status.VALID

        # THEN
        self.assertEqual(expected_status, actual_result.status)

    def test_returns_valid_when_the_interval_is_a_start_plus_an_amount(self):
        """Verifies that validate_structural accepts an interval stored as a start plus an amount.

        Compared with: a Python ``(start, start + timedelta)`` pair stores a computed end, and forgets how it was
        written.
        """
        # GIVEN: a two-hour meeting starting at 12:30 UTC on 4 November 2026
        given_json = """
            { "schema": "utv/1.0", "kind": "interval",
              "value": { "domain": "physical", "construction": "start-amount",
                         "start": { "state": "included",
                                    "point": { "schema": "utv/1.0", "kind": "instant",
                                               "value": { "system": { "system": "utv.timescale", "id": "unix-posix", "version": "1" },
                                                          "ticks": "1793795400" } } },
                         "amount": { "schema": "utv/1.0", "kind": "amount",
                                     "value": { "basis": "elapsed", "seconds": { "numerator": "7200", "denominator": "1" } } } } }
            """

        # WHEN
        actual_result = validate_structural(given_json)

        # EXPECTATIONS
        expected_status = Status.VALID

        # THEN
        self.assertEqual(expected_status, actual_result.status)

    def test_returns_valid_when_the_value_is_a_union_of_ranges(self):
        """Verifies that validate_structural accepts a union of two separate date ranges.

        Compared with: Python has no range type, so two ranges are a list of hand-made pairs.
        """
        # GIVEN: 1–5 November and 20–25 November 2026, as one value
        given_json = """
            { "schema": "utv/1.0", "kind": "set",
              "value": { "operation": "union", "members": [
                { "schema": "utv/1.0", "kind": "interval",
                  "value": { "domain": "civil/gregory",
                             "start": { "state": "included", "point": { "schema": "utv/1.0", "kind": "civil",
                                        "value": { "form": "date", "fields": { "year": "2026", "month": 11, "day": 1 },
                                                   "calendar": { "id": "gregory", "chronology": "proleptic" } } } },
                             "end": { "state": "excluded", "point": { "schema": "utv/1.0", "kind": "civil",
                                      "value": { "form": "date", "fields": { "year": "2026", "month": 11, "day": 6 },
                                                 "calendar": { "id": "gregory", "chronology": "proleptic" } } } } } },
                { "schema": "utv/1.0", "kind": "interval",
                  "value": { "domain": "civil/gregory",
                             "start": { "state": "included", "point": { "schema": "utv/1.0", "kind": "civil",
                                        "value": { "form": "date", "fields": { "year": "2026", "month": 11, "day": 20 },
                                                   "calendar": { "id": "gregory", "chronology": "proleptic" } } } },
                             "end": { "state": "excluded", "point": { "schema": "utv/1.0", "kind": "civil",
                                      "value": { "form": "date", "fields": { "year": "2026", "month": 11, "day": 26 },
                                                 "calendar": { "id": "gregory", "chronology": "proleptic" } } } } } } ] } }
            """

        # WHEN
        actual_result = validate_structural(given_json)

        # EXPECTATIONS
        expected_status = Status.VALID

        # THEN
        self.assertEqual(expected_status, actual_result.status)

    def test_returns_valid_when_the_value_is_a_weekly_recurrence(self):
        """Verifies that validate_structural accepts a weekly recurrence anchored in a named zone.

        Compared with: Python has no recurrence type; a list of computed ``datetime`` values goes stale when zone
        rules change.
        """
        # GIVEN: every Tuesday at 09:00 in Paris, from 3 November 2026, as an RFC 5545 rule
        given_json = """
            { "schema": "utv/1.0", "kind": "recurrence",
              "value": { "intent": { "schema": "utv/1.0", "kind": "civil",
                                     "value": { "form": "datetime",
                                                "fields": { "year": "2026", "month": 11, "day": 3, "hour": 9, "minute": 0 },
                                                "calendar": { "id": "gregory", "chronology": "proleptic" },
                                                "zone": { "type": "iana", "id": "Europe/Paris" }, "zoneIntent": "follow-zone-id" } },
                         "algorithm": { "system": "rfc", "id": "5545", "version": "1" },
                         "rule": "FREQ=WEEKLY;BYDAY=TU" } }
            """

        # WHEN
        actual_result = validate_structural(given_json)

        # EXPECTATIONS
        expected_status = Status.VALID

        # THEN
        self.assertEqual(expected_status, actual_result.status)

    def test_returns_valid_when_the_year_is_uncertain(self):
        """Verifies that validate_structural accepts a year known only to lie within bounds.

        Compared with: Python can only store one date, such as ``date(1347, 1, 1)``, and hope nobody asks.
        """
        # GIVEN: about 1347, somewhere between 1340 and 1355
        given_json = """
            { "schema": "utv/1.0", "kind": "uncertain",
              "value": { "target": { "schema": "utv/1.0", "kind": "civil",
                                     "value": { "form": "year", "fields": { "year": "1347" },
                                                "calendar": { "id": "gregory", "chronology": "proleptic" } } },
                         "model": "bounds",
                         "parameters": { "earliest": { "year": "1340" }, "latest": { "year": "1355" },
                                         "interpretation": "possible range, not 95% confidence" } } }
            """

        # WHEN
        actual_result = validate_structural(given_json)

        # EXPECTATIONS
        expected_status = Status.VALID

        # THEN
        self.assertEqual(expected_status, actual_result.status)

    def test_returns_unsupported_when_the_version_is_newer(self):
        """Verifies that validate_structural returns unsupported when the value is from a newer version.

        Compared with: code reading the JSON with ``json.loads`` would fail on a missing key, or read it as if nothing
        had changed.
        """
        # GIVEN: a well-formed date labelled with a future specification version
        given_json = """
            { "schema": "utv/2.0", "kind": "civil",
              "value": { "form": "date", "fields": { "year": "2026", "month": 11, "day": 5 },
                         "calendar": { "id": "gregory", "chronology": "proleptic" } } }
            """

        # WHEN
        actual_result = validate_structural(given_json)

        # EXPECTATIONS: unsupported, not invalid: the value may be fine, this library just doesn't read it
        expected_status = Status.UNSUPPORTED
        expected_code = "unsupported-version"

        # THEN
        self.assertEqual(expected_status, actual_result.status)
        self.assertIn(expected_code, [d.code for d in actual_result.diagnostics])

    def test_returns_invalid_when_the_version_label_is_malformed(self):
        """Verifies that validate_structural returns invalid when the version label is malformed.

        Compared with: ``json.loads`` has no notion of a version label and accepts any string.
        """
        # GIVEN: a date labelled "utv/4", an unreleased draft label
        given_json = """
            { "schema": "utv/4", "kind": "civil",
              "value": { "form": "date", "fields": { "year": "2026", "month": 11, "day": 5 },
                         "calendar": { "id": "gregory", "chronology": "proleptic" } } }
            """

        # WHEN
        actual_result = validate_structural(given_json)

        # EXPECTATIONS
        expected_status = Status.INVALID
        expected_code = "unknown-schema"

        # THEN
        self.assertEqual(expected_status, actual_result.status)
        self.assertIn(expected_code, [d.code for d in actual_result.diagnostics])

    def test_returns_invalid_when_a_key_appears_twice(self):
        """Verifies that validate_structural returns invalid when a key appears twice.

        Compared with: ``json.loads`` accepts the text and keeps the last month.
        """
        # GIVEN: a date with two different months
        given_json = (
            '{"schema":"utv/1.0","kind":"civil","value":{"form":"date",'
            '"fields":{"year":"2026","month":11,"month":12,"day":5},'
            '"calendar":{"id":"gregory","chronology":"proleptic"}}}'
        )

        # WHEN
        actual_result = validate_structural(given_json)

        # EXPECTATIONS: invalid, rather than whichever month a parser happens to keep
        expected_status = Status.INVALID
        expected_code = "duplicate-key"

        # THEN
        self.assertEqual(expected_status, actual_result.status)
        self.assertIn(expected_code, [d.code for d in actual_result.diagnostics])

    def test_returns_invalid_when_the_kind_is_unknown(self):
        """Verifies that validate_structural returns invalid when the kind is unknown.

        Compared with: ``json.loads`` accepts any kind; there is no schema to fail closed on.
        """
        # GIVEN: a value of a kind the specification does not define
        given_json = """
            { "schema": "utv/1.0", "kind": "moment", "value": {} }
            """

        # WHEN
        actual_result = validate_structural(given_json)

        # EXPECTATIONS: fail closed: an unknown kind is rejected, not ignored
        expected_status = Status.INVALID
        expected_code = "unknown-kind"

        # THEN
        self.assertEqual(expected_status, actual_result.status)
        self.assertIn(expected_code, [d.code for d in actual_result.diagnostics])
