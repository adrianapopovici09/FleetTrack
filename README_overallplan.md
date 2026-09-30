# FleetTrack Enterprise .NET Curriculum

## Phase 1: Modern APIs & Data Persistence

### Week 1: API Foundation & Middleware

- [ ]  **W1D1: .NET 8 Minimal APIs** — Microsoft Learn: Create a minimal API | Run `dotnet new webapi -n FleetTrack.Api`. Create GET and POST endpoints for `/api/shipments` returning hardcoded JSON.
- [ ]  **W1D2: Dependency Injection** — Microsoft Learn: DI in ASP.NET Core | Create `IShipmentService` and `ShipmentService`. Register as a Singleton in `Program.cs`.
- [ ]  **W1D3: Global Exceptions** — Microsoft Learn: Handle errors in Web APIs | Implement `IExceptionHandler` to return standardized `ProblemDetails` JSON.
- [ ]  **W1D4: Structured Logging** — Serilog ASP.NET Core Docs | Install Serilog, configure console/file sinks, and log incoming POST requests.
- [ ]  **W1D5: DTO Mapping** — Microsoft API Design Best Practices | Create `CreateShipmentRequest` DTO and map it to your domain model.
- [ ]  **W1D6: API Versioning** — Asp.Versioning Wiki | Install `Asp.Versioning.Http` and restrict routes to `/api/v1/shipments`.
- [ ]  **W1D7: Code Review & Refactor** | Extract service registrations out of `Program.cs` into clean extension methods.

### Week 2: Database Persistence & Docker

- [ ]  **W2D1: Docker Compose** — Docker Getting Started Guide | Write a `docker-compose.yml` for PostgreSQL 15 and spin it up.
- [ ]  **W2D2: EF Core Config** — Npgsql EF Core Provider Docs | Install provider, create `FleetTrackDbContext`, and connect it to your Postgres container.
- [ ]  **W2D3: Entities & Migrations** — Microsoft Learn: EF Core Migrations | Create the `Shipment` entity and run `dotnet ef migrations add InitialCreate`.
- [ ]  **W2D4: Repository Pattern** — Martin Fowler Repository Pattern | Implement `IShipmentRepository` to decouple EF Core from your services.
- [ ]  **W2D5: FluentValidation** — FluentValidation ASP.NET Core Guide | Write request validators and execute them before hitting the database.
- [ ]  **W2D6: Seed Data** | Override `OnModelCreating` in your DbContext to inject mock records on startup.
- [ ]  **W2D7: Async/Await** — Microsoft Async/Await Best Practices | Convert all synchronous data access to asynchronous methods (`ToListAsync()`).

---

## Phase 2: Clean Architecture & CQRS

### Week 3: Clean Architecture Separation

- [ ]  **W3D1: Onion Layout** — Jason Taylor Clean Architecture Template | Create class libraries for Domain, Application, and Infrastructure.
- [ ]  **W3D2: The Domain Layer** | Move core entities to `Domain`. Ensure zero dependencies on EF Core or ASP.NET.
- [ ]  **W3D3: Application Layer** | Move repository interfaces and DTOs to `Application`. Reference `Domain`.
- [ ]  **W3D4: Infrastructure Layer** | Move `DbContext` and repository implementations to `Infrastructure`.
- [ ]  **W3D5: Dependency Inversion** — Microsoft SOLID Principles Guide | Wire up project references and DI mappings in `Program.cs`.
- [ ]  **W3D6: Domain Driven Design Value Objects** — Microsoft Value Objects Guide | Refactor `TrackingNumber` into an immutable Value Object class.
- [ ]  **W3D7: Code Review** | Audit project dependencies to ensure architectural boundaries are respected.

### Week 4: CQRS & MediatR Integration

- [ ]  **W4D1: MediatR Concept** — MediatR GitHub Wiki | Install MediatR and remove legacy direct services.
- [ ]  **W4D2: Commands (Writes)** | Create `CreateShipmentCommand` and its handler. Refactor POST endpoint.
- [ ]  **W4D3: Queries (Reads)** | Create `GetShipmentByIdQuery` and its handler. Refactor GET endpoint.
- [ ]  **W4D4: Validation Pipeline** — MediatR Pipeline Behaviors | Create a pipeline behavior to automatically run FluentValidation before commands execute.
- [ ]  **W4D5: Logging Pipeline** | Create a timing/logging pipeline behavior using a `Stopwatch`.
- [ ]  **W4D6: Domain Events** — Microsoft Domain Events Guide | Add a `ShipmentCreatedDomainEvent` to the entity.
- [ ]  **W4D7: Dispatching Events** | Intercept `SaveChanges` in EF Core to publish domain events via MediatR.

---

## Phase 3: Distributed Systems & Event-Driven Architecture

### Week 5: Messaging (RabbitMQ & MassTransit)

- [ ]  **W5D1: Broker Setup** | Add `rabbitmq:3-management` container to `docker-compose.yml`.
- [ ]  **W5D2: MassTransit Config** — MassTransit Quickstart | Configure MassTransit with RabbitMQ in your API.
- [ ]  **W5D3: Integration Events** | Create `FleetTrack.Contracts` library and define `ShipmentStatusChangedEvent`.
- [ ]  **W5D4: Publishing Events** | Update status endpoints to publish events using `IPublishEndpoint`.
- [ ]  **W5D5: The Worker Project** | Create a .NET Worker Service (`FleetTrack.NotificationWorker`).
- [ ]  **W5D6: Consuming Events** | Implement a MassTransit Consumer to handle the status event in the worker.
- [ ]  **W5D7: Fault Queues** | Test fault handling and error queue routing in MassTransit.

