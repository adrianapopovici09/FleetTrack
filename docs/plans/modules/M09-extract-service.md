# M09 · From modules to services

**Time:** ~17 h · **Prereq:** M08 · **Outcome:** Tracking runs as its own service with its own database, behind a gateway, integrated by messages. You've experienced, measured and documented what the split costs and buys.

**Why it matters:** most senior and architect roles involve distributed systems. Interviewers want to hear *when* you'd split, *how* you'd do it safely, and *what breaks*. After this module you answer from experience, not slides.

Each task: 📖 **Learn** → 🔨 **Build** → ✅ **Done when**.

---

### T1 · Justify and plan the split
- 📖 **Learn (60 min):** legitimate triggers vs fashion; the distributed-monolith trap. [Microservice trade-offs (Fowler)](https://martinfowler.com/articles/microservice-trade-offs.html) · [Microservice prerequisites (Fowler)](https://martinfowler.com/bliki/MicroservicePrerequisites.html) · [How to break a monolith into microservices (Dehghani)](https://martinfowler.com/articles/break-monolith-into-microservices.html)
- 🔨 **Build:** apply ADR-008's triggers to every module. Write the Tracking extraction plan: what moves (code, schema, consumers), what stays, what data Tracking needs from Shipments and how it gets it, cutover steps and rollback.
- ✅ **Done when:** **ADR-012** is written *before* any code moves, including why *not* to extract Dispatch or Billing.

### T2 · New service, new database
- 📖 **Learn (30 min):** [Database-per-service](https://microservices.io/patterns/data/database-per-service.html) · [Data considerations for microservices](https://learn.microsoft.com/azure/architecture/microservices/design/data-considerations)
- 🔨 **Build:** `src/Services/FleetTrack.Tracking.Service` (its own host, `AddFleetTrackDefaults()`, health, Wolverine) with its own `tracking` database in compose. Move the Tracking module code; the monolith no longer references it; the simulator now targets the service.
- ✅ **Done when:** both apps run in compose, the monolith's architecture tests prove no Tracking reference remains, and Tracking's tests run independently.

### T3 · Replicate data, don't share it
- 📖 **Learn (30 min):** [Event-carried state transfer (Fowler, "What do you mean by event-driven?")](https://martinfowler.com/articles/201701-event-driven.html) · [Materialized view pattern](https://learn.microsoft.com/azure/architecture/patterns/materialized-view)
- 🔨 **Build:** Tracking consumes `ShipmentBooked` / `ShipmentRerouted` into a local read table (destination + tenant), plus a one-off backfill for existing shipments.
- ✅ **Done when:** Tracking matches arrivals with Shipments stopped, and backfill + live events equal a fresh replay (test).

### T4 · Gateway with YARP
- 📖 **Learn (45 min):** [YARP overview](https://learn.microsoft.com/aspnet/core/fundamentals/servers/yarp/yarp-overview) · [YARP configuration files](https://learn.microsoft.com/aspnet/core/fundamentals/servers/yarp/config-files) · [Gateway routing pattern](https://learn.microsoft.com/azure/architecture/patterns/gateway-routing) · [WebSockets through YARP](https://learn.microsoft.com/aspnet/core/fundamentals/servers/yarp/websockets)
- 🔨 **Build:** `src/FleetTrack.Gateway`: `/api/v1/tracking/*` → Tracking, everything else → monolith, including SignalR/WebSocket proxying. Clients only know the gateway.
- ✅ **Done when:** the live map and the existing API tests work through the gateway unchanged.

### T5 · One justified synchronous call
- 📖 **Learn (45 min):** sync calls couple availability. [gRPC client factory](https://learn.microsoft.com/aspnet/core/grpc/clientfactory) · [Build resilient HTTP apps (standard resilience handler)](https://learn.microsoft.com/dotnet/core/resilience/http-resilience) · [Circuit breaker pattern](https://learn.microsoft.com/azure/architecture/patterns/circuit-breaker)
- 🔨 **Build:** the shipment page needs the live "last known position": the monolith calls Tracking over gRPC with a 300 ms deadline, the standard resilience handler, and a fallback ("position unavailable").
- ✅ **Done when:** with Tracking stopped, the shipment page still works with a degraded field; latency is recorded with and without the call.

### T6 · Contracts across deployables
- 📖 **Learn (30 min):** [Consumer-driven contracts (Fowler)](https://martinfowler.com/articles/consumerDrivenContracts.html) · [Pact .NET](https://github.com/pact-foundation/pact-net)
- 🔨 **Build:** move shared message contracts into a versioned `FleetTrack.Contracts` package (local NuGet feed, or a project reference + snapshot tests), plus a contract test that fails on an incompatible producer change.
- ✅ **Done when:** changing a field type in `ShipmentBooked` breaks the contract test before anything is deployed.

### T7 · Break it
- 📖 **Learn (15 min):** [Retry storm antipattern](https://learn.microsoft.com/azure/architecture/antipatterns/retry-storm/)
- 🔨 **Build:** (1) a sync call chain booking → Tracking → Shipments, then stop Tracking. (2) Aggressive retries (5×, no jitter) with Tracking slowed to 2 s under k6. (3) An old Tracking version receiving a new message version.
- ✅ **Done when:** each failure is recorded with what you observed; update ADR-012 and ADR-003 with the costs you saw.

---

## Quiz → [answers](../answers/M09.md)
1. Name four legitimate triggers for extracting a service and two bad reasons that teams often use.
2. Why database-per-service? What do you lose compared with one shared database?
3. Event-carried state transfer vs querying the owning service: trade-offs?
4. Service A's uptime is 99.9%, and it synchronously depends on B (99.9%) and C (99.9%). What's A's best-case availability, and what does that tell you?
5. What is a distributed monolith, and what are its symptoms?
6. How do you migrate data out of a shared database without downtime? Outline the steps.
7. *Code reading:* `var pos = await trackingClient.GetLastPositionAsync(req);` with no deadline, no cancellation token and no fallback, on the shipment page. What can happen?
8. What does a gateway give you, and what must it *not* become?
9. How do you keep message contracts compatible across independently deployed services?
10. *Design:* your company has 40 microservices, deploys are coordinated in a monthly "release train", and incidents span 6 services. Diagnose and propose a plan.

## Design drill (20 min)
"We want to split our e-commerce monolith into microservices. Which service do you extract first and how, step by step?"

## Review
M05 Q8 · M06 Q1 · M07 Q1

## Exit check
Tracking deploys independently with its own database, the gateway serves all traffic, the degraded mode is tested, the contract test is in CI, and ADR-012 explains the costs you observed.
