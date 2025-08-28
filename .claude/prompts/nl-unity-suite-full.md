# Unity NL/T Editing Suite — CI Agent Contract

You are running inside CI for the `unity-mcp` repo. Use only the tools allowed by the workflow. Work autonomously; do not prompt the user. Do NOT spawn subagents.

**Print this once, verbatim, early in the run:**
AllowedTools: Write,Bash(printf:*),Bash(echo:*),Bash(scripts/nlt-revert.sh:*),mcp__unity__manage_editor,mcp__unity__list_resources,mcp__unity__read_resource,mcp__unity__apply_text_edits,mcp__unity__script_apply_edits,mcp__unity__validate_script,mcp__unity__find_in_file,mcp__unity__read_console

---

## Mission
1) Pick target file (prefer):
   - `unity://path/Assets/Scripts/LongUnityScriptClaudeTest.cs`
2) Execute **all** NL/T tests in order using minimal, precise edits.
3) Validate each edit with `mcp__unity__validate_script(level:"standard")`.
4) **Report**: write one `<testcase>` XML fragment per test to `reports/<TESTID>_results.xml`. Do **not** read or edit `$JUNIT_OUT`.
5) **Restore** the file after each test using the OS‑level helper (fast), not a full‑file text write.

---

## Environment & Paths (CI)
- Always pass: `project_root: "TestProjects/UnityMCPTests"` and `ctx: {}` on list/read/edit/validate.
- **Canonical URIs only**:
  - Primary: `unity://path/Assets/...` (never embed `project_root` in the URI)
  - Relative (when supported): `Assets/...`
- File paths for the helper script are workspace‑relative:
  - `TestProjects/UnityMCPTests/Assets/...`

CI provides:
- `$JUNIT_OUT=reports/junit-nl-suite.xml` (pre‑created; leave alone)
- `$MD_OUT=reports/junit-nl-suite.md` (synthesized from JUnit)
- Helper script: `scripts/nlt-revert.sh` (snapshot/restore)

---

## Tool Mapping
- **Anchors/regex/structured**: `mcp__unity__script_apply_edits`
- **Precise ranges / atomic batch**: `mcp__unity__apply_text_edits` (non‑overlapping ranges)
- **Validation**: `mcp__unity__validate_script(level:"standard")`
- **Reporting**: `Write` small XML fragments to `reports/*_results.xml`
- **Snapshot/Restore**: `Bash(scripts/nlt-revert.sh:*)`
 - Never call generic Bash like `mkdir`; the revert helper creates needed directories. Use only `scripts/nlt-revert.sh` for snapshot/restore.

> Don’t use `mcp__unity__create_script`. Avoid the header/`using` region entirely.

---

## Output Rules (JUnit fragments only)
- For each test, create **one** file: `reports/<TESTID>_results.xml` containing exactly a single `<testcase ...> ... </testcase>`.
- Put human‑readable lines (PLAN/PROGRESS/evidence) **inside** `<system-out><![CDATA[ ... ]]></system-out>`.
- Evidence windows only (±20–40 lines). If showing a unified diff, cap at 100 lines and note truncation.
- **Never** open/patch `$JUNIT_OUT` or `$MD_OUT`; CI merges fragments and synthesizes Markdown.

**Example fragment**
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

```

Note: Emit the PLAN line only in NL‑0 (do not repeat it for later tests).


### Fast Restore Strategy (OS‑level)

- Snapshot once at NL‑0, then restore after each test via the helper.
- Snapshot (once after confirming the target):
  ```bash
  scripts/nlt-revert.sh snapshot "TestProjects/UnityMCPTests/Assets/Scripts/LongUnityScriptClaudeTest.cs" "reports/_snapshots/LongUnityScriptClaudeTest.cs.baseline"
  ```
- Log `snapshot_sha=...` printed by the script.
- Restore (after each mutating test):
  ```bash
  scripts/nlt-revert.sh restore "TestProjects/UnityMCPTests/Assets/Scripts/LongUnityScriptClaudeTest.cs" "reports/_snapshots/LongUnityScriptClaudeTest.cs.baseline"
  ```
- Then `read_resource` to confirm and (optionally) `validate_script(level:"standard")`.
- If the helper fails: fall back once to a guarded full‑file restore using the baseline bytes; then continue.

### Guarded Write Pattern (for edits, not restores)

- Before any mutation: `buf = read_text(uri)`; `pre_sha = sha256(read_bytes(uri))`.
- Write with `precondition_sha256 = pre_sha`.
- On `{status:"stale_file"}`:
  - Retry once using the server hash (`data.current_sha256` or `data.expected_sha256`).
  - If absent, one re‑read then a final retry. No loops.
- After success: immediately re‑read raw bytes and set `pre_sha = sha256(read_bytes(uri))` before any further edits in the same test.
- Prefer anchors (`script_apply_edits`) for end‑of‑class / above‑method insertions. Keep edits inside method bodies. Avoid header/using.

### Execution Order (fixed)

- Run exactly: NL-0, NL-1, NL-2, NL-3, NL-4, T-A, T-B, T-C, T-D, T-E, T-F, T-G, T-H, T-I, T-J (15 total).
- NL‑0 must include the PLAN line (len=15).
- After each testcase, include `PROGRESS: <k>/15 completed`.

### Test Specs (concise)

- NL‑0. Sanity reads — Tail ~120; ±40 around `Update()`. Then snapshot via helper.
- NL‑1. Replace/insert/delete — `HasTarget → return currentTarget != null;`; insert `PrintSeries()` after `GetCurrentTarget` logging "1,2,3"; verify; delete `PrintSeries()`; restore.
- NL‑2. Anchor comment — Insert `// Build marker OK` above `public void Update(...)`; restore.
- NL‑3. End‑of‑class — Insert `// Tail test A/B/C` (3 lines) before final brace; restore.
- NL‑4. Compile trigger — Record INFO only.

- T‑A. Anchor insert (text path) — Insert helper after `GetCurrentTarget`; verify; delete via `regex_replace`; restore.
- T‑B. Replace body — Single `replace_range` inside `HasTarget`; restore.
- T‑C. Header/region preservation — Edit interior of `ApplyBlend`; preserve signature/docs/regions; restore.
- T‑D. End‑of‑class (anchor) — Insert helper before final brace; remove; restore.
- T‑E. Lifecycle — Insert → update → delete via regex; restore.
- T‑F. Atomic batch — One call: two small `replace_range` + one end‑of‑class comment; all‑or‑nothing; restore.
- T‑G. Path normalization — Same edit with `unity://path/Assets/...` then `Assets/...`; second returns `{status:"no_change"}`.
- T‑H. Validation — `standard` after edits; `basic` only for transient checks.
- T‑I. Failure surfaces — Record INFO on `{too_large}`, `{stale_file}`, overlap rejection, validation failure, `{using_guard}`.
- T‑J. Idempotency — Repeat `replace_range` → `{status:"no_change"}`; repeat delete → no‑op.

### Status & Reporting

- Safeguard statuses are non‑fatal; record and continue.
- End each testcase `<system-out>` with `VERDICT: PASS` or `VERDICT: FAIL`.