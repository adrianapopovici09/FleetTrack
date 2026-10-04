# M00 · Foundations & guardrails

**Time:** ~9 h · **Prereq:** none · **Outcome:** a repo where the build, tests, local stack and architecture rules are automatic, and where you understand every piece because you wrote it.

**Why it matters:** "set up a new service the team can't easily break" is a classic senior task. Interviewers ask how you'd keep a codebase healthy at scale, and knowing containers, configuration and health checks by hand is what lets you debug them when a tool's abstraction leaks.

Each task: 📖 **Learn** (read first) → 🔨 **Build** → ✅ **Done when**.

---

### T1 · SDK pinning & Central Package Management ✔ done
- 📖 **Learn (20 min):** LTS (3 years) vs STS (2 years) support, and how `rollForward` picks an SDK. [.NET support policy](https://dotnet.microsoft.com/platform/support/policy/dotnet-core) · [global.json overview](https://learn.microsoft.com/dotnet/core/tools/global-json) · [Central Package Management](https://learn.microsoft.com/nuget/consume-packages/central-package-management)
- 🔨 **Build:** `global.json` + `Directory.Packages.props`.
- ✅ **Done when:** packages have no versions in `.csproj` files.

### T2 · Layered skeleton & the dependency rule ✔ done
- 📖 **Learn (30 min):** the dependency rule: dependencies point inward, and the core owns the interfaces (ports) that the infrastructure implements. [Architectural principles (Microsoft)](https://learn.microsoft.com/dotnet/architecture/modern-web-apps-azure/architectural-principles) · [Common web application architectures: Clean Architecture](https://learn.microsoft.com/dotnet/architecture/modern-web-apps-azure/common-web-application-architectures#clean-architecture)
- 🔨 **Build:** `src/` Domain / Application / Infrastructure / API with references `API → Application → Domain`, `Infrastructure → Application`.
- ✅ **Done when:** it builds and the reference matrix is in ADR-003.

### T3 · Build guardrails ✔ done
- 📖 **Learn (30 min):**
  - `Directory.Build.props` applies MSBuild properties to every project below it. [Customize your build by folder](https://learn.microsoft.com/visualstudio/msbuild/customize-by-directory)
  - .NET analyzers, `AnalysisLevel` and enforcing code style at build time. [Code analysis overview](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/overview) (read "Enable additional rules" and "Enforce on build")
  - `.editorconfig` severities. [Configuration files for code analysis](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/configuration-files)
  - Nullable reference types. [Nullable reference types](https://learn.microsoft.com/dotnet/csharp/nullable-references)
- 🔨 **Build:** `Directory.Build.props` at the root with `Nullable=enable`, `TreatWarningsAsErrors=true`, `EnforceCodeStyleInBuild=true`, `AnalysisLevel=latest-recommended`, `ImplicitUsings=enable`. Add an `.editorconfig` (`dotnet new editorconfig`; the `.slnx` already references one that doesn't exist yet). Add the missing `app.Run()` to `Program.cs`. Fix whatever breaks. (The ADR-002 write-up for this happens in T9.)
- ✅ **Done when:** `dotnet build` from the root shows 0 warnings; `dotnet format --verify-no-changes` exits 0; an unused variable now fails the build.

### T4 · Local stack with docker compose ✔ done
- 📖 **Learn (45 min):**
  - Images vs containers, volumes, networks, port mapping. [Docker overview](https://docs.docker.com/get-started/docker-overview/) · [Volumes](https://docs.docker.com/engine/storage/volumes/)
  - Compose services, `.env` files, `healthcheck`, `depends_on: condition: service_healthy`. [Compose file reference: services](https://docs.docker.com/reference/compose-file/services/) · [Startup order](https://docs.docker.com/compose/how-tos/startup-order/)
  - The official Postgres image's env vars and init scripts. [postgres on Docker Hub](https://hub.docker.com/_/postgres) (read "Environment Variables" and "Initialization scripts")
- 🔨 **Build:** `deploy/compose.yaml` with **Postgres 17** only: a named volume, `POSTGRES_*` from a git-ignored `.env` (commit a `.env.example`), and a `pg_isready` healthcheck. Optionally add pgAdmin. Connect with `psql` both inside the container and from your host.
- ✅ **Done when:** `docker compose -f deploy/compose.yaml up -d` → `docker compose ps` shows Postgres `healthy`; data survives `down` + `up` and is gone after `down -v`; you can explain each line of the file.

### T5 · Configuration & health checks, by hand ◀ NEXT TASK
- 📖 **Learn (45 min):**
  - Configuration providers and their precedence; `__` in env var names. [Configuration in ASP.NET Core](https://learn.microsoft.com/aspnet/core/fundamentals/configuration/) (read "Default application configuration sources" and "Environment variables")
  - Options pattern + validation at startup. [Options pattern](https://learn.microsoft.com/dotnet/core/extensions/options) (read "Options validation" and `ValidateOnStart`)
  - Local secrets. [Safe storage of app secrets](https://learn.microsoft.com/aspnet/core/security/app-secrets)
  - Liveness vs readiness. [Health checks in ASP.NET Core](https://learn.microsoft.com/aspnet/core/host-and-deploy/health-checks) (read "Separate readiness and liveness probes")
- 🔨 **Build:** `DatabaseOptions` bound from `ConnectionStrings:FleetTrack` with `ValidateOnStart()`. Add `AddHealthChecks().AddNpgSql(...)` (package `AspNetCore.HealthChecks.NpgSql`) with `/health/live` (no dependencies) and `/health/ready` (checks the DB), replacing the hand-written `/health` string. Put it in your own `AddFleetTrackDefaults()` / `MapFleetTrackDefaults()` extension methods; you'll grow them later and compare with Aspire in M13. Local connection string via `dotnet user-secrets`.
- ✅ **Done when:** a missing connection string makes the app fail at startup with a clear message; stopping Postgres → `/health/ready` returns 503 while `/health/live` stays 200.

### T6 · Test harness with real dependencies
- 📖 **Learn (45 min):**
  - Why not EF InMemory: it isn't a relational database. [Choosing a testing strategy (EF Core)](https://learn.microsoft.com/ef/core/testing/choosing-a-testing-strategy)
  - Integration tests with `WebApplicationFactory`. [Integration tests in ASP.NET Core](https://learn.microsoft.com/aspnet/core/test/integration-tests)
  - Testcontainers for Postgres. [Testcontainers for .NET: PostgreSQL module](https://dotnet.testcontainers.org/modules/postgres/)
  - Architecture tests. [ArchUnitNET guide](https://archunitnet.readthedocs.io/en/latest/guide/) (or [NetArchTest README](https://github.com/BenMorris/NetArchTest))
- 🔨 **Build:** rename the test project to `FleetTrack.UnitTests`. Add `FleetTrack.IntegrationTests` (NUnit + `Testcontainers.PostgreSql` + `Microsoft.AspNetCore.Mvc.Testing`) with one test running `SELECT version()` on a container, and one `WebApplicationFactory` test hitting `/health/ready` with the container's connection string injected. Add `FleetTrack.ArchitectureTests`: Domain depends on no framework and no other project; Application doesn't reference Infrastructure.
- ✅ **Done when:** `dotnet test` is green with no local Postgres running; adding `using Microsoft.EntityFrameworkCore;` to a Domain class fails an architecture test.

### T7 · Continuous integration
- 📖 **Learn (30 min):** [Building and testing .NET with GitHub Actions](https://docs.github.com/actions/use-cases-and-examples/building-and-testing/building-and-testing-net) · [Dependabot options](https://docs.github.com/code-security/dependabot/working-with-dependabot/dependabot-options-reference) · [Protected branches](https://docs.github.com/repositories/configuring-branches-and-merges-in-your-repository/managing-protected-branches/about-protected-branches) · [`dotnet list package --vulnerable`](https://learn.microsoft.com/dotnet/core/tools/dotnet-list-package)
- 🔨 **Build:** `.github/workflows/ci.yml`: checkout → setup-dotnet (reads `global.json`) → restore → build → format check → test (Testcontainers works on `ubuntu-latest`) → `dotnet list package --vulnerable --include-transitive`. Add `dependabot.yml` (nuget, github-actions, docker) and branch protection requiring the check.
- ✅ **Done when:** a PR shows the check, and a PR with a formatting violation is blocked.

### T8 · Break it
- 📖 **Learn (10 min):** reread "Separate readiness and liveness probes" in the health-checks doc from T5.
- 🔨 **Build:** (1) point `/health/live` at the DB check, stop Postgres, and write down what an orchestrator would do to every instance. (2) Push a "temporary" Domain → Infrastructure reference and watch CI catch it. Revert both.
- ✅ **Done when:** you've written two short journal notes explaining each failure.

### T9 · Decide: ADR-001, finish ADR-002/003
- 📖 **Learn (20 min):** what makes an ADR useful later. [ADR GitHub organisation](https://adr.github.io/) · [Documenting architecture decisions (Nygard)](https://cognitect.com/blog/2011/11/15/documenting-architecture-decisions)
- 🔨 **Build:** **ADR-001**: docker compose vs native installs vs Aspire, recording *why* Aspire is deferred to M13. Finish **ADR-002** (the "Build guardrails" section from T3, a real "bad" consequence, and a revisit trigger) and **ADR-003** (revisit trigger). Use the [template](../../decisions/adr-template.md).
- ✅ **Done when:** each ADR has ≥2 options, at least one negative consequence and a revisit trigger.

---

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
