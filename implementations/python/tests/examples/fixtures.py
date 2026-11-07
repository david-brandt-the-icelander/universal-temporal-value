"""Shared test data for the example tests. Each factory returns a fresh, valid value."""

from pathlib import Path

from utv import ZoneRules

_REPOSITORY_ROOT = Path(__file__).resolve().parents[4]


def demo_zone_rules() -> ZoneRules:
    """The pinned zone rules of ``conformance/rules/demo-zones.json``: America/New_York, Europe/Paris and
    Atlantic/Reykjavik, for 2026 and 2027 only. UTV never reads the host's time zone database."""
    return ZoneRules.load(_REPOSITORY_ROOT / "conformance" / "rules" / "demo-zones.json")
