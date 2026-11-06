"""What Python's ``json`` module does with temporal JSON, compared with UTV's ``validate_structural``."""

import json
import unittest


class TestLoads(unittest.TestCase):
    def test_keeps_the_last_value_when_a_key_appears_twice(self):
        """Verifies that json.loads accepts duplicate keys and keeps the last one.

        Compared with: ``utv.validate_structural`` rejects the same text as invalid (duplicate-key): two months in one
        date is a contradiction, not something to settle by taking the last.
        """
        # GIVEN: a UTV date with two different months
        given_json = (
            '{"schema":"utv/1.0","kind":"civil","value":{"form":"date",'
            '"fields":{"year":"2026","month":11,"month":12,"day":5},'
            '"calendar":{"id":"gregory","chronology":"proleptic"}}}'
        )

        # WHEN
        actual_document = json.loads(given_json)

        # EXPECTATIONS: the second month, December
        expected_month = 12

        # THEN
        self.assertEqual(expected_month, actual_document["value"]["fields"]["month"])
