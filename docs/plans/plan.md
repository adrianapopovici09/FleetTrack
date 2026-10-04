# FleetTrack — Senior .NET & Architecture Learning Plan

**Goal:** become a credible senior .NET engineer / solution architect by building one realistic system end to end, and be able to *explain every decision* in an interview or a design review.

**Project:** FleetTrack, a multi-tenant freight & fleet tracking platform with shipments, dispatch, live GPS telemetry, alerts and an AI assistant. The domain is chosen because it naturally forces the hard problems: concurrency, reliability, high-volume ingest, real-time push, tenant isolation, long-running workflows, and a genuine reason to split one part into its own service.

**Budget:** ~8 h/week (4 sessions × ~2 h) → **~26 weeks** for the core path, plus one optional frontend module.

---

## 📍 Where you are

> **Update this box at the end of every session.**

| | |
| --- | --- |
| **Current module** | [M00 · Foundations & guardrails](modules/M00-foundations.md) |
| **Done** | M00-T1 SDK pinning + Central Package Management · M00-T2 layered skeleton · M00-T3 build guardrails (`Directory.Build.props`, `.editorconfig`, `app.Run()`; build 0 warnings, format clean) · M00-T4 local stack (`deploy/compose.yaml`: Postgres 17, project `name:`, `.env`/`.env.example` with `:?` fail-fast, port bound to `127.0.0.1` with `POSTGRES_PORT` fallback, named volume, `pg_isready -h 127.0.0.1` healthcheck; all ✅ checks passed) |
| **▶ Next task** | **[M00-T5 · Configuration & health checks, by hand](modules/M00-foundations.md)**: read the configuration, options, app-secrets and health-check pages (~45 min), then bind `DatabaseOptions` with `ValidateOnStart()`, add `/health/live` + `/health/ready` (NpgSql check) in `AddFleetTrackDefaults()` / `MapFleetTrackDefaults()`, connection string via `dotnet user-secrets`. Add `secrets.json` to `.gitignore` at this point. |
| **Then** | T6 test harness → T7 CI → T8 break it → T9 ADRs |
| **Weak spots to revisit** | Named volume vs bind mount (and why bind mounts hurt Postgres on Windows) · `POSTGRES_*` vars only apply on first init of an empty volume; rotate passwords with `ALTER USER` |
| **Known loose ends** | `Program.cs` has a temporary hand-written `/health` endpoint (replaced in T5) · the test project is still named `FleetTrack.PublicAPI.UnitTests` (renamed in T6) · ADR-002/003 need finishing (T9) · optional: `.editorconfig` has `insert_final_newline = false` and most style rules at `silent`/`suggestion`, so few are enforced in the build (raise the ones you care about to `warning` when the code grows) · optional: add a `.gitattributes` (`* text=auto`) to settle the CRLF/LF mix in `deploy/` · `.gitignore` was trimmed on purpose; rules get re-added in the task that needs them |
| **Last session** | 2026-10-04: finished M00-T4 (compose stack built step by step; line-explanation check done) |

---

## 1. How each module works

Every module in [`modules/`](modules/) is a numbered list of **tasks** (T1, T2, …), done in order. Each task is one or two sessions and carries everything it needs:

| Part of a task | What it contains |
| --- | --- |
| 📖 **Learn** | The concept in a sentence or two, plus the specific pages to read (often a named section), with a time estimate. Read before building. |
| 🔨 **Build** | What to implement in FleetTrack |
| ✅ **Done when** | An objective check. The task is done only when it passes. |

