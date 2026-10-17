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

**M00 · Foundations**: SDK pinning, Central Package Management, layered skeleton, build guardrails, docker compose, configuration and health checks, test harness, CI and the first ADRs are done. Next: M01 · C# & runtime.

## Quick start

**Prerequisites:** the .NET SDK version pinned in [`global.json`](global.json), and Docker Desktop (or another Docker engine) running.

Commands are PowerShell, run from the repository root.

1. **Clone the repository.**

   ```powershell
   git clone https://github.com/adrianapopovici09/FleetTrack.git
   cd FleetTrack
   ```

2. **Create the local environment file.** Copy the example, then replace every `CHANGE_ME` with your own values. `deploy/.env` is git-ignored, so never commit it.

   ```powershell
   Copy-Item deploy/.env.example deploy/.env
   ```

   `POSTGRES_USER`, `POSTGRES_PASSWORD` and `POSTGRES_DB` are applied **only the first time** the data volume is created. To change them later, either run `docker compose -f deploy/compose.yaml down -v` (this deletes the data) or use `ALTER USER` in `psql`.

3. **Start Postgres and wait until it's healthy.**

   ```powershell
   docker compose -f deploy/compose.yaml up -d
   docker compose -f deploy/compose.yaml ps    # STATUS should show (healthy)
   ```

4. **Build and run the tests.** The build must show 0 warnings (warnings are errors). The integration tests start their own Postgres with Testcontainers, so step 3 isn't needed for them, only Docker.

   ```powershell
   dotnet build FleetTrack.slnx
   dotnet test  FleetTrack.slnx    # check the test count, not just "succeeded"
   ```

5. **Set the API's connection string.** Use the values from `deploy/.env`. The API reads the connection string from configuration, and Compose doesn't pass it in.

   ```powershell
   dotnet user-secrets set "ConnectionStrings:FleetTrack" "Host=127.0.0.1;Port=<POSTGRES_PORT>;Database=<POSTGRES_DB>;Username=<POSTGRES_USER>;Password=<POSTGRES_PASSWORD>" --project src/FleetTrack.API
   ```

   In containers or CI, set the environment variable `ConnectionStrings__FleetTrack` instead (the `__` stands for `:`). If the connection string is missing, the API refuses to start and prints these instructions.

6. **Run the API and check its health.**

   ```powershell
   dotnet run --project src/FleetTrack.API
   ```

   Then, in another terminal:

   ```powershell
   curl.exe http://localhost:5182/health/live     # 200: the process is up
   curl.exe http://localhost:5182/health/ready    # 200: Postgres is reachable; 503 if it's stopped
   ```

To stop the stack, run `docker compose -f deploy/compose.yaml down`. Your data survives this; add `-v` to delete the data too.
