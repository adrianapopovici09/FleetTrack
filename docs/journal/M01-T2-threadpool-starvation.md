# M01-T2: thread-pool starvation, sync-over-async vs async

* **Date:** 2026-10-17
* **Task:** M01-T2 (Thread-pool starvation) · [lesson](../plans/reading/M01-T2-threadpool-starvation.md)
* **Lab:** [labs/M01-T2-starvation](../../labs/M01-T2-starvation/) (`Program.cs`, `load.js`)

## What I did

I ran the same 500 ms delay as a blocking endpoint and as an async one, under the same load, twice each, and recorded k6 results and thread-pool counters.

| | |
| --- | --- |
| Machine | 4 physical cores; **.NET sees 8 logical processors** (`dotnet.process.cpu.count` = 8 in the counters CSV), so the pool's minimum is probably 8 threads, not 4. The prediction below was made with 4. |
| Runtime | .NET 10, Release build, no debugger. The runs used the **Development** environment: Visual Studio had added a `launchSettings.json` that `dotnet run` picked up. Little effect here (no `appsettings.Development.json`, trivial endpoints); the file has since been removed so future runs use Production. |
| Endpoints | `/slow-sync`: `Task.Delay(500).Wait()` (blocks a pool thread) · `/slow-async`: `await Task.Delay(500)` |
| Load | k6, 100 VUs (closed model), 30 s, one endpoint per run |
| Measured with | k6 summary (`http_reqs` rate, `http_req_duration` p95) + `dotnet-counters` (`System.Runtime`: thread-pool thread count, queue length) |
| Caveat | k6 and the API share the same 4 cores |

### How to run

Use three terminals, from the repository root. **Restart the API before every run.**

```powershell
# 1. the API
dotnet run -c Release --project labs/M01-T2-starvation

# 2. live counters (the process is named after the project)
dotnet-counters monitor -n Starvation --counters System.Runtime

# 3. the load (then repeat with ENDPOINT=slow-async)
k6 run -e ENDPOINT=slow-sync labs/M01-T2-starvation/load.js
```

During the sync run, also try `curl.exe -w "%{time_total}s\n" http://localhost:5000/ping` and see how long a request that does no blocking work at all takes.

### My prediction (written before running)

Little's law: concurrency = throughput × latency. With blocking code, each in-flight request holds one thread, so **max throughput = threads ÷ 0.5 s**. 100 VUs are always waiting for a response, so **latency = 100 ÷ throughput**.

| | `/slow-sync` | `/slow-async` |
| --- | --- | --- |
| RPS at the start | 4 threads ÷ 0.5 s = **8 req/s** | 100 ÷ 0.5 s = **200 req/s** (the ceiling for 100 VUs) |
| RPS at the end of 30 s | pool adds ~1–2 threads/s → 34–64 threads → **~70–130 req/s** | **~200 req/s** |
| RPS over the whole run | well below the end value, because it climbs from 8 | **~200 req/s** |
| Latency | start: 100 ÷ 8 = **12.5 s**; end: 100 ÷ 70–130 = **~0.8–1.4 s** | 100 ÷ 200 = **0.5 s** (just the delay; no queueing) |
| p95 | **several seconds**: the slow early requests are well over 5 % of the total, so they set the p95 | **just over 500 ms** |
| Pool thread count | climbs steadily from 4 towards **34–64**, never levelling off | stays at **~4**: no thread blocks, so no starvation is detected |
| Queue length | **~96** at the start, slowly falling as threads are added | **~0** |

Why: the sync endpoint holds a thread for the whole 500 ms, and above its minimum (one thread per core) the pool adds threads only slowly. The async endpoint gives the thread back during the delay, so 4 threads are enough for 100 concurrent requests. Recent .NET versions add threads faster when they detect blocking on a task, so sync results above the predicted range wouldn't be a surprise.

## What I observed

### Run 1

| | `/slow-sync` | `/slow-async` |
| --- | --- | --- |
| Requests (total) | 1,259 | **5,900** (4.7×) |
| RPS (`http_reqs` rate) | 41.7 req/s | **194.0 req/s** |
| Latency (`http_req_duration`) | avg 2.39 s · median 1.02 s · p90 5.10 s · **p95 5.11 s** · max 12.25 s | avg 514 ms · median 512 ms · p90 519 ms · **p95 521 ms** · max 616 ms |
| Failed requests | 0 % | 0 % |
| Thread count at ~5 s / ~15 s / ~30 s | 52 / 65 / 99 | 9 / 9 / 9 |
| Queue length at ~5 s / ~15 s / ~30 s | 50 / 107 / 0 | 0 / 0 / 0 (one short blip to 36 at ~20 s) |
| Process CPU (8 logical processors) | ~0–5 % throughout | ~0–6 % throughout |
| `/ping` time during the run | not measured | not measured |

Counters: [M01-sync-run1.csv](../perf/M01-sync-run1.csv) · [M01-async-run1.csv](../perf/M01-async-run1.csv). In that file, `dotnet-counters` exports the thread count and queue length as **per-second changes** (`/ 1 sec`), so the values above are running totals of those changes, from an idle baseline of ~1 thread.

#### `/slow-sync` run 1, second by second

```
time      threads  queue  work items/s
15:41:58     6       2        11      ← load starts
15:42:00    28      18        56      ← jumps of ~20 threads at once, much faster than 1–2/s
15:42:03    52      50       136
15:42:05    55      85        12      ← from here: about +1 thread per second
15:42:10    62      35       272
15:42:13    65     107        10      ← queue at its peak
15:42:18    71      85        14
15:42:22    76      82        20
15:42:23    96      18       293      ← another jump of 20 threads
15:42:24    97       1       352      ← threads ≈ concurrency (100 VUs): the queue drains
15:42:27    99       0       735      ← no more starvation: work flows freely
15:42:29    99       0       374      ← load ends; the 99 threads stay alive afterwards
```

Observations to explain in the write-up:
- **Two injection speeds.** The pool sometimes added ~20 threads at once (at the start, and at 15:42:23), and otherwise about one per second. Recent .NET recognises threads blocked waiting on a task (like `.Wait()`) and adds threads faster in bursts; the steady +1/s is the normal starvation injection.
- **Starvation ended only when the thread count reached about the concurrency level** (~96–99 threads for 100 VUs). From then on the queue was empty and work items per second jumped from ~10–20 to 300–700.
- **CPU stayed almost idle** the whole time, while the slowest requests took 12 s: the classic starvation signature.
- **The queue swung up and down** (35 → 107 → 30) instead of falling smoothly, as batches of blocked requests completed together every 500 ms.

#### `/slow-async` run 1, second by second

```
time      threads  queue  work items/s
15:45:02     6       0       609      ← load starts: ~600–660 work items/s from the first second
15:45:06     9       0       659      ← +3 threads, then flat for the rest of the run
15:45:15     9       0       641
15:45:22     9      36       533      ← the only queue blip, gone within ~2 s
15:45:25     9       0       659
15:45:31     9       0       660
15:45:33    21       0       335      ← load ends: +12 threads while k6 closes 100 connections at once
```

Observations:
- **9 threads served 100 concurrent requests** for the whole run, which is about the pool's minimum for 8 logical processors. No thread ever blocked, so the pool never saw starvation and never injected threads.
- **~650 work items/s for ~194 requests/s** is about 3 work items per request: the request itself, the timer completing `Task.Delay`, and the continuation after the `await`. Each one takes microseconds, so a handful of threads is plenty.
- **Latency is the delay itself** (p95 521 ms vs 500 ms of `Task.Delay`): requests never waited for a thread.

#### Prediction vs measurement

| | Predicted | Measured |
| --- | --- | --- |
| Sync RPS over the run | well below the ~70–130 end value | 41.7 |
| Sync slowest request | 100 ÷ 8 = 12.5 s | 12.25 s |
| Sync p95 | several seconds | 5.11 s |
| Sync threads | 4 → 34–64, still climbing | ~1 → 99, in bursts of ~20 plus ~1/s; levelled off at ≈ concurrency |
| Async RPS | ~200 (100 VUs ÷ 0.5 s) | 194 |
| Async p95 | just over 500 ms | 521 ms |
| Async threads | ~4 (one per core) | ~9 (one per **logical** processor, 8, plus one) |

### Run 2

| | `/slow-sync` | `/slow-async` |
| --- | --- | --- |
| Requests (total) | 3,443 | 5,900 |
| RPS | 112.9 req/s | 194.0 req/s |
| Latency | avg 877 ms · median 514 ms · p90 520 ms · **p95 522 ms** · **max 13.24 s** | avg 515 ms · median 513 ms · p90 520 ms · **p95 523 ms** · max 610 ms |
| Failed requests | 0 % | 0 % |
| Thread count at ~5 s / ~15 s / ~30 s | 55 / 101 / 103 | 11 / 11 / 12 |
| Queue length at ~5 s / ~15 s / ~30 s | 50 / 0 / 0 | 0 / 0 / 0 (one blip to 88 at ~16 s, drained within a second) |

Counters: [M01-sync-run2.csv](../perf/M01-sync-run2.csv) · [M01-async-run2.csv](../perf/M01-async-run2.csv).

**Async is reproducible; sync isn't.** Both async runs gave exactly 5,900 requests at 194 req/s, p95 521–523 ms, ~9–12 threads and ~620–660 work items/s. The two sync runs differed a lot (41.7 vs 112.9 req/s, p95 5.11 s vs 522 ms), because their behaviour depends on the thread pool's non-deterministic injection.

#### `/slow-sync` run 2, second by second

```
time      threads  queue  work items/s
15:56:13    27      78       201     ← load starts; +26 threads at once
15:56:14    39      66         0     ← complete freeze: zero work items completed…
15:56:17    61      44         0     ← …for 12 s, while injection slows from +12/s to +4/s
15:56:21    81      40         0
15:56:25    97      24         0
15:56:26    99       0       659     ← threads ≈ 100 VUs: everything unblocks at once
15:56:28   101       0       724     ← free flow for the rest of the run, ~513 ms per request
15:56:42   103       0       753
15:57:03    52       0         0     ← ~20 s after the load ends, idle threads retire
15:57:04     0       0         0
```

Why run 2's p95 looks healthy:
- **Two phases:** a 13-second total freeze, then ~17 s of free flow once the pool had one thread per VU.
- **The freeze hit only ~100 requests** (the first one of each VU), about 3 % of 3,443. The p95 is decided by the slowest 5 %, so it can't see them; only the **max (13.24 s)** and the **average (877 ms vs a 514 ms median)** show it.
- **Different shape from run 1:** in run 1 the pool injected in bursts with pauses and requests trickled through; in run 2 it injected steadily (+26, +12, +9 … +4/s) and caught up sooner, but nothing completed until it did. Thread injection isn't deterministic, which is why the task asks for two runs.
- **Idle threads retire ~20 s after the load stops** (103 → 0), so the next burst starts from scratch and starves again.

## What would happen in production

SetMinThreads is not a fix because the ceil can still be hit and the issue appers again.
The measurements to look after: rising thread count, queue length above 0, low CPU with high latency, max latency

## Why

For the sync endpoints there was the need to add new threads to the pool because the others were blocked waiting, while the async endpoints did not because instead of waiting threads were freed to the pool to do other work while the waiting happened.
The prediction was off because the calculations I did used 4 cores, instead of actual 8. Also the threads were added much faster than just one or two per second.
In sync runs, the p95 showed different behavior. There was a 13s freeze with 0 work items finished, then when the pool reached 100 threads it performed at almost the same speed as async and the freeze was totally omited by the p95 because of when the threads were added to the thread pool.
In async this behavior is not seen because there is no need for new threads to be added to the pool.

## The fix / the rule I'll follow

The rule is to always use async all the way. Task.Run is still executed on the thread pool and the sync still blocks.
