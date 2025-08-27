You are running a strict, two-step Unity MCP wiring check.

Rules (must follow exactly):
- Do not plan, narrate, or print any text besides raw tool results.
- Make exactly the two tool calls below, in order, with the exact JSON shown.
- If a call fails, print the exception type and message exactly, then stop.

1) Call mcp__unity__find_in_file with:
{
  "project_relative_file": "TestProjects/UnityMCPTests/Assets/Scripts/LongUnityScriptClaudeTest.cs",
  "pattern": "class\\s+LongUnityScriptClaudeTest"
}

2) Call mcp__unity__list_resources with:
{ "ctx": {}, "under": "ClaudeTests", "pattern": "*.cs" }

Stop after step 2.
