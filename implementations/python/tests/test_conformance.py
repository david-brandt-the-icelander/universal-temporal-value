import json
import unittest
from pathlib import Path

from utv import Status, validate_structural

ROOT = Path(__file__).resolve().parents[3]
EXAMPLES = ROOT / "conformance" / "examples"


def _load(name):
    with open(EXAMPLES / name, encoding="utf-8") as fh:
        return json.load(fh)


class ConformanceExamples(unittest.TestCase):
    def test_valid_examples(self):
        for name, value in _load("valid.json").items():
            with self.subTest(name=name):
                r = validate_structural(json.dumps(value))
                self.assertEqual(r.status, Status.VALID, [d.to_dict() for d in r.diagnostics])

    def test_invalid_structural_examples(self):
        for name, value in _load("invalid-structural.json").items():
            with self.subTest(name=name):
                r = validate_structural(json.dumps(value))
                self.assertEqual(r.status, Status.INVALID, name)
                self.assertNotEqual(r.status, Status.VALID)


POSIX = {"schema": "utv/1.0", "kind": "instant",
         "value": {"system": {"system": "utv.timescale", "id": "unix-posix", "version": "1"}, "ticks": "0"}}


class BundledSchema(unittest.TestCase):
    def test_bundled_schema_matches_repository(self):
        bundled = Path(__file__).resolve().parents[1] / "src" / "utv" / "data" / "utv-1.0.schema.json"
        self.assertEqual(bundled.read_bytes(), (ROOT / "schema" / "utv-1.0.schema.json").read_bytes())


class Envelope(unittest.TestCase):
    def check(self, value, status):
        text = value if isinstance(value, str) else json.dumps(value)
        r = validate_structural(text)
        self.assertEqual(r.status, status, [d.to_dict() for d in r.diagnostics])
        return r

    def test_baseline_valid(self):
        r = self.check(POSIX, Status.VALID)
        self.assertEqual(r.to_dict()["schema"], "utv-result/1.0")

    def test_duplicate_key(self):
        r = self.check('{"schema":"utv/1.0","schema":"utv/1.0","kind":"instant","value":{}}', Status.INVALID)
        self.assertEqual(r.diagnostics[0].code, "duplicate-key")

    def test_newer_minor_unsupported(self):
        self.check(dict(POSIX, schema="utv/1.1"), Status.UNSUPPORTED)
        self.check(dict(POSIX, schema="utv/1.12"), Status.UNSUPPORTED)

    def test_other_well_formed_versions_unsupported(self):
        for s in ["utv/2.0", "utv/0.9", "utv/10.3"]:
            with self.subTest(s=s):
                self.check(dict(POSIX, schema=s), Status.UNSUPPORTED)

    def test_other_schema_invalid(self):
        for s in ["utv/4", "utv/1", "utv/1.01", "utv/01.0", "utv/1.0 ", "UTV/1.0", "utv/1.0\n", 1, None]:
            with self.subTest(s=s):
                self.check(dict(POSIX, schema=s), Status.INVALID)
        self.check({"kind": "instant", "value": {}}, Status.INVALID)

    def test_unknown_top_level_property(self):
        r = self.check(dict(POSIX, extra=1), Status.INVALID)
        self.assertTrue(any(d.code == "unknown-property" for d in r.diagnostics))

    def test_unknown_kind(self):
        r = self.check(dict(POSIX, kind="moment"), Status.INVALID)
        self.assertEqual(r.diagnostics[0].code, "unknown-kind")

    def test_malformed_json(self):
        self.check("{", Status.INVALID)
        self.check("[]", Status.INVALID)


if __name__ == "__main__":
    unittest.main()
