# M05 · Modular monolith & vertical slices

**Time:** ~13 h · **Prereq:** M04 · **Outcome:** FleetTrack refactored from horizontal layers into modules with their own data, contracts and slices, with boundaries enforced by tests and ready to be split later.

**Why it matters:** "how would you structure this system?" and "monolith or microservices?" are the defining architect questions. A modular monolith is the answer most experienced architects give for a new product, but only if you can show how the boundaries are kept honest.

Each task: 📖 **Learn** → 🔨 **Build** → ✅ **Done when**.

---

### T1 · Find the bounded contexts
- 📖 **Learn (60 min):** a bounded context is where a word has one meaning; a context map shows how contexts relate (customer/supplier, conformist, anti-corruption layer, shared kernel). [BoundedContext (Fowler)](https://martinfowler.com/bliki/BoundedContext.html) · [DDD Crew: context mapping](https://github.com/ddd-crew/context-mapping) · [Identify domain-model boundaries](https://learn.microsoft.com/dotnet/architecture/microservices/architect-microservice-container-applications/identify-microservice-domain-model-boundaries)
- 🔨 **Build:** walk the story *book → assign → pick up → track → deliver → invoice* and mark where words change meaning. Define **Shipments**, **Dispatch**, **Tracking**, **Billing** (stub), and later **Identity/Tenancy**. Draw the context map (Mermaid) with relationships and what crosses each line.
- ✅ **Done when:** `docs/architecture/context-map.md` exists with a short glossary per context.

### T2 · Refactor layers into modules with vertical slices
- 📖 **Learn (60 min):** layers group by technical concern, while modules and slices group by business capability. [Modular monolith primer (Grzybek)](https://www.kamilgrzybek.com/blog/posts/modular-monolith-primer) · [Vertical slice architecture (Bogard)](https://www.jimmybogard.com/vertical-slice-architecture/) · [Modular monolith with DDD (sample repo)](https://github.com/kgrzybek/modular-monolith-with-ddd) (skim the structure only)
- 🔨 **Build:** `src/FleetTrack.Host` (the composition root, formerly API) and `src/Modules/Shipments/FleetTrack.Shipments` + `FleetTrack.Shipments.Contracts`, same for Dispatch. Inside a module: `Features/<UseCase>/` slices, `Domain/` (from M04), and `Data/` with the module's own `DbContext` and **Postgres schema** (`shipments`, `dispatch`). Each module exposes `AddShipmentsModule()` / `MapShipmentsEndpoints()`.
- ✅ **Done when:** all existing tests are green, the old `Application`/`Infrastructure` projects are deleted, and each `DbContext` maps only its own schema.

### T3 · Enforce the boundaries
- 📖 **Learn (30 min):** [ArchUnitNET guide](https://archunitnet.readthedocs.io/en/latest/guide/) · [Internal access modifier & InternalsVisibleTo](https://learn.microsoft.com/dotnet/standard/assembly/friend)
- 🔨 **Build:** rules: a module may reference only other modules' `*.Contracts`; types are `internal` by default (only contracts are public); Shipments never touches Dispatch's `DbContext`; the Host references modules only for registration.
- ✅ **Done when:** referencing `FleetTrack.Dispatch` (not `.Contracts`) from Shipments fails a test.

### T4 · Cross-module query and event
- 📖 **Learn (30 min):** options for module communication: in-process contract calls vs events vs local copies. [Communication in a modular monolith (Grzybek)](https://www.kamilgrzybek.com/blog/posts/modular-monolith-integration-styles)
- 🔨 **Build:** `IShipmentsApi.GetForPlanningAsync(id)` in Shipments.Contracts, returning a DTO and never the entity. Shipments publishes `ShipmentBooked` (in-process, after commit), and Dispatch reacts by creating a planning entry.
- ✅ **Done when:** an integration test books a shipment and finds the planning entry, and no SQL touches another module's schema.

### T5 · Database-level isolation (recommended)
- 📖 **Learn (20 min):** [Postgres schemas](https://www.postgresql.org/docs/current/ddl-schemas.html) · [Privileges (GRANT)](https://www.postgresql.org/docs/current/ddl-priv.html)
- 🔨 **Build:** a Postgres role per module that can only access its own schema, with each module's `DbContext` connecting as its role.
- ✅ **Done when:** a deliberate cross-schema query from Dispatch fails with `permission denied`.

### T6 · Break it
- 🔨 **Build:** add a "quick" EF navigation from `DispatchPlan` to `Shipment` across schemas. List everything that must change if Shipments renames a column, and which test caught it (add one if none did). Revert.
- ✅ **Done when:** you've written a journal note and added the guarding test if one was missing.

### T7 · Decide: ADR-007, ADR-008, update ADR-003
- 📖 **Learn (30 min):** [Microservice trade-offs (Fowler)](https://martinfowler.com/articles/microservice-trade-offs.html) · [MonolithFirst (Fowler)](https://martinfowler.com/bliki/MonolithFirst.html)
- 🔨 **Build:** **ADR-007** bounded contexts & module rules. **ADR-008** the trigger list for extracting a service (scaling profile, team ownership, release cadence, failure isolation, compliance), which you'll apply in M09. Update **ADR-003**: layered → modular.
- ✅ **Done when:** ADR-008 has concrete, checkable triggers, not "when it gets big".

---

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
"You've inherited a 7-year-old monolith with 300 tables and everything references everything. Management wants microservices. What do you do?"

## Review
M04 Q2 · M04 Q3 · M03 Q3

## Exit check
Modules with their own schemas, boundary tests in CI, a cross-module event + contract query tested, and ADR-007/008 written.
