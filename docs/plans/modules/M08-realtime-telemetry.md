# M08 · Real-time telemetry

**Time:** ~16 h · **Prereq:** M07 (M01-L3 parser) · **Outcome:** a Tracking module that ingests device telemetry over gRPC at a measured rate, stores it efficiently, detects geofence events and pushes live positions to clients.

## Why it matters
High-throughput ingestion and real-time push are classic system-design topics, and they're where naive CRUD designs collapse. This module gives you real numbers to quote.

## Concepts
- **gRPC:** protobuf contracts, unary vs server/client/bidirectional streaming, deadlines, HTTP/2, why it suits device→backend and service→service traffic.
- **Backpressure:** bounded `System.Threading.Channels` (`BoundedChannelFullMode`), batching, what to do when full (wait, drop, reject).
- **Unreliable devices:** retries, duplicates, out-of-order and late data, clock skew; idempotency keys `(device_id, recorded_at)`.
- **Time-series in Postgres:** declarative range partitioning by day, BRIN indexes, retention by dropping partitions, `COPY` binary import.
- **Spatial:** PostGIS `geography`, GiST indexes, `ST_Contains`/`ST_DWithin`, NetTopologySuite in EF.
- **Push:** SignalR hubs, groups per tenant/shipment, coalescing (latest position only), throttling, reconnection; a Redis-protocol backplane (Valkey) for scale-out.

Read: [gRPC on .NET](https://learn.microsoft.com/aspnet/core/grpc/) · [Channels](https://learn.microsoft.com/dotnet/core/extensions/channels) · [Postgres partitioning](https://www.postgresql.org/docs/current/ddl-partitioning.html) · [PostGIS intro](https://postgis.net/workshops/postgis-intro/) · [SignalR scale-out](https://learn.microsoft.com/aspnet/core/signalr/scale)

## Labs

- [ ] **L1 · Postgres image with PostGIS + pgvector.** `deploy/postgres/Dockerfile` `FROM postgis/postgis:17-3.5` + install `postgresql-17-pgvector`; switch compose to build it. (pgvector is used in M14; one custom image now saves a migration later.)
  ✅ `CREATE EXTENSION postgis; CREATE EXTENSION vector;` both succeed in a migration.
- [ ] **L2 · Tracking module + gRPC ingest.** `telemetry.proto` with a client-streaming `Ingest(stream PositionBatch) returns (IngestAck)`. Plus a **device simulator** console app: N devices driving along routes, with configurable rate, duplicates and out-of-order sends.
  ✅ The simulator streams 1,000 devices × 1 msg/s; the service acks; integration test with a gRPC client.
- [ ] **L3 · Bounded pipeline + batch writer.** gRPC handler → bounded `Channel<Position>` → a `BackgroundService` writer batching (500 rows or 200 ms) with binary `COPY` into a staging table, then `INSERT … ON CONFLICT DO NOTHING` (dedupe). Expose the channel depth as a metric/log.
  ✅ `docs/perf/M08-ingest.md`: sustained msg/s, p95 ack latency, and behaviour when the channel is full (you choose and justify: block the stream vs reject).
- [ ] **L4 · Partitioned storage.** `tracking.positions` partitioned by day, BRIN on `recorded_at`, B-tree on `(device_id, recorded_at desc)`, a job that creates future partitions and drops ones older than 30 days. Compare with an unpartitioned table at 50M rows.
  ✅ Measurements: insert rate, "last 24 h for one device" query time, table/index size, retention cost (`DROP` partition vs `DELETE`).
- [ ] **L5 · Geofences.** Depot/customer polygons (`geography`); on each batch, detect enter/exit transitions per device (only *changes* emit `GeofenceEntered`/`GeofenceExited` via the outbox). Shipments reacts: arrival at the destination geofence → `AtDestination`.
  ✅ Tests with fixture tracks: a vehicle crossing a fence emits exactly one enter + one exit; jitter at the border doesn't flap (add hysteresis/dwell time).
- [ ] **L6 · Live push with SignalR.** `TrackingHub` with groups per tenant and per shipment; push **coalesced** positions (latest per vehicle per second). A tiny HTML page + the JS client draws positions. Stretch: Valkey backplane (`AddStackExchangeRedis`) in compose + two API instances.
  ✅ Simulator → ingest → hub → browser shows vehicles moving. A load note: messages/s sent vs received after coalescing.

## Break it
1. Make the channel unbounded and run the simulator at 10× rate with a slowed writer. Watch memory grow (`dotnet-counters`).
2. Drop the dedupe and run the simulator with 10% duplicates. Count the inflated distance in a "km driven" query.

## Decide
- **ADR-010** Telemetry ingest & storage: gRPC vs HTTP batch vs MQTT; native partitioning vs TimescaleDB; retention.
- **ADR-011** Real-time transport: SignalR vs SSE vs polling, coalescing policy.

## Quiz → [answers](../answers/M08.md)
1. Why gRPC for device ingest, and what are its drawbacks (browsers, load balancers, debugging)?
2. Client streaming vs unary batch calls: trade-offs for devices on flaky mobile networks?
3. What happens with an unbounded channel when consumers are slower than producers? What are the bounded-channel full modes?
4. Why is one row per ping in a normal, unpartitioned table a problem at 40M rows/day?
5. BRIN vs B-tree for `recorded_at`: why does BRIN work here and fail elsewhere?
6. How do you handle out-of-order and duplicate pings without losing late-but-valid data?
7. *Code reading:* the hub does `await Clients.All.SendAsync("pos", p)` for every ping. Name three problems.
8. Why does geofence detection need hysteresis or a dwell time?
9. When do you need a SignalR backplane, and what does it cost?
10. *Design:* 5,000 vehicles × 1 ping/5 s and 10,000 dashboard viewers. Estimate the write rate, storage per month and push fan-out, and name your two biggest risks.

## Design drill (20 min)
"Design the backend for a live ride-tracking feature (like Uber's map) for 1M concurrent rides." Cover ingest, storage, matching viewers to rides, fan-out and cost.

## Review
M06 Q5 · M03 Q5 · M01 Q3

## Exit check
The ingest benchmark is recorded, the partitioned table compared with numbers, geofence → shipment state tested end to end, the live map works, and ADR-010/011 are written.
