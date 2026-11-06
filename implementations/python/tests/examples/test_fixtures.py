"""Tests of the shared test data in ``fixtures``."""

import unittest

from .fixtures import demo_zone_rules


class TestDemoZoneRules(unittest.TestCase):
    def test_contains_the_three_demo_zones_when_loaded(self):
        """Verifies that fixtures.demo_zone_rules loads the three demo zones when called.

        Compared with: nothing; this tests the fixture the zone examples share.
        """
        # WHEN
        actual_rules = demo_zone_rules()

        # EXPECTATIONS
        expected_zone_ids = ["America/New_York", "Atlantic/Reykjavik", "Europe/Paris"]

        # THEN
        self.assertEqual(expected_zone_ids, sorted(actual_rules.zones))
