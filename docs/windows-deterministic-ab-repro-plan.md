# Windows Deterministic A/B Repro Plan (PR #688 Follow-up Validation)

## Goal
Provide deterministic, repeatable Windows tests that compare:

- **Before**: commit `8c6cefdd` (baseline prior to localhost/reload hardening)
- **After**: branch `fix/localhost-ipv6-resolution`

This plan validates the fixes for:

1. Localhost IPv4/IPv6 connection ambiguity (WebSocket fallback candidates)
2. HTTP domain-reload resume reliability within retry window

This plan does **not** claim to validate a dedicated WinError 64 event-loop policy fix, because that mitigation is not part of this branch.

## Controlled Test Environment
Keep these constant for A/B runs:

- Same Windows machine
- Same Unity version
- Same project checkout
- Same Claude Code extension version
- Unity HTTP URL field set to: `http://localhost:8080`

Recommended log capture:

- Unity Console logs exported to file
- Server terminal output saved per run

## Branch/Commit Under Test

### Before
```bash
git checkout 8c6cefdd
```

### After
```bash
git checkout fix/localhost-ipv6-resolution
```

**Important:** Close and reopen Unity after each checkout. `HttpBridgeReloadHandler` is
registered via `[InitializeOnLoad]`, and `EditorPrefs` state from a prior run can carry
over if Unity stays open across checkouts. A fresh launch ensures the editor loads the
correct version of every assembly with no stale static state.

## Pre-Test Cleanup (before each run)

Run these before every test to eliminate stale state from a previous run:

1. **Kill stale server processes on port 8080:**
```powershell
$proc = Get-NetTCPConnection -LocalPort 8080 -ErrorAction SilentlyContinue |
        Select-Object -ExpandProperty OwningProcess -Unique
if ($proc) { Stop-Process -Id $proc -Force; Write-Host "Killed PID(s): $proc" }
else { Write-Host "Port 8080 clear" }
```
2. **Verify hosts file matches the intended test configuration:**
```powershell
Get-Content C:\Windows\System32\drivers\etc\hosts | Select-String "localhost"
```
   Compare the output against the hosts-file mode required for the current test
   (dual-stack for B tests, single-family for A tests). Fix before proceeding if
   it doesn't match.
3. **Flush DNS** (always, even if hosts file looks correct):
```powershell
ipconfig /flushdns
```

## Preflight Checks (each run)

1. Confirm Unity MCP window is open and HTTP Local mode is selected.
2. Confirm Unity URL value is exactly `http://localhost:8080`.
3. Enable MCP Debug logging in Unity (Advanced Settings). The `[HTTP Reload]` retry
   logs and `[WebSocket] Connect failed` logs are emitted at Debug level and will not
   appear without this.

## Test A1: Forced localhost -> IPv6 resolution, server bound IPv4 only

### Why this is deterministic
By forcing `localhost` to resolve to `::1` while server binds only `127.0.0.1`, initial connect path must fail unless fallback logic works.

### Setup
1. Edit `C:\Windows\System32\drivers\etc\hosts` as Administrator:
   - Keep `::1 localhost`
   - Remove/comment `127.0.0.1 localhost`
