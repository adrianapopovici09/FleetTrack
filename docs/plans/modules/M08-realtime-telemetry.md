# M08 · Real-time telemetry

**Time:** ~17 h · **Prereq:** M07 (and the M01-T4 parser) · **Outcome:** a Tracking module that ingests device telemetry over gRPC at a measured rate, stores it efficiently, detects geofence events and pushes live positions to clients.

**Why it matters:** high-throughput ingestion and real-time push are classic system-design topics, and they're where naive CRUD designs collapse. This module gives you real numbers to quote.

Each task: 📖 **Learn** → 🔨 **Build** → ✅ **Done when**.

---

### T1 · A Postgres image with PostGIS + pgvector
- 📖 **Learn (20 min):** [Dockerfile reference](https://docs.docker.com/reference/dockerfile/) · [postgis/postgis image](https://hub.docker.com/r/postgis/postgis) · [pgvector installation](https://github.com/pgvector/pgvector#installation)
- 🔨 **Build:** `deploy/postgres/Dockerfile` `FROM postgis/postgis:17-3.5` that installs `postgresql-17-pgvector`; switch compose to build it. (pgvector is used in M14; one custom image now saves a migration later.)
- ✅ **Done when:** `CREATE EXTENSION postgis; CREATE EXTENSION vector;` both succeed in a migration.

### T2 · gRPC ingest + device simulator
- 📖 **Learn (60 min):** protobuf contracts, unary vs streaming calls, deadlines, HTTP/2. [gRPC on .NET overview](https://learn.microsoft.com/aspnet/core/grpc/) · [Create a gRPC service](https://learn.microsoft.com/aspnet/core/tutorials/grpc/grpc-start) · [gRPC services: streaming methods](https://learn.microsoft.com/aspnet/core/grpc/services#client-streaming-method) · [Deadlines and cancellation](https://learn.microsoft.com/aspnet/core/grpc/deadlines-cancellation)
- 🔨 **Build:** `telemetry.proto` with client-streaming `Ingest(stream PositionBatch) returns (IngestAck)`, plus a **device simulator** console app: N devices driving along routes, with configurable rate, duplicates and out-of-order sends.
- ✅ **Done when:** the simulator streams 1,000 devices × 1 msg/s and the service acks; an integration test drives it with a gRPC client.

### T3 · Bounded pipeline + batch writer
- 📖 **Learn (45 min):** backpressure with bounded channels. [System.Threading.Channels](https://learn.microsoft.com/dotnet/core/extensions/channels) (read "Bounding strategies") · [An introduction to System.Threading.Channels (Toub)](https://devblogs.microsoft.com/dotnet/an-introduction-to-system-threading-channels/) · [Npgsql binary COPY](https://www.npgsql.org/doc/copy.html) · [INSERT … ON CONFLICT](https://www.postgresql.org/docs/current/sql-insert.html#SQL-ON-CONFLICT)
- 🔨 **Build:** gRPC handler → bounded `Channel<Position>` → a `BackgroundService` writer batching (500 rows or 200 ms) with binary COPY into a staging table, then `INSERT … ON CONFLICT DO NOTHING` (dedupe). Log or expose the channel depth.
- ✅ **Done when:** `docs/perf/M08-ingest.md` records sustained msg/s, p95 ack latency, and what happens when the channel is full (block the stream vs reject: choose and justify).

### T4 · Partitioned time-series storage
- 📖 **Learn (45 min):** [Table partitioning](https://www.postgresql.org/docs/current/ddl-partitioning.html) · [BRIN indexes](https://www.postgresql.org/docs/current/brin-intro.html)
- 🔨 **Build:** `tracking.positions` partitioned by day, BRIN on `recorded_at`, a B-tree on `(device_id, recorded_at desc)`, and a job that creates future partitions and drops those older than 30 days. Compare with an unpartitioned table at 50M rows.
- ✅ **Done when:** you've recorded insert rate, "last 24 h for one device" query time, table/index sizes, and retention cost (`DROP` partition vs `DELETE`).

### T5 · Geofences with PostGIS
- 📖 **Learn (60 min):** [PostGIS workshop: geography](https://postgis.net/workshops/postgis-intro/geography.html) · [PostGIS workshop: spatial indexing](https://postgis.net/workshops/postgis-intro/indexing.html) · [Npgsql spatial mapping (NetTopologySuite)](https://www.npgsql.org/efcore/mapping/nts.html)
- 🔨 **Build:** depot/customer polygons (`geography`); per batch, detect enter/exit transitions per device, emitting only *changes* as `GeofenceEntered`/`GeofenceExited` via the outbox. Shipments reacts: arrival at the destination fence → `AtDestination`.
- ✅ **Done when:** with fixture tracks, crossing a fence emits exactly one enter + one exit, and jitter at the border doesn't flap (hysteresis or dwell time).

### T6 · Live push with SignalR
- 📖 **Learn (45 min):** [SignalR introduction](https://learn.microsoft.com/aspnet/core/signalr/introduction) · [Hubs & groups](https://learn.microsoft.com/aspnet/core/signalr/groups) · [JavaScript client](https://learn.microsoft.com/aspnet/core/signalr/javascript-client) · [Scale-out with a Redis-protocol backplane](https://learn.microsoft.com/aspnet/core/signalr/redis-backplane)
- 🔨 **Build:** `TrackingHub` with groups per tenant and per shipment; push **coalesced** positions (latest per vehicle per second); a tiny HTML page using the JS client draws the vehicles. Stretch: a Valkey backplane (`AddStackExchangeRedis`) + two API instances.
- ✅ **Done when:** simulator → ingest → hub → browser shows vehicles moving, with a note on messages/s before and after coalescing.

### T7 · Break it
- 🔨 **Build:** (1) make the channel unbounded and run the simulator at 10× with a slowed writer; watch memory in `dotnet-counters`. (2) Drop the dedupe and run with 10% duplicates; measure the inflated "km driven".
- ✅ **Done when:** both failures are recorded with numbers.

### T8 · Decide: ADR-010 & ADR-011
- 📖 **Learn (20 min):** [Choosing gRPC vs HTTP APIs](https://learn.microsoft.com/aspnet/core/grpc/comparison) · [Real-time options: SignalR transports](https://learn.microsoft.com/aspnet/core/signalr/introduction#transports)
- 🔨 **Build:** **ADR-010** ingest & storage (gRPC vs HTTP batch vs MQTT; native partitioning vs TimescaleDB; retention). **ADR-011** real-time transport (SignalR vs SSE vs polling; coalescing policy).
- ✅ **Done when:** both ADRs cite your measurements.

---

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
