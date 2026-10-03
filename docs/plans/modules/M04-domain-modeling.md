# M04 · Domain modelling (tactical DDD)

**Time:** ~8 h · **Prereq:** M03 · **Outcome:** a behaviour-rich `Shipment` aggregate whose invariants can't be broken by callers, plus a clear errors-vs-exceptions policy.

## Why it matters
"Where does business logic live?" separates senior from mid-level answers. Anemic models with logic scattered across services are the most common maintainability problem in enterprise .NET.

## Concepts
- **Value objects:** immutable, validated on construction, equality by value (`record` / `readonly record struct`), EF complex types.
- **Aggregates:** a consistency boundary, not a table group; one aggregate per transaction; reference other aggregates by id; small aggregates.
- **Invariants:** rules that must always hold, enforced by the root's methods. Collections are exposed read-only.
- **State machines:** explicit legal transitions instead of if-chains.
- **Domain events:** facts raised by the aggregate, dispatched after commit.
- **Result pattern vs exceptions:** expected business failures as values; exceptions for the unexpected.
- **Time:** `TimeProvider` for deterministic tests.
- **When NOT to:** CRUD-only areas (settings, reference data) don't need DDD.

Read: [Effective Aggregate Design (Vernon, part 1)](https://www.dddcommunity.org/wp-content/uploads/files/pdf_articles/Vernon_2011_1.pdf) · [Domain model with DDD (.NET guide)](https://learn.microsoft.com/dotnet/architecture/microservices/microservice-ddd-cqrs-patterns/net-core-microservice-domain-model) · [Value objects](https://learn.microsoft.com/dotnet/architecture/microservices/microservice-ddd-cqrs-patterns/implement-value-objects) · [TimeProvider](https://learn.microsoft.com/dotnet/standard/datetime/timeprovider-overview)

## Labs

- [ ] **L1 · Value objects.** `TrackingNumber` (format + check digit), `Address`, `Weight` (unit conversion), `Money` (currency-safe arithmetic, explicit rounding), `TimeWindow`. Map them with EF complex types / converters.
  ✅ Unit tests: invalid values can't be constructed; `Money(10, EUR) + Money(5, USD)` fails; persisted and re-read values are equal.
- [ ] **L2 · The `Shipment` aggregate.** Private setters and a factory method; `AddStop`, `RemoveStop`, `AddItem` maintain totals and stop ordering; collections exposed as `IReadOnlyList`.
  ✅ Invariant tests (stop sequence contiguous, totals always consistent, can't modify a delivered shipment); no public setter left on the aggregate.
- [ ] **L3 · Lifecycle state machine.** `Draft → Booked → Assigned → PickedUp → InTransit → Delivered`, plus `Cancelled` and `OnHold`, each transition with a reason, appending a `StatusHistory` entry. Illegal transition → `Result.Failure(ShipmentErrors.InvalidTransition)` → 409 at the API.
  ✅ A parameterised test covers **every** (from, to) pair: legal ones succeed, all others fail.
- [ ] **L4 · Result pattern at the boundary.** A small `Result<T>` / `Error` type (or a vetted library); map error codes to ProblemDetails in one place (update ADR-004).
  ✅ Handlers have no `try/catch` for business failures; the error → HTTP mapping table is a single class with tests.
- [ ] **L5 · Domain events after commit.** The aggregate records `ShipmentBooked`, `ShipmentDelivered`; a `SaveChanges` interceptor dispatches them *after* a successful commit to in-process handlers.
  ✅ A test proves a failed commit dispatches nothing. Time-dependent rules use `TimeProvider` (`FakeTimeProvider` in tests).

## Break it
Temporarily make `Stops` a public `List<>` and write a "helpful" service that adds a stop directly. Show which invariant test fails, or worse, which one *doesn't* fail and should. Then run **Stryker.NET** on the Domain project (stretch) and look at surviving mutants.

## Decide
Update **ADR-004**: Result vs exceptions, final mapping rules.

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
