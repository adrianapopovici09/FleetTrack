# M01-T1 reading notes: how async/await really works

Task: [M01-T1](../modules/M01-csharp-runtime.md) · Sources: [How async/await really works in C# (Stephen Toub)](https://devblogs.microsoft.com/dotnet/how-async-await-really-works/) · [ConfigureAwait FAQ](https://devblogs.microsoft.com/dotnet/configureawait-faq/) · [Understanding the whys, whats and whens of ValueTask](https://devblogs.microsoft.com/dotnet/understanding-the-whys-whats-and-whens-of-valuetask/)

---

## 1. The problem async solves: threads are expensive, waiting is cheap

A web server handles each request on a thread. A thread is costly: it reserves memory for its stack (about 1 MB by default) and the operating system has to schedule it. Most of a typical request's time, though, is spent **waiting**: for Postgres to return rows, for an HTTP call to answer, for a file to be read. While the request is only waiting, its thread does nothing useful.

**Asynchronous I/O** lets the thread go back to the pool while the operation is in flight. The operating system notifies .NET when the data arrives, and only then does a thread pick up the rest of the work. The result is that a few dozen threads can serve thousands of concurrent requests. For example, if FleetTrack's `GET /shipments` spends 50 ms waiting for Postgres and 1 ms computing, async code needs a thread for 1 ms instead of 51 ms per request.

Two consequences worth knowing:

- **Async improves scalability, not speed.** A single request isn't faster with `await`; it's often a few microseconds slower because of the bookkeeping. The win is that the server can handle more requests at once with the same threads.
- **Async helps I/O-bound work, not CPU-bound work.** Wrapping a heavy calculation in `Task.Run` inside a web request just moves the work to another pool thread. That's useful in a desktop app (to keep the UI thread free), but on a server it gains nothing and adds overhead.

---

## 2. What the compiler does with an `async` method

`async` and `await` don't exist at runtime. The C# compiler rewrites an `async` method into a **state machine**: a generated type that remembers where the method stopped, so it can resume there later.

Roughly, for a method like this:

```csharp
async Task<int> CountShipmentsAsync()
{
    var conn = await OpenAsync();        // await #1
    var count = await QueryAsync(conn);  // await #2
    return count;
}
```

the compiler produces:

1. **A state machine struct** (a class in Debug builds, to support debugging features) that implements `IAsyncStateMachine`. Every local variable that's used after an `await` (here `conn`) becomes a **field** of that struct, because the method's stack frame disappears whenever the method returns early.
2. **An integer `state` field.** `-1` means "running or not started", `0` means "paused at await #1", `1` means "paused at await #2", and so on.
3. **A `MoveNext()` method** containing your original code, cut at every `await` and wrapped in a `switch (state)` so that each call jumps to the right place.
4. **A builder** (`AsyncTaskMethodBuilder<int>`) that creates the `Task<int>` the caller receives, and completes it with `SetResult` or `SetException` when the method finishes.

Your original method becomes a small stub: it creates the state machine, calls `builder.Start()` (which runs `MoveNext()` **synchronously, on the caller's thread**, up to the first `await` that actually has to wait), and returns `builder.Task`.

This is why **an async method runs synchronously until its first incomplete await.** Any code before that, including argument validation and any exception it throws, runs on the caller's thread before the caller even receives the task.

---

## 3. What `await` actually does

`await x` is a pattern, not a feature tied to `Task`. The compiler calls `x.GetAwaiter()` and then:

1. **Checks `awaiter.IsCompleted`.** If the operation has already finished (for example the data was already buffered, or a cache returned a value), the method simply **continues on the same thread**, with no allocation and no thread switch. This "fast path" is very common, and it's why the thread id often does *not* change after an `await`.
2. **If it isn't complete,** the method saves its state (`state = 0`, locals already in fields) and asks the awaiter to call `MoveNext()` when the operation finishes (via `builder.AwaitUnsafeOnCompleted`). The first time this happens, the state machine is copied from the stack to the heap (boxed), because it has to outlive the current stack frame. Then **the method returns** to its caller, and the thread is free.
3. **When the operation completes,** something calls `MoveNext()` again. The `switch` jumps to the code after the `await`, which calls `awaiter.GetResult()`. That returns the value, or rethrows the exception if the operation failed.

**Which thread runs the continuation?** It depends on what was captured at the `await`:

- If there was a **`SynchronizationContext`** (section 4) and you didn't write `ConfigureAwait(false)`, the continuation is posted back to that context, for example the UI thread in a desktop app.
- Otherwise, if a non-default `TaskScheduler` is current, it is queued to that scheduler (rare in application code).
- Otherwise it runs on a **thread-pool thread**, and often directly on the thread that completed the operation (for example the I/O completion thread), without an extra queue hop.

So in a console app or in ASP.NET Core, the code after an incomplete `await` usually runs on a **different** pool thread than the code before it. **Your code must never assume it stays on one thread**, for example by using `[ThreadStatic]` fields or thread-affine locks such as `Monitor`/`lock` across an `await` (the compiler forbids `await` inside `lock` for exactly this reason).

### ExecutionContext is not SynchronizationContext

These two are easy to confuse:

- **`ExecutionContext`** carries "ambient" data along the logical flow of the async operation: `AsyncLocal<T>` values, the current culture, and things like the current `Activity` used for tracing. It **always flows** across `await`, whatever thread runs the continuation. `ConfigureAwait` does not affect it. That's how a correlation id or a tenant id stored in an `AsyncLocal` survives the thread switches inside one request.
- **`SynchronizationContext`** decides **where** the continuation runs. It's the only thing `ConfigureAwait(false)` changes.

---

## 4. `SynchronizationContext` and `ConfigureAwait(false)`

A **`SynchronizationContext`** is an object that knows how to "post work back to a particular place". Windows Forms and WPF install one that posts back to the single UI thread, because UI controls may only be touched from that thread. Classic ASP.NET (on .NET Framework) installed one that restored the request's `HttpContext` and allowed only one thread into a request at a time.

**`ConfigureAwait(false)`** tells `await`: "don't post the continuation back to the captured context; run it wherever is convenient". It has two benefits in code that runs under a context: it avoids queueing work onto a busy UI thread, and it avoids a classic **deadlock**:

1. UI code calls `GetDataAsync().Result`, which blocks the UI thread while it waits.
2. Inside `GetDataAsync`, an `await` completes and wants to resume **on the UI thread**.
3. The UI thread is blocked waiting for that very task, so the task can never finish. Both sides wait forever.

**ASP.NET Core has no `SynchronizationContext`.** Continuations simply run on the thread pool. As a result:

- In **application code** (endpoints, handlers, EF Core calls in FleetTrack), `ConfigureAwait(false)` makes no practical difference, and most teams leave it out to keep the code readable.
- In **library code** (a NuGet package that a WPF app or a legacy ASP.NET app might use), `ConfigureAwait(false)` on every `await` is still the guideline, because the library can't know which context its caller has.
- The *particular* `.Result` deadlock above can't happen in ASP.NET Core, but **blocking on async code is still harmful there**. Each blocked request holds a pool thread while it waits. Under load, the pool runs out of threads faster than it can add them (it injects new threads slowly, by design). That's **thread-pool starvation**, the subject of M01-T2.

**When not to rely on `ConfigureAwait(false)`:** it isn't a fix for sync-over-async. If the awaited task has already completed, the `ConfigureAwait(false)` has no effect and the rest of the method keeps running on the original context. So "add `ConfigureAwait(false)` everywhere so `.Result` is safe" is a fragile workaround, not a design. The real fix is "async all the way".

---

## 5. `Task` vs `ValueTask`

A `Task<T>` is a **class**, so every async method that returns one allocates it on the heap. The exception is when the result can be served from a cache: the runtime caches completed tasks for a few common values (for example `true`/`false`), and you can cache your own. On a hot path (one called millions of times per second) these allocations add garbage-collection pressure, which you'll measure in M01-T4.

**`ValueTask<T>`** is a **struct** that holds *either* the result directly (when the method completed synchronously, so nothing is allocated), *or* a `Task<T>`, *or* an `IValueTaskSource<T>`. The last option is an advanced, poolable object that lets types like sockets reuse the same object for every read instead of allocating a new task.

The typical case where `ValueTask` pays off is a method that **usually completes synchronously**. One example is a stream read that is usually served from a buffer already in memory. Another is a cache lookup where most calls hit.

### The rules that make `ValueTask` dangerous

Because a `ValueTask` may wrap a pooled object that's reused after you consume it, you must:

- **Await it only once.** Awaiting it a second time may read the result of a *different* operation that has since reused the pooled object.
- **Never await it concurrently** from two places.
- **Never call `.Result` / `.GetAwaiter().GetResult()` before it has completed.** That isn't supported for the pooled kind and may not block.
- If you need any of those things (storing it, awaiting it twice, `Task.WhenAll`), **call `.AsTask()` once** and use the resulting `Task`.

### Industry practice

**Default to `Task`.** It's simpler, it can be awaited any number of times, and it composes with `WhenAll`/`WhenAny`. Use `ValueTask` only on hot paths where a profiler or BenchmarkDotNet shows the allocation matters *and* synchronous completion is common. If an async method almost always actually waits (a database call, for example), a `ValueTask` still ends up wrapping an allocated object, and you've only added the misuse rules. Interfaces designed for very high throughput (`Stream.ReadAsync(Memory<byte>)`, `IAsyncEnumerator<T>.MoveNextAsync`) use `ValueTask` for exactly this reason. Typical business-level APIs like FleetTrack's handlers and repositories don't need it.

---

## 6. Pitfalls a senior developer is expected to know

- **`async void`.** An exception thrown from an `async void` method can't be caught by its caller, because there is no task to carry it. It is raised on the `SynchronizationContext`, or on the thread pool if there is none, which **crashes the process**. Use `async void` only for event handlers, where the signature requires it.
- **Sync-over-async** (`.Result`, `.Wait()`, `.GetAwaiter().GetResult()`): this deadlocks under a context and causes thread-pool starvation on servers. Sometimes it can't be avoided, for example in a constructor or a legacy synchronous interface. In that case keep it at the outermost edge of the code and document it.
- **Async-over-sync** (`Task.Run(() => SyncMethod())` exposed as `SyncMethodAsync`): this pretends to be asynchronous but still uses a thread for the whole wait. Let the *caller* decide to use `Task.Run`.
- **Exceptions:** `await` rethrows the first exception directly. `.Wait()` and `.Result` wrap it in an `AggregateException`. `Task.WhenAll` collects every exception in `task.Exception`, but `await` on it only throws the first one.
- **Eliding `async`/`await`** (writing `return _repo.GetAsync(id);` instead of `return await …`): this saves one state machine. However, exceptions thrown before the task is created now escape synchronously, `using` blocks may dispose a resource before the task finishes, and the method disappears from the stack trace. Elide only in trivial pass-through methods with no `using`/`try`.
- **Fire-and-forget** (calling an async method without awaiting it): exceptions are lost, and in ASP.NET Core the request's scoped services (like `DbContext`) may be disposed while the work is still running. Use a background service with a queue instead (you'll build this kind of thing in M06).
- **Forgetting `CancellationToken`:** an async method that can't be cancelled keeps running, and using the database, after the client has gone. That's M01-T3.

---

## What to remember

1. The compiler turns an async method into a state machine; locals that survive an `await` become fields, and the method runs synchronously until its first incomplete `await`.
2. An `await` on an already-completed task continues on the same thread; on an incomplete task it returns, and the continuation runs wherever the captured context (or the thread pool) puts it.
3. `ExecutionContext` (`AsyncLocal`) always flows; `ConfigureAwait(false)` only changes *where* the continuation runs.
4. ASP.NET Core has no `SynchronizationContext`, so `ConfigureAwait(false)` doesn't matter in app code; blocking on async is still harmful because of thread-pool starvation.
5. Default to `Task`; use `ValueTask` only for measured hot paths that usually complete synchronously, and await it exactly once.

## Interview questions this prepares you for

- "Does `await` create a new thread?" (No. It frees the current thread while waiting; a continuation runs later on some thread, which may be the same one.)
- "What does `ConfigureAwait(false)` do, and do you need it in ASP.NET Core?"
- "Explain the classic `.Result` deadlock. Can it happen in ASP.NET Core? If not, why is `.Result` still bad there?"
- "When would you return `ValueTask` instead of `Task`, and what can go wrong?"
- "Why is `async void` dangerous?"
- "How does an `AsyncLocal` value survive across `await` when the thread changes?"

## For FleetTrack

- The lab goes in `labs/` (a throwaway console project), not in `src/`. Before running it, **write down your prediction** for each case: completed task, incomplete task (`Task.Delay`), `Task.Yield()`, `ConfigureAwait(false)`. Then compare. A console app has no `SynchronizationContext`, so think about whether `ConfigureAwait(false)` can change anything there.
- In SharpLab, switch to **Release** mode to see the state machine as a struct, and find: the hoisted locals, the `state` field, the `IsCompleted` check, and the `AwaitUnsafeOnCompleted` call.
- FleetTrack's handlers and EF Core calls will return `Task`, with no `ConfigureAwait(false)`. The M01-T4 telemetry parser and the M08 ingest path are where allocation-sensitive choices like `ValueTask` might earn a place, and only once a benchmark shows they help.
