import os
from pathlib import Path
import sys
import importlib


# Add Server dir to path for telemetry imports
SERVER_DIR = Path(__file__).resolve().parents[1] / "Server"
sys.path.insert(0, str(SERVER_DIR))


def test_telemetry_basic():
    from telemetry import (
        get_telemetry,
        record_telemetry,
        record_milestone,
        RecordType,
        MilestoneType,
        is_telemetry_enabled,
    )

    # Should not raise
    assert isinstance(is_telemetry_enabled(), bool)

    # Record simple version event
    record_telemetry(RecordType.VERSION, {"version": "3.0.2", "test_run": True})

    # Record a milestone
    first_flag = record_milestone(MilestoneType.FIRST_STARTUP, {"test_mode": True})
    assert isinstance(first_flag, bool)

    # Collector should be retrievable
    collector = get_telemetry()
    assert collector is not None


def test_telemetry_disabled(monkeypatch):
    # Disable telemetry via env and reload module
    monkeypatch.setenv("DISABLE_TELEMETRY", "true")
    import telemetry

    importlib.reload(telemetry)

    from telemetry import is_telemetry_enabled, record_telemetry, RecordType

    assert is_telemetry_enabled() is False

    # Should not raise even when disabled (no-op)
    record_telemetry(RecordType.USAGE, {"test": "should_be_ignored"})


def test_data_storage():
    from telemetry import get_telemetry

    collector = get_telemetry()
    assert collector is not None

    # Ensure key files/paths are present
    cfg = collector.config
    assert cfg.data_dir is not None
    assert cfg.uuid_file is not None
    assert cfg.milestones_file is not None

