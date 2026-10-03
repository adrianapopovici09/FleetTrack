# M09 · From modules to services

**Time:** ~16 h · **Prereq:** M08 · **Outcome:** Tracking runs as its own service with its own database, behind a gateway, integrated by messages. You've experienced, measured and documented what the split costs and buys.

## Why it matters
Most senior and architect roles involve distributed systems: designing them, operating them, or untangling them. Interviewers want to hear *when* you'd split, *how* you'd do it safely, and *what breaks*. After this module you can answer from experience, not slides.

## Concepts
- **When to split** (your ADR-008 triggers): independent scaling, failure isolation, team ownership, release cadence, different technology/storage needs. "Clean code" is not a trigger.
- **Data ownership:** database-per-service; no shared tables; replicating needed data via events (**event-carried state transfer**) vs querying the owner.
- **Data migration while live:** dual writes vs change data capture vs backfill + event replay; cutover and rollback.
- **Sync vs async:** a synchronous call couples availability (A calls B → A's uptime ≤ B's uptime); prefer events; when you need sync, use gRPC with deadlines + resilience.
- **Gateway:** YARP routes, path rewrites, header propagation, keeping a stable public API while internals move.
- **Distributed failure modes:** partial failure, timeouts vs slow responses, retry storms, cascading failure, version skew between services.
- **Contract testing:** consumer-driven contracts (Pact) or schema/snapshot checks on messages and gRPC protos.
- **The "distributed monolith" smell:** services that must deploy together or call each other in chains.

Read: [Monolith to microservices, data patterns](https://learn.microsoft.com/azure/architecture/microservices/design/data-considerations) · [Strangler fig](https://learn.microsoft.com/azure/architecture/patterns/strangler-fig) · [YARP](https://learn.microsoft.com/aspnet/core/fundamentals/servers/yarp/yarp-overview) · [Microservices trade-offs (Fowler)](https://martinfowler.com/articles/microservice-trade-offs.html) · [Pact](https://docs.pact.io/)

## Labs

- [ ] **L1 · Justify and plan the split.** Apply ADR-008's triggers to every module. Write the extraction plan for Tracking: what moves (code, schema, consumers), what stays, which data Tracking needs from Shipments and how it gets it, the cutover steps and the rollback.
  ✅ **ADR-012** written *before* any code moves, including the reasons *not* to extract Dispatch or Billing.
- [ ] **L2 · New service, new database.** `src/Services/FleetTrack.Tracking.Service` (its own host, `AddFleetTrackDefaults()`, health, Wolverine) with its own Postgres database (`tracking`) in compose. Move the Tracking module code; the monolith no longer references it. The device simulator now targets the service.
  ✅ Both apps run in compose; the monolith's architecture tests prove no reference to Tracking remains; Tracking's tests run independently.
- [ ] **L3 · Data: replicate, don't share.** Tracking needs shipment destination + tenant for geofence → shipment matching. Consume `ShipmentBooked`/`ShipmentRerouted` into a local read table (event-carried state transfer). Write a one-off backfill for existing shipments (replay or export/import).
  ✅ Tracking matches arrivals with Shipments stopped. Backfill + live events produce the same data as a fresh replay (test).
- [ ] **L4 · Gateway.** `src/FleetTrack.Gateway` with **YARP**: `/api/v1/tracking/*` → Tracking service, everything else → monolith, plus SignalR/WebSocket proxying. Clients only know the gateway.
  ✅ The browser live map and the existing API tests work through the gateway unchanged.
- [ ] **L5 · One justified synchronous call.** The shipment details page needs "last known position" live: the monolith calls Tracking over **gRPC** with a 300 ms deadline, standard resilience handler, and a fallback ("position unavailable").
  ✅ Stopping the Tracking container: the shipment page still works, with a degraded field. Latency recorded with and without the call.
- [ ] **L6 · Contracts across deployables.** Move shared message contracts into a versioned `FleetTrack.Contracts` package (local NuGet feed or project reference + snapshot tests). Add a contract test that fails when the producer changes a message incompatibly.
  ✅ Changing a field type in `ShipmentBooked` breaks the contract test before anything is deployed.

## Break it
1. **Chain of sync calls:** temporarily make booking call Tracking synchronously, which calls back to Shipments. Stop Tracking and watch booking fail. Note: this is the distributed monolith.
2. **Retry storm:** set aggressive retries (5×, no jitter) on the gRPC call, slow Tracking down to 2 s, run k6, and watch the load multiply.
3. **Version skew:** deploy an old Tracking against a new message version and see what your tolerant reader does.

## Decide
**ADR-012** Extracting Tracking: triggers met, data ownership, integration style (events + one gRPC call), gateway, migration and rollback. Update **ADR-003** with the hybrid architecture.

## Quiz → [answers](../answers/M09.md)
1. Name four legitimate triggers for extracting a service and two bad reasons that teams often use.
2. Why database-per-service? What do you lose compared with one shared database?
3. Event-carried state transfer vs querying the owning service: trade-offs (freshness, availability, coupling, storage)?
4. Service A's uptime is 99.9%, and it synchronously depends on B (99.9%) and C (99.9%). What's A's best-case availability, and what does that tell you?
5. What is a distributed monolith, and what are its symptoms?
6. How do you migrate data out of a shared database without downtime? Outline the steps.
7. *Code reading:* `var pos = await trackingClient.GetLastPositionAsync(req);` with no deadline, no cancellation token and no fallback, on the shipment page. What can happen?
8. What does a gateway give you, and what must it *not* become?
9. How do you keep message contracts compatible across independently deployed services?
10. *Design:* your company has 40 microservices, deploys are coordinated in a monthly "release train", and incidents span 6 services. Diagnose and propose a plan.

## Design drill (20 min)
"We want to split our e-commerce monolith into microservices. Which service do you extract first and how, step by step?" (Pick by triggers, strangler via gateway, data migration, rollback, measure.)

## Review
M05 Q8 · M06 Q1 · M07 Q1

## Exit check
Tracking deploys independently with its own database, the gateway serves all traffic, the degraded mode is tested, the contract test is in CI, and ADR-012 explains the costs you observed.
