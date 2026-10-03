# M10 · Performance & caching

**Time:** ~13 h · **Prereq:** M09 · **Outcome:** a repeatable method (measure → hypothesise → change → re-measure), correct caching, load-test results and SLOs you can defend.

**Why it matters:** "the system is slow, what do you do?" is asked in nearly every senior interview, and at work the answer has to come with evidence. Caching is the most common fix and the most common source of subtle bugs.

Each task: 📖 **Learn** → 🔨 **Build** → ✅ **Done when**.

---

### T1 · Baseline load test
- 📖 **Learn (45 min):** percentiles, coordinated omission, open vs closed load models. [k6: scenarios & executors](https://grafana.com/docs/k6/latest/using-k6/scenarios/) · [k6: open and closed models](https://grafana.com/docs/k6/latest/using-k6/scenarios/concepts/open-vs-closed/) · [Little's law (explained)](https://en.wikipedia.org/wiki/Little%27s_law)
- 🔨 **Build:** k6 scenarios through the gateway: shipment list (70%), details with last position (20%), booking (10%), ramping to the failure point with an arrival-rate executor.
- ✅ **Done when:** `docs/perf/M10-baseline.md` records RPS at the knee, p95/p99, error rate, and the first resource that saturated.

### T2 · Profile and fix the top hotspots
- 📖 **Learn (60 min):** [ASP.NET Core performance best practices](https://learn.microsoft.com/aspnet/core/fundamentals/best-practices) · [dotnet-trace](https://learn.microsoft.com/dotnet/core/diagnostics/dotnet-trace) · [dotnet-counters](https://learn.microsoft.com/dotnet/core/diagnostics/dotnet-counters) · [Viewing traces in speedscope / PerfView](https://learn.microsoft.com/dotnet/core/diagnostics/dotnet-trace#view-the-trace-captured-from-dotnet-trace)
- 🔨 **Build:** profile under load with `dotnet-trace` (flame graph) + `dotnet-counters`; find the top 2 issues (allocations, sync I/O, chatty SQL, serialisation) and fix them one at a time.
- ✅ **Done when:** each fix has a before/after table, and rejected hypotheses are written down too.

### T3 · Manual cache-aside first
- 📖 **Learn (40 min):** [Cache-aside pattern](https://learn.microsoft.com/azure/architecture/patterns/cache-aside) · [Distributed caching in ASP.NET Core](https://learn.microsoft.com/aspnet/core/performance/caching/distributed) · [Valkey](https://valkey.io/) (Redis-protocol compatible, BSD licence)
- 🔨 **Build:** add `valkey/valkey` to compose. Cache shipment details with `IDistributedCache` (the StackExchange.Redis provider works with Valkey) by hand: key design (`tenant:{t}:shipment:{id}:v{n}`), TTL with jitter, invalidation on update via the domain event.
- ✅ **Done when:** tests prove updates are visible immediately after a write and another tenant can never read the cached entry.

### T4 · HybridCache & stampede protection
- 📖 **Learn (30 min):** [HybridCache library](https://learn.microsoft.com/aspnet/core/performance/caching/hybrid) (read "Stampede protection" and "Tags")
- 🔨 **Build:** replace T3 with `HybridCache` (L1 + Valkey L2) and tag-based invalidation (`shipment:{id}`, `tenant:{t}`); simulate 500 concurrent misses on one key with and without it.
- ✅ **Done when:** DB hits during the stampede are measured: hand-rolled vs HybridCache.

### T5 · HTTP-level caching
- 📖 **Learn (30 min):** [Output caching middleware](https://learn.microsoft.com/aspnet/core/performance/caching/output) · [MDN: HTTP caching](https://developer.mozilla.org/docs/Web/HTTP/Guides/Caching)
- 🔨 **Build:** output caching for the public tracking page (vary by tracking number, evict by tag on status change); `ETag`/304 for shipment GETs (reusing M02).
- ✅ **Done when:** a k6 rerun is compared with the baseline.

### T6 · SLOs & capacity maths
- 📖 **Learn (40 min):** [Google SRE: Service Level Objectives](https://sre.google/sre-book/service-level-objectives/) · [Azure: performance testing & capacity planning](https://learn.microsoft.com/azure/well-architected/performance-efficiency/capacity-planning)
- 🔨 **Build:** define SLOs (e.g. shipment list p95 < 200 ms, 99.9% success) and compute capacity for 10× tenants (instances, DB connections, Valkey memory).
- ✅ **Done when:** `docs/perf/slo.md` has the SLOs, the capacity maths and the next bottleneck you predict.

### T7 · Break it & decide
- 🔨 **Build:** (1) forget the tenant in the cache key and show the leak in a test. (2) Populate 10,000 keys with a fixed TTL and no jitter, and watch them expire together. (3) Compare k6 fixed-VU vs arrival-rate results while the server slows (coordinated omission). Then write **ADR-013**: caching strategy (what's cached where, key rules, invalidation, what's never cached).
- ✅ **Done when:** the three failures are recorded and ADR-013 is written.

---

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
Baseline + 2 profiled fixes with numbers, caching tests (staleness, tenant isolation), the stampede comparison, the SLO + capacity doc, and ADR-013.