2. Flush DNS:
```powershell
ipconfig /flushdns
```
3. Verify .NET resolves `localhost` to only IPv6:
```powershell
[System.Net.Dns]::GetHostAddresses("localhost") | ForEach-Object { $_.IPAddressToString }
```
   Expected output: `::1` only. If `127.0.0.1` also appears, the hosts file change
   did not take effect — do not proceed. This can happen on some Windows 10/11
   configurations where the DNS Client service resolves `localhost` independently of
   the hosts file. Troubleshoot before continuing (disable "Smart Multi-Homed Name
   Resolution" or restart the DNS Client service).
4. Start server (IPv4-only bind):
```powershell
cd Server
uv run src/main.py --transport http --http-host 127.0.0.1 --http-port 8080
```
5. Verify forced mismatch:
```powershell
Test-NetConnection ::1 -Port 8080
Test-NetConnection 127.0.0.1 -Port 8080
```
Expected:
- `::1:8080` fails
- `127.0.0.1:8080` succeeds

### Action
Start HTTP session in Unity.

### Expected outcome
- **Before** (`8c6cefdd`): session connect fails/stalls; no reliable auto-recovery.
- **After** (`fix/localhost-ipv6-resolution`): fallback succeeds and Unity logs include:
  - `Connect failed for ws://localhost...`
  - `Connected via fallback host '127.0.0.1' after 'localhost' failed.`

## Test A2: Forced localhost -> IPv4 resolution, server bound IPv6 only

### Prerequisite
IPv6 must be enabled on the test machine. Verify with:
```powershell
Get-NetAdapterBinding -ComponentId ms_tcpip6 | Where-Object { $_.Enabled }
```
If no adapters show IPv6 enabled, skip this test — the server cannot bind to `::1`.

### Why this is deterministic
Mirror of A1. Initial connect goes to wrong family; fallback must recover.

### Setup
1. Edit hosts:
   - Keep `127.0.0.1 localhost`
   - Remove/comment `::1 localhost`
2. Flush DNS:
```powershell
ipconfig /flushdns
```
3. Verify .NET resolves `localhost` to only IPv4:
```powershell
[System.Net.Dns]::GetHostAddresses("localhost") | ForEach-Object { $_.IPAddressToString }
```
   Expected output: `127.0.0.1` only. If `::1` also appears, stop and troubleshoot
   (same guidance as A1 step 3).
4. Start server (IPv6-only bind):
```powershell
cd Server
uv run src/main.py --transport http --http-host ::1 --http-port 8080
```
5. Verify forced mismatch:
```powershell
Test-NetConnection 127.0.0.1 -Port 8080
Test-NetConnection ::1 -Port 8080
```
Expected:
- `127.0.0.1:8080` fails
- `::1:8080` succeeds

### Action
Start HTTP session in Unity.

### Expected outcome
- **Before**: fails or remains disconnected.
- **After**: fallback connects via `::1`.

## Test B1: Deterministic domain-reload recovery inside retry window

### What changed between before and after
The baseline (`8c6cefdd`) already has `HttpBridgeReloadHandler.cs`, but it makes a
**single fire-and-forget resume attempt** immediately after reload — no retries. The
branch replaces this with a 6-attempt retry schedule `[0, 1, 3, 5, 10, 30]` seconds
(cumulative: attempts fire at ~0, 1, 4, 9, 19, 49 seconds after reload completes).

The baseline also uses async `StopAsync` in `OnBeforeAssemblyReload` (fire-and-forget,
may leave an orphaned socket), while the branch uses synchronous `ForceStop` for clean
teardown before reload.

### Why this is deterministic
The server is stopped **before** triggering reload. By the time `OnAfterAssemblyReload`
fires, the server has been down for the entire compilation window. The baseline's single
immediate attempt is guaranteed to fail against a stopped server, with no retry to catch
a later restart. The branch's retry schedule spans ~49 seconds, giving a wide window to
restart the server and have a subsequent attempt succeed.

### Setup
1. Return hosts to normal dual-stack entries.
2. Start server on localhost/8080 and establish HTTP session.
3. Verify connected state in MCP for Unity window.

### Action
1. **Stop server** (Ctrl+C in the server terminal).
2. **Start a stopwatch** (or note the time).
3. **Trigger domain reload**: edit and save any C# file in the Unity project.
4. **Wait for reload to complete**: watch Unity's status bar for the compile spinner
   to finish, and the Console for reload-related log output.
5. **Checkpoint — verify the baseline attempt has fired**:
   - **Before**: look for `Failed to resume HTTP MCP bridge after domain reload` in
     the Unity Console (this is the single-shot failure at Warn level, always visible).
   - **After**: look for `[HTTP Reload] Resume attempt 1/6` followed by a failure
     message (Debug level — requires step 4 of Preflight Checks).
6. **Restart server** once the checkpoint is confirmed (~15-30 seconds after stopping).
7. **Wait 30 seconds** after restarting and observe the MCP for Unity window.

### Expected outcome
- **Before** (`8c6cefdd`): Unity shows `No Session` / disconnected. The single resume
  attempt already fired and failed (confirmed at step 5). No further attempts occur.
  Manual reconnect is the only recovery path.
- **After** (`fix/localhost-ipv6-resolution`): a later retry attempt catches the
  restarted server. Unity logs show:
  - `[HTTP Reload] Resume attempt 1/6` ... failed
  - (possibly more failed attempts)
  - `[HTTP Reload] Resume succeeded on attempt X`
  - MCP for Unity window returns to Connected state without manual intervention.

### Why "before" cannot spuriously pass
The server is stopped before reload begins. Unity compilation takes several seconds.
The baseline's single `StartAsync` call fires immediately after `OnAfterAssemblyReload`
— at which point the server has been down for the entire compilation window. There is
no code path in the baseline that retries, so a later server restart cannot be detected.

## Test B2: Boundary — retry budget exhaustion (non-overclaim guard)

### Why this is deterministic
The branch's retry schedule has 6 attempts spanning ~49 seconds. By waiting until all
attempts have exhausted before restarting the server, we confirm the retry budget is
finite and the system does not falsely claim infinite recovery.

### Setup
Same as B1: normal dual-stack hosts, establish HTTP session, verify connected.

### Action
1. **Stop server**.
2. **Trigger domain reload** (edit + save C# file).
3. **Wait for reload to complete** and watch the Unity Console for retry attempts.
4. **Wait until all 6 attempts have fired and failed** (~50-60 seconds after reload
   completes). In the **after** branch, look for:
   - `[HTTP Reload] Resume attempt 6/6` followed by a failure
   - `Failed to resume HTTP MCP bridge after domain reload` (final Warn log)
5. **Restart server** only after step 4 is confirmed.
6. **Wait 30 seconds** and observe.

### Expected outcome
- **After**: Unity remains disconnected. All 6 retry attempts exhausted before the
  server came back. Manual reconnect is required.
- This confirms the retry window has a defined boundary and does not overclaim.

## Evidence Checklist
For each test case capture:

1. Tested revision (`before` or `after`)
2. Hosts-file mode used
3. Exact server launch command
4. `Test-NetConnection` outputs (A tests)
5. Unity logs containing fallback/resume markers:
   - A tests: `[WebSocket] Connect failed for ...` and `[WebSocket] Connected via fallback host ...`
   - B tests (before): `Failed to resume HTTP MCP bridge after domain reload`
   - B tests (after): `[HTTP Reload] Resume attempt X/6`, `[HTTP Reload] Resume succeeded on attempt X`,
     or `[HTTP Reload] Resume attempt 6/6` followed by final failure (B2)
6. B test checkpoint confirmation: log evidence that the baseline's single attempt
   (or all 6 branch attempts for B2) fired before server was restarted
7. Final state: connected vs no session

## Pass/Fail Criteria

- A1 and A2 are **pass** only if:
  - before fails (or remains disconnected), and
  - after succeeds through fallback under forced family mismatch.

- B1 is **pass** only if:
  - before: the single resume attempt fires and fails (confirmed via Warn log),
    Unity remains disconnected after server restart, and
  - after: retry schedule catches the restarted server, Unity auto-recovers
    without manual reconnect, and `Resume succeeded on attempt X` appears in logs.

- B2 is **pass** only if:
  - after: all 6 retry attempts fire and fail (confirmed via log),
    Unity remains disconnected after server restart, and
    manual reconnect is required to recover.

## Result Template

Copy this table for each full A/B run. Fill in one row per test execution.

| Test | Revision | Hosts Mode | Server Bind | Checkpoint Log (exact message) | Final State | Verdict |
|------|----------|------------|-------------|-------------------------------|-------------|---------|
| A1 | before (`8c6cefdd`) | `::1` only | `127.0.0.1` | | connected / disconnected | PASS / FAIL |
| A1 | after (branch) | `::1` only | `127.0.0.1` | | connected / disconnected | PASS / FAIL |
| A2 | before (`8c6cefdd`) | `127.0.0.1` only | `::1` | | connected / disconnected | PASS / FAIL |
| A2 | after (branch) | `127.0.0.1` only | `::1` | | connected / disconnected | PASS / FAIL |
| B1 | before (`8c6cefdd`) | dual-stack | default | | connected / disconnected | PASS / FAIL |
| B1 | after (branch) | dual-stack | default | | connected / disconnected | PASS / FAIL |
| B2 | after (branch) | dual-stack | default | | connected / disconnected | PASS / FAIL |

**Checkpoint Log column guidance:**
- A tests: paste the `[WebSocket] Connected via fallback host ...` line, or the final error if it failed.
- B1 before: paste `Failed to resume HTTP MCP bridge after domain reload`.
- B1 after: paste `[HTTP Reload] Resume succeeded on attempt X`.
- B2 after: paste `[HTTP Reload] Resume attempt 6/6` + final failure line.

**Overall result:** All rows must show expected verdicts for the plan to pass.
A single unexpected result requires investigation before the PR can be considered validated.

## Diagnostic: Why Does My Machine Work When Others Fail?

The IPv4/IPv6 localhost issue is **more common on newer Windows** (10 1803+, 11) where
IPv6 is aggressively preferred. It is not an old-Windows problem. The failure occurs when
`.NET`'s `ClientWebSocket.ConnectAsync` resolves `localhost` to `::1` (IPv6) first, but
the Python server is bound to `127.0.0.1` (IPv4) only. Unity's Mono runtime does not
implement Happy Eyeballs (try both address families) — it picks the first resolved
address and uses it.

If you cannot reproduce the failure natively, run these commands to understand why your
machine is shielded. Compare the output with a failing machine to identify the
differentiator.

### 1. What does .NET resolve `localhost` to (and in what order)?

This is the ground truth. The first address in the list is what `ClientWebSocket` will
connect to.
```powershell
[System.Net.Dns]::GetHostAddresses("localhost") | ForEach-Object { "$($_.AddressFamily): $($_.IPAddressToString)" }
```
- If `InterNetwork: 127.0.0.1` appears first → your machine will always connect to IPv4.
- If `InterNetworkV6: ::1` appears first → your machine hits the bug.

### 2. Hosts file entry order

The most common cause. If `127.0.0.1 localhost` is listed first (or `::1 localhost` is
absent/commented), IPv4 wins the resolution race.
```powershell
type C:\Windows\System32\drivers\etc\hosts | findstr localhost
```

### 3. IPv6 prefix policy table

Even with both hosts entries present, Windows applies RFC 6724 address sorting. By
default `::1/128` (IPv6 loopback) has precedence 50 and `::ffff:0:0/96` (IPv4-mapped)
has precedence 35 — meaning IPv6 wins when both are candidates.
```powershell
netsh interface ipv6 show prefixes
```

### 4. IPv6 disabled components registry key

Some machines have IPv6 partially or fully disabled at the registry level. A value of
`0x20` disables IPv6 prefix preference (forces IPv4 preferred). Gaming-oriented OEM
installs, Dell/Alienware network optimizer tools, and "network optimization" guides
commonly set this.
```powershell
reg query HKLM\SYSTEM\CurrentControlSet\Services\Tcpip6\Parameters /v DisabledComponents 2>$null
```
- Key absent or `0x0` → IPv6 fully enabled (default)
- `0x20` → IPv6 enabled but IPv4 preferred for address selection
- `0xff` → IPv6 fully disabled

### 5. IPv6 adapter status

Confirms whether IPv6 is enabled at the adapter level.
```powershell
Get-NetAdapterBinding -ComponentId ms_tcpip6 | Format-Table Name, Enabled
```

### Likely differentiators

Machines that **work** (never see the failure) typically have one or more of:
- `127.0.0.1 localhost` listed first or exclusively in the hosts file
- `DisabledComponents` registry key set to `0x20` or higher (OEM/gaming tweaks)
- Killer networking or similar drivers that deprioritize IPv6

Machines that **fail** typically have:
- Default/clean Windows 10 1803+ or Windows 11 install
- Both `::1 localhost` and `127.0.0.1 localhost` in hosts with `::1` first, or no
  hosts entries for localhost (Windows resolves to both, prefers IPv6)
- No registry tweaks — default prefix policy prefers IPv6 loopback

## Safety/Reset
After testing, restore default hosts entries:

```text
127.0.0.1 localhost
::1 localhost
```

Then run:
```powershell
ipconfig /flushdns
```

