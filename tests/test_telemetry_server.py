import importlib
import sys
from pathlib import Path
import pytest

# Allow importing telemetry from Server
SERVER_DIR = Path(__file__).resolve().parents[1] / "Server"
sys.path.insert(0, str(SERVER_DIR))

@pytest.fixture(autouse=True)
def _cwd(monkeypatch):
    # Ensure telemetry package can locate pyproject.toml via cwd-relative lookup
    src_dir = Path(__file__).resolve().parents[1] / "MCPForUnity" / "UnityMcpServer~" / "src"
    if not src_dir.exists():
        # Fallback to UnityMcpBridge layout if MCPForUnity path not present
        fallback = Path(__file__).resolve().parents[1] / "UnityMcpBridge" / "UnityMcpServer~" / "src"
        if fallback.exists():
            src_dir = fallback
    monkeypatch.chdir(src_dir)


def test_telemetry_basic():
    from telemetry import (
        get_telemetry,
        record_telemetry,
        record_milestone,
        RecordType,
        MilestoneType,
        is_telemetry_enabled,
    )

    assert isinstance(is_telemetry_enabled(), bool)
    record_telemetry(RecordType.VERSION, {"version": "3.0.2", "test_run": True})
    first = record_milestone(MilestoneType.FIRST_STARTUP, {"test_mode": True})
    assert isinstance(first, bool)
    assert get_telemetry() is not None


def test_telemetry_disabled(monkeypatch):
    monkeypatch.setenv("DISABLE_TELEMETRY", "true")
    import telemetry

    importlib.reload(telemetry)
    from telemetry import is_telemetry_enabled, record_telemetry, RecordType

    assert is_telemetry_enabled() is False
    record_telemetry(RecordType.USAGE, {"test": "ignored"})

    # restore module state for later tests
    monkeypatch.delenv("DISABLE_TELEMETRY", raising=False)
    importlib.reload(telemetry)


def test_data_storage():
    from telemetry import get_telemetry

    coll = get_telemetry()
    cfg = coll.config
    assert cfg.data_dir is not None
    assert cfg.uuid_file is not None
    assert cfg.milestones_file is not None

