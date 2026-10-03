# Phase 6 — Learn & Build Tasks (W17: caching & performance engineering)

> **Read** → **Build** → **Done when**. Questions: `agent.md` §7. Checkbox: [`../plan.md`](../plan.md). Extra reading: [Caching guidance](https://learn.microsoft.com/azure/architecture/best-practices/caching) · [BenchmarkDotNet](https://benchmarkdotnet.org/articles/overview.html) · [k6 docs](https://grafana.com/docs/k6/latest/) · [`dotnet-counters`](https://learn.microsoft.com/dotnet/core/diagnostics/dotnet-counters)

## Week 17 — Caching and measured performance

### W17D1 · Cache topology & key design
**Read:** [Caching guidance](https://learn.microsoft.com/azure/architecture/best-practices/caching) · [Distributed caching in ASP.NET Core](https://learn.microsoft.com/aspnet/core/performance/caching/distributed) · [Cache-aside pattern](https://learn.microsoft.com/azure/architecture/patterns/cache-aside)
**Build:** `ICacheService` abstraction in Application with a Redis implementation; keys carrying tenant + version + filters; cache-aside for the shipment list with TTL + jitter and single-flight (stampede protection).
**Done when:** a wrong-key test (different tenant/filters) can never return another's data, and a cold cache under 200 concurrent requests triggers one DB query.

### W17D2 · Invalidation & cross-instance consistency
**Read:** [Cache invalidation strategies](https://learn.microsoft.com/azure/architecture/best-practices/caching) · [Redis pub/sub](https://redis.io/docs/latest/develop/interact/pubsub/)
**Build:** version-bump invalidation (`shipments:ver:{tenant}` incremented on writes) plus pub/sub eviction for read models; document staleness bounds.
**Done when:** after a write, no instance serves stale shipment data beyond the documented staleness window (test with two instances).

### W17D3 · HTTP-level caching
**Read:** [Response caching](https://learn.microsoft.com/aspnet/core/performance/caching/response) · [Output caching](https://learn.microsoft.com/aspnet/core/performance/caching/output) · [HTTP caching (MDN)](https://developer.mozilla.org/docs/Web/HTTP/Caching)
**Build:** output caching for reference data + per-tenant read-only lists; correct `Cache-Control`/`Vary`; `ETag` + 304 reuse for everything else (`If-None-Match`).
**Done when:** a repeat GET returns 304 or a cached response without hitting the database (prove via logs/metrics).

### W17D4 · Measure before optimising
**Read:** [BenchmarkDotNet](https://benchmarkdotnet.org/articles/overview.html) · [`dotnet-trace`](https://learn.microsoft.com/dotnet/core/diagnostics/dotnet-trace) · [.NET diagnostics tools](https://learn.microsoft.com/dotnet/core/diagnostics/)
**Build:** `perf/FleetTrack.Benchmarks` with baselines for the three hottest paths (list projection, position upsert, serialisation); capture allocation + CPU; one documented finding per area in `docs/perf/`.
**Done when:** three baselines exist with spread reported and at least one optimisation is proposed (not yet applied).

### W17D5 · Load testing, SLOs & capacity math
**Read:** [k6 getting started](https://grafana.com/docs/k6/latest/get-started/) · [SLOs (Google SRE)](https://sre.google/sre-book/service-level-objectives/) · [Performance testing guidance](https://learn.microsoft.com/azure/architecture/antipatterns/)
**Build:** k6 scenarios (list, detail, telemetry ingest, hub connect) with p95/p99 targets; write `docs/ops/slo.md` (SLIs, SLOs, error budget) and a per-tenant cost estimate; capacity arithmetic from the 500 msg/s model.
**Done when:** SLOs are written, a k6 run reports against them, and the capacity estimate names instances/pool sizes.

### D6 · Integrate
Apply the top three findings (indexes, projections, caching), re-run benchmarks and record the deltas in `docs/perf/`.

### D7 · Review
Latency budgets, tail latency, Amdahl, connection/thread-pool limits, cache cost; week 17 quiz.