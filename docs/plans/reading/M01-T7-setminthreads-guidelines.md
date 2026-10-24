# M01-T7 · Break it: `SetMinThreads`, and writing team guidelines

Task: [M01-T7](../modules/M01-csharp-runtime.md)

This file is the complete lesson for the task; you don't need to read anything else. Original source, if you ever want to go deeper: [ThreadPool.SetMinThreads](https://learn.microsoft.com/dotnet/api/system.threading.threadpool.setminthreads) (the Remarks section).

**Reading time:** about 30 minutes. It builds directly on M01-T2 ([lesson](M01-T2-threadpool-starvation.md), [your write-up](../../journal/M01-T2-threadpool-starvation.md)), so have your T2 numbers at hand.

## Contents

1. [Where we are: the T2 story in five lines](#1-where-we-are-the-t2-story-in-five-lines)
2. [What the thread pool's "minimum" really means](#2-what-the-thread-pools-minimum-really-means)
3. [`SetMinThreads`: the API and the configuration switch](#3-setminthreads-the-api-and-the-configuration-switch)
4. [What a thread costs](#4-what-a-thread-costs)
5. [Why it's a band-aid: Little's law again](#5-why-its-a-band-aid-littles-law-again)
6. [When it *is* legitimate](#6-when-it-is-legitimate)
7. [The real fixes, and what to do while you can't apply them](#7-the-real-fixes-and-what-to-do-while-you-cant-apply-them)
8. [Writing team guidelines that people follow](#8-writing-team-guidelines-that-people-follow)
9. [Glossary](#9-glossary)
10. [Summary, check yourself, interview questions, for FleetTrack](#10-summary)

---

## 1. Where we are: the T2 story in five lines

1. `/slow-sync` blocks a thread-pool thread for 500 ms (`Task.Delay(500).Wait()`); `/slow-async` gives the thread back while it waits.
2. With 100 virtual users, the sync endpoint needs about **one thread per in-flight request**, ~100 threads. The pool starts with about one per logical processor (8 on your machine).
3. Above that minimum, the pool adds threads **gradually**. Until it caught up, requests queued, and you saw the 13-second freeze with the CPU almost idle.
4. Once the pool had ~100 threads, the sync endpoint ran almost as well as the async one.
5. The async endpoint served the same load with ~9 threads from the first second.

Point 4 invites an obvious question: if ~100 threads fix it, why not have them **from the start**? That's what `SetMinThreads` does, and this task is about why it's tempting, why it's usually wrong, and when it's right.

---

## 2. What the thread pool's "minimum" really means

The name suggests the pool *keeps* that many threads alive at all times. It doesn't. The **minimum** is the number of threads the pool will create **on demand, immediately, without throttling**:

```
   threads in use
        │
   max  ┤- - - - - - - - - - - - - - - - - - - - - (a hard ceiling, normally very high: 32,767 on 64-bit)
        │
        │            above the minimum: the pool adds threads SLOWLY
        │            (hill-climbing + starvation detection: roughly one
        │             or a few per second, with bursts when it sees
        │             threads blocked on tasks, as in your T2 run 1)
        │
   min  ┤━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        │            up to the minimum: a new thread is created the moment
        │            work is queued and no thread is free (no delay)
        0
```

- **Threads aren't pre-created.** Right after startup, the pool may have just a few threads even with a minimum of 200. It creates them as work arrives.
- **Below the minimum there's no injection delay.** That's the whole effect of raising it: the slow "add a thread, wait, measure" phase starts later.
- **Idle threads above the minimum retire** after about 20 seconds without work (you saw 103 → 0 at the end of T2 run 2). Raising the minimum also means those threads are kept around instead of retiring.

Why the pool is cautious above the minimum: more threads don't make CPU-bound work faster. On 8 logical processors, 8 busy threads already use all the CPU, and 200 just take turns. The pool can't tell whether a waiting work item needs **more threads** (because existing ones are blocked) or **more CPU** (because they're all busy), so it adds threads slowly and measures whether throughput improves. Blocking code defeats that design: threads are "busy" doing nothing.

---

## 3. `SetMinThreads`: the API and the configuration switch

```csharp
// At the very start of Program.cs, before the app starts handling requests
bool ok = ThreadPool.SetMinThreads(workerThreads: 200, completionPortThreads: 200);
ThreadPool.GetMinThreads(out int worker, out int io);   // check what you actually got
```

- **`workerThreads`** is the number that matters: these threads run your request handlers, continuations and `Task.Run` work.
- **`completionPortThreads`** were dedicated to I/O completions on Windows in older .NET. Since .NET 7, the managed thread pool handles I/O completions on worker threads by default, so this value matters little on current .NET. By convention it's set to the same value.
- **It returns `false`** and changes nothing if a value is negative or above the maximum. Code that ignores the return value can believe it set a minimum that never took effect. Log the result.
- **It's process-wide.** One library calling it changes the behaviour of the whole app.

The same setting can come from configuration instead of code, which is better because operations can change it without a rebuild:

```xml
<!-- In the .csproj: written into the app's runtimeconfig.json at build time -->
<PropertyGroup>
  <ThreadPoolMinThreads>200</ThreadPoolMinThreads>
</PropertyGroup>
```

…or directly in `runtimeconfig.json` as `"System.Threading.ThreadPool.MinThreads": 200`.

---

## 4. What a thread costs

"Just add threads" sounds free. It isn't:

| Cost | What happens |
| --- | --- |
| **Memory** | Each thread reserves address space for its stack (about 1–1.5 MB by default, committed only as it's used) plus kernel and runtime structures. 200 blocked threads mostly hold memory that does nothing. |
| **Context switches** | The OS scheduler shares the logical processors between all runnable threads. When many wake at once (a batch of 500 ms delays finishing together), they compete for 8 processors, and the switching itself costs CPU and cache. |
| **Longer GC pauses** | To collect, the GC suspends **every** managed thread and scans each one's stack for references. More threads means more stacks to scan and more threads to stop and restart. |
| **Contention** | More threads hitting the same lock, connection pool or downstream service at the same time means more waiting there instead of in the thread-pool queue. The bottleneck moves, it doesn't go away. |

None of these matter at 200 threads on a big server. They're the reason "set it to 5,000" is a bad idea.

---

## 5. Why it's a band-aid: Little's law again

From T2: **concurrency = throughput × latency**. For blocking code, every in-flight request holds a thread, so:

```
threads needed = requests per second × seconds each request blocks a thread
```

`SetMinThreads` picks a **fixed** number of threads. The number you *need* depends on two things that change all the time:

1. **Load.** Twice the traffic, twice the threads. A minimum sized for today's peak starves at next month's peak, or during a marketing campaign.
2. **How long each call blocks.** This one is the dangerous part. Suppose the blocking call goes to a database or another service, and that dependency slows down from 100 ms to 1 s. Each request now holds its thread **10 times longer**, so you need 10 times the threads at the *same* traffic. The minimum stops covering you exactly when a dependency is struggling. And at that moment, the extra threads you *do* get pile more concurrent requests onto the struggling dependency.

That's the definition of a band-aid: it removes the **symptom** (the slow ramp-up and the freeze) at one particular load and latency, but keeps the **cause** (one thread wasted per waiting request). It also **hides the cause**: the thread-count graph looks flat and healthy, so nobody fixes the blocking code until the day the numbers move past the setting.

Compare with async: the number of threads stays near the processor count *whatever* the latency, because a waiting request holds no thread at all.

---

## 6. When it *is* legitimate

"Band-aid" doesn't mean "never". Senior answers name the legitimate cases:

1. **Blocking code you can't change yet.** A vendor SDK (software development kit) with only synchronous methods, a legacy library, a sync-only database driver. Until it's replaced, the blocking is real, and a correctly sized minimum stops the freeze. Make it **temporary on purpose**: a ticket to remove it, a comment explaining why, and a review date.
2. **Known, bounded bursts.** An app that receives a predictable burst at startup or on a schedule (a batch job firing 150 parallel calls at 02:00) can set the minimum to cover that burst, so it doesn't pay the ramp-up every time.
3. **During a migration.** While a large codebase is moving from sync to async, some sync-over-async paths remain. A minimum keeps production stable while the work is in progress.
4. **As an emergency mitigation.** Production is freezing now, the fix needs a release, and changing configuration (§3) is faster. Do it, then fix the code.

In all four cases, do it **deliberately**:
- **Size it** with Little's law plus headroom (expected peak RPS × worst realistic blocking time × 1.5, for example), not with a round number off the internet.
- **Watch it:** thread count, queue length and the dependency's latency. If the thread count reaches the minimum, you're about to starve again.
- **Prefer the configuration switch** to code, so it can be tuned and removed without a release.

---

## 7. The real fixes, and what to do while you can't apply them

**The fix is async all the way down** (M01-T1, T2): the waiting request holds no thread, so the thread pool never needs to grow.

When some sync code must stay for a while, the senior moves are about **containing** it instead of feeding it threads:

- **Bound the concurrency of the blocking part.** Put a `SemaphoreSlim` (with `WaitAsync`) in front of the sync call, sized for what the dependency can take. Requests beyond the limit wait without holding a thread, or fail fast. This is a **bulkhead**: a slow dependency can use at most N threads, not the whole pool. (Resilience libraries come in a later module; a semaphore is the "by hand" version.)
- **Shed load instead of queueing it forever.** ASP.NET Core has built-in rate limiting middleware (`Microsoft.AspNetCore.RateLimiting`, part of the framework), including a **concurrency limiter**. Returning a fast `503 Service Unavailable` for request 401 is better than making all 400 wait 13 seconds. Clients and load balancers can retry somewhere else, and the requests that are accepted stay fast.
- **Don't "fix" it with `Task.Run`.** Wrapping the sync call in `await Task.Run(() => client.Get())` makes the endpoint look async, but `Task.Run` still uses a pool thread, which still blocks for the whole call. You've moved the blocking, not removed it (your T2 production note says this).

---

## 8. Writing team guidelines that people follow

The second half of the task is 5 rules for a team wiki. Most wiki guidelines fail for the same reasons: they're vague ("use async properly"), they don't say why, or nothing checks them. A rule that works has five parts:

| Part | What it is |
| --- | --- |
| **The rule** | One imperative sentence, specific enough that a reviewer can say "this line breaks it" |
| **Why** | The failure it prevents, ideally one you've seen (your labs count). People follow rules they understand and work around rules they don't |
| **Example** | ❌ one bad line, ✅ the good version. Readers copy the example, not the prose |
| **Exception** | When the rule doesn't apply. A rule with no exceptions gets ignored the first time it's wrong |
| **Enforced by** | An analyzer, a build setting, a test, or "code review". If it's only review, say so |

An example on a topic the task doesn't ask for, to show the shape:

> **Rule: Get the current time from `TimeProvider`, never from `DateTime.UtcNow` directly.**
> - **Why:** code that reads the clock directly can't be tested deterministically. A test for "shipment is overdue after 48 h" would have to wait 48 hours, or be flaky around midnight.
> - **❌** `if (DateTime.UtcNow - shipment.CreatedAt > TimeSpan.FromHours(48))`
> - **✅** `if (clock.GetUtcNow() - shipment.CreatedAt > TimeSpan.FromHours(48))`, with `TimeProvider` injected (`TimeProvider.System` in production, `FakeTimeProvider` in tests).
> - **Exception:** logging timestamps and diagnostics, where the real clock is what you want.
> - **Enforced by:** code review; a banned-API analyzer can make it a build error later.

**Where the rules come from:** the best ones come from what you've **measured or broken** yourself. That's exactly what M01's labs give you: thread-pool starvation (T2), cancellation not reaching the database (T3), allocations on hot paths (T4), captive dependencies (T5). A rule that says "we saw this freeze for 13 seconds" carries more weight than one that says "Microsoft recommends".

**Useful built-in analyzers to cite** in "Enforced by" (they're part of the .NET SDK, no package needed):
- **CA2016:** forward the `CancellationToken` to methods that accept one (you saw it fail the build in T3).
- **CA1849:** call the async version of a method inside an async method, instead of the blocking one.
- **CA2012:** use `ValueTask`s correctly (await them once, don't store them).
- `ValidateScopes` / `ValidateOnBuild` in a test: not an analyzer, but an automated check (your T5 lab).

**Keep it to 5.** A list of 30 rules is a list nobody reads. Pick the five whose violation is most expensive in production and hardest to spot in review.

---

## 9. Glossary

| Term | Meaning |
| --- | --- |
| **Thread-pool minimum** | The number of threads the pool creates on demand without delay; not a number of threads kept ready |
| **Thread injection** | The pool adding threads above the minimum, gradually, while it measures whether throughput improves |
| **Hill climbing** | The pool's algorithm for choosing a thread count by trying changes and measuring throughput |
| **Worker / completion-port threads** | Threads that run queued work / threads that handled I/O completions on Windows (little difference since .NET 7) |
| **`runtimeconfig.json`** | A file next to the app that sets runtime options (thread-pool minimum, GC mode) without code changes |
| **Context switch** | The OS swapping one running thread for another on a processor; costs CPU time and cache |
| **Band-aid** | A change that hides a symptom under current conditions without removing its cause |
| **Bulkhead** | A limit on how much of a shared resource (threads, connections) one dependency can use, so its failure can't sink everything |
| **Load shedding** | Rejecting excess requests quickly (e.g. `503`) instead of queueing them until everything is slow |
| **Concurrency limiter** | A limit on how many requests are processed at the same time; ASP.NET Core has one built in |
| **SDK (software development kit)** | A library a vendor ships to call their service |

---

## 10. Summary

1. The thread-pool **minimum** is how many threads the pool creates **without delay** when work arrives. Raising it skips the slow ramp-up; it doesn't pre-create threads.
2. Threads cost memory, context switches, longer GC pauses and contention elsewhere, so "set it very high" isn't free.
3. With blocking code, **threads needed = RPS × blocking time**. A fixed minimum covers one load and one latency. It fails when either grows, most dangerously when a dependency slows down, and it hides the cause in the meantime.
4. It's legitimate for blocking code you can't change yet, known bursts, migrations and emergencies: sized with Little's law, monitored, set in configuration, with a plan to remove it.
5. The fix is async all the way down. Until then, contain the blocking part (a bulkhead with `SemaphoreSlim`) and shed load (a concurrency limiter, fast `503`s). `Task.Run` around sync code moves the blocking, it doesn't remove it.
6. A good guideline is a specific rule + why + bad/good example + exception + how it's enforced. Five rules from real incidents beat thirty from a blog.

### Check yourself

1. After `SetMinThreads(200, 200)`, how many threads does the pool have one second after startup, with no traffic? (§2)
2. Why does the pool add threads slowly above the minimum, instead of creating one whenever work waits? (§2)
3. Why should you check the return value of `SetMinThreads`? (§3)
4. Name three costs of having 1,000 mostly blocked threads. (§4)
5. A sync endpoint calls a database that normally answers in 50 ms, at 300 RPS. How many threads does it need? And if the database slows down to 2 s? (§5)
6. Give two situations where raising the minimum is the right call, and what you'd do alongside it. (§6)
7. Why is `await Task.Run(() => syncClient.Get())` not a fix for thread-pool starvation? (§7)
8. What's the difference between a bulkhead and load shedding? (§7)
9. Rewrite "use async properly" as a rule a reviewer could apply to one specific line. (§8)

**Before you build: predict.** Rerun T2's `/slow-sync` with `SetMinThreads(200, 200)`:
- At 100 VUs: what happens to the RPS, the p95, the max latency, the freeze and the thread-count curve, compared with your T2 runs?
- At 400 VUs (`k6 run --vus 400 …`; the command-line flag overrides the script's 100): how many threads would be needed, and what do you expect to see?

Write your predictions down before running.

### Interview questions this prepares you for

- "Our API freezes under load, CPU is low, and someone suggests `SetMinThreads(1000, 1000)`. What do you say?"
- "When is raising the thread-pool minimum the right decision?"
- "What does a thread cost? Why not just run one per request?"
- "A downstream service gets slow and our whole API goes down with it. Why, and how do you prevent it?"
- "How would you introduce async/cancellation guidelines in a team of 15 developers working on a large legacy codebase?"

### For FleetTrack

- **The lab:** reuse `labs/M01-T2-starvation`. Add `ThreadPool.SetMinThreads(200, 200)` (and log its return value and `GetMinThreads`) at the top of `Program.cs`, then run the same three terminals as in T2 (API, `dotnet-counters`, k6). Restart the API before each run. Run `/slow-sync` at 100 VUs, and optionally at 400 VUs to see where the band-aid stops working. Remove the change afterwards, or keep it behind a command-line flag, so the T2 lab still shows the original behaviour.
- **The write-up:** `docs/journal/M01.md`. Plan rule 4 also asks for an end-of-module journal with **three things you learned, one surprise and one open question**, so this one file covers both: the module reflection, the `SetMinThreads` results and explanation (why it's a band-aid, when it's legitimate), and the **5 team rules** in the §8 format.
- **Later in FleetTrack:** the bulkhead and load-shedding ideas in §7 come back with the resilience work for synchronous calls to the Tracking service (M09) and with performance testing (M10). Your 5 rules feed into the review checklist from then on.