The last tasks of each module are always **Break it** (cause the failure the module protects against, on purpose) and **Decide** (write the module's ADRs in [`../decisions/`](../decisions/)). Then, at the end of the module:

| Part | What you do | Time |
| --- | --- | --- |
| **Quiz** | 10 questions: concept, scenario, code-reading, design. Answer **without looking**, then check [`answers/`](answers/) or ask Claude to grade you. | 30 min |
| **Design drill** | One system-design interview question, answered out loud, 20 min timed | 20 min |
| **Review** | 3 questions from earlier modules (spaced repetition) | 10 min |
| **Exit check** | The module's gate. Don't start the next module until it passes. | — |

**Learning rules**

1. **Build it by hand once, then adopt the tool.** You write docker compose and your own health/config/OpenTelemetry setup before meeting Aspire, a naive outbox before Wolverine's, manual cache-aside before `HybridCache`, hand-written Bicep before `azd`. You can't judge a tool you've never needed, and you shouldn't depend on one whose behaviour you can't explain.
2. **Break it before you fix it.** Every module includes a failure you cause on purpose (lost update, duplicate message, cross-tenant leak, thread-pool starvation…). Seeing the bug is what makes the fix stick.
3. **Numbers, not adjectives.** Performance, throughput and cost claims need a measurement in `docs/perf/`.
4. **Explain to learn.** After each module, write `docs/journal/Mxx.md` with three things you learned, one surprise and one open question. This becomes your interview material.
5. **Use Claude as a tutor, not a code generator.** The rules are in [`CLAUDE.md`](../../CLAUDE.md): hints before solutions, quizzes graded one question at a time, code reviewed against the acceptance checks.
6. **Timebox.** If a task passes 3 h, ship the smallest version that meets ✅, write the rest down as a follow-up and move on.

---

## 2. The architecture path (and why it isn't "just a monolith")

| Stage | Modules | Shape | What you learn |
| --- | --- | --- | --- |
| 1. Layered | M00–M04 | One API, `Domain / Application / Infrastructure / API` projects (already built) | The dependency rule, and why layers alone don't create boundaries |
| 2. Modular monolith | M05–M08 | One deployable, modules with their own schema, contracts and vertical slices; in-process + broker messaging | Real boundaries without network costs; modules that are *ready* to be split |
| 3. Hybrid: monolith + extracted service | M09 onward | **Tracking** (telemetry ingest, positions, geofences) becomes its own service with its own database, behind a YARP gateway, integrated by messages | Data ownership, distributed consistency, contract versioning, service-to-service auth, distributed tracing, independent deployment |

**Why this order:** starting with microservices buries the learning under infrastructure, and you'd draw boundaries before understanding the domain (the most common real-world microservices failure). Staying monolith-only leaves you with no hands-on answer to the distributed-systems questions senior roles ask. Extracting **one** service *for a stated reason* teaches both sides, and gives you the strongest interview story: "here's when I split, why, and what it cost."

**Why Tracking is the one to extract:** it's write-heavy and spiky (devices), needs different storage (partitioned time-series + PostGIS), scales independently of the shipment UI, and can degrade without stopping bookings. Shipments/Dispatch/Billing stay together because they share transactional workflows.

---

## 3. Roadmap & tracker

Update the status and quiz score as you go. Status: `☐` not started · `◐` in progress · `☑` done.

| # | Module | Weeks | Key skills | Status | Quiz |
| --- | --- | --- | --- | --- | --- |
| M00 | [Foundations & guardrails](modules/M00-foundations.md) | 1 | SDK pinning, CPM, analyzers, docker compose, health checks, config, Testcontainers, CI, architecture tests | ◐ | — |
| M01 | [Modern C# & runtime essentials](modules/M01-csharp-runtime.md) | 1 | async/await internals, cancellation, GC, `Span<T>`, DI lifetimes, BenchmarkDotNet | ☐ | — |
| M02 | [API design that survives clients](modules/M02-api-design.md) | 1.5 | Minimal APIs, OpenAPI + Scalar, validation, ProblemDetails, versioning, keyset paging, ETags, idempotency | ☐ | — |
| M03 | [Data with EF Core 10 & PostgreSQL](modules/M03-data-efcore.md) | 1.5 | Modelling, migrations, query plans, N+1, transactions, concurrency, Dapper reads, bulk ops | ☐ | — |
| M04 | [Domain modelling (tactical DDD)](modules/M04-domain-modeling.md) | 1 | Value objects, aggregates, invariants, state machines, domain events, Result pattern, `TimeProvider` | ☐ | — |
| M05 | [Modular monolith & vertical slices](modules/M05-modular-monolith.md) | 1.5 | Bounded contexts, refactor layered → modules, module contracts, schema-per-module, fitness tests | ☐ | — |
| M06 | [Messaging & reliability](modules/M06-messaging.md) | 2 | Outbox by hand → Wolverine, RabbitMQ, idempotent consumers, DLQ, contract evolution | ☐ | — |
| M07 | [Workflows, sagas & background work](modules/M07-workflows-sagas.md) | 1 | Sagas, timeouts, compensation, scheduled jobs, `BackgroundService` | ☐ | — |
| M08 | [Real-time telemetry](modules/M08-realtime-telemetry.md) | 2 | gRPC streaming, `Channels` + backpressure, Postgres partitioning, PostGIS geofences, SignalR | ☐ | — |
| M09 | [From modules to services](modules/M09-extract-service.md) | 2 | Extracting Tracking: own DB, data migration, YARP gateway, sync vs async calls, contract tests, distributed failure modes | ☐ | — |
| M10 | [Performance & caching](modules/M10-performance-caching.md) | 1.5 | Profiling, allocations, `HybridCache` + Redis, output caching, k6, SLOs, capacity maths | ☐ | — |
| M11 | [Security & multi-tenancy](modules/M11-security-tenancy.md) | 2 | OIDC (Keycloak), policies, BOLA, service-to-service auth, Postgres RLS, rate limiting, secrets, threat model | ☐ | — |
| M12 | [Observability & operations](modules/M12-observability.md) | 1 | OpenTelemetry by hand across gateway → services → broker, metrics, logs, Grafana LGTM, SLO alerts, game day | ☐ | — |
| M13 | [Cloud, delivery & Aspire](modules/M13-cloud-delivery.md) | 2 | Container images, hand-written Bicep → Container Apps (free tier) or k3d, GitHub OIDC, migration bundles, Key Vault; **then Aspire + `azd` as the comparison** | ☐ | — |
| M14 | [AI-native features](modules/M14-ai-native.md) | 2 | `Microsoft.Extensions.AI`, tool calling, structured output, RAG on pgvector, MCP server, evals in CI | ☐ | — |
| M15 | [Architect's toolkit & capstone](modules/M15-architect-capstone.md) | 1.5 | Legacy modernisation (strangler fig), C4, ADR review, mock design interviews, .NET 11 upgrade | ☐ | — |
| M16 | [Frontend slice](modules/M16-frontend-optional.md) *(optional)* | 1.5 | React + TS + TanStack Query, generated client, live map, Playwright E2E | ☐ | — |

**Milestones (portfolio checkpoints)**

- **After M05:** a clean modular monolith with a tested shipment API. *Interview story: "how I structure a .NET system and enforce it."*
- **After M09:** messaging, sagas, live tracking, and one extracted service. *Story: "reliability under at-least-once delivery, and when and how I split a service."*
- **After M12:** secure, multi-tenant, observable across services. *Story: "how I prove tenant isolation and debug a distributed request."*
- **After M15:** deployed to Azure, with an AI assistant, C4 docs and 5+ design write-ups. *The full portfolio.*

---

## 4. Technology choices (and why)

Each row is decided properly in the module that introduces it. The "why" here is the short version.

| Concern | Choice | Why this, for learning *and* the job market | Alternative you should be able to discuss |
| --- | --- | --- | --- |
| Runtime | **.NET 10 LTS**, C# 14 | Supported until Nov 2028; what employers run | .NET 11 (STS): upgrade exercise in M15 |
| Local environment | **docker compose**, written by you, growing one service per module | You learn what a stack really needs (images, networks, volumes, env vars, healthchecks, startup order) before any tool hides it | **Aspire**: introduced in M13 *after* you've built the plumbing by hand, so you can judge what it automates |
| Service plumbing | **Hand-written** `AddFleetTrackDefaults()` extension: health checks, options validation, OpenTelemetry (M12), resilience | Every line is code you wrote and can explain | Aspire ServiceDefaults (M13 comparison) |
| Architecture | **Layered → modular monolith → monolith + one extracted service** (see §2) | Teaches boundaries first, then distribution with a real reason | Microservices-first, Clean Architecture everywhere |
| API | **Minimal APIs** + built-in OpenAPI + **Scalar** UI + .NET 10 built-in validation | Current Microsoft direction; less ceremony than controllers | Controllers, FastEndpoints |
| Gateway | **YARP** (M09) | Microsoft's reverse proxy; the same tool you'll use for strangler-fig migrations (M15) | Azure API Management, Ocelot |
| Handlers | **Plain handler classes** first, then **Wolverine** in M06 | Learn the pattern without magic; Wolverine is MIT and covers in-process handlers, messaging, outbox and sagas in one tool | MediatR (commercial since 2025), MassTransit (v9 commercial) |
| Database | **PostgreSQL 17 + PostGIS + pgvector** (one custom image) | One engine covers relational, JSONB, spatial, vectors and partitioned time-series. Learn to defend "just use Postgres". | SQL Server, TimescaleDB, Cosmos DB |
| ORM / reads | **EF Core 10** for writes, **Dapper** for hot reads | The real-world combination; teaches when an ORM helps and when it hurts | EF-only, Marten |
| Messaging | **RabbitMQ** (local and in the cloud deployment) | Free and portable; Wolverine keeps the transport swappable | Azure Service Bus (discussed in M13; Standard tier isn't free), Kafka (M08/M09 design drills) |
| Service-to-service | **Messages by default; gRPC** where a synchronous call is justified | Forces you to argue sync vs async per interaction | REST between services |
| Device ingest | **gRPC client streaming** | gRPC skills are expected in senior .NET roles; streaming fits device telemetry | HTTP batch POST, MQTT |
| Real-time | **SignalR** + Valkey/Redis backplane | Standard .NET push stack | SSE, Azure SignalR / Web PubSub (paid) |
| Caching | **HybridCache** (L1 + **Valkey** L2, Redis-compatible) + output caching | The modern .NET caching API with stampede protection built in; Valkey is the BSD-licensed Redis fork, and the same client/API applies | Redis, `IDistributedCache` directly |
| Identity | **Keycloak** container locally; Entra ID discussed | Real OIDC flows without a cloud tenant | Entra ID, Duende IdentityServer |
| Resilience | `Microsoft.Extensions.Http.Resilience` (Polly v8) | Built-in standard pipeline | Raw Polly |
| Testing | **NUnit** (already set up) + **Testcontainers** + `WebApplicationFactory` + **ArchUnitNET** + **Verify** + **Bogus** + **k6** | Real databases in tests, executable architecture rules, snapshot API contracts | xUnit v3, TUnit, EF InMemory (avoid) |
| Observability | **OpenTelemetry** SDK configured by hand → **Grafana LGTM** container (Tempo, Loki, Prometheus, Grafana) | Vendor-neutral; you wire every exporter yourself; the same signals locally and in the cloud | Seq, Application Insights, Aspire dashboard |
| Cloud | **Azure Container Apps** (free tier) with **hand-written Bicep** + GitHub Actions (OIDC); fallback **k3d/kind** locally | The most common .NET cloud target; Container Apps has a monthly free grant; writing Bicep yourself teaches what's actually deployed | AKS, App Service; `azd`/Aspire-generated infra (M13 comparison) |
| AI | **`Microsoft.Extensions.AI`** over **Ollama** (local, free), **pgvector**, **MCP C# SDK**, `Microsoft.Extensions.AI.Evaluation` | Provider-neutral .NET abstractions mean the paid providers are a config change you can *discuss* without paying for | Azure OpenAI, GitHub Models; Semantic Kernel, Microsoft Agent Framework |
| Frontend (optional) | **React + TypeScript + Vite + TanStack Query** | Largest market; demonstrates a client architecture | Blazor |

**Licence rule:** check the production licence before adding any package and note it in the ADR.

### Cost: $0

Everything in this plan is free or open source. The two places that *could* cost money have free paths built in:

| Area | Free choice | Notes |
| --- | --- | --- |
| IDE | VS Code + C# Dev Kit, Visual Studio Community, or Rider (free for non-commercial use) | — |
| Everything local | .NET, Postgres/PostGIS/pgvector, RabbitMQ, **Valkey** (BSD fork of Redis, drop-in compatible), Keycloak, Grafana LGTM, Ollama, YARP | All run in docker compose on your machine |
| Libraries | Wolverine, EF Core, Dapper, Scalar, NUnit, Testcontainers, ArchUnitNET, Verify, Bogus, BenchmarkDotNet | MIT/Apache. Commercial-licence libraries are deliberately excluded (MediatR, AutoMapper, MassTransit v9, FluentAssertions v8). |
| Tools | k6, oasdiff, gitleaks, Trivy, Stryker.NET, Pact, PerfView | Free / OSS |
| CI | GitHub Actions + GitHub Container Registry (`ghcr.io`) | Free for public repos; ~2,000 min/month on private repos is enough for this plan |
| **Cloud (M13)** | Azure **free account** (credit for 30 days + 12 months of free services) with free-tier SKUs only, a **$1 budget alert** and teardown after every session | No free credit available? Do M13 against a local Kubernetes cluster (**k3d/kind**) instead: the same pipeline and IaC lessons, $0. Azure needs a card on signup even for the free tier. |
| **AI (M14)** | **Ollama** locally (default) | Optional free hosted alternative: GitHub Models (rate-limited free tier). Azure OpenAI is never required. |
| Maps (M16) | MapLibre + OpenFreeMap / free demo tiles | No API key needed |

---

## 5. ADR register

Write ADRs in [`../decisions/`](../decisions/) with the [template](../decisions/adr-template.md): context, ≥2 real options, decision, consequences (including the bad ones), and a revisit trigger.

| ADR | Decision | Module | Status |
| --- | --- | --- | --- |
| 001 | Local dev environment: docker compose (revisit in M13 with Aspire) | M00 / M13 | ☐ |
| 002 | Runtime & build guardrails | M00 | ◐ (exists; complete it) |
| 003 | Architecture style and evolution path (layered → modular → hybrid) | M00 / M05 / M09 | ◐ (exists; complete it) |
| 004 | Error handling: Result vs exceptions, ProblemDetails contract | M02 / M04 | ☐ |
| 005 | API versioning & breaking-change policy | M02 | ☐ |
| 006 | Persistence: EF Core + Dapper split, repository usage | M03 | ☐ |
| 007 | Bounded contexts & module boundaries | M05 | ☐ |
| 008 | When to extract a service (trigger list) | M05 | ☐ |
| 009 | Messaging: broker + framework | M06 | ☐ |
| 010 | Telemetry ingest & storage (gRPC, partitioning) | M08 | ☐ |
| 011 | Real-time transport | M08 | ☐ |
| 012 | Extracting Tracking: data ownership, integration style, gateway | M09 | ☐ |
| 013 | Caching strategy | M10 | ☐ |
| 014 | Identity, authorization & service-to-service auth | M11 | ☐ |
| 015 | Tenancy & isolation model | M11 | ☐ |
| 016 | Cloud platform & delivery (and the Aspire verdict) | M13 | ☐ |
| 017 | AI integration boundary & provider strategy | M14 | ☐ |
| 018 | MCP exposure & tool authorization | M14 | ☐ |
| 019 | Retrieval design & eval gate | M14 | ☐ |
| 020 | Legacy modernisation approach | M15 | ☐ |

---

## 6. Repository conventions

- **Branch per task:** `m03/t3-keyset-paging`; commit messages start with the task id (`M02-T3: keyset paging`).
- **Where things go:** `docs/decisions/` ADRs · `docs/design/` design-drill write-ups · `docs/perf/` measurements · `docs/journal/` learning log · `docs/architecture/` diagrams · `labs/` throwaway katas (M01) · `deploy/` compose + Bicep.
- **Dependency rule** (until M05, then per module): `API → Application → Domain`, `Infrastructure → Application`; nothing references Infrastructure except the composition root. From M00-T6 onward this is enforced by tests.
