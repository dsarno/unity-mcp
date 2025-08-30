import sys
import pytest
import pathlib
import importlib.util
import types


ROOT = pathlib.Path(__file__).resolve().parents[1]
SRC = ROOT / "UnityMcpBridge" / "UnityMcpServer~" / "src"
sys.path.insert(0, str(SRC))

# stub mcp.server.fastmcp
mcp_pkg = types.ModuleType("mcp")
server_pkg = types.ModuleType("mcp.server")
fastmcp_pkg = types.ModuleType("mcp.server.fastmcp")
class _D: pass
fastmcp_pkg.FastMCP = _D
fastmcp_pkg.Context = _D
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


manage_script_edits = _load(SRC / "tools" / "manage_script_edits.py", "manage_script_edits_mod_guard")


class DummyMCP:
    def __init__(self): self.tools = {}
    def tool(self, *args, **kwargs):
        def deco(fn): self.tools[fn.__name__] = fn; return fn
        return deco


def setup_tools():
    mcp = DummyMCP()
    manage_script_edits.register_manage_script_edits_tools(mcp)
    return mcp.tools


def test_regex_delete_structural_guard(monkeypatch):
    tools = setup_tools()
    apply = tools["script_apply_edits"]

    # Craft a minimal C# snippet with a method; a bad regex that deletes only the header and '{'
    # will unbalance braces and should be rejected by preflight.
    bad_pattern = r"(?m)^\s*private\s+void\s+PrintSeries\s*\(\s*\)\s*\{"
    contents = (
        "using UnityEngine;\n\n"
        "public class LongUnityScriptClaudeTest : MonoBehaviour\n{\n"
        "private void PrintSeries()\n{\n    Debug.Log(\"1,2,3\");\n}\n"
        "}\n"
    )

    def fake_send(cmd, params):
        # Only the initial read should be invoked; provide contents
        if cmd == "manage_script" and params.get("action") == "read":
            return {"success": True, "data": {"contents": contents}}
        # If preflight failed as intended, no write should be attempted; return a marker if called
        return {"success": True, "message": "SHOULD_NOT_WRITE"}

    monkeypatch.setattr(manage_script_edits, "send_command_with_retry", fake_send)

    resp = apply(
        ctx=None,
        name="LongUnityScriptClaudeTest",
        path="Assets/Scripts",
        edits=[{"op": "regex_replace", "pattern": bad_pattern, "replacement": ""}],
        options={"validate": "standard"},
    )

    assert isinstance(resp, dict)
    assert resp.get("success") is False
    assert resp.get("code") == "validation_failed"
    data = resp.get("data", {})
    assert data.get("status") == "validation_failed"
    # Helpful hint to prefer structured delete
    assert "delete_method" in (data.get("hint") or "")


# Parameterized robustness cases
BRACE_CONTENT = (
    "using UnityEngine;\n\n"
    "public class LongUnityScriptClaudeTest : MonoBehaviour\n{\n"
    "private void PrintSeries()\n{\n    Debug.Log(\"1,2,3\");\n}\n"
    "}\n"
)

ATTR_CONTENT = (
    "using UnityEngine;\n\n"
    "public class LongUnityScriptClaudeTest : MonoBehaviour\n{\n"
    "[ContextMenu(\"PS\")]\nprivate void PrintSeries()\n{\n    Debug.Log(\"1,2,3\");\n}\n"
    "}\n"
)

EXPR_CONTENT = (
    "using UnityEngine;\n\n"
    "public class LongUnityScriptClaudeTest : MonoBehaviour\n{\n"
    "private void PrintSeries() => Debug.Log(\"1\");\n"
    "}\n"
)


@pytest.mark.parametrize(
    "contents,pattern,repl,expect_success",
    [
        # Unbalanced deletes (should fail with validation_failed)
        (BRACE_CONTENT, r"(?m)^\s*private\s+void\s+PrintSeries\s*\(\s*\)\s*\{", "", False),
        # Remove method closing brace only (leaves class closing brace) -> unbalanced
        (BRACE_CONTENT, r"\n\}\n(?=\s*\})", "\n", False),
        (ATTR_CONTENT, r"(?m)^\s*private\s+void\s+PrintSeries\s*\(\s*\)\s*\{", "", False),
        # Expression-bodied: remove only '(' in header -> paren mismatch
        (EXPR_CONTENT, r"(?m)private\s+void\s+PrintSeries\s*\(", "", False),
        # Safe changes (should succeed)
        (BRACE_CONTENT, r"(?m)^\s*Debug\.Log\(.*?\);\s*$", "", True),
        (EXPR_CONTENT, r"Debug\.Log\(\"1\"\)", "Debug.Log(\"2\")", True),
    ],
)
def test_regex_delete_variants(monkeypatch, contents, pattern, repl, expect_success):
    tools = setup_tools()
    apply = tools["script_apply_edits"]

    def fake_send(cmd, params):
        if cmd == "manage_script" and params.get("action") == "read":
            return {"success": True, "data": {"contents": contents}}
        return {"success": True, "message": "WRITE"}

    monkeypatch.setattr(manage_script_edits, "send_command_with_retry", fake_send)

    resp = apply(
        ctx=None,
        name="LongUnityScriptClaudeTest",
        path="Assets/Scripts",
        edits=[{"op": "regex_replace", "pattern": pattern, "replacement": repl}],
        options={"validate": "standard"},
    )

    if expect_success:
        assert isinstance(resp, dict) and resp.get("success") is True
    else:
        assert isinstance(resp, dict) and resp.get("success") is False and resp.get("code") == "validation_failed"


