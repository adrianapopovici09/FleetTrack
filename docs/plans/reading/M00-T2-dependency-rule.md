# M00-T2 reading notes: layered architecture and the dependency rule

Task: [M00-T2](../modules/M00-foundations.md) · Sources: [Architectural principles (Microsoft)](https://learn.microsoft.com/dotnet/architecture/modern-web-apps-azure/architectural-principles) · [Common web application architectures: Clean Architecture](https://learn.microsoft.com/dotnet/architecture/modern-web-apps-azure/common-web-application-architectures#clean-architecture)

---

## 1. The principles behind it

Microsoft's guide lists a handful of principles. These are the ones that come up most in reviews and interviews.

### Separation of concerns
Code that does different jobs should live in different places. Business rules ("a vehicle can't be assigned to two drivers at once") shouldn't be mixed with database code or HTTP code. When they're mixed, you can't change one without risking the other, and you can't test the rules without a database.

### Encapsulation
A class or module hides how it works and exposes only what others need. Others depend on the *what* (its public methods), not the *how*. That's what lets you change the inside without breaking the callers.

### Dependency inversion
This is the most important one for this task. Normally high-level code (business logic) calls low-level code (the database), so it depends on it. **Dependency inversion** flips this:
- The business layer defines an **interface** describing what it needs, for example `IVehicleRepository` with `GetById` and `Save`.
- The database layer **implements** that interface.

Now the database layer depends on the business layer, not the other way round. The business logic knows nothing about Postgres or EF Core.

### Explicit dependencies
A class should ask for everything it needs through its constructor, rather than secretly creating it or grabbing it from a global. That makes dependencies visible and lets tests pass in fakes. Dependency injection in ASP.NET Core is built around this.

### Single responsibility
A class should have one reason to change. A class that validates input, calculates prices *and* sends emails changes whenever any of those three requirements change.

### Persistence ignorance
Domain objects (`Vehicle`, `Trip`) shouldn't know how they're stored: no EF attributes, no SQL, no base class from a database library. Then the domain stays simple to test and isn't tied to one storage technology.

### Bounded contexts
A large system is split into areas, each with its own model and its own data. "Vehicle" might mean something different to the maintenance area than to the billing area, and that's fine as long as each area owns its own model. (FleetTrack uses this from M05 onwards.)

---

## 2. The dependency rule and Clean Architecture

**The dependency rule:** source code dependencies may only point **inward**, toward the business logic. Inner layers never reference outer layers.

Clean Architecture (and its close relatives, **Onion Architecture** and **Hexagonal Architecture / Ports and Adapters**) arranges code in rings:

| Layer | Contains | Depends on |
| --- | --- | --- |
| **Domain** (the centre) | Entities, value objects, business rules | Nothing |
| **Application** | Use cases ("assign driver to vehicle"), and interfaces for what it needs from the outside world | Domain |
| **Infrastructure** | EF Core, email sending, external API clients: the *implementations* of those interfaces | Application (to implement its interfaces) |
| **API / UI** | HTTP endpoints; also wires everything together at startup | Application (and Infrastructure only for registration) |

In Hexagonal Architecture terms:
- An interface the core defines (`IVehicleRepository`) is a **port**.
- A class that implements it for a specific technology (`EfVehicleRepository`) is an **adapter**.

### Why it's worth it

- **Testability:** you can test the business logic with simple fakes, no database needed.
- **Replaceability:** swapping Postgres for another database, or one email provider for another, touches only Infrastructure.
- **Enforced by the compiler:** if Domain has no project reference to EF Core, nobody *can* use EF Core in Domain. A rule enforced by the build is far stronger than one written in a wiki.

### When *not* to use it (interviewers love this)

- **Simple CRUD apps** ("create, read, update, delete" with almost no business rules). Four projects and lots of interfaces add ceremony with no benefit. A single project with clear folders is often better.
- **Prototypes and short-lived tools.**
- **Interfaces with exactly one implementation that will never change** are sometimes pure overhead. Senior engineers add an abstraction where it buys testability or flexibility, not by reflex.
- **Vertical Slice Architecture** is a popular alternative. Code is organised by *feature* ("AssignDriver" folder with its endpoint, logic and data access together) rather than by technical layer. It reduces jumping between projects, at the cost of less strict separation.

---

## Interview questions this prepares you for

- "Explain dependency inversion with an example." (Business layer owns the interface; infrastructure implements it.)
- "Where does the `IRepository` interface live, and why?" (In the inner layer that *uses* it, not next to the implementation.)
- "When would you not use Clean Architecture?"
- "How do you stop people from breaking the layering six months from now?" (Project references, plus architecture tests, see [T6](M00-T6-test-harness.md).)

## For FleetTrack

`API → Application → Domain`, `Infrastructure → Application`. The reference matrix is recorded in ADR-003.
