# M01-T1: which thread runs the code after an await?

* **Date:** 2026-10-17
* **Task:** M01-T1 (How async/await really works)
* **Note:** drafted by Claude from my answers in the session, using a real run of the lab. Reread it and rewrite anything I couldn't explain myself.

## What I did

I wrote a console app ([labs/M01-T1-async](../../labs/M01-T1-async/Program.cs)) that logs `Environment.CurrentManagedThreadId` and `Thread.CurrentThread.IsThreadPoolThread` before and after different awaits. Each case runs in **its own process** (`dotnet run -- <case>`), so every case starts on the main thread. In one process, once a case resumes on a pool thread, `Main` stays on the pool, and the next case wouldn't start on the main thread any more.

Before running it, I predicted the results.

## What I observed

The console app has no `SynchronizationContext` (`SynchronizationContext.Current is null: True`).

| Case | Before | After |
| --- | --- | --- |
| 1 `await Task.CompletedTask` | thread 2, main thread | thread 2, main thread |
| 2 `await Task.Delay(100)` | thread 2, main thread | thread 5, pool thread |
| 3 `await Task.Yield()` | thread 2, main thread | thread 4, pool thread |
| 4 `await Task.Delay(100).ConfigureAwait(false)` | thread 2, main thread | thread 5, pool thread |
| 5 two awaits: A → B → C | A: thread 2, main | B: thread 5, pool → C: thread 5, pool |

I ran case 5 five times, and B → C stayed on the same pool thread every time.

My first predictions for 2–4 were "may be the same, may be different". After the hint about the hidden entry point, the real answer is that the **first** incomplete await **always** moves off the main thread.

## What would happen in production

In ASP.NET Core there's no main thread running the request and no `SynchronizationContext`. Each request starts on a pool thread, and after every incomplete await it continues on **whatever pool thread is free**. Under load, with many requests in flight, the continuation is very likely to land on a different pool thread from the one before the await. My B → C result (same thread every time) only happened because the app was idle and one pool thread kept getting the work back.

So any code that relies on staying on one thread across an await breaks in production, even if it worked in a quiet test: `[ThreadStatic]` fields, thread-affine locks, or assuming a thread id stays the same. Per-request data has to travel in the `ExecutionContext` (`AsyncLocal`), which flows across awaits whatever thread runs next.

## Why

- **The fast path (case 1).** `await` first checks `IsCompleted`. If the task has already finished, the method just carries on, synchronously, on the same thread. Nothing is captured, nothing is scheduled.
- **The slow path (cases 2–5).** If the task isn't finished, the state machine saves its position (locals live in its fields, on the heap once it's boxed), registers a continuation and **returns**. The thread is now free.
- **Why the main thread can't come back.** For `async Task Main`, the compiler generates a hidden synchronous entry point that calls `Main(args).GetAwaiter().GetResult()`. After the first incomplete await, the main thread is **blocked** in that `GetResult()` call, waiting for the whole `Main` task. It's busy, so the continuation has to run on a **thread-pool thread**.
- **`Task.Yield()` (case 3)** returns an awaiter whose `IsCompleted` is always false. That forces the slow path, and with no context the continuation is queued to the pool.
- **`ConfigureAwait(false)` (case 4)** only means "don't capture the `SynchronizationContext`". A console app doesn't have one, so it changes nothing, and case 4 behaves exactly like case 2. It would matter in a WPF app or in library code.
- **After the first switch (B → C)** you're already on the pool, and which pool thread runs the next continuation depends on which one is free. It isn't predictable, even if it looked stable here.

## The fix / the rule I'll follow

The code after an `await` runs on the same thread **only if the awaited task had already completed** (the fast path). Otherwise it runs wherever the captured context sends it, and with no context (console apps, ASP.NET Core) that means any free pool thread. So I never rely on thread identity across an await, and I keep per-request state in the `ExecutionContext` instead.

## Still to do

- Decompile one async method in SharpLab (Release mode), and annotate the hoisted locals, the `state` field, the `IsCompleted` check, and `AwaitUnsafeOnCompleted` + `return` (reading notes §4).
