# reload_sentinel.py
import os, io, re


def _resolve_project_root(hint: str | None) -> str:
    """Best-effort absolute project root resolution.
    Prefers env UNITY_PROJECT_ROOT/MCP_UNITY_PROJECT_ROOT; falls back to asking Unity;
    last resort: current working directory.
    """
    # 1) Environment overrides
    env = (os.environ.get("UNITY_PROJECT_ROOT") or os.environ.get("MCP_UNITY_PROJECT_ROOT") or "").strip()
    if not env:
        env = (hint or "").strip()
    if env:
        pr = env if os.path.isabs(env) else os.path.abspath(env)
        return pr

    # 2) Ask Unity via bridge (best effort)
    try:
        from unity_connection import send_command_with_retry  # type: ignore
        resp = send_command_with_retry("manage_editor", {"action": "get_project_root"})
        if isinstance(resp, dict) and resp.get("success"):
            data = resp.get("data") or {}
            pr = (data.get("projectRoot") or data.get("path") or "").strip()
            if pr:
                return pr if os.path.isabs(pr) else os.path.abspath(pr)
    except Exception:
        pass

    # 3) Fallback
    return os.getcwd()


def _project_package_sentinel(project_root_abs: str) -> str:
    # Packages/com.coplaydev.unity-mcp/Editor/Sentinel/__McpReloadSentinel.cs
    return os.path.join(
        project_root_abs,
        "Packages",
        "com.coplaydev.unity-mcp",
        "Editor",
        "Sentinel",
        "__McpReloadSentinel.cs",
    )


def _project_assets_sentinel(project_root_abs: str) -> str:
    # Assets/Editor/__McpReloadSentinel.cs (project-local copy)
    return os.path.join(project_root_abs, "Assets", "Editor", "__McpReloadSentinel.cs")


def flip_reload_sentinel(project_root: str,
                         rel_path: str = "Assets/Editor/__McpReloadSentinel.cs") -> None:
    """
    Atomically toggle a constant to force an Editor assembly IL change.
    This produces a real on-disk edit that Unity's watcher will see,
    causing compile + domain reload even when unfocused.
    Resolves the sentinel path INSIDE the Unity project (Packages or Assets),
    avoiding process working-directory issues.
    """
    # Prefer an explicit override for the exact file to touch
    override_path = (os.environ.get("MCP_UNITY_SENTINEL_PATH") or "").strip()
    project_root_abs = _resolve_project_root(project_root)

    candidate_paths: list[str] = []

    if override_path:
        path = override_path if os.path.isabs(override_path) else os.path.join(project_root_abs, override_path)
        candidate_paths.append(os.path.abspath(path))
    else:
        # 1) Project package sentinel
        candidate_paths.append(os.path.abspath(_project_package_sentinel(project_root_abs)))
        # 2) Project assets-level sentinel (historical default)
        candidate_paths.append(os.path.abspath(_project_assets_sentinel(project_root_abs)))
        # 3) If caller passed a rel_path, resolve it under the project
        if rel_path:
            rp = rel_path if os.path.isabs(rel_path) else os.path.join(project_root_abs, rel_path)
            candidate_paths.append(os.path.abspath(rp))

    # Choose the first existing file among candidates; otherwise, create under package path
    path = next((p for p in candidate_paths if os.path.exists(p)), candidate_paths[0])

    # Ensure parent directory exists inside the project
    os.makedirs(os.path.dirname(path), exist_ok=True)

    if not os.path.exists(path):
        seed = (
            "#if UNITY_EDITOR\n"
            "namespace MCP.Reload\n"
            "{\n"
            "    internal static class __McpReloadSentinel\n"
            "    {\n"
            "        internal const int Tick = 1;\n"
            "    }\n"
            "}\n"
            "#endif\n"
        )
        with io.open(path, "w", encoding="utf-8", newline="\n") as f:
            f.write(seed)

    with io.open(path, "r", encoding="utf-8") as f:
        src = f.read()

    m = re.search(r"(const\s+int\s+Tick\s*=\s*)(\d+)(\s*;)", src)
    if m:
        nxt = "2" if m.group(2) == "1" else "1"
        new_src = src[:m.start(2)] + nxt + src[m.end(2):]
    else:
        new_src = src + "\n// MCP touch\n"

    tmp = path + ".tmp"
    with io.open(tmp, "w", encoding="utf-8", newline="\n") as f:
        f.write(new_src)
    os.replace(tmp, path)
