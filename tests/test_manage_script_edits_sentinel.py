import sys
import pathlib
import importlib.util
import types
import time
import json

import pytest


# Add server src to import path
ROOT = pathlib.Path(__file__).resolve().parents[1]
SRC = ROOT / "UnityMcpBridge" / "UnityMcpServer~" / "src"
sys.path.insert(0, str(SRC))


# Stub mcp.server.fastmcp so manage_script_edits can import without the dependency
mcp_pkg = types.ModuleType("mcp")
server_pkg = types.ModuleType("mcp.server")
fastmcp_pkg = types.ModuleType("mcp.server.fastmcp")


class _Dummy:
    pass


fastmcp_pkg.FastMCP = _Dummy
fastmcp_pkg.Context = _Dummy
server_pkg.fastmcp = fastmcp_pkg
mcp_pkg.server = server_pkg
sys.modules.setdefault("mcp", mcp_pkg)
sys.modules.setdefault("mcp.server", server_pkg)
sys.modules.setdefault("mcp.server.fastmcp", fastmcp_pkg)


def load_module(path: pathlib.Path, name: str):
    spec = importlib.util.spec_from_file_location(name, path)
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


manage_script_edits = load_module(SRC / "tools" / "manage_script_edits.py", "manage_script_edits")


def test_trigger_sentinel_honors_status_dir(monkeypatch: pytest.MonkeyPatch, tmp_path: pathlib.Path):
    calls = []

    def fake_send(cmd, params, *args, **kwargs):
        calls.append((cmd, params))
        return {"success": True}

    monkeypatch.setattr(manage_script_edits, "send_command_with_retry", fake_send)
    monkeypatch.setenv("UNITY_MCP_STATUS_DIR", str(tmp_path))

    # Case 1: Latest status says reloading => should not send command
    (tmp_path / "unity-mcp-status-1.json").write_text(json.dumps({"reloading": True}))
    manage_script_edits._trigger_sentinel_async()
    time.sleep(0.3)
    assert len(calls) == 0

    # Case 2: Newer status says not reloading => should send command once
    time.sleep(0.1)
    (tmp_path / "unity-mcp-status-2.json").write_text(json.dumps({"reloading": False}))
    manage_script_edits._trigger_sentinel_async()
    time.sleep(0.3)
    assert len(calls) == 1


