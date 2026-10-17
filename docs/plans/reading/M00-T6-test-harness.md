# M00-T6 · Testing with real dependencies

Task: [M00-T6](../modules/M00-foundations.md)

This file is the complete lesson for the task; you don't need to read anything else. Original sources, if you ever want to go deeper: [Choosing a testing strategy (EF Core)](https://learn.microsoft.com/ef/core/testing/choosing-a-testing-strategy) · [Integration tests in ASP.NET Core](https://learn.microsoft.com/aspnet/core/test/integration-tests) · [Testcontainers for .NET: PostgreSQL](https://dotnet.testcontainers.org/modules/postgres/) · [ArchUnitNET guide](https://archunitnet.readthedocs.io/en/latest/guide/)

**Reading time:** about 50 minutes.

## Contents

1. [Kinds of tests, and how many of each](#1-kinds-of-tests-and-how-many-of-each)
2. [Test doubles: fakes, stubs and mocks](#2-test-doubles-fakes-stubs-and-mocks)
3. [Why not EF Core's InMemory provider](#3-why-not-ef-cores-inmemory-provider)
4. [Testcontainers: real dependencies started by the tests](#4-testcontainers-real-dependencies-started-by-the-tests)
5. [`WebApplicationFactory`: the whole API, in memory](#5-webapplicationfactory-the-whole-api-in-memory)
6. [Keeping integration tests isolated and fast](#6-keeping-integration-tests-isolated-and-fast)
7. [Architecture tests](#7-architecture-tests)
8. [A test you've never seen fail proves nothing](#8-a-test-youve-never-seen-fail-proves-nothing)
9. [NUnit essentials used in FleetTrack](#9-nunit-essentials-used-in-fleettrack)
10. [Glossary](#10-glossary)
11. [Summary, check yourself, interview questions, for FleetTrack](#11-summary)

---

## 1. Kinds of tests, and how many of each

| Kind | What it exercises | Speed | When it fails, you know… |
| --- | --- | --- | --- |
| **Unit test** | One small piece of logic, in isolation: no database, network or file system | Milliseconds | …exactly which rule broke |
| **Integration test** | Your code working *together* with real infrastructure (a real database, the whole HTTP pipeline) | Seconds | …that something in the combination broke |
| **End-to-end (E2E) test** | The deployed system, driven the way a user would (browser, public API) | Seconds to minutes | …that a user-visible flow is broken, but rarely why |

**The test pyramid** (the classic advice) says many unit tests, fewer integration tests and very few E2E tests, because the lower layers are faster and more precise.

**The test trophy** (popular in modern .NET and web teams) puts the **most** weight on integration tests. The argument: most real bugs live *between* pieces (a LINQ query that doesn't translate to SQL, a missing DI registration, a wrong JSON property name, a constraint violation), and with tools like Testcontainers, real-database tests now take seconds instead of minutes.

A balanced senior view:
- **Pure business rules** (pricing, state transitions, "a cancelled shipment can't be delivered") → **unit tests**. They're fast, precise, and easy to read.
- **Anything touching the database, serialization, middleware or DI wiring** → **integration tests**, against the real engine.
- **A handful of critical user journeys** → E2E or smoke tests after deployment.

What to avoid is the "ice-cream cone": lots of slow, flaky E2E tests and few fast ones.

---

## 2. Test doubles: fakes, stubs and mocks

A **test double** stands in for a real dependency in a test. The names are used loosely, but the distinction helps in interviews:

- **Stub:** returns canned answers. `clock.UtcNow` always returns 2026-10-17 10:00.
- **Fake:** a working but simplified implementation. `InMemoryVehicleRepository` stores vehicles in a `Dictionary`.
- **Mock:** records how it was called, so the test can **verify interactions**: "`SendAsync` was called exactly once with this email".

The trade-off is that **mocks couple tests to implementation details**. If a test verifies that `repository.GetAsync` was called before `repository.SaveAsync`, it breaks when you refactor, even though the behaviour didn't change. Prefer checking **outcomes** (what state ended up where) with fakes, and keep interaction checks for things that genuinely are the behaviour, such as "an email was sent".

Mocking libraries need the usual licence check. Moq added a controversial telemetry feature in 2023, which is why many teams switched to NSubstitute or hand-written fakes. Hand-written fakes are often the simplest choice for a few interfaces.

---

## 3. Why not EF Core's InMemory provider

EF Core ships an **InMemory provider** that pretends to be a database. It's tempting: fast, no setup. **Microsoft's own documentation advises against using it for testing**, because it isn't a relational database. Here are the concrete bugs it hides (quiz Q3: you need to *name* them, not just say "it's different"):

1. **Constraints are ignored.** A unique index on `LicensePlate` isn't enforced, so a test inserting two vehicles with plate `B-123-ABC` **passes**, and production throws `DbUpdateException` (Postgres error 23505, unique violation). Foreign keys aren't enforced either, so you can save a `Shipment` pointing to a non-existent `Customer`.
2. **LINQ that can't be translated to SQL runs as C# instead.** For example, `db.Vehicles.Where(v => MyHelper.IsElectric(v))` calls your own C# method, which Postgres can't execute. Against InMemory it just runs in memory and works. Against Postgres, EF Core throws "could not be translated" **at runtime**, on the first real request.
3. **No real transactions.** Rollback and isolation behaviour isn't tested, so concurrency bugs are invisible.
4. **Different semantics.** Case-sensitive vs case-insensitive string comparison, null handling, decimal precision and date/time behaviour can all differ from Postgres.
5. **No raw SQL and no database-specific features**, such as Postgres `jsonb` columns, PostGIS geometry (M08), full-text search or `FOR UPDATE SKIP LOCKED` (M06).

**SQLite in-memory** is closer, because it really is relational and enforces constraints. But it's still a **different engine**, with different SQL, types and functions, so Postgres-specific code still goes untested.

**The reliable option is testing against the same engine you run in production.** When a real database really isn't practical for a piece of logic, don't fake the *database*. Put a **repository** in front of data access and use a simple fake repository in unit tests. You then test the business rules honestly, and test the repository itself against real Postgres.

---

## 4. Testcontainers: real dependencies started by the tests

**Testcontainers** is a library that starts real Docker containers (Postgres, RabbitMQ, Valkey…) **from your test code**, and removes them afterwards:

```csharp
var container = new PostgreSqlBuilder("postgres:17").Build();
await container.StartAsync();                         // pulls the image if needed, starts it, waits until ready
var cs = container.GetConnectionString();             // Host=127.0.0.1;Port=<random>;Database=...;Username=...;Password=...
// ... run tests ...
await container.DisposeAsync();                        // stops and removes it
```

What it gives you:
- **The same engine as production**, at the same major version as `deploy/compose.yaml`.
- **A fresh, empty database every run.** Tests don't depend on what's installed on the machine or left over from yesterday. The goal: "`dotnet test` works on a fresh laptop and in CI, with no setup beyond Docker".
- **A random free host port** and a ready-made connection string, so parallel test runs (or your own Compose Postgres on 5432) don't clash.
- **A wait strategy:** `StartAsync()` returns only when the database actually accepts connections, not merely when the container has started (the same lesson as `depends_on` in T4).
- **Cleanup even after a crash.** A tiny helper container called **Ryuk** watches the test process and removes its containers if the process dies without cleaning up.

The costs:
- **It requires Docker** (the GitHub-hosted Linux runners have it; M00-T7).
- **Startup takes a few seconds**, so start a container per test *class* or per test *run*, never per test (§6).

---

## 5. `WebApplicationFactory`: the whole API, in memory

`WebApplicationFactory<TEntryPoint>` (package `Microsoft.AspNetCore.Mvc.Testing`) starts your **real** ASP.NET Core application **inside the test process**, on an in-memory test server instead of a network port. `CreateClient()` gives you an `HttpClient` whose requests go through the **entire pipeline**: routing, middleware, DI, model binding, serialization, filters, and your endpoint.

```csharp
public sealed class FleetTrackApiFactory(string connectionString) : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");                                   // not Development → user secrets NOT loaded
        builder.UseSetting("ConnectionStrings:FleetTrack", connectionString); // the container's connection string
        // builder.ConfigureTestServices(s => s.Replace(...))               // swap a service for a fake if needed
    }
}
```

Key points:
- **Overriding configuration.** `UseSetting` injects a value with high priority. Combined with a non-`Development` environment name, it guarantees the test uses the **container**, never a developer's local database from user secrets (T5 §1.2). Without the `Testing` environment, a test could pass on your machine by talking to your own Compose Postgres, and fail in CI.
- **Replacing services:** `ConfigureTestServices` runs **after** the app's own registrations, so it can replace a real email sender with a fake, for example.
- **Visibility of `Program`.** With top-level statements, the compiler generates the `Program` class as `internal`, and the test project needs to see it. Before .NET 10 you added `public partial class Program;` to the API by hand. Since .NET 10 the Web SDK generates that declaration for you, which is why FleetTrack's API has no such line and `WebApplicationFactory<Program>` still compiles.
- **Speed:** much faster than starting the app as a separate process, and it tests far more than calling a handler method directly.

What it **doesn't** test: the real Kestrel network layer, TLS, reverse proxies, or the container image itself. Those belong to deployment smoke tests (M13).

---

## 6. Keeping integration tests isolated and fast

When tests share a database, one test's data can break another: a test counting vehicles finds the 3 another test inserted. Common strategies, with their trade-offs:

| Strategy | How | Pros | Cons |
| --- | --- | --- | --- |
| **Container per test class** | `[OneTimeSetUp]` starts one; FleetTrack does this today | Simple, strong isolation between classes | A few seconds per class; adds up as classes grow |
| **One container for the whole run** | NUnit `[SetUpFixture]` at the namespace or assembly level | Fastest startup | Needs another isolation strategy *inside* it |
| **Transaction per test, rolled back** | Begin in `[SetUp]`, roll back in `[TearDown]` | Very fast cleanup | Doesn't work if the code under test commits its own transaction, or uses several connections |
| **Reset data between tests** | A tool like **Respawn** deletes all rows (keeping the schema) | Works with any code under test | A little slower; tests within a class run sequentially |
| **Database or schema per test** | Create a uniquely named database for each test | Perfect isolation, allows parallel tests | Slowest setup; more moving parts |

A common mature setup is **one container per test run, plus a Respawn reset before each test**. That's the "shared container" loose end in `plan.md`, worth doing once there are several integration-test classes.

Two more rules for reliable tests:
- **Never depend on test order.** Each test sets up exactly the data it needs.
- **Flaky tests are bugs.** A test that sometimes fails "for no reason" trains people to re-run until green, and then they re-run past real failures too.

---

## 7. Architecture tests

### 7.1 What they are

An **architecture test** is an ordinary unit test that checks the **structure** of your code rather than its behaviour: "Domain must not depend on EF Core", "Application must not depend on Infrastructure", "classes ending in `Handler` must be `sealed`". Libraries like **ArchUnitNET** (Apache-2.0) and **NetArchTest** load your **compiled assemblies**, inspect every type and its dependencies, and let you express rules fluently:

```csharp
[Test]
public void Application_ShouldNotDependOn_Infrastructure() =>
    Types().That().Are(ApplicationLayer)
           .Should().NotDependOnAny(InfrastructureLayer)
           .Check(Architecture);
```

### 7.2 Why they matter (quiz Q6, "we do code review")

- **Reviews miss things.** A single `using Microsoft.EntityFrameworkCore;` in a 40-file PR, on a busy Friday, slips through. A test never gets tired and never skims.
- **They catch what project references can't.** Domain can't reference Infrastructure (that would be a cycle), but it *can* gain a package reference to EF Core, and the build stays green. You proved exactly this in M00-T8.
- **They're executable documentation.** The rule is written down **and** enforced, and the failure message tells the newcomer *why*.
- **They stop architecture erosion,** the slow drift where a codebase loses its intended structure one shortcut at a time, each one reasonable on its own.
- **Reviews and tests are complementary.** Tests enforce the mechanical rules, which frees reviewers to discuss design and naming. The strongest counter-argument is "we automate what can be automated, and review the rest".

### 7.3 Limits

- They check only the rules you wrote.
- They see **types and dependencies**, not behaviour. "Business rules live in the domain" can't be fully expressed as a structural rule.
- Too many hyper-specific rules become noise and get deleted. Encode the few rules that really matter.

---

## 8. A test you've never seen fail proves nothing

This was the biggest lesson of the task. **A green test only means something if you've seen it go red** when the rule is broken. FleetTrack's first ArchUnitNET rules passed **vacuously**: they would have passed whatever the code did. Two reasons:

1. **`Types()` excludes referenced types by default.** The rule "Domain must not depend on any `Microsoft.*` type" was checked against an architecture that contained *only FleetTrack's own types*. Framework types like `Microsoft.EntityFrameworkCore.DbContext` weren't in it at all, so "depends on none of them" was trivially true. The fix was `Types(includeReferenced: true)`.
2. **`ResideInNamespace("Microsoft")` is an exact match.** It matches the namespace `Microsoft` itself, not `Microsoft.EntityFrameworkCore`. The fix was a pattern: `ResideInNamespaceMatching(@"^Microsoft(\..*)?$")`.

Why didn't a guard catch it? ArchUnitNET by default fails a rule whose `That()` part matches **no types**, so an empty *subject* is caught. But here the subject (Domain's types) wasn't empty; the empty set was the **object** of the rule (the framework types it must not depend on). That guard doesn't cover an empty object.

**The habit to keep:** for every new guardrail, **break it on purpose once** (a probe) and watch it fail. Then revert. That's what M00-T8 did, and it applies to every alert, health check and validation rule you'll build later: an alert that has never fired might not be wired up.

---

## 9. NUnit essentials used in FleetTrack

| Attribute / API | Meaning |
| --- | --- |
| `[Test]` | A test method |
| `[TestCase(1, 2)]` | The same test with different arguments |
| `[SetUp]` / `[TearDown]` | Runs before / after **each** test in the class |
| `[OneTimeSetUp]` / `[OneTimeTearDown]` | Runs once before / after **all** tests in the class (start and stop the container here) |
| `[SetUpFixture]` | A class whose one-time setup runs once for a whole **namespace or assembly** |
| `Assert.That(actual, Is.EqualTo(expected))` | The constraint-based assertion style |
| `Assert.Multiple(() => { ... })` | Reports every failed assertion in the block, not just the first |

NUnit creates **one instance of a test class for all its tests** by default. That's why fields set in `[OneTimeSetUp]` (like the container) are shared, and also why you must **not** keep per-test state in fields without resetting it in `[SetUp]`. (xUnit, by contrast, creates a new instance per test.) FleetTrack is on **NUnit 5** with `NUnit3TestAdapter`, the piece that lets `dotnet test` discover NUnit tests, and `NUnit.Analyzers`, which flags misused assertions at compile time.

---

## 10. Glossary

| Term | Meaning |
| --- | --- |
| **Unit / integration / E2E test** | Tests one piece in isolation / pieces with real infrastructure / the deployed system like a user |
| **Test pyramid / trophy** | Mostly unit tests / mostly integration tests, as the recommended balance |
| **Test double (stub, fake, mock)** | A stand-in for a dependency: canned answers / working simple version / records calls for verification |
| **EF Core InMemory provider** | A non-relational fake "database" for EF Core; not recommended for testing |
| **Testcontainers** | A library that starts real Docker containers from tests and cleans them up |
| **Ryuk** | Testcontainers' helper container that removes leftovers if the test process crashes |
| **`WebApplicationFactory`** | Runs the real ASP.NET Core app in-process, on an in-memory server, for tests |
| **Respawn** | A library that deletes test data between tests while keeping the schema |
| **Architecture test** | A test that checks code *structure* (allowed dependencies, naming) on compiled assemblies |
| **Architecture erosion** | The gradual loss of a codebase's intended structure through small shortcuts |
| **Vacuous test** | A test that passes no matter what, because what it checks is empty or never exercised |
| **Probe (in testing)** | A deliberate, temporary violation to prove a guardrail actually fails |

---

## 11. Summary

1. Unit tests for business rules; integration tests (real engine) for anything touching the database, serialization or wiring; a few E2E tests for critical journeys.
2. EF Core InMemory hides real bugs: ignored unique and foreign-key constraints, untranslatable LINQ silently running as C#, no transactions, different semantics. Test against Postgres.
3. Testcontainers gives every run a fresh real database on a random port, waits until it's ready, and cleans up. Start it per class or per run, never per test.
4. `WebApplicationFactory` runs the whole pipeline in-process. Override configuration with `UseSetting`, and use a non-`Development` environment so user secrets can't leak in.
5. Choose an isolation strategy deliberately: container per class, shared container plus Respawn, transactions, or database per test.
6. Architecture tests enforce structure that code review and project references miss, but only if you've seen them fail.

### Check yourself

1. Name two concrete bugs the InMemory provider would hide in FleetTrack, with an example of each. (§3)
2. Why does FleetTrack's test factory set the environment to `Testing` instead of leaving it as `Development`? What could go wrong otherwise? (§5)
3. Your integration-test suite takes 90 seconds, mostly starting containers. What would you change, and what new problem does that create? (§6)
4. A colleague says "architecture tests are pointless, we do code review." Give your strongest answer in three sentences. (§7.2)
5. Explain why the first "Domain depends on no `Microsoft.*`" rule passed even though it was broken. (§8)
6. Why must you be careful with instance fields in an NUnit test class? (§9)

### Interview questions this prepares you for

- "Why not use EF Core InMemory for tests? Name two bugs it hides."
- "How do you make integration tests reliable and fast in CI?"
- "How do you keep integration tests from interfering with each other?"
- "Your team says architecture tests are pointless because there's code review. Respond."
- "Mocks vs fakes: when would you use each?"
- "How do you know a test actually tests something?"

### For FleetTrack

- Three test projects: **UnitTests** (still the template test; real ones arrive with domain code), **IntegrationTests** (a Testcontainers `postgres:17` per fixture, a `SELECT version()` test, and a `WebApplicationFactory` test on `/health/ready` using `UseSetting` + `UseEnvironment("Testing")`), and **ArchitectureTests** (ArchUnitNET layer rules and "Domain depends on no `Microsoft.*`", using `Types(includeReferenced: true)` and `ResideInNamespaceMatching`, proven with a probe).
- `dotnet test` is green with **no local Postgres running**, only Docker. The exit check confirmed 9 tests under NUnit 5.
- Loose end: once there are more integration-test classes, share one container via `[SetUpFixture]`, and reset data with Respawn.
