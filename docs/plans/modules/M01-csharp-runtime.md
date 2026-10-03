# M01 · Modern C# & runtime essentials

**Time:** ~8 h · **Prereq:** M00 · **Outcome:** you can explain what the runtime does under your code, and diagnose the async, memory and DI bugs that senior interviews (and production incidents) are made of.

## Why it matters
Senior .NET interviews almost always probe async/await, the GC, DI lifetimes and performance basics. In real work these are the root causes of "the API hangs under load" and "memory keeps growing."

## Concepts
- **async/await:** the compiler-generated state machine, what `await` actually does, `SynchronizationContext` (and why ASP.NET Core has none), `ConfigureAwait(false)` in libraries, `Task` vs `ValueTask`, `IAsyncEnumerable<T>`.
- **Thread pool:** sync-over-async (`.Result`, `.Wait()`) causes thread-pool starvation; the thread pool's injection rate makes it slow to recover.
- **Cancellation:** `CancellationToken` flows from `HttpContext.RequestAborted` down to the DB call; linked tokens and timeouts.
- **Memory:** stack vs heap, Gen0/1/2 + LOH, allocations as the main perf lever, `Span<T>`/`Memory<T>`, `ArrayPool<T>`, `stackalloc`.
- **Modern C#:** records, `required`/`init`, primary constructors, pattern matching, collection expressions, file-scoped types, C# 14 `field` keyword and extension members.
- **DI lifetimes:** singleton/scoped/transient, captive dependencies, `ValidateScopes`/`ValidateOnBuild`, keyed services, `IServiceScopeFactory` in background services.

Read: [Async in depth](https://learn.microsoft.com/dotnet/standard/async-in-depth) · [ConfigureAwait FAQ](https://devblogs.microsoft.com/dotnet/configureawait-faq/) · [GC fundamentals](https://learn.microsoft.com/dotnet/standard/garbage-collection/fundamentals) · [Memory & spans](https://learn.microsoft.com/dotnet/standard/memory-and-spans/) · [DI guidelines](https://learn.microsoft.com/dotnet/core/extensions/dependency-injection-guidelines)

## Labs
These go in `labs/` (a throwaway console + benchmark solution, not part of FleetTrack's architecture).

- [ ] **L1 · Thread-pool starvation.** Write a minimal API endpoint that calls `Task.Delay(500).Result`. Hammer it with `k6` or `bombardier` (100 concurrent users) while watching `dotnet-counters monitor -n <proc> System.Runtime` (ThreadPool Thread Count, Queue Length). Then make it properly async and repeat.
  ✅ `docs/perf/M01-threadpool.md` with both runs: RPS, p95, thread count and queue length.
- [ ] **L2 · Cancellation end to end.** Endpoint → service → `Task.Delay` / `NpgsqlCommand` with the request's `CancellationToken`. Cancel the request (close curl) and log where the cancellation surfaces. Add a linked token with a 2 s timeout.
  ✅ A test proves that cancelling the request stops the DB query (check `pg_stat_activity` or the log).
- [ ] **L3 · Allocations with BenchmarkDotNet.** Parse a telemetry line `"DEV-42;2026-10-03T10:00:00Z;45.75;21.22;63.5"` three ways: `string.Split` + `double.Parse`, `Span<char>` slicing + `double.Parse(ReadOnlySpan<char>)`, and `Utf8Parser` over bytes. Use `[MemoryDiagnoser]`.
  ✅ A results table in `docs/perf/M01-parsing.md` and a one-paragraph explanation of *why* each version allocates what it does. (This parser is reused in M08.)
- [ ] **L4 · Captive dependency.** Register a scoped `ShipmentContext` and inject it into a singleton. Run with `ValidateScopes = false`, then `true`. Then fix it with `IServiceScopeFactory`.
  ✅ A test shows the startup failure with validation on; the fixed version passes.
- [ ] **L5 · Modern C# refactor.** Take 3 classes from FleetTrack (or a previous job's style) and rewrite them with records, primary constructors, `required`, pattern matching and collection expressions. Write down one feature you'd *avoid* in a team codebase and why.
  ✅ Before/after in your journal.

## Break it
In L1, add `ThreadPool.SetMinThreads(200, 200)` and rerun the sync-over-async version. It "fixes" the symptom. Write down why this is a band-aid and when (if ever) it's legitimate.

## Decide
No ADR. Write `docs/journal/M01.md` and add a *team guideline* section (5 bullet rules on async, cancellation and DI you'd put in a team wiki).

## Quiz → [answers](../answers/M01.md)
1. What does the compiler turn an `async` method into, and what happens at an `await` on an incomplete task?
2. Why is `.Result` dangerous in ASP.NET Core even without a `SynchronizationContext`?
3. When should you return `ValueTask` instead of `Task`? Name one rule you must follow when consuming a `ValueTask`.
4. What's the difference between Gen0, Gen2 and the LOH? Why can a 90 KB byte array be worse than many 1 KB arrays?
5. Why can't `Span<T>` be a field of a class or be used across an `await`?
6. *Code reading:* `services.AddSingleton<ReportService>(); services.AddDbContext<AppDb>();` and `ReportService` takes `AppDb` in its constructor. What's wrong, and what symptoms appear in production?
7. *Code reading:* `public async void OnMessage(Message m) { await Handle(m); }` What are the two problems?
8. `Task.WhenAll` over 10,000 HTTP calls: what goes wrong and how do you bound it?
9. What does `IAsyncEnumerable<T>` give you over returning `Task<List<T>>` from an API endpoint?
10. *Scenario:* an API's p99 latency spikes every few minutes, while CPU stays low. Name three runtime-level causes you'd check and the tool for each.

## Design drill (20 min)
"Our .NET API handles 200 RPS fine but falls over at 400 RPS with low CPU. Walk me through your investigation." (Think thread pool, connection pools, sync-over-async, GC, downstream timeouts.)

## Review (spaced repetition)
M00 Q3 · M00 Q5 · M00 Q10

## Exit check
L1 and L3 measurements are committed with explanations, and you can answer Q1, Q2 and Q6 out loud without notes.
