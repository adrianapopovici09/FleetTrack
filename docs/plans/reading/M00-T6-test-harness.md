# M00-T6 reading notes: testing with real dependencies

Task: [M00-T6](../modules/M00-foundations.md) · Sources: [Choosing a testing strategy (EF Core)](https://learn.microsoft.com/ef/core/testing/choosing-a-testing-strategy) · [Integration tests in ASP.NET Core](https://learn.microsoft.com/aspnet/core/test/integration-tests) · [Testcontainers for .NET: PostgreSQL](https://dotnet.testcontainers.org/modules/postgres/) · [ArchUnitNET guide](https://archunitnet.readthedocs.io/en/latest/guide/)

---

## 1. Kinds of tests

- **Unit tests** check one small piece of logic in isolation, with no database or network. They're very fast and give precise failures. Best for business rules.
- **Integration tests** check that pieces work *together* with real infrastructure: your code plus a real database, or the whole HTTP pipeline. They're slower, but they catch the bugs that live between the pieces.
- **End-to-end tests** drive the whole deployed system the way a user would. They're the most realistic, and also the slowest and most fragile.

The classic **test pyramid** says to write many unit tests, fewer integration tests and very few end-to-end tests. Many .NET teams now favour a **"test trophy"** shape with more integration tests, because modern tools (below) make real-database tests fast enough, and those tests catch more real bugs per test.

---

## 2. Why not EF Core's InMemory database

EF Core offers an "InMemory" provider that pretends to be a database, which is tempting because it's fast and needs no setup. Microsoft's own documentation **advises against** using it for testing, because **it isn't a relational database**. Bugs it hides:

- **No constraints:** unique indexes and foreign keys aren't enforced, so a duplicate licence plate passes the test and fails in production.
- **No real transactions:** rollback behaviour isn't tested.
- **No SQL translation:** a LINQ query that EF can't translate into SQL works fine in memory (it just runs as C#) but **throws at runtime** against a real database.
- **Different behaviour:** string comparison and case sensitivity, null handling and decimal precision can all differ.
- **No raw SQL**, no database-specific features (Postgres JSON columns, for example).

**SQLite in-memory** is closer (it really is relational) but is still a different database engine from Postgres. The most reliable option is testing against **the same database engine you run in production**.

When a real database really isn't practical, the alternative is to test business logic *without* a database by putting a **repository** interface in front of data access and using a simple fake in unit tests.

---

## 3. `WebApplicationFactory`: testing the whole API in memory

`WebApplicationFactory<Program>` (package `Microsoft.AspNetCore.Mvc.Testing`) starts your real ASP.NET Core app **inside the test process**, using an in-memory server instead of a real network port. You get an `HttpClient` that sends requests through the entire pipeline: routing, middleware, dependency injection, serialization and your endpoints.

- You can **override things for the test** in `ConfigureWebHost`: replace services, or inject configuration such as the test database's connection string.
- `Program` must be visible to the test project. With top-level statements this is done with `public partial class Program { }`.
- It's much faster than starting the real app as a separate process, and it tests far more than calling a method directly.

---

## 4. Testcontainers: real dependencies, started by the tests

**Testcontainers** is a library that starts real Docker containers (Postgres, Redis, RabbitMQ…) **from your test code**, and removes them when the tests finish.

- Each run gets a fresh, empty database, so tests don't depend on what's installed on the machine. "`dotnet test` works on a fresh laptop and in CI" is the goal.
- It maps the container to a **random free port** and gives you the connection string, so parallel test runs don't clash.
- A helper container called **Ryuk** cleans up leftover containers even if the test process crashes.
- **It requires Docker** on the machine (CI runners on Linux have it).

### Speed and isolation trade-offs
- Starting a container takes a few seconds, so start it **once per test class or test run** (`[OneTimeSetUp]` in NUnit), not once per test.
- Tests sharing one database can affect each other. Common fixes:
  - wrap each test in a transaction and roll it back;
  - reset the data between tests with a tool like **Respawn**;
  - give each test its own database or schema.

---

## 5. Architecture tests: making the design rules executable

An **architecture test** is a normal unit test that checks the *structure* of your code. For example: "the Domain assembly must not reference EF Core", or "Application must not reference Infrastructure". Libraries like **ArchUnitNET** and **NetArchTest** inspect the compiled assemblies and let you write these rules in a readable, fluent style.

### Why they matter
- Code review misses things, especially a single `using` in a 40-file PR, or on a busy day. An automated test never gets tired.
- They're **executable documentation**: the rule is written down *and* enforced.
- They stop **architecture erosion**, the slow drift where a codebase quietly loses its intended structure, one shortcut at a time.

### Limits
- They only check what you've written as a rule.
- They work on compiled assemblies, so they check references and types, not behaviour.
- Too many overly specific rules become annoying and get deleted. Encode the few rules that really matter.

---

## Interview questions this prepares you for

- "Why not use EF Core InMemory for tests? Name two bugs it hides."
- "How do you make integration tests reliable and fast in CI?"
- "Your team says architecture tests are pointless because there's code review. Respond."
- "How do you keep integration tests from interfering with each other?"

## For FleetTrack

There are three test projects: `UnitTests`, `IntegrationTests` (Testcontainers Postgres + `WebApplicationFactory` hitting `/health/ready`) and `ArchitectureTests` (Domain has no framework references; Application doesn't reference Infrastructure).
