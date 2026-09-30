# Phase 1 — Learn & Build Tasks (W3–W5: contracts, data access, documents)

> Same rule as Phase 0: **Read** (primary sources) → **Build** (the artifact) → verify **Done when**. Questions: `agent.md` §7. Checkbox: [`../plan.md`](../plan.md).
> **Extra reading:** [API design best practices](https://learn.microsoft.com/azure/architecture/best-practices/api-design) · [Use The Index, Luke (SQL performance)](https://use-the-index-luke.com/) · [EF Core docs root](https://learn.microsoft.com/ef/core/)

## Week 3 — Contract-first APIs & generated clients

### W3D1 · API contract & versioning policy
**Read:** [ASP.NET API versioning wiki](https://github.com/dotnet/aspnet-api-versioning/wiki) · [RESTful API design (Microsoft)](https://learn.microsoft.com/azure/architecture/best-practices/api-design)
**Build:** add `Asp.Versioning.Http`; move routes into a `/api/v{v:apiVersion}` group; produce one OpenAPI document per version; write `docs/api/versioning-and-deprecation.md` (what counts as breaking, deprecation window, sunset header).
**Done when:** `GET /api/v1/shipments` works, the unversioned route is gone, and the policy document is committed.

### W3D2 · Pagination, filtering, sorting (keyset)
**Read:** [No offset (Use The Index, Luke)](https://use-the-index-luke.com/no-offset) · [EF Core pagination](https://learn.microsoft.com/ef/core/querying/pagination)
**Build:** cursor pagination ordered by `(created_at, id)` with an opaque base64 cursor; whitelisted filter + sort parameters; response envelope `{ items, nextCursor }`; tests including "insert rows while paging" and a 10k-row benchmark.
**Done when:** no `offset`/`page` parameter exists, ordering is stable under concurrent inserts, and the benchmark numbers are in the journal.

### W3D3 · Conditional requests & optimistic concurrency
**Read:** [RFC 9110 §If-Match / ETag](https://www.rfc-editor.org/rfc/rfc9110#field.if-match) · [EF Core optimistic concurrency](https://learn.microsoft.com/ef/core/saving/concurrency)
**Build:** map a concurrency token (`RowVersion` → Postgres `xmin`); `GET` returns `ETag`; `PUT/PATCH` requires `If-Match` and returns **412** on mismatch; parallel-update test asserts one success and one conflict.
**Done when:** two concurrent updates to the same shipment cannot both succeed, and the conflict returns a `ProblemDetails` with a stable `type`.

### W3D4 · Idempotency for writes
**Read:** [Stripe idempotent requests](https://docs.stripe.com/api/idempotent_requests) · [IETF Idempotency-Key draft](https://datatracker.ietf.org/doc/draft-ietf-httpapi-idempotency-key-header/)
**Build:** `Idempotency-Key` support (endpoint filter + `idempotency_keys` table: key, tenant, request hash, status, response body, expiry); replay the stored response on match; **409** when the same key is reused with a different body; store the row in the same transaction as the write.
**Done when:** two identical `POST /shipments` retries create exactly one shipment and return the same body; the conflict path is tested.

### W3D5 · Generated clients & drift detection
**Read:** [Kiota (OpenAPI client generation)](https://learn.microsoft.com/openapi/kiota/) · [NSwag / OpenAPI tooling](https://learn.microsoft.com/aspnet/core/tutorials/web-api-help-pages-using-swagger)
**Build:** generate a C# client (Kiota) and a TypeScript client from the OpenAPI document; add a CI step that regenerates and fails on diff; replace the scratch requests in `FleetTrack.http` with the versioned endpoints.
**Done when:** CI fails when the spec changes without regenerating the clients, and both clients compile.

### D6 · Integrate
Complete the error catalogue (every status code with an example), enforce request-size/timeouts limits, and review that no endpoint bypasses the shared error mapper.

### D7 · Review — pick the API style deliberately
**Read:** [API styles: REST/gRPC/GraphQL comparison](https://learn.microsoft.com/aspnet/core/grpc/comparison) · [GraphQL: when to use](https://graphql.org/learn/)
**Build:** write `docs/architecture/api-styles.md` — why REST + minimal APIs for the ops console today, what would justify gRPC (internal service-to-service) or GraphQL (many client shapes) later; answer the week 3 quiz.

## Week 4 — EF Core architecture & data integrity

### W4D1 · Model configuration & migrations
**Read:** [EF Core modelling](https://learn.microsoft.com/ef/core/modeling/) · [Value conversions](https://learn.microsoft.com/ef/core/modeling/value-conversions) · [Migrations](https://learn.microsoft.com/ef/core/managing-schemas/migrations/)
**Build:** an `IEntityTypeConfiguration<Shipment>`; conventions for naming, enum→string, `decimal(18,4)`, `timestamptz`; a migration adding real indexes (`(tenant_id, shipment_number)` unique, `(tenant_id, status, created_at)`, FK indexes); export an ER diagram to `docs/architecture/`.
**Done when:** the migration applies to a fresh container, the diagram is committed, and every FK has an index (verify with `\d+`).

### W4D2 · Relational shape of the aggregate
**Read:** [Owned entity types](https://learn.microsoft.com/ef/core/modeling/owned-entities) · [Postgres JSONB](https://www.postgresql.org/docs/current/datatype-json.html) · [Global query filters](https://learn.microsoft.com/ef/core/querying/filters)
**Build:** map `ShipmentStop`/`ShipmentItem` as child tables (and document why not table-per-hierarchy), add a JSONB `attributes` column for tenant-specific fields, implement soft delete with a global query filter, add retention columns.
**Done when:** the schema-v2 migration applies, the soft-delete filter is verified by a test, and JSONB round-trips through the API.

### W4D3 · Querying without leaks
**Read:** [EF Core efficient querying](https://learn.microsoft.com/ef/core/performance/efficient-querying) · [Tracking vs no-tracking](https://learn.microsoft.com/ef/core/querying/tracking) · [Dapper](https://github.com/DapperLib/Dapper)
**Build:** DTO projections for list + detail (no aggregate loads on reads); `IQueryable` never leaves Infrastructure; a command-counting test that fails on N+1; one compiled query for the hottest read.
**Done when:** the list endpoint issues exactly one SQL statement, and before/after timings are recorded in the journal.

### W4D4 · Transactions & unit of work
**Read:** [EF Core transactions](https://learn.microsoft.com/ef/core/saving/transactions) · [Connection resiliency](https://learn.microsoft.com/ef/core/miscellaneous/connection-resiliency)
**Build:** the transaction boundary owned by the use case; a bounded, logged retry strategy for transient failures; shipment + idempotency row written atomically; document what is atomic vs eventual.
**Done when:** a test that throws mid-write leaves no partial data, and retries are configured with limits (no infinite loops).

### W4D5 · Repository vs DbContext — decide with evidence
**Read:** [Fowler: Repository](https://martinfowler.com/eaaCatalog/repository.html) · [DbContext configuration](https://learn.microsoft.com/ef/core/dbcontext-configuration/) · [Data access guidance](https://learn.microsoft.com/dotnet/architecture/modern-web-apps-azure/architectural-principles)
**Build:** keep the repository for aggregate load/save + concurrency control, use `DbContext`/Dapper directly for projections; measure both paths; write **ADR-006** (what is abstracted, what deliberately is not, and why).
**Done when:** ADR-006 contains the measured comparison (query counts, latency) and names what you refused to abstract.

### D6 · Integrate
Index review: `EXPLAIN ANALYZE` the list/detail queries against 10k seeded rows, adjust indexes, store the plans in `docs/perf/`.

### D7 · Review — polyglot persistence
**Read:** [Choosing a data store](https://learn.microsoft.com/azure/architecture/guide/technology-choices/data-store-overview) · [What is time-series data](https://www.timescale.com/learn/what-is-time-series-data)
**Build:** `docs/architecture/storage-decisions.md` — what stays in Postgres, what will need Timescale/`pgvector`/blob storage, and the trigger for each; answer the week 4 quiz.

## Week 5 — Documents, integrations and the outbound boundary

### W5D1 · Document storage + retrieval baseline
**Read:** [Azure Blob concepts](https://learn.microsoft.com/azure/storage/blobs/storage-blobs-introduction) · [MinIO docs](https://min.io/docs/minio/linux/index.html) · [User-delegation SAS](https://learn.microsoft.com/azure/storage/blobs/storage-blob-user-delegation-sas-create-dotnet) · [pgvector](https://github.com/pgvector/pgvector)
**Build:** `Document` metadata table + storage bucket; `POST /shipments/{id}/documents` returns a short-lived pre-signed PUT URL; a confirm endpoint marks it stored and enqueues the scan hook; downloads use pre-signed GET with an audit row; **enable the `vector` extension and create `document_chunk` now** (used in W29D1).
**Done when:** upload → confirm → download works end to end, the API never proxies bytes, and the migration creates the extension + chunk table.

### W5D2 · Anti-corruption layer for partners
**Read:** [Anti-corruption layer](https://learn.microsoft.com/azure/architecture/patterns/anti-corruption-layer) · [Bounded context](https://martinfowler.com/bliki/BoundedContext.html)
**Build:** partner payload DTOs + translator in Infrastructure, contract tests over a recorded sample payload, mapping into your own command — no partner type reaches Domain.
**Done when:** renaming a partner field changes only the ACL folder, and the contract test fails if the partner shape changes.

### W5D3 · Outbound HTTP resilience
**Read:** [IHttpClientFactory](https://learn.microsoft.com/dotnet/core/extensions/httpclient-factory) · [Polly v8](https://www.pollydocs.org/)
**Build:** typed client for an external routing/geocoding provider with timeout + jittered retry + circuit breaker; OTel instrumentation; tests using a stubbed message handler.
**Done when:** policy tests prove retry counts and breaker opening, and a simulated 5xx never surfaces as an unhandled exception.

### W5D4 · Outbound webhooks
**Read:** [Standard Webhooks](https://www.standardwebhooks.com/) · [Webhook best practices](https://docs.github.com/webhooks/using-webhooks/best-practices-for-using-webhooks)
**Build:** subscriber registry + HMAC-signed payloads (timestamp + nonce), delivery attempts with backoff, delivery log, replay endpoint; test signature verification and replay rejection.
**Done when:** a failing subscriber is retried, visible in the delivery log and replayable; signatures verified both ways.

### W5D5 · Seed & demo data at volume
**Read:** [Bogus](https://github.com/bchavez/Bogus) · [Test data builders](https://learn.microsoft.com/dotnet/architecture/microservices/microservice-ddd-cqrs-patterns/)
**Build:** deterministic `--seed` CLI task: ~10k shipments with stops/items/documents, environment-gated, idempotent, anonymised.
**Done when:** seeding twice produces identical data and the paged list stays under ~200 ms at 10k rows.

### D6 · Integrate
Extract the shared `Result`/error types, remove duplicated mapping, publish OpenAPI examples for the document and webhook endpoints.

### D7 · Review — phase gate 1
Run the fitness tests, write the phase-gate note (proven / deferred / risky), and answer the week 5 quiz.


