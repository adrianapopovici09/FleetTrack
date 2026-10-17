# M00-T2 · Layered architecture and the dependency rule

Task: [M00-T2](../modules/M00-foundations.md)

This file is the complete lesson for the task; you don't need to read anything else. Original sources, if you ever want to go deeper: [Architectural principles (Microsoft)](https://learn.microsoft.com/dotnet/architecture/modern-web-apps-azure/architectural-principles) · [Common web application architectures: Clean Architecture](https://learn.microsoft.com/dotnet/architecture/modern-web-apps-azure/common-web-application-architectures#clean-architecture)

**Reading time:** about 35 minutes.

## Contents

1. [The principles behind layering](#1-the-principles-behind-layering)
2. [Dependency inversion, with a worked example](#2-dependency-inversion-with-a-worked-example)
3. [The dependency rule and Clean Architecture](#3-the-dependency-rule-and-clean-architecture)
4. [The composition root: who wires it all together](#4-the-composition-root-who-wires-it-all-together)
5. [How the rule gets broken in practice](#5-how-the-rule-gets-broken-in-practice)
6. [Layers are not boundaries](#6-layers-are-not-boundaries)
7. [When not to use it, and the alternatives](#7-when-not-to-use-it-and-the-alternatives)
8. [Glossary](#8-glossary)
9. [Summary, check yourself, interview questions, for FleetTrack](#9-summary)

---

## 1. The principles behind layering

Microsoft's architecture guide lists a handful of principles. Layered and Clean Architecture are really just these principles applied to a whole solution.

**Separation of concerns.** Code that does different jobs should live in different places. A business rule like "a vehicle can't be assigned to two drivers at once" shouldn't sit in the same method as SQL and HTTP-status handling. If it does, you can't change the database code without risking the rule, and you can't test the rule without a database and a web server.

**Encapsulation.** A class or module hides *how* it works and exposes only *what* others need. A `Shipment` class exposes `MarkDelivered()` instead of a public `Status` setter. That way the rule "you can't deliver a cancelled shipment" is checked in one place, and nobody can bypass it.

**Dependency inversion.** High-level policy shouldn't depend on low-level details; both should depend on an abstraction owned by the high-level side. This is the most important principle for this task, so section 2 works through it in full.

**Explicit dependencies.** A class should ask for everything it needs through its **constructor**, not create it secretly (`new NpgsqlConnection(...)` inside a method) or grab it from a global (`ServiceLocator.Get<...>()`). Explicit dependencies are visible in the signature, and tests can pass in fakes. ASP.NET Core's dependency injection (DI) container is built around this idea.

**Single responsibility.** A class should have one reason to change. A `ShipmentService` that validates input, calculates prices *and* sends emails has to change whenever pricing rules, email templates or validation rules change, and every change risks breaking the other two jobs.

**Persistence ignorance.** Domain objects (`Vehicle`, `Shipment`) shouldn't know how they're stored. That means no EF Core attributes, no SQL, and no base class from a database library. The domain is then trivial to unit-test and isn't tied to one storage technology.

**Bounded contexts.** A large system is split into areas, each with its own model and its own data. "Vehicle" means *mileage and service history* to Maintenance, but *capacity and current location* to Dispatch. Forcing one shared `Vehicle` class on both produces a bloated class that's hard to change. FleetTrack uses bounded contexts from M05 onwards.

---

## 2. Dependency inversion, with a worked example

### 2.1 Without inversion

```csharp
// Application layer
public class AssignDriverHandler
{
    public async Task Handle(Guid vehicleId, Guid driverId)
    {
        await using var db = new FleetDbContext();                 // depends directly on EF Core
        var vehicle = await db.Vehicles.FindAsync(vehicleId);
        vehicle!.AssignDriver(driverId);
        await db.SaveChangesAsync();
    }
}
```

The use case (high-level policy) now depends on EF Core and on Postgres (low-level details):
- To unit-test "assign driver", you need a database.
- Switching storage means editing every handler.
- The Application project must reference EF Core, so *every* class in it can use EF Core. Nothing stops the next developer from writing raw SQL inside a business rule.

### 2.2 With inversion

The **Application layer** says what it *needs*, as an interface it owns:

```csharp
// FleetTrack.Application
public interface IVehicleRepository
{
    Task<Vehicle?> GetAsync(Guid id, CancellationToken ct);
    Task SaveAsync(Vehicle vehicle, CancellationToken ct);
}

public class AssignDriverHandler(IVehicleRepository vehicles)      // explicit dependency
{
    public async Task Handle(Guid vehicleId, Guid driverId, CancellationToken ct)
    {
        var vehicle = await vehicles.GetAsync(vehicleId, ct)
                      ?? throw new VehicleNotFoundException(vehicleId);
        vehicle.AssignDriver(driverId);                             // the business rule lives in Domain
        await vehicles.SaveAsync(vehicle, ct);
    }
}
```

The **Infrastructure layer** provides it:

```csharp
// FleetTrack.Infrastructure  (references Application)
public class EfVehicleRepository(FleetDbContext db) : IVehicleRepository
{
    public Task<Vehicle?> GetAsync(Guid id, CancellationToken ct) => db.Vehicles.FindAsync([id], ct).AsTask();
    public Task SaveAsync(Vehicle v, CancellationToken ct) => db.SaveChangesAsync(ct);
}
```

### 2.3 Flow of control vs source-code dependencies

This is the subtle part, so here is a picture:

```
                 at RUNTIME (who calls whom)

   AssignDriverHandler ──calls──► IVehicleRepository ──is──► EfVehicleRepository ──► Postgres

                 at COMPILE TIME (who references whom)

   FleetTrack.Application  ◄──references──  FleetTrack.Infrastructure
   (owns IVehicleRepository)                (implements it)
```

At runtime, control flows **outward**: the handler ends up calling EF Core. But the **source-code dependency** points **inward**: Infrastructure references Application, never the reverse. The dependency has been *inverted* relative to the flow of control. That's what the name means.

**Who owns the interface matters.** The interface lives in the layer that **uses** it (Application), not next to the implementation (Infrastructure). If `IVehicleRepository` lived in Infrastructure, Application would have to reference Infrastructure to see it, and you'd be back to the original dependency.

---

## 3. The dependency rule and Clean Architecture

**The dependency rule:** source-code dependencies may only point **inward**, toward the business logic. An inner layer never references an outer layer, and never even mentions a type from one.

Clean Architecture (and its close relatives **Onion Architecture** and **Hexagonal Architecture**, also called **Ports and Adapters**) arranges code in rings:

```
        ┌───────────────────────────────────────────────┐
        │  API (endpoints, Program.cs)                  │
        │   ┌───────────────────────────────────────┐   │
        │   │  Infrastructure (EF Core, email, HTTP │   │
        │   │  clients: the adapters)               │   │
        │   │   ┌───────────────────────────────┐   │   │
        │   │   │  Application (use cases,      │   │   │
        │   │   │  ports = interfaces it needs) │   │   │
        │   │   │   ┌───────────────────────┐   │   │   │
        │   │   │   │  Domain (entities,    │   │   │   │
        │   │   │   │  rules); depends on   │   │   │   │
        │   │   │   │  nothing              │   │   │   │
        │   │   │   └───────────────────────┘   │   │   │
        │   │   └───────────────────────────────┘   │   │
        │   └───────────────────────────────────────┘   │
        └───────────────────────────────────────────────┘
                  arrows (references) point inward only
```

| Layer | Contains | References |
| --- | --- | --- |
| **Domain** | Entities (`Vehicle`), value objects (`Coordinates`), business rules, domain events | Nothing; not even `Microsoft.*` frameworks |
| **Application** | Use cases (`AssignDriverHandler`), the **ports** (interfaces) they need, DTOs for input/output | Domain |
| **Infrastructure** | The **adapters**: EF Core `DbContext` and repositories, email senders, external API clients | Application (to implement its ports) |
| **API** | HTTP endpoints, request/response mapping, `Program.cs` (the composition root, §4) | Application, plus Infrastructure for registration only |

In Hexagonal terms:
- A **port** is an interface the core defines (`IVehicleRepository`, `IClock`, `IEmailSender`).
- An **adapter** is a class that implements a port for one specific technology (`EfVehicleRepository`, `SmtpEmailSender`). There are also *driving* adapters that call **into** the core, like an HTTP endpoint or a message consumer.

### Why it's worth it

- **Testability.** Business logic can be tested with simple in-memory fakes, in milliseconds, with no database.
- **Replaceability.** Swapping one email provider for another touches only Infrastructure and the registration code.
- **Enforced by the compiler.** If the Domain project has no reference to EF Core, nobody *can* use EF Core in Domain, because it won't compile. A rule the build enforces is far stronger than one written in a wiki. FleetTrack goes a step further with architecture tests (M00-T6) that also catch things project references can't, such as a `Microsoft.*` package reference sneaking into Domain.

---

## 4. The composition root: who wires it all together

If Application only knows `IVehicleRepository`, *something* still has to say "when someone asks for `IVehicleRepository`, give them an `EfVehicleRepository`". That something is the **composition root**: the single place, at application startup, where all the concrete types are registered with the DI container. In ASP.NET Core, that's `Program.cs` in the API project.

That's why **API references Infrastructure** even though endpoints should never use Infrastructure types directly. The reference exists **only for registration**:

```csharp
// Program.cs (API): the composition root, as FleetTrack has it today
builder.Services.AddFleetTrackDefaults();                  // health checks etc. (API)
builder.Services.AddInfrastructure(builder.Configuration); // adapters (Infrastructure)
// an AddApplication() will join these once Application has use cases to register
```

FleetTrack recorded this exception in **ADR-003**. The trade-off is that because API can see Infrastructure, an endpoint *could* inject `FleetDbContext` directly and bypass Application. There are two common guards:
- An **architecture test** that allows API types to depend on Infrastructure only from the registration code (for example "only `Program` and `*Extensions` classes may use `FleetTrack.Infrastructure`"). FleetTrack doesn't have this rule yet; it's worth adding once there's a `DbContext` to misuse.
- A separate tiny **"Host" project** as the composition root, so the API project itself never references Infrastructure.

---

## 5. How the rule gets broken in practice

These are the mistakes you'll be asked to spot in code reviews:

- **Leaking infrastructure types through a port.** A repository method returning `IQueryable<Vehicle>` lets the caller write LINQ that EF Core translates into SQL. Application then depends on EF Core *behaviour* (what's translatable) without referencing EF Core at all. Prefer ports that return finished results (`Task<IReadOnlyList<Vehicle>>`), or a query specification.
- **Framework types in Domain.** `[Required]`, `[Column("plate")]` or `DbContext` in a domain class ties the domain to ASP.NET or EF Core. Use EF Core's fluent configuration in Infrastructure instead.
- **Application referencing Infrastructure "just for one helper".** Once the reference exists, everything in Infrastructure is reachable, and the rule is gone for good.
- **A `Common` / `Shared` / `Utils` project referenced by everything.** It slowly fills with unrelated code and couples all layers together. Put each helper next to the layer that owns its concept.
- **Business rules in endpoints or handlers.** For example, `if (vehicle.Status == "Retired") return BadRequest()` in an endpoint. The rule should live in the domain (`vehicle.AssignDriver` throws, or returns a failure result), and the endpoint only translates the result into HTTP. That's FleetTrack architecture rule 4: endpoints and handlers orchestrate.

Notice that **project references only catch the first kind of mistake partially and the others not at all.** That's why architecture tests and code review are both still needed.

---

## 6. Layers are not boundaries

This is an important limitation, and it's the reason FleetTrack moves on to a modular monolith in M05.

Layers separate code by **technical role**: domain, use cases, data access, HTTP. They say nothing about **business areas**. In a layered app, *every* feature lives in the same four projects. A `Shipment` handler can call the `Billing` repository, Dispatch's EF entities can have navigation properties into Tracking's tables, and they can all share one `DbContext`. Over time, every feature becomes coupled to every other feature, even though the layering is perfect. This is sometimes called a **"layered big ball of mud"**.

**Boundaries** (bounded contexts, modules) split the code by **business capability**: Shipments, Dispatch, Billing, Tracking. Each one owns its data and exposes only a small public contract. M05 adds those boundaries, and keeps the layering *inside* each module.

You can't draw good module boundaries before you understand the domain. Boundaries drawn too early, in the wrong place, are harder to fix than no boundaries at all. That's why FleetTrack starts layered (ADR-003) and only splits into modules once the features show where they naturally cluster.

---

## 7. When not to use it, and the alternatives

Interviewers love "when would you *not* do this?"

- **Simple CRUD apps** (create, read, update, delete with almost no business rules). Four projects, repositories and mapping layers add ceremony with no benefit. One project with clear folders is often better.
- **Prototypes, spikes and short-lived tools**, where speed matters more than long-term structure.
- **Interfaces with exactly one implementation that will never change** are sometimes pure overhead. Senior engineers add an abstraction where it buys testability or flexibility, not out of habit. For example, wrapping `DbContext` (which is already a repository and unit of work) in a generic `IRepository<T>` that only forwards calls is widely considered an anti-pattern.

**Vertical Slice Architecture** is the popular alternative. Code is organised by **feature**, not by technical layer: an `AssignDriver/` folder holds its endpoint, its handler, its validation and its data access together.
- **Pros:** a change to one feature touches one folder; there's less jumping between projects and less mapping code; each slice can choose its own approach (EF Core for one, raw SQL for another).
- **Cons:** separation within a slice is less strict, shared rules can get duplicated across slices, and it needs discipline to keep domain logic out of the handlers.
- In practice many teams **combine** the two: vertical slices inside modules, with a shared domain model per module.

---

## 8. Glossary

| Term | Meaning |
| --- | --- |
| **Layer** | A group of code with one technical role (Domain, Application, Infrastructure, API) |
| **Dependency rule** | Source-code references point inward only; inner layers never mention outer ones |
| **Dependency inversion** | High-level code owns an abstraction; low-level code implements it, so the reference points "upward" against the flow of control |
| **Port** | An interface the core defines for something it needs (`IVehicleRepository`) |
| **Adapter** | A class implementing a port for a specific technology (`EfVehicleRepository`), or one that drives the core (an endpoint) |
| **Composition root** | The single startup place (`Program.cs`) where concrete types are registered with DI |
| **DI (dependency injection)** | A container creates objects and passes in their constructor dependencies |
| **Persistence ignorance** | Domain types know nothing about how they're stored |
| **Bounded context** | A business area with its own model and its own data (M05) |
| **Vertical slice** | Code organised by feature, with each feature's endpoint, logic and data access together |
| **DTO (data transfer object)** | A plain object for carrying data across a boundary (for example a request body), with no behaviour |

---

## 9. Summary

1. Layering applies separation of concerns, dependency inversion and persistence ignorance to the whole solution.
2. Dependency inversion means the layer that **uses** an interface owns it. At runtime control flows outward, but references point inward.
3. Domain references nothing, Application references Domain, and Infrastructure references Application. API references both, but uses Infrastructure only in the composition root.
4. Project references enforce part of the rule; architecture tests and reviews catch the rest (leaky `IQueryable`, framework attributes, rules in endpoints).
5. Layers separate *technical* roles, not *business* areas, so they don't create boundaries. That's what modules add in M05.
6. Don't use full layering for simple CRUD or prototypes; vertical slices are a common alternative.

### Check yourself

1. Draw the runtime call chain and the compile-time reference chain for `AssignDriverHandler` → Postgres. Which way does each point? (§2.3)
2. Why must `IVehicleRepository` live in Application rather than Infrastructure? (§2.3)
3. Why does API reference Infrastructure, and what's the risk of that reference? (§4)
4. A repository returns `IQueryable<Shipment>`. Which rule does that quietly break? (§5)
5. Your layers are perfect, but changing Billing keeps breaking Dispatch. Why didn't layering prevent that? (§6)
6. Name one situation where you'd recommend a single project with folders instead. (§7)

### Interview questions this prepares you for

- "Explain dependency inversion with an example." (The business layer owns the interface, and infrastructure implements it; runtime flow and reference direction are opposite.)
- "Where does the `IRepository` interface live, and why?"
- "What's a composition root, and why does the API project reference Infrastructure?"
- "When would you *not* use Clean Architecture?"
- "How do you stop people from breaking the layering six months from now?" (Project references, architecture tests, an ADR explaining the rules, and code review for what tools can't see.)
- "Clean Architecture vs vertical slices: which would you choose, and why?"

### For FleetTrack

- The references are `API → Application → Domain` and `Infrastructure → Application`, plus `API → Infrastructure` for the composition root only. The reference matrix and that exception are in **ADR-003**.
- `FleetTrack.ArchitectureTests` enforces the inward rules: Domain depends on no other layer and no `Microsoft.*`, and Application depends on neither Infrastructure nor API. Nothing yet restricts *where* API may use Infrastructure (§4).
- `AddInfrastructure(...)` in Infrastructure registers its adapters and is called from `Program.cs`.
- Watch out for business rules drifting into endpoints as soon as real features arrive (M02–M04).
