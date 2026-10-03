# Phase 5 — Learn & Build Tasks (W14–W16: real-time tracking & telemetry)

> **Read** → **Build** → **Done when**. Questions: `agent.md` §7. Checkbox: [`../plan.md`](../plan.md). Extra reading: [TimescaleDB docs](https://docs.tigerdata.com/) · [PostGIS docs](https://postgis.net/docs/) · [SignalR intro](https://learn.microsoft.com/aspnet/core/signalr/introduction)

## Week 14 — Telemetry ingestion

### W14D1 · Ingestion contract & device model
**Read:** [Azure IoT architecture](https://learn.microsoft.com/azure/architecture/guide/iot/) · [Device identity & auth](https://learn.microsoft.com/azure/iot-hub/iot-hub-devguide-security)
**Build:** `POST /api/v1/telemetry` accepting batched pings (`deviceId`, `occurredAt`, lat/lon, speed, heading) with device API-key/HMAC auth; explicit rules for late, duplicate and out-of-order pings; clock-skew tolerance in the contract.
**Done when:** the contract states behaviour for late/duplicate/out-of-order/skewed pings, and validation rejects malformed batches.

### W14D2 · High-throughput ingest path
**Read:** [`System.Threading.Channels`](https://learn.microsoft.com/dotnet/core/extensions/channels) · [Background services](https://learn.microsoft.com/aspnet/core/fundamentals/host/hosted-services) · [Queue-based load levelling](https://learn.microsoft.com/azure/architecture/patterns/queue-based-load-leveling)
**Build:** a bounded `Channel<PositionBatch>` (~10k) + a background drain batching 500 rows with idempotent upserts; `429 + Retry-After` when full; throughput + p95 measured with a synthetic device flood.
**Done when:** the benchmark reports msg/s and p95, and overload rejects rather than growing memory.

### W14D3 · Time-series storage (**ADR-011**)
**Read:** [Timescale hypertables](https://docs.tigerdata.com/use-timescale/latest/hypertables/) · [Retention & compression](https://docs.tigerdata.com/use-timescale/latest/data-retention/) · [Postgres partitioning](https://www.postgresql.org/docs/current/ddl-partitioning.html)
**Build:** a `position` hypertable with 1-day chunks + retention/compression policies (or partition-per-day equivalent); compare plans and write throughput against a plain table.
**Done when:** ADR-011 holds measured numbers (write throughput, query latency, storage) and retention is applied.

### W14D4 · Hot-read patterns
**Read:** [Efficient querying](https://learn.microsoft.com/ef/core/performance/efficient-querying) · [Materialised view pattern](https://learn.microsoft.com/azure/architecture/patterns/materialized-view)
**Build:** "last known position per vehicle" (maintained table/cache), paged track history, distance/dwell derived by continuous aggregate or job; benchmark each read.
**Done when:** last-position is an index seek served from cache in <10 ms at 5k vehicles.

### W14D5 · Geospatial layer
**Read:** [PostGIS geography](https://postgis.net/docs/using_postgis_dbmanagement.html) · [`ST_Contains`](https://postgis.net/docs/ST_Contains.html) · [Spatial indexing](https://postgis.net/workshops/postgis-intro/indexing.html)
**Build:** `Geofence` polygons + spatial index; enter/exit detection with hysteresis (no flapping); point-in-polygon and boundary tests.
**Done when:** a simulated crossing produces exactly one enter and one exit event.

### D6 · Integrate
Run ingest + read load together; tune chunk interval, batch size and retention from measurements.

### D7 · Review
Why "one row per ping in the normal table" fails (index bloat, vacuum, query cost); week 14 quiz.

## Week 15 — Real-time push architecture

### W15D1 · Transport decision (**ADR-012**)
**Read:** [SignalR introduction](https://learn.microsoft.com/aspnet/core/signalr/introduction) · [gRPC streaming](https://learn.microsoft.com/aspnet/core/grpc/streaming) · [Server-sent events (MDN)](https://developer.mozilla.org/docs/Web/API/Server-sent_events/Using_server-sent_events)
**Build:** implement both an SSE endpoint and a SignalR hub for the same "latest positions" feed; compare reconnect, auth, fan-out cost and browser support.
**Done when:** ADR-012 decides per interaction (map = SignalR, ticker = SSE) with spike evidence.

### W15D2 · Hub design & subscription authorisation
**Read:** [SignalR groups](https://learn.microsoft.com/aspnet/core/signalr/groups) · [SignalR authn/authz](https://learn.microsoft.com/aspnet/core/signalr/authn-and-authz)
**Build:** `FleetHub` with per-tenant/shipment groups, `IUserIdProvider`, and an authorisation check on subscribe (tenant + role for that shipment).
**Done when:** a test proves an unauthorised client cannot subscribe to another tenant's shipment group.

### W15D3 · Scale-out & backplane
**Read:** [SignalR scale-out](https://learn.microsoft.com/aspnet/core/signalr/scale) · [Redis backplane](https://learn.microsoft.com/aspnet/core/signalr/redis-backplane)
**Build:** Redis backplane; two API instances pushing to one client; monitor connection counts and message rates.
**Done when:** with two instances, a client on instance A receives updates published on instance B.

### W15D4 · Event → push pipeline
**Read:** [Event-driven architecture](https://learn.microsoft.com/azure/architecture/guide/architecture-styles/event-driven) · [Back pressure](https://www.reactivemanifesto.org/glossary#Back-Pressure)
**Build:** a coalescing buffer (latest position per vehicle, flushed ~2 s) feeding the hub, with per-group throttling/dedupe and monotonic sequence numbers.
**Done when:** clients receive bounded updates/second regardless of ingest rate, with ordering preserved per shipment.

### W15D5 · Subscriptions, presence & privacy
**Read:** [OWASP API1: BOLA](https://owasp.org/API-Security/editions/2023/en/0xa1-broken-object-level-authorization/) · [SignalR groups](https://learn.microsoft.com/aspnet/core/signalr/groups)
**Build:** per-role payload trimming, subscription caps per user, presence ("who is watching"), per-client update-rate limits.
**Done when:** a driver-role client cannot receive unauthorised fields or shipments (test-proven).

### D6 · Integrate
End-to-end drill: simulated device → ingestion → event → live push rendered in a throwaway page; capture one trace across the path.

### D7 · Review
Freshness vs consistency, fan-out cost modelling, reconnect + backfill; week 15 quiz.

## Week 16 — Tracking features & algorithms

### W16D1 · Geofence-driven state transitions
**Read:** [Domain events vs integration events](https://learn.microsoft.com/dotnet/architecture/microservices/microservice-ddd-cqrs-patterns/domain-events-design-implementation) · [Saga pattern](https://learn.microsoft.com/azure/architecture/reference-architectures/saga/saga)
**Build:** `GeofenceEntered`/`Exited` → Shipments handler → legal transition (`InTransit → AtStop`) → status event + `ShipmentStatusChanged` published.
**Done when:** a simulated arrival moves shipment state with no manual call, and an impossible transition is refused and logged.

### W16D2 · ETA computation
**Read:** [PostGIS `ST_Distance`](https://postgis.net/docs/ST_Distance.html) · [Caching guidance](https://learn.microsoft.com/azure/architecture/best-practices/caching)
**Build:** ETA from remaining distance + recent speed + historical dwell, returned as a window with confidence and freshness; short-TTL cache; optional routing-provider upgrade path.
**Done when:** cached ETAs are computed in <20 ms and the response states basis + freshness.

### W16D3 · Alerts & rules engine
**Read:** [Alerting on SLOs](https://sre.google/workbook/alerting-on-slos/) · [Technology choices (async processing)](https://learn.microsoft.com/azure/architecture/guide/technology-choices/messaging)
**Build:** per-tenant rule storage (speeding, idle, dwell, deviation, temperature) evaluated in a background consumer — never in the ingest path.
**Done when:** thresholds are data (no redeploy) and evaluation runs off the hot path (prove with an ingest benchmark).

### W16D4 · Alert delivery
**Read:** [Monitoring distributed systems (SRE book)](https://sre.google/sre-book/monitoring-distributed-systems/)
**Build:** notifications via the worker with severity, mute windows, dedupe keys, escalation, and an audit trail.
**Done when:** 1 000 triggering positions produce a bounded number of notifications (dedupe proven).

### W16D5 · Event fan-out to modules
**Read:** [Event-carried state transfer](https://martinfowler.com/articles/201701-event-driven.html)
**Build:** Tracking → Shipments/Billing/Notifications contracts; publish the event map diagram in `docs/architecture/`.
**Done when:** the event map names producer, consumer and payload per cross-module event, and no module reads another's tables.

### D6 · Integrate
Refactor duplicated position queries into read models and re-measure.

### D7 · Review — phase gate 5
Ingestion benchmark, live push demo, geofence→state machine integration, ADR-011/012 accepted; week 16 quiz.