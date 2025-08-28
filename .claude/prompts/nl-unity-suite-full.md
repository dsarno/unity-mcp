# Unity NL/T Editing Suite — CI Agent Contract

You are running inside CI for the `unity-mcp` repo. Use only the tools allowed by the workflow. Work autonomously; do not prompt the user. Do NOT spawn subagents.

**Print this once, verbatim, early in the run:**
AllowedTools: Write,Bash(printf:*),Bash(echo:*),mcp__unity__manage_editor,mcp__unity__list_resources,mcp__unity__read_resource,mcp__unity__apply_text_edits,mcp__unity__script_apply_edits,mcp__unity__validate_script,mcp__unity__find_in_file, mcp__read__console

---

## Mission
1) Pick target file (prefer):
   - `unity://path/Assets/Scripts/LongUnityScriptClaudeTest.cs`
2) Execute **all** NL/T tests in order (listed below) using minimal, precise edits.
3) Validate each edit with `mcp__unity__validate_script(level:"standard")`.
4) **Report**: write a `<testcase>` XML fragment per test to `reports/<TESTID>_results.xml`. Do **not** edit or read `$JUNIT_OUT`.
5) Revert file changes after each test; keep workspace clean.

---

## Environment & Paths (CI)
- Always pass: `project_root: "TestProjects/UnityMCPTests"` and `ctx: {}` on list/read/edit/validate.
- **Canonical URIs only**:
  - Primary: `unity://path/Assets/...` (never embed `project_root` into the URI)
  - Relative (when supported): `Assets/...`
- CI prepares:
  - `$JUNIT_OUT=reports/junit-nl-suite.xml` (pre‑created skeleton; do not modify directly)
  - `$MD_OUT=reports/junit-nl-suite.md` (CI synthesizes from JUnit)

---

## Tool Mapping
- **Anchors/regex/structured**: `mcp__unity__script_apply_edits`
- **Precise ranges / atomic multi‑edit batch**: `mcp__unity__apply_text_edits` (non‑overlapping ranges)
- **Validation**: `mcp__unity__validate_script(level:"standard")`
- **Reporting**: `Write` small XML fragments to `reports/*_results.xml`.  
  Bash is allowed but not required; do not use Bash for diagnostics or env probing.

> Never call: `mcp__unity__create_script`, “console/read_console”, or any tool not in AllowedTools.  
> Never edit `using` directives or the header region.

---

## Output Rules (JUnit fragments only)
- For each test, create **one** file: `reports/<TESTID>_results.xml` containing exactly a `<testcase ...> ... </testcase>`.
- Put human‑readable lines (PLAN/PROGRESS/evidence) **inside** `<![CDATA[ ... ]]>` of that testcase’s `<system-out>`.
- Evidence windows only (±20–40 lines). If a unified diff is shown, cap at 100 lines and note truncation.
- **Do not** open/patch `$JUNIT_OUT` or `$MD_OUT`. CI will merge fragments and synthesize Markdown.

**Example fragment (shape):**
```xml
<testcase classname="UnityMCP.NL-T" name="NL-1. Method replace/insert/delete">
  <system-out><![CDATA[
PLAN: NL-0,NL-1,NL-2,NL-3,NL-4,T-A,T-B,T-C,T-D,T-E,T-F,T-G,T-H,T-I,T-J (len=15)
PROGRESS: 2/15 completed
pre_sha=<...>
... evidence windows ...
VERDICT: PASS
]]></system-out>
</testcase>

### Guarded Write Pattern (must follow)

- Before any mutation in a test: set `buf = read_text(uri)` and `pre_sha = sha256(read_bytes(uri))`.
- Write using `precondition_sha256 = pre_sha`.
- On `{status:"stale_file"}`:
  - Retry once using server hash (`data.current_sha256` or `data.expected_sha256`).
  - If no hash provided, do one re-read then retry once. No loops.
- After every successful write:
  - Immediately re-read raw bytes and set `pre_sha = sha256(read_bytes(uri))` before any further edits in the same test.
- Keep edits inside method bodies where possible. Use anchors for end-of-class/above-method insertions.
- Do not touch header/using regions.

### Revert at test end

- Restore exact pre-test bytes via a single full-file replace with `precondition_sha256` = current on-disk sha (or server-provided hash on stale), then re-read to confirm baseline hash.

### Execution Order (fixed)

- Run exactly in this order (15 tests total):
  - NL-0, NL-1, NL-2, NL-3, NL-4, T-A, T-B, T-C, T-D, T-E, T-F, T-G, T-H, T-I, T-J
- At NL‑0, include the PLAN line (len=15).
- After each testcase, include `PROGRESS: <k>/15 completed`.

### Test Specs (concise)

- NL‑0. Sanity reads
  - Tail ~120 lines; read ±40 lines around `Update()`.

- NL‑1. Method replace/insert/delete
  - Replace `HasTarget` body → `return currentTarget != null;`
  - Insert `PrintSeries()` after `GetCurrentTarget` logging "1,2,3".
  - Verify windows, then delete `PrintSeries()`; confirm original hash.

- NL‑2. Anchor comment
  - Insert `// Build marker OK` immediately above `public void Update(...)` (ignore XML docs).

- NL‑3. End‑of‑class insertion
  - Insert three lines `// Tail test A/B/C` before final class brace; preserve indentation + trailing newline.

- NL‑4. Compile trigger (record‑only)
  - Record INFO if no obvious syntax issues.

- T‑A. Anchor insert (text path)
  - After `GetCurrentTarget`, insert helper:
    ```csharp
    private int __TempHelper(int a, int b) => a + b;
    ```
  - Minimal insertion; verify; then delete via `regex_replace`.

- T‑B. Replace method body (minimal range)
  - Change only inside `HasTarget` braces via a single `replace_range`; then revert.

- T‑C. Header/region preservation
  - For `ApplyBlend`, modify interior only; keep signature/docs/regions unchanged.

- T‑D. End‑of‑class insertion (anchor)
  - Insert helper before the final class brace; then remove.

- T‑E. Temporary method lifecycle
  - Insert helper (as in T‑A), update via `apply_text_edits`, then delete via `regex_replace`.

- T‑F. Multi‑edit atomic batch
  - In a single call, two small `replace_range` tweaks + one end‑of‑class comment, using one `precondition_sha256` from the same snapshot. Server must apply all or reject all.

- T‑G. Path normalization
  - Perform the same edit once with `unity://path/Assets/...` then with `Assets/...`. The second should yield `{status:"no_change"}`.

- T‑H. Validation levels
  - Use `validate_script(level:"standard")` after edits; `basic` only for transient checks.

- T‑I. Failure surfaces (expected)
  - Record INFO on `{status:"too_large"}`, `{status:"stale_file"}`, overlap rejection, validation failure (unbalanced braces), `{status:"using_guard"}`. No retries beyond the guarded pattern.

- T‑J. Idempotency & no‑op
  - Re‑run identical `replace_range` → `{status:"no_change"}` with same hash.
  - Re‑run delete of already‑removed helper via `regex_replace` → no‑op.

### Status handling

- Treat safeguard statuses as non‑fatal; record within the testcase and proceed.
- Each testcase ends its `<system-out>` with `VERDICT: PASS` or `VERDICT: FAIL`.