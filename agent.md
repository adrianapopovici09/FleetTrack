# FleetTrack — Agent & Study File

> **What this is.** The working file for the FleetTrack build *and* my study companion: architectural guardrails, the architecture syllabus (**including AI/LLM/MCP**), the domain model, the ADR register, the **quiz + answer key with worked examples for every day**, status snapshot and session log.
> **The plan:** [`docs/plans/plan.md`](docs/plans/plan.md) — 29 weeks · 145 build days, checkbox tracker (one plan, no versions).
> Project overview and quick start: [`README.md`](README.md). Rule: this file is read first and updated last in every session.

---

## 1. How to study and build (the daily loop)

One session = one day from `plan.md`, ≈ 2–3 focused hours:

1. **Pick up:** open `plan.md`, take the first unchecked day.
2. **Prime (5 min):** read the day's *focus* concept in §4 below and write one line in the journal: *"this matters because …"*. Never skip this — it's what turns typing into learning.
3. **Build:** produce the day's artifact. Keep tests running (`dotnet test`) and CI green.
4. **Self-quiz (10 min):** answer that day's questions in §7 **without looking at the answers**, then read them plus the worked example. If you can't state the *trade-off* and *when NOT to use it*, the day isn't done.
5. **Record (D6/D7 only):** write/append the ADR, update the ADR register (§5), update the session log (§8).
6. **Close the loop:** flip the checkbox in `plan.md`; note any plan deviation — a deviation only counts once it's an ADR.

**How to use me as the agent:** ask for any of these, at any time —
`quiz me on week 6` (I ask, you answer, I grade) · `review my ADR-007` · `critique this design/diagram` · `find the leak in this slice` · `what would break if Tracking became a service?` · `give me a harder variant of this exercise` · `explain X with a FleetTrack example`.

**Anti-patterns for this plan:** bingeing "learning" days with no artifact · skipping D6/D7 · adding a framework before an ADR · optimising before a measurement · letting repo hygiene rot in the frontend too · **letting an agent (or an AI feature) touch the domain without guardrails** — no writes without approval gates, no AI feature without evals, cost caps and a kill switch.

---

## 2. Snapshot

| Item | Value |
| --- | --- |
| Repo root | `d:\Work\FleetTrack` |
| Remote / branch | `origin` → github.com/adrianapopovici09/FleetTrack · branch `main` |
| Plan | `docs/plans/plan.md` — 29 weeks · 145 build days · **current position: Phase 0 · W1D1** |
| Solution | `FleetTrack/FleetTrack.slnx` → will move to `src/` layout in W1D2 |
| Project today | `FleetTrack/FleetTrack/FleetTrack.csproj` — ASP.NET Core minimal API, `net10.0` |
| Stack target | .NET 10 LTS · modular monolith · Postgres 17 (+Timescale/PostGIS/**pgvector**) · RabbitMQ · Wolverine · Redis · SignalR · Aspire 13 · React 19+TS · Azure Container Apps + Bicep · OTel |
| AI stack | `Microsoft.Extensions.AI` (`IChatClient`/`IEmbeddingGenerator`) over Azure OpenAI/Foundry · **Ollama** for local/offline dev · **MCP** (consume dev servers *and* expose FleetTrack tools) · pgvector hybrid retrieval · eval suites in CI · per-tenant token/cost caps · prompt/model version registry |
| Open decisions (ADRs to write) | 004 mediator/tooling · 009 broker · 010 messaging framework · 013 identity provider · 015 frontend stack · **017–023 AI/MCP/retrieval/evals/agentic scope** |
| Ignored by git | `bin/`, `obj/`, `output/`, `.vs/`, `*.user`, test/coverage output, `node_modules/`, `dist/`, IaC state (see `.gitignore`) |
| Cadence | 5 build days + D6 integrate + D7 review per week |

> **Assumed default (flip it with ADR-015 if you disagree):** frontend = **React 19 + TypeScript + Vite**. Blazor (single-language, faster to first UI) and Angular (enterprise .NET shops) are documented alternatives in the syllabus §4.11; only Phase 8 day titles change.

---

## 3. Architectural principles (guardrails that must not break)

1. **Dependency rule.** `Domain` depends on nothing; `Application` defines ports (interfaces) and use cases; `Infrastructure` implements ports; `Api` composes. Any arrow pointing the wrong way is a bug.
2. **No framework type crosses a boundary** — no `DbSet`, `HttpContext`, `IMediator`, broker or SignalR types in `Domain`/`Application` signatures.
3. **Every write is a use case** (command/slice). Endpoints, EF configuration and controllers contain no business rules.
4. **Aggregate = consistency boundary.** One transaction touches one aggregate (plus its outbox row). Cross-aggregate rules are eventually consistent.
5. **Modules talk through contracts**, never by reading another module's tables or entities.
6. **Idempotency wherever a retry is possible** — messaging consumers, webhooks, telemetry ingestion, `Idempotency-Key` POSTs.
7. **Isolation is proven, not promised:** tenant filter in code + Postgres RLS + architecture tests + an isolation test matrix (API/DB/cache/broker/search).
8. **Observability is part of "done":** each new flow ships with a trace, a metric and structured logs.
9. **Every architectural change gets an ADR** (context → options → decision → consequences). No silent plan drift.
10. **Measure before optimising**, and write the numbers in the journal; "feels faster" is not evidence.
11. **Guardrails are executable:** NetArchTest suites run in CI and fail the build on violation.
12. **Licence check before adoption** (MediatR/AutoMapper/MassTransit are commercial for for-profit production; Wolverine is MIT) — see ADR-004/010.

---

## 4. Architecture syllabus (the concept behind each phase)

Read the relevant theme *before* the build day. Each theme ends with the same questions: **what problem does it solve, what does it cost, and when is it the wrong choice?** — those are the answers a senior engineer is expected to give out loud.

### 4.1 Architecture styles — choosing deliberately

| Style | Use when | Avoid when | Cost it adds | In FleetTrack |
| --- | --- | --- | --- | --- |
| Layered (N-tier) | Small team, CRUD-ish, short life | Domain logic is complex or long-lived | Anemic model, layer churn | Only as a first cut (W2) |
| Hexagonal / ports & adapters | External systems change (DB, broker, partner APIs) | Tiny app with one adapter | Indirection for every port | Core shape from W2D1 |
| Clean / onion | Domain must outlive frameworks; testability matters | Team won't respect the dependency rule | Build discipline, arch tests | Our internal structure |
| Vertical slices | Feature throughput matters, few cross-cutting rules | Heavy shared domain invariants | Duplication unless harnessed | Use-case folders (W9) |
| Modular monolith | **Default for this project** — one deployable, real boundaries | Modules genuinely need different scaling/teams/blast radius | Discipline to keep modules honest | Whole app (W7D2) |
| Microservices | Independent scaling/ownership/isolation, mature ops | You have one team and no SLO pressure | Network, data, ops, debugging, cost | Explicitly rejected until ADR-008 triggers |
| Serverless/Functions | Spiky, event-driven glue, per-request cost model | Long-running work, cold-start-sensitive latency | Local dev + observability complexity | Only for webhook receivers/exports (W24D2) |

**FleetTrack rule:** monolith first; split on *pressure evidence* (team size, scaling profile, compliance boundary), never on fashion. Write the trigger list in ADR-008.

### 4.2 The dependency rule & how it's enforced

- Arrows point inward: `Infrastructure → Application → Domain`; `Api → Application` (+ composition of Infrastructure). Nothing points at Infrastructure except `Api`'s composition root.
- Ports are owned by the **caller side** (Application), not by the implementation — otherwise swapping the DB forces changes in the domain.
- Enforcement is mechanical, not cultural: project references + **NetArchTest fitness functions** in CI (domain purity, no framework types in signatures, module isolation, no `IgnoreQueryFilters()` outside Infrastructure).
- What breaks it in practice: `IQueryable` in signatures, DTOs reused as entities, `IConfiguration`/`HttpContext` injected into handlers, "temporary" shared projects.

### 4.3 Strategic DDD — boundaries first

- **Bounded context:** where a word means exactly one thing. In FleetTrack: Shipments (the commercial agreement), Dispatch (planning/assignment), Tracking (positions, geofences, alerts), Billing (charges, invoices), Identity (users/tenants), Notifications.
- **Context map:** how they relate — customer/supplier, conformist, **anti-corruption layer** for partner/EDI feeds, shared kernel kept deliberately tiny (ids, `Money`, `TrackingNumber`).
- **Core vs supporting vs generic:** Shipments/Dispatch/Tracking are core (build and own them); Identity/Notifications/Billing integration are generic (buy or standardise).
- **Heuristic:** you cannot validate a boundary on a whiteboard — walk one end-to-end business story (book → assign → pickup → deliver → invoice → pay) and see where the language changes.

### 4.4 Tactical DDD — aggregate design

- Aggregate = the **consistency boundary**, not "a related set of tables". One transaction, one aggregate (+ its outbox row).
- Reference other aggregates **by id**, never by navigation property — a mutable object graph across aggregates is how you lose invariants.
- Small aggregates: `Shipment` + `ShipmentStop`/`ShipmentItem` change together; `Vehicle`/`Driver`/`Customer` are separate aggregates referenced by id.
- Invariants live in the aggregate root's methods (`shipment.Assign(vehicle, at)`) — validity is expressed by the method's preconditions, not by callers remembering rules.
- Value objects replace primitive obsession: `TrackingNumber`, `Address`, `Weight`, `Money`, `DateRange`. Immutable, validated in the constructor, equality by value — no `string trackingNumber` parameters floating around.
- **When an anemic model is fine:** genuine CRUD with no rules that must hold across fields (e.g. `TenantSettings`). Don't perform DDD theatre on it.

### 4.5 CQRS & read models

- **Three levels, pick the cheapest that works:** (1) same model, different methods; (2) separate query DTOs/projections over the same tables; (3) separate read store maintained by events. FleetTrack uses (2) everywhere and (3) only where lists/KPIs get expensive (W10).
- **Reads never load aggregates.** A query returning 50 shipment rows must issue one SQL projection, not 50 aggregate rehydrations.
- **Read models are derived data:** they must be rebuildable from scratch (write the rebuild command *before* you need it) and projectors must be idempotent.
- **Costs:** eventual consistency (stale UI moments — design the UX), double code paths, projection lag you must monitor, extra storage.
- **When NOT to use CQRS:** small CRUD admin screens, low-traffic endpoints, or when the "read model" is just a different `SELECT` — that's level 2, not a whole architecture.

### 4.6 Event-driven architecture, outbox & idempotency

- **Event vs command:** commands are addressed to one handler and can be rejected; events are facts that already happened and are broadcast. Never name an event `AssignVehicle` — name the fact (`VehicleAssigned`).
- **Choreography vs orchestration:** choreography = modules react to facts (low coupling, hard to see the end-to-end flow); orchestration = one coordinator drives the steps (visible, but it becomes a god-object). Use choreography inside the monolith first; reach for a saga when a *multi-step business process with timeouts/compensation* appears (W12).
- **Outbox pattern:** publishing to a broker inside a DB transaction is impossible to make exactly-once; so write the message to an outbox row in the same transaction and let a relay publish it. **Inbox pattern** = dedupe incoming messages by `MessageId` so retries don't double-apply.
- **Delivery semantics reality:** at-least-once delivery + idempotent consumers = effectively-once business effect. Design *consumers* to be replayable, not the broker to be magic.
- **Poison messages:** bounded retries → dead-letter queue → alert → replay tool. A DLQ nobody watches is data loss with extra steps.
- **Contract evolution:** additive changes only, version field, consumer-driven contract tests (W11D3).

### 4.7 Sagas & long-running workflows

- A saga is a **state machine persisted in the database** that reacts to messages/events and calls out with commands, with timeouts and compensations.
- FleetTrack process (W12D2): `VehicleAssigned` → notify driver → await acceptance or timeout → escalate to dispatcher → (re)assign. Each step is idempotent; the timeout is a first-class branch, not an afterthought.
- **Compensation ≠ rollback:** in business terms it's a new fact ("assignment released"), never an undo of an event that already happened.
- Costs: extra state, harder debugging, optimistic-concurrency conflicts in the saga store, "stuck saga" operational concerns (build the stuck-saga view in W12D5).
- **When NOT to use a saga:** 2–3 synchronous steps with no waiting, or when a single transaction can express the rule — don't distribute a problem you can keep local.

### 4.8 Real-time, streaming & telemetry architecture

- **Transport choice (W15D1):** polling (simple, wasteful) → SSE (one-way, cheap, HTTP-friendly) → WebSockets/SignalR (bidirectional, groups, reconnection semantics) → gRPC streaming (service-to-service, typed). FleetTrack uses SSE for read-only tickers and SignalR for the live map/dispatch board.
- **Fan-out is the real cost:** N watchers × M updates/second. Coalesce (send latest position, not every position), throttle per group, and push *deltas* rather than full payloads.
- **Ingestion is not CRUD:** device fleets retry, batch, arrive out of order, and can be offline for hours. Design for idempotent upserts, bounded queues (`System.Threading.Channels`), and *backpressure* (reject/429 before unbounded memory growth).
- **Time-series storage:** one row per ping in a normal table dies by indexing cost and vacuum churn; TimescaleDB hypertables + chunk/retention/compression policies (or partition-per-day) keep it cheap, and continuous aggregates pre-compute distance/dwell.
- **Spatial:** PostGIS geography types + spatial indexes for geofences; enter/exit detection is an event source (which then drives shipment state — architecture meeting business).
- **Freshness vs consistency:** "last known position" caching, ETA staleness, and what the UI shows when data is 30 s old are product decisions — document them.

### 4.9 Data & storage topology

- **Start with one Postgres and defend it:** relational + JSONB + full-text + PostGIS + Timescale covers ~95% of this domain. Every extra store doubles ops, backup, consistency and debugging cost.
- **Add a store only for a measured reason:** time-series volume (Timescale), text relevance/ranking at scale (search engine), massive blob volume (object storage), hot-read latency (cache).
- **Data ownership follows module boundaries:** one writer per table; other modules read through contracts/projections/event-carried state — never by joining another module's tables.
- **Migrations are code:** reviewed, reversible-of-intent, run by the pipeline; production uses expand/contract (add nullable → backfill → switch → drop) so deploy and migration are independent (W24D4).
- **Derived data is disposable:** read models, caches and projections must be rebuildable; if you can't rebuild it, it's a source of truth and belongs in the model.

### 4.10 Caching & performance engineering

- **Cache topology:** in-process (fast, per-instance, stale risk), distributed Redis (shared, still stale), output/response cache (HTTP-level, cheap wins), CDN (static/edge). Choose by data volatility and blast radius, not by habit.
- **Key design carries correctness:** include tenant + user + version in the key; version the key to invalidate (`shipments:v3:tenant:list`), don't hunt for keys to delete.
- **Cache-aside is the default; stampede protection (single-flight/lock) and TTL jitter are the parts people skip** and then blame Redis for.
- **Method:** measure (BenchmarkDotNet for code, `dotnet-trace`/counters for runtime, `EXPLAIN ANALYZE` for SQL, k6 for system) → one hypothesis → one change → re-measure → record. Keep a `docs/perf/` journal.
- **Think in budgets:** p95 latency budget per call, tail latency (p99) as the user-visible truth, connection-pool and thread-pool limits before "add more instances".

### 4.11 Security architecture

- **AuthN vs AuthZ:** identity comes from the IdP (OIDC), authorisation is *your* policy decision (`RequirePolicy`, resource-based handlers). Never trust a role claim to imply object-level access — that's BOLA, the #1 API vulnerability.
- **Flows:** SPA → Authorization Code + PKCE (no client secret in a browser); machine clients → client credentials or API keys + HMAC; validate issuer/audience/lifetime, cache JWKS, tolerate clock skew, never accept `alg: none`.
- **Defence in depth for tenancy:** token claim → application filter → database RLS. Every layer alone is a single mistake away from a cross-tenant leak, and only the DB layer survives a missing `WHERE`.
- **Secrets:** user-secrets in dev, Key Vault + managed identity in prod, Data Protection key ring persisted, no secret ever in git or in an image.
- **Threat model early (STRIDE-lite per endpoint group):** spoofing (tokens), tampering (signed webhooks/device payloads), repudiation (audit trail), information disclosure (tenant isolation, PII scrubbing), DoS (rate limits, request size limits, quotas), elevation (role/policy matrix).
- **Costs:** auth is a cross-cutting concern — put it in one place (policy catalogue + pipeline behaviour), or it will drift into 40 endpoints with 40 slightly different checks.

### 4.12 Observability architecture

- **Three pillars, one correlation:** structured logs (events + context), traces (spans across HTTP → handler → EF → broker → consumer → SignalR), metrics (numbers over time). Correlation id/`traceparent` must survive the broker boundary.
- **Instrument the domain, not just the framework:** saga duration, ingestion lag, cache hit rate, projection lag, DLQ depth, per-tenant request cost — these are the numbers that explain incidents.
- **SLIs → SLOs → alerts:** define what "working" means (success rate, p95 latency, freshness lag) and alert on **symptom burn rate**, not on CPU. Every alert links to a runbook with a first action.
- **Cost control:** metric cardinality is a budget (never label by `shipmentId`), sample verbose logs, and export traces with head/tail sampling rules.
- **Local parity:** Aspire dashboard (traces/logs/metrics + MCP access) so the same signals exist on a laptop as in staging.

### 4.13 Cloud & deployment topology

- **Container first, orchestrator second:** a small, non-root, multi-stage image + health probes + graceful shutdown is 80% of cloud readiness. Then choose Container Apps (simplest, revisions/traffic splitting), AKS (control, cost at scale, ops burden) or App Service (least infrastructure-shaped).
- **Twelve-factor habits that actually bite:** config from environment, logs to stdout, stateless instances, disposability (any instance can die), dev/prod parity, admin processes as one-off tasks.
- **IaC or it didn't happen:** Bicep/Terraform describes environments; click-ops drifts and can't be reviewed. Keep `.terraform.lock.hcl` in git, state in a remote backend.
- **Deploy ≠ migrate:** run schema changes as separate, backward-compatible steps (expand/contract) so old and new code can run simultaneously during a rolling deploy.
- **Progressive delivery:** revisions/slots + smoke tests + traffic shifting + a written rollback. "Redeploy the previous image" is a plan only if it includes data compatibility.
- **Cost is an architecture metric:** per-tenant cost, idle vs peak, autoscaling rules that match the load shape (telemetry is spiky and constant; billing is bursty). Record an estimate before scaling out.
- **DR basics:** PITR backups + a *tested* restore into staging, retention/archival policy, secrets rotation, and a documented RTO/RPO that a real incident could meet.

### 4.14 Frontend architecture (client-side)

- **Rendering decision first:** CSR/SPA (rich, long-lived sessions, internal apps), SSR (public pages, SEO, fast first paint), SSG/ISR (marketing/docs), hybrid (public SSR shell + app SPA). FleetTrack's ops console is CSR; a customer tracking page is a candidate for SSR.
- **BFF?** Add a backend-for-frontend when the client needs aggregation, token handling or shape-shifting — otherwise a well-designed API + typed client is enough. A BFF is an architecture decision, not a folder.
- **State split:** *server state* (TanStack Query/SWR semantics: cache, revalidate, retry, dedupe) vs *UI state* (local, ephemeral). Putting server data in a global store is the classic client architecture mistake.
- **Contracts, not guesses:** generate the client from OpenAPI, map `ProblemDetails` to form errors in one place, propagate the correlation id so client errors join server traces.
- **Auth:** Authorization Code + PKCE, silent renew, route guards, 401/403 UX. Never keep tokens in `localStorage` if you can avoid it; never trust the client for authorisation.
- **Realtime UX:** reconnect + resubscribe + backfill on resume; coalesce updates; show freshness ("updated 12 s ago") rather than pretending the data is live.
- **Alternatives and their trade-offs:** React + TS (largest ecosystem/talent pool, best for hiring and complex UIs) · Blazor (one language, one deploy, faster to first UI, heavier payload/WASM trade-offs) · Angular (opinionated, enterprise/government shops, more ceremony). Pick one properly; sampling all three teaches none.
- **Budget-driven UI work:** virtualise long lists, split routes/bundles, memoise the map layer, and keep a Lighthouse budget in CI like any other test.

### 4.15 System design & ADR practice (the senior differentiator)

- **The repeatable shape:** clarify requirements → list constraints (team, budget, latency, compliance) → rough capacity estimate → 2–3 candidate designs → trade-off table → decision + risks + revisit trigger.
- **Back-of-envelope for this domain:** 5k devices × 1 ping/10 s = 500 msg/s steady, ~43M rows/day; ~10k human watchers × 1 update/10 s = 1k push/s. Design numbers like these out loud — it changes the answer (batching, compression, coalescing, retention).
- **The six cases to be able to whiteboard (Phase 11):** high-throughput ingestion · event-driven pipeline with saga · multi-tenant SaaS isolation · real-time fan-out at scale · batch billing/reconciliation · RAG-on-.NET (natural-language shipment queries).
- **Diagram levels (C4):** context (who talks to what) → container (deployables/stores) → component (modules inside a container) → code (rarely). One page, versioned in `docs/architecture/`.
- **ADR quality bar:** context/forces, ≥2 real options, the decision, consequences (including the *negative* ones), and a trigger for revisiting. "We chose X because it's popular" is not an ADR.
- **Interview framing:** state assumptions, name the failure modes, quantify, and say what you'd monitor. Showing *why not* a design is worth more than showing the design.

### 4.16 AI/LLM integration architecture (Phase 12)

- **Pick the weakest integration shape that solves the problem:** inline (extract/summarise/classify inside an existing workflow) → assistant (chat over *your* tools) → retrieval-augmented answers → agentic multi-step workflows. Each step up adds latency, cost, non-determinism and failure modes.
- **AI at the edge, never in the domain:** the model translates intent into *existing* use cases. Pricing, compliance and routing rules stay deterministic code; the LLM never invents a shipment or a price.
- **Provider abstraction:** `Microsoft.Extensions.AI` (`IChatClient`/`IEmbeddingGenerator`) so Azure OpenAI/Foundry, OpenAI and **local Ollama** are swappable; pin model versions, plan for deprecations, and route by task (small model for classification, larger for reasoning).
- **Prompt & context engineering is software engineering:** prompts are versioned files reviewed in PRs; context comes from read models, not raw tables; every call has an explicit **token budget** (truncate/summarise on overflow); determinism means temperature 0 + schema-constrained output.
- **Tools are the integration point:** strict JSON schemas, authorisation enforced *inside* the tool (tenant + role), idempotency keys, an approval gate for anything that writes, and an audit record of every invocation.
- **Validate model output like any untrusted input:** schema validation, repair-retry, refusal path, deterministic fallback. Non-negotiable guardrails: PII redaction, cost/latency budgets, timeouts, kill switch.
- **When NOT to use an LLM:** exact rules (pricing, tax, compliance, routing maths), anything where a wrong answer is expensive and untestable, and every question a `SELECT` can answer.

### 4.17 MCP — how to use it, and how to expose your system through it

- **What it is:** a standard protocol between an *agent host* (your IDE/assistant) and *servers* that expose **tools** (actions), **resources** (read-only content) and **prompts** (templates). Transports: **stdio** for local processes, **HTTP/SSE** for remote; capability negotiation and versioning are part of the protocol.
- **Using it (the developer side):** wire read-only MCP servers into your coding agent — git/GitHub, read-only Postgres, OpenAPI, Aspire dashboard, docs search. Commit the setup (`AGENTS.md` + `.mcp.json`) so it's reproducible. **Never grant:** production data, write tokens, cloud credentials, or "all repos".
- **Exposing it (the product side):** wrap *existing* use cases as MCP tools — start read-only (`get_shipment`, `list_delayed_shipments`, `get_last_position`, `search_documents`), publish projections as resources, add write tools only behind approvals. Authentication and tenant scoping follow the same rules as the HTTP API; every invocation is audited.
- **MCP vs plain HTTP vs a bespoke plugin API:** MCP wins when several independent clients/agents need discoverable tools without bespoke glue; a normal HTTP API remains right for your own UI and integrations. They are complementary — the MCP server should call the same use cases.
- **Threat model (treat it as production software):** prompt injection arriving through tool output or documents, tool poisoning/masquerading, confused deputy through over-broad scopes, cross-tenant leakage, unbounded consumption (cost/DoS), and silent tool-contract drift. Mitigate with least privilege, allow-lists, approval gates, per-tenant caps, versioned tool contracts and an invocation audit trail.

### 4.18 RAG & retrieval architecture

- **RAG is for unstructured knowledge only** (BOL/POD scans, contracts, notes, emails). If the answer lives in columns, a query beats a vector search — every time.
- **Pipeline:** ingest → chunk **per document type** (a POD is not a contract) → embed → store in `pgvector` **with tenant and shipment metadata** → retrieve with **hybrid search (BM25 + vector)** and metadata filters → rerank → ground the answer → cite.
- **Retrieval quality is data quality:** bad chunking, missing metadata filters or a stale index produce confident nonsense. Measure it: recall@k, groundedness, answer relevance.
- **Lifecycle:** re-embed on model change (embeddings are model-specific), delete/purge chunks with the document (GDPR), version the index, and keep ingestion idempotent.
- **Costs:** embedding storage grows with documents, retrieval latency adds to user-visible latency, and "just increase k" degrades answers instead of improving them.

### 4.19 Evals, guardrails & AI safety

- **Eval pyramid:** deterministic checks (schema, tool choice, tenant boundaries) → golden datasets with expected outcomes → rubric / LLM-as-judge for open-ended answers → scheduled human review. Run the first two in CI as **release gates**, not manual rituals.
- **Guardrails:** authorisation stays deterministic and outside the model; writes go through approval gates; outputs are validated before use; retrieved content is treated as **untrusted data** (an injected "ignore your instructions" in a POD scan must fail closed).
- **Safety nets:** PII redaction on prompts/logs, content-safety filtering, refusal path, per-request and per-tenant cost/step ceilings, timeouts, kill switch, and full audit of AI-driven actions.
- **Red teaming:** keep a prompt suite that tries to break tenant isolation, leak data through tools, or trigger unbounded loops — and run it on every prompt/tool change.
- **Regression discipline:** prompt and model versions are recorded with every eval run so a quality drop is attributable (prompt vs model vs data).

### 4.20 AI operations & economics

- **Observe like production:** GenAI semantic conventions on spans (model, prompt version, tokens in/out, latency, cost) plus dashboards per feature and per tenant; alert on cost spikes, refusal-rate changes and latency regressions.
- **Cost levers, in order:** smaller models for classification/extraction, prompt trimming and caching of stable prefixes, batching, streaming where UX needs it, retrieval that narrows context instead of padding it.
- **Budgets and fairness:** per-tenant token/cost caps with graceful degradation ("assistant unavailable for this tenant") so one customer can't spend your margin.
- **Lifecycle:** model deprecation plans, prompt registry with rollback, A/B comparisons on real tasks, and a periodic question: *does this feature still earn its cost?* Removing an AI feature is a legitimate architectural outcome.

### 4.21 AI-assisted development (how to use agents well)

- **Guardrails first:** a repo rules file (`AGENTS.md`/Copilot instructions) encoding the dependency rule, ADR requirement and test expectations; read-only MCP tooling by default; no agent writes to production data.
- **Work in small, reviewable diffs** with tests as the contract — the agent proposes, CI and the architecture tests dispose. Never let an agent silently widen scope or add a dependency.
- **What agents are good at:** mechanical refactors, test scaffolding, migration drafts, documentation, exploratory spikes, boilerplate (options/DI/endpoints). **Keep human:** architecture decisions, ADR rationale, security and cost trade-offs, tenant-isolation design.
- **Measure the workflow:** track rework/defect rate and time-to-artifact in the journal; if AI assistance isn't measurably helping a phase, drop it there.

---

## 5. ADR register

One row per architecture decision. **Status** becomes `Accepted` only when the artifact from that day exists in the repo. Write ADRs with: context/forces → options (≥2, with real costs) → decision → consequences (including the bad ones) → revisit trigger.

| ADR | Title | Status | Decision / open question | Week |
| --- | --- | --- | --- | --- |
| 001 | Local orchestration | Proposed | Aspire AppHost vs docker-compose for dev services + wiring | W1D3 |
| 002 | Runtime & build guardrails | Proposed | .NET 10 LTS; warnings-as-errors, analyzers, CPM; upgrade day for .NET 11 | W1D1 |
| 003 | Architecture style | Draft | Modular monolith, clean layering inside modules, vertical slices for use cases | W1D7 / W2D5 |
| 004 | Mediator & handler tooling | **Open** | Wolverine (MIT) vs MediatR Community (licence thresholds) vs FastEndpoints vs hand-rolled | W2D5 → decide W9D1 |
| 005 | Object mapping | **Open** | Mapperly (source-gen) + manual mapping vs AutoMapper (commercial in prod) | W2D5 |
| 006 | Persistence access | Open | EF Core for writes + Dapper/raw SQL for read models; where repositories earn their keep | W4D5 |
| 007 | Bounded contexts | Open | Shipments/Dispatch/Tracking/Billing/Identity/Notifications + context map, shared kernel size | W7D1 |
| 008 | When to split a service | Open | Explicit trigger list (team size, scaling profile, compliance, release cadence) — not a vibe | W7D5 |
| 009 | Broker | Open | RabbitMQ (local + portable) vs Azure Service Bus (managed) vs Kafka (streaming) | W11D1 |
| 010 | Messaging framework | **Open** | Wolverine (MIT, sagas+outbox) vs MassTransit v8 (free, unsupported) vs v9 (paid) | W11D2 |
| 011 | Telemetry storage | Open | TimescaleDB vs partition-per-day Postgres vs external TSDB — must show numbers | W14D3 |
| 012 | Real-time transport | Open | SignalR (+Redis backplane) vs SSE vs polling per interaction | W15D1 |
| 013 | Identity provider | Open | Entra ID vs Keycloak vs `MapIdentityApi` (learning-only) | W18D1 |
| 014 | Tenancy & isolation | Open | Shared schema + `TenantId` + RLS vs schema-per-tenant vs DB-per-tenant | W19D1 |
| 015 | Frontend stack | Proposed | React 19 + TS + Vite (**current default**); Blazor 10 or Angular documented alternatives | W20D1 |
| 016 | Cloud platform & IaC | Open | Azure Container Apps + Bicep (vs AKS / App Service / Terraform) + cost estimate | W24D2 |
| 017 | AI integration boundary | Proposed | Assistant = adapter over existing use cases (tools); the deterministic core owns the rules; a kill switch on every AI path | W27D1 |
| 018 | LLM provider & model strategy | **Open** | `Microsoft.Extensions.AI` abstraction; Azure OpenAI/Foundry in cloud, **Ollama** locally; model tiers, version pinning, fallback/routing | W27D2 |
| 019 | MCP adoption (dev workflow) | **Open** | Which MCP servers the coding agent may use, scopes, and what is explicitly denied (prod data, write tokens, cloud creds) | W28D1–2 |
| 020 | MCP exposure & authorization | **Open** | Read tools first, write tools behind approvals; per-user/tenant auth; invocation audit; tool-contract versioning | W28D3 |
| 021 | Retrieval design | Open | `pgvector` in the same Postgres; hybrid BM25+vector with rerank; chunking per document type; re-embed/lifecycle policy | W29D1 |
| 022 | Eval & release policy | Open | Golden datasets + deterministic checks as CI gates, LLM-as-judge for open-ended answers, thresholds + rollback | W29D4 |
| 023 | Agentic scope & autonomy | Open | Step/timeout/cost ceilings; approval gates for money/status changes; no unsupervised writes | W29D3 |

> A changed plan = a new ADR or an amendment row. If we swap a default (e.g. React → Blazor), update ADR-015 **and** the Phase 8 day titles in `plan.md`, then log it in §8.

---

## 6. Domain model — what goes under a Shipment

`Shipment` is the **aggregate root of one freight movement**: a commercial agreement to move goods from A to B, with a history, a price and a set of documents. Everything that only exists because of that movement lives inside the aggregate; anything with an independent life (customer, vehicle, driver) is a separate aggregate referenced **by id**.

### 6.1 Minimal viable Shipment (used from W2D3)

`Id` · `ShipmentNumber` (human ref, `SHP-2026-000123`) · `TrackingNumber` (VO) · `Status` · `CustomerId` + `CustomerName` snapshot · `Origin`/`Destination` (`Address` VO) · `TotalWeightKg` · `TotalPieces` · `CreatedAtUtc`/`CreatedBy` · `TenantId` · `RowVersion`.

Grow it deliberately: W4 modelling, W6 value objects + invariants, W8 lifecycle/pricing/assignment rules, W9 slices, W19 tenant scoping.

### 6.2 What hangs off the shipment

| Group | Members | Notes |
| --- | --- | --- |
| Identity & audit | `ShipmentNumber`, `TrackingNumber` (VO), `Status`, `RowVersion`, created/updated by/at, soft delete | Audit filled by the EF interceptor (W6D4) |
| Parties | Shipper, consignee, bill-to/payer, carrier, notify party | Id **+ snapshot** of name/contact/address at booking — master data changes later |
| Stops | `ShipmentStop` collection: sequence, type (pickup/delivery/intermediate), `Address`, appointment window (`DateRange`), instructions, actual arrival/departure | Multi-stop loads; simple case = 2 rows |
| Cargo | `ShipmentItem` → `Package`: description, quantity + UoM, gross/net/tare (`Weight`), volume, dimensions, commodity/HS, declared value (`Money`), hazmat, temperature range | Totals rolled up **on the shipment** (`TotalWeightKg`, `TotalPieces`, `TotalVolumeM3`) |
| Assignment | `VehicleId`, `TrailerId`, `DriverId` (+ name/licence snapshot), sub-contractor, assigned-at, acceptance state | Owned by Dispatch; Shipment stores the assignment id/state, not the vehicle graph |
| Status history | `ShipmentStatusEvent[]`: occurred-at, status, location, source (device/driver/EDI/manual), reason, user | **Append-only** — this is track & trace |
| References | customer PO, order number, quote/rate id, container + seal numbers, SLA/commitment, Incoterms, priority, tags, tenant-specific attributes (JSONB) | Cheap extension point instead of schema churn |
| Documents | BOL, POD (+ signature/photo), packing list, customs/T1, DG declaration, temperature log | Metadata row + blob reference (W5D1), never bytes in the DB |
| Financials | base freight, fuel surcharge, accessorials, discounts/tax, currency, payment terms, invoice ref, COD, cost vs sell | `Money` VO, `decimal`, explicit rounding (W8D3) |
| Tenant | `TenantId` on every aggregate; RLS policy; per-tenant config/features | Isolation proven by tests, not convention (W19) |

### 6.3 Aggregate boundaries — what stays inside

| Inside `Shipment` (one transaction) | Separate aggregate (referenced by id) | Owned by another module |
| --- | --- | --- |
| `ShipmentStop`, `ShipmentItem`/`Package`, `ShipmentStatusEvent`, `ShipmentDocument` (metadata), `ShipmentCharge` | `Customer`, `Vehicle`, `Trailer`, `Driver`, `Location`, `Rate/Quote`, `Invoice` | `Position`/`Device` (Tracking), `AlertRule`/`Alert` (Tracking), `User`/`Tenant` (Identity), notifications (Notifications) |

Heuristics: if it can't be changed independently of the shipment, it's inside; if it lives across shipments, it's outside. Never navigate into another aggregate to *write* — publish an event and let that module own the change.

### 6.4 Tracking & telemetry model (Phase 5)

| Entity | Shape | Notes |
| --- | --- | --- |
| `Device` | id, vehicle id, serial/IMEI, keys, firmware, last-seen | Identity + authentication for devices (W18D3) |
| `Position` | device id, occurred-at, lat/lon (geography), speed, heading, ignition, accuracy, received-at, tenant id | Time-series (Timescale hypertable), idempotent upsert on (device, occurred-at), retention + compression |
| `TrackSegment` | device/vehicle, start/end, distance, duration, dwell stops | Derived by continuous aggregate/job — never computed on read at the edge |
| `Geofence` | polygon (geography), type (depot/customer/zone), tenant id | Enter/exit detection emits events that move shipment state (W16D1) |
| `AlertRule` / `Alert` | thresholds per tenant (speeding, idle, dwell, deviation, temperature), severity, mute window | Rule evaluation is a background consumer, not the ingest path |
| `Eta` | shipment id, computed-at, eta window, confidence, model version | Cache with short TTL; show freshness in the UI |
| `IngestionState` | last processed batch/sequence per device | Makes ingest resumable and dedupe-able |

### 6.5 Lifecycle, state machine and the saga

```
Draft → Booked → Assigned → PickedUp → InTransit → AtStop → OutForDelivery → Delivered → Invoiced
              ↘ Cancelled        ↘ OnHold        ↘ Exception (delay / damage / refused)
```

- Transitions are guarded methods on the aggregate (`shipment.Assign(...)`, `shipment.MarkPickup(...)`), each validating preconditions and recording a **reason**; illegal transitions return 409, never silently succeed.
- Every transition appends a `ShipmentStatusEvent` (append-only) — that history is the product's track-and-trace feature.
- **Who owns what:** Shipments owns status; Dispatch owns assignment and the dispatch saga; Tracking owns positions/geofences/alerts; Billing owns charges/invoices. Cross-module effects are events, not shared tables.
- **The saga** (W12D2) drives the parts that wait on humans/third parties (driver acceptance, appointment confirmation) with timeouts and compensations. The aggregate stays synchronous and fast.

### 6.6 Event map (domain vs integration)

| Event | Kind | Produced by | Consumed by |
| --- | --- | --- | --- |
| `ShipmentCreated` | Domain (in-process) | Shipments | Notifications, read-model projector |
| `ShipmentAssigned` / `AssignmentReleased` | Domain + integration | Shipments/Dispatch | Dispatch saga, Notifications, Tracking |
| `ShipmentStatusChanged` | Integration (`Contracts`) | Shipments | Notifications worker, Billing, customer webhook, SignalR notifier |
| `PositionRecorded` (batched) | Internal/queue | Tracking ingest | Tracking projections, alert evaluation |
| `GeofenceEntered` / `GeofenceExited` | Domain + integration | Tracking | Shipments (state), Notifications |
| `InvoiceIssued` | Integration | Billing | Notifications, webhook subscribers |

### 6.7 What must **not** go under a Shipment

Vehicle/driver master data, user accounts, tenant configuration, raw telemetry points, invoices as editable documents, or anything a second shipment also needs to change. If two aggregates must change together in one transaction, redesign the boundary — don't merge the tables.

### 6.8 AI feature model (Phase 12)

| Entity / concept | Shape | Notes |
| --- | --- | --- |
| `AssistantSession` / `Message` | session id, user, tenant, model, prompt version, tokens, cost, created-at | Conversation state is **not** domain state; short-lived with a retention policy |
| `ToolInvocation` (audit) | tool name, arguments hash, user/tenant, authorisation result, latency, tokens, result summary, correlation id | The audit trail that makes AI-driven actions defensible |
| `ApprovalRequest` | proposed action + payload, requested-by (agent), approved/rejected by (human), expiry | Every write proposed by AI goes through this gate |
| `PromptTemplate` / `PromptVersion` | id, version, body, variables, owner, eval results | Prompts are versioned artifacts (git + registry), never anonymous strings |
| `ModelDeployment` | provider, model, version, context window, price per 1k tokens in/out, deprecation date | Basis for routing, cost and upgrade decisions |
| `DocumentChunk` (embedding) | document id, shipment id, tenant id, chunk index, text, embedding vector, model, created-at | `pgvector`; hybrid search; purged with the document (GDPR) |
| `EvalCase` / `EvalRun` | case (input + expected constraints), run (prompt/model version, pass/fail, scores, latency, cost) | CI release gate + regression tracking |
| `UsageBudget` | tenant id, period, tokens used, cost, cap, state | Checked *before* calls; graceful degradation when exceeded |

**Guardrails encoded here:** the assistant can read only what the user can read (same authorisation path as the API); writes require an `ApprovalRequest`; every invocation is audited; no prompt/model version ships without an `EvalRun` above threshold.

---

## 7. Quiz & worked examples (weeks 1–29)

**How to use:** the question for each day is in `plan.md`; here you get the answer **and** a worked example. Cover the answer column, answer out loud, then compare — the goal is to state the *trade-off* and *when it's the wrong choice*, not to recite definitions. Ask me: *"quiz me on week 9"*, *"grade my answers for week 3"*, *"give me a harder variant"*.

### Week 1 — toolchain & guardrails

| Day | Question | Answer |
| --- | --- | --- |
| W1D1 | Why LTS rather than STS here? | STS versions last ~18 months: .NET 9 (STS) goes end-of-support in Nov 2026 — mid-plan. .NET 10 LTS is supported to Nov 2028, so upgrades become a scheduled task (W26D4), not an interruption. |
| W1D2 | Which project reference must never exist? | Any arrow pointing *at* `Infrastructure` or a framework from `Domain` (e.g. `Domain → EF Core`). It compiles fine, which is exactly why the fitness test exists. |
| W1D3 | What does an Aspire AppHost give you that `docker compose` doesn't? | A typed resource graph: connection strings/env wiring, health, and logs/traces/metrics in one dashboard, plus a deployment manifest. Compose stays more portable and simpler in CI — hence ADR-001 instead of dogma. |
| W1D4 | Why Testcontainers instead of the EF InMemory provider? | InMemory ignores SQL semantics (constraints, indexes, transactions, `timestamptz`, concurrency tokens); a real Postgres container fails the tests that should fail. |
| W1D5 | What is the cheapest CI gate that prevents architectural drift? | Build with warnings-as-errors + run the architecture/fitness tests on every PR (`dotnet format --verify-no-changes` too). Drift is rejected before a human reviews it. |

**Worked example (W1D2/W2D5).** `Application` references `Domain`; `Infrastructure` references `Application` + `Domain`; `Api` references `Application` (+ `Infrastructure` only in the composition root). The wrong arrow (`Domain → Npgsql.EntityFrameworkCore.PostgreSQL`) compiles and runs — the fitness test `Domain_should_not_depend_on_infrastructure` catches it in CI:
`Types.InAssembly(domainAssembly).ShouldNot().HaveDependencyOnAny("Microsoft.EntityFrameworkCore", "Npgsql", "Microsoft.AspNetCore")`.

### Week 2 — skeleton, first slice, boundary tests

| Day | Question | Answer |
| --- | --- | --- |
| W2D1 | Where do ports live, and why there? | In `Application` (the caller owns the interface). Implementation-owned interfaces force callers to depend on implementation details and make adapters expensive to swap. |
| W2D2 | How do you keep configuration knowledge at the edge? | Typed options records bound and validated with `ValidateOnStart()`; `Domain`/`Application` receive values, never `IConfiguration`. |
| W2D3 | What does each test layer prove about the same use case? | Fake adapter (unit): rules, branching, error paths — fast. Container (integration): SQL mapping, constraints, transactions, concurrency token behaviour. |
| W2D4 | Why insist on stable `ProblemDetails` `type` URIs? | Clients branch on machine-readable `type`, not prose; renaming a type becomes a visible contract change instead of a silent break. |
| W2D5 | What is *not* worth an architecture test? | Anything the compiler already guarantees (one class per file, sealed types) — it becomes maintenance noise. Test what the compiler can't see: dependency direction, module isolation, framework leakage. |

**Worked example (W2D3/W2D4).** Before: the endpoint validated input, ran three EF queries and applied freight rules inline — no test can reach the rules without HTTP + a database. After: `POST /shipments` → validates shape → `CreateShipmentHandler` (rules, unit-tested with a fake `IShipmentRepository`) → `EfShipmentRepository` (integration-tested against Postgres). Same behaviour, two cheap test seams, plus a `ProblemDetails` mapping table shared by every endpoint.

### Week 3 — contract-first APIs

| Day | Question | Answer |
| --- | --- | --- |
| W3D1 | When is a breaking API change acceptable? | Only with a version bump + deprecation window + parallel support, never silently. Prefer additive changes first. |
| W3D2 | Why keyset paging once lists grow? | Offset pages scan/skip N rows and shift when rows are inserted concurrently (duplicates or missed rows); a cursor is an index seek on a stable sort key. |
| W3D3 | What does `ETag` + `If-Match` actually protect? | Lost updates: two dispatchers editing one shipment — the second write gets 412 instead of silently overwriting. |
| W3D4 | Why is `Idempotency-Key` mandatory for `POST /shipments`? | A timeout + retry (client, gateway, proxy) would otherwise book the load twice and double-price it. |
| W3D5 | What does generated-client drift detection prevent? | Contract divergence that only shows up in production; CI fails when the OpenAPI document changed and the client wasn't regenerated. |

**Worked example (W3D4).** Handler order matters: ① read `Idempotency-Key` → ② if a stored record exists and the body hash matches, replay the stored response (the original 201 + body) → ③ if it exists with a *different* hash, return 409 (key reuse with a different payload is a client bug) → ④ otherwise reserve the key, execute the use case, and store the response **in the same transaction** as the shipment insert. The fingerprint must include the tenant and user, not just the body.

### Week 4 — EF Core architecture & data integrity

| Day | Question | Answer |
| --- | --- | --- |
| W4D1 | Why `timestamptz` instead of `timestamp`? | `timestamptz` stores an absolute instant (UTC); `timestamp` is an ambiguous wall-clock value — freight crosses time zones and DST, so ambiguity becomes a data bug. |
| W4D2 | When is JSONB right, and when is it wrong? | Right for sparse, per-tenant, read-as-a-whole attributes; wrong for anything you filter, join, constrain or index often (that's a column, or a table). |
| W4D3 | How do you stop `IQueryable` leaking out of the data layer? | Ports return materialised DTOs/read models; `IQueryable` never appears in an Application/Api signature (enforced by an architecture test). |
| W4D4 | Why one transaction per aggregate? | It keeps transactions short and local, avoids distributed locks and long-held connections; cross-aggregate effects go through events + outbox (documented eventual consistency). |
| W4D5 | When does a repository earn its place? | When you load/save an aggregate, need concurrency control, or want a swappable test seam. For a straight projection, `DbContext`/Dapper directly is more honest than a fake abstraction. |

**Worked example (W4D3).** N+1 fix — before: `foreach (var s in shipments) totals.Add(await _db.ShipmentItems.CountAsync(i => i.ShipmentId == s.Id));` (1 + N round trips, invisible in tests, fatal at 10k rows). After: one query projecting shipment + aggregated totals (`GroupBy`/subquery) into the read DTO, verified by counting commands and by `EXPLAIN ANALYZE` before/after timings recorded in the journal.

### Week 5 — documents, integrations and the outbound boundary

| Day | Question | Answer |
| --- | --- | --- |
| W5D1 | Why keep blobs out of the database? | DB size/backup/restore/vacuum cost, memory pressure, and no streaming; store a reference + metadata and move bytes with pre-signed URLs so uploads never transit the API. |
| W5D2 | What is the anti-corruption layer protecting you from? | Partner/EDI shape changes leaking into the domain: translations and tests live at the boundary, so an upstream rename doesn't touch the aggregate. |
| W5D3 | Why must `HttpClient` be injected rather than constructed? | `new HttpClient()` per call exhausts sockets and ignores DNS changes; `IHttpClientFactory` + typed clients centralise resilience policies, timeouts and tracing. |
| W5D4 | How do you make outbound webhooks trustworthy? | HMAC-signed payloads with timestamp/nonce, bounded retries with backoff, delivery log and replay endpoint; the receiver verifies the signature and rejects replays. |
| W5D5 | Why seed with realistic volume instead of three rows? | Paging, index and query-plan decisions only reveal themselves at volume; small fixtures hide exactly the problems this phase designs for. |

**Worked example (W5D1).** Document upload flow: ① client asks the API for an upload slot → ② API authorises, inserts a `pending` document row, returns a short-lived pre-signed PUT URL → ③ the client uploads directly to storage (progress/retry on the client, no API bandwidth) → ④ the client confirms, the API marks the row `confirmed` and enqueues a scan/virus check → ⑤ GET returns a short-lived pre-signed download URL, and every download is written to the audit trail.

### Week 6 — tactical DDD

| Day | Question | Answer |
| --- | --- | --- |
| W6D1 | Why rename code to match the domain glossary? | The model *is* the shared language: misnomers (consignment vs shipment vs order) create wrong boundaries and slow every conversation; code that speaks the business needs no translation layer. |
| W6D2 | What does a value object give you that a `string` doesn't? | Validation at construction (no invalid instances), value equality, no argument mix-ups (`Address` vs `TrackingNumber`), and a name that documents intent. |
| W6D3 | What is the aggregate root actually protecting? | Invariants spanning its children — totals consistent with items, only legal state transitions — so no caller can construct an invalid shipment. |
| W6D4 | Why put audit fields and PII purge in an interceptor instead of handlers? | It's impossible to forget, consistent on every write path, and retention/purge rules compose in one place with soft delete. |
| W6D5 | Why dispatch domain events only *after* the transaction commits? | Handlers reacting before commit can act on changes that are later rolled back (phantom events); collect events, save, then publish facts that are actually true. |

**Worked example (W6D3).** `Shipment.AddItem(item)` rejects non-positive weight/quantity and recalculates `TotalWeightKg`/`TotalPieces`; `shipment.Assign(vehicleId, assignmentId, at)` fails with `AssignmentNotAllowed` unless status is `Booked`; `Items` is exposed as `IReadOnlyCollection<ShipmentItem>`. Tests: adding an item changes totals; assigning twice fails; assigning after `Delivered` fails.

### Week 7 — strategic DDD & module boundaries

| Day | Question | Answer |
| --- | --- | --- |
| W7D1 | How do you know a bounded context boundary is right? | Walk a business story end to end: the split is where vocabulary changes meaning (shipment as agreement vs dispatch as plan) or where a different owner would decide differently. |
| W7D2 | What turns a folder into a real module? | It owns its data and invariants, exposes a published contract, and its internals cannot be referenced — enforced by a test, not by convention. |
| W7D3 | Why forbid cross-module EF navigation properties? | They couple schemas silently: one module's rename or delete breaks another's queries and transactions; contracts/events keep changes local and reviewable. |
| W7D4 | When is extracting the second module worth it? | When you can name its data, its owner and its contract. Extracting earlier adds indirection with no ordering benefit. |
| W7D5 | Name three triggers that *do* justify splitting a module into a service. | Independent scaling profile · separate team/release cadence · isolation/compliance requirement (also: different language/runtime or availability target). |

**Worked example (W7D1/W7D5).** Context map: `Shipments` (core, owns the agreement) ⇄ `Dispatch` (core, owns assignment + saga) ⇄ `Tracking` (core, owns positions/geofences); `Billing`/`Notifications` are supporting (event consumers); the partner EDI feed enters through an **ACL** in `Shipments/Infrastructure`; shared kernel = `ShipmentId`, `VehicleId`, `Money`, `TrackingNumber`. ADR-008 records the split triggers, so "let's go microservices" needs evidence rather than enthusiasm.

### Week 8 — freight rules worth modelling properly

| Day | Question | Answer |
| --- | --- | --- |
| W8D1 | Why a state machine instead of status checks sprinkled through handlers? | One definition of legal transitions, guards and reasons; an illegal change becomes a single 409 rule, and the UI can ask which actions are currently allowed. |
| W8D2 | How do you stop two dispatchers assigning the same vehicle? | An aggregate invariant (`VehicleAlreadyAssigned`) plus optimistic concurrency (`RowVersion`): the loser gets a conflict and must re-read instead of silently overwriting. |
| W8D3 | Why a `Money` value object rather than a bare `decimal`? | Currency + rounding rules + guarded arithmetic (`EUR` + `USD` must fail loudly); bare decimals let currency mismatches reach invoices. |
| W8D4 | Why inject `TimeProvider` for appointment windows? | Deterministic tests for time-dependent rules and one clock source (time zones/DST) instead of `DateTime.Now` scattered through the code. |
| W8D5 | Why model exceptions (delay/damage/refusal) explicitly? | They're normal freight business: first-class states + reasons give correct compensations, KPIs and audit, instead of inferring truth from a notes field. |

**Worked example (W8D1/W8D2).** Transition table: `Booked → Assigned|Cancelled`, `Assigned → PickedUp|Booked(release)|Cancelled`, `PickedUp → InTransit|Exception`, … each with a guard (`OnlyBookedMayBeAssigned`) and a recorded reason. Concurrency test: two parallel assignment requests for the same vehicle → one 200, one 409 (`OptimisticConcurrency`), and the database shows no double booking.

### Week 9 — mediator layer & vertical slices

| Day | Question | Answer |
| --- | --- | --- |
| W9D1 | What does a mediator buy you, and what does it cost? | Buys: one pipeline for validation/timing/transaction/authorisation and decoupled callers. Costs: indirection (harder to navigate and debug), reflection/source-gen overhead, and a licence review — MediatR is commercial for production use, Wolverine is MIT. |
| W9D2 | Why one folder per use case instead of a service class with 15 methods? | Cohesion: command, handler, validator and mapping change together; a service class becomes a change magnet and a merge-conflict hotspot. |
| W9D3 | Why must queries avoid loading aggregates? | Reads need shapes, not invariants; projections are far cheaper and can't accidentally trigger domain logic or writes. |
| W9D4 | What order should pipeline behaviours run in? | Outermost timing/logging (measures everything, including failures) → authorisation + validation (fail fast, cheapest) → transaction (wraps only real work) → handler. Other orders either measure nothing or hold a transaction open for a rejected request. |
| W9D5 | Why not use exceptions for expected business failures? | Exceptions are for exceptional states: they cost allocations/stack unwinding and hide the contract. Expected failures (conflict, illegal transition) belong in a `Result` with a mapping table to `ProblemDetails`. |

**Worked example (W9D5).** `Result<ShipmentId>` carries codes `Shipment.NotFound`, `Shipment.IllegalTransition`, `Shipment.VehicleConflict`; one mapper turns them into `404`, `409`, `409 (+Retry-After)` and `400` for validation. Every endpoint uses the same mapper, so the error taxonomy stays consistent and appears in OpenAPI examples.

### Week 10 — read models, search & reporting

| Day | Question | Answer |
| --- | --- | --- |
| W10D1 | Why must a read model be rebuildable from the source of truth? | It's derived data: bugs, new projections and schema changes need a replay path, otherwise nobody dares change the projector. |
| W10D2 | Materialised view or maintained projection table? | Views are a quick win but the refresh blocks under load; maintained tables are cheap to read and need idempotent projectors plus lag monitoring — decide with measurements. |
| W10D3 | When is a dedicated search engine justified? | Ranking/relevance, fuzziness, facets, or cross-entity search at scale — not for exact lookups like "find by tracking number" (that's an index on an existing table). |
| W10D4 | Why stream exports instead of returning them inline? | Large result sets blow up memory and hit request timeouts; stream to blob storage in a background job and notify the user when the file is ready. |
| W10D5 | How do you operate eventual consistency? | Measure projection lag, alert on thresholds, keep a reconciliation/rebuild command, and design the UI to show freshness instead of implying the data is live. |

**Worked example (W10D1).** The projector for `ShipmentStatusChanged` upserts `shipment_list_read` by shipment id and stores the last applied event sequence per row, so replaying an event is a no-op. A `rebuild` CLI command truncates and replays the stream, then verifies parity (counts + checksum) against the write model — that's the safety net for every future projection change.

### Week 11 — broker, contracts and the publish path

| Day | Question | Answer |
| --- | --- | --- |
| W11D1 | How do you choose a broker? | Match semantics to need: RabbitMQ for work queues/commands with routing, Kafka for streams with replay and retention, Azure Service Bus for managed Azure with sessions/FIFO — then weigh ops burden and cost. |
| W11D2 | What is the licence trap in this week's decision? | MassTransit v8 is free but unsupported (no patches), v9+ requires a paid subscription, Wolverine is MIT — the "obvious" library can create legal and support debt, so record it in an ADR. |
| W11D3 | Why must integration contracts avoid domain types? | Consumers would be coupled to your internals: a domain refactor becomes a breaking message change. Contracts are explicit, versioned and additive-only. |
| W11D4 | Why isn't "publish after `SaveChanges`" enough? | The process can crash between commit and publish → the event is lost forever. An outbox row written in the same transaction is recoverable by a relay. |
| W11D5 | What makes a consumer safe to retry? | Idempotency: dedupe by message id in an inbox table plus operations that tolerate re-application (upserts, guarded transitions, natural keys). |

**Worked example (W11D4/W11D6).** Order-and-proof drill: ① stop RabbitMQ ② `POST /shipments/{id}/status` → 200 with the outbox row written ③ verify the API stayed healthy (no 500s, only publishing pauses) ④ start RabbitMQ ⑤ assert the message arrives **exactly once** in business terms (consumer applied once), the trace continues across the broker, and the DLQ is empty.

### Week 12 — sagas & long-running processes

| Day | Question | Answer |
| --- | --- | --- |
| W12D1 | When do you choose a saga over choreography? | When the process has multiple steps with waiting or timeouts, needs end-to-end visibility, and requires compensation (driver acceptance, appointment confirmation). 2–3 independent reactions are fine as choreography. |
| W12D2 | What must persisted saga state contain to be safe? | Current step, business keys (`shipmentId`, `vehicleId`), deadlines for every wait, the data each step needs to be idempotent, and a version/concurrency token for optimistic updates. |
| W12D3 | How should timeouts be modelled? | As scheduled messages/deadlines the saga reacts to — never an in-memory timer or a cron that guesses state; each timeout has an explicit branch and compensation. |
| W12D4 | What does compensation mean here? | Publishing *new facts* that reverse the business effect (release the vehicle, cancel the appointment, notify the customer). You never delete the original event — that already happened. |
| W12D5 | What does "operating a saga" actually require? | Visibility of in-flight and stuck instances (counts by state and age), a manual escalate/replay path, and alerts on age thresholds. Otherwise failures are invisible until a customer complains. |

**Worked example (W12D2).** States: `AwaitingDriverAcceptance → (Accepted) Confirmed | (Timeout) Escalated → (Reassign) AwaitingDriverAcceptance | Cancelled`. Each transition is guarded by the current state + version; the same `DriverAccepted` message delivered twice is a no-op because the transition from `Confirmed` is not allowed. A stuck-saga query (`age > 15 min and state = Escalated`) drives the ops alert.

### Week 13 — resilience & load control

| Day | Question | Answer |
| --- | --- | --- |
| W13D1 | Why a policy catalogue instead of retries sprinkled around the code? | Every dependency has different failure semantics; central policies stop retry storms, make timeouts explicit, and are testable in one place (instead of 40 bespoke `try/catch` blocks). |
| W13D2 | Why bound queues and shed load deliberately? | Unbounded queues turn overload into memory exhaustion and total failure; rejecting early (429/503 with `Retry-After`) keeps the system responsive and honest about capacity. |
| W13D3 | Why per-tenant rate limits and quotas? | Fairness and blast-radius control in a shared system: one tenant's retry loop must not degrade everyone else — and the noisy tenant must be identifiable. |
| W13D4 | What is graceful degradation, concretely in FleetTrack? | Keep booking/tracking (core) alive while disabling or staling the rest: read-only mode, cached responses, deferred alert recomputation, feature flags to switch non-critical paths off. |
| W13D5 | What does a chaos drill prove that unit tests can't? | The interaction of real timeouts, retries, circuits, queues and health checks under partial failure — plus the quality of your *detection* and runbook. |

**Worked example (W13D1).** Retry arithmetic: 3 attempts with 500 ms base and jitter, inside a 2 s timeout budget for the routing-provider call, with a circuit breaker opening after 20 failures in 30 s → when the provider degrades, calls fail fast (and the UI shows "ETA unavailable"), instead of each request holding threads for 2 s and cascading into pool exhaustion.

### Week 14 — telemetry ingestion

| Day | Question | Answer |
| --- | --- | --- |
| W14D1 | Why can't devices be treated like well-behaved API clients? | They batch, retry, duplicate, arrive out of order, have clock skew, and go offline for hours — so ingestion must be idempotent (device + occurred-at), order-tolerant and explicit about late data. |
| W14D2 | Why not insert one row per ping inline in the request? | Per-request round trips cap throughput and couple device latency to database health; batching through a bounded channel smooths spikes and gives a place to apply backpressure. |
| W14D3 | What makes time-series data different from OLTP data? | Append-heavy volume with range/aggregate queries and almost no single-row updates: chunked storage, compression and retention policies replace conventional indexing strategies. |
| W14D4 | Why cache "last known position"? | It's the hottest read and tolerates seconds of staleness; recomputing it from the full history on every poll wastes I/O and competes with ingestion writes. |
| W14D5 | Why PostGIS instead of computing distances in C#? | Spatial indexes plus set-based predicates ("which vehicles are inside this polygon") and correct geodesic maths — doing it in code means loading rows and re-inventing geometry. |

**Worked example (W14D2).** Ingest pipeline: endpoint validates + authenticates device → writes batch to a bounded `Channel<PositionBatch>` (capacity 10k) → background service drains in batches of 500, performs an idempotent `INSERT … ON CONFLICT (device_id, occurred_at) DO UPDATE` → publishes a coalesced `PositionRecorded`. When the channel is full the API answers `429 + Retry-After` (devices buffer locally) instead of growing memory until the pod is OOM-killed.

### Week 15 — real-time push architecture

| Day | Question | Answer |
| --- | --- | --- |
| W15D1 | How do you choose a transport? | By interaction shape: polling for slow admin data, SSE for one-way feeds (cheap, HTTP-friendly), WebSockets/SignalR for bidirectional + groups, gRPC streaming for service-to-service. The real cost driver is fan-out, not the protocol itself. |
| W15D2 | What must a hub check before adding a connection to a group? | Authorisation for that specific shipment (tenant + role), not merely "authenticated user" — otherwise you've built a cross-tenant leak behind a nice UI. |
| W15D3 | Why does adding a backplane change the architecture? | Instances become interchangeable: any instance can push to any connection, so you can scale out and roll deploys without sticky sessions or losing messages held in one process's memory. |
| W15D4 | Why coalesce and throttle pushes? | A firehose multiplies by the number of watchers; sending the latest state every N ms (or on change) keeps bandwidth/CPU bounded and the UI actually readable. |
| W15D5 | What does least privilege mean for subscriptions? | Only the shipments, fields and update rate the role needs: trimmed payloads and per-role projections instead of broadcasting whole objects to everyone in the tenant. |

**Worked example (W15D3/W15D5).** Map update path: ingest → coalescing buffer (latest position per vehicle, flushed every 2 s) → `IHubContext<FleetHub>` push to `tenant:{id}:dispatch` and `shipment:{id}` groups (authorised on subscribe) → client patches the marker position and shows "updated 2 s ago". On reconnect the client requests a snapshot for the affected shipments — deltas alone would leave gaps after a network drop.

### Week 16 — tracking features & algorithms

| Day | Question | Answer |
| --- | --- | --- |
| W16D1 | Why wire geofences into the domain state machine instead of the ingest path? | Arrival/departure are business facts: Tracking detects them (fast, spatial) and publishes events; Shipments owns legality and appends the status event — one writer per fact. |
| W16D2 | Why give ETAs a confidence range? | An ETA presented as a promise creates support cost; a window plus freshness and the basis ("last 30 min of movement") is honest, testable and still useful. |
| W16D3 | Why are alert rules per-tenant data instead of code? | Customers have different thresholds and SLAs; per-tenant configuration means a rule change is a data change (auditable), not a deployment. |
| W16D4 | Why are dedupe, muting and escalation part of the design? | Without them alarms become noise and get ignored — alert fatigue is a design defect, so severity, mute windows and dedupe keys belong in the model. |
| W16D5 | Why document an event map for the modules? | The event map *is* the contract between bounded contexts; without it, consumers are discovered only when a rename breaks a workflow in production. |

**Worked example (W16D1).** Flow: `PositionRecorded` → geofence evaluation (`ST_Contains(geofence, position)`) → `GeofenceEntered` (depot or customer site) → Shipments handler checks the shipment's stops → legal transition (`InTransit → AtStop`) → status event appended + `ShipmentStatusChanged` published → notifier pushes to watchers and Billing receives the event for timestamped evidence of arrival.

### Week 17 — caching & performance engineering

| Day | Question | Answer |
| --- | --- | --- |
| W17D1 | What belongs in a cache key? | Everything that changes the answer: tenant, user/permission scope, filters, sort, version and locale. A wrong key is a wrong answer or a data leak — not merely a cache miss. |
| W17D2 | Why is invalidation harder than caching? | You can't reliably locate every derived copy across instances and processes; prefer version-bumped keys (`shipments:v7:tenant:…`) that make old entries unreachable and expire naturally. |
| W17D3 | When is HTTP-level caching the right tool? | For shared, non-personalised, slow-changing payloads (reference data, map tiles, per-tenant read-only lists) plus `ETag`/304 for everything that can't be cached. |
| W17D4 | What makes a benchmark trustworthy? | Release build, warm-up, realistic data volume, repeated runs reporting spread (not a single number), a stored baseline, and one change at a time. |
| W17D5 | How do SLOs change engineering decisions? | They define "done" before incidents do: a p95 budget forces index/query/cache decisions early, and an error budget tells you when to stop shipping features and fix reliability. |

**Worked example (W17D1/W17D2).** Cache-aside for the shipment list: key `shipments:v7:tenant:{tenantId}:status:{status}:cursor:{cursor}` with 30–60 s TTL + jitter, plus single-flight so a cold key isn't fetched by 200 concurrent requests. On any write that affects the list, the mutation path bumps the tenant version (`INCR shipments:ver:{tenantId}`) — every stale key becomes unreachable without a delete storm, and the next read repopulates.

### Week 18 — identity & authorization

| Day | Question | Answer |
| --- | --- | --- |
| W18D1 | Why outsource identity (OIDC) instead of building login yourself? | Password storage, MFA, federation, consent and token issuance are solved, high-liability problems; an IdP also gives you machine-to-machine flows and rotation for free. `MapIdentityApi` is acceptable for learning, painful in production. |
| W18D2 | What is BOLA and how do you prevent it? | Broken Object Level Authorisation: trusting the id in the route/body instead of checking that *this caller* may touch *that object*. Prevent with resource-based authorisation on every by-id operation, tenant-scoped queries, and per-role tests. |
| W18D3 | Why sign partner requests (HMAC) as well as issuing API keys? | The key identifies the caller; the signature proves the payload wasn't altered, and timestamp + nonce prevent replay. Keys alone leak through logs and proxies and protect nothing about the body. |
| W18D4 | Why is a secret in `appsettings.json` worse in containers than on a VM? | Images, layers and config maps get copied, cached and shared; rotation then means rebuilding images. Managed identity + Key Vault keeps secrets out of configuration entirely. |
| W18D5 | What does a threat model add if you already follow the OWASP checklist? | It's per-design rather than per-checklist: naming assets, actors, trust boundaries and abuse cases specific to *this* system (e.g. guessable tracking URLs exposing another tenant's cargo). |

**Worked example (W18D2).** Resource-based check for `GET /shipments/{id}`: the query is tenant-scoped (`WHERE tenant_id = @tenant`), and a policy handler additionally verifies the caller's role permits viewing *this* shipment's status (`claims role=driver → only shipments assigned to their vehicle`). Test matrix runs every endpoint as: same tenant allowed, same tenant forbidden, other tenant (expect 404, not 403, so existence isn't leaked).

### Week 19 — multi-tenancy & data isolation

| Day | Question | Answer |
| --- | --- | --- |
| W19D1 | How do you choose the tenancy model? | By isolation requirement, compliance constraints, per-tenant scale variance, ops cost and migration pain. Shared schema + `TenantId` + RLS is the default winner at this size; DB-per-tenant is a compliance/cost decision, not a technical taste. |
| W19D2 | Why RLS if the application already filters by tenant? | Defence in depth: application bugs, ad-hoc scripts and admin tooling bypass app-level filters. RLS fails closed inside the database and is testable independently of the code path. |
| W19D3 | What breaks first when one tenant hammers the system? | Shared connection and thread pools plus slow queries starve everyone else. Contain it with per-tenant quotas, statement timeouts, query budgets and per-tenant metrics before adding hardware. |
| W19D4 | What must tenant offboarding prove? | Complete export/deletion of personal data across database, blobs, caches, search and backups (respecting retention rules) with an auditable record of the deletion — GDPR requires evidence, not intent. |
| W19D5 | Why per-tenant metrics rather than global averages? | Fairness decisions and incident triage need per-tenant error rates, latency and cost; aggregates hide exactly the tenant causing the incident. |

**Worked example (W19D2/W19D3).** RLS setup: every tenant table gets `POLICY tenant_isolation USING (tenant_id = current_setting('app.tenant_id')::uuid)`; a connection interceptor issues `SET LOCAL app.tenant_id = …` per request/transaction (reset on connection return). Isolation tests connect as the *application role* (not superuser) and assert that a query without a tenant predicate returns zero rows, and that `IgnoreQueryFilters()` appears nowhere outside `Infrastructure` (architecture test).

### Week 20 — client foundations

| Day | Question | Answer |
| --- | --- | --- |
| W20D1 | What drives SPA vs SSR? | Audience and content: an internal ops console is a SPA (long sessions, rich interactions); a public customer tracking page benefits from SSR for first paint and shareable links. Add a BFF only when you need aggregation or server-side token handling. |
| W20D2 | Why generate the API client instead of hand-writing `fetch` calls? | Types follow the contract, so contract changes break the build instead of production; auth, error mapping and correlation ids live in one generated seam. |
| W20D3 | Where does server state belong? | In a query cache with explicit keys, invalidation and stale policies — not mirrored into a global store, which creates two sources of truth and stale UI. |
| W20D4 | Why map `ProblemDetails` to form errors centrally? | The server is the authority on validation; one mapper keeps field messages consistent across forms and prevents business rules from being duplicated (and drifting) in the client. |
| W20D5 | How do you keep optimistic updates safe? | Apply locally, keep the previous state, roll back on error, and surface the conflict (409) to the user instead of silently retrying a business action. |

**Worked example (W20D4/W20D5).** `POST /shipments` returns `400` with `errors: { "origin.postalCode": ["not serviceable"], "items[0].weightKg": ["must be > 0"] }` → the single mapper resolves those paths onto form fields, focuses the first invalid one, and shows the summary; the optimistic "shipment created" row is removed from the list if the server rejects it, with the conflict reason displayed.

### Week 21 — shipment operations UI

| Day | Question | Answer |
| --- | --- | --- |
| W21D1 | Why put filters, sorting and paging in the URL? | Shareable/bookmarkable views, working back/forward, and one definition of state that also drives the API query and the tests. Ops users send each other links all day. |
| W21D2 | Why is the timeline the centre of the detail page? | The status history is the product's evidence: showing source, reason and actor turns "where is my load?" support calls into lookups. |
| W21D3 | Why shouldn't uploads be proxied through the API? | Bandwidth, CPU and memory cost on the API tier, request-size limits, and no natural progress/retry semantics. Pre-signed direct upload + confirmation solves all three. |
| W21D4 | What is the hard part of a live map? | Rendering cost (hundreds of markers/trails) and update coalescing — not the socket. Cluster or virtualise, throttle updates, then optimise code. |
| W21D5 | Where does optimistic UI break? | On rejection or conflict: without rollback *and* a visible reason, users believe the action succeeded. Always pair optimistic updates with an explicit conflict path. |

**Worked example (W21D4).** Map budget: ≤ 60 update payloads/s per client, latest-position-only deltas (never full traces), trail simplified server-side (Douglas–Peucker) to ~50 points, markers rendered on a canvas layer with clustering above 200 items, and geofence polygons loaded once per tenant. Measured with a synthetic fleet of 500 vehicles; the socket was never the bottleneck.

### Week 22 — client quality & testing

| Day | Question | Answer |
| --- | --- | --- |
| W22D1 | Why test behaviour instead of implementation? | Implementation tests break on every refactor and produce false confidence; behaviour tests (accessible queries + user actions) prove what a user can actually do. |
| W22D2 | What do E2E tests cover that nothing else can? | The journey: routing, auth, API, realtime and persistence working together. Keep them few, deterministic and run against a real containerised stack. |
| W22D3 | Why is accessibility a correctness issue for ops software? | Keyboard-only dispatchers, screen readers and low-contrast warehouse screens; a11y tooling also catches missing labels/semantics that plain tests ignore. |
| W22D4 | Why propagate the correlation id from the browser? | One trace across client and server turns "the page was slow" into the exact span, query or push; otherwise client errors can't be joined to server logs. |
| W22D5 | When is offline support worth its complexity? | When users work in dead zones (yards, drivers): scope it to specific actions (status updates, POD capture) because the real cost is the sync queue and conflict resolution. |

**Worked example (W22D2).** Playwright journey: start the Aspire/Testcontainers stack → authenticate against the test IdP → create a shipment via the UI → assign a vehicle → publish a simulated device position → assert the marker and status change appear (realtime) → upload a document → assert the timeline shows all four events. Runs on PR, with traces/artifacts uploaded on failure.

### Week 23 — observability as a design activity

| Day | Question | Answer |
| --- | --- | --- |
| W23D1 | Why structured logs with event ids instead of interpolated strings? | Queryable fields and stable identity: messages get reworded, event ids don't, so dashboards and alerts keep working (and PII can be scrubbed by field). |
| W23D2 | What must a trace carry to survive the broker? | Propagated context (W3C `traceparent`) on the message, a span per hop/consumer, and a link between the outbox write and the publish so the async gap stays connected. |
| W23D3 | Which metrics actually explain incidents here? | Ingest lag, saga duration and stuck count, DLQ depth, projection lag, cache hit rate, per-tenant latency/errors. CPU and memory are context, not causes. |
| W23D4 | What makes an alert good? | Symptom-based and tied to an SLO, actionable (links to a runbook), low-noise (burn-rate windows rather than instantaneous spikes), and owned by a person. |
| W23D5 | What does "operability by design" mean concretely? | Features that let a human intervene safely — replay/requeue, pause a consumer, inspect saga state, rebuild a projection — built alongside the feature, not during the incident. |

**Worked example (W23D2).** One journey: browser (web OTel) → API request span → command handler span → EF query span → outbox insert → publish span (with `traceparent` header) → consumer span (new process, same trace id) → hub push span. The dashboard shows the async gap as a child, not a new trace, which is what makes "where did the 4 s go?" answerable.

### Week 24 — cloud platform, IaC and delivery

| Day | Question | Answer |
| --- | --- | --- |
| W24D1 | Why does graceful shutdown matter in containers? | A rolling deploy sends `SIGTERM` then `SIGKILL`: without draining in-flight requests and message handlers you silently drop work (including half-processed messages). |
| W24D2 | How do you choose between Container Apps, AKS and App Service? | By ops capacity and required control: Container Apps gives revisions/scaling without cluster ops, AKS fits when you need the platform's full control *and* have the team, App Service when infrastructure should be least visible. |
| W24D3 | Why is infrastructure as code non-negotiable? | Reproducibility and review: click-ops drifts invisibly, can't be code-reviewed, and can't be rebuilt identically after an incident or for a new environment. |
| W24D4 | Why can't you just run migrations during a deploy? | During a rolling deploy old and new code run at the same time; any non-backward-compatible change breaks that mixed state. Expand/contract separates schema change from code release. |
| W24D5 | What makes a DR plan credible? | A *tested* restore into another environment with measured timings, defined RTO/RPO, and a person who has actually executed it — not a document nobody has run. |

**Worked example (W24D4).** Adding a required column to `Shipment` (renaming `weight` → `weight_kg`): ① deploy migration adding `weight_kg` nullable (+ backfill job) → ② deploy code writing both/reading new with a fallback → ③ backfill verified (zero nulls) → ④ deploy code reading only the new column → ⑤ drop the old column. Each step is independently deployable and rollback-safe, which is impossible with a single "rename column" migration.

### Week 25 — system design practice

| Day | Question | Answer |
| --- | --- | --- |
| W25D1 | What is the first thing you do in a design question? | Clarify requirements and constraints, then produce rough numbers before drawing boxes — the estimate usually eliminates half the candidate designs (e.g. 500 msg/s is a batching problem, not a Kafka problem). |
| W25D2 | How do you justify a saga in a design review? | Show the waiting steps, the deadlines and the compensations. No waiting and no partial failure → a transaction is simpler and you should not distribute the problem. |
| W25D3 | How do you choose the isolation level in a tenancy design? | By the strongest requirement (regulatory, security, cost) and what the trade costs you: shared schema + RLS is cheapest to run, DB-per-tenant is most isolated and most expensive to operate and migrate. |
| W25D4 | How do you design for 100k watchers without unlimited budget? | Coalesce and push only changes, aggregate for overview views, cap subscriptions per user, backplane for scale-out, and price fan-out per watcher because that's the real cost driver. |
| W25D5 | What makes batch billing safe to re-run? | Idempotent runs keyed by (tenant, period), deterministic calculation from immutable facts, explicit late-data/correction handling, and an audit trail for every adjustment. |

**Worked example (W25D1).** FleetTrack capacity sketch: 5 000 devices × 1 ping/10 s = **500 msg/s** ≈ 43 M rows/day ≈ 1.3 B/month (compressed + retention → manageable on Timescale); watchers 10 000 × 1 update/10 s = **1 000 push/s** (coalesced to 0.5 Hz per watcher); API peak ~300 rps with p95 < 200 ms → 2–4 instances behind the platform's autoscaler. These numbers decide batching, retention and cache strategy — before any technology choice.

### Week 26 — capstone hardening & portfolio

| Day | Question | Answer |
| --- | --- | --- |
| W26D1 | What makes a capstone demo convincing? | An end-to-end journey that runs from a clean clone with realistic data (tenant → book → assign → track → document → invoice), plus the ADRs that explain *why* it is built this way. |
| W26D2 | What belongs in a hardening pass? | Security (authorisation matrix, secrets, dependency audit), performance (SLO evidence), accessibility, error budgets, dead code removal, and a verified backup/restore. |
| W26D3 | Why document "why not"? | The rejected options are the highest-value knowledge for future maintainers and interviewers; decisions without alternatives look arbitrary and get re-litigated. |
| W26D4 | Why is an upgrade day a learning exercise? | It rehearses the LTS/STS policy, exposes dependency discipline, and proves your fitness + E2E suites actually protect you during change (which is their real purpose). |
| W26D5 | When should an LLM feature ship? | When it's additive, at the edge, observable (cost/latency/quality), guarded by deterministic authorisation, and has a rule-based fallback for when the model is unavailable or wrong. |

**Worked example (W26D3).** "Why not" page structure: for each of the 16 ADRs, one line of *rejected option* + reason — e.g. "microservices for Tracking (rejected: one team, no independent scaling pressure, would add 3 workflows and a data-consistency problem for no user-visible gain)", "Kafka instead of RabbitMQ (rejected: no replay/stream-processing requirement at 500 msg/s; RabbitMQ is simpler to operate locally)".

### Week 27 — LLM integration foundations

| Day | Question | Answer |
| --- | --- | --- |
| W27D1 | Why can't the model live inside the domain model? | Domain invariants must hold deterministically and be testable: a non-deterministic component inside the aggregate makes correctness unverifiable and couples business rules to a vendor's model version. Keep it at the edge, translating intent into existing use cases. |
| W27D2 | Why abstract the provider behind `IChatClient`? | Model deprecations, price/performance shifts and data-residency rules force swaps; the abstraction keeps application code stable, allows routing (small model for extraction, larger for reasoning) and lets you run **Ollama** locally with no cloud dependency in dev/CI. |
| W27D3 | Why is context a budget rather than "add more"? | Cost scales with tokens, latency grows, and relevance drops past a point (lost-in-the-middle). Budget per use case, retrieve narrowly instead of dumping, summarise on overflow. |
| W27D4 | What stops the model doing something the user isn't allowed to do? | Authorisation enforced *inside* the tool using the same policy as the HTTP API, tenant scoping, an approval gate for writes, and an audit record — the model can only propose, never decide. |
| W27D5 | Why validate model output like untrusted input? | Models confidently produce wrong shapes and values; schema validation, repair-retry and an explicit refusal path convert silent corruption into a visible error. |

**Worked example (W27D4).** Tool contract for `assign_shipment`: `{ shipmentId, vehicleId }` with `additionalProperties: false`; the handler loads the shipment **scoped to the caller's tenant**, runs the existing authorisation policy, calls the same `AssignShipmentCommand` the UI uses (idempotency key included), and if the caller lacks `dispatch:write` it returns an `ApprovalRequest` instead of performing the write. Every call — including denials — is written to `ToolInvocation` with the correlation id, so the action is traceable end to end.

### Week 28 — MCP: using it, exposing FleetTrack through it

| Day | Question | Answer |
| --- | --- | --- |
| W28D1 | What problem does MCP actually solve? | A standard, discoverable way for *any* agent host to use your tools and resources without bespoke glue per client: N×M integrations become N+M, with capability negotiation and versioning built in. |
| W28D2 | What must a coding agent never receive through MCP here? | Production data, write-capable database credentials, cloud/admin tokens, or blanket repository access. Read-only scopes and allow-lists; commit the config (`.mcp.json` + `AGENTS.md`) so it is reviewable. |
| W28D3 | Why expose read tools before write tools? | Reads demonstrate value at minimal risk; writes need the whole safety chain (authorisation, approval, idempotency, audit) before exposure is worth it. |
| W28D4 | Where does prompt injection bite hardest with MCP? | Tool *output* (documents, notes, fetched pages) entering the model's context: treat retrieved content as data, never as instructions, and keep authorisation outside the model. |
| W28D5 | How do you change a prompt or tool safely? | Version it, run the golden task suite in CI, compare scores against the baseline, roll back by version — exactly like code (ADR-022). |

**Worked example (W28D3/W28D4).** `FleetTrack.Mcp` server tools: `get_shipment`, `list_delayed_shipments`, `get_last_position`, `search_documents` (read, tenant-scoped), `create_shipment_draft` (writes only a draft + raises `ApprovalRequest`), and resources `shipment://{id}/timeline` (read-only projection). Ingress rules: the caller's token is exchanged for the same claims used by the API, every tool call re-checks tenant + role, tool descriptions are never taken from user content, and a per-tenant token/call cap prevents unbounded consumption.

### Week 29 — retrieval, agents and AI operations

| Day | Question | Answer |
| --- | --- | --- |
| W29D1 | When is RAG the wrong answer? | When the data is structured ("which shipments are late?") — a query is cheaper, exact and auditable. RAG is for unstructured documents where similarity, not exactness, is the goal. |
| W29D2 | Why cite sources in the answer? | Trust and verifiability: a citation turns "the model said" into "the POD shows", and it exposes weak retrieval instead of hiding it behind fluent prose. |
| W29D3 | What makes an agentic workflow safe? | Deterministic authorisation, approval gates for consequential writes, step/timeout/cost ceilings, idempotent effects and stuck-state handling — autonomy is bounded by explicit constraints, not good intentions. |
| W29D4 | What does an eval gate look like in CI? | Golden cases run on every PR: deterministic assertions (schema, tool choice, tenant boundary) must pass 100%, judged quality is compared against a stored baseline threshold, and failures block the merge. |
| W29D5 | What are the first three AI cost levers? | Smaller models for narrow tasks, shorter/trimmed context (retrieval instead of dumping), and caching stable prompt prefixes — before reaching for batching or streaming tricks. |

**Worked example (W29D4/W29D5).** Eval gate wiring: `dotnet run --project tools/FleetTrack.Evals --suite golden --fail-under 0.95` runs 60 cases (30 tool-selection, 20 grounded-answer, 10 red-team prompts attempting cross-tenant access) against the pinned model + prompt version, writes an `EvalRun` row (pass rate, groundedness, p95 latency, cost) and fails CI if quality drops or cost per case exceeds the budget. The dashboard plots pass rate and cost per tenant over time, so a prompt change that "feels better" but costs 3× or leaks a tenant is caught before release.

---

## 8. Commands, session log & deviation log

### 8.1 Commands (from the repo root `d:\Work\FleetTrack`)

```powershell
# build / run / test
dotnet build .\FleetTrack\FleetTrack.slnx
dotnet run   --project .\FleetTrack\src\FleetTrack.Api            # after the W1D2/W7D2 restructure
dotnet watch --project .\FleetTrack\src\FleetTrack.Api
dotnet test  .\FleetTrack\FleetTrack.slnx
dotnet format .\FleetTrack\FleetTrack.slnx --verify-no-changes

# local stack (W1D3) — either host
dotnet run --project .\FleetTrack\src\FleetTrack.AppHost           # Aspire AppHost
docker compose up -d                                               # alternative (ADR-001)

# EF Core (W2D3+) — migrations belong to the project that owns the DbContext
dotnet ef migrations add <Name> --project .\FleetTrack\src\FleetTrack.Infrastructure --startup-project .\FleetTrack\src\FleetTrack.Api
dotnet ef database update       --project .\FleetTrack\src\FleetTrack.Infrastructure --startup-project .\FleetTrack\src\FleetTrack.Api

# frontend (W20+)
npm --prefix .\web install ; npm --prefix .\web run dev ; npm --prefix .\web run build
npm --prefix .\web run test ; npx --prefix .\web playwright test

# infrastructure (W24+) — keep *.terraform.lock.hcl committed
terraform -chdir=.\infra plan ; terraform -chdir=.\infra apply
az deployment group what-if -g <rg> -f .\infra\main.bicep

# git hygiene / verification
git status --short ; git check-ignore -v <path> ; git rm -r --cached <path>
```

### 8.2 Session log

| Date | Session | Outcome / artifact |
| --- | --- | --- |
| 2026-09-30 | Repo hygiene | `.gitignore` completed and verified (`bin/`, `obj/`, `output/`, VS user files, test output, plus frontend/IaC/local-secret patterns). No generated file is tracked. |
| 2026-09-30 | The plan | `docs/plans/plan.md` is the single plan of record: **29 weeks · 145 build days**, 13 phases with gates, stack table, day-level checkboxes + focus + check question. No versions, no archived history. |
| 2026-09-30 | AI/LLM/MCP track | New **Phase 12 (W27–W29)**: LLM foundations & provider abstraction, tools over use cases, **MCP both ways**, pgvector RAG with citations, agentic workflows with approval gates, evals as CI gates, AI ops/cost/safety. Threaded earlier: agent+MCP dev setup (W1D5), pgvector baseline (W5D1), hybrid search (W10D3), OWASP LLM Top 10 (W18D5), GenAI metrics (W23D3), AI-readiness review (W26D5). |
| 2026-09-30 | Study companion | `agent.md` rebuilt: daily study loop, guardrails, architecture syllabus §4.1–**4.21** (incl. AI/MCP/RAG/evals/AI-ops/AI-assisted development), ADR register (001–**023**), domain model incl. telemetry/tenancy/**AI features**, quiz + worked examples for **all 29 weeks**. |

**Current position:** Phase 0 · **W1D1 — .NET 10 LTS bootstrap & build guardrails** (not started).

**Next session checklist (W1D1):**
1. `dotnet --list-sdks` → add `global.json` pinned to the .NET 10 SDK.
2. Add `Directory.Build.props` (`Nullable=enable`, `TreatWarningsAsErrors=true`, `EnforceCodeStyleInBuild=true`, analyzers) and `Directory.Packages.props` (central package management).
3. Add `.editorconfig`; run `dotnet build` and fix every warning that becomes an error.
4. Write **ADR-002** (runtime + guardrails) in `docs/adr/`, flip W1D1 to `[x]` in `plan.md`, log it above.
5. At the end of week 1 (W1D5): commit `AGENTS.md` + `.mcp.json` (read-only dev MCP servers) so agent assistance is guard-railed from the start.
6. Ask me: *"quiz me on week 1"* before moving to W1D2.

### 8.3 Deviation log

Anything that changes the plan (skipped days, reordered phases, swapped stack, abandoned approach) gets one line here so the plan stays honest.

| Date | Day/area | Deviation | Why |
| --- | --- | --- | --- |
| — | — | *(none yet)* | — |
