# M04 · Domain modelling (tactical DDD)

**Time:** ~9 h · **Prereq:** M03 · **Outcome:** a behaviour-rich `Shipment` aggregate whose invariants can't be broken by callers, plus a clear errors-vs-exceptions policy.

**Why it matters:** "where does business logic live?" separates senior from mid-level answers. Anemic models with logic scattered across services are the most common maintainability problem in enterprise .NET.

Each task: 📖 **Learn** → 🔨 **Build** → ✅ **Done when**.

---

### T1 · Value objects
- 📖 **Learn (40 min):** a value object is immutable, validated on construction, and has equality by value. [Implement value objects](https://learn.microsoft.com/dotnet/architecture/microservices/microservice-ddd-cqrs-patterns/implement-value-objects) · [ValueObject (Fowler)](https://martinfowler.com/bliki/ValueObject.html) · [EF Core complex types](https://learn.microsoft.com/ef/core/what-is-new/ef-core-8.0/whatsnew#value-objects-using-complex-types)
- 🔨 **Build:** `TrackingNumber` (format + check digit), `Address`, `Weight` (unit conversion), `Money` (currency-safe arithmetic, explicit rounding), `TimeWindow`, mapped with complex types/converters.
- ✅ **Done when:** unit tests show invalid values can't be constructed, `Money(10, EUR) + Money(5, USD)` fails, and persisted values read back equal.

### T2 · The Shipment aggregate & its invariants
- 📖 **Learn (60 min):** an aggregate is a consistency boundary: one per transaction, reference others by id, and keep it small. [Effective Aggregate Design, part 1 (Vernon, PDF)](https://www.dddcommunity.org/wp-content/uploads/files/pdf_articles/Vernon_2011_1.pdf) · [Design a DDD-oriented domain model](https://learn.microsoft.com/dotnet/architecture/microservices/microservice-ddd-cqrs-patterns/ddd-oriented-microservice) · [Implement a domain model with EF Core](https://learn.microsoft.com/dotnet/architecture/microservices/microservice-ddd-cqrs-patterns/net-core-microservice-domain-model)
- 🔨 **Build:** private setters + a factory method; `AddStop`, `RemoveStop` and `AddItem` maintain totals and stop order; collections exposed as `IReadOnlyList`.
- ✅ **Done when:** invariant tests pass (stop sequence contiguous, totals consistent, a delivered shipment can't be modified), and no public setter remains on the aggregate.

### T3 · Lifecycle state machine
- 📖 **Learn (30 min):** making states and transitions explicit beats if-chains. [State pattern (Refactoring.Guru)](https://refactoring.guru/design-patterns/state) · [Stateless library README](https://github.com/dotnet-state-machine/stateless) (for ideas; a hand-written transition table is fine)
- 🔨 **Build:** `Draft → Booked → Assigned → PickedUp → InTransit → Delivered`, plus `Cancelled` and `OnHold`. Each transition takes a reason and appends a `StatusHistory` entry. An illegal transition returns a failure that becomes a 409 at the API.
- ✅ **Done when:** a parameterised test covers **every** (from, to) pair: legal ones succeed, all others fail.

### T4 · Result pattern at the boundary
- 📖 **Learn (30 min):** expected business failures as values, exceptions for the unexpected. [Railway-oriented programming (Wlaschin)](https://fsharpforfunandprofit.com/rop/) (the concept only) · [Exceptions best practices](https://learn.microsoft.com/dotnet/standard/exceptions/best-practices-for-exceptions)
- 🔨 **Build:** a small `Result<T>` / `Error` type; map error codes to ProblemDetails in one place; update ADR-004.
- ✅ **Done when:** handlers have no `try/catch` for business failures, and the error → HTTP mapping is one class with tests.

### T5 · Domain events after commit + testable time
- 📖 **Learn (40 min):** [Domain events: design and implementation](https://learn.microsoft.com/dotnet/architecture/microservices/microservice-ddd-cqrs-patterns/domain-events-design-implementation) · [SaveChanges interceptors](https://learn.microsoft.com/ef/core/logging-events-diagnostics/interceptors#savechanges-interception) · [TimeProvider overview](https://learn.microsoft.com/dotnet/standard/datetime/timeprovider-overview)
- 🔨 **Build:** the aggregate records `ShipmentBooked` / `ShipmentDelivered`; a `SaveChanges` interceptor dispatches them *after* a successful commit to in-process handlers. Time-based rules use `TimeProvider`.
- ✅ **Done when:** a test proves a failed commit dispatches nothing, and time rules are tested with `FakeTimeProvider`.

### T6 · Break it
- 📖 **Learn (15 min, optional):** [Stryker.NET mutation testing](https://stryker-mutator.io/docs/stryker-net/introduction/)
- 🔨 **Build:** make `Stops` a public `List<>` and write a "helpful" service that adds a stop directly. Which invariant test fails, and which one *doesn't* but should? Stretch: run Stryker on Domain and inspect the surviving mutants.
- ✅ **Done when:** you've written a journal note and added the missing test.

---

## Quiz → [answers](../answers/M04.md)
1. What's the difference between an entity and a value object? Give two FleetTrack examples of each.
2. Why should an aggregate reference other aggregates by id rather than by navigation property?
3. `Shipment` and `Vehicle` must both change when a vehicle is assigned. One transaction or two? Justify it.
4. Why dispatch domain events after commit rather than before?
5. *Code reading:* `public List<Stop> Stops { get; set; } = new();` Name three invariants this makes impossible to guarantee.
6. When are exceptions still the right choice even if you use a Result type?
7. How do you test "a shipment can't be picked up more than 48 h after booking" without `Thread.Sleep` or flaky clocks?
8. When is an anemic model perfectly fine?
9. Why is `decimal` + an explicit rounding mode required for `Money`, and why should `Money` carry its currency?
10. *Design:* how would you model a shipment that is split into two partial deliveries? What changes in the aggregate?

## Design drill (20 min)
"Walk me through how you'd model an order-to-delivery flow in a logistics system: aggregates, boundaries and what happens when two dispatchers act at once."

## Review
M03 Q2 · M03 Q9 · M02 Q4

## Exit check
No public setters on the aggregate, a full transition-table test, after-commit events tested, and ADR-004 updated.
