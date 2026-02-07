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

## Preflight Checks (each run)

1. Confirm server process is not already running on port 8080.
2. Confirm Unity MCP window is open and HTTP Local mode is selected.
3. Confirm Unity URL value is exactly `http://localhost:8080`.

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
3. Start server (IPv4-only bind):
```powershell
cd Server
uv run src/main.py --transport http --http-host 127.0.0.1 --http-port 8080
```
4. Verify forced mismatch:
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
3. Start server (IPv6-only bind):
```powershell
cd Server
uv run src/main.py --transport http --http-host ::1 --http-port 8080
```
4. Verify forced mismatch:
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

### Why this is deterministic
Server is intentionally unavailable during initial resume attempts, then restored before retry schedule ends.

### Setup
1. Return hosts to normal dual-stack entries.
2. Start server on localhost/8080 and establish HTTP session.

### Action
1. Stop server.
2. Trigger domain reload by editing + saving any C# file.
3. Restart server after ~15-20 seconds.

### Expected outcome
- **Before**: often stuck in `No Session` until manual reconnect.
- **After**: auto-resume succeeds within retry window, with logs like:
  - `[HTTP Reload] Resume attempt X/6`
  - `[HTTP Reload] Resume succeeded on attempt X`

## Test B2: Boundary behavior (non-overclaim guard)

### Why this is deterministic
Confirms current implementation has finite retry budget.

### Action
Repeat B1 but restart server after >50 seconds.

### Expected outcome
- **After**: auto-resume may not recover; manual reconnect required.
- This confirms mitigation limits and prevents overclaiming.

## Evidence Checklist
For each test case capture:

1. Tested revision (`before` or `after`)
2. Hosts-file mode used
3. Exact server launch command
4. `Test-NetConnection` outputs
5. Unity logs containing fallback/resume markers
6. Final state: connected vs no session

## Pass/Fail Criteria

- A1 and A2 are **pass** only if:
  - before fails (or remains disconnected), and
  - after succeeds through fallback under forced family mismatch.

- B1 is **pass** only if:
  - before does not reliably self-recover, and
  - after self-recovers without manual reconnect.

- B2 is **pass** only if:
  - behavior demonstrates finite retry window and no false claim of infinite retry.

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

