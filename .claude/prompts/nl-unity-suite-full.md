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

## Tool mapping (use these APIs)
When the tests say **replace_range** or **regex_replace**, call:
- `mcp__unity__apply_text_edits` for precise text edits, including atomic multi-edit batches (multiple non-overlapping ranges applied together in one call).
- `mcp__unity__script_apply_edits` for regex/anchor or structured method/class edits (pattern- or symbol-based changes).
- `mcp__unity__validate_script` for validation (`level: "standard"`).
Edits within a batch are applied atomically; ranges must be non-overlapping.


## Output Requirements (match NL suite conventions)
- JUnit at `$JUNIT_OUT` if set, otherwise `reports/junit-nl-suite.xml`. Suite name `UnityMCP.NL-T`.
- Markdown at `$MD_OUT` if set, otherwise `reports/junit-nl-suite.md` (CI synthesizes this from JUnit at the end; you do not need to write markdown mid-run).
- Log allowed tools once as a single line: `AllowedTools: ...`.
- For every edit: Read → Write (with precondition hash). On `{status:"stale_file"}`, retry once using a server-provided hash (`current_sha256` or `expected_sha256`) if present; otherwise perform a single re-read and retry.
- Evidence windows only (±20–40 lines); cap unified diffs to 100 lines and note truncation.
- End `<system-out>` with `VERDICT: PASS` or `VERDICT: FAIL`.

### Reporting discipline (must-follow)
- CI pre-creates the report skeletons. Do NOT rewrite wrappers or `$JUNIT_OUT` during the run.
- Do NOT create alternate report files (e.g., `reports/junit-*-updated.xml`).
- Prefer end-of-suite emission: buffer all `<testcase>` fragments in memory during the run, then Write once at the end to `reports/nl_final_results.xml` containing multiple `<testcase>` siblings (no wrappers). CI will assemble into `$JUNIT_OUT`.
- If you must emit per-test files instead, use `reports/nl<CASE>_results.xml` with exactly one `<testcase>` and a `<system-out><![CDATA[...]]></system-out>` that ends with `VERDICT:`.
- Fragments must contain only `<testcase>` (no `<testsuite>`/`<testsuites>`, no leading markers).
- Do NOT use Bash redirection (`>`, `>>`) to write files. Use the Write tool only, and only to paths under `reports/*_results.xml`.
- Do not write markdown mid-run; CI will synthesize the final markdown from JUnit.
- Keep transient state in memory; if persistence is required, use Write to files under `reports/`.

## Safety & hygiene
- Make edits in-place, then revert after validation so the workspace is clean.
- At suite start, capture baseline `{ text, sha256 }` for the target file. After each test, revert to baseline via a guarded write using the current on-disk sha as `precondition_sha256` (use server-provided `current_sha256` on `stale_file`). Only re-read to confirm when validation requires it or a retry failed.
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

### Execution order (must follow; do not regex-filter)
Run tests exactly in this order:
NL-0, NL-1, NL-2, NL-3, NL-4,
T-A, T-B, T-C, T-D, T-E, T-F, T-G, T-H, T-I, T-J.
At suite start, emit a single line plan:
PLAN: NL-0,NL-1,NL-2,NL-3,NL-4,T-A,T-B,T-C,T-D,T-E,T-F,T-G,T-H,T-I,T-J (len=16 inc. bootstrap)
After each testcase, emit:
PROGRESS: <k>/16 completed

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
- Maintain a per-test in-memory working buffer `buf` (text) and `pre_sha = sha256(read_bytes(uri))` (raw bytes; no normalization) at the start of each test.
- After a successful write, update `buf` locally by applying the same edit and recompute `pre_sha` from the on-disk bytes only if needed; prefer avoiding a re-read when positions are stable.
- If a write returns `stale_file`, prefer retrying once without reading using a server-provided hash (`data.current_sha256` or `data.expected_sha256`). Only if neither is present, perform a single re-read and retry; otherwise record failure and continue.
- Re-read only at well-defined points: (a) at the start of each test, (b) after a failed stale retry, or (c) when validation demands it.
- Always revert any mutations at the end of each test, then re-read to confirm clean state before the next test.
- Never abort the suite on a single test failure; log the failure (including `{ status: ... }`) and proceed to the next test.

Logging (print these around each write for CI clarity):
- `pre_sha=<sha256(raw bytes)>` before write
- on stale: `stale: expected=<...> current=<...> retry_pre_sha=<picked>`
- after success: `post_sha=<sha256(raw bytes)>`

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
  # Precondition: buf (text) and pre_sha (sha256 over raw bytes) are current for this test
  edit = make_edit_from_text(buf)         # compute ranges/anchors against in-memory buf
  res  = write(uri, edit, precondition_sha256=pre_sha)
  if res.status == "ok":
      buf     = apply_local(edit, buf)    # update buffer without re-read when possible
      # Optionally refresh pre_sha by hashing on-disk bytes if subsequent ops require exact sync
      # pre_sha = sha256(read_bytes(uri))
  elif res.status == "stale_file":
      # Fast path: retry once using server-provided hash; avoid read if hash is present
      next_sha = (res.data.current_sha256 or res.data.expected_sha256) if hasattr(res, 'data') else None
      if next_sha:
          edit2 = edit_or_recomputed(edit, buf)   # often unchanged if anchors/ranges remain stable
          res2  = write(uri, edit2, precondition_sha256=next_sha)
          if res2.status == "ok":
              buf = apply_local(edit2, buf)
          else:
              record_failure_and_continue()
      else:
          fresh_text  = read(uri)
          fresh_bytes = read_bytes(uri)
          pre_sha     = sha256(fresh_bytes)
          edit2       = make_edit_from_text(fresh_text)
          res2        = write(uri, edit2, precondition_sha256=pre_sha)
          if res2.status == "ok":
              buf = apply_local(edit2, fresh_text)
          else:
              record_failure_and_continue()    # do not loop forever
```
Notes: Prefer `mcp__unity__script_apply_edits` for anchor/regex operations; use `mcp__unity__apply_text_edits` only for precise `replace_range` steps. Always re‑read before each subsequent test so offsets are never computed against stale snapshots.

Revert guidance:
- At test start, snapshot the exact original bytes (including any BOM). For revert, prefer a full-file replace back to that snapshot (single edit). If that’s not available, compute the minimal edit against current `buf` to restore exact content, then confirm hash matches the baseline.

### Status handling
- Treat expected safeguard statuses as non-fatal: `using_guard`, `unsupported`, and similar should record INFO in JUnit and continue.
- For idempotency cases (e.g., T-J), `{ status: "no_change" }` counts as PASS; for tests that require a real change, treat `{ status: "no_change" }` as SKIP/INFO and continue.
