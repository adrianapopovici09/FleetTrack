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
