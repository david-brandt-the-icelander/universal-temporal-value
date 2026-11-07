"""How to use ``utv.gregorian.validate_gregorian_date``, the semantic check of civil values in the proleptic
Gregorian calendar at the JSON layer, compared with Python, where a ``datetime.date`` that exists is always valid and
there is no other calendar at all."""

import unittest

from utv import Status
from utv.gregorian import validate_gregorian_date


class TestValidateGregorianDate(unittest.TestCase):
    def test_returns_valid_when_the_date_exists(self):
        """Verifies that validate_gregorian_date returns valid when the JSON names a real Gregorian date.

        Compared with: ``date(2026, 11, 5)`` exists too.
        """
        # GIVEN: 5 November 2026 as UTV JSON: fields in a named calendar, with no time and no zone
        given_json = """
            { "schema": "utv/1.0", "kind": "civil",
              "value": { "form": "date", "fields": { "year": "2026", "month": 11, "day": 5 },
                         "calendar": { "id": "gregory", "chronology": "proleptic" } } }
            """

        # WHEN
        actual_result = validate_gregorian_date(given_json)

        # EXPECTATIONS
        expected_status = Status.VALID

        # THEN
        self.assertEqual(expected_status, actual_result.status)

    def test_returns_invalid_when_well_formed_json_names_a_missing_day(self):
        """Verifies that validate_gregorian_date returns invalid when well-formed JSON names a day that does not exist.

        Compared with: Python has one check, in the ``date`` constructor; UTV keeps structure and meaning as separate
        checks.
        """
        # GIVEN: 29 February 2026 as UTV JSON: structurally valid, but 2026 is a common year
        given_json = """
            { "schema": "utv/1.0", "kind": "civil",
              "value": { "form": "date", "fields": { "year": "2026", "month": 2, "day": 29 },
                         "calendar": { "id": "gregory", "chronology": "proleptic" } } }
            """

        # WHEN
        actual_result = validate_gregorian_date(given_json)

        # EXPECTATIONS: invalid, with the reason, instead of rolling over to 1 March
        expected_status = Status.INVALID
        expected_code = "day-out-of-range"

        # THEN
        self.assertEqual(expected_status, actual_result.status)
        self.assertIn(expected_code, [d.code for d in actual_result.diagnostics])

    def test_returns_unsupported_when_the_calendar_is_not_gregorian(self):
        """Verifies that validate_gregorian_date returns unsupported when the date is in another calendar.

        Compared with: Python's standard library has no Hebrew calendar, so it cannot hold this date at all.
        """
        # GIVEN: 14 Heshvan 5787 as UTV JSON, in the Hebrew calendar
        given_json = """
            { "schema": "utv/1.0", "kind": "civil",
              "value": { "form": "date", "fields": { "year": "5787", "month": 2, "day": 14 },
                         "calendar": { "id": "hebrew", "chronology": "proleptic" } } }
            """

        # WHEN
        actual_result = validate_gregorian_date(given_json)

        # EXPECTATIONS: unsupported, kept as written, rather than guessed or converted
        expected_status = Status.UNSUPPORTED
        expected_code = "unsupported-calendar"

        # THEN
        self.assertEqual(expected_status, actual_result.status)
        self.assertIn(expected_code, [d.code for d in actual_result.diagnostics])

    def test_returns_valid_when_the_value_is_only_a_year(self):
        """Verifies that validate_gregorian_date returns valid when the value is a year with no month or day.

        Compared with: ``date(1347, 1, 1)`` would claim it was 1 January.
        """
        # GIVEN: the year 1347 as UTV JSON
        given_json = """
            { "schema": "utv/1.0", "kind": "civil",
              "value": { "form": "year", "fields": { "year": "1347" },
                         "calendar": { "id": "gregory", "chronology": "proleptic" } } }
            """

        # WHEN
        actual_result = validate_gregorian_date(given_json)

        # EXPECTATIONS
        expected_status = Status.VALID

        # THEN
        self.assertEqual(expected_status, actual_result.status)
