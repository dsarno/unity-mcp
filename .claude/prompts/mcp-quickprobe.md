You are running a two-step Unity MCP connectivity probe. Do exactly the two tool calls below, print their raw results, and stop.

1) Call mcp__unity__list_resources with this JSON (exactly):
   { "ctx": {}, "under": "", "pattern": "*" }

2) Call mcp__unity__read_resource with this JSON (exactly):
   { "uri": "unity://spec/script-edits" }

Rules:
- Print raw tool results verbatim to the console (no reformatting).
- If a call throws a validation or runtime error, print the exception type and message exactly.
- Do not run any other tools or commands. Stop after step 2.
