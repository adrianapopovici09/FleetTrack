# M00 · Foundations & guardrails

**Time:** ~9 h · **Prereq:** none · **Outcome:** a repo where the build, tests, local stack and architecture rules are automatic, and where you understand every piece because you wrote it.

## Why it matters
"Set up a new service the team can't easily break" is a classic senior task. Interviewers ask how you'd keep a codebase healthy at scale: analyzers, CI gates and architecture tests are the answer. Knowing containers, health checks and configuration by hand is what lets you debug them when a tool's abstraction leaks.

## Concepts
- **SDK pinning & LTS policy:** `global.json` + `rollForward`; LTS (3 years) vs STS (2 years since .NET 9).
- **Central Package Management:** one version per package for the whole repo (`Directory.Packages.props`).
- **Build-wide settings:** `Directory.Build.props` applies to every project (nullable, warnings-as-errors, analyzers).
- **Containers for dev:** images vs containers, volumes, networks, port mapping, env vars, `healthcheck` + `depends_on: condition: service_healthy`.
- **Configuration:** the provider order (appsettings → env-specific → user-secrets → env vars → command line), `__` for nesting in env vars, options pattern + `ValidateOnStart()`.
- **Health checks:** liveness ("process is up") vs readiness ("can serve traffic: DB reachable"); why mixing them causes restart loops.
- **Real dependencies in tests:** Testcontainers starts a real Postgres for the test run. The EF InMemory provider lies about SQL semantics.
- **Fitness functions:** tests that fail when the architecture is violated.

Read: [Central Package Management](https://learn.microsoft.com/nuget/consume-packages/central-package-management) · [Compose file reference](https://docs.docker.com/reference/compose-file/) · [Configuration in ASP.NET Core](https://learn.microsoft.com/aspnet/core/fundamentals/configuration/) · [Health checks](https://learn.microsoft.com/aspnet/core/host-and-deploy/health-checks) · [Testcontainers for .NET](https://dotnet.testcontainers.org/)

## Labs

- [x] **L1 · SDK + CPM.** `global.json` and `Directory.Packages.props` exist. ✅ Done.
- [x] **L2 · Layered skeleton.** `src/` Domain / Application / Infrastructure / API, references follow the dependency rule. ✅ Done.
- [ ] **L3 · Build guardrails.** ◀ **NEXT TASK** Create `Directory.Build.props` at the root with `Nullable=enable`, `TreatWarningsAsErrors=true`, `EnforceCodeStyleInBuild=true`, `AnalysisLevel=latest-recommended`, `ImplicitUsings=enable`. Add an `.editorconfig` (`dotnet new editorconfig`; the `.slnx` already references one that doesn't exist yet). Add the missing `app.Run()` at the end of `Program.cs`. Fix whatever breaks.
  ✅ `dotnet build` from the root has 0 warnings; `dotnet format --verify-no-changes` exits 0; an unused variable now fails the build.
- [ ] **L4 · Local stack with docker compose.** `deploy/compose.yaml` with **Postgres 17** only: named volume, `POSTGRES_*` env vars from a git-ignored `.env` file (commit a `.env.example`), a `pg_isready` healthcheck. Optionally add pgAdmin. Connect with `psql` from the container and from your host.
  ✅ `docker compose -f deploy/compose.yaml up -d` → `docker compose ps` shows Postgres `healthy`; data survives `down` + `up` (and is gone after `down -v`). You can explain each line of the file.
- [ ] **L5 · Config + health, by hand.** In Infrastructure: `DatabaseOptions` bound from `ConnectionStrings:FleetTrack` with `ValidateOnStart()`. In the API: `AddHealthChecks().AddNpgSql(...)` (`AspNetCore.HealthChecks.NpgSql`), `/health/live` (no dependencies) and `/health/ready` (checks the DB) instead of the hand-written string endpoint. Put this in your own `AddFleetTrackDefaults()` / `MapFleetTrackDefaults()` extension methods. You'll grow them in later modules and compare them with Aspire's ServiceDefaults in M13. Local connection string via `dotnet user-secrets`.
  ✅ Missing connection string → app fails at startup with a clear message. Stopping the Postgres container → `/health/ready` returns 503 while `/health/live` stays 200.
- [ ] **L6 · Test harness.** Rename the test project to `FleetTrack.UnitTests`. Add `FleetTrack.IntegrationTests` (NUnit + `Testcontainers.PostgreSql` + `Microsoft.AspNetCore.Mvc.Testing`) with one test that runs `SELECT version()` on a container, and one `WebApplicationFactory` test hitting `/health/ready` with the container's connection string injected. Add `FleetTrack.ArchitectureTests` with **ArchUnitNET** (or NetArchTest) rules: Domain depends on no framework and no other project; Application doesn't reference Infrastructure.
  ✅ `dotnet test` green with no local Postgres running (Testcontainers handles it). Adding `using Microsoft.EntityFrameworkCore;` to a Domain class makes an architecture test fail.
- [ ] **L7 · CI.** `.github/workflows/ci.yml`: checkout → setup-dotnet (reads `global.json`) → restore → build → format check → test (Testcontainers works on `ubuntu-latest`) → `dotnet list package --vulnerable --include-transitive`. Add `dependabot.yml` (nuget + github-actions + docker) and branch protection requiring the check.
  ✅ A PR shows the check; a PR with a formatting violation is blocked.

## Break it
1. Point `/health/live` at the database check, stop Postgres, and imagine an orchestrator restarting every instance on each DB blip. Write down why liveness must not depend on downstream systems.
2. Create a "temporary" reference from Domain to Infrastructure and push it. Watch the architecture test catch it in CI.

## Decide
- **ADR-001** Local dev environment: docker compose vs running services natively vs Aspire (deferred to M13; record *why* you're deferring it).
- Complete **ADR-002** (why warnings are errors, LTS policy) and **ADR-003** (fill in the bad consequences and a revisit trigger; the plan revisits it in M05 and M09).

## Quiz → [answers](../answers/M00.md)
1. What's the difference between `Directory.Build.props` and `Directory.Packages.props`?
2. `global.json` pins `10.0.100` with `rollForward: latestFeature`. A machine has only `10.0.300` and `11.0.100` installed. Which SDK is used, and why?
3. Why are Testcontainers-based tests preferred over the EF Core InMemory provider? Name two concrete bugs InMemory hides.
4. Liveness vs readiness: what does each answer, and what goes wrong if liveness checks the database?
5. An environment variable `ConnectionStrings__FleetTrack` and `appsettings.json` both set the connection string. Which wins, and why the double underscore?
6. A colleague says "architecture tests are pointless, we do code review." Give your strongest counter-argument.
7. *Code reading:* `depends_on: [postgres]` in compose, yet the API still crashes on startup with "connection refused". Why, and what are two fixes?
8. Why might `TreatWarningsAsErrors` be painful on a legacy codebase, and how would you introduce it gradually?
9. What's the difference between a named volume and a bind mount, and which do you want for Postgres data?
10. *Design:* you join a team of 12 developers on a 6-year-old .NET solution with 40 projects and no CI rules. What are your first three guardrails, and in what order?

## Design drill (20 min)
"Design the developer platform for a 5-team .NET organisation: how do new services get created, built, tested and kept consistent?" Cover templates, shared build props, CI, package governance, local environments and architecture rules.

## Exit check
Clean clone → `docker compose up` → `dotnet build` (0 warnings) → `dotnet test` green → `/health/ready` works → CI required on `main` → ADR-001/002/003 complete.
