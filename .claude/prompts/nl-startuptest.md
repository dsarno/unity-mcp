# CLAUDE TASK: Unity MCP startup checks

You are running in CI at the repository root. Use only allowed tools.

- Verify that the MCP server `unity` is connected.
- List tools and assert presence of these IDs:
  - mcp__unity__script_apply_edits
  - mcp__unity__manage_script
  - mcp__unity__list_resources
  - mcp__unity__read_resource
- Try native resources: call ListMcpResourcesTool. If it returns [], fall back to mcp__unity__list_resources.
- Read one resource:
  - Prefer `unity://spec/script-edits` via native read; otherwise use mcp__unity__read_resource.
- Perform one minimal structured edit preview (no write):
  - Use mcp__unity__script_apply_edits with `options.preview=true` to insert a comment above `Update` in `ClaudeTests/longUnityScript-claudeTest.cs`, then stop.

Output:
- Write a short JUnit at `reports/claude-nl-tests.xml` with a single `<testsuite>` containing 2–3 `<testcase>` entries (tools present, resources readable, preview edit ok). On failure, include a `<failure>` element with concise reason.
- Also write a brief markdown summary at `reports/claude-nl-tests.md`.
