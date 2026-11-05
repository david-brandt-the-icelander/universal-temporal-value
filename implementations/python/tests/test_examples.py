"""The runnable examples: every outcome in the shared catalog examples/traditional.json, via
examples/traditional.py, and the side-by-side tour examples/everyday.py."""

import contextlib
import importlib.util
import io
import unittest
from pathlib import Path
from unittest import mock
from zoneinfo import ZoneInfoNotFoundError

_EXAMPLES = Path(__file__).resolve().parents[1] / "examples"


def _load(name):
    spec = importlib.util.spec_from_file_location(f"{name}_example", _EXAMPLES / f"{name}.py")
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


traditional = _load("traditional")
everyday = _load("everyday")


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


class EverydayExample(unittest.TestCase):
    """The example checks its own UTV outcomes; native output isn't asserted (it varies by host)."""

    def run_main(self):
        out = io.StringIO()
        with contextlib.redirect_stdout(out):
            code = everyday.main()
        return code, out.getvalue()

    def test_main_succeeds(self):
        code, text = self.run_main()
        self.assertEqual(code, 0, text)
        self.assertTrue(text.rstrip().endswith("Every UTV outcome as expected."))
        for n in range(1, 15):
            self.assertIn(f"== {n}. ", text)

    def test_main_succeeds_without_a_tz_database(self):
        def missing(key):
            raise ZoneInfoNotFoundError(key)

        with mock.patch.object(everyday, "ZoneInfo", missing):
            code, text = self.run_main()
        self.assertEqual(code, 0, text)
        self.assertIn("not available on this host; native side skipped", text)


if __name__ == "__main__":
    unittest.main()
