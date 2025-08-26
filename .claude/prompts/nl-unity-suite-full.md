# Unity NL/T Editing Suite — Full Coverage (NL-0 … T-J)
Version: 1.1.0 (update this when the prompt changes materially)
Consumed by: .github/workflows/claude-nl-suite.yml (Unity NL suite job)

You are running in CI at the repository root. Use only the tools allowed by the workflow (see `allowed_tools` in .github/workflows/claude-nl-suite.yml).
At the start of the first test, log the effective `allowed_tools` list into the `<system-out>` for easier troubleshooting.

## Sharding and filtering
- Honor a `TEST_FILTER` variable (passed via the workflow `vars` JSON) of the form `group:<name>`.
- Supported groups: `edits`, `scenes`, `assets`, `menu`, `shader`, `validate`.
- Default if missing or unrecognized: `group:edits`.
- Only run tests mapped to the selected group. For other groups, emit a minimal JUnit with zero or informational testcases and a markdown note indicating no applicable tests for the group.

### Variables
- `TEST_FILTER`: selection filter (e.g., `group:edits`).
- `JUNIT_OUT`: path for JUnit XML output. Default: `reports/claude-nl-tests.xml`.
- `MD_OUT`: path for summary markdown. Default: `reports/claude-nl-tests.md`.

### MCP connectivity preflight
- Before running any tests in a shard, perform a quick MCP connectivity check with retries (60–90s total):
  1. Attempt `mcp__unity__manage_editor` with `{ action: "get_state" }`.
  2. If unsupported, attempt `mcp__unity__list_resources` with `{ project_root: "TestProjects/UnityMCPTests", under: "Assets", pattern: "*.cs", limit: 5 }`.
  3. Treat transient "Could not connect to Unity" as retryable until the window expires.
- On success: record an INFO testcase noting attempts and elapsed time and continue.
- On failure: emit a single failing testcase (e.g., `NL-Preflight.MCPConnect`) with `<failure>` message and stop the shard.

## Test target
- Primary file: `TestProjects/UnityMCPTests/Assets/Scripts/LongUnityScriptClaudeTest.cs`
- Prefer structured edit tools via MCP for method/class edits; use text-range ops when specified.
- Include `precondition_sha256` for any write (text-path or structured edit). In CI/headless, pass `project_root: "TestProjects/UnityMCPTests"` when reading/writing by URI.
  - Hash must be the SHA-256 of the on-disk file bytes immediately before applying the edit (normalize line endings per Implementation notes).

## Output requirements
- JUnit XML at `JUNIT_OUT` (or `reports/claude-nl-tests.xml` if unset). Create the `reports/` directory if missing.
- One `<testsuite name="UnityMCP.NL-T">` wrapping all `<testcase>` elements.
- Each `<testcase>` must set:
  - `classname` ∈ {`UnityMCP.NL`, `UnityMCP.T`}
  - `name` = short, unique id (e.g., `NL-1.ReplaceHasTarget`, `T-F.AtomicBatch`)
  - `time` in seconds (float)
- Emit `<system-out>` with evidence and end with a single terminal line: `VERDICT: PASS` or `VERDICT: FAIL` (uppercase, exact match).
- For any test that performs changes, include a compact unified diff in `<system-out>` using the standard format and cap to 300 lines. If truncated, include `...diff truncated...` before `VERDICT: ...`.
- On failure: include `<failure>` with a concise message and an evidence window (10–20 lines) from the target file around the anchor/edited region, in addition to the diff.
- Summary markdown at `MD_OUT` (or `reports/claude-nl-tests.md` if unset) with checkboxes, windowed reads, and inline diffs for changed tests.
- XML safety: Wrap all `<system-out>`, `<system-err>`, and `<failure>` contents in CDATA blocks to avoid XML escaping issues (e.g., `&` in code). Use the following rule for embedded CDATA terminators: if `]]>` appears in content, split as `]]]]><![CDATA[>`. Example:

  ```xml
  <testcase classname="UnityMCP.NL" name="Example">
    <system-out><![CDATA[
Tail window...
--- a/File.cs
+++ b/File.cs
@@ ...
VERDICT: PASS
]]></system-out>
  </testcase>
  ```

  JUnit pass/fail is determined by the presence of `<failure>` or `<error>`. Keep `VERDICT: ...` for human readability inside CDATA; do not rely on it for status.
- Upload both JUnit and markdown outputs for the shard as workflow artifacts.
- Restore workspace at end (clean tree).

## Safety & hygiene
- Make edits in-place, then revert after validation so the workspace is clean.
  - Preferred: `git restore --staged --worktree :/` (or `git checkout -- .` on older Git) to discard all changes.
  - Avoid `git stash` in CI unless you also clear stashes, as they may complicate cleanup.
- Never push commits from CI.
- Do not start/stop Unity or modify licensing/activation steps; assume Unity is already running and licensed by the workflow. If a license error is detected in logs, record failure in JUnit and stop the shard.

## Group mapping
- `group:edits`: Run all NL-* and T-* tests defined below (NL-0 … NL-4, T-A … T-J).
- `group:scenes`, `group:assets`, `group:menu`, `group:shader`, `group:validate`: No-op for this prompt version; emit a minimal report with an informational `<system-out>` indicating no applicable tests for the selected group.

## CI headless hints
- For `mcp__unity__list_resources`/`read_resource`, specify:
  - `project_root`: string (required—no default), e.g., `"TestProjects/UnityMCPTests"`
  - `ctx`: object (optional, defaults to `{}`)
- Canonical URIs:
  - `unity://path/Assets/Scripts/LongUnityScriptClaudeTest.cs`
  - `Assets/Scripts/LongUnityScriptClaudeTest.cs` (normalized by the server)

## NL-0. Sanity Reads (windowed)
- Tail 120 lines of the target file; expect to find the class closing brace `^\s*}\s*$` and at least one `Debug\\.Log` call.
- Show 40 lines around method `Update` (anchor: `^\s*public\s+void\s+Update\s*\(`).
- Pass if:
  - Tail window contains the final class brace.
  - The `Update` window contains the method signature line and at least one statement.

## NL-1. Method replace/insert/delete (natural-language)
- Replace `HasTarget` with block-bodied version returning `currentTarget != null`.
- Insert `PrintSeries()` after `GetCurrentTarget` that logs `1,2,3` via `UnityEngine.Debug.Log("1,2,3");`.
- Verify by reading 20 lines around the anchor.
- Delete `PrintSeries()` and verify removal; confirm file hash equals the pre-edit hash.
- Pass on matched diffs and windows.

## NL-2. Anchor comment insertion
- Insert a single-line C# comment `// Build marker OK` on the line immediately preceding the `public void Update(...)` signature (ignoring XML doc comments).
- Pass if the comment is adjacent to the signature with no blank line in between.

## NL-3. End-of-class insertion
- Insert a 3-line comment `// Tail test A`, `// Tail test B`, `// Tail test C` immediately before the final class brace.
- Preserve existing indentation; ensure the file ends with a single trailing newline.

## NL-4. Compile trigger (record-only)
- After an edit, ensure no obvious syntax issues; record as INFO. Unity compile runs in a separate step.

## T-A. Anchor insert (text path)
- After `GetCurrentTarget`, insert `private int __TempHelper(int a, int b) => a + b;` via a single `replace_range` at the exact insertion point (range start=end).
- Normalize line endings to LF (`\n`) for hashing and diff emission; preserve original on write if required by the server.
- Verify; then delete with `regex_replace` targeting only that helper block.
- Pass if round-trip leaves the file exactly as before.

## T-B. Replace method body with minimal range
- Identify `HasTarget` body lines; single `replace_range` to change only inside braces; then revert.
- Pass on exact-range change + revert.

