# Unity NL/T Editing Suite

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
- JUnit at `$JUNIT_OUT` if set, otherwise `reports/junit-nl-suite.xml`. Suite name `UnityMCP.NL-T`.
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
- At suite start, capture baseline `{ text, sha256 }` for the target file. After each test, revert to baseline via a guarded write using the baseline `precondition_sha256`; re-read to confirm the hash matches.
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
- In a single `mcp__unity__apply_text_edits`/`mcp__unity__script_apply_edits` call, include two `replace_range` tweaks + one end-of-class comment insert using one `precondition_sha256` computed from the same snapshot. The server must apply all edits atomically or reject the entire batch (no partial application).

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
- Compute `precondition_sha256` over raw on-disk bytes (no normalization), unless the server explicitly documents and applies identical normalization on both sides.
- If a write returns `stale_file`, re-read and retry once with the returned hash; otherwise record failure and continue.
- Never abort the suite on a single test failure; log the failure (including `{ status: ... }`) and proceed to the next test.

### Test driver (must follow)
For each test NL-0..NL-4, then T-A..T-J:
1) READ → compute `pre_sha = sha256(read_bytes(uri))`.
2) RUN using the guarded write pattern for every mutation.
3) VALIDATE with `mcp__unity__validate_script(level:"standard")` unless the step is read-only.
4) RE-READ evidence windows; write JUnit + Markdown entries.
5) REVERT: if the test mutated the file, restore the exact pre-test content via a guarded full-file replace using `pre_sha` as `precondition_sha256`; re-read and confirm the hash matches.
6) Append `VERDICT: PASS` or `VERDICT: FAIL` to `<system-out>` for that testcase.
7) Continue to the next test regardless of outcome.

### Guarded write pattern (must use for every edit)
```pseudo
function guarded_write(uri, make_edit_from_text):
  text = read(uri)                      # include ctx:{} and project_root
  buf  = read_bytes(uri)                # raw on-disk bytes for hashing
  sha  = sha256(buf)                    # no normalization
  edit = make_edit_from_text(text)      # compute ranges/anchors against *this* text
  res  = write(uri, edit, precondition_sha256=sha)
  if res.status == "stale_file":
      fresh       = read(uri)
      fresh_bytes = read_bytes(uri)
      # Prefer server-provided expected_sha256 if present; else recompute from raw bytes
      sha2  = res.expected_sha256 or sha256(fresh_bytes)
      edit2 = make_edit_from_text(fresh)   # recompute ranges vs fresh text
      res2  = write(uri, edit2, precondition_sha256=sha2)
      if res2.status != "ok":
          record_failure_and_continue()    # do not loop forever
```
Notes: Prefer `mcp__unity__script_apply_edits` for anchor/regex operations; use `mcp__unity__apply_text_edits` only for precise `replace_range` steps. Always re‑read before each subsequent test so offsets are never computed against stale snapshots.

### Status handling
- Treat expected safeguard statuses as non-fatal: `using_guard`, `unsupported`, and similar should record INFO in JUnit and continue.
- For idempotency cases (e.g., T-J), `{ status: "no_change" }` counts as PASS; for tests that require a real change, treat `{ status: "no_change" }` as SKIP/INFO and continue.
