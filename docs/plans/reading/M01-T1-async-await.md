# M01-T1 · How async/await really works

Task: [M01-T1](../modules/M01-csharp-runtime.md)

This file is the complete lesson for the task; you don't need to read anything else. If you ever want to go deeper, the original sources are: [How async/await really works in C# (Stephen Toub)](https://devblogs.microsoft.com/dotnet/how-async-await-really-works/) · [ConfigureAwait FAQ](https://devblogs.microsoft.com/dotnet/configureawait-faq/) · [Understanding the whys, whats and whens of ValueTask](https://devblogs.microsoft.com/dotnet/understanding-the-whys-whats-and-whens-of-valuetask/)

**Reading time:** about 60 minutes. Sections 1–6 are the core; 7–9 build on them.

## Contents

1. [Why async exists: threads are expensive, waiting is cheap](#1-why-async-exists-threads-are-expensive-waiting-is-cheap)
2. [How we got here: callbacks, then `Task`, then `async`/`await`](#2-how-we-got-here-callbacks-then-task-then-asyncawait)
3. [What a `Task` is](#3-what-a-task-is)
4. [What the compiler generates for an `async` method](#4-what-the-compiler-generates-for-an-async-method)
5. [What `await` does, step by step](#5-what-await-does-step-by-step)
6. [Which thread runs the code after `await`?](#6-which-thread-runs-the-code-after-await)
7. [`SynchronizationContext`, the classic deadlock, and `ConfigureAwait(false)`](#7-synchronizationcontext-the-classic-deadlock-and-configureawaitfalse)
8. [`Task` vs `ValueTask`](#8-task-vs-valuetask)
9. [Pitfalls a senior developer is expected to know](#9-pitfalls-a-senior-developer-is-expected-to-know)
10. [Glossary](#10-glossary)
11. [Summary, check yourself, interview questions, for FleetTrack](#11-summary)

---

## 1. Why async exists: threads are expensive, waiting is cheap

A **thread** is the operating system's unit of execution: something that runs your code, one instruction after another. .NET keeps a **thread pool**, a set of ready-made threads that run small pieces of work, so it doesn't create a new thread for every job. ASP.NET Core runs each incoming request on a thread-pool thread.

Threads are costly. Each one reserves memory for its stack (around 1 MB by default on Windows). The operating system has to schedule it, and switching between threads takes time. The pool also adds new threads **slowly** when it runs short: roughly one or two per second beyond its minimum. That's deliberate, so it doesn't overreact to short spikes.

Most of a typical web request's time, though, is spent **waiting** rather than computing: for Postgres to return rows, for another HTTP service to answer, or for a file to be read. During that wait, the CPU does nothing for this request.

**An analogy.** A restaurant has five waiters (the threads). In a *synchronous* restaurant, a waiter takes an order, walks to the kitchen and **stands there** until the food is ready. With five waiters, only five tables can be served at once, even though the waiters are mostly standing still. In an *asynchronous* restaurant, the waiter hands the order to the kitchen and goes to serve other tables. When the kitchen rings the bell, *any* free waiter brings the food out. Five waiters can now serve fifty tables.

That's exactly what `await` does. When a request awaits a database query, the thread goes back to the pool to serve other requests. When the database answers, the operating system notifies .NET, and *some* pool thread (not necessarily the same one) picks up the rest of the request.

**A worked example.** Say FleetTrack's `GET /shipments` waits 50 ms for Postgres and computes for 1 ms.
- **Synchronously,** each request holds a thread for 51 ms. With 100 threads you can serve about 100 × (1000 / 51) ≈ 2,000 requests per second, and request number 101 has to wait for a free thread.
- **Asynchronously,** each request holds a thread for about 1 ms. The same 100 threads could in theory serve about 100,000 requests per second, and at that point the database becomes the bottleneck instead.

Two consequences that interviews love:

- **Async improves scalability, not speed.** A single request isn't faster with `await`. It's usually a few microseconds *slower*, because of the bookkeeping described in section 4. What you gain is the ability to serve many more requests at the same time with the same threads.
- **Async helps I/O-bound work, not CPU-bound work.** *I/O-bound* means the time goes into waiting for input/output (network, disk, database). *CPU-bound* means the time goes into computation (resizing an image, hashing a password). Wrapping CPU work in `Task.Run` inside a web request just moves it to another pool thread, which gains nothing and adds overhead. `Task.Run` is useful in desktop apps, to keep the UI thread free so the window doesn't freeze.

---

## 2. How we got here: callbacks, then `Task`, then `async`/`await`

Knowing the history explains *why* `async`/`await` looks the way it does.

**Step 1: blocking calls.** `stream.Read(buffer)` holds the thread until the data arrives. It's simple to write, but it wastes threads (section 1).

**Step 2: callbacks.** In early .NET you'd write `stream.BeginRead(buffer, callback)` and later `stream.EndRead(result)` inside the callback. This is called the **APM** (Asynchronous Programming Model). The thread is freed, but the code becomes unreadable. Try writing "read in a loop until the end of the file, inside a `try`/`catch`, then close the file" using callbacks. The loop becomes recursion, the `try`/`catch` can't wrap the callback, and every step nests deeper. This is known as **callback hell**.

**Step 3: `Task` (.NET 4, 2010).** Instead of passing a callback *in*, the method returns an object that *represents the future result*: a `Task<int>`. You can attach a continuation to it (`task.ContinueWith(...)`), store it, combine it with others (`Task.WhenAll`), and read its exception. This is better, but you still write continuations by hand.

**Step 4: `async`/`await` (C# 5, 2012).** The compiler writes the continuations for you. You write code that *looks* sequential: loops, `try`/`catch`, `using`. The compiler splits it into pieces that run when each awaited operation finishes.

**The key idea to keep:** `async`/`await` is a *compiler feature* that builds the callback code for you on top of `Task`. Nothing magic happens at runtime that you couldn't write by hand.

---

## 3. What a `Task` is

A `Task` (or `Task<T>`) is an object that represents **an operation that will finish at some point**, plus its outcome. It's what other languages call a *promise* or a *future*. It has:

- **A status:** still running, finished successfully (`RanToCompletion`), failed with an exception (`Faulted`), or cancelled (`Canceled`).
- **A result** (for `Task<T>`), or **an exception**, once it has finished.
- **A list of continuations:** pieces of code to run when it finishes.

An important point: **a `Task` is not a thread.** A task that represents a database query has no thread "inside" it running the query. The operating system and the network card do the waiting. When the data arrives, a thread is borrowed briefly to complete the task and run its continuations.

You can see this with `Task.Delay(1000)`. It doesn't put a thread to sleep for a second. It sets a timer, and when the timer fires, the task is marked complete.

---

## 4. What the compiler generates for an `async` method

Take this method:

```csharp
async Task<int> CountShipmentsAsync(string tenant)
{
    Console.WriteLine("A: before");
    string sql = await BuildQueryAsync(tenant);   // await #1
    Console.WriteLine("B: between");
    int count = await RunQueryAsync(sql);         // await #2
    Console.WriteLine("C: after");
    return count;
}
```

The compiler turns it into two things.

### 4.1 A small stub that replaces your method

```csharp
Task<int> CountShipmentsAsync(string tenant)
{
    var sm = new CountShipmentsStateMachine();
    sm.tenant  = tenant;                                   // parameters are copied into fields
    sm.builder = AsyncTaskMethodBuilder<int>.Create();     // will produce the Task<int>
    sm.state   = -1;                                       // "not started / running"
    sm.builder.Start(ref sm);                              // runs MoveNext() right now, on THIS thread
    return sm.builder.Task;                                // hand the caller a Task<int>
}
```

### 4.2 A state machine that contains your code, cut into pieces

A **state machine** is an object that remembers "which step am I on", so it can stop and resume later. Here is a cleaned-up version of what the compiler generates. SharpLab shows the real thing, with names like `<>1__state`.

```csharp
struct CountShipmentsStateMachine : IAsyncStateMachine
{
    public int state;                              // -1 running, 0 = paused at await #1, 1 = at await #2, -2 = finished
    public AsyncTaskMethodBuilder<int> builder;
    public string tenant;                          // parameter
    private string sql;                            // local used after an await → "hoisted" into a field
    private TaskAwaiter<string> awaiter1;
    private TaskAwaiter<int> awaiter2;

    public void MoveNext()
    {
        int count;
        try
        {
            if (state == 0) goto Resume1;          // jump back to where we paused
            if (state == 1) goto Resume2;

            Console.WriteLine("A: before");
            awaiter1 = BuildQueryAsync(tenant).GetAwaiter();
            if (!awaiter1.IsCompleted)             // FAST PATH CHECK
            {
                state = 0;                          // remember where we are
                builder.AwaitUnsafeOnCompleted(ref awaiter1, ref this);  // "call MoveNext when done"
                return;                             // ← the method RETURNS here; the thread is free
            }
        Resume1:
            state = -1;
            sql = awaiter1.GetResult();            // gets the value, or rethrows the exception

            Console.WriteLine("B: between");
            awaiter2 = RunQueryAsync(sql).GetAwaiter();
            if (!awaiter2.IsCompleted)
            {
                state = 1;
                builder.AwaitUnsafeOnCompleted(ref awaiter2, ref this);
                return;
            }
        Resume2:
            state = -1;
            count = awaiter2.GetResult();

            Console.WriteLine("C: after");
        }
        catch (Exception ex)
        {
            state = -2;
            builder.SetException(ex);              // the Task becomes Faulted
            return;
        }
        state = -2;
        builder.SetResult(count);                  // the Task becomes RanToCompletion
    }
}
```

Read it slowly. Five things matter:

1. **Locals become fields.** `sql` is used after an `await`, but the method *returns* at each incomplete `await`, so its stack frame is gone. The value has to live somewhere that survives, which is a field of the state machine. This is called **hoisting**.
2. **`state` remembers the position.** Each call to `MoveNext()` jumps to the right place with the `if (state == …) goto`.
3. **Your method runs synchronously until the first incomplete `await`.** `builder.Start` calls `MoveNext()` immediately, on the caller's thread. "A: before" is printed *before* the caller even receives the `Task`.
4. **Exceptions go into the task.** The whole body is in a `try`/`catch` that calls `SetException`. So even an exception thrown on the very first line (before any `await`) does **not** throw at the call site. It's stored in the returned `Task` and rethrown when someone awaits it.
5. **It's a struct in Release builds** (and a class in Debug builds, which makes debugging easier). If every `await` completes immediately, the struct never leaves the stack and nothing extra is allocated. The first time an `await` really has to wait, the runtime copies the state machine to the heap ("boxes" it), because it now has to outlive the current call. In modern .NET, that heap object *is* the returned `Task`, so it costs one allocation, not two.

---

## 5. What `await` does, step by step

`await x` works on anything that has a `GetAwaiter()` method returning an "awaiter" with `IsCompleted`, `OnCompleted(...)` and `GetResult()`. `Task`, `ValueTask` and `Task.Yield()` all provide one. For each `await`:

1. **Get the awaiter** and check **`IsCompleted`**.
2. **Already complete → the fast path.** Call `GetResult()` and keep going **on the same thread**, with no allocation and no thread switch. This is very common: data already buffered, a cache hit, a `Task.CompletedTask` or `Task.FromResult(...)`.
3. **Not complete → the slow path.** Save `state`, register "call `MoveNext()` when you finish" with the awaiter, and **return**. The thread goes back to whoever called `MoveNext()`: the original caller the first time, or the thread pool on later resumptions.
4. **Later, when the operation completes,** `MoveNext()` is called again (on some thread, see section 6), jumps to the resume point, and calls `GetResult()`. That returns the value, or rethrows the exception the operation failed with.

Here's a timeline for the slow path, where `RunQueryAsync` waits for Postgres:

```
Thread 7 (pool)                         Postgres                 Thread 12 (pool)
──────────────                          ────────                 ────────────────
CountShipmentsAsync()
  MoveNext()
   "A: before"
   await BuildQueryAsync  → already done (fast path)
   "B: between"
   await RunQueryAsync    → NOT done
     state = 1, register continuation
     return  ─────────────►  Thread 7 is free, serves other requests
                                 … 50 ms …
                               rows arrive ─────────────────►  MoveNext()
                                                                  jump to Resume2
                                                                  "C: after"
                                                                  SetResult(count)
                                                                  → the caller's await resumes
```

Notice that "A" and "B" ran on thread 7, while "C" ran on thread 12. Also notice the **cost of one incomplete await**: one heap allocation (the first time), registering a continuation, and later scheduling it. That's a few hundred nanoseconds, which is nothing next to a 50 ms database call but noticeable in a tight loop over in-memory data.

### `Task.Yield()`

`await Task.Yield()` returns an awaiter whose `IsCompleted` is **always false**. It forces the slow path: the method returns, and the rest of it is queued to run later. You'll use it in the lab to make an "artificially asynchronous" step. In real code it's rarely needed.

---

## 6. Which thread runs the code after `await`?

When an `await` takes the slow path, it **captures** where the continuation should run, at the moment of the `await`:

| Situation at the `await` | Where the code after `await` runs |
| --- | --- |
| `SynchronizationContext.Current` is set (WPF/WinForms UI thread, classic ASP.NET), and no `ConfigureAwait(false)` | Posted back to that context, for example back onto the UI thread |
| No `SynchronizationContext`, but a custom `TaskScheduler` is current (rare) | Queued to that scheduler |
| Neither (console apps, ASP.NET Core, worker services) | On a **thread-pool thread**, often directly on the thread that completed the operation |
| The awaited task was **already complete** | Nothing is captured. The code just continues on the **same thread** |

(Section 7 explains what a `SynchronizationContext` is.)

**Rule to remember:** after an `await` that actually waits, assume you may be on a different thread. Never rely on thread identity across an `await`. Two concrete examples:
- A `[ThreadStatic]` field holds a separate value for each thread. If you set it before an `await` and read it after, you may read another thread's value.
- `lock` (`Monitor`) must be released by the thread that took it. That's why the compiler won't let you write `await` inside a `lock` block. For an async-friendly lock, use `SemaphoreSlim.WaitAsync()`.

### `ExecutionContext`: the data that always flows

If threads change, how does "the current request's data" survive? Through **`ExecutionContext`**. This is a bag of ambient values that .NET carries along the *logical* flow of an async operation, whatever thread it's on. It contains:
- `AsyncLocal<T>` values (for example a correlation id or the current tenant id),
- the current culture,
- the current `Activity`, used for distributed tracing (you'll use it in M12).

`ExecutionContext` **always** flows across `await`, and `ConfigureAwait` does **not** affect it. Don't confuse it with `SynchronizationContext`:

| | `ExecutionContext` | `SynchronizationContext` |
| --- | --- | --- |
| Answers | "*What data* travels with this logical operation?" | "*Where* (on which thread or queue) should the continuation run?" |
| Affected by `ConfigureAwait(false)`? | No | Yes, that's its only job |
| Exists in ASP.NET Core? | Yes | No |

---

## 7. `SynchronizationContext`, the classic deadlock, and `ConfigureAwait(false)`

### 7.1 What a `SynchronizationContext` is

A `SynchronizationContext` is an object with one important method, `Post(work)`, which means "run this work in *my* special place". Different app types install different ones:

- **WPF and Windows Forms:** "my place" is the single **UI thread**. Window controls may only be touched from the thread that created them, so after `await LoadDataAsync()` in a button-click handler, your code *must* come back to the UI thread to update a text box. The context makes that automatic.
- **Classic ASP.NET (.NET Framework):** "my place" is "this request's context". It restores `HttpContext.Current` and lets only one thread at a time run code for a request.
- **ASP.NET Core, console apps, worker services:** **none.** `SynchronizationContext.Current` is `null`.

### 7.2 The classic deadlock

This only happens where there *is* a single-threaded context, such as a UI app:

```csharp
// WPF button click handler (runs on the UI thread)
void OnClick(object s, EventArgs e)
{
    var data = LoadAsync().Result;   // (1) BLOCKS the UI thread until LoadAsync finishes
    textBox.Text = data;
}

async Task<string> LoadAsync()
{
    var json = await http.GetStringAsync(url);  // (2) captured the UI context
    return json.Trim();                          // (3) wants to run ON THE UI THREAD
}
```

1. The UI thread calls `LoadAsync()`, which starts the HTTP call and returns an incomplete task.
2. `.Result` **blocks** the UI thread, waiting for that task.
3. The HTTP response arrives. The continuation (`return json.Trim()`) is posted to the UI context, but the UI thread is blocked in `.Result`.
4. The task can't finish without the UI thread, and the UI thread won't move until the task finishes. **Both wait forever.**

The real fix is **"async all the way"**: make `OnClick` an `async void` event handler and write `var data = await LoadAsync();`.

### 7.3 `ConfigureAwait(false)`

`await task.ConfigureAwait(false)` means: "**don't** capture the current context; run the continuation wherever is convenient (the thread pool)". In the example above, writing `await http.GetStringAsync(url).ConfigureAwait(false)` inside `LoadAsync` lets step 3 run on a pool thread. The task completes, `.Result` returns, and there's no deadlock.

Here are the questions people actually ask about it:

**Does it guarantee the rest of the method runs off the original context?** No. If the awaited task was **already complete**, the fast path applies: nothing is captured or switched, and the method continues on the current thread, still inside the context. That's why library code puts `ConfigureAwait(false)` on **every** `await`, not just the first one.

**Does it affect `AsyncLocal` / `ExecutionContext`?** No. Those always flow (section 6).

**Do I need it in ASP.NET Core application code?** No. There's no context to capture, so it changes nothing, and most teams leave it out for readability. FleetTrack won't use it.

**So who still needs it?** **Library authors.** A NuGet package doesn't know whether it will be called from a WPF app, a classic ASP.NET app, or ASP.NET Core. Using `ConfigureAwait(false)` on every `await` keeps the library safe for all of them, and slightly faster under a context. The analyzer rule **CA2007** can enforce this in library projects.

**Is it a fix for `.Result`?** It's a band-aid, not a fix. It only helps if *every* `await` in the whole call chain uses it, including the ones inside third-party libraries you don't control. The real fix is still async all the way.

**What about `ConfigureAwait(true)`?** That's the default. Writing it changes nothing; it only documents intent.

**Anything newer?** Since .NET 8 there's `ConfigureAwait(ConfigureAwaitOptions)`. Its flags include `ForceYielding`, which always takes the slow path, like `Task.Yield`, and `SuppressThrowing`, which waits without rethrowing the task's exception and is only allowed on a non-generic `Task`. These are niche, but nice to recognise.

### 7.4 Why blocking is still bad in ASP.NET Core

Without a context, the classic deadlock above can't happen. But `.Result` / `.Wait()` still **holds a pool thread** for the entire wait. Under load, many requests block at once, and the pool runs out of threads. It only adds about one or two new threads per second, so new requests queue up, latency explodes, and the app looks "hung" even though the CPU is idle. That's **thread-pool starvation**, and you'll reproduce it and measure it in M01-T2.

---

## 8. `Task` vs `ValueTask`

### 8.1 The cost of `Task`

`Task<T>` is a **class**, so a method that returns one usually allocates it on the heap. Allocations aren't free. Each one adds work for the **garbage collector** (GC), the part of .NET that finds and frees memory nobody uses any more (covered in M01-T4). On a hot path, meaning code called millions of times per second, those allocations add up.

There are two cheap cases already handled for you:
- If an `async` method completes without ever really waiting, the runtime can reuse **cached completed tasks** for a few very common results, such as `true`/`false`, and for non-generic `Task`.
- You can cache your own completed tasks, for example `private static readonly Task<int> Zero = Task.FromResult(0);`.

### 8.2 What `ValueTask` is

`ValueTask<T>` is a **struct** that holds **one of three things**:

1. **The result itself**, when the method completed synchronously. Nothing is allocated.
2. **A `Task<T>`**, when the method really had to wait.
3. **An `IValueTaskSource<T>`**, an advanced, *reusable* object. High-performance types such as sockets keep one around and reuse it for every read, so even the asynchronous case allocates nothing.

There's also a non-generic `ValueTask`, for methods that don't return a value.

**Where it pays off:** methods that **usually complete synchronously**. A cache in front of a database is the textbook example:

```csharp
public ValueTask<Vehicle> GetVehicleAsync(Guid id)
{
    if (_cache.TryGetValue(id, out var vehicle))
        return new ValueTask<Vehicle>(vehicle);           // cache hit: no allocation at all

    return new ValueTask<Vehicle>(LoadAndCacheAsync(id)); // cache miss: wraps a normal Task
}
```

If 95 % of the calls hit the cache, 95 % of the calls allocate nothing.

### 8.3 The rules that make `ValueTask` dangerous

Because the third kind wraps a **pooled object that gets reused** after you consume it, a `ValueTask` must be treated like a one-time ticket:

```csharp
ValueTask<int> vt = stream.ReadAsync(buffer);

int a = await vt;
int b = await vt;            // ❌ awaiting twice: the object may already belong to ANOTHER read

var t1 = vt.AsTask();
var t2 = vt.AsTask();        // ❌ same problem: converting twice

int c = vt.Result;           // ❌ reading the result before it has completed is not supported

Task.WhenAll(vt1, vt2);      // ❌ doesn't compile: WhenAll takes Tasks
```

The rules:
- **Await it exactly once.**
- **Never await it from two places at the same time.**
- **Never read `.Result` (or call `.GetAwaiter().GetResult()`) before it has completed.**
- **If you need any of those** (storing it, awaiting it twice, `Task.WhenAll`), **call `.AsTask()` once** and work with the `Task`.

The analyzer rule **CA2012** ("Use ValueTasks correctly") catches many of these mistakes.

### 8.4 Industry practice: when to use which

- **Default to `Task`.** It's simpler, can be awaited any number of times, and works with `WhenAll`/`WhenAny`.
- **Use `ValueTask` only when both are true:** the method sits on a hot path where a profiler or benchmark shows the allocation matters, *and* it usually completes synchronously.
- **When not to:** methods that almost always truly wait, such as a database query or an HTTP call. There, a `ValueTask` just wraps an allocated `Task` anyway, so you gain nothing and inherit the misuse rules. FleetTrack's handlers and repositories will return `Task`.
- This is why low-level, high-throughput APIs like `Stream.ReadAsync(Memory<byte>)` and `IAsyncEnumerator<T>.MoveNextAsync()` return `ValueTask`, while ordinary business APIs return `Task`.

---

## 9. Pitfalls a senior developer is expected to know

**`async void`.** A method declared `async void` returns no task, so its caller can't await it or catch its exceptions. An exception inside it is raised directly on the `SynchronizationContext`, or on the thread pool if there is none, and that **crashes the process**. Use `async void` only for event handlers, where the signature requires `void`.

**Sync-over-async** (`.Result`, `.Wait()`, `.GetAwaiter().GetResult()`). This deadlocks under a single-threaded context (section 7.2) and starves the thread pool on servers (section 7.4). If a synchronous API truly can't be changed (a constructor, or an old interface you must implement), block at the outermost point possible and leave a comment explaining why. Of the three, `.GetAwaiter().GetResult()` is preferred because it throws the original exception instead of an `AggregateException`.

**Async-over-sync** (`public Task<int> ComputeAsync() => Task.Run(() => Compute());`). This *pretends* to be asynchronous but still uses a thread for the whole operation. Callers think it's cheap I/O, but it's CPU work on someone else's thread. Expose the synchronous method, and let the *caller* decide whether to use `Task.Run`.

**Exceptions.** `await` rethrows the original exception. `.Wait()` and `.Result` wrap it in an `AggregateException`. `await Task.WhenAll(a, b)` throws only the **first** exception. All of them are in `whenAllTask.Exception.InnerExceptions` if you need them.

**Eliding `async`/`await`**, meaning writing `Task<Vehicle> GetAsync(Guid id) => _repo.GetAsync(id);` instead of `async Task<Vehicle> GetAsync(Guid id) => await _repo.GetAsync(id);`. This saves one state machine. However:
- Exceptions thrown *before* the inner task is created now throw at the call site, instead of being stored in the task.
- Inside a `using` block, the resource is disposed as soon as the method returns, **before** the task finishes:

  ```csharp
  Task<string> ReadAsync()
  {
      using var reader = new StreamReader("file.txt");
      return reader.ReadToEndAsync();   // ❌ reader is disposed while the read is still running
  }
  ```
- The method disappears from stack traces, which makes debugging harder.

Elide only in trivial one-line pass-throughs with no `using` and no `try`.

**Fire-and-forget** (calling an async method without awaiting it: `_ = SendEmailAsync();`). Any exception is silently lost. In ASP.NET Core, the request's scoped services (such as a `DbContext`) may be disposed while the work is still running, which causes random `ObjectDisposedException`s. For background work, use a hosted background service with a queue. Reliable messaging is M06.

**Forgetting `CancellationToken`.** If the client disconnects, an async method that can't be cancelled keeps running and keeps the database busy for nothing. That's M01-T3.

**`async` without `await`.** The compiler warns (CS1998), and the method runs fully synchronously while still paying for a state machine. Either add the real `await`, or remove `async` and return `Task.FromResult(...)` / `Task.CompletedTask`.

---

## 10. Glossary

| Term | Meaning |
| --- | --- |
| **Thread** | The operating system's unit of execution; runs code one instruction after another |
| **Thread pool** | .NET's shared set of reusable threads for short pieces of work; ASP.NET Core runs requests on it |
| **I/O-bound / CPU-bound** | Time spent waiting for input/output (network, disk, DB) vs time spent computing |
| **`Task` / `Task<T>`** | An object representing an operation that will finish later, with its result or exception (a "promise") |
| **Continuation** | The code that runs after an awaited operation finishes |
| **State machine** | The compiler-generated type that holds an async method's position (`state`) and its hoisted locals |
| **Hoisting** | Moving a local variable into a field of the state machine so it survives across an `await` |
| **Awaiter** | The object `GetAwaiter()` returns; it has `IsCompleted`, `OnCompleted` and `GetResult` |
| **Fast path / slow path** | `await` on a completed task (continue immediately) vs on an incomplete one (return now, resume later) |
| **Boxing (of the state machine)** | Copying the struct state machine to the heap the first time it has to outlive the call |
| **`SynchronizationContext`** | Decides *where* continuations run (for example the UI thread); none in ASP.NET Core |
| **`ExecutionContext`** | Ambient data (`AsyncLocal`, culture, tracing `Activity`) that always flows across `await` |
| **`ConfigureAwait(false)`** | "Don't capture the context for this await"; matters for libraries and UI apps |
| **Sync-over-async** | Blocking on async code (`.Result`, `.Wait()`); causes deadlocks or thread-pool starvation |
| **Thread-pool starvation** | All pool threads are blocked, so new work queues up while the pool grows slowly |
| **`ValueTask<T>`** | A struct that avoids allocating when a method completes synchronously; await it only once |
| **`IValueTaskSource<T>`** | A reusable object behind a `ValueTask`, used by high-performance types such as sockets |
| **GC (garbage collector)** | The runtime component that frees memory nothing refers to any more (M01-T4) |

---

## 11. Summary

1. Async frees threads while waiting. That's a scalability win for I/O-bound work, not a speed-up and not a tool for CPU-bound work.
2. The compiler turns an `async` method into a state machine. Locals that survive an `await` become fields, the method runs synchronously until its first *incomplete* `await`, and exceptions are stored in the returned task.
3. `await` on a completed task continues on the same thread with no cost. On an incomplete task, the method returns, and the continuation runs later wherever the captured context, or the thread pool, puts it.
4. `ExecutionContext` (and so `AsyncLocal`) always flows. `ConfigureAwait(false)` only changes *where* the continuation runs, and matters for libraries and UI apps, not ASP.NET Core app code.
5. Blocking on async deadlocks under a single-threaded context and starves the thread pool on servers. Go async all the way.
6. Default to `Task`. Use `ValueTask` only on measured hot paths that usually complete synchronously, and await it exactly once.

### Check yourself

Try to answer these without looking back. The answers are in the sections given.

1. A request spends 2 ms computing and 100 ms waiting for an external API. What does making it async improve, and what does it *not* improve? (§1)
2. Where does the variable `sql` from §4 live while the method is paused, and why can't it stay on the stack? (§4)
3. An async method throws `ArgumentNullException` on its first line, before any `await`. The caller does `var t = DoAsync(null);` and doesn't await. Is anything thrown on that line? (§4)
4. Why does the thread id *sometimes* stay the same after an `await` and sometimes change? (§5, §6)
5. You store a tenant id in an `AsyncLocal<string>` and then `await` with `ConfigureAwait(false)`. Is the value still there afterwards? (§6)
6. Explain the `.Result` deadlock in a WPF app in four steps. Why can't it happen in ASP.NET Core, and why is `.Result` still bad there? (§7)
7. Give one method where `ValueTask` is a good choice and one where it's pointless. (§8)
8. What's wrong with returning `reader.ReadToEndAsync()` from inside a `using` block without awaiting it? (§9)

**For the lab, predict before you run.** In a console app, what will the thread id be before and after each of these: `await Task.CompletedTask`, `await Task.Delay(100)`, `await Task.Yield()`, and `await Task.Delay(100).ConfigureAwait(false)`? Write down your predictions with a reason for each, then run the code and compare.

### Interview questions this prepares you for

- "Does `await` create a new thread?"
- "What does `ConfigureAwait(false)` do, and do you need it in ASP.NET Core?"
- "Explain the classic `.Result` deadlock. Can it happen in ASP.NET Core? If not, why is `.Result` still bad there?"
- "When would you return `ValueTask` instead of `Task`, and what can go wrong?"
- "Why is `async void` dangerous?"
- "How does an `AsyncLocal` value survive an `await` when the thread changes?"
- "Our API's CPU is at 10 % but requests time out under load. What would you suspect first?" (Sync-over-async causing thread-pool starvation; preview of M01-T2.)

### For FleetTrack

- **The lab** goes in `labs/`, a throwaway console project outside FleetTrack's architecture. Log `Environment.CurrentManagedThreadId` around each `await` from the prediction exercise above.
- **SharpLab** ([sharplab.io](https://sharplab.io)): paste a small async method, choose "C#" output and **Release** mode, and find the four things from §4: the hoisted locals, the `state` field, the `IsCompleted` check, and the `AwaitUnsafeOnCompleted` call followed by `return`. Annotate them in your journal note.
- **The ✅ journal note** must explain, in your own words, *when* the code after `await` runs on a different thread and *why*. Use the vocabulary from §5–§6: fast path, slow path, captured context, thread pool.
- **In FleetTrack itself:** handlers and EF Core calls return `Task`, with no `ConfigureAwait(false)`, no `.Result`, no `async void`, and they always accept a `CancellationToken`. The M01-T4 parser and the M08 telemetry ingest path are where `ValueTask`-style allocation choices might earn a place, and only once a benchmark shows they help.
