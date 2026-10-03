# FleetTrack

A multi-tenant **fleet & freight tracking platform** (shipments, dispatch, live GPS telemetry, geofences, alerts and an AI assistant), built as a hands-on learning and portfolio project for **senior .NET engineering and software architecture**.

The system evolves the way real systems do: **layered API → modular monolith → monolith + an extracted Tracking service**, then secured, observed, deployed to Azure and extended with AI. Every step has a written decision record.

## Learning plan

| | |
| --- | --- |
| [**Plan & tracker**](docs/plans/plan.md) | 17 modules (~26 weeks at ~8 h/week), architecture path, technology choices, ADR register |
| [**Modules**](docs/plans/modules/) | Per module: ordered tasks, each with what to read 📖, what to build 🔨 and a done-check ✅; then a quiz, a design drill and review questions |
| [**Answer keys**](docs/plans/answers/) | Model answers for every quiz (don't peek first) |
| [**CLAUDE.md**](CLAUDE.md) | How Claude acts as a tutor: `quiz M03`, `drill M08`, `review M06-T2`, `status` |

## Architecture (target)

```
            ┌──────────── YARP gateway ────────────┐
 clients ──►│  /api/v1/*            /api/v1/tracking│
            └──────┬───────────────────────┬───────┘
                   ▼                       ▼
   FleetTrack monolith              Tracking service ◄── gRPC stream ── devices
   (Shipments · Dispatch ·          (ingest, positions,
    Billing modules)                 geofences, SignalR)
        │      ▲  events (outbox) via RabbitMQ                ▲      │
        ▼      └──────────────────────────────────────────────┘      ▼
   Postgres (fleettrack)                                  Postgres (tracking)
   + Valkey · Keycloak · OpenTelemetry → Grafana · Ollama (local LLM)
```

## Stack

.NET 10 · Minimal APIs · EF Core 10 + Dapper · PostgreSQL 17 (PostGIS, pgvector) · RabbitMQ · Wolverine · gRPC · SignalR · HybridCache + Valkey · YARP · Keycloak (OIDC) · OpenTelemetry + Grafana · docker compose → Azure Container Apps free tier (Bicep, GitHub Actions) · Aspire (as the comparison in M13) · Microsoft.Extensions.AI + MCP · NUnit, Testcontainers, ArchUnitNET, k6.

**Cost: $0.** Everything is free or open source; the cloud module uses free-tier SKUs only, and has a local Kubernetes fallback (see [plan §4](docs/plans/plan.md)).

## Current status

**M00 · Foundations**: SDK pinning, Central Package Management, the layered skeleton and build guardrails are done. Next: docker compose, health checks, test harness, CI.

## Quick start

```powershell
docker compose -f deploy/compose.yaml up -d   # local dependencies (from M00-T4)
dotnet build .\FleetTrack.slnx
dotnet test  .\FleetTrack.slnx
dotnet run   --project .\src\FleetTrack.API
```
