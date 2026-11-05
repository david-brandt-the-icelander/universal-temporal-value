"""Every fixture in the shared conformance/operations/everyday.json (read from the repository, not copied):
status, candidates and the diagnostic codes listed in ``expect.diagnostics``."""

import json
import unittest
from pathlib import Path

import utv
from utv import Status, validate_structural

ROOT = Path(__file__).resolve().parents[3]
FIXTURES = ROOT / "conformance" / "operations" / "everyday.json"

OPERATIONS = {
    "parseIso": utv.parse_iso,
    "formatIso": utv.format_iso,
    "fields": utv.fields,
    "add": utv.add,
    "compare": utv.compare,
    "daysBetween": utv.days_between,
    "posixSecondsBetween": utv.posix_seconds_between,
    "contains": utv.contains,
    "overlaps": utv.overlaps,
    "resolve": utv.resolve,
    "toZone": utv.to_zone,
    "toOffset": utv.to_offset,
}


def load():
    with open(FIXTURES, encoding="utf-8") as fh:
        doc = json.load(fh)
    rules = {name: utv.load_rules(ROOT / path) for name, path in doc["rules"].items()}
    return doc["fixtures"], rules


def canonical(x):
    """JSON text with sorted keys: compares as JSON (1 != true, "1" != 1), ignoring key order."""
    return json.dumps(x, sort_keys=True, separators=(",", ":"))


def run(fixture, rules):
    args = [rules.get(a, a) if isinstance(a, str) and fixture["op"] in ("resolve", "toZone") else a for a in fixture["args"]]
    return OPERATIONS[fixture["op"]](*args)


class EverydayFixtures(unittest.TestCase):
    def test_fixture_count(self):
        fixtures, _ = load()
        self.assertTrue(fixtures)
        self.assertEqual(len({f["id"] for f in fixtures}), len(fixtures), "fixture ids must be unique")
        self.assertTrue(all(f["op"] in OPERATIONS for f in fixtures))

    def test_every_fixture(self):
        fixtures, rules = load()
        for f in fixtures:
            with self.subTest(id=f["id"]):
                res = run(f, rules)
                expect = f["expect"]
                why = f"{f['id']}: {[d.to_dict() for d in res.diagnostics]}"
                self.assertEqual(res.status.value, expect["status"], why)
                if expect["status"] == "unique":
                    want = [expect["result"]]
                elif expect["status"] == "multiple":
                    want = expect["results"]
                else:
                    want = []
                self.assertEqual(canonical(res.candidates), canonical(want), f["id"])
                codes = {d.code for d in res.diagnostics}
                for code in expect.get("diagnostics", []):
                    self.assertIn(code, codes, why)

    def test_candidates_are_valid_utv(self):
        """Every UTV value an operation returns passes structural validation."""
        fixtures, rules = load()
        for f in fixtures:
            res = run(f, rules)
            for c in res.candidates:
                if isinstance(c, dict) and "schema" in c:
                    with self.subTest(id=f["id"]):
                        self.assertEqual(validate_structural(json.dumps(c)).status, Status.VALID)


if __name__ == "__main__":
    unittest.main()
