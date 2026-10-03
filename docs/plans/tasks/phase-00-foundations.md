# Phase 0 — Learn & Build Tasks (W1–W2: foundations & architectural guardrails)

> **How to use this file.** One day = one session (2–3 h). **Read** first (15–40 min, primary sources only), then **Build** the artifact, then verify **Done when**. Self-test questions: `agent.md` §7. Day checkbox: [`../plan.md`](../plan.md).
> Links are primary sources (Microsoft Learn / specs / official docs). If one moves, search its title — the concept matters, not the URL.
> **Extra reading for the whole phase:** [Microsoft architecture guides](https://learn.microsoft.com/dotnet/architecture/) · [ADR format (MADR)](https://adr.github.io/madr/)

## Week 1 — Toolchain, guardrails, local topology

### W1D1 ·.NET 10 LTS bootstrap & build guardrails
**Read:** [.NET support policy (LTS vs STS)](https://dotnet.microsoft.com/platform/support/policy/dotnet-core) · [Central Package Management](https://learn.microsoft.com/nuget/consume-packages/central-package-management) · [Code analysis overview](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/overview)
**Build:** ① `dotnet new globaljson --sdk-version 10.0.100 --roll-forward latestFeature` ② `Directory.Build.props`: `Nullable`, `TreatWarningsAsErrors`, `EnforceCodeStyleInBuild`, `EnableNETAnalyzers`, `AnalysisLevel=latest-recommended`, `ImplicitUsings` ③ `Directory.Packages.props` with `ManagePackageVersionsCentrally` and move the OpenAPI package version out of the csproj ④ add `.editorconfig` ⑤ `dotnet build` and fix every newly-flagged warning.
**Done when:** clean build from the repo root, `dotnet format --verify-no-changes` exits 0, `docs/adr/ADR-002-runtime-and-guardrails.md` written (LTS over STS; why warnings are errors).

### W1D2 · Architecture skeleton & the dependency rule
**Read:** [Hexagonal architecture (Cockburn)](https://alistair.cockburn.us/hexagonal-architecture/) · [Clean Architecture template (reference structure)](https://github.com/jasontaylordev/CleanArchitecture) · [Architectural principles](https://learn.microsoft.com/dotnet/architecture/modern-web-apps-azure/architectural-principles)
**Build:** create `src/` and `tests/`; `dotnet new classlib` for `FleetTrack.Domain`, `FleetTrack.Application`, `FleetTrack.Infrastructure`; move the API to `src/FleetTrack.Api`; references: Api→Application, Infrastructure→Application+Domain, Application→Domain (nothing depends on Infrastructure); add all to `FleetTrack.slnx`; start `ADR-003-architecture-style.md`.
**Done when:** `dotnet build` green, reference matrix written in ADR-003, and you can state which reference the fitness test will forbid (W2D5).

### W1D3 · Local topology with Aspire AppHost
**Read:** [Aspire overview](https://learn.microsoft.com/dotnet/aspire/get-started/aspire-overview) · [aspire.dev](https://aspire.dev/) · [Aspire PostgreSQL integration](https://learn.microsoft.com/dotnet/aspire/database/postgresql-integration)
**Build:** `dotnet new aspire-apphost`; add hosting integrations for **PostgreSQL (pgvector/Timescale image)**, RabbitMQ, Redis, Seq (community package) and MinIO; wire the API with `WithReference(...)`/`WaitFor(...)`; `dotnet run` the AppHost and open the dashboard (resources, logs, traces).
**Done when:** all resources healthy in the dashboard, the API sees the connection strings, and `ADR-001-local-orchestration.md` records Aspire vs docker-compose (plus the CI fallback).

### W1D4 · Test harness on day one
**Read:** [xUnit v3 getting started](https://xunit.net/docs/getting-started/v3/getting-started) · [Testcontainers for .NET](https://dotnet.testcontainers.org/) · [Integration tests with WebApplicationFactory](https://learn.microsoft.com/aspnet/core/test/integration-tests)
**Build:** `tests/FleetTrack.IntegrationTests` (xUnit + FluentAssertions + `Testcontainers.PostgreSql` + `Microsoft.AspNetCore.Mvc.Testing`); one test that starts a real Postgres container and runs a `SELECT 1` through Npgsql, plus one `WebApplicationFactory` smoke test hitting `/`; enable coverage collection (`--collect:"XPlat Code Coverage"`).
**Done when:** `dotnet test` green in under ~60 s locally, and a deliberately broken assertion fails (prove the harness is real).

### W1D5 · CI from day one + agent guardrails
**Read:** [Building and testing .NET with GitHub Actions](https://docs.github.com/actions/automating-builds-and-tests/building-and-testing-net) · [Dependabot configuration](https://docs.github.com/code-security/dependabot/dependabot-version-updates/configuration-options-for-the-dependabot.yml-file) · [MCP — what it is](https://modelcontextprotocol.io/)
**Build:** `.github/workflows/ci.yml` (checkout → setup-dotnet 10 → restore → build → `dotnet format --verify-no-changes` → test → `dotnet list package --vulnerable`), `dependabot.yml` (nuget + github-actions), PR template, branch protection with required checks; commit `AGENTS.md` (dependency rule, ADR rule, test expectation) and `.mcp.json` with **read-only** MCP servers (filesystem, git, read-only Postgres).
**Done when:** the pipeline runs green on a PR, required checks block merges, and `AGENTS.md`/`.mcp.json` are in the repo.

### D6 · Integrate
Fix CI/analyzer fallout, add test-specific build rules, verify that a **clean clone** can restore/build/test, and record the commit convention (`W2D3: …`) in `CONTRIBUTING.md`.

### D7 · Review — choose the architecture style
**Read:** [Monolith vs microservices](https://learn.microsoft.com/dotnet/architecture/microservices/architect-microservice-container-applications/microservices-architecture) · [Modular monolith primer](https://www.kamilgrzybek.com/blog/posts/modular-monolith-primer)
**Build:** finish **ADR-003** with rejected options (microservices, plain layered, vertical-slice-only) and a revisit trigger; answer the week 1 quiz in `agent.md` §7.

## Week 2 — Skeleton, first slice, boundary tests

### W2D1 · Ports & adapters in code
**Read:** [Dependency injection in .NET](https://learn.microsoft.com/dotnet/core/extensions/dependency-injection) · [Architectural principles (DIP)](https://learn.microsoft.com/dotnet/architecture/modern-web-apps-azure/architectural-principles)
**Build:** define `IShipmentRepository` **in Application**; sketch the `Shipment` entity in Domain with zero framework imports; implement `EfShipmentRepository` in Infrastructure; keep the composition root in Api only.
**Done when:** nothing outside Infrastructure references EF Core and Application compiles without an EF package reference.

### W2D2 · DI, options and fail-fast configuration
**Read:** [Options pattern](https://learn.microsoft.com/dotnet/core/extensions/options) · [Configuration in .NET](https://learn.microsoft.com/dotnet/core/extensions/configuration)
**Build:** `AddApplication()` / `AddInfrastructure()` extension methods; typed options records bound and validated with `ValidateOnStart()` + `ValidateDataAnnotations()`; local connection string via `dotnet user-secrets`.
**Done when:** starting the API with a missing/invalid setting fails immediately with a clear message (no silent defaults).

### W2D3 · First vertical slice end-to-end
**Read:** [Minimal APIs quick reference](https://learn.microsoft.com/aspnet/core/fundamentals/minimal-apis) · [EF Core: your first app](https://learn.microsoft.com/ef/core/get-started/overview/first-app)
**Build:** `POST /api/v1/shipments`: endpoint → `CreateShipmentHandler` (Application) → `IShipmentRepository`; `FleetTrackDbContext` with a minimal `Shipment` mapping; `dotnet ef migrations add InitialCreate`; a fake in-memory adapter for unit tests.
**Done when:** an integration test posts a shipment and reads it back from Postgres, the handler is unit-tested with the fake adapter, and the migration is committed.

### W2D4 · API surface conventions & error contract
**Read:** [Problem Details (RFC 9457)](https://www.rfc-editor.org/rfc/rfc9457.html) · [Handle errors in ASP.NET Core APIs](https://learn.microsoft.com/aspnet/core/fundamentals/error-handling-api) · [OpenAPI in .NET](https://learn.microsoft.com/aspnet/core/fundamentals/openapi/overview)
**Build:** route group `/api/v1/shipments`; `AddProblemDetails()` + a custom `IExceptionHandler`; write the error-code table (400/401/403/404/409/422/500) with stable `type` URNs; return `201 + Location`; add OpenAPI examples.
**Done when:** every failure path returns `application/problem+json` with a documented `type`, and the OpenAPI document shows both endpoints with examples.

### W2D5 · Fitness functions & licensing decisions
**Read:** [NetArchTest](https://github.com/BenMorris/NetArchTest) · [MediatR/AutoMapper commercial editions](https://www.jimmybogard.com/automapper-and-mediatr-commercial-editions-launch-today/) · [Wolverine (MIT)](https://wolverinefx.net/)
**Build:** `tests/FleetTrack.ArchitectureTests` with rules — Domain has no framework dependencies, Application doesn't reference Infrastructure, handlers depend on abstractions; **ADR-004** (mediator/tooling: Wolverine vs MediatR Community vs FastEndpoints, with licence + support implications) and **ADR-005** (mapping: Mapperly vs AutoMapper vs manual).
**Done when:** adding a forbidden reference makes a test fail; both ADRs contain ≥2 options with real costs.

### D6 · Integrate
Consistency pass: folder-per-feature layout, naming, `dotnet format`, delete throwaway code from W2D3, run the full suite plus CI locally.

### D7 · Review — boundaries & the module map
Draft the module map (Shipments, Dispatch, Tracking, Billing, Identity, Notifications) and mark the seams you expect to matter; answer the week 2 quiz; move ADR-003 to *Accepted*.

