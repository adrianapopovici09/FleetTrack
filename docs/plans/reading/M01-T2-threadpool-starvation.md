# M01-T2 · Thread-pool starvation (sync-over-async)

Task: [M01-T2](../modules/M01-csharp-runtime.md)

This file is the complete lesson for the task; you don't need to read anything else. Original sources, if you ever want to go deeper: [Debug thread-pool starvation (tutorial)](https://learn.microsoft.com/dotnet/core/diagnostics/debug-threadpool-starvation) · [dotnet-counters](https://learn.microsoft.com/dotnet/core/diagnostics/dotnet-counters) · [k6 getting started](https://grafana.com/docs/k6/latest/get-started/running-k6/)

**Reading time:** about 50 minutes. **Builds on:** [M01-T1](M01-T1-async-await.md) §1 and §7.4.

## Contents

1. [How the .NET thread pool works](#1-how-the-net-thread-pool-works)
2. [What thread-pool starvation is](#2-what-thread-pool-starvation-is)
3. [Estimating it before you measure: Little's law](#3-estimating-it-before-you-measure-littles-law)
4. [Where sync-over-async hides in real code](#4-where-sync-over-async-hides-in-real-code)
5. [Fixes, and the band-aids that aren't fixes](#5-fixes-and-the-band-aids-that-arent-fixes)
6. [Diagnosing it: `dotnet-counters` and friends](#6-diagnosing-it-dotnet-counters-and-friends)
7. [Load testing with k6](#7-load-testing-with-k6)
8. [Measuring honestly](#8-measuring-honestly)
9. [Glossary](#9-glossary)
10. [Summary, check yourself, interview questions, for FleetTrack](#10-summary)

---

## 1. How the .NET thread pool works

### 1.1 The idea

Creating an operating-system thread is expensive (about 1 MB of reserved stack, kernel bookkeeping), and having many more threads than CPU cores wastes time switching between them. So .NET keeps a **thread pool**: a shared set of reusable threads that run short pieces of work, called **work items**. ASP.NET Core runs every request, and every continuation after an `await`, as work items on this pool.

```
              ┌──────────────── global queue ────────────────┐
  new work ──►│  item  item  item  item  item  …              │
              └──────────────────────────────────────────────┘
                    │            │            │
                 ┌──▼──┐      ┌──▼──┐      ┌──▼──┐
                 │ T1  │      │ T2  │      │ T3  │   worker threads
                 │local│      │local│      │local│   (each has its own local queue;
                 └─────┘      └─────┘      └─────┘    idle threads "steal" from busy ones)
```

- Work is put on a **global queue**, or on the **local queue** of the thread that created it (continuations often go there).
- Each worker thread takes an item, runs it to completion, then takes the next.
- An idle thread with an empty local queue **steals** work from other threads' queues. This is called **work stealing**, and it keeps all the cores busy.
- The **queue length** (how many items are waiting) is one of the two numbers you'll watch in this task.

### 1.2 How many threads?

- **Minimum:** by default, one worker thread **per CPU core** (`Environment.ProcessorCount`). The pool creates up to this many threads **immediately**, on demand.
- **Above the minimum, it adds threads slowly, on purpose.** For normal busy CPU work, more threads than cores doesn't help. It only adds context switching. So the pool uses a feedback algorithm called **hill climbing**: it adds a thread, measures whether throughput (completed work items per second) improved, and keeps or removes it.
- **When every thread is busy and the queue isn't moving,** the pool detects **starvation** and injects extra threads. But it does this cautiously, on the order of **one or two threads per second**, because it can't tell "blocked waiting for I/O" apart from "busy computing".
- **Maximum:** very large (thousands), so in practice you never hit it. You hit the slow injection rate long before.

Recent .NET versions recognise some kinds of blocking (for example a thread waiting on `Task.Result`) and add threads faster in that case. That softens the problem, but it doesn't remove it: every blocked thread is still a thread doing nothing.

### 1.3 Why this design is right for async code

With async code, a pool thread **never waits**. It runs a request until its first incomplete `await`, goes back to the pool, and later some thread runs the continuation (M01-T1 §5). With 8 cores, 8 threads can keep thousands of requests in flight, because each request needs a thread only for the microseconds or milliseconds it's actually computing. The pool's "few threads, add slowly" policy is **perfect for async code and terrible for blocking code**.

---

## 2. What thread-pool starvation is

**Thread-pool starvation** happens when all pool threads are **blocked** (waiting, not computing), so queued work, including new requests *and* the continuations that would unblock the waiting threads, can't run. The pool then grows only at its slow injection rate.

Here is the classic trigger, sync-over-async in an endpoint:

```csharp
app.MapGet("/slow-sync", () =>
{
    Task.Delay(500).Wait();     // ❌ blocks this pool thread for 500 ms (Task.Delay returns a plain Task, so .Wait() rather than .Result)
    return "done";
});
```

What happens with 8 cores and 100 concurrent clients:

```
t = 0 s     100 requests arrive. 8 threads start 8 of them; each blocks in .Result for 500 ms.
            92 requests wait in the queue.
t = 0.5 s   The 8 delays finish… but each Task.Delay continuation that completes .Result is
            queued work too, so it competes with the 92 waiting requests.
            Threads unblock, finish, take the next request, and block again.
            Throughput is capped at about 8 threads / 0.5 s = 16 requests per second.
t = 1–30 s  The pool notices starvation and adds ~1–2 threads per second.
            Thread count climbs steadily: 8 → 10 → 12 → … Queue length stays high.
            Latency for each request = time waiting in the queue + 500 ms; it climbs into seconds.
```

**The tell-tale symptoms**, which you'll see in your measurements:
- **Low CPU** (the threads aren't computing, they're waiting), yet requests are slow or timing out.
- **Thread count rising steadily** without levelling off.
- **Queue length high** and not draining.
- **Latency far higher than the work itself takes** (seconds, for 500 ms of waiting).
- **Even unrelated endpoints become slow.** `/health/live` has to queue behind the blocked work too, so it can time out, and an orchestrator may then **restart a healthy-but-starved instance**. That's a nasty link to M00-T5: liveness checks nothing external, yet it can still fail under starvation.

**The async version** releases the thread during the wait:

```csharp
app.MapGet("/slow-async", async () =>
{
    await Task.Delay(500);      // ✓ the thread goes back to the pool for 500 ms
    return "done";
});
```

With 100 concurrent clients, every request takes about 500 ms, and throughput is close to 100 / 0.5 s = **200 requests per second**, with the same handful of threads and almost no queue.

---

## 3. Estimating it before you measure: Little's law

Your M00 drill feedback said "no estimates". This task is a perfect place to practise, because one formula predicts most of the result.

**Little's law:** in any stable system,

```
concurrency  =  throughput  ×  latency
   (L)            (λ, per s)     (W, in s)
```

It holds for a shop, a database connection pool or a thread pool. Rearranged for blocking code, where **each in-flight request occupies one thread**:

```
max throughput  =  number of threads  /  time each request holds a thread
```

**Worked example for the sync endpoint.** You have 8 cores (so 8 threads to start with), and each request blocks a thread for 0.5 s.
- Max throughput at the start is 8 / 0.5 = **16 requests/s**.
- To serve 100 concurrent clients at full speed, you'd need 100 threads. The pool starts with 8 and adds about 1–2 per second, so it needs **roughly a minute** to get there, if it ever does.
- So over a 30-second test, expect throughput to **start around 16 req/s and creep up**, with latency in the **seconds**.

**For the async endpoint,** a thread is held for microseconds, not 0.5 s. The limit is the clients: 100 clients ÷ 0.5 s per request = **about 200 requests/s**, with latency just over 500 ms.

Write down **your own** numbers (use your machine's core count) before you run anything. A senior engineer predicts, measures and then explains the difference. A prediction that turns out wrong is the most useful outcome, because then there's something to explain.

---

## 4. Where sync-over-async hides in real code

Nobody writes `Task.Delay(500).Wait()` on purpose. In real code, blocking hides in places like these:

| Hiding place | Example | Why it blocks |
| --- | --- | --- |
| `.Result` / `.Wait()` / `.GetAwaiter().GetResult()` | `var user = _repo.GetAsync(id).Result;` | Holds the thread until the task completes |
| Synchronous I/O APIs | `File.ReadAllText`, `HttpClient.Send`, EF Core `SaveChanges()` / `ToList()` instead of the `Async` versions | The thread waits for the disk, network or database |
| A synchronous interface you must implement | An old `IValidator.Validate()` that has to call an async service | Forces a `.Result` somewhere inside |
| Constructors and DI factories | `services.AddSingleton(sp => LoadConfigAsync().Result)` | Constructors can't be `async` |
| `Thread.Sleep` | Retry loops with `Thread.Sleep(1000)` | Holds the thread for the full delay; use `await Task.Delay` |
| Lock contention | A `lock` around slow work, with many requests waiting to enter | Waiting threads are blocked, not computing |
| Logging or telemetry that writes synchronously | A logger that writes to a network sink on the request thread | Each log call waits for the network |

**Fake async is a trap too:** `Task.Run(() => SyncMethod())` doesn't free anything. It just moves the blocking to *another* pool thread (M01-T1 §9). Under load, it starves the pool exactly the same way.

---

## 5. Fixes, and the band-aids that aren't fixes

**The real fix: async all the way.** Replace each blocking call with its async version and `await` it, all the way up the call chain to the endpoint. `async` is "contagious" upward: once a method awaits, its callers should await it too. That's normal and fine.

**When a sync API truly can't change** (a third-party interface, a constructor):
- Move the async work **out** of it. For example, load the data at startup in a hosted service, or use a factory method `static async Task<Foo> CreateAsync()` instead of a constructor.
- If you really must block, do it **once, at the outermost edge** (for example in `Main` of a console tool), never on a hot request path. Leave a comment explaining why.

**`ThreadPool.SetMinThreads(200, 200)` is a band-aid, not a fix.**
- What it does: the pool creates up to 200 threads immediately instead of injecting them slowly. The starvation symptoms vanish under *this* load.
- Why it's not a fix: each blocked thread still costs memory and context switching. The ceiling just moves (at 300 concurrent clients you starve again). And it hides the real bug from everyone who comes later.
- When it's legitimate: as a **temporary production mitigation** while the real fix is coded, or for a known, bounded amount of unavoidable blocking (legacy code). Record it with a comment or an ADR, and a plan to remove it.

**Rate limiting and timeouts** (ASP.NET Core's rate limiter, or a reverse proxy) protect the service from being overwhelmed while it's degraded, but they reject or fail requests rather than fixing anything. They complement the fix; they don't replace it.

---

## 6. Diagnosing it: `dotnet-counters` and friends

### 6.1 The diagnostic tools

.NET ships free command-line diagnostic tools, installed as global .NET tools:

```bash
dotnet tool install --global dotnet-counters     # live metrics
dotnet tool install --global dotnet-stack        # snapshot of every thread's call stack
dotnet tool install --global dotnet-trace        # detailed event recording (for later)
```

They attach to a **running** process, with no code changes and no restart, which is exactly what you need in a production incident.

### 6.2 `dotnet-counters`

```bash
dotnet-counters ps                                   # list .NET processes and their ids
dotnet-counters monitor -n <process-name> --counters System.Runtime
dotnet-counters monitor -p <pid> --counters System.Runtime,Microsoft.AspNetCore.Hosting
```

It refreshes a table of live **counters** (metrics) once per second. The ones that matter for this task, under the `System.Runtime` meter:

| Counter | What it tells you | Starvation looks like |
| --- | --- | --- |
| **Thread pool thread count** (`dotnet.thread_pool.thread.count`) | How many pool threads exist right now | Climbs steadily, by about one or two per second |
| **Thread pool queue length** (`dotnet.thread_pool.queue.length`) | Work items waiting for a thread | High, and not draining |
| **Thread pool completed work items** (`dotnet.thread_pool.work_item.count`) | Throughput of the pool | Low compared with the async run |
| **Lock contentions** (`dotnet.monitor.lock_contentions`) | Times a thread had to wait to enter a `lock` | Rises if contention is the cause |
| **CPU usage** (process CPU time) | How busy the CPU is | **Low**: the threads are waiting, not working |

Names differ between .NET versions. Since .NET 9, the runtime publishes these as `System.Runtime` metrics with names like `dotnet.thread_pool.thread.count`, while older versions show EventCounter names like `threadpool-thread-count` and `threadpool-queue-length`. `dotnet-counters` shows whichever your app emits, so just look for "thread pool … thread count" and "queue length".

From `Microsoft.AspNetCore.Hosting` you get request-level metrics, such as active requests (`http.server.active_requests`) and request duration.

### 6.3 Finding *where* it blocks: `dotnet-stack`

Counters tell you **that** the pool is starved. To find **which code** blocks, take a snapshot of every thread's stack during the load test:

```bash
dotnet-stack report -p <pid>
```

When many threads show the same frames, for example `Task.Wait` / `ManualResetEventSlim.Wait` underneath your endpoint method, you've found the culprit. In a real incident this is the step that points you to the line of code.

---

## 7. Load testing with k6

### 7.1 What k6 is

**k6** (by Grafana Labs) is a free, open-source load-testing tool. Its licence is AGPL-3.0, which is fine for running it as a local tool, since you aren't distributing a modified k6. You write the test in **JavaScript**, and k6 runs it with many simulated users. On Windows it's a single program: `winget install k6 --source winget` (or `choco install k6`).

### 7.2 A minimal script

```javascript
// load.js
import http from 'k6/http';

export const options = {
  vus: 100,            // 100 virtual users
  duration: '30s',     // for 30 seconds
};

export default function () {
  http.get('http://localhost:5000/slow-sync');   // each VU loops: request, wait for response, repeat
}
```

```bash
k6 run load.js
```

- **A VU (virtual user)** is one simulated client running the `default` function in a loop: send a request, **wait for the response**, send the next one.
- At the end, k6 prints a summary. The lines you need:
  - **`http_reqs`**: total requests, and the rate per second. **That's your RPS.**
  - **`http_req_duration`**: latency statistics, including `p(95)`. **That's your p95**: 95 % of requests were faster than this value.
  - **`http_req_failed`**: the share of failed requests (timeouts, errors).

**Why p95 and not the average?** An average hides the slow tail. If 90 % of requests take 500 ms and 10 % take 8 seconds, the average is about 1.3 s, which describes nobody's experience. The p95 shows the 8-second tail that real users are complaining about.

### 7.3 Thresholds: turning a load test into a pass/fail check

```javascript
export const options = {
  vus: 100,
  duration: '30s',
  thresholds: {
    http_req_duration: ['p(95)<800'],   // fail the run if p95 ≥ 800 ms
    http_req_failed:   ['rate<0.01'],   // fail if more than 1 % of requests fail
  },
};
```

k6 then **exits with a non-zero code** when a threshold is broken. That's what lets a load test fail a pipeline (M00-T7 §2: exit codes are how steps fail). You'll use this in M10.

### 7.4 Closed vs open models: a senior point

The `vus` setup above is a **closed model**: each VU waits for its response before sending the next request. When the server slows down, the VUs automatically **send fewer requests**. That hides how bad things are, because real users don't politely wait for each other; they keep arriving. This measurement bias is called **coordinated omission**.

An **open model** sends requests at a **fixed arrival rate**, whatever the server does:

```javascript
export const options = {
  scenarios: {
    steady: {
      executor: 'constant-arrival-rate',
      rate: 150, timeUnit: '1s',          // 150 new requests every second, no matter what
      duration: '30s',
      preAllocatedVUs: 200,               // enough VUs to keep up the rate while requests are slow
    },
  },
};
```

For this task the plan asks for **100 VUs** (the closed model), which is fine for showing the difference. Just know that real capacity testing usually uses an arrival rate, and say so if an interviewer asks how you'd load-test properly.

---

## 8. Measuring honestly

Measurements are only worth something if the comparison is fair. A checklist:

- **Run a Release build without a debugger.** `dotnet run -c Release`, or run the built DLL. A debugger and a Debug build distort timings.
- **Same machine, same conditions for both runs.** k6 and the app share your CPU. That's acceptable when comparing two versions *on the same setup*, but say so in the write-up.
- **Restart the app between runs.** Otherwise the async run inherits the extra threads the sync run created, and the comparison is unfair.
- **Warm up.** The first seconds include JIT compilation (compiling methods the first time they run). Either ignore the first few seconds, or do a short warm-up run first.
- **Run each test at least twice.** If the numbers differ a lot between runs, report that instead of picking the nicer one.
- **Record the context:** core count, .NET version, VUs, duration, which endpoint.
- **Watch the counters during the run.** k6's summary only gives you the *end* result. The thread count climbing over time, live in `dotnet-counters`, is the story.

### What `docs/perf/M01-threadpool.md` should contain

1. **Setup:** machine (cores), .NET version, k6 settings, the two endpoints.
2. **Prediction:** your Little's law estimate (§3) for each endpoint, written *before* running.
3. **Results table:** for each run, RPS, p95, thread count (start → end) and queue length (typical value).
4. **Explanation:** why the numbers differ, in your own words, using: blocked vs released threads, the slow injection rate, queue length, Little's law. Compare with your prediction.
5. **What you'd do in production:** the fix, and how you'd detect this in a real system (alerts on queue length or thread count, latency, low CPU).

---

## 9. Glossary

| Term | Meaning |
| --- | --- |
| **Thread pool** | .NET's shared set of reusable worker threads that run short work items |
| **Work item** | One unit of queued work: a request, or a continuation after an `await` |
| **Global / local queue** | The shared queue of work / a per-thread queue, used for locality |
| **Work stealing** | An idle thread taking work from another thread's local queue |
| **Hill climbing** | The pool's feedback algorithm: add a thread, measure throughput, keep it or not |
| **Thread injection** | The pool adding threads above its minimum, slowly (about one or two per second) |
| **Thread-pool starvation** | All pool threads blocked, so queued work waits while the pool grows slowly |
| **Sync-over-async** | Blocking a thread on asynchronous work (`.Result`, `.Wait()`) |
| **Little's law** | concurrency = throughput × latency; predicts capacity limits |
| **`dotnet-counters` / `dotnet-stack`** | CLI tools: live runtime metrics / call stacks of every thread in a running process |
| **k6** | An open-source load-testing tool, scripted in JavaScript |
| **VU (virtual user)** | A simulated client in k6 that loops over the test function |
| **RPS** | Requests per second (throughput) |
| **p95 latency** | The response time that 95 % of requests were faster than |
| **Closed / open model** | Load where clients wait for responses (VUs) / load at a fixed arrival rate |
| **Coordinated omission** | The bias where a slowing server receives fewer requests, hiding its real latency |
| **JIT** | Just-in-time compilation: .NET compiles each method the first time it runs |

---

## 10. Summary

1. The thread pool starts with about one thread per core and adds more only slowly (about one or two per second). That's ideal for async code and terrible for blocking code.
2. Starvation means all pool threads are blocked, so new requests *and* the continuations that would unblock them sit in the queue. The symptoms are low CPU, a steadily rising thread count, a high queue length, latency in seconds, and even health checks timing out.
3. Little's law predicts it: with blocking code, max throughput = threads ÷ time each request holds a thread.
4. Sync-over-async hides in `.Result`/`.Wait()`, synchronous I/O APIs, constructors and DI factories, `Thread.Sleep` and lock contention. `Task.Run` doesn't fix it.
5. The fix is async all the way. `SetMinThreads` is a temporary, documented band-aid.
6. `dotnet-counters` shows *that* the pool is starved, and `dotnet-stack` shows *where* it's blocked. k6 generates the load; read RPS and p95, not the average.
7. Measure fairly (Release build, restart between runs, warm up, run twice), and predict before you measure.

### Check yourself

1. Why does the pool add threads slowly above its minimum, and why does that hurt blocking code specifically? (§1.2, §1.3)
2. You have 4 cores, and an endpoint blocks for 200 ms. Estimate the maximum RPS at the start of a load test. How many threads would 100 concurrent clients need? (§3)
3. CPU is at 10 %, but requests time out. Which two counters do you look at first, and what values would confirm starvation? (§2, §6.2)
4. A colleague "fixes" a slow endpoint by wrapping its sync database call in `Task.Run`. What happens under load? (§4)
5. When is `ThreadPool.SetMinThreads` acceptable, and what must accompany it? (§5)
6. Why can `/health/live` fail on a starved instance even though it checks nothing external, and what does the orchestrator then do? (§2)
7. Why does a VU-based (closed) load test understate how bad a slowdown is? (§7.4)

**Before you build: predict.** Find your machine's core count (`[Environment]::ProcessorCount` in PowerShell, or `Environment.ProcessorCount` in C#). Then, for 100 VUs over 30 s against both endpoints, write down the expected RPS, a rough p95, and what the thread count will do. Post your predictions here before running anything.

### Interview questions this prepares you for

- "Our API's CPU is at 10 %, but requests time out under load. What do you suspect, and how do you confirm it?"
- "What is thread-pool starvation, and how do you fix it properly?"
- "Why doesn't `Task.Run` fix a blocking call?"
- "Would you ever call `ThreadPool.SetMinThreads` in production?"
- "How would you load-test an API, and which numbers would you report?"
- "Explain Little's law and use it to estimate capacity."

### For FleetTrack

- The lab goes in `labs/` (a throwaway minimal API, outside FleetTrack's architecture), with two endpoints: `/slow-sync` (`Task.Delay(500).Wait()`) and `/slow-async` (`await Task.Delay(500)`).
- The write-up goes in `docs/perf/M01-threadpool.md` (§8).
- The habit carries into FleetTrack: no `.Result`, `.Wait()`, synchronous EF Core calls or `Thread.Sleep` on request paths. A good review question for every PR is "does anything here block a thread?"
- k6 comes back in M10 (performance and caching), with thresholds and arrival-rate scenarios, against FleetTrack's real endpoints.
