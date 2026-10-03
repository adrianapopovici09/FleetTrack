# M01 · Modern C# & runtime essentials

**Time:** ~9 h · **Prereq:** M00 · **Outcome:** you can explain what the runtime does under your code, and diagnose the async, memory and DI bugs that senior interviews (and production incidents) are made of.

**Why it matters:** senior .NET interviews almost always probe async/await, the GC, DI lifetimes and performance basics. In real work these are the root causes of "the API hangs under load" and "memory keeps growing".

These tasks go in `labs/` (a throwaway console + benchmark solution, outside FleetTrack's architecture). Each task: 📖 **Learn** → 🔨 **Build** → ✅ **Done when**.

---

### T1 · How async/await really works
- 📖 **Learn (60 min):**
  - The compiler-generated state machine, and what `await` does on an incomplete task. [How async/await really works in C# (Toub)](https://devblogs.microsoft.com/dotnet/how-async-await-really-works/) (long; read up to "SynchronizationContext and ConfigureAwait")
  - `SynchronizationContext`, and why ASP.NET Core has none. [ConfigureAwait FAQ](https://devblogs.microsoft.com/dotnet/configureawait-faq/)
  - `Task` vs `ValueTask`. [Understanding the whys, whats and whens of ValueTask](https://devblogs.microsoft.com/dotnet/understanding-the-whys-whats-and-whens-of-valuetask/)
- 🔨 **Build:** a console app that logs `Environment.CurrentManagedThreadId` before and after awaits (completed vs incomplete tasks, `Task.Yield`, `ConfigureAwait(false)`). Decompile one async method with [SharpLab](https://sharplab.io) and annotate the state machine.
- ✅ **Done when:** a journal note explains, in your own words, when the code after `await` runs on a different thread, and why.

### T2 · Thread-pool starvation (sync-over-async)
- 📖 **Learn (30 min):** [Debug thread-pool starvation (tutorial)](https://learn.microsoft.com/dotnet/core/diagnostics/debug-threadpool-starvation) · [dotnet-counters](https://learn.microsoft.com/dotnet/core/diagnostics/dotnet-counters) · [k6 getting started](https://grafana.com/docs/k6/latest/get-started/running-k6/)
- 🔨 **Build:** a minimal API endpoint calling `Task.Delay(500).Result`. Load it with k6 (100 VUs) while running `dotnet-counters monitor -n <proc> System.Runtime` (thread-pool thread count, queue length). Then make it properly async and repeat.
- ✅ **Done when:** `docs/perf/M01-threadpool.md` has both runs: RPS, p95, thread count and queue length, with an explanation.

### T3 · Cancellation end to end
- 📖 **Learn (30 min):** [Cancellation in managed threads](https://learn.microsoft.com/dotnet/standard/threading/cancellation-in-managed-threads) · [Recommended patterns for CancellationToken (Cleary)](https://devblogs.microsoft.com/premier-developer/recommended-patterns-for-cancellationtoken/)
- 🔨 **Build:** endpoint → service → Npgsql query (`SELECT pg_sleep(10)`) passing the request's `CancellationToken`. Abort the request and log where cancellation surfaces. Add a linked token with a 2 s timeout.
- ✅ **Done when:** a test (or a documented manual check via `pg_stat_activity`) proves that aborting the request cancels the DB query.

### T4 · Memory, the GC and allocation-free code
- 📖 **Learn (60 min):**
  - Generations, the LOH, and what triggers a GC. [Fundamentals of garbage collection](https://learn.microsoft.com/dotnet/standard/garbage-collection/fundamentals)
  - `Span<T>`, `Memory<T>`, and why spans can't live on the heap. [Memory- and span-related types](https://learn.microsoft.com/dotnet/standard/memory-and-spans/) · [Memory<T> and Span<T> usage guidelines](https://learn.microsoft.com/dotnet/standard/memory-and-spans/memory-t-usage-guidelines)
  - Benchmarking correctly. [BenchmarkDotNet: getting started](https://benchmarkdotnet.org/articles/guides/getting-started.html)
- 🔨 **Build:** parse a telemetry line `"DEV-42;2026-10-03T10:00:00Z;45.75;21.22;63.5"` three ways: `string.Split` + `double.Parse`; `ReadOnlySpan<char>` slicing; `Utf8Parser` over bytes. Benchmark them with `[MemoryDiagnoser]`. (This parser is reused in M08.)
- ✅ **Done when:** a results table in `docs/perf/M01-parsing.md` + one paragraph explaining *why* each version allocates what it does.

### T5 · DI lifetimes & captive dependencies
- 📖 **Learn (30 min):** [Service lifetimes](https://learn.microsoft.com/dotnet/core/extensions/dependency-injection#service-lifetimes) · [DI guidelines: captive dependencies and scope validation](https://learn.microsoft.com/dotnet/core/extensions/dependency-injection-guidelines) · [Keyed services](https://learn.microsoft.com/dotnet/core/extensions/dependency-injection#keyed-services)
- 🔨 **Build:** inject a scoped service into a singleton. Run with `ValidateScopes = false`, then `true`. Fix it with `IServiceScopeFactory`.
- ✅ **Done when:** a test shows the startup failure with validation on, and the fixed version passes.

### T6 · Modern C# for team codebases
- 📖 **Learn (40 min):** [What's new in C# 12](https://learn.microsoft.com/dotnet/csharp/whats-new/csharp-12) (primary constructors, collection expressions) · [What's new in C# 13](https://learn.microsoft.com/dotnet/csharp/whats-new/csharp-13) · [What's new in C# 14](https://learn.microsoft.com/dotnet/csharp/whats-new/csharp-14) (`field` keyword, extension members) · [Records](https://learn.microsoft.com/dotnet/csharp/fundamentals/types/records) · [Pattern matching](https://learn.microsoft.com/dotnet/csharp/fundamentals/functional/pattern-matching)
- 🔨 **Build:** rewrite 3 classes (from FleetTrack or past work) using records, primary constructors, `required`, pattern matching and collection expressions.
- ✅ **Done when:** before/after in your journal, plus one feature you'd *avoid* in a team codebase and why.

### T7 · Break it & write team guidelines
- 📖 **Learn (10 min):** [ThreadPool.SetMinThreads](https://learn.microsoft.com/dotnet/api/system.threading.threadpool.setminthreads) (read the remarks).
- 🔨 **Build:** in T2, add `ThreadPool.SetMinThreads(200, 200)` and rerun the sync-over-async version. Then write `docs/journal/M01.md` with a **team guideline** section: 5 rules on async, cancellation and DI you'd put in a team wiki.
- ✅ **Done when:** the note explains why `SetMinThreads` is a band-aid (and when it's legitimate), and the 5 rules are written.

---

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
"Our .NET API handles 200 RPS fine but falls over at 400 RPS with low CPU. Walk me through your investigation."

## Review
M00 Q3 · M00 Q5 · M00 Q10

## Exit check
T2 and T4 measurements committed with explanations, and you can answer Q1, Q2 and Q6 out loud without notes.
