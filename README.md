# 🛡️ Network Security Toolkit

An educational, open-source cybersecurity project built with **.NET 8/10**, demonstrating core network security principles through three practical modules:

- 🔍 **Port Scanning** — async TCP/UDP scanning with job-based status polling
- 🔐 **Brute-force Demo & Defense** — controlled attack simulation with layered defense mechanisms
- 📊 **Monitoring** — network, application, and security metrics via Prometheus + Grafana

Built as both a portfolio showcase and the foundation for a university thesis (*diplomski rad*).

---

## Overview

This is a web application — an ASP.NET Core Web API with a Blazor Server frontend — with an optional standalone console app for running individual tools independently (e.g. running the brute-force simulator from a separate machine).

The project is intentionally built with architecture first: SOLID principles and design patterns are established early, before functionality is expanded. See [`ROADMAP.md`](./ROADMAP.md) for the full phased development plan.

### Modules

**Port Scanner**
- TCP/UDP scanning with configurable port ranges and timeouts
- Async job pattern (start scan → job ID → poll status), non-blocking
- Results exportable to CSV/JSON

**Brute-force Demo with Defense**
- ASP.NET Core Identity-based login system
- Controlled `BruteForceSimulator` targeting the app's own test accounts
- Defense pipeline built with Chain of Responsibility: **lockout → rate limiting → captcha**

**Monitoring**
- Three layers: network (ping/latency/packet loss/traceroute), application performance (scan throughput, duration), and security events (login attempts, lockouts)
- Prometheus-compatible `/metrics` endpoint (`prometheus-net`)
- Dashboards-as-code: Grafana dashboard JSON checked into `/monitoring`
- Monitored hosts are populated automatically from scanner results, not a static list

---

## Architecture

```
/NetworkSecurityToolkit
  /Core           — UI-agnostic business logic (scanner, brute-force, defenses, monitoring)
  /WebApp         — Blazor Server frontend + Web API (MudBlazor UI)
  /ConsoleApp     — CLI entry point for running Core modules independently
  /Data           — EF Core DbContext
  /Tests          — Unit tests
```

**Core is UI-agnostic** — both `WebApp` and `ConsoleApp` depend on `Core`, never the reverse. This keeps the business logic testable and reusable across entry points.

### Design patterns in use

| Pattern | Where |
|---|---|
| Strategy | Scan types (TCP/UDP), export formats |
| Factory | Scanner instantiation based on configuration |
| Chain of Responsibility | Defense pipeline (lockout → rate limiting → captcha) |
| Observer | Host status change notifications (metrics, logs, alerts) |
| Repository | EF Core data access |
| Template Method | General scan workflow (prepare → execute → process results) |

---

## Tech Stack

| Layer | Technology |
|---|---|
| Runtime | .NET 8 (LTS), .NET 10 |
| Web/API | ASP.NET Core Web API |
| UI | Blazor Server (real-time via built-in SignalR) |
| UI components | MudBlazor |
| Auth | ASP.NET Core Identity |
| ORM / Database | EF Core + SQLite |
| Tests | xUnit + Moq |
| Monitoring | Prometheus (`prometheus-net`) + Grafana |
| Logging | Serilog |
| CI | GitHub Actions |

---

## Getting Started

### Prerequisites
- .NET 8 SDK or later
- (Optional) A VM or host for scan/monitoring targets — testing is done against an isolated Kali Linux VM (VirtualBox, Host-only Adapter)

### Build & run

```bash
git clone https://github.com/Nero265/network-security-toolkit.git
cd network-security-toolkit
dotnet build
```

Run the web app:
```bash
dotnet run --project WebApp
```

Run the console app:
```bash
dotnet run --project ConsoleApp
```

Run tests:
```bash
dotnet test
```

---

## Testing Environment

Development and scan/monitoring testing is done against an isolated **Kali Linux VM** (VirtualBox, Host-only Adapter network) — a safe, self-contained target with no risk to third-party infrastructure.

A secondary validation phase against physical Cisco lab equipment (if available) is planned as a later, optional step.

---

## Roadmap

Development follows a phased plan — see [`ROADMAP.md`](./ROADMAP.md) for details on each phase, from architecture setup through documentation and polish.

---

## Documentation

- [`ROADMAP.md`](./ROADMAP.md) — phased development plan
- `ARCHITECTURE.md` — architecture diagram and SOLID/pattern rationale *(coming in a later phase)*
- `SECURITY.md` — responsible use disclaimer *(coming in a later phase)*
- `CONTRIBUTING.md` — contribution guidelines *(coming soon)*

---

## License

This project is licensed under the [MIT License](./LICENSE).

## Disclaimer

This project includes a controlled brute-force simulation tool intended **only** for use against its own test accounts and endpoints, in isolated test environments. It is not intended for use against systems you do not own or have explicit permission to test.
