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

## 2026-09-15 — Add unit/integration tests for TcpPortScanner

**Branch:** feature/scanner-tests  
**Issues/PRs:** #18/#19

**Work done:**
- Implemented xUnit async unit tests for TcpPortScanner targeting unresolvable hosts and port sorting logic.
- Created a local integration test (TcpPortScannerIntegrationTests) utilizing raw sockets against a target IP address to verify the detection of various port states.
- Refactored the core ScanAsync method by removing an unreachable array-length condition (if (addresses.Length == 0)).
- Added .gitignore input for future local integration test config files.

**Why / decisions:**
- Refactoring Choice: Opted to completely remove the custom ArgumentException validation block after discovering that .NET's underlying Dns.GetHostAddressesAsync always throws a SocketException on unresolvable hosts rather than returning an empty array. This simplified the code and aligned it with native .NET network stack behavior.
- Testing Strategy: Chose to use raw OS network interfaces (127.0.0.1 and manually against the VirtualBox-bridged Kali VM via a Skip-marked test, not run in CI) for integration test validation instead of heavy mocking. For a low-level network utility like a port scanner, testing actual socket responses ensures true-to-life behavior.

**Problems & solutions:**
- Problem (Flaky Tests): The integration test for closed ports randomly failed with Xunit.Sdk.EqualException, expecting PortState.Closed but intermittently getting PortState.Filtered or PortState.Open.
- Analysis: The Actual: Open anomaly occurred because the OS dynamically allocated the hardcoded guess port (openPort + 1) to another active process. The Actual: Filtered failure was triggered by a race condition where a strict 200ms timeout expired before the OS could reply under high CPU load, or due to Windows Defender Firewall silently dropping the SYN packets (Stealth Mode).
- Solution:
  - Introduced a temporary socket binding pattern (tempSocket.Bind(..., 0)) to let the OS explicitly assign a guaranteed unused port, which was closed immediately prior to the scan.
  - Increased the test scanner timeout to TimeSpan.FromSeconds(1).
  - Relaxed the assertion using Assert.True to accept both Closed and Filtered states as valid non-open responses under unpredictable OS firewall rules.

**Next:**
- Merge the completed feature/scanner-tests branch into main.
- Update the GitHub Actions workflow (CI.yml) to introduce explicit test filtering via --filter "Category!=Integration" to elegantly isolate the Kali VM test from the cloud runner without using rigid code-level skips.

---

<!-- Add new entries above this line, newest on top -->