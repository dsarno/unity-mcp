# Goal
Fast preflight to confirm the Unity MCP server is reachable and usable in CI.

# What to do
1) Use **ListMcpResourcesTool** first to probe the Unity MCP server for any resources.  
   - If it returns `[]`, try Unity’s direct tools **mcp__unity__list_resources** with just `under` and `pattern`.  
   - **Do not** pass `ctx: ""`. If a `ctx` object is required, pass `{}` (an empty JSON object) or omit it entirely.

2) Locate a test C# file under `ClaudeTests/` (e.g., `ClaudeTests/longUnityScript-claudeTest.cs`) using Bash/Glob and **Read** a small window of lines to confirm anchors like `Update()` exist.

3) Do **not** make destructive edits here. This step is only a smoke test to ensure we can list/read resources successfully before the full NL/T suite.

# Guardrails
- No wildcards in tool names were enabled; you must work with the explicit tools allowed by the workflow.
- Prefer aggregator tools (ListMcpResourcesTool / ReadMcpResourceTool) first; drop down to `mcp__unity__*` tools only when necessary and with correct argument shapes.
- Keep logs short and actionable.

# Output
- Print a brief bullet summary to stdout that includes:
  - Whether resources were detected.
  - The path of the target file you’ll use later.
  - Any issues to watch for (e.g., permission prompts).
