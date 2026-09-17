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

## 2026-09-16 — Async Job Pattern Web API via Channels & BackgroundService

**Branch:** `feature/scan-job-api`  
**Issues/PRs:** #21/#27 

**Work done:**
- Implemented the **Async Job Pattern** endpoints (`POST /api/scan/tcp` and
  `GET /api/scan/{id}/status`) to expose the TCP scanner via the Web API.
- Created an immutable `ScanJob` model (`sealed record` with `init`-only
  properties throughout, including `CreatedAt`) with `DateTimeOffset` for
  precise, time-zone-aware timestamps.
- Built a thread-safe `InMemoryScanJobStore` using `ConcurrentDictionary<Guid, ScanJob>`
  for state management (Repository pattern — swappable for a SQLite/EF Core
  implementation in Phase 2 without touching the controller).
- Replaced the initial `Task.Run` fire-and-forget design with a
  **producer-consumer architecture**: a FIFO queue (`IScanJobQueue` /
  `ChannelScanJobQueue`) backed by `System.Threading.Channels` decouples job
  submission from processing, and a hosted `BackgroundService`
  (`ScanBackgroundWorker`) drains it sequentially — avoiding uncontrolled
  thread-per-request spawning from the earlier `Task.Run` approach.
- Added a full suite of xUnit lifecycle unit tests (`ScanJobLifecycleTests`)
  covering state machine transitions (`Pending` → `Running` → `Completed`/`Failed`)
  and not-found handling.
- Added `StartPort <= EndPort` validation in the controller and configured
  `JsonStringEnumConverter` so `ScanJobStatus` serializes as a string, not a
  numeric value, in API responses.

**Why / decisions:**
- **BackgroundService + Channels over Task.Run**: `Task.Run` per request risks
  thread pool starvation and unthrottled concurrent scans under load.
  `System.Threading.Channels` (`SingleReader = true`, `SingleWriter = false`,
  since multiple HTTP request threads can enqueue concurrently) decouples the
  HTTP request from execution — a bounded, well-understood queue that a single
  worker drains sequentially.
- **Programming to `IScanJobStore` / `IScanJobQueue`**: both are Repository-pattern
  abstractions. The controller and worker only know the interface; today's
  backing implementation is in-memory, but swapping to SQLite/EF Core in
  Phase 2 is a single-line DI registration change in `Program.cs`.
- **`sealed` on new implementation classes**: enables the JIT to devirtualize
  and inline calls when it can prove no derived type exists. Individually
  small, but the .NET team seals nearly every internal class for this exact
  reason — see Stephen Toub, ["Performance Improvements in .NET 6" — "Peanut Butter"](https://devblogs.microsoft.com/dotnet/performance-improvements-in-net-6/#peanut-butter).
- **`ValueTask` pass-through in `ChannelScanJobQueue`**: `EnqueueAsync`/`DequeueAsync`
  return the channel's `ValueTask` directly rather than wrapping it in
  `async`/`await`, avoiding an unnecessary compiler-generated state machine
  for a simple pass-through operation.

**Problems & solutions:**
- *Problem (job stuck at `Running` forever on scan failure)*: in the first
  draft of `ScanBackgroundWorker`, an exception thrown by `_portScanner.ScanAsync`
  (e.g. a `SocketException` from an unresolvable host — already known to
  happen from the 2026-09-14 entry) was only logged by the outer catch block;
  `_jobStore.MarkFailed` was never called. The job silently stayed `Running`
  indefinitely, and a client polling `GET /api/scan/{id}/status` never got a
  definitive answer.
  * *Solution*: wrapped the scan call in its own inner `try/catch`, scoped
    specifically around `ScanAsync` → `MarkCompleted`, so any exception there
    (other than `OperationCanceledException`, which signals shutdown) transitions
    the job to `Failed` with the error message. The outer catch remains as a
    safety net for unexpected failures elsewhere in the loop (e.g. the store
    or logger itself failing).
- *Dilemma (record value-equality vs. optimistic concurrency)*: `ScanJob` is a
  `record`, which uses value-based `Equals`. An external CAS loop
  (`while(true)` + `TryUpdate`) built on that equality is theoretically
  susceptible to the ABA problem if state ever moved backward. Since the job
  state machine is strictly monotonic (`Pending → Running → Completed/Failed`,
  never reversed) this wasn't an exploitable bug in practice, but replaced the
  manual loop with `ConcurrentDictionary.AddOrUpdate`, which performs the
  read-transform-write atomically at the bucket level — shorter code and
  removes the theoretical risk entirely.
- *Problem (flaky time assertions in tests)*: `Assert.True(createdJob.CreatedAt
  <= DateTimeOffset.UtcNow)` intermittently failed due to clock resolution
  under tight timing.
  * *Solution*: replaced direct relational comparisons with `Assert.NotEqual(default, ...)`
    plus a delta check (`TotalSeconds < 5`) to decouple the test from exact
    OS clock alignment.
- *Problem (`CS8858` compiler error)*: `ScanJob` was accidentally declared as
  a plain `class` in an early draft, breaking the `with` expression.
  * *Solution*: changed to `sealed record`.

**Next:**
- Merge `feature/scan-job-api` into `main`.
- **#25 — Harden TCP scan API against resource abuse**: port-count limits,
  concurrent job limits, private/loopback target restriction, structured
  logging for scan-related events.
- **#26 — Refactor: seal internal and test classes for performance
  optimization ("Peanut Butter" effect)**: apply `sealed` consistently across
  `Core` and `Tests` where inheritance isn't needed.
- SQLite/EF Core persistence and ASP.NET Identity remain Phase 2.

---

## 2026-09-17 — Complete sealed-class audit across Core/Tests

**Branch:** `refactor/seal-classes`  
**Issues/PRs:** #26/#28  

**Work done:**
- Audited all classes/records in `Core`, `Tests`, and `WebApp` for the
  `sealed` modifier, following up on the pattern started in #21/#27.
- Sealed `TcpPortScannerTests` and `TcpPortScannerIntegrationTests`.
- Sealed the `PortScanResult` record.

**Why / decisions:**
- Sealing lets the JIT devirtualize and inline calls when it can prove no
  derived type exists — individually small per call site, but the .NET team
  applies it broadly across the runtime for exactly this reason (Stephen
  Toub, ["Performance Improvements in .NET 6" — "Peanut Butter"](https://devblogs.microsoft.com/dotnet/performance-improvements-in-net-6/#peanut-butter)).
- Interfaces (`IPortScanner`, `IScanJobQueue`, `IScanJobStore`) were left
  as-is — can't be sealed. Enums (`PortState`, `ScanJobStatus`) were left
  as-is — implicitly non-inheritable in C# already, so the modifier doesn't
  apply.

**Problems & solutions:**
- *Finding*: `PortScanResult` (from the original Phase 1 scanner work) was
  declared as a plain `record`, not `sealed record`. C# records are **not**
  sealed by default — a common misconception — so this predates the sealing
  convention established later in the `ScanJob` design. Fixed by adding
  `sealed` explicitly.

**Next:** Issue #25 (harden scan API against resource abuse) — port count
limits, concurrent job cap, target restriction, structured logging.

---
<!-- Add new entries above this line, newest on top -->