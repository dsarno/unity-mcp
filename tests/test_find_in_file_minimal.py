import sys
import pathlib
import importlib.util
import types
import asyncio
import pytest

ROOT = pathlib.Path(__file__).resolve().parents[1]
SRC = ROOT / "UnityMcpBridge" / "UnityMcpServer~" / "src"
sys.path.insert(0, str(SRC))

from tools.resource_tools import register_resource_tools  # type: ignore

class DummyMCP:
    def __init__(self):
        self.tools = {}

    def tool(self, *args, **kwargs):
        def deco(fn):
            self.tools[fn.__name__] = fn
            return fn
        return deco

@pytest.fixture()
def resource_tools():
    mcp = DummyMCP()
    register_resource_tools(mcp)
    return mcp.tools

def test_find_in_file_returns_positions(resource_tools, tmp_path):
    proj = tmp_path
    assets = proj / "Assets"
    assets.mkdir()
    f = assets / "A.txt"
    f.write_text("hello world", encoding="utf-8")
    find_in_file = resource_tools["find_in_file"]
    loop = asyncio.new_event_loop()
    try:
        resp = loop.run_until_complete(
            find_in_file(uri="unity://path/Assets/A.txt", pattern="world", ctx=None, project_root=str(proj))
        )
    finally:
        loop.close()
    assert resp["success"] is True
    assert resp["data"]["matches"] == [{"startLine": 1, "startCol": 7, "endLine": 1, "endCol": 12}]
