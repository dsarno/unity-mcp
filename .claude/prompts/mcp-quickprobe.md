# Unity MCP QuickProbe (no planning, 2 calls max)

You must perform **exactly two** tool calls against the MCP server named "unity".  
Do not use any tools except the Unity MCP tools listed below.  
Output only the raw tool results (no prose before/after).

**Call 1 — Read spec**
- Tool: `mcp__unity__read_resource`
- Goal: Read the server’s script-edit spec resource at `unity://spec/script-edits`.

**Call 2 — File discovery**
- Prefer to list Unity-exposed resources under the repo’s test area OR search for a known class.
- EITHER:
  - Tool: `mcp__unity__list_resources` with a pattern that finds C# files beneath `ClaudeTests`.
  - OR, if listing isn’t applicable per tool schema, use:
    - Tool: `mcp__unity__find_in_file` to search `ClaudeTests/longUnityScript-claudeTest.cs`
      for the string `class LongUnityScriptClaudeTest`.

**Rules**
- Do not use: Bash, Read, Write, ListMcpResourcesTool, ReadMcpResourceTool.
- Let the tool schemas from the MCP handshake dictate exact argument names/types.
- If Call 1 fails, still attempt Call 2 and return its raw result.
- Print each result exactly as returned by the tool (JSON or text).