### Week 6: Resiliency & Reliability

- [ ]  **W6D1: The Outbox Pattern** — MassTransit EF Core Outbox Docs | Configure MassTransit Outbox to store messages safely inside Postgres.
- [ ]  **W6D2: Outbox Worker** | Verify message delivery works even when RabbitMQ is temporarily offline.
- [ ]  **W6D3: Polly Retries** — Polly Resilience Docs | Wrap external calls in a Polly retry policy.
- [ ]  **W6D4: Circuit Breaker** | Implement a Polly Circuit Breaker policy for failing dependencies.
- [ ]  **W6D5: Rate Limiting** — Microsoft Rate Limiting Middleware | Restrict API endpoints using built-in rate limiters.
- [ ]  **W6D6: Background Jobs** — Quartz.NET Docs | Set up recurring background jobs.
- [ ]  **W6D7: End of Phase QA** | Conduct failure recovery tests on messaging and outbox functionality.

---

## Phase 4: Enterprise Scale & Production Readiness

### Week 7: Distributed Caching (Redis)

- [ ]  **W7D1: Redis Setup** | Add a `redis:7` container to `docker-compose.yml`.
- [ ]  **W7D2: IDistributedCache** — Microsoft Distributed Caching Docs | Configure Redis cache in the Infrastructure layer.
- [ ]  **W7D3: Cache Abstraction** | Define an `ICacheService` interface in Application.
- [ ]  **W7D4: MediatR Caching** | Create a MediatR caching pipeline behavior.
- [ ]  **W7D5: Cache Invalidation** | Ensure updates/creates clear the corresponding cache keys.
- [ ]  **W7D6: Output Caching** | Implement HTTP-level output caching for static responses.
- [ ]  **W7D7: Load Testing** — Grafana k6 Get Started | Benchmark query performance with and without Redis.

### Week 8: Security & Identity

- [ ]  **W8D1: JWT Setup** — Microsoft JWT Authentication Docs | Configure JWT Bearer authentication middleware.
- [ ]  **W8D2: Token Generation** | Build a mock login endpoint returning signed JWTs.
- [ ]  **W8D3: Endpoint Security** | Apply `[Authorize]` tags to protected endpoints.
- [ ]  **W8D4: Role-Based Access** | Implement Admin vs User role checks.
- [ ]  **W8D5: API Key Middleware** | Write custom middleware for API key authentication.
- [ ]  **W8D6: User Context** | Extract user claims into an `ICurrentUserService`.
- [ ]  **W8D7: Row-Level Security** | Scope database queries to match tenant/user IDs.

### Week 9: Unit Testing & Code Quality

- [ ]  **W9D1: xUnit Setup** — xUnit.net Getting Started | Create test projects using xUnit and FluentAssertions.
- [ ]  **W9D2: Testing Domain** | Write unit tests for Value Objects.
- [ ]  **W9D3: Mocking** — NSubstitute Docs | Mock repositories to test handlers in isolation.
- [ ]  **W9D4: Testing MediatR** | Test command handlers using mock dependencies.
- [ ]  **W9D5: Testing Validation** | Validate validation rules with unit tests.
- [ ]  **W9D6: Architecture Tests** — NetArchTest GitHub | Write architectural constraint tests.
- [ ]  **W9D7: Code Coverage** | Generate code coverage reports using XPlat tools.

### Week 10: Integration Testing

- [ ]  **W10D1: Testcontainers** — Testcontainers for .NET | Set up Testcontainers for PostgreSQL.
- [ ]  **W10D2: In-Memory API** — Microsoft Integration Tests Guide | Configure `WebApplicationFactory`.
- [ ]  **W10D3: Ephemeral DBs** | Wire test factory to use the Testcontainer connection string.
- [ ]  **W10D4: DB Seeding** | Run migrations automatically before tests execute.
- [ ]  **W10D5: End-to-End Tests** | Test full HTTP request-to-database workflows.
- [ ]  **W10D6: Testcontainers MQ** | Add RabbitMQ container to integration test suite.
- [ ]  **W10D7: Test Optimization** | Share container fixtures to optimize test execution speed.

### Week 11: Observability & Health

- [ ]  **W11D1: Health Checks** — Microsoft Health Checks Guide | Map a basic `/health` endpoint.
- [ ]  **W11D2: External Health** | Add database and message broker health check extensions.
- [ ]  **W11D3: OpenTelemetry** — Microsoft OpenTelemetry Docs | Configure distributed tracing spans.
- [ ]  **W11D4: Distributed Tracing** | Ensure trace IDs flow through RabbitMQ messages.
- [ ]  **W11D5: Prometheus Metrics** | Expose application metrics endpoints.
- [ ]  **W11D6: Seq Logging** | Route Serilog output to a local Seq instance.
- [ ]  **W11D7: Aspire Dashboard** | Explore .NET Aspire dashboard for local visualization.

### Week 12: CI/CD & Deployment

- [ ]  **W12D1: Docker Multi-Stage** — Docker Best Practices | Write multi-stage Dockerfiles for API and Worker.
- [ ]  **W12D2: GitHub Repo** | Initialize repository and push your complete project.
- [ ]  **W12D3: CI Pipeline (Build)** — GitHub Actions Docs | Create `.github/workflows/ci.yml` for automated builds.
- [ ]  **W12D4: CI Pipeline (Test)** | Add test execution steps to the CI pipeline.
- [ ]  **W12D5: Docker Push** | Configure CD workflow to push images to GitHub Container Registry.
- [ ]  **W12D6: Architecture Docs** | Create system diagrams and update the repository README.
- [ ]  **W12D7: Portfolio Polish** | Finalize documentation explaining your architectural decisions and tech stack.