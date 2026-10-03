# Phase 2 — Learn & Build Tasks (W6–W8: DDD & modular-monolith boundaries)

> **Read** → **Build** → **Done when**, as before. Questions: `agent.md` §7. Checkbox: [`../plan.md`](../plan.md). Extra reading: [.NET DDD guide](https://learn.microsoft.com/dotnet/architecture/microservices/microservice-ddd-cqrs-patterns/) · [Effective Aggregate Design](https://www.dddcommunity.org/library/vernon_2011/) · [Context mapping](https://github.com/ddd-crew/context-mapping)

## Week 6 — Tactical DDD

### W6D1 · Glossary → model (ubiquitous language)
**Read:** [Ubiquitous language](https://martinfowler.com/bliki/UbiquitousLanguage.html) · [DDD primer](https://learn.microsoft.com/dotnet/architecture/microservices/microservice-ddd-cqrs-patterns/ddd-oriented-microservice)
**Build:** `docs/domain/glossary.md` — one term per concept per context (shipment, consignment, leg, stop, POD, drayage, dwell); rename code so no two names mean the same thing.
**Done when:** the glossary is committed and every domain class maps to a glossary entry.

### W6D2 · Value objects
**Read:** [Value object (Fowler)](https://martinfowler.com/bliki/ValueObject.html) · [EF value conversions](https://learn.microsoft.com/ef/core/modeling/value-conversions)
**Build:** `TrackingNumber`, `Address`, `Weight`, `Money`, `DateRange` — immutable, validated in the constructor, equality by value, mapped via converters/owned types; unit tests each.
**Done when:** an invalid VO can't be constructed and equality + DB round-trip tests pass.

### W6D3 · Aggregate root & invariants
**Read:** [Effective Aggregate Design](https://www.dddcommunity.org/library/vernon_2011/) · [Aggregate design](https://learn.microsoft.com/dotnet/architecture/microservices/microservice-ddd-cqrs-patterns/microservice-domain-model)
**Build:** `Shipment` as a real root — `AddItem`, `RemoveItem`, `Assign`, `MarkPickup`… with guards and recomputed totals; children exposed as `IReadOnlyCollection`; one invariant test per rule.
**Done when:** no public API can build an invalid shipment and each invariant has a failing-then-passing test.

### W6D4 · Lifecycle, audit & GDPR purge
**Read:** [EF Core interceptors](https://learn.microsoft.com/ef/core/logging-events-diagnostics/interceptors) · [Right to erasure](https://ico.org.uk/for-organisations/uk-gdpr-guidance-and-resources/individual-rights/individual-rights/right-to-erasure/)
**Build:** `SaveChanges` interceptor stamping created/updated by+at; soft delete + retention columns; a `purge-party-pii` command anonymising party data while keeping the shipment skeleton.
**Done when:** audit fields populate automatically and the purge test shows PII gone, aggregate intact, audit record written.

### W6D5 · Domain events (in-process)
**Read:** [Domain events: design & implementation](https://learn.microsoft.com/dotnet/architecture/microservices/microservice-ddd-cqrs-patterns/domain-events-design-implementation)
**Build:** aggregate collects events; a small dispatcher publishes them **after commit**; a `ShipmentCreated` handler (e.g. write the first read-model row).
**Done when:** a test proves exactly-one dispatch after commit and zero dispatches on rollback.

### D6 · Integrate
Refactor one anemic area into a behaviour-rich model with tests green throughout.

### D7 · Review
Summarise your own aggregate-design lessons; answer the week 6 quiz.

## Week 7 — Strategic DDD & module boundaries

### W7D1 · Bounded contexts & context map
**Read:** [Bounded context](https://martinfowler.com/bliki/BoundedContext.html) · [Context mapping patterns](https://github.com/ddd-crew/context-mapping)
**Build:** `docs/architecture/context-map.md` — Shipments, Dispatch, Tracking, Billing, Identity, Notifications; relationships (ACL, shared kernel); core vs supporting; **ADR-007**.
**Done when:** every context declares its data owner and its published contracts.

### W7D2 · Module anatomy inside the monolith
**Read:** [Modular monolith primer](https://www.kamilgrzybek.com/blog/posts/modular-monolith-primer) · [Vertical slice architecture](https://www.jimmybogard.com/vertical-slice-architecture/)
**Build:** restructure into modules, each with Api/Application/Domain/Infrastructure folders and its own registration; no cross-module EF navigation properties.
**Done when:** the Shipments module is fully moved and the solution has zero cross-module entity references.

### W7D3 · Module rules + fitness tests
**Read:** [NetArchTest](https://github.com/BenMorris/NetArchTest) · [Architectural principles](https://learn.microsoft.com/dotnet/architecture/modern-web-apps-azure/architectural-principles)
**Build:** per-module public contracts (interfaces/events) + architecture tests forbidding references to another module's internals; a minimal shared-kernel project.
**Done when:** a cross-module internal reference fails CI; the shared kernel holds only ids/`Money`/`TrackingNumber`.

### W7D4 · Extract the second module (Tracking)
**Read:** [Domain events across boundaries](https://learn.microsoft.com/dotnet/architecture/microservices/microservice-ddd-cqrs-patterns/domain-events-design-implementation)
**Build:** Tracking module skeleton (device/position tables, in/out events) wired to Shipments by id + subscription.
**Done when:** assignment events reach Tracking and a stub position update raises an event back to Shipments.

### W7D5 · The monolith → service seam (**ADR-008**)
**Read:** [MonolithFirst](https://martinfowler.com/bliki/MonolithFirst.html) · [Microservices trade-offs](https://learn.microsoft.com/dotnet/architecture/microservices/architect-microservice-container-applications/microservices-architecture)
**Build:** ADR-008: explicit split triggers (scaling profile, team split, compliance, release cadence) + consequences (data ownership, transactions, contracts, on-call cost) for Tracking.
**Done when:** the ADR names measurable conditions that would actually make you split.

### D6 · Integrate
Remove coupling the module tests exposed; one registration entry point per module.

### D7 · Review
Synchronous coupling, cross-module transactions, where idempotency becomes mandatory; week 7 quiz.

## Week 8 — Freight rules worth modelling properly

### W8D1 · Shipment lifecycle state machine
**Read:** [State pattern](https://refactoring.guru/design-patterns/state) · [Stateless library](https://github.com/dotnet-state-machine/stateless)
**Build:** legal transitions + guards + reasons on `Shipment`; illegal transition → 409 with a stable code; a test per transition (allowed *and* refused); an `allowedActions` field on the detail DTO.
**Done when:** the transition matrix is fully tested and no other code decides status with `if/switch`.

### W8D2 · Assignment & capacity rules
**Read:** [EF optimistic concurrency](https://learn.microsoft.com/ef/core/saving/concurrency)
**Build:** one active assignment per vehicle, weight/volume capacity checks, concurrency token, parallel-assignment test.
**Done when:** two concurrent assignments of one vehicle → one 200, one 409; no double booking in the DB.

### W8D3 · Pricing & charges (`Money`)
**Read:** [Money pattern (Fowler)](https://martinfowler.com/eaaCatalog/money.html) · [`decimal` and rounding](https://learn.microsoft.com/dotnet/api/system.math.round)
**Build:** charge lines + accessorials, explicit `MidpointRounding` rules, currency-safe arithmetic, totals derived from lines.
**Done when:** currency mismatch fails loudly, rounding has tests, totals always equal the sum of lines.

### W8D4 · Stop sequencing & appointment windows
**Read:** [TimeProvider](https://learn.microsoft.com/dotnet/api/system.timeprovider) · [Dates, times and time zones](https://learn.microsoft.com/dotnet/standard/datetime/)
**Build:** ordering invariants, window-overlap detection, `TimeProvider` injected wherever time matters; `FakeTimeProvider` tests across a DST boundary.
**Done when:** sequencing violations are refused and all time tests are deterministic.

### W8D5 · Exceptions & compensation
**Read:** [Compensating transaction pattern](https://learn.microsoft.com/azure/architecture/patterns/compensating-transaction)
**Build:** delay/damage/refusal states with reasons; compensation commands (release vehicle, re-plan stop, notify customer), each idempotent.
**Done when:** unhappy paths have first-class states + audit entries and replaying a compensation is a no-op.

### D6 · Integrate
Move remaining rules out of handlers into the model; delete leaky abstractions.

### D7 · Review — phase gate 2
Invariants tested, module rules enforced, ADR-007/008 written; week 8 quiz.
