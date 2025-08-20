You are running a two-step Unity MCP wiring check. Do exactly the two tool calls below, print their raw results, and stop.

1) Call mcp__unity__find_in_file with this JSON (exactly):
   {
     "project_relative_file": "ClaudeTests/longUnityScript-claudeTest.cs",
     "pattern": "class\\s+LongUnityScriptClaudeTest"
   }

2) Call mcp__unity__list_resources with this JSON (exactly):
   { "ctx": {}, "under": "ClaudeTests", "pattern": "*.cs" }

Rules:
- Print the raw tool results verbatim to the console (no reformatting).
- If a call throws a validation or runtime error, print the exception type and message exactly.
- Do not run any other tools or commands. Stop after step 2.
