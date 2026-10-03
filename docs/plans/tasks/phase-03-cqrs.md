# Phase 3 — Learn & Build Tasks (W9–W10: CQRS, read models & the mediator layer)

> **Read** → **Build** → **Done when**. Questions: `agent.md` §7. Checkbox: [`../plan.md`](../plan.md). Extra reading: [CQRS pattern](https://learn.microsoft.com/azure/architecture/patterns/cqrs) · [CQRS (Fowler)](https://martinfowler.com/bliki/CQRS.html)

## Week 9 — Mediator layer & vertical slices

### W9D1 · Decide the mediator (revisit ADR-004)
**Read:** [Wolverine for MediatR users](https://wolverinefx.net/guide/migrating-to-wolverine) · [MediatR wiki](https://github.com/jbogard/MediatR/wiki) · [FastEndpoints](https://fast-endpoints.com/)
**Build:** spike ≥2 options (Wolverine / MediatR Community / FastEndpoints / hand-rolled) over `CreateShipment` + `GetShipmentById`; micro-benchmark dispatch overhead; finalise **ADR-004** (licence, support, indirection, performance).
**Done when:** spike code is merged or deleted, benchmark numbers are recorded, ADR-004 is *Accepted* with rejected options stated.

### W9D2 · Command slices (writes)
**Read:** [Vertical slice architecture](https://www.jimmybogard.com/vertical-slice-architecture/) · [Screaming architecture](https://blog.cleancoder.com/uncle-bob/2011/09/30/Screaming-Architecture.html)
**Build:** `Features/Shipments/CreateShipment/` with command, handler, validator, mapping, endpoint; POST refactored onto the slice; service classes removed.
**Done when:** adding a write use case touches exactly one folder and the old service is gone.

### W9D3 · Query slices (reads)
**Read:** [EF Core projections](https://learn.microsoft.com/ef/core/querying/projections) · [Dapper](https://github.com/DapperLib/Dapper)
**Build:** `GetShipmentsPaged` + `GetShipmentById` returning DTOs via projection (reuse the W3D2 cursor/filters); no aggregate loading on reads.
**Done when:** a test asserts one SQL statement and that `ShipmentItem` entities are never loaded for a read.

### W9D4 · Cross-cutting pipeline behaviours
**Read:** [Wolverine middleware](https://wolverinefx.net/guide/handlers/middleware.html) · [MediatR pipeline behaviours](https://github.com/jbogard/MediatR/wiki/Behaviors)
**Build:** validation, timing/logging, transaction and authorisation behaviours; document + test ordering and short-circuit semantics.
**Done when:** a rejected request holds no transaction and slow-request warnings name the use case.

### W9D5 · Errors at the boundary
**Read:** [Error handling: exception or result](https://enterprisecraftsmanship.com/posts/error-handling-exception-or-result/) · [Problem Details (RFC 9457)](https://www.rfc-editor.org/rfc/rfc9457.html)
**Build:** `Result<T>` with domain error codes + one mapper to `ProblemDetails`; handlers stop throwing for expected failures; taxonomy documented and reflected in OpenAPI examples.
**Done when:** every expected failure is a `Result` with a code and one mapper owns all HTTP mapping.

### D6 · Integrate
Create the slice template/conventions (a `dotnet new` template or a conventions doc + analyzer) so new use cases stay uniform.

### D7 · Review
When CQRS is overkill, read-your-writes pitfalls, UX for eventual consistency; week 9 quiz.

## Week 10 — Read models, search & reporting

### W10D1 · Denormalised read models
**Read:** [Materialised view pattern](https://learn.microsoft.com/azure/architecture/patterns/materialized-view) · [Event-carried state transfer](https://martinfowler.com/articles/201701-event-driven.html)
**Build:** `shipment_list_read` maintained by idempotent handlers (last-applied-sequence column) + a `rebuild` CLI command that truncates and replays.
**Done when:** rebuild reproduces the write model (checksum-equal) and replaying an event twice is a no-op.

### W10D2 · KPI & dashboard aggregates
**Read:** [Postgres materialised views](https://www.postgresql.org/docs/current/rules-materializedviews.html) · [TimescaleDB continuous aggregates](https://docs.tigerdata.com/use-timescale/latest/continuous-aggregates/)
**Build:** status counts, on-time %, per-tenant KPIs as maintained tables or continuous aggregates; measure refresh cost vs query cost.
**Done when:** the dashboard answers from the aggregate (no full scans) and staleness is documented.

### W10D3 · Search architecture (keyword + vector)
**Read:** [Postgres full-text search](https://www.postgresql.org/docs/current/textsearch.html) · [`pg_trgm`](https://www.postgresql.org/docs/current/pgtrgm.html) · [pgvector](https://github.com/pgvector/pgvector) · [Hybrid search](https://learn.microsoft.com/azure/search/hybrid-search-ranking)
**Build:** one endpoint combining FTS + trigram + `pgvector` similarity with tenant filters and a rerank step; benchmark each mode and the combination.
**Done when:** hybrid wins on your own query set and the criteria for an external search engine are written down.

### W10D4 · Export & reporting
**Read:** [Async streams](https://learn.microsoft.com/dotnet/csharp/asynchronous-programming/generate-consume-asynchronous-stream)
**Build:** a background export streaming CSV to blob storage (never materialise the whole table), a notification when ready, and a pre-signed download.
**Done when:** exporting 100k rows keeps memory flat (measured) and the request returns immediately.

### W10D5 · Projection lag & reconciliation
**Read:** [OpenTelemetry metrics API](https://opentelemetry.io/docs/specs/otel/metrics/api/) · [SLOs (Google SRE)](https://sre.google/sre-book/service-level-objectives/)
**Build:** a projection-lag metric (event time vs applied time), an alert threshold, and a reconciliation job comparing counts/checksums between models.
**Done when:** lag appears on the dashboard and a stalled projector raises the alert.

### D6 · Integrate
Load-test the read paths; record read-model vs live-query latency in `docs/perf/`.

### D7 · Review — phase gate 3
Mediator decision recorded, slice conventions followed, measured read comparison, lag observable; week 10 quiz.