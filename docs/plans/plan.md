# FleetTrack — Engineering & Architecture Plan (29 weeks · 145 build days)

> **This file is the plan.** There is one plan, no versions and no archived history: the checkbox list below is both the curriculum and the tracker.
> Working file — architecture syllabus, ADR register, quiz answers with worked examples: [`agent.md`](../../agent.md). Project overview and quick start: [`README.md`](../../README.md).

## How this plan works

- **Cadence:** every week = **5 build days (D1–D5)** + **D6 integrate / refactor / QA** + **D7 review: quiz + ADR + journal**. D6/D7 are real slots — they are how architecture stays coherent instead of drifting.
- **29 weeks = 145 build days.** At 7 slots/week ≈ 21 weeks; a build day ≈ 2–3 focused hours.
- **AI is a first-class architectural concern, not a bolt-on:** agent- and MCP-assisted development from W1, embeddings/pgvector baseline in W5, hybrid retrieval in W10, OWASP-LLM threat modelling in W18, GenAI telemetry and cost in W23, and a full **Phase 12 — AI, LLM & MCP architecture** (W27–W29).
- **Every day ends with an artifact** (file, migration, test, ADR, diagram, report, measurement). No "read about X" days.
- **Architecture is the spine:** one architecture theme per phase; each phase yields ≥1 ADR + ≥1 diagram + 1 measured result.
- **Each day carries a check question** and a *focus* concept — questions, answers and worked examples are in `agent.md` §7.
- **Changed by ADR only.** Plan edits that aren't recorded as an ADR in `agent.md` §5 don't count.

## Status legend

`[x]` done · `[~]` in progress · `[ ]` not started · `[!]` blocked / needs a decision

## Stack (each choice backed by an ADR)

| Area | Choice | ADR |
| --- | --- | --- |
| Runtime | .NET 10 LTS → .NET 11 upgrade day (W26D4) | 002 |
| Style | Modular monolith + clean layering + vertical slices | 003 |
| Mediator | Wolverine *or* MediatR Community *or* FastEndpoints (decide W9D1) | 004 |
| Mapping | Mapperly (source-gen) + manual — no AutoMapper in prod | 005 |
| Data access | EF Core for writes, Dapper/raw SQL for hot reads | 006 |
| Docs/blob | MinIO locally → Azure Blob in prod | — |
| Data | PostgreSQL 17 + TimescaleDB + PostGIS | 011 |
| Messaging | RabbitMQ + Wolverine/MassTransit (licence-checked) | 009, 010 |
| Real-time | SignalR (+ Redis backplane) | 012 |
| Identity | Entra ID *or* Keycloak (OIDC + PKCE) | 013 |
| Tenancy | Shared schema + `TenantId` + Postgres RLS | 014 |
| Frontend | React 19 + TS + Vite (Blazor documented alternative) | 015 |
| Local orchestration | Aspire 13 AppHost | 001 |
| Cloud | Azure Container Apps + Bicep | 016 |
| Observability | OpenTelemetry + Seq + Prometheus/Grafana (+ GenAI semantic conventions) | — |
| AI integration | `Microsoft.Extensions.AI` (`IChatClient`/`IEmbeddingGenerator`) over Azure OpenAI/Foundry, OpenAI or local Ollama; streaming + structured outputs | 017, 018 |
| AI tooling surface | **MCP everywhere it fits:** consume MCP dev servers in the agent workflow *and* expose FleetTrack use cases as an MCP server | 019, 020 |
| Retrieval | `pgvector` in the same Postgres + hybrid retrieval (BM25 + vector) with reranking; citations and "no answer" path | 021 |
| AI quality & safety | Eval suites in CI, prompt/model version registry + rollback, per-tenant token/cost caps, content safety, prompt-injection defences, audit of AI-driven actions | 022 |
| Agentic workflows | Human-in-the-loop approval gates for writes; step/timeout/cost ceilings; use an agent framework only where orchestration is genuinely justified | 023 |

---

## Phase 0 — Foundations & Architectural Guardrails (W1–W2)

*Architecture theme: choosing a style, enforcing the dependency rule, guardrails that make later phases cheap.*

### Week 1 — Toolchain, guardrails, local topology

- [ ] **W1D1 · .NET 10 LTS bootstrap & build guardrails** — `global.json`, `net10.0`, central package management (`Directory.Packages.props`), `Directory.Build.props` (nullable, `TreatWarningsAsErrors`, analyzers, `EnforceCodeStyleInBuild`), `.editorconfig` → *props files + build passes clean* · **focus: LTS vs STS policy**
- [ ] **W1D2 · Solution topology & the dependency rule** — `src/`, `tests/`, `web/`, `docs/adr/`; `.slnx` project map; who may reference whom (`Api → Application → Domain`, `Infrastructure` implements ports) → *solution + reference matrix* · **focus: dependency inversion**
- [ ] **W1D3 · Local topology with Aspire AppHost** — Postgres/Timescale, RabbitMQ, Redis, Seq/OTel collector, MinIO, Mailpit as resources; connection strings and health from the AppHost → *AppHost runs `dotnet run`* · **focus: dev/prod parity** · ADR-001
- [ ] **W1D4 · Test harness on day one** — xUnit + FluentAssertions + `WebApplicationFactory` + first Testcontainers Postgres test; coverage collection configured → *green `dotnet test`* · **focus: shift-left**
- [ ] **W1D5 · CI from day one + agent guardrails** — GitHub Actions (restore/build/test/format/analyzers, `dotnet list package --vulnerable`, Dependabot, required checks, PR template) **and** the AI-assist baseline: `AGENTS.md`/Copilot instructions encoding the dependency rule + ADR rule, plus read-only MCP dev servers (git, Postgres, OpenAPI, Aspire) wired into the coding agent → *CI green + agent guardrails committed* · **focus: fitness before features; guardrails for AI-assisted work**
- [ ] **D6 · Integrate** — fix warnings-as-errors fallout; extend `.gitignore` (node/terraform/Aspire); branch protection + commit convention
- [ ] **D7 · Review** — architecture styles compared (layered / hexagonal / clean / modular monolith / microservices / vertical slices) → **ADR-003 draft** · *quiz + journal*

### Week 2 — Skeleton, first slice, boundary tests

- [ ] **W2D1 · Ports & adapters in code** — interfaces live in Application, implementations in Infrastructure; composition root only in Api → *projects wired, nothing leaks* · **focus: ports & adapters**
- [ ] **W2D2 · DI, options & fail-fast configuration** — extension-method registration (`AddApplication/AddInfrastructure`), options validated on start, user-secrets, precedence order → *bad config fails at boot* · **focus: config at the edge**
- [ ] **W2D3 · First vertical slice** — `POST /api/v1/shipments` → Application use case → `IShipmentRepository` → Postgres adapter (fake adapter for unit tests) → *slice + unit + integration test* · **focus: slice over layer**
- [ ] **W2D4 · API surface conventions** — route groups per feature, endpoint filters for validation, RFC 9457 `ProblemDetails` with stable `type` URNs, `201 + Location`, 401/403/404/409 semantics → *error contract + OpenAPI* · **focus: error contract design**
- [ ] **W2D5 · Fitness functions & licensing decisions** — NetArchTest suite (domain purity, no infra leakage, handlers use abstractions), **ADR-004** mediator/tooling (MediatR Community vs Wolverine vs FastEndpoints) and **ADR-005** mapping (Mapperly vs AutoMapper) → *arch tests fail on violation* · **focus: automated architecture**
- [ ] **D6 · Integrate** — consistency pass: folder-per-feature, naming, `dotnet format`; delete superseded in-memory adapter
- [ ] **D7 · Review** — module map draft (bounded contexts guessed) + boundary reasoning · *quiz + journal*

**Phase gate 0:** CI green · dependency rule proven by tests · 3 ADRs · local stack reproducible from a clean clone.

---

## Phase 1 — API Surface, Contracts & Data Access (W3–W5)

*Architecture theme: contracts and persistence boundaries — the API is a product, the database is an implementation detail.*

### Week 3 — Contract-first APIs & generated clients

- [ ] **W3D1 · API contract & versioning policy** — OpenAPI as the deliverable; `Asp.Versioning` with `/api/v1`; written deprecation + breaking-change policy → *versioning/deprecation doc* · **focus: contract evolution**
- [ ] **W3D2 · Pagination, filtering, sorting** — keyset (cursor) paging vs offset at volume; stable ordering; whitelisted filters; response envelope → *`GET /shipments?cursor&limit&status&sort`* · **focus: cursor paging**
- [ ] **W3D3 · Conditional requests & concurrency** — `ETag`/`If-Match`, `RowVersion`/`xmin` optimistic concurrency, 412 semantics, never check-then-act → *concurrency tests* · **focus: lost updates**
- [ ] **W3D4 · Idempotency for writes** — `Idempotency-Key` header, stored fingerprint + replayed response, 409 when a key is reused with a different body → *idempotency store + tests* · **focus: at-least-once delivery**
- [ ] **W3D5 · Generated clients & drift detection** — generate C# + TS clients from OpenAPI; CI job fails when generated code is stale → *checked-in clients* · **focus: contracts as code**
- [ ] **D6 · Integrate** — error catalogue (every status code documented with an example), request-size and timeout limits
- [ ] **D7 · Review** — REST vs RPC vs GraphQL: when GraphQL earns its keep (many clients, aggregate views) and what it costs (caching, authorisation clarity, query cost control) · *quiz + journal*

### Week 4 — EF Core architecture & data integrity

- [ ] **W4D1 · Model configuration & migrations** — `IEntityTypeConfiguration`, naming strategy, enums→string, decimal precision, `timestamptz`, migration hygiene → *initial migration + model diagram* · **focus: persistence ignorance**
- [ ] **W4D2 · Relational shape for the aggregate** — owned types vs tables, FK/index strategy, JSONB for tenant-specific attributes, soft delete + query filters → *schema v2* · **focus: map domain to relations**
- [ ] **W4D3 · Querying without leaks** — DTO projections, `AsNoTracking`, split queries, compiled queries; `IQueryable` never escapes Application; N+1 hunt → *perf note + tests* · **focus: query boundary**
- [ ] **W4D4 · Transactions & unit of work** — transaction boundary owned by the use case, no EF types in Domain, transient retry, atomic vs eventual decisions → *transactional use case + tests* · **focus: consistency boundaries**
- [ ] **W4D5 · Repository vs DbContext** — when a repository earns its place (aggregates, concurrency, test seams) vs when it's ceremony; Dapper/raw SQL for read models → **ADR-006** · **focus: abstraction cost**
- [ ] **D6 · Integrate** — index review with `EXPLAIN ANALYZE` against Bogus-seeded volume (≥10k shipments, 100k telemetry rows later)
- [ ] **D7 · Review** — "ORM everywhere" critique; polyglot persistence (relational vs time-series vs search vs blob) and when each wins · *quiz + journal*

### Week 5 — Documents, integrations and the outbound boundary

- [ ] **W5D1 · Document storage + retrieval baseline** — MinIO/Azurite + SDK; metadata row vs blob; pre-signed upload/download URLs; virus-scan hook; **enable the `vector` extension and add the `document_chunk` table now** (RAG arrives in W29D1) → *`POST /shipments/{id}/documents`* · **focus: blobs out of the DB; get the retrieval schema in early**
- [ ] **W5D2 · Anti-corruption layer** — partner payloads + translator, contract tests, external types never enter Domain → *ACL + tests* · **focus: protect the model**
- [ ] **W5D3 · Outbound HTTP resilience** — `IHttpClientFactory` + Polly v8 timeout/retry/circuit breaker; typed clients; never `new HttpClient()` → *typed client + policies* · **focus: dependency budgets**
- [ ] **W5D4 · Outbound webhooks** — HMAC signing, retry with backoff, delivery log, replay endpoint, subscriber registry → *dispatcher + tests* · **focus: push integration**
- [ ] **W5D5 · Seed & demo data** — deterministic seeds, realistic volume, environment-gated, anonymised fixtures → *seed task/CLI* · **focus: testability at volume**
- [ ] **D6 · Integrate** — extract shared Result/error types; kill duplicated mapping; publish OpenAPI examples
- [ ] **D7 · Review** — is the Api/Application/Infrastructure boundary still clean? Write the phase-gate note · *quiz + journal*

**Phase gate 1:** complete OpenAPI contract for shipments + documents · paging, concurrency and idempotency tests green · index review recorded with numbers.

---

## Phase 2 — Domain Modeling & Modular Monolith Boundaries (W6–W8)

*Architecture theme: strategic + tactical DDD, and where the module seams will be when distribution eventually becomes justified.*

### Week 6 — Tactical DDD: the aggregate, invariants and events

- [ ] **W6D1 · Glossary → model** — ubiquitous language per context (shipment, consignment, leg, stop, POD, drayage); rename code to match; publish glossary → *glossary doc + renames* · **focus: language shapes the model**
- [ ] **W6D2 · Value objects** — `TrackingNumber`, `Address`, `Weight`, `Money`, `DateRange`: immutability, value equality, validation in the constructor, EF `ValueConverter`/owned mapping → *VOs + tests* · **focus: kill primitive obsession**
- [ ] **W6D3 · Aggregate root & invariants** — `Shipment` guards totals and legality; children change only through methods; collections never leak → *aggregate + invariant tests* · **focus: invariant-first design**
- [ ] **W6D4 · Lifecycle, audit & purge** — audit interceptor (created/updated/by), soft delete + retention policy, GDPR purge of party PII → *interceptor + tests* · **focus: cross-cutting in persistence**
- [ ] **W6D5 · Domain events (in-process)** — aggregate collects events, dispatch after commit, handlers live in Application → *`ShipmentCreated` handler + tests* · **focus: decouple reactions**
- [ ] **D6 · Integrate** — refactor anemic → behaviour-rich model with tests staying green
- [ ] **D7 · Review** — aggregate design heuristics: smaller aggregates, reference by id, eventual consistency *inside* the model · *quiz + journal*

### Week 7 — Strategic DDD & module boundaries

- [ ] **W7D1 · Bounded contexts & context map** — Shipments, Dispatch, Tracking, Billing, Identity, Notifications; upstream/downstream, shared kernel, ACL → *context map diagram + ADR-007* · **focus: boundaries before technology**
- [ ] **W7D2 · Module anatomy in the monolith** — per-module Api/Application/Domain/Infrastructure, module registration, no cross-module EF navigation → *module template + Shipments refactor* · **focus: modular monolith**
- [ ] **W7D3 · Module communication rules** — published module contracts/events instead of reaching into another module; minimal shared kernel; enforced by arch tests → *fitness tests* · **focus: contracts between modules**
- [ ] **W7D4 · Extract the second module** — Tracking (positions, geofences, alerts) wired to Shipments by id + events → *Tracking module skeleton* · **focus: seams in practice**
- [ ] **W7D5 · The monolith → service seam** — what would break if Tracking became a service: data ownership, transactions, contracts, deployment, on-call cost → **ADR-008** · **focus: when NOT to distribute**
- [ ] **D6 · Integrate** — remove accidental coupling the module tests exposed; single module registration entry point
- [ ] **D7 · Review** — synchronous coupling, cross-module transactions, where idempotency becomes mandatory · *quiz + journal*

### Week 8 — Freight rules worth modelling properly

- [ ] **W8D1 · Shipment lifecycle state machine** — legal transitions, guards, reasons; illegal transition → 409; no nested if-chains → *state machine + tests* · **focus: make state explicit**
- [ ] **W8D2 · Assignment & capacity rules** — one active assignment per vehicle, weight/volume capacity, conflict detection, optimistic locking → *rules + concurrency tests* · **focus: invariants under concurrency**
- [ ] **W8D3 · Pricing & charges** — charge lines, accessorials, `Money` arithmetic and rounding rules; totals derived, never ad hoc → *pricing model + tests* · **focus: money correctness**
- [ ] **W8D4 · Stop sequencing & appointment windows** — ordering invariants, overlapping windows, time zones, `TimeProvider` for deterministic tests → *rules + tests* · **focus: time is a domain concern**
- [ ] **W8D5 · Exceptions & compensation** — delay/damage/refusal flows, who compensates what, complete audit trail → *exception model + handlers* · **focus: unhappy paths first**
- [ ] **D6 · Integrate** — domain review: move logic out of handlers into the model; kill leaky abstractions
- [ ] **D7 · Review** — phase gate: model coherent, boundaries enforced, invariants covered by tests · *quiz + journal*

**Phase gate 2:** context map + module map · aggregate invariants tested · module rules enforced by fitness tests · ADR-007/008 written.

---

## Phase 3 — CQRS, Read Models & the Mediator Layer (W9–W10)

*Architecture theme: separate the write model from the read model — but only as far as the measurements justify.*

### Week 9 — Mediator layer & vertical slices

- [ ] **W9D1 · Decide the mediator (revisit ADR-004)** — MediatR Community vs Wolverine vs FastEndpoints vs a hand-rolled dispatcher; spike + dispatch-overhead benchmark → *decision + spike* · **focus: what a mediator costs you**
- [ ] **W9D2 · Command slices (writes)** — one folder per use case: command + handler + validator + mapping; no god services → *`CreateShipment` slice* · **focus: cohesion per use case**
- [ ] **W9D3 · Query slices (reads)** — DTO projections via Dapper/EF, no aggregate loading for reads, reuse of paging/filters → *`GetShipmentsPaged` slice* · **focus: read/write asymmetry**
- [ ] **W9D4 · Cross-cutting pipeline behaviours** — validation, timing/logging, transaction, authorisation behaviours; behaviour ordering + short-circuit semantics → *behaviours + tests* · **focus: pipeline over decoration**
- [ ] **W9D5 · Errors at the boundary** — exceptions vs Result types; domain failure → `ProblemDetails` mapping table; no exceptions for flow control → *error taxonomy + tests* · **focus: failure as first-class**
- [ ] **D6 · Integrate** — slice template/conventions so new use cases stay consistent (template or conventions doc + analyzer)
- [ ] **D7 · Review** — when CQRS is overkill; read-your-writes pitfalls; designing UX for eventual consistency · *quiz + journal*

### Week 10 — Read models, search & reporting

- [ ] **W10D1 · Denormalised read models** — shipment list projection maintained by events; rebuild command; idempotent projectors → *read model + rebuild* · **focus: derived data is rebuildable**
- [ ] **W10D2 · KPI & dashboard aggregates** — status counts, on-time %, per-tenant KPIs; materialised views vs maintained tables → *KPI endpoints* · **focus: aggregate query cost**
- [ ] **W10D3 · Search architecture (keyword + vector)** — Postgres full-text/trigram, **pgvector hybrid retrieval (BM25 + vector) with reranking**, tenant-filtered; when an external search engine is still justified → *hybrid search endpoint + benchmark* · **focus: retrieval is not `LIKE`; hybrid beats either alone**
- [ ] **W10D4 · Export & reporting** — streaming CSV/Excel without memory blowup; background export + notification → *export job + tests* · **focus: streaming large results**
- [ ] **W10D5 · Projection lag & reconciliation** — lag metric, reconciliation job, alert thresholds, catch-up strategy → *lag metric + alert* · **focus: operating eventual consistency**
- [ ] **D6 · Integrate** — load test read paths; record read-model vs live-query latency side by side
- [ ] **D7 · Review** — phase gate: read/write separation justified with numbers, not preference · *quiz + journal*

