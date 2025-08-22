# Unity NL/T Editing Suite — Full Coverage (NL-0 … T-J)

You are running in CI at the repository root. Use only the tools allowed by the workflow.

## Test target
- Primary file: `TestProjects/UnityMCPTests/Assets/Scripts/LongUnityScriptClaudeTest.cs`
- Prefer structured edit tools via MCP for method/class edits; use text-range ops when specified.
- Include `precondition_sha256` for any text-path write. In CI/headless, pass `project_root: "TestProjects/UnityMCPTests"` when reading/writing by URI.

## Output requirements
- JUnit XML at `reports/claude-nl-tests.xml`; each test = one `<testcase>` with `classname="UnityMCP.NL"` or `UnityMCP.T`.
- On failure: include `<failure>` with concise message and last evidence window (10–20 lines).
- Summary markdown at `reports/claude-nl-tests.md` with checkboxes and windowed reads.
- Restore workspace at end (clean tree).

## Safety & hygiene
- Make edits in-place, then revert after validation (git stash/reset or counter-edits) so the workspace is clean.
- Never push commits from CI.

## CI headless hints
- For `mcp__unity__list_resources`/`read_resource`, include `project_root: "TestProjects/UnityMCPTests"`. `ctx` is optional.

## NL-0. Sanity Reads (windowed)
- Tail 120 lines of the target file.
- Show 40 lines around method `Update`.
- Pass if both windows render expected anchors.

## NL-1. Method replace/insert/delete (natural-language)
- Replace `HasTarget` with block-bodied version returning `currentTarget != null`.
- Insert `PrintSeries()` after `GetCurrentTarget` logging `1,2,3`.
- Verify by reading 20 lines around the anchor.
- Delete `PrintSeries()` and verify removal.
- Pass on matched diffs and windows.

## NL-2. Anchor comment insertion
- Add comment `Build marker OK` immediately above `Update`.
- Pass if comment appears directly above `public void Update()`.

## NL-3. End-of-class insertion
- Insert a 3-line comment `Tail test A/B/C` before the final class brace.
- Pass if windowed read shows three lines at intended location.

## NL-4. Compile trigger (record-only)
- After an edit, ensure no obvious syntax issues; record as INFO. Unity compile runs in a separate step.

## T-A. Anchor insert (text path)
- After `GetCurrentTarget`, insert `private int __TempHelper(int a, int b) => a + b;` via range-based text edit.
- Verify; then delete with `regex_replace` targeting only that helper block.
- Pass if round-trip leaves the file exactly as before.

## T-B. Replace method body with minimal range
- Identify `HasTarget` body lines; single `replace_range` to change only inside braces; then revert.
- Pass on exact-range change + revert.

## T-C. Header/region preservation
- For `ApplyBlend`, change only interior lines via `replace_range`; signature and region markers must remain untouched.
- Pass if unchanged.

## T-D. End-of-class insertion (anchor)
- Find final class brace; insert before to append a temporary helper; then remove.
- Pass if insert/remove verified.

## T-E. Temporary method lifecycle
- Insert helper (T-A), update helper implementation via `apply_text_edits`, then delete with `regex_replace`.
- Pass if lifecycle completes and file returns to original checksum.

## T-F. Multi-edit atomic batch
- In one call, perform two `replace_range` tweaks and one comment insert at class end; verify all-or-nothing behavior.
- Pass if either all 3 apply or none.

## T-G. Path normalization
- Run the same edit once with `unity://path/Assets/Scripts/LongUnityScriptClaudeTest.cs` and once with `Assets/Scripts/LongUnityScriptClaudeTest.cs` (if supported).
- Pass if both target the same file and no duplication.

## T-H. Validation levels
- After edits, run `validate` with `level: "standard"`, then `"basic"` for temporarily unbalanced text ops; final state must be valid.
- Pass if validation OK and final file compiles in the Unity step.

## T-I. Failure surfaces (expected)
- Too large payload: `apply_text_edits` with >15 KB aggregate → expect `{status:"too_large"}`.
- Stale file: resend with old `precondition_sha256` after external change → expect `{status:"stale_file"}`.
- Overlap: two overlapping ranges → expect rejection.
- Unbalanced braces: remove a closing `}` → expect validation failure and no write.
- Header guard: attempt insert before the first `using` → expect `{status:"header_guard"}`.
- Anchor aliasing: `insert`/`content` alias → expect success (aliased to `text`).
- Auto-upgrade: try a text edit overwriting a method header → prefer structured `replace_method` or return clear error.
- Pass when each negative case returns expected failure without persisting changes.

## T-J. Idempotency & no-op
- Re-run the same `replace_range` with identical content → expect success with no change.
- Re-run a delete of an already-removed helper via `regex_replace` → clean no-op.
- Pass if both behave idempotently.

### Implementation notes
- Always capture pre/post windows (±20–40 lines) as evidence in JUnit or system-out.
- For any file write, include `precondition_sha256` and verify post-hash in logs.
- Restore repository to original state at end (`git status` must be clean).


