import os

# Ensure telemetry is disabled during test collection and execution to avoid
# any background network or thread startup that could slow or block pytest.
os.environ.setdefault("DISABLE_TELEMETRY", "true")
os.environ.setdefault("UNITY_MCP_DISABLE_TELEMETRY", "true")
os.environ.setdefault("MCP_DISABLE_TELEMETRY", "true")

# Avoid collecting tests under the two 'src' package folders to prevent
# duplicate-package import conflicts (two different 'src' packages).
collect_ignore = [
    "UnityMcpBridge/UnityMcpServer~/src",
    "MCPForUnity/UnityMcpServer~/src",
]
collect_ignore_glob = [
    "UnityMcpBridge/UnityMcpServer~/src/*",
    "MCPForUnity/UnityMcpServer~/src/*",
]

def pytest_ignore_collect(path, config):
    p = str(path)
    return (
        "/UnityMcpBridge/UnityMcpServer~/src/" in p
        or "/MCPForUnity/UnityMcpServer~/src/" in p
        or p.endswith("UnityMcpBridge/UnityMcpServer~/src")
        or p.endswith("MCPForUnity/UnityMcpServer~/src")
    )