## T-C. Header/region preservation
- For `ApplyBlend`, change only interior lines via `replace_range`.
  - Do not modify: method signature line, attributes, XML docs, `#region`/`#endregion` markers, or surrounding whitespace outside the body braces.
- Pass if unchanged.

## T-D. End-of-class insertion (anchor)
- Find final class brace; insert before to append a temporary helper; then remove.
- Pass if insert/remove verified.

## T-E. Temporary method lifecycle
- Insert helper (T-A), update helper implementation via `apply_text_edits`, then delete with `regex_replace`.
- Pass if lifecycle completes and file returns to original checksum.

## T-F. Multi-edit atomic batch
- In one call, perform two `replace_range` tweaks and one comment insert at class end.
- The server must apply all edits atomically or reject the entire batch.
- On rejection, respond with `{ status: "atomic_reject", reason, conflicts: [...] }` and leave the file unchanged (hash equals precondition).
- Pass if either all 3 apply or `status == "atomic_reject"` with unchanged file hash.

## T-G. Path normalization
- Run the same edit with both URIs:
  1) `unity://path/Assets/Scripts/LongUnityScriptClaudeTest.cs`
  2) `Assets/Scripts/LongUnityScriptClaudeTest.cs`
- The server must canonicalize both to the same absolute path under `project_root` and reject duplicate-application within a single batch.
- Pass if both map to the same file path and the second attempt returns `{ status: "no_change" }`.

## T-H. Validation levels
- Validation levels:
  - `basic`: lexical checks (UTF-8, balanced quotes, no NULs), can tolerate temporarily unbalanced braces.
  - `standard`: `basic` + C# tokenization and brace balance + forbid edits before first `using`.
- After edits, run `validate` with `level: "standard"`. If a text op is intentionally transiently unbalanced, allow `basic` only for the intermediate step; final state must pass `standard`.
- Pass if validation OK and final file compiles in the Unity step.

## T-I. Failure surfaces (expected)
- Too large payload: `apply_text_edits` with >15 KB aggregate → expect `{status:"too_large"}`.
- Stale file: resend with old `precondition_sha256` after external change → expect `{status:"stale_file"}`.
- Overlap: two overlapping ranges → expect rejection.
- Unbalanced braces: remove a closing `}` → expect validation failure and no write.
- Using-directives guard: attempt insert before the first `using` → expect `{status:"using_guard"}`.
- Parameter aliasing: accept `insert`/`content` as aliases for `text` in insertion APIs → expect success. Server should echo the canonical key `text` in responses.
- Auto-upgrade: try a text edit overwriting a method header → prefer structured `replace_method` or return clear error.
- Pass when each negative case returns expected failure without persisting changes.

<!-- Enumerate statuses for CI assertions -->
- Permitted statuses (string enum):
  - "ok"
  - "no_change"
  - "too_large"
  - "stale_file"
  - "overlap"
  - "unbalanced"
  - "using_guard"
  - "atomic_reject"
  - "unsupported"
- All non-"ok"/"no_change" statuses MUST NOT modify files (verify via unchanged post-hash).

## T-J. Idempotency & no-op
- Re-run the same `replace_range` with identical content → expect `{ status: "no_change" }` and unchanged hash.
- Re-run a delete of an already-removed helper via `regex_replace` → clean no-op with `{ status: "no_change" }`.
- Pass if both behave idempotently.

### Implementation notes
- Always capture pre/post windows (±20–40 lines) as evidence in JUnit or system-out.
- For any file write, include `precondition_sha256` computed over file bytes after normalizing line endings to LF (`\n`) and ensuring UTF-8 without BOM, unless the server specifies otherwise.
- Verify the post-edit file hash in logs and include both pre- and post-hashes in `<system-out>`.
- Restore repository to original state at end (`git status` must be clean). If not clean, mark the suite as FAIL.


