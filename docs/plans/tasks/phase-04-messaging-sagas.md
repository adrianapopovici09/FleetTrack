# Phase 4 — Learn & Build Tasks (W11–W13: messaging, sagas & resilience)

> **Read** → **Build** → **Done when**. Questions: `agent.md` §7. Checkbox: [`../plan.md`](../plan.md). Extra reading: [Asynchronous messaging](https://learn.microsoft.com/azure/architecture/guide/technology-choices/messaging) · [Transactional outbox](https://learn.microsoft.com/azure/architecture/patterns/transactional-outbox-ids) · [Retry](https://learn.microsoft.com/azure/architecture/patterns/retry) · [Saga](https://learn.microsoft.com/azure/architecture/reference-architectures/saga/saga) · [Circuit breaker](https://learn.microsoft.com/azure/architecture/patterns/circuit-breaker)

## Week 11 — Broker, contracts and the publish path

### W11D1 · Broker choice & topology (**ADR-009**)
**Read:** [RabbitMQ tutorials](https://www.rabbitmq.com/tutorials) · [Exchange types](https://www.rabbitmq.com/tutorials/amqp-concepts) · [Kafka intro](https://kafka.apache.org/intro) · [ASP.NET Core + RabbitMQ (MassTransit)](https://learn.microsoft.com/dotnet/architecture/microservices/multi-container-microservice-net-applications/rabbitmq-event-bus-development-testing)
**Build:** add RabbitMQ to the AppHost; define naming + exchange/queue/DLQ topology; publish one real event (`ShipmentStatusChanged`) and consume it in a worker project.
**Done when:** the message is visible in the management UI, a DLQ exists, and ADR-009 records RabbitMQ vs Service Bus vs Kafka with costs.

### W11D2 · Messaging framework decision (**ADR-010**)
**Read:** [MassTransit licence terms](https://massient.com/license) · [MassTransit v8 → v9 changes](https://masstransit.io/) · [Wolverine messaging](https://wolverinefx.net/guide/messaging/introduction.html)
**Build:** wrap the W11D1 publish/consume in the chosen framework (Wolverine or MassTransit v8 pinned); record licence/support implications and the migration cost in ADR-010.
**Done when:** the abstraction is in place, four typical scenarios (publish, consume, retry, DLQ) work, and the licence position is explicit.

### W11D3 · Integration contracts
**Read:** [Schema evolution / message versioning](https://learn.microsoft.com/azure/architecture/best-practices/api-design) · [Pact contract testing](https://docs.pact.io/) · [Semantic versioning](https://semver.org/)
**Build:** `FleetTrack.Contracts` with versioned messages (no domain types), additive-evolution rules written down, and consumer-driven contract tests for one producer/consumer pair.
**Done when:** renaming a domain property doesn't change any contract, and the contract test fails if a consumer's expectation is broken.

### W11D4 · Publish path with the outbox
**Read:** [Transactional outbox pattern](https://learn.microsoft.com/azure/architecture/patterns/transactional-outbox-ids) · [Wolverine + EF Core outbox](https://wolverinefx.net/guide/durability/efcore/outbox.html) · [MassTransit outbox](https://masstransit.io/documentation/patterns/transactional-outbox)
**Build:** messages written to an outbox table in the same transaction as the state change; a relay publishes them with `MessageId` + `traceparent` headers; publish-after-commit tested.
**Done when:** killing the process between commit and publish still delivers the message on restart (test it by stopping the broker).

### W11D5 · Consumer design & inbox
**Read:** [Idempotent consumer pattern](https://learn.microsoft.com/azure/architecture/reference-architectures/containers/aks-microservices/aks-microservices-advanced) · [Idempotency keys](https://datatracker.ietf.org/doc/draft-ietf-httpapi-idempotency-key-header/)
**Build:** a consumer with an inbox table deduping by message id, a poison-message path to the DLQ, and a replay tool/endpoint.
**Done when:** delivering the same message three times applies the effect once, and a poisoned message can be fixed and replayed.

### D6 · Integrate
Broker-down drill: stop RabbitMQ → write via API → restart → assert no loss and no duplicate business effect; record it in `docs/ops/`.

### D7 · Review
Choreography vs orchestration; event vs command naming; week 11 quiz.

## Week 12 — Sagas & long-running processes

### W12D1 · Process design
**Read:** [Saga pattern](https://learn.microsoft.com/azure/architecture/reference-architectures/saga/saga) · [Process manager (Enterprise Integration Patterns)](https://www.enterpriseintegrationpatterns.com/patterns/messaging/ProcessManager.html)
**Build:** a sequence diagram for assignment → driver acceptance → timeout → escalation; decide saga vs choreography and record why.
**Done when:** the design shows every wait, every deadline and every compensation branch.

### W12D2 · Implement the dispatch saga
**Read:** [Wolverine sagas](https://wolverinefx.net/guide/durability/sagas.html) · [MassTransit state machine](https://masstransit.io/documentation/patterns/saga/state-machine)
**Build:** persisted saga state with optimistic concurrency, guarded idempotent transitions (`AwaitingAcceptance → Confirmed | Escalated → Reassigned | Cancelled`).
**Done when:** restarting the process mid-saga resumes correctly, and duplicate messages don't double-apply.

### W12D3 · Timeouts & scheduling
**Read:** [Wolverine scheduled messages](https://wolverinefx.net/guide/messaging/scheduled.html) · [Quartz.NET](https://www.quartz-scheduler.net/) · [MassTransit scheduling](https://masstransit.io/documentation/patterns/scheduling)
**Build:** deadline messages driving the timeout branch, plus one recurring reconciliation job; timeouts visible in logs/traces.
**Done when:** a saga sitting past its deadline escalates automatically (test with fake time).

### W12D4 · Compensation
**Read:** [Compensating transaction](https://learn.microsoft.com/azure/architecture/patterns/compensating-transaction)
**Build:** compensation commands (release the vehicle, cancel the appointment, notify the customer) that are idempotent and audited.
**Done when:** replaying a compensation is a no-op and the original failure plus compensation are both visible in the audit trail.

### W12D5 · Operate the flow
**Read:** [Health endpoint monitoring](https://learn.microsoft.com/aspnet/core/host-and-deploy/health-checks) · [Aspire dashboard](https://learn.microsoft.com/dotnet/aspire/fundamentals/dashboard/overview)
**Build:** a saga-state endpoint (counts by state and age), a stuck-saga query/alert, and a replay runbook.
**Done when:** you can answer "which flows are stuck right now and why" from the dashboard + endpoint.

### D6 · Integrate
Failure-injection suite: crash mid-flow, duplicate delivery, out-of-order events — each with an asserted outcome.

### D7 · Review
Process manager, routing slip, event-carried state transfer and their failure modes; week 12 quiz.

## Week 13 — Resilience & load control

### W13D1 · Policy catalogue (Polly v8)
**Read:** [Polly docs](https://www.pollydocs.org/) · [Retry pattern](https://learn.microsoft.com/azure/architecture/patterns/retry) · [Circuit breaker](https://learn.microsoft.com/azure/architecture/patterns/circuit-breaker)
**Build:** named pipelines per dependency class (timeout → retry+jitter → circuit breaker → bulkhead) applied to HTTP clients, EF transient faults and the broker; policy tests with a flaky stub.
**Done when:** a dependency failing 100% fails fast (breaker open) instead of holding threads, and the policy tests prove the behaviour.

### W13D2 · Backpressure & load shedding
**Read:** [Rate limiting middleware](https://learn.microsoft.com/aspnet/core/performance/rate-limit) · [`System.Threading.Channels`](https://learn.microsoft.com/dotnet/core/extensions/channels) · [Queue-based load levelling](https://learn.microsoft.com/azure/architecture/patterns/queue-based-load-leveling)
**Build:** bounded channels/queues with concurrency limits, queue-depth metrics, and explicit 429 vs 503 semantics under overload.
**Done when:** a load test shows graceful degradation (rejections, not OOM/timeouts) at 2× expected load.

### W13D3 · Rate limiting & quotas
**Read:** [Rate limiting middleware](https://learn.microsoft.com/aspnet/core/performance/rate-limit) · [Throttling pattern](https://learn.microsoft.com/azure/architecture/patterns/throttling)
**Build:** per-tenant/per-API-key limits with `Retry-After`, partition keys derived from tenant + endpoint, and metrics per partition.
**Done when:** one abusive tenant is throttled while others are unaffected (prove it with a concurrent load test).

### W13D4 · Graceful degradation
**Read:** [Feature flags (Azure App Configuration)](https://learn.microsoft.com/azure/azure-app-configuration/concept-feature-management) · [Bulkhead pattern](https://learn.microsoft.com/azure/architecture/patterns/bulkhead)
**Build:** a read-only mode, cached fallbacks for non-critical reads, explicit health states (healthy/degraded/unhealthy) and emergency feature flags.
**Done when:** killing Redis/Seq/RabbitMQ leaves booking + tracking usable and reports "degraded" honestly.

### W13D5 · Chaos & failure drills
**Read:** [Chaos engineering principles](https://principlesofchaos.org/) · [Azure Chaos Studio](https://learn.microsoft.com/azure/chaos-studio/chaos-studio-overview)
**Build:** inject broker/DB/Redis failure + latency; record behaviour, recovery time and detection quality in a drill report.
**Done when:** `docs/ops/failure-drills.md` contains three drills with before/after evidence and named runbook steps.

### D6 · Integrate
Consolidate resilience configuration in one place; delete ad-hoc retries/try-catch and replace them with policies.

### D7 · Review — phase gate 4
Outbox/inbox proven under outage, saga times out and compensates, policy catalogue and drill report complete; week 13 quiz.