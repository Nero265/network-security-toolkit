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

## 2026-09-20 — Scan API guardrails (#25)

**Branch:** `feature/scan-port-limit`  
**Issues/PRs:** #25/#32  

**Work done:**
- `MaxPortsPerScan` guardrail: `ScanApiOptions` (`WebApp/Options/`) bound via `IOptions<T>`, checked in `ScanController` before job creation, rejects with `400` when the requested port range exceeds the configured maximum.
- `MaxActiveJobs` guardrail: added `IScanJobStore.CountActive()`, backed by an `Interlocked`-managed counter in `InMemoryScanJobStore` (incremented in `Create`, decremented in `MarkCompleted`/`MarkFailed`). `ScanController` rejects new requests with `503 Service Unavailable` once the active count reaches the configured limit.
- Target restriction: new `PrivateNetworkRanges` (`Core/Network/`) checks a resolved `IPAddress` against loopback, RFC 1918 private, link-local, and IPv6 unique-local/link-local ranges using `System.Net.IPNetwork`. `ScanController` resolves the request host via DNS, rejects public targets with `403 Forbidden` unless `AllowPublicTargets` is explicitly enabled in config.
- Structured logging: added `ILogger<ScanController>` to log job creation (`Information`) and all three rejection paths — port-count, active-jobs, target-restriction, including unresolvable-host (`Warning`) — using message templates throughout, not string interpolation. `ScanBackgroundWorker`'s existing `started`/`completed`/`failed` logging was left untouched (already correct).
- Test coverage: `ScanControllerTests` extended with accept/reject cases for all three guardrails plus an `AllowPublicTargets: true` override case; new `PrivateNetworkRangesTests` using `[Theory]`/`[InlineData]` for boundary-value coverage of each CIDR range; one `Mock<ILogger<T>>` + `Verify` test confirming the target-restriction rejection is logged at `Warning` level.

**Why / decisions:**
- **`Interlocked` counter over LINQ scan for `CountActive()`**: a `_jobs.Values.Count(...)` scan over `ConcurrentDictionary` is only weakly consistent under concurrent mutation (no snapshot guarantee), and gets more expensive over time since completed/failed jobs are never pruned. An `Interlocked`-managed field is atomic, O(1), and — for a capacity guardrail rather than a billing-critical counter — more than accurate enough.
- **Redundant DNS resolve accepted in `ScanController`**: rather than resolving once and threading the `IPAddress` through `ScanJob`/the queue into `TcpPortScanner`, the controller does its own resolve for the target-restriction check. Threading a pre-resolved address through would require a larger model/signature change, and would introduce a TTL/round-robin mismatch risk between the address validated at request time and the address actually scanned later once the job is dequeued — a resolve-twice approach is simpler and safer here, at the cost of one extra (cheap, likely OS-cached) DNS call per request.
- **`403` over `Failed` job status for target-restriction rejection**: rejected hosts never become a `ScanJob` at all — the check happens before `_jobStore.Create(...)`, consistent with how port-count and active-jobs rejections already work. `403 Forbidden` was chosen over `400`/`503` because it's semantically "request understood, not permitted" rather than "malformed" or "server busy".
- **`AllowPublicTargets` as a plain `bool`, not a configurable CIDR allow-list**: the issue asked for a default-restricted, configurable toggle, not a general-purpose policy engine — a bool is sufficient scope for a demo/portfolio project and can be extended non-destructively later if ever needed.
- **Link-local (`169.254.0.0/16`, `fe80::/10`) treated as allowed**, alongside loopback and RFC 1918 private ranges — a deliberate scope decision, not an oversight.

**Problems & solutions:**
- *Problem (dependency-injection breaking change)*: adding `ILogger<ScanController>` as a 4th constructor parameter broke every existing `ScanController` test.
  - *Solution*: added a `_logger` field defaulted to `NullLogger<ScanController>.Instance` at the class level, so most tests don't need to pass a logger explicitly; the one test verifying logging behavior passes a real `Mock<ILogger<ScanController>>` instead.
- *Problem (Moq + `ILogger` verification is awkward)*: `ILogger.Log<TState>` is generic, so a normal `It.IsAny<TState>()` setup doesn't compile against a `Mock<ILogger<T>>`.
  - *Solution*: used Moq's `It.IsAnyType` wildcard with a `(state, _) => state.ToString()!.Contains(...)` predicate to assert on the formatted message content, since asserting on individual structured fields isn't directly accessible through the mock without a custom `ILogger` capture implementation.
- *Off-by-one caught before merge*: initial port-count check used `portCount >= MaxPortsPerScan` instead of `>`, which would have rejected a scan of exactly `MaxPortsPerScan` ports — caught via test review, not a failing test, since no boundary test existed yet at that point.

**Found, not fixed (filed separately):**
- `TcpPortScanner.ScanPortAsync` hardcodes `AddressFamily.InterNetwork` for the socket while `ScanAsync` picks `addresses[0]` from DNS resolution without filtering by family — an IPv6-first resolution would silently break scanning (likely returning all-`Filtered` instead of a clear error). Filed as its own issue; not part of #25's scope.

**Next:**
- Merge `feature/scan-port-limit` into `main` via PR #32 (`Closes #25`).
- Separate follow-up issue: add `Location` header to the `202 Accepted` response on `POST /api/scan/tcp` (scoped earlier, not yet started).
- Separate follow-up issue: fix hardcoded `AddressFamily.InterNetwork` in `TcpPortScanner`.
- Phase 1 still open: UDP scan (`UdpPortScanner`, Strategy pattern already in place from `TcpPortScanner`/`IPortScanner`) is the remaining item before Phase 1 is considered complete.
---

## 2026-09-21 — Fix IPv6 address family mismatch in TcpPortScanner

**Branch:** `fix/scanner-ipv6-address-family`  
**Issues/PRs:** #31/#33  

**Work done:**
- Fixed `ScanPortAsync` hardcoding `AddressFamily.InterNetwork` for the socket regardless of the resolved address's actual family.
- Socket is now created using `ipAddress.AddressFamily`, taken from the same resolved `IPAddress` already flowing through `ScanAsync`.
- Added `ScanAsync_WhenHostResolvesToIPv6_DoesNotThrowAddressFamilyMismatch`, scanning `"::1"` directly (literal IPv6 loopback, no real DNS lookup) to confirm the scanner no longer breaks on IPv6 targets.

**Why / decisions:**
- **`ipAddress.AddressFamily` over a hardcoded constant**: the resolved address already carries its own family — reading it off the address itself removes the implicit IPv4-only assumption without changing `ScanAsync`'s address-selection logic (`addresses[0]`) or introducing dual-stack scanning, which is out of scope here.
- **Scope kept to the socket family bug only**: `addresses[0]`'s non-deterministic DNS-order selection is a separate, pre-existing behavior — not touched, consistent with keeping this fix small and reviewable.
- **Test via public `ScanAsync` API, not `internal`/`InternalsVisibleTo`**: avoided widening `ScanPortAsync`'s visibility just for test access; `"::1"` as a host string exercises the real fixed code path through `Dns.GetHostAddressesAsync` (which parses the literal without a real network round-trip) while staying a true unit test — no Kali VM or `Category=Integration` marker needed.

**Problems & solutions:**
- *Background*: bug was identified during #25 (2026-09-20 entry) — `ScanPortAsync` hardcoded `AddressFamily.InterNetwork` while `ScanAsync` picked `addresses[0]` without filtering by family, meaning an IPv6-first DNS resolution would silently produce all-`Filtered` results instead of a clear error. Deliberately filed separately at the time to keep #25's scope to the API guardrails.

**Next:**
- Merge `fix/scanner-ipv6-address-family` into `main` via PR #<broj> (`Closes #<broj>`).
- Phase 1 still open: `UdpPortScanner` remains the last item before Phase 1 is considered complete.

---

## 2026-09-26 — Location header on scan job creation (#30)

**Branch:** `feature/scan-location-header`  
**Issues/PRs:** #30/#34  

**Work done:**
- Added `Name = "GetScanStatus"` to the `[HttpGet("{id}/status")]` route on `GetJobStatus`, so the existing `AcceptedAtAction(nameof(GetJobStatus), ...)` call in `StartTcpScan` reliably resolves a `Location` header pointing at the job's status endpoint.
- Added a unit test (`StartTcpScan_ReturnsAccepted_WithCorrectLocationRouteValues`) asserting `ActionName` and `RouteValues["id"]` on the returned `AcceptedAtActionResult`.
- Added the project's first integration test (`ScanControllerLocationHeaderTests`), using `WebApplicationFactory<Program>` to send a real in-memory HTTP request and assert the `Location` header's `AbsolutePath` against the expected status URL.
- Added `public partial class Program { }` to `WebApp/Program.cs` to make the top-level-statement-generated `Program` class visible to `WebApplicationFactory<Program>` in the `Tests` project.
- Pinned `Microsoft.AspNetCore.Mvc.Testing` to the `8.0.*` line in `Tests.csproj` (`dotnet add` defaulted to the `10.0.x` line, which targets `net10.0` and is incompatible with the project's `net8.0` target).

**Why / decisions:**
- **Explicit `Name` on the route over relying on convention-based action/controller matching**: `AcceptedAtAction`/`CreatedAtAction` link generation via bare `nameof(...)` is convention-based and can silently fail to resolve a URL (empty `Location` header, no compile error) if the route is ever grouped, prefixed, or restructured. An explicit route name decouples link generation from the action method's name/location.
- **`AcceptedAtAction` kept as-is, not replaced with manual `Url.Link` + header assignment**: it already returns the correct `202 Accepted` status (vs. `CreatedAtAction`'s `201`, which would incorrectly imply the resource is immediately available) and uses the same named-route mechanism internally — no need for a manual alternative once the route has a name.
- **Both a unit test and an integration test, not just one**: the unit test asserts routing *intent* (`ActionName`/`RouteValues`) cheaply and fast; it cannot observe the actual materialized `Location` header, since `IUrlHelper` isn't wired to real routes outside a running host. Only a `WebApplicationFactory`-based test exercises the real ASP.NET Core routing/middleware pipeline and can assert on the literal header value — which is what the issue's acceptance criteria actually require  — see Microsoft's guide on [Integration tests in ASP.NET Core](https://learn.microsoft.com/en-us/aspnet/core/test/integration-tests) for the `WebApplicationFactory` pattern used here.
- **Not marked `[Trait("Category", "Integration")]` / not excluded by the CI filter**: despite the "integration test" name, it runs fully in-memory via `TestServer` with all dependencies (`IScanJobStore`, `IScanJobQueue`) mocked — no real network I/O, no dependency on the Kali VM. The existing `--filter "Category!=Integration"` CI convention is reserved for tests that depend on real external infrastructure; this test doesn't qualify and stays in the normal CI run.
- **`Microsoft.AspNetCore.Mvc.Testing` version pinned to `8.0.*`**: `dotnet add package` without a version resolves to the latest available release across all major versions, not the latest compatible with the project's target framework.

**Problems & solutions:**
- *Problem (SSL/TLS record exception during manual verification)*: manually testing the endpoint via the JetBrains HTTP client against `https://localhost:7049` threw `NotSslRecordException`. Decoding the raw bytes in the exception showed a plaintext `400 Bad Request` from Kestrel — the app was running under the `http` launch profile (port 5104), not `https` (port 7049), so Kestrel wasn't listening for TLS on 7049 at all; the client's TLS handshake bytes were received as garbage plaintext.
  - *Solution*: ran `dotnet run --project WebApp --launch-profile https` explicitly, confirmed both `https://localhost:7049` and `http://localhost:5104` in the startup log before retesting.
- *Problem (integration test hung indefinitely)*: the first version of the integration test only mocked `IScanJobStore`/`IScanJobQueue` for the DI container but left `ScanBackgroundWorker` (registered via `AddHostedService`) running against the mocked queue. Moq's loose mock returns an immediately-completed `default(Guid)` for the unconfigured `DequeueAsync`, so the worker's dequeue loop never actually awaited anything — spinning in a tight loop and starving the thread pool the test's own HTTP call needed to complete.
  - *Solution*: added `services.RemoveAll<IHostedService>()` in the test's `WithWebHostBuilder` override, removing `ScanBackgroundWorker` from the test host entirely — the test only needs to verify the synchronous controller response, not background job processing.
- *Problem (`Microsoft.AspNetCore.Mvc.Testing` restore failure, `NU1202`)*: `dotnet add Tests package Microsoft.AspNetCore.Mvc.Testing` pulled `10.0.12`, incompatible with the project's `net8.0` target.
  - *Solution*: re-ran with `--version 8.0.*` to pin to the compatible major version line.

**Next:**
- Open PR for `feature/scan-location-header`, `Closes #30`, squash merge.
- Phase 1 remaining item unchanged: UDP scan (`UdpPortScanner`).
<!-- Add new entries above this line, newest on top -->