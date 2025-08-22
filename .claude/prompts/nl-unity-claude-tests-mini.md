# Unity NL Editing Suite — Natural Mode

You are running inside CI for the **unity-mcp** repository. Your task is to demonstrate end‑to‑end **natural‑language code editing** on a representative Unity C# script using whatever capabilities and servers are already available in this session. Work autonomously. Do not ask the user for input. Do NOT spawn subagents, as they will not have access to the mcp server process on the top-level agent.

## Mission
1) **Discover capabilities.** Quietly inspect the tools and any connected servers that are available to you at session start. If the server offers a primer or capabilities resource, read it before acting.
2) **Choose a target file.** Prefer `TestProjects/UnityMCPTests/Assets/Scripts/LongUnityScriptClaudeTest.cs` if it exists; otherwise choose a simple, safe C# script under `TestProjects/UnityMCPTests/Assets/`.
3) **Perform a small set of realistic edits** using minimal, precise changes (not full-file rewrites). Examples of small edits you may choose from (pick 3–6 total):
   - Insert a new, small helper method (e.g., a logger or counter) in a sensible location.
   - Add a short anchor comment near a key method (e.g., above `Update()`), then add or modify a few lines nearby.
   - Append an end‑of‑class utility method (e.g., formatting or clamping helper).
   - Make a safe, localized tweak to an existing method body (e.g., add a guard or a simple accumulator).
   - Optionally include one idempotency/no‑op check (re‑apply an edit and confirm nothing breaks).
4) **Validate your edits.** Re‑read the modified regions and verify the changes exist, compile‑risk is low, and surrounding structure remains intact.
5) **Report results.** Produce both:
   - A JUnit XML at `reports/claude-nl-tests.xml` containing a single suite named `UnityMCP.NL` with one testcase per sub‑test you executed (mark pass/fail and include helpful failure text).
   - A summary markdown at `reports/claude-nl-summary.md` that explains what you attempted, what succeeded/failed, and any follow‑ups you would try.
6) **Be gentle and reversible.** Prefer targeted, minimal edits; avoid wide refactors or non‑deterministic changes.

## Assumptions & Hints (non‑prescriptive)
- A Unity‑oriented MCP server is expected to be connected. If a server‑provided **primer/capabilities** resource exists, read it first. If no primer is available, infer capabilities from your visible tools in the session.
- In CI/headless mode, when calling `mcp__unity__list_resources` or `mcp__unity__read_resource`, include:
  - `ctx: {}`
  - `project_root: "TestProjects/UnityMCPTests"` (the server will also accept the absolute path passed via env)
  Example: `{ "ctx": {}, "under": "Assets/Scripts", "pattern": "*.cs", "project_root": "TestProjects/UnityMCPTests" }`
- If the preferred file isn’t present, locate a fallback C# file with simple, local methods you can edit safely.
- If a compile command is available in this environment, you may optionally trigger it; if not, rely on structural checks and localized validation.

## Output Requirements
- `reports/claude-nl-tests.xml` — JUnit XML, suite `UnityMCP.NL`, each sub‑test a separate `<testcase>` with `<failure>` text when relevant.
- `reports/claude-nl-summary.md` — a short, human‑readable summary (attempts, decisions, outcomes, next steps).

## Guardrails
- No destructive operations. Keep changes minimal and well‑scoped.
- Don’t leak secrets or environment details beyond what’s needed in the reports.
- Work without user interaction; do not prompt for approval mid‑flow.

> If capabilities discovery fails, still produce the two reports that clearly explain why you could not proceed and what evidence you gathered.