**Phase gate 3:** mediator decision recorded · slice conventions followed · measured read-path comparison · projection lag observable.

---

## Phase 4 — Messaging, Sagas & Resilience (W11–W13)

*Architecture theme: event-driven integration — facts, contracts, at-least-once reality, and processes that outlive a request.*

### Week 11 — Broker, contracts and the publish path

- [ ] **W11D1 · Broker choice & topology** — RabbitMQ vs Azure Service Bus vs Kafka/SQS; exchanges/queues/routing keys, DLQ design, naming conventions → **ADR-009** + topology doc · **focus: broker semantics**
- [ ] **W11D2 · Messaging framework decision** — MassTransit v8 (free/unsupported) vs v9 (paid) vs Wolverine (MIT) vs raw client; migration cost → **ADR-010** + wiring · **focus: licence-aware engineering**
- [ ] **W11D3 · Integration contracts** — `FleetTrack.Contracts`: versioned messages, no domain types crossing, additive evolution rules, consumer-driven contract tests → *contracts package + tests* · **focus: schema evolution**
- [ ] **W11D4 · Publish path with the outbox** — publish after commit via an outbox relay; correlation/`traceparent` headers; message ids; fail-fast vs retry → *publisher + tests* · **focus: atomic state + message**
- [ ] **W11D5 · Consumer design & inbox** — idempotent consumer, inbox dedupe by message id, poison handling, DLQ replay tool → *consumer + inbox + tests* · **focus: effectively-once effects**
- [ ] **D6 · Integrate** — broker-down drill: prove no loss and no duplicates on recovery
- [ ] **D7 · Review** — choreography vs orchestration; event vs command naming · *quiz + journal*

### Week 12 — Sagas & long-running processes

- [ ] **W12D1 · Process design** — sequence diagram: assignment → driver acceptance → timeout → escalation; decide saga vs choreography → *design doc + diagram* · **focus: model the process first**
- [ ] **W12D2 · Implement the dispatch saga** — persisted state, optimistic concurrency, idempotent transitions → *saga + tests* · **focus: state that survives restarts**
- [ ] **W12D3 · Timeouts & scheduling** — delayed messages / recurring jobs, timeout policies, reminders, escalation paths → *timeout handling + tests* · **focus: time as an event**
- [ ] **W12D4 · Compensation** — release the vehicle, cancel the appointment, notify the customer; compensations must be idempotent → *compensation handlers + tests* · **focus: business rollback**
- [ ] **W12D5 · Operate the flow** — saga state endpoint, stuck-saga detection, dashboard, replay runbook → *ops view + runbook* · **focus: debugging distributed state**
- [ ] **D6 · Integrate** — failure-injection suite: crash mid-flow, duplicate delivery, out-of-order events
- [ ] **D7 · Review** — process manager, routing slip, event-carried state transfer — and their failure modes · *quiz + journal*

### Week 13 — Resilience & load control

- [ ] **W13D1 · Policy catalogue** — Polly v8 pipelines per dependency class (timeout, retry + jitter, circuit breaker, bulkhead) → *policies + tests* · **focus: failure isolation**
- [ ] **W13D2 · Backpressure & shedding** — bounded `Channels`/queues, concurrency limits, queue-depth metrics, 429 vs 503 semantics → *policy + load proof* · **focus: protect the core**
- [ ] **W13D3 · Rate limiting & quotas** — per-tenant/per-key limits, `Retry-After`, fairness under contention → *limiter + tests* · **focus: quotas are fairness**
- [ ] **W13D4 · Graceful degradation** — read-only mode, cached fallbacks, explicit health states, emergency feature flags → *degradation playbook* · **focus: degrade, don't die**
- [ ] **W13D5 · Chaos & failure drills** — kill broker/DB/cache, inject latency, partition the network; record behaviour before/after → *drill report* · **focus: prove the resilience story**
- [ ] **D6 · Integrate** — consolidate resilience configuration; delete ad-hoc try/catch and hand-rolled retries
- [ ] **D7 · Review** — phase gate: documented failure modes with recovery evidence · *quiz + journal*

**Phase gate 4:** outbox/inbox proven under broker outage · saga runs, times out and compensates · policy catalogue + chaos drill report.

---

## Phase 5 — Real-Time Tracking & Telemetry (W14–W16)

*Architecture theme: streaming ingestion, time-series and spatial data, and push architecture — the part of the domain that makes FleetTrack a tracking product rather than a CRUD app.*

### Week 14 — Telemetry ingestion

- [ ] **W14D1 · Ingestion contract & device model** — payload shape, device identity/keys, batch + timestamp semantics, clock skew, out-of-order pings → *ingestion API + validation* · **focus: contracts with unreliable devices**
- [ ] **W14D2 · High-throughput ingest path** — bounded `Channels`, batching, async persistence, idempotent upserts, no per-request round trip → *ingest service + throughput test* · **focus: batching & backpressure**
- [ ] **W14D3 · Time-series storage** — TimescaleDB hypertables, chunk/retention/compression policies, continuous aggregates vs partition-per-day → *telemetry schema + ADR-011* · **focus: volume changes the design**
- [ ] **W14D4 · Hot-read patterns** — last-known position per vehicle, track history, distance/dwell computations, caching of hot reads → *tracking endpoints + benchmarks* · **focus: read shapes differ from writes**
- [ ] **W14D5 · Geospatial layer** — PostGIS geography, geofence polygons, enter/exit detection, spatial indexes, distance/ETA basics → *geofence detection + tests* · **focus: spatial data in a relational store**
- [ ] **D6 · Integrate** — run ingest + read load together; tune chunk interval/retention with numbers
- [ ] **D7 · Review** — why "one row per ping in the normal table" fails (index bloat, vacuum, query cost) and what replaces it · *quiz + journal*

