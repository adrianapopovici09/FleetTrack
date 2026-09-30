# FleetTrack

A **fleet & freight tracking platform** built deliberately as an architect/senior-level learning and portfolio project: shipments, dispatch, live GPS tracking, documents (BOL/POD), billing — on a modular monolith that grows the way real systems grow.

**AI is a first-class architectural concern here**, not a feature bolted on at the end: LLM integration behind a provider abstraction, tool calling over existing use cases, **MCP both ways** (used in the development workflow *and* exposed as a server for the product), RAG on Postgres + `pgvector`, agentic workflows with human approval gates, eval gates in CI, and per-tenant cost/safety operations.

## Where things live

| Path | Purpose |
| --- | --- |
| [`docs/plans/plan.md`](docs/plans/plan.md) | **The plan** — 29 weeks · 145 build days (5 build days + D6 integrate + D7 review each week), checkbox tracker, phase gates, stack table |
| [`agent.md`](agent.md) | Working file — guardrails, architecture syllabus (incl. AI/LLM/MCP), domain model, **ADR register**, quiz + worked examples for every day, commands, session log |
| `FleetTrack/` | Solution (`FleetTrack.slnx`) + API project |
| `docs/adr/` | Architecture decision records (created from W1D2) |

## Current status

**Phase 0 · W1D1 — .NET 10 LTS bootstrap & build guardrails** (not started). The repo today holds the solution, a minimal-API scaffold, CI/hygiene groundwork and these docs.

## Stack (every choice is backed by an ADR)

| Area | Choice |
| --- | --- |
| Runtime | .NET 10 LTS (upgrade day to .NET 11 in W26D4 — .NET 8/9 go end-of-support on 10 Nov 2026) |
| Architecture | Modular monolith, clean layering inside modules, vertical slices for use cases |
| Data | PostgreSQL 17 + TimescaleDB (telemetry) + PostGIS (geofences) + `pgvector` (retrieval) |
| Messaging | RabbitMQ + Wolverine (MIT licence; outbox/inbox, sagas) |
| Real-time | SignalR + Redis backplane |
| Frontend | React 19 + TypeScript + Vite (Blazor/Angular documented as alternatives) |
| Cloud | Docker → Azure Container Apps + Bicep; GitHub Actions pipelines |
| Observability | OpenTelemetry, Seq, Prometheus/Grafana, GenAI semantic conventions |
| AI | `Microsoft.Extensions.AI` over Azure OpenAI/Foundry (Ollama locally), MCP client **and** server, hybrid RAG, evals, cost caps |

## How to work the plan

One session = one day (≈2–3 h): read the day's *focus* concept → build the artifact → answer the day's check question from `agent.md` §7 without looking → on D6/D7 write the ADR and log the session. Flip the checkbox in `plan.md` when the artifact exists. Full loop: [`agent.md`](agent.md) §1.

Ask the agent anything at any time: *"quiz me on week 6"*, *"grade my answers for week 3"*, *"review my ADR-009"*, *"critique this diagram"*, *"what breaks if Tracking became a service?"*.

## Quick start

```powershell
dotnet build .\FleetTrack\FleetTrack.slnx
dotnet run   --project .\FleetTrack\FleetTrack\FleetTrack.csproj
dotnet test  .\FleetTrack\FleetTrack.slnx
# from W1D3: local stack via Aspire AppHost (or docker compose)
```

## Roadmap

| Phase | Weeks | Theme | Gate artifact |
| --- | --- | --- | --- |
| 0 | 1–2 | Foundations, guardrails, agent/MCP dev setup | CI green, dependency rule proven by tests, 3 ADRs |
| 1 | 3–5 | API contracts, data access, documents | OpenAPI contract, paging/concurrency/idempotency tests |
| 2 | 6–8 | DDD, aggregate invariants, module boundaries | Context + module map, fitness tests, ADR-007/008 |
| 3 | 9–10 | CQRS, read models, mediator decision | Measured read-path comparison, projection lag |
| 4 | 11–13 | Messaging, outbox/inbox, sagas, resilience | Broker-outage proof, saga with compensation, drill report |
| 5 | 14–16 | **Real-time tracking & telemetry** | Ingestion benchmark, live push demo, geofence→state machine |
| 6 | 17 | Caching & measured performance | Before/after optimisation numbers, SLOs |
| 7 | 18–19 | Security, identity, multi-tenancy | Threat model (API + LLM), RLS + isolation test matrix |
| 8 | 20–22 | Frontend architecture | Ops console E2E, Lighthouse/bundle budgets |
| 9 | 23 | Observability & operations | End-to-end trace, SLOs, runbook |
| 10 | 24 | Cloud, IaC & delivery | One-command staging deploy, tested restore drill |
| 11 | 25–26 | System design, capstone, staff skills | 5 design docs, 20+ ADRs, .NET 11 upgrade |
| 12 | 27–29 | **AI, LLM & MCP architecture** | Assistant + MCP server, eval gate in CI, cost caps, AI runbook |
