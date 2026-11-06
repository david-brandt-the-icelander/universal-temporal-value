"""The runnable examples: every outcome in the shared catalog examples/traditional.json, via
examples/traditional.py, and the snapshot of the side-by-side tour examples/everyday.py."""

import contextlib
import importlib.util
import io
import os
import re
import subprocess
import sys
import tempfile
import unittest
from pathlib import Path

_EXAMPLES = Path(__file__).resolve().parents[1] / "examples"


def _load(name):
    spec = importlib.util.spec_from_file_location(f"{name}_example", _EXAMPLES / f"{name}.py")
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


traditional = _load("traditional")


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


TASKS = [
    "now", "a-date", "weekday-week", "feb-30", "add-days", "month-end", "days-between", "compare-dates",
    "time-of-day", "datetime-plus-hours", "parse-offsets", "meeting-in-paris", "dst-gap", "dst-overlap",
    "timestamps", "day-vs-24h", "ranges", "round-trip",
]
HOST = " ‡"  # rows that depend on the machine (clock, tz database, Python version) end with this


def run_everyday(**env):
    """Run examples/everyday.py as a reader would, and return its output lines."""
    environ = dict(os.environ, PYTHONPATH=str(_EXAMPLES.parent / "src"), PYTHONIOENCODING="utf-8", **env)
    done = subprocess.run([sys.executable, str(_EXAMPLES / "everyday.py")], capture_output=True, text=True,
                          encoding="utf-8", env=environ, check=False, timeout=120)
    if done.returncode != 0:
        raise AssertionError(f"everyday.py exited with {done.returncode}:\n{done.stderr}")
    return done.stdout.splitlines()


def stable(lines):
    return [line for line in lines if not line.endswith(HOST)]


class EverydayExample(unittest.TestCase):
    """The example's output is compared with the committed snapshot, except host-dependent rows."""

    SNAPSHOT = _EXAMPLES / "everyday.expected.txt"

    def snapshot(self):
        return self.SNAPSHOT.read_text(encoding="utf-8").splitlines()

    def test_output_matches_snapshot(self):
        self.assertEqual(stable(run_everyday()), stable(self.snapshot()))

    def test_snapshot_without_a_tz_database(self):
        """With no tz database, only host-dependent (‡) rows go missing."""
        with tempfile.TemporaryDirectory() as empty:
            self.assertEqual(stable(run_everyday(PYTHONTZPATH=empty)), stable(self.snapshot()))

    def test_layout(self):
        lines = self.snapshot()
        self.assertTrue(all(len(line) <= 100 for line in lines), [line for line in lines if len(line) > 100])
        self.assertEqual([line for line in lines if line[:1].isdigit()][-1], "14. Round trip")
        self.assertTrue(any(line.endswith(HOST) for line in lines))
        self.assertFalse(any("{" in line for line in lines), "no JSON in the output")

    def test_task_markers(self):
        source = (_EXAMPLES / "everyday.py").read_text(encoding="utf-8")
        begins = re.findall(r"^# BEGIN (\S+)$", source, re.M)
        ends = re.findall(r"^# END (\S+)$", source, re.M)
        self.assertEqual(begins, TASKS)
        self.assertEqual(ends, TASKS)


if __name__ == "__main__":
    unittest.main()