### Week 15 — Real-time push architecture

- [ ] **W15D1 · Transport decision** — polling vs SSE vs WebSockets/SignalR vs gRPC streaming: auth, resumption, fan-out cost → **ADR-012** · **focus: choose transport by interaction shape**
- [ ] **W15D2 · Hub design** — groups per tenant/shipment, connection lifecycle, `IUserIdProvider`, per-subscription authorisation → *hub + tests* · **focus: authorising a subscription**
- [ ] **W15D3 · Scale-out & backplane** — Redis/Azure SignalR backplane, stateless vs sticky, connection limits, monitoring → *backplane + load proof* · **focus: horizontal fan-out**
- [ ] **W15D4 · Event → push pipeline** — domain/integration events become notifications; coalescing, throttling, dedupe, ordering guarantees → *notifier + tests* · **focus: don't push raw firehose**
- [ ] **W15D5 · Subscriptions, presence & privacy** — who may watch which shipment (tenant + role), update rate limits, trimmed payloads → *authorisation matrix + tests* · **focus: least data, fewest watchers**
- [ ] **D6 · Integrate** — end-to-end drill: simulated device → ingestion → event → live push to a client
- [ ] **D7 · Review** — freshness vs consistency, cost of fan-out, reconnect/backfill strategy · *quiz + journal*

### Week 16 — Tracking features & algorithms

- [ ] **W16D1 · Geofence-driven state transitions** — arrival/departure events move shipment state (wired into the state machine + saga) → *integration + tests* · **focus: events meeting the domain**
- [ ] **W16D2 · ETA computation** — distance/speed model + historical dwell, caching, confidence range, when to call a routing provider → *ETA service + tests* · **focus: honest estimates**
- [ ] **W16D3 · Alerts & rules engine** — speeding/idle/dwell/route-deviation thresholds per tenant; rule storage + evaluation → *rules engine + alerts* · **focus: tenant-specific policy**
- [ ] **W16D4 · Alert delivery** — notifications via the worker, escalation, muting, full audit → *consumer + tests* · **focus: alert fatigue is a design flaw**
- [ ] **W16D5 · Event fan-out to modules** — Tracking → Shipments/Billing/Notifications contracts; document the event map → *event map + contracts* · **focus: event-carried state transfer**
- [ ] **D6 · Integrate** — refactor duplicated position queries into read models; measure again
- [ ] **D7 · Review** — phase gate: telemetry architecture defensible at the documented load · *quiz + journal*

**Phase gate 5:** ingestion benchmark (msg/s, p95) · live push demo from simulated device · geofence→state machine integration tested · ADR-011/012.

---

## Phase 6 — Caching, Performance & Scale (W17)

*Architecture theme: performance engineering with evidence — measure, hypothesise, change, re-measure, record.*

### Week 17 — Caching and measured performance

- [ ] **W17D1 · Cache topology & key design** — cache-aside vs read-through; tenant + version inside keys; TTL/staleness policy; stampede protection → *`ICacheService` + tests* · **focus: correctness lives in the key**
- [ ] **W17D2 · Invalidation & cross-instance consistency** — evict vs version-bump, pub/sub invalidation, write-through trade-offs → *strategy + tests* · **focus: invalidation is the hard part**
- [ ] **W17D3 · HTTP-level caching** — output/response caching, cache headers, `ETag` reuse, shared vs per-user caching → *output cache config* · **focus: cheapest wins first**
- [ ] **W17D4 · Measure before optimising** — BenchmarkDotNet, `dotnet-counters`/`dotnet-trace`, allocation hunting, query plans → *perf report #1* · **focus: evidence over intuition**
- [ ] **W17D5 · Load testing & capacity math** — k6/NBomber scenarios, SLOs (p95/p99, error rate), capacity estimate + per-tenant cost → *perf report #2 + SLO doc* · **focus: capacity arithmetic**
- [ ] **D6 · Integrate** — apply the top three findings, re-measure, record the deltas
- [ ] **D7 · Review** — latency budgets, tail latency, Amdahl, connection/thread-pool limits, cache cost · *quiz + journal*

**Phase gate 6:** before/after numbers for at least three optimisations · SLO document · cache invalidation verified by tests.

---

## Phase 7 — Security, Identity & Multi-Tenancy (W18–W19)

*Architecture theme: identity is delegated, authorisation is yours, isolation is proven — in code and in the database.*

### Week 18 — Identity & authorization

- [ ] **W18D1 · OIDC architecture** — Entra ID/Keycloak vs `MapIdentityApi` (learning only); flows (PKCE, client credentials), token validation pitfalls, JWKS caching → *auth wiring + ADR-013* · **focus: outsource identity**
- [ ] **W18D2 · Authorization model** — policy catalogue, resource-based authorisation (BOLA defence), roles vs scopes, tenant admin → *policy catalogue + tests* · **focus: object-level authz**
- [ ] **W18D3 · Machine clients & partner APIs** — API keys hashed at rest with rotation/scopes, HMAC request signing, replay protection (nonce/timestamp) → *key handler + tests* · **focus: authenticating devices and partners**
- [ ] **W18D4 · Secrets & key management** — user-secrets in dev → Key Vault + managed identity in prod; Data Protection key ring; rotation runbook → *secrets ADR + runbook* · **focus: secrets are architecture**
- [ ] **W18D5 · Threat model: OWASP API + OWASP LLM Top 10** — STRIDE-lite per endpoint group; BOLA/BFLA, mass assignment, injection, SSRF; **LLM risks: prompt injection (direct and via ingested documents), insecure output handling, excessive agency of tools/agents, data exfiltration through tool calls, unbounded consumption**; automated scanning (CodeQL, dependency audit) → *threat model doc incl. AI abuse cases* · **focus: attack the design, not the code**
- [ ] **D6 · Integrate** — security regression tests; headers (HSTS/CSP), CORS policy, CSRF strategy for cookie flows
- [ ] **D7 · Review** — token pitfalls, confused deputy, why client-side checks are UX only · *quiz + journal*

### Week 19 — Multi-tenancy & data isolation

- [ ] **W19D1 · Tenancy model decision** — shared schema + `TenantId` + RLS vs schema-per-tenant vs DB-per-tenant: isolation, cost, compliance, migration pain → **ADR-014** · **focus: isolation is a business requirement**
- [ ] **W19D2 · Enforce isolation in code** — tenant context from the token, EF global query filters, write guards, arch test forbidding `IgnoreQueryFilters()` outside Infrastructure → *filters + tests* · **focus: make the safe path the easy path**
- [ ] **W19D3 · Enforce isolation in the database** — Postgres RLS + session tenant variable, per-tenant indexes, ownership on telemetry tables → *migration + tests* · **focus: defence in depth**
- [ ] **W19D4 · Tenant lifecycle** — onboarding/provisioning, per-tenant configuration and feature flags, offboarding with data export/delete (GDPR) → *provisioning workflow* · **focus: tenant operations**
- [ ] **W19D5 · Noisy neighbour & fair usage** — per-tenant quotas, heavy-query protection, noisy-tenant detection and alerting → *quota enforcement + metrics* · **focus: fairness in a shared system**
- [ ] **D6 · Integrate** — isolation test matrix across API, database, cache, broker and search
- [ ] **D7 · Review** — phase gate: isolation proven by tests; per-tenant cost measured · *quiz + journal*

**Phase gate 7:** OIDC working end-to-end (API + machine client) · threat model · RLS + filters + isolation test matrix green · ADR-013/014 accepted.

---

## Phase 8 — Frontend Architecture (W20–W22)

*Architecture theme: the client is a system too — rendering model, state ownership, contracts, auth, realtime UX and measured budgets. (Default React 19 + TS + Vite; the Blazor/Angular variants map 1:1 onto these days — see ADR-015 and `agent.md` §4.14.)*

### Week 20 — Client foundations

- [ ] **W20D1 · Frontend architecture decision** — SPA vs SSR/SSG/hybrid; BFF or straight API; React vs Blazor vs Angular trade-offs → **ADR-015** + `web/` scaffold · **focus: rendering model first**
- [ ] **W20D2 · Type-safe API layer** — generated client, single `ProblemDetails` error mapper, auth interceptor with silent renew, cancellation/timeout → *api client + tests* · **focus: one place handles cross-cutting**
- [ ] **W20D3 · State architecture** — server state vs UI state, cache keys/invalidation, optimistic updates, no global store for server data → *query layer + devtools* · **focus: cache ownership**
- [ ] **W20D4 · Routing, layout & design system** — route structure, layouts, design tokens/theming, loading/empty/error patterns, a11y baseline → *app shell + component seed* · **focus: consistency by construction**
- [ ] **W20D5 · Forms & validation architecture** — schema validation, server error mapping to fields, multi-step shipment wizard, unsaved-changes guard → *create/edit flow* · **focus: server is the source of truth**
- [ ] **D6 · Integrate** — frontend CI: typecheck, lint, unit tests, build, preview deployment
- [ ] **D7 · Review** — SSR/CSR/SSG trade-offs, hydration, request waterfalls · *quiz + journal*

### Week 21 — Shipment operations UI (the product)

- [ ] **W21D1 · Shipment list & operations dashboard** — server paging/filters/sorting reflected in the URL, saved views, bulk actions, keyboard access → *list + dashboard* · **focus: URL is state**
- [ ] **W21D2 · Shipment detail & timeline** — status event timeline, documents, charges, assignment panel, deep links → *detail page* · **focus: make state visible**
- [ ] **W21D3 · Document handling** — upload with progress/retry via pre-signed URLs, preview, download audit trail → *documents UI* · **focus: never proxy megabytes**
- [ ] **W21D4 · Live tracking map** — MapLibre/Leaflet + SignalR updates, trail rendering, geofence overlays, clustering, many-point performance → *live map* · **focus: realtime UI budgets**
- [ ] **W21D5 · Dispatch board** — drag/drop assignment, 409 conflict feedback, optimistic UI with rollback → *dispatch board* · **focus: optimistic UI needs an exit**
- [ ] **D6 · Integrate** — performance pass: route splitting, bundle budget, map memory, Lighthouse
- [ ] **D7 · Review** — rendering cost, list virtualisation, offline/reconnect expectations · *quiz + journal*

### Week 22 — Client quality & testing

- [ ] **W22D1 · Component & behaviour tests** — Testing Library/bUnit, MSW/fake handlers matching the real contract, accessible queries → *test suite* · **focus: test behaviour, not internals**
- [ ] **W22D2 · End-to-end tests** — Playwright against the containerised stack, authenticated flows, resilient selectors, trace artifacts in CI → *E2E suite* · **focus: the only test that proves a journey**
- [ ] **W22D3 · Accessibility & localisation** — keyboard/ARIA audit, contrast, i18n + formatting (dates, weights, currency) → *a11y report + i18n wiring* · **focus: accessibility is correctness**
- [ ] **W22D4 · Client observability** — error boundaries, web OTel, correlation id propagation into API traces → *client telemetry + trace proof* · **focus: one trace, client to DB**
- [ ] **W22D5 · Offline/PWA slice** — service worker, offline action queue, conflict resolution on reconnect → *offline-capable slice* · **focus: designing for disconnection**
- [ ] **D6 · Integrate** — full-stack regression: E2E + API + realtime
- [ ] **D7 · Review** — phase gate: a complete journey ships, is tested and is deployed to a preview env · *quiz + journal*

**Phase gate 8:** ops console usable end-to-end (create → assign → track → documents) · E2E green in CI · Lighthouse/bundle budget enforced.

---

## Phase 9 — Observability & Operations (W23)

*Architecture theme: you cannot operate what you cannot see — design the signals, then the alerts, then the runbooks.*

### Week 23 — Observability as a design activity

- [ ] **W23D1 · Structured logging as a design tool** — event ids, scopes, tenant/correlation context, PII scrubbing, sampling rules → *logging conventions + Seq/OTel collector* · **focus: logs are events, not strings**
- [ ] **W23D2 · Distributed tracing end-to-end** — OTel spans across HTTP → handler → EF → broker → consumer → SignalR; baggage propagation; trace↔log linking → *trace of one full journey* · **focus: one trace across boundaries**
- [ ] **W23D3 · Metrics & dashboards (incl. AI)** — RED/USE metrics plus domain metrics (ingest lag, saga duration, cache hit rate, DLQ depth, projection lag) **and GenAI metrics: tokens in/out, cost per tenant/feature, model+prompt version, tool-call latency/error rate, eval scores over time, refused/grounded answers** → *3 Grafana dashboards* · **focus: measure what explains incidents — including the AI spend**
- [ ] **W23D4 · SLIs, SLOs & alerting** — define "working", symptom-based alerts with burn rates, every alert links to a runbook → *SLO doc + alert/runbook set* · **focus: alerts must be actionable**
- [ ] **W23D5 · Operability by design** — saga/queue/DLQ admin views, safe replay/requeue tooling, dashboards for tenant admins → *ops endpoints + tooling* · **focus: build the tools you'll need at 3 a.m.**
- [ ] **D6 · Integrate** — replay a past failure drill with observability: reconstruct the incident from traces/metrics/logs
- [ ] **D7 · Review** — metric cardinality and cost, alert fatigue, sampling trade-offs · *quiz + journal*

**Phase gate 9:** one journey traceable end-to-end · SLO + alerts live · a written runbook you'd hand to a colleague.

---

## Phase 10 — Cloud Architecture, IaC & Delivery (W24)

*Architecture theme: reproducible environments and safe change — infrastructure as code, deploys decoupled from migrations, progressive delivery, and cost as a design metric.*

### Week 24 — Cloud platform, IaC and pipelines

- [ ] **W24D1 · Containerisation & runtime behaviour** — multi-stage builds, non-root, health/liveness probes, resource limits, graceful shutdown, config from environment/Key Vault → *Dockerfiles for API, worker and web* · **focus: the process must die well**
- [ ] **W24D2 · Platform decision** — Container Apps vs AKS vs App Service: scaling model, networking, secrets, cost, ops burden → **ADR-016** · **focus: match platform to ops capacity**
- [ ] **W24D3 · Infrastructure as code** — Bicep/Terraform for dev/staging/prod parity, parameterisation, remote state, review process → *IaC modules + environments* · **focus: no click-ops**
- [ ] **W24D4 · Delivery pipeline & zero-downtime change** — build → test → scan → push → deploy; expand/contract migrations; revisions/traffic shifting; rollback plan → *CD workflow + runbook* · **focus: deploy ≠ migrate**
- [ ] **W24D5 · Production data operations** — PITR backups, tested restore drill, retention/archival, secret rotation, capacity/cost review → *DR drill report + cost estimate* · **focus: an untested backup is a hope**
- [ ] **D6 · Integrate** — deploy the whole stack to staging; smoke tests in the pipeline; verify telemetry from the deployed system
- [ ] **D7 · Review** — blast radius, progressive delivery, migration safety, cost per tenant · *quiz + journal*

**Phase gate 10:** one command deploys to staging from IaC · smoke tests gate the deploy · restore drill documented with timings.

---

## Phase 11 — System Design, Capstone & Staff Skills (W25–W26)

*Architecture theme: turning a working system into an explainable one — design documents, diagrams, ADRs, trade-off framing and a hardening pass.*

### Week 25 — System design practice (grounded in FleetTrack)

- [ ] **W25D1 · Case: high-throughput telemetry ingestion** — requirements → capacity estimate → design → failure modes → trade-offs (batching, Timescale, backpressure) → *design doc #1* · **focus: quantify before designing**
- [ ] **W25D2 · Case: event-driven shipment pipeline with saga** — ordering, duplication, timeouts, compensation, visibility → *design doc #2* · **focus: at-least-once by design**
- [ ] **W25D3 · Case: multi-tenant SaaS isolation & noisy neighbours** — isolation vs cost vs compliance; per-tenant quotas and telemetry → *design doc #3* · **focus: economics of tenancy**
- [ ] **W25D4 · Case: real-time tracking at 100k concurrent watchers** — fan-out, coalescing, backplane, cost per watcher → *design doc #4* · **focus: fan-out cost modelling**
- [ ] **W25D5 · Case: batch billing & reconciliation** — late data, corrections, idempotent runs, auditability → *design doc #5* · **focus: financial correctness**
- [ ] **D6 · Integrate** — red-team your own designs: apply the "when NOT" rules and correct them in place
- [ ] **D7 · Review** — design-interview framing: requirements → constraints → options → decision → risks → monitoring · *quiz + journal*

### Week 26 — Capstone hardening & portfolio

- [ ] **W26D1 · Capstone scope** — tenant onboarding + live tracking + billing summary slice; acceptance criteria + demo script → *capstone plan* · **focus: finish something end-to-end**
- [ ] **W26D2 · Hardening pass** — security, performance, accessibility, error budgets, dead-code removal → *hardening list closed* · **focus: quality is a phase, not a hope**
- [ ] **W26D3 · Architecture documentation** — 12–16 ADRs, C4 context/container/component diagrams, "why not" section → *docs/architecture/* · **focus: explainability is the deliverable**
- [ ] **W26D4 · .NET 11 upgrade day** — apply the LTS/STS policy in practice: upgrade, run fitness + E2E tests, document breaking changes → *upgrade PR + delta note* · **focus: upgrades are routine, not events**
- [ ] **W26D5 · AI-readiness review** — audit existing flows before Phase 12: are use cases tool-shaped (explicit contracts, idempotent, tenant-authorised)? Is every action auditable? Which AI use cases are genuinely worth building — and which are just a `SELECT`? → *AI-readiness checklist + candidate use-case list* · **focus: prepare the seams before adding a model**
- [ ] **D6 · Integrate** — documentation set: README, runbooks, onboarding guide, demo script
- [ ] **D7 · Review** — retrospective: what to keep, what to drop, the next 90 days; publish the portfolio · *quiz + journal*

**Phase gate 11:** 5 design docs + 20+ ADRs + diagrams · capstone demo runs from a clean clone · upgrade to .NET 11 complete.

---

## Phase 12 — AI, LLM & MCP Architecture (W27–W29)

*Architecture theme: models are an **edge** concern, never a domain concern — put them behind contracts, keep authorisation and audit deterministic, and measure quality, latency and cost like any other dependency.*

### Week 27 — LLM integration foundations

- [ ] **W27D1 · Where AI belongs in the architecture** — the assistant is an adapter over existing use cases (tools); the deterministic core stays authoritative; every AI path has a kill switch → **ADR-017 AI integration boundary** · **focus: AI at the edge, never in the domain**
- [ ] **W27D2 · Provider abstraction** — `Microsoft.Extensions.AI` (`IChatClient`/`IEmbeddingGenerator`); Azure OpenAI/Foundry vs OpenAI vs **local Ollama**; streaming, cancellation, timeouts, retries, model routing/fallback; keys via Key Vault → **ADR-018 provider strategy** · **focus: no provider lock-in**
- [ ] **W27D3 · Prompt & context engineering** — system prompts as versioned files in git, context assembled from read models, few-shot examples, explicit **token budgets** (truncate/summarise), temperature and schema policy per use case → *prompt library + token budget table* · **focus: context is a budgeted resource**
- [ ] **W27D4 · Tool calling over your own use cases** — strict JSON schemas, tenant authorisation enforced *inside* the tool, idempotency keys, an approval gate for writes, audit of every invocation → *tool catalogue + authorisation matrix* · **focus: the model proposes, the domain disposes**
- [ ] **W27D5 · Structured outputs & validation** — schema-constrained responses, validation + repair retry, hallucination containment, deterministic fallback and an explicit "I can't answer that" path → *contract tests + fallback tests* · **focus: validate model output like any input**
- [ ] **D6 · Integrate** — assistant host behind a feature flag + kill switch; every model call traced (prompt version, model, tokens, latency)
- [ ] **D7 · Review** — quiz + when NOT to use an LLM (pricing, compliance, routing maths, anything a `SELECT` answers) · *quiz + journal*

### Week 28 — MCP: using it, and exposing FleetTrack through it

- [ ] **W28D1 · MCP fundamentals** — host/client/server roles, transports (stdio vs HTTP/SSE), tools vs resources vs prompts, capability negotiation and versioning; where MCP beats a bespoke plugin API → **ADR-019 MCP adoption** · **focus: one standard protocol for tool access**
- [ ] **W28D2 · Using MCP in the development workflow** — connect the coding agent to MCP servers (git/GitHub, read-only Postgres, OpenAPI, Aspire dashboard, docs search); least-privilege scopes; explicitly **deny** production data and write tokens; commit a reproducible agent setup (`AGENTS.md` + `.mcp.json`) → *agent setup + scope policy* · **focus: how to *use* MCP day to day**
- [ ] **W28D3 · Exposing FleetTrack as an MCP server** — start with read use cases (`get_shipment`, `list_delayed_shipments`, `get_last_position`, `search_documents`), add write tools behind approvals, publish read-only projections as resources, authenticate per user/tenant → **ADR-020 MCP exposure & authz** · **focus: how to *integrate* MCP into the product**
- [ ] **W28D4 · MCP security & operations** — tool poisoning and untrusted content, confused-deputy risks, over-broad scopes, prompt injection arriving *through* tool output, rate limits + cost caps, invocation audit trail, tool-contract versioning → *MCP threat model + guardrails* · **focus: every tool is an attack surface**
- [ ] **W28D5 · Tool/prompt evals & release gates** — golden task suite (tool selection, arguments, refusals, tenant boundaries), CI regression gate, prompt/tool version registry + rollback → *eval suite + registry* · **focus: change prompts like you change code**
- [ ] **D6 · Integrate** — end-to-end demo: MCP client (agent) → FleetTrack MCP server → authorised use case → approval gate → audited action
- [ ] **D7 · Review** — quiz + MCP design trade-offs (one server vs many, scopes, MCP vs plain HTTP) · *quiz + journal*

### Week 29 — Retrieval, agents & AI operations

- [ ] **W29D1 · RAG architecture on Postgres** — chunking per document type (BOL/POD/notes), embeddings in `pgvector`, tenant/shipment/date metadata filters, **hybrid search + reranking**, chunk lifecycle and re-embedding when the model changes → **ADR-021 retrieval design** · **focus: retrieval is a data problem**
- [ ] **W29D2 · Grounding, citations & freshness** — answer only from retrieved context, cite document + section, show data freshness, refuse when evidence is weak, capture user feedback → *grounded answer contract + citation UI* · **focus: no evidence, no claim**
- [ ] **W29D3 · Agentic workflows with human gates** — multi-step plans over tools, approval gates for money/status-changing actions, step/timeout/cost ceilings, deterministic authorisation, idempotent effects, stuck-agent handling → **ADR-023 agentic scope** · **focus: autonomy is a dial, not a switch**
- [ ] **W29D4 · Evals & quality gates** — golden datasets, deterministic checks + rubric/LLM-as-judge, retrieval metrics (recall@k, groundedness), CI thresholds, regression dashboards, red-team prompts → **ADR-022 eval & release policy** · **focus: ship on evidence, not vibes**
- [ ] **W29D5 · AI operations & cost** — token/cost budgets per tenant with alerting, latency budgets, PII redaction, content safety, model/prompt rollback runbook, incident playbook for bad answers, documentation of what the AI may and may not do → *AI runbook + cost dashboard* · **focus: run AI like production**
- [ ] **D6 · Integrate** — wire the eval gate into CI and the cost/quality dashboards into observability; finalise the runbook
- [ ] **D7 · Review** — quiz + AI trade-offs (build vs buy, hosted vs local, cost per tenant, when to *remove* an AI feature) · *quiz + journal*

**Phase gate 12:** assistant + MCP server in the repo · ≥1 eval suite gating CI · per-tenant cost caps + dashboards · AI threat model + runbook · ADR-017…023 accepted.

---

## Adapting this plan (rules, not vibes)

1. **Reorder freely, delete deliberately.** Skipping a day is allowed only if the artifact genuinely adds no value — write a one-line note in `agent.md` §8.
2. **Every change of stack/approach = an ADR row** in `agent.md` §5 (context → options → decision → consequences → revisit trigger).
3. **If a phase runs long, cut scope, not quality gates.** Phase gates (tests, ADRs, measurements) are the point of the plan.
4. **Frontend alternatives:** swap Phase 8 to Blazor or Angular by rewriting ADR-015 and the Phase 8 day titles — every other phase is unaffected.
5. **Licence rule:** before adopting a library, check its production licence and record it (see ADR-004/005/010).
6. **Journal discipline:** after each day, two lines — what you built, what surprised you. That journal is your interview material.
7. **AI features follow the same rules as any other dependency:** ADR first; evals, cost caps and a kill switch before shipping; and never let a model own a decision the domain owns (pricing, legality, authorisation).
8. **MCP scope:** dev-workflow servers stay read-only and least-privilege; product-facing MCP tools use the *same* authorisation, idempotency and audit paths as the HTTP API.
9. **Retrieval is data work:** if the answer is structured, use SQL. RAG only for genuinely unstructured content — and measure retrieval quality, don't trust it.
