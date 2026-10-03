# M05 · Modular monolith & vertical slices

**Time:** ~12 h · **Prereq:** M04 · **Outcome:** FleetTrack refactored from horizontal layers into modules with their own data, contracts and slices, with boundaries enforced by tests and ready to be split later.

## Why it matters
"How would you structure this system?" and "monolith or microservices?" are the defining architect questions. A modular monolith is the answer most experienced architects give for a new product, but only if you can show how the boundaries are kept honest.

## Concepts
- **Strategic DDD:** bounded contexts, ubiquitous language per context, context map (customer/supplier, conformist, anti-corruption layer, shared kernel).
- **Layers vs modules:** horizontal layers (`Application`, `Infrastructure`) group by *technical* concern, so every feature touches every project. Modules group by *business capability*.
- **Vertical slices:** one folder per use case (endpoint + request + handler + validation + data access). Clean-architecture layering only *inside* modules complex enough to need it.
- **Module rules:** each module owns its schema (one writer per table); other modules use its **public contract** (interfaces/DTOs/events), never its internals or tables; no cross-module joins or EF navigations.
- **Communication:** in-process calls through contracts for queries; events for reactions (the broker comes in M06).
- **Seams for later:** what makes a module extractable (data ownership, async integration, no shared transactions).

Read: [Modular monolith primer (Grzybek)](https://www.kamilgrzybek.com/blog/posts/modular-monolith-primer) · [Vertical slice architecture (Bogard)](https://www.jimmybogard.com/vertical-slice-architecture/) · [Bounded context (Fowler)](https://martinfowler.com/bliki/BoundedContext.html) · [Context mapping](https://github.com/ddd-crew/context-mapping)

## Labs

- [ ] **L1 · Find the contexts.** Walk the story *book → assign → pick up → track → deliver → invoice* and mark where words change meaning ("shipment" for sales vs "load" for dispatch). Define the contexts: **Shipments**, **Dispatch**, **Tracking**, **Billing** (stub), plus **Identity/Tenancy** (later). Draw the context map (Mermaid) with relationships and what crosses each line.
  ✅ `docs/architecture/context-map.md` + a short glossary per context.
- [ ] **L2 · Refactor to modules.** New layout: `src/FleetTrack.Host` (the composition root, formerly API) and `src/Modules/Shipments/FleetTrack.Shipments` + `FleetTrack.Shipments.Contracts`, likewise for Dispatch. Inside a module: `Features/<UseCase>/` slices; `Domain/` for the aggregate (from M04); `Data/` with the module's own `DbContext` and **Postgres schema** (`shipments`, `dispatch`). Each module exposes one `AddShipmentsModule()` / `MapShipmentsEndpoints()`.
  ✅ All existing tests are green after the move. The old `Application`/`Infrastructure` projects are deleted. Each `DbContext` only maps its own schema.
- [ ] **L3 · Enforce the boundaries.** ArchUnitNET rules: a module may reference only other modules' `*.Contracts`; `internal` by default (only contracts are public); no type in Shipments references Dispatch's `DbContext`; the Host references modules only for registration.
  ✅ Referencing `FleetTrack.Dispatch` (not `.Contracts`) from Shipments fails the build/test.
- [ ] **L4 · Cross-module query and event.** Dispatch needs shipment weight + stops to plan an assignment: expose `IShipmentsApi.GetForPlanningAsync(id)` in Shipments.Contracts (returns a DTO, never the entity). Shipments publishes `ShipmentBooked` (in-process, after commit) and Dispatch reacts by creating a planning entry.
  ✅ An integration test books a shipment and finds the Dispatch planning entry. No SQL query touches another module's schema (check with a test that inspects the generated SQL or the schema permissions).
- [ ] **L5 · Database-level isolation (stretch but recommended).** A Postgres role per module that can only access its own schema; each module's `DbContext` connects with its own role.
  ✅ A deliberate cross-schema query from Dispatch fails with `permission denied`.

## Break it
Add a "quick" EF navigation from `DispatchPlan` to `Shipment` across schemas. Count what has to change if Shipments renames a column, and note which test caught it (if none did, add one).

## Decide
- **ADR-007** Bounded contexts & module boundaries (rules, contracts, schema ownership).
- **ADR-008** When to extract a module into a service: a concrete trigger list (independent scaling profile, team ownership, release cadence, failure isolation, compliance). You'll apply it in M09.
- Update **ADR-003**: layered → modular, and why.

## Quiz → [answers](../answers/M05.md)
1. What's the practical difference between a layer and a module? Why do horizontal layers alone not prevent coupling?
2. How do you identify a bounded context boundary in a real business conversation?
3. Why must each module own its tables, even inside one database?
4. Module A needs data owned by module B on every request. What are your three options, and their trade-offs?
5. What is a shared kernel, and why should it stay tiny?
6. *Code reading:* `public class ShipmentService(ShipmentsDbContext s, DispatchDbContext d)` in the Shipments module. What's wrong and how would you fix it?
7. Vertical slices duplicate some code across features. When is that duplication acceptable, and when do you extract shared code?
8. What makes a module *extractable* later? Name four properties.
9. Name three reasons to choose a modular monolith over microservices for a new product, and two reasons that would flip the decision.
10. *Design:* two teams will work on FleetTrack next year. How would you split ownership, and what would you enforce so they don't block each other in one repo and one deployable?

## Design drill (20 min)
"You've inherited a 7-year-old monolith with 300 tables and everything references everything. Management wants microservices. What do you do?" (Module boundaries first, measure coupling, strangler, when not to split.)

## Review
M04 Q2 · M04 Q3 · M03 Q3

## Exit check
Modules with their own schemas, boundary tests in CI, a cross-module event + contract query tested, and ADR-007/008 written.
