# Phase 11 — Learn & Build Tasks (W25–W26: system design, capstone & staff skills)

> **Read** → **Build** → **Done when**. Questions: `agent.md` §7. Checkbox: [`../plan.md`](../plan.md). Extra reading: [System Design Primer](https://github.com/donnemartin/system-design-primer) · [Azure architecture center](https://learn.microsoft.com/azure/architecture/) · [C4 model](https://c4model.com/) · [ADR](https://adr.github.io/) · [Postmortem culture (Google SRE)](https://sre.google/sre-book/postmortem-culture/)

## Week 25 — System design practice (grounded in FleetTrack)

> Each day: **clarify requirements → estimate capacity → 2–3 candidate designs → trade-off table → decision + risks + monitoring → write it up** in `docs/design/`. Use the back-of-envelope model in `agent.md` §4.15 (5 000 devices × 1 ping/10 s = 500 msg/s; 10 000 watchers = 1 000 push/s).

### W25D1 · Case: high-throughput telemetry ingestion
**Read:** [Azure IoT reference architecture](https://learn.microsoft.com/azure/architecture/reference-architectures/iot) · [Performance antipatterns](https://learn.microsoft.com/azure/architecture/antipatterns/)
**Build:** `docs/design/01-telemetry-ingestion.md` — capacity maths, batching/backpressure design, storage choice, failure modes (device flood, broker outage), what you would monitor.
**Done when:** every number traces back to a stated assumption and the design names what you would *not* do (e.g. Kafka at 500 msg/s).

### W25D2 · Case: event-driven shipment pipeline with saga
**Read:** [Saga reference architecture](https://learn.microsoft.com/azure/architecture/reference-architectures/saga/saga) · [Idempotent consumer](https://learn.microsoft.com/azure/architecture/reference-architectures/containers/aks-microservices/aks-microservices-advanced)
**Build:** `docs/design/02-shipment-pipeline.md` — ordering, duplication, timeouts, compensation, visibility of in-flight flows, why orchestration beats choreography here.
**Done when:** the design states failure semantics per hop (at-least-once + idempotent effect) and how stuck flows are detected.

### W25D3 · Case: multi-tenant SaaS isolation & noisy neighbours
**Read:** [Multitenant architecture](https://learn.microsoft.com/azure/architecture/guide/multitenant/overview) · [Tenancy models](https://learn.microsoft.com/azure/architecture/guide/multitenant/considerations/tenancy-models)
**Build:** `docs/design/03-multitenancy.md` — isolation vs cost vs compliance, per-tenant quotas and metrics, what changes if a customer demands DB-per-tenant.
**Done when:** the design includes cost-per-tenant and the migration path between tenancy models.

### W25D4 · Case: real-time tracking at 100k concurrent watchers
**Read:** [SignalR scale-out](https://learn.microsoft.com/aspnet/core/signalr/scale) · [Event-driven architecture](https://learn.microsoft.com/azure/architecture/guide/architecture-styles/event-driven)
**Build:** `docs/design/04-realtime-fanout.md` — fan-out maths, coalescing, backplane/edge strategy, per-watcher cost, degradation when the push tier saturates.
**Done when:** the arithmetic (watchers × update rate) and the cost levers are explicit.

### W25D5 · Case: batch billing & reconciliation
**Read:** [Batch processing](https://learn.microsoft.com/azure/architecture/data-guide/big-data/batch-processing) · [Idempotency keys](https://datatracker.ietf.org/doc/draft-ietf-httpapi-idempotency-key-header/)
**Build:** `docs/design/05-batch-billing.md` — idempotent runs keyed by (tenant, period), late data + corrections, audit of adjustments, reprocessing rules.
**Done when:** the design shows a re-run cannot double-bill and every correction is traceable.

### D6 · Integrate
Red-team your own designs against the "when NOT" rules in `agent.md` §4 and correct the documents in place.

### D7 · Review
Design-interview framing (requirements → constraints → options → decision → risks → monitoring); week 25 quiz.

## Week 26 — Capstone hardening & portfolio

### W26D1 · Capstone scope
**Read:** [Definition of done (Agile Alliance)](https://www.agilealliance.org/glossary/definition-of-done/) · [Demo-driven development](https://martinfowler.com/bliki/DemoDrivenDevelopment.html)
**Build:** decide the capstone slice (tenant onboarding → book → assign → live track → documents → billing summary), acceptance criteria, and a scripted demo that runs from a clean clone.
**Done when:** the demo script has exact commands/steps and a seeded dataset, and someone else can run it unaided.

### W26D2 · Hardening pass
**Read:** [OWASP ASVS](https://owasp.org/www-project-application-security-verification-standard/) · [Performance budgets](https://web.dev/articles/performance-budgets-101) · [Technical debt quadrant](https://martinfowler.com/bliki/TechnicalDebtQuadrant.html)
**Build:** close a written hardening list — authorisation matrix re-check, secret scan, dependency/vulnerability scan, accessibility pass, dead code + feature-flag removal, verified backup/restore, SLO evidence.
**Done when:** `docs/ops/hardening-checklist.md` is fully ticked and every item links to an artifact or a measurement.

### W26D3 · Architecture documentation
**Read:** [C4 model](https://c4model.com/) · [ADRs](https://adr.github.io/) · [arc42](https://arc42.org/overview)
**Build:** `docs/architecture/` with C4 context/container/component diagrams, the ADR index (001–023), a **"why not" page** (rejected option + reason per ADR) and a one-page system overview.
**Done when:** a new senior engineer can explain the system after 30 minutes with the docs and no verbal handover.

### W26D4 · .NET 11 upgrade day
**Read:** [What's new in .NET](https://learn.microsoft.com/dotnet/core/whats-new/) · [Breaking changes](https://learn.microsoft.com/dotnet/core/compatibility/) · [Upgrade assistant](https://learn.microsoft.com/dotnet/core/porting/upgrade-assistant-overview)
**Build:** upgrade SDK/TFM/dependencies, run the whole suite (unit, integration, architecture, E2E), fix breaking changes, and record a delta note (what broke; what it says about your fitness functions).
**Done when:** the upgrade PR is green in CI and the delta note is in `docs/ops/`.

### W26D5 · AI-readiness review
**Read:** [`Microsoft.Extensions.AI`](https://learn.microsoft.com/dotnet/ai/microsoft-extensions-ai) · [Model Context Protocol](https://modelcontextprotocol.io/) · [OWASP LLM Top 10](https://genai.owasp.org/llm-top-10/)
**Build:** an AI-readiness checklist (are use cases tool-shaped, idempotent, tenant-authorised, auditable?) plus a ranked candidate list of AI use cases with the test "would a query answer this better?".
**Done when:** the list names ≥2 use cases you decide **not** to build with AI, and why.

### D6 · Integrate
Documentation set: `README.md` refresh, runbooks index, onboarding guide, demo script, portfolio summary.

### D7 · Review
Retrospective: what to keep, what to drop, the next 90 days; week 26 quiz.

**Phase gate 11:** 5 design docs + 20+ ADRs + C4 diagrams · capstone demo runs from a clean clone · .NET 11 upgrade complete.