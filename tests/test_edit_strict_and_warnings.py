import sys
import pathlib
import importlib.util
import types


ROOT = pathlib.Path(__file__).resolve().parents[1]
SRC = ROOT / "MCPForUnity" / "UnityMcpServer~" / "src"
sys.path.insert(0, str(SRC))

# stub mcp.server.fastmcp
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


def _load(path: pathlib.Path, name: str):
    spec = importlib.util.spec_from_file_location(name, path)
    mod = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(mod)
    return mod


manage_script = _load(SRC / "tools" / "manage_script.py", "manage_script_mod3")


class DummyMCP:
    def __init__(self): self.tools = {}

    def tool(self, *args, **kwargs):
        def deco(fn): self.tools[fn.__name__] = fn; return fn
        return deco


def setup_tools():
    mcp = DummyMCP()
    # Import tools to trigger decorator-based registration
    import tools.manage_script
    from registry import get_registered_tools
    for tool_info in get_registered_tools():
        name = tool_info['name']
        if any(k in name for k in ['script', 'apply_text', 'create_script', 'delete_script', 'validate_script', 'get_sha']):
            mcp.tools[name] = tool_info['func']
    return mcp.tools
class DummyContext:
    def info(self, *args, **kwargs):
        pass
    def warning(self, *args, **kwargs):
        pass
    def error(self, *args, **kwargs):
        pass



def test_explicit_zero_based_normalized_warning(monkeypatch):
    tools = setup_tools()
    apply_edits = tools["apply_text_edits"]

    def fake_send(cmd, params):
        # Simulate Unity path returning minimal success
        return {"success": True}

    import unity_connection
    monkeypatch.setattr(unity_connection, "send_command_with_retry", fake_send)

    # Explicit fields given as 0-based (invalid); SDK should normalize and warn
    edits = [{"startLine": 0, "startCol": 0,
              "endLine": 0, "endCol": 0, "newText": "//x"}]
    resp = apply_edits(DummyContext(), uri="unity://path/Assets/Scripts/F.cs",
                       edits=edits, precondition_sha256="sha")

    assert resp["success"] is True
    data = resp.get("data", {})
    assert "normalizedEdits" in data
    assert any(
        w == "zero_based_explicit_fields_normalized" for w in data.get("warnings", []))
    ne = data["normalizedEdits"][0]
    assert ne["startLine"] == 1 and ne["startCol"] == 1 and ne["endLine"] == 1 and ne["endCol"] == 1


def test_strict_zero_based_error(monkeypatch):
    tools = setup_tools()
    apply_edits = tools["apply_text_edits"]

    def fake_send(cmd, params):
        return {"success": True}

    import unity_connection
    monkeypatch.setattr(unity_connection, "send_command_with_retry", fake_send)

    edits = [{"startLine": 0, "startCol": 0,
              "endLine": 0, "endCol": 0, "newText": "//x"}]
    resp = apply_edits(DummyContext(), uri="unity://path/Assets/Scripts/F.cs",
                       edits=edits, precondition_sha256="sha", strict=True)
    assert resp["success"] is False
    assert resp.get("code") == "zero_based_explicit_fields"
