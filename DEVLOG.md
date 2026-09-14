# Development Log — Network Security Toolkit

> Write an entry after every work session (the entry itself should take 5-10 min max).  
> Purpose: (1) source material for the thesis, (2) documentation for GitHub/LinkedIn, (3) a personal reminder of why something was done a certain way.

---

## How to fill this out

- **Date**: YYYY-MM-DD
- **Branch**: name of the feature branch you worked on
- **Work done**: 2-4 concrete bullets (not "worked on the scanner" but "added async TCP scan with SemaphoreSlim throttling")
- **Why / decisions**: brief rationale for choices made, alternatives considered and rejected
- **Problems & solutions**: what got you stuck and how you got past it (this is gold for the thesis — "problem analysis")
- **Next**: what's planned next, so you don't have to remember at the next session
- **Screenshots/evidence**: link to an image in `/docs/images/` if there's a visual result

Write it while it's fresh — don't leave it for "later", you'll forget details and decisions.

---

## 2026-09-14 — TCP Port Scanner (socket-based) + DNS optimization

**Branches:** `feature/port-scanner-core`, `feature/scanner-dns-resolve-once`
**Issues/PRs:** #13/#14, #15/#16

**Work done:**
- Implemented `IPortScanner` interface + `TcpPortScanner` (Strategy pattern)
- Switched from `TcpClient` to a raw `Socket` for scanning
- Throttling via `Parallel.ForEachAsync` (`MaxDegreeOfParallelism`)
- Per-port timeout via a linked `CancellationTokenSource` (timeout + caller's token)
- DNS resolved once per scan in `ScanAsync`, instead of once per port
- Removed an overly broad `catch (Exception)`; only `SocketException` and `OperationCanceledException` are now handled explicitly

**Why / decisions:**
- **Socket over TcpClient**: direct access to `SocketErrorCode` distinguishes `Closed` (connection refused) from `Filtered` (timeout/other); `socket.Close(0)` sends RST instead of a graceful close, avoiding TIME_WAIT buildup when scanning many ports quickly; Socket also has lower allocation overhead per connection than TcpClient, which internally wraps a Socket plus its own object layer
- **DNS resolved once, not per port**: avoids N redundant DNS lookups when scanning N ports on the same host; side benefit — since `AddressFamily` is taken from the resolved `IPAddress`, the scanner now works correctly against both IPv4 and IPv6 hosts
- **Removed generic catch**: swallowing all exceptions as `Filtered` risked hiding real bugs (e.g. `ObjectDisposedException`); narrowing to expected exception types makes failures visible instead of silently misreported as a scan result

**Problems & solutions:**
- Problem: initial `TcpClient`-based implementation leaked resources on timeout because the `CancellationTokenSource` wasn't passed into `ConnectAsync`
- Solution: linked `CancellationTokenSource` (timeout token + caller's token) passed directly into `ConnectAsync`/`socket.ConnectAsync`

**Next:** unit/integration tests for `TcpPortScanner`

---

## 2026-XX-XX — [next entry title]

**Branch:**

**Work done:**
-

**Why / decisions:**
-

**Problems & solutions:**
-

**Next:**
-

**Screenshots/evidence:**

---

<!-- Add new entries above this line, newest on top -->