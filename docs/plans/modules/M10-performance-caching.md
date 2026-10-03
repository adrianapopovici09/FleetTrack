# M10 · Performance & caching

**Time:** ~12 h · **Prereq:** M09 · **Outcome:** a repeatable method (measure → hypothesise → change → re-measure), correct caching, load-test results and SLOs you can defend.

## Why it matters
"The system is slow, what do you do?" is asked in nearly every senior interview, and at work the answer has to come with evidence. Caching is the most common fix and the most common source of subtle bugs.

## Concepts
- **Method:** define the target (SLO) → reproduce under load → profile → fix one thing → re-measure → record.
- **Tools:** BenchmarkDotNet (micro), `dotnet-counters` / `dotnet-trace` / `dotnet-gcdump` (runtime), `EXPLAIN ANALYZE` (SQL), **k6** (system load).
- **Latency:** p50/p95/p99, tail latency, coordinated omission, Little's law, connection-pool and thread-pool limits.
- **Caching layers:** HTTP (`Cache-Control`, ETag, output cache) → in-process (L1) → distributed (Valkey/Redis, L2). **HybridCache** combines L1 + L2 with stampede protection and tag-based invalidation.
- **Cache correctness:** keys include tenant + version; TTL + jitter; invalidation by tag/version vs delete; stampede (thundering herd); never cache per-user data in shared caches without the user in the key.
- **Capacity maths:** requests/s × cost per request → instances, DB connections, cost.

Read: [Performance best practices (ASP.NET Core)](https://learn.microsoft.com/aspnet/core/fundamentals/best-practices) · [HybridCache](https://learn.microsoft.com/aspnet/core/performance/caching/hybrid) · [Output caching](https://learn.microsoft.com/aspnet/core/performance/caching/output) · [dotnet-trace](https://learn.microsoft.com/dotnet/core/diagnostics/dotnet-trace) · [k6 docs](https://grafana.com/docs/k6/latest/)

## Labs

- [ ] **L1 · Baseline load test.** k6 scenarios through the gateway: shipment list (70%), shipment details with last position (20%), booking (10%), ramping to the failure point.
  ✅ `docs/perf/M10-baseline.md`: RPS at the knee, p95/p99, error rate, and the first resource that saturated (CPU, DB connections, thread pool…).
- [ ] **L2 · Profile and fix the top hotspot.** Use `dotnet-trace` + flame graph (PerfView or speedscope) and `dotnet-counters` during the load; find the top 2 issues (allocations, sync I/O, chatty SQL, serialisation) and fix them one at a time.
  ✅ A before/after table for each fix; a rejected hypothesis is written down too.
- [ ] **L3 · Manual cache-aside first.** Cache the shipment details with `IDistributedCache` + Valkey (add `valkey/valkey` to compose; it speaks the Redis protocol, so you use the normal StackExchange.Redis-based packages) by hand: key design (`tenant:{t}:shipment:{id}:v{n}`), TTL with jitter, invalidation on update via the domain event.
  ✅ A test proves updates are visible immediately after the write (no stale read) and another tenant can never get the cached entry.
- [ ] **L4 · HybridCache.** Replace L3 with `HybridCache` (L1 + Valkey L2), tag-based invalidation (`shipment:{id}`, `tenant:{t}`). Simulate a stampede (500 concurrent misses on one key) with and without it.
  ✅ Measured DB hits during the stampede: hand-rolled vs HybridCache.
- [ ] **L5 · HTTP-level caching.** Output caching for the public tracking page (vary by tracking number; evict by tag on status change); `ETag`/304 for shipment GETs (reuse M02).
  ✅ k6 re-run with numbers vs the baseline.
- [ ] **L6 · SLOs & capacity.** Define SLOs (e.g. shipment list p95 < 200 ms, 99.9% success); compute the capacity for 10× today's tenants (instances, DB connections, Valkey memory).
  ✅ `docs/perf/slo.md` with the SLOs, capacity maths and the next bottleneck you predict.

## Break it
1. Forget the tenant in the cache key and show a cross-tenant leak in a test.
2. Cache with a fixed TTL and no jitter for 10,000 keys populated at once, then watch them expire together (synchronized misses).
3. Run k6 with a fixed request rate vs fixed virtual users (VUs) while the server slows down, and see coordinated omission hide the real latency.

## Decide
**ADR-013** Caching strategy: what's cached where, key rules, invalidation, what's never cached.

## Quiz → [answers](../answers/M10.md)
1. Why are p99 and p95 more important than average latency? What's coordinated omission?
2. Walk through your method when someone reports "the API is slow".
3. Cache-aside vs read-through vs write-through: trade-offs?
4. What's a cache stampede and how does HybridCache prevent it?
5. Why include a version or tag in cache keys, instead of deleting keys on update?
6. *Code reading:* `cache.Set($"shipment:{id}", dto, TimeSpan.FromHours(1));` in a multi-tenant system with user-specific fields. Problems?
7. When is caching the wrong fix?
8. Little's law: 400 RPS at 250 ms average latency means how many concurrent requests? What does that imply for a DB pool of 100?
9. What does output caching do differently from response caching (`Cache-Control`)?
10. *Design:* the product page of a flash sale gets 50k RPS for 10 minutes. Design the read path.

## Design drill (20 min)
"Design a URL shortener / product catalogue read path handling 100k RPS with 99.99% availability." Focus on caching tiers, invalidation and hot keys.

## Review
M08 Q4 · M03 Q10 · M01 Q10

## Exit check
Baseline + 2 profiled fixes with numbers, caching tests (staleness, tenant isolation), stampede comparison, and SLO + capacity doc. ADR-013 written.
