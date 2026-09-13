# 🗺️ Roadmap — Network Security Toolkit

This roadmap defines the development order for the project, with an emphasis
on setting up the architecture and design principles (SOLID, design patterns)
early, before expanding functionality.

---

## Phase 0 — Setup (2-3 days)

- Repo structure, `.gitignore`, solution setup (Core / WebApp / ConsoleApp / Tests)
- GitHub Actions CI (build + test on push/PR)
- README / CONTRIBUTING skeleton

---

## Phase 1 — Port Scanner + core architecture (1.5-2 weeks)

- `IPortScanner` interface + `TcpPortScanner` implementation (async, non-blocking)
- Strategy pattern established here (makes it easy to add `UdpPortScanner` later)
- Web API endpoint: `POST /api/scan/tcp` → returns job ID; `GET /api/scan/{id}/status`
  (async job pattern, non-blocking HTTP call)
- Unit tests for scanner logic (mock network calls)
- UDP scan added after TCP, once the pattern is proven

---

## Phase 2 — Brute-force demo + defenses (2 weeks)

- ASP.NET Identity login system
- `BruteForceSimulator` (controlled, only against the project's own login endpoint)
- `DefenseMechanisms` via Chain of Responsibility (lockout → rate limiting → captcha)
- Tests for the defense pipeline (priority — highest portfolio value of this module)

---

## Phase 3 — Monitoring module (2 weeks)

- Ping / Traceroute logic (async)
- `/metrics` endpoint (Prometheus format, `prometheus-net` NuGet package)
- Prometheus + Grafana installed **natively** locally (not Docker) to reduce
  resource overhead during development
- Observer pattern for host status changes

---

## Phase 4 — Blazor UI (2 weeks)

- Dashboard connecting all modules (scan results, monitoring status, login demo)
- UI comes last since it relies on an already-stable API

---

## Phase 5 — Documentation + polish (1 week)

- `ARCHITECTURE.md` — diagram + explanation of SOLID / design pattern decisions
- `SECURITY.md` — responsible use disclaimer
- Swagger / OpenAPI documentation
- Screenshots (Grafana dashboard, Blazor UI) in the README
- Docker Compose file as an **optional** "quick start" way to run the whole
  stack (Prometheus + Grafana + app), for those who don't want a native install

---

## Phase 6 — Optional, later

- Shodan API integration (comparing scan results)
- VirusTotal API integration (URL/hash checking)
- SNMP integration
- Python module for advanced security features (separate future work, outside
  the main .NET showcase)

---

## ⏱️ Timeline

~10-12 weeks at a normal pace. Faster is possible with intensive work, but the
phase order (architecture → functionality → UI → documentation) should be kept
regardless of pace.

## 🧭 Approach notes

Recommended order within each phase: first establish the interfaces and a
basic "skeleton" data flow (breadth), then deepen functionality (depth). This
ensures the repo has a working, demonstrable version of all modules at any
given time, rather than one module being 100% complete while the others don't
exist yet.
