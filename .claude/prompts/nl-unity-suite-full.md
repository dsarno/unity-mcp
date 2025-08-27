# Unity NL/T Editing Suite — Hybrid (Mini setup + Full tests)

You are running inside CI for the unity-mcp repository. Use only the tools allowed by the workflow. Work autonomously; do not prompt the user. Do NOT spawn subagents.

## Mission
1) Discover capabilities (primer/capabilities if available).
2) Choose target file: prefer `TestProjects/UnityMCPTests/Assets/Scripts/LongUnityScriptClaudeTest.cs`; otherwise a simple C# under `TestProjects/UnityMCPTests/Assets/`.
3) Execute the full NL/T test list below using minimal, precise edits. Keep changes small and reversible.
4) Validate each edit via re-read and windows/diffs.
5) Report results in JUnit XML and Markdown.

## Assumptions & Hints
- Include `ctx: {}` and `project_root: "TestProjects/UnityMCPTests"` for list/read/edit operations in CI.
- If the preferred file is missing, select a safe alternative C# script under `Assets/`.
- If compilation is unavailable, rely on structural checks and validation tools.

+## Tool mapping (use these APIs)
+When the tests say **replace_range** or **regex_replace**, call:
+- `mcp__unity__apply_text_edits` for single-range inserts/replacements.
+- `mcp__unity__script_apply_edits` for regex/anchor operations.
+- `mcp__unity__validate_script` for validation (`level: "standard"`).


## Output Requirements (match NL suite conventions)
- JUnit at `$JUNIT_OUT` if set, otherwise `reports/junit-nl-suite.xml`. Suite name `UnityMCP.NL`.
- Markdown at `$MD_OUT` if set, otherwise `reports/junit-nl-suite.md`.
- Log allowed tools once as a single line: `AllowedTools: ...`.
- For every edit: Read → Write (with precondition hash) → Re-read; on `{status:"stale_file"}` retry once after re-read.
- Evidence windows only (±20–40 lines); cap unified diffs to 100 lines and note truncation.
- End `<system-out>` with `VERDICT: PASS` or `VERDICT: FAIL`.

### Reporting discipline (must-follow)
- At suite start, create a failing skeleton JUnit and Markdown via Write:
  - JUnit: one suite `UnityMCP.NL-T`, testcase `NL-Suite.Bootstrap` failed with message `bootstrap`.
  - Markdown: stub header and empty checklist.
- After each test, update both files: append/replace testcases with evidence windows and diffs; maintain terminal VERDICT line.
- On fatal error/time budget, flush current progress so CI never sees an empty reports/.

## Safety & hygiene
- Make edits in-place, then revert after validation so the workspace is clean.
- Never push commits from CI.
- Do not modify Unity start/stop/licensing; assume Unity is already running per workflow.

## CI headless hints
- For `mcp__unity__list_resources`/`read_resource`, specify:
  - `project_root`: `"TestProjects/UnityMCPTests"`
  - `ctx`: `{}`
- Canonical URIs:
  - `unity://path/Assets/Scripts/LongUnityScriptClaudeTest.cs`
  - `Assets/Scripts/LongUnityScriptClaudeTest.cs`

## Full NL/T Test List (imported)

### NL-0. Sanity Reads (windowed)
- Tail 120 lines; read 40 lines around `Update()` signature.

### NL-1. Method replace/insert/delete
- Replace `HasTarget` body to `return currentTarget != null;`
- Insert `PrintSeries()` after `GetCurrentTarget` logging `"1,2,3"`.
- Verify windows, then delete `PrintSeries()`; confirm original hash.

### NL-2. Anchor comment insertion
- Insert `// Build marker OK` immediately above `public void Update(...)` (ignore XML docs).

### NL-3. End-of-class insertion
- Insert three lines `// Tail test A/B/C` before final class brace; preserve indentation and trailing newline.

### NL-4. Compile trigger (record-only)
- Ensure no obvious syntax issues; record INFO.

### T-A. Anchor insert (text path)
- After `GetCurrentTarget`, insert `private int __TempHelper(int a, int b) => a + b;` via `replace_range` at insertion point; verify; then delete via `regex_replace`.

### T-B. Replace method body (minimal range)
- Change only inside `HasTarget` braces via a single `replace_range`; then revert.

### T-C. Header/region preservation
- For `ApplyBlend`, modify interior lines only; keep signature/docs/regions unchanged.

### T-D. End-of-class insertion (anchor)
- Find final class brace; insert helper before; then remove.

### T-E. Temporary method lifecycle
- Insert helper (T-A), update via `apply_text_edits`, then delete via `regex_replace`.

### T-F. Multi-edit atomic batch
- In one call, do two `replace_range` tweaks + one end-of-class comment insert. Must be atomic or rejected as a whole.

### T-G. Path normalization
- Run the same edit with both URIs; second attempt should return `{ status: "no_change" }`.

### T-H. Validation levels
- Use `validate_script` with `level: "standard"` after edits; only allow `basic` for transient steps.

### T-I. Failure surfaces (expected)
- Too large payload → `{status:"too_large"}`
- Stale file (old hash) → `{status:"stale_file"}`
- Overlap → rejection
- Unbalanced braces → validation failure
- Using-directives guard → `{status:"using_guard"}`
- Parameter aliasing accepted; server echoes canonical keys.
- Auto-upgrade: prefer structured edits or return clear error.

### T-J. Idempotency & no-op
- Re-run identical `replace_range` → `{ status: "no_change" }` and unchanged hash.
- Re-run delete of already-removed helper via `regex_replace` → no-op.

### Implementation notes
- Capture pre/post windows; include pre/post hashes in logs.
- Normalize line endings to LF when computing `precondition_sha256`.
- If a write returns `stale_file`, re-read and retry once with the returned hash; otherwise record failure and continue.
