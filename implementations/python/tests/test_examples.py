"""Every outcome in the shared catalog examples/traditional.json, via examples/traditional.py."""

import contextlib
import importlib.util
import io
import unittest
from pathlib import Path

_PATH = Path(__file__).resolve().parents[1] / "examples" / "traditional.py"
_spec = importlib.util.spec_from_file_location("traditional_example", _PATH)
traditional = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(traditional)


class TraditionalCatalog(unittest.TestCase):
    def test_every_expectation(self):
        entries = traditional.load_catalog()
        self.assertTrue(entries)
        for entry in entries:
            with self.subTest(id=entry["id"]):
                _, mismatches = traditional.check_entry(entry)
                self.assertEqual(mismatches, [])

    def test_runner_reports_a_mismatch(self):
        entry = dict(traditional.load_catalog()[0], expect={"structural": "invalid"})
        lines = []
        self.assertEqual(traditional.tour([entry], out=lines.append), 1)
        self.assertIn("  MISMATCH: structural expected invalid, got valid", lines)

    def test_main_succeeds(self):
        out = io.StringIO()
        with contextlib.redirect_stdout(out):
            code = traditional.main()
        self.assertEqual(code, 0)
        self.assertTrue(out.getvalue().rstrip().endswith("examples, every outcome as expected."))


if __name__ == "__main__":
    unittest.main()
