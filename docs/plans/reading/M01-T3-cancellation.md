# M01-T3 · Cancellation end to end

Task: [M01-T3](../modules/M01-csharp-runtime.md)

This file is the complete lesson for the task; you don't need to read anything else. Original sources, if you ever want to go deeper: [Cancellation in managed threads](https://learn.microsoft.com/dotnet/standard/threading/cancellation-in-managed-threads) · [Recommended patterns for CancellationToken (Stephen Cleary)](https://devblogs.microsoft.com/premier-developer/recommended-patterns-for-cancellationtoken/)

**Reading time:** about 45 minutes. **Builds on:** [M01-T1](M01-T1-async-await.md) (tasks, exceptions in tasks) and [M01-T2](M01-T2-threadpool-starvation.md) (wasted work under load).

## Contents

1. [Why cancellation matters](#1-why-cancellation-matters)
2. [The model: cooperative cancellation](#2-the-model-cooperative-cancellation)
3. [The API: `CancellationTokenSource` and `CancellationToken`](#3-the-api-cancellationtokensource-and-cancellationtoken)
4. [How a cancelled operation ends: `OperationCanceledException`](#4-how-a-cancelled-operation-ends-operationcanceledexception)
5. [Timeouts and linked tokens](#5-timeouts-and-linked-tokens)
6. [Recommended patterns (Cleary)](#6-recommended-patterns-cleary)
7. [Cancellation in ASP.NET Core](#7-cancellation-in-aspnet-core)
8. [Cancellation all the way down to Postgres](#8-cancellation-all-the-way-down-to-postgres)
9. [When *not* to cancel](#9-when-not-to-cancel)
10. [Pitfalls](#10-pitfalls)
11. [Glossary](#11-glossary)
12. [Summary, check yourself, interview questions, for FleetTrack](#12-summary)

---

## 1. Why cancellation matters

A user opens FleetTrack's shipment report, which runs a 10-second query. After 2 seconds they lose patience and close the tab. **Without cancellation**, the server doesn't care: it keeps the query running in Postgres for the remaining 8 seconds, holds a database connection from the pool, maybe holds row locks, and then builds a response that nobody will ever read.

One abandoned request is harmless. Under load it isn't:
- **Users retry.** An impatient user who refreshes three times creates three expensive queries, and two of them are pure waste.
- **Resources are finite.** The database connection pool (default 100 connections in Npgsql), Postgres CPU and I/O are shared by every request. Abandoned work starves real work, much like the blocked threads starved real requests in T2.
- **Cascading slowness.** A slow downstream service makes callers time out and retry, which adds more load to the slow service. Cancelling abandoned work is one of the tools that stops this spiral.

**Cancellation** lets the code that *started* an operation tell the code *doing* it: "stop, nobody needs the result any more". **Timeouts** are the same mechanism triggered by a clock instead of a person.

---

## 2. The model: cooperative cancellation

.NET cancellation is **cooperative**. Nobody can forcibly stop a running piece of code. The requester only **signals** a request, and the running code **checks** for that signal at safe points and stops itself.

Why not just kill the thread? .NET used to have `Thread.Abort()`, which injected an exception at a random point in the target thread. That could happen halfway through updating a data structure, inside a `finally` block or while holding a lock, leaving the program in a corrupt state. It was so dangerous that .NET Core removed it entirely: calling it throws `PlatformNotSupportedException`. Cooperative cancellation means **the code being cancelled decides where it's safe to stop**.

The design separates two roles:

```
   the requester                                  the worker(s)
   ─────────────                                  ─────────────
   CancellationTokenSource  ──── .Token ────►  CancellationToken
   • can Cancel()                                • can only OBSERVE: "has cancellation been requested?"
   • can CancelAfter(timeout)                    • passed down to every method that does the work
   • owns the lifetime (Dispose)                 • a cheap struct, safe to copy and share
```

**Why two types?** The **ability to cancel** and the **ability to observe** are different permissions. A method that receives a `CancellationToken` can react to cancellation but can't *cause* it. If methods received the source, any method deep in a library could cancel the whole operation for everyone. It's the same principle as least privilege.

One source can hand its token to many workers: the endpoint, the service, the database call and an HTTP call can all observe the **same** token, so one `Cancel()` stops them all.

---

## 3. The API: `CancellationTokenSource` and `CancellationToken`

### 3.1 The source (the requester's side)

```csharp
using var cts = new CancellationTokenSource();
CancellationToken token = cts.Token;

cts.Cancel();                              // request cancellation now
cts.CancelAfter(TimeSpan.FromSeconds(2));  // request it automatically after 2 s (a timeout)
```

- **`Cancel()`** sets the token to "cancelled" and runs all callbacks registered on it (§3.3). Calling it twice is harmless.
- **`CancelAfter(t)`** starts an internal **timer**. That's why the source implements `IDisposable`: always dispose it (`using`), or the timer and its registrations live longer than they should.
- **`new CancellationTokenSource(TimeSpan)`** is a shortcut for "create, then `CancelAfter`".
- **`TryReset()`** makes a source reusable *if* it hasn't been cancelled yet. It's an optimisation for hot loops; normally you just create a new source.

### 3.2 The token (the worker's side)

| Member | Meaning |
| --- | --- |
| `IsCancellationRequested` | `true` once someone called `Cancel()` (or the timeout fired) |
| `ThrowIfCancellationRequested()` | Throws `OperationCanceledException` if cancellation was requested; the standard way to stop |
| `Register(callback)` | Runs a callback when cancellation is requested (§3.3) |
| `CanBeCanceled` | `false` for `CancellationToken.None`, so code can skip cancellation work entirely |
| `CancellationToken.None` (= `default`) | A token that will **never** be cancelled |

The token is a small **struct**, so copying it and passing it everywhere costs almost nothing.

### 3.3 Three ways to observe cancellation

**1. Polling**, for your own loops and CPU work:

```csharp
foreach (var position in positions)
{
    ct.ThrowIfCancellationRequested();     // stop between items, never halfway through one
    Process(position);
}
```

**2. Passing it on**, the most common case. Most async APIs accept a token and do the observing for you: `Task.Delay(1000, ct)`, `stream.ReadAsync(buffer, ct)`, `httpClient.GetAsync(url, ct)`, EF Core's `ToListAsync(ct)`, Npgsql's `ExecuteScalarAsync(ct)`.

**3. Registering a callback**, to wire cancellation into something that doesn't understand tokens:

```csharp
using CancellationTokenRegistration reg = ct.Register(() => legacyClient.Abort());
await legacyClient.DoWorkAsync();
```

Dispose the registration (`using`) when the work is done. Otherwise the callback stays attached to a long-lived token, which keeps the object it references alive in memory.

---

## 4. How a cancelled operation ends: `OperationCanceledException`

**The convention:** a method that stops because of cancellation **throws `OperationCanceledException`** (OCE). It doesn't return `null`, an empty list or `false`. Returning a normal-looking value would make the caller believe the work finished.

- `TaskCanceledException` derives from `OperationCanceledException`, and some APIs (like `Task.Delay` and `HttpClient`) throw it. **Always catch the base type, `OperationCanceledException`**, so both are handled.
- An `async` method that throws OCE produces a task in the **`Canceled`** state rather than `Faulted`. `await` rethrows the OCE to the caller.

**Catching it properly:**

```csharp
try
{
    await service.RunReportAsync(ct);
}
catch (OperationCanceledException) when (ct.IsCancellationRequested)
{
    // cancelled because WE asked for it: expected, not an error. Log at Information/Debug, don't alert.
}
```

The `when (ct.IsCancellationRequested)` filter matters. An OCE can also come from **somewhere else**, for example a timeout inside a library that you never requested. Without the filter, you'd silently treat a real failure as "the user cancelled".

**Never swallow it accidentally:**

```csharp
catch (Exception ex)          // ❌ also catches OperationCanceledException
{
    logger.LogError(ex, "Report failed");   // every cancelled request now looks like an error and pages someone
    return Results.Problem();
}
```

Either catch OCE separately *before* the general `catch`, or add `when (ex is not OperationCanceledException)`.

---

## 5. Timeouts and linked tokens

### 5.1 A timeout is just cancellation on a timer

```csharp
using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(2));
await repository.RunReportAsync(timeout.Token);    // cancelled after 2 s if not finished
```

### 5.2 Linked tokens: "whichever comes first"

Usually you want **both**: stop if the client goes away, **and** stop after 2 seconds anyway. `CreateLinkedTokenSource` creates a source that is cancelled when **any** of its parent tokens is cancelled, **or** when you cancel it yourself (including by its own timer):

```csharp
public async Task<int> RunReportAsync(CancellationToken requestAborted)
{
    using var linked = CancellationTokenSource.CreateLinkedTokenSource(requestAborted);
    linked.CancelAfter(TimeSpan.FromSeconds(2));            // our own deadline on top

    try
    {
        return await repository.QueryAsync(linked.Token);   // pass the LINKED token down
    }
    catch (OperationCanceledException) when (!requestAborted.IsCancellationRequested)
    {
        // the client is still there, so it was OUR 2-second timeout that fired
        throw new TimeoutException("Report query exceeded 2 s.");
    }
}
```

```
 requestAborted ─────┐
                     ├──► linked token ──► repository ──► Npgsql ──► Postgres
 CancelAfter(2 s) ───┘     (fires on whichever happens first)
```

**Telling the two causes apart** matters, because they mean different things:
- **The client went away** (`requestAborted` is cancelled): nobody is listening, so just stop. It's not an error.
- **Your timeout fired** (`requestAborted` is not cancelled): the client is still waiting, and *you* gave up. That **is** a problem worth logging and measuring, and the client should get an error response, typically **504 Gateway Timeout** when a downstream dependency was too slow, or **503 Service Unavailable**.

Dispose the linked source too (`using`): it holds registrations on its parent tokens.

### 5.3 Deadlines across layers

When several layers each have a timeout, they have to fit together. For example, an HTTP handler with a 2 s budget calling a service with a 5 s timeout means the inner timeout can never fire. Pass the **remaining** budget down (a **deadline**) rather than giving every layer its own fixed timeout. gRPC (M09) makes this explicit with deadlines that propagate automatically across services. FleetTrack's architecture rule 3, "synchronous calls need a deadline", comes from here.

---

## 6. Recommended patterns (Cleary)

Stephen Cleary's widely cited guidance, with the reason for each rule:

1. **Accept a `CancellationToken` in every async method that does I/O or can take a while, and make it the last parameter.** It's a consistent convention, and callers always know where to look.
2. **Pass it on to every async call that accepts one.** A token that stops at your method protects nothing below it. The analyzer rule **CA2016** ("forward the `CancellationToken` parameter") flags calls where you forgot. In FleetTrack it is a **build error**: `AnalysisLevel=latest-recommended` reports it as a warning, and warnings-as-errors does the rest. (Verified in the M01-T3 lab: removing the token from `ExecuteReaderAsync` failed the build; the probe had to pass `CancellationToken.None` explicitly.)
3. **Default value only on public API boundaries.** Public library methods often use `CancellationToken cancellationToken = default` for convenience. Internal methods should *require* it, so nobody can forget to pass it.
4. **Respond by throwing `OperationCanceledException`**, usually via `ThrowIfCancellationRequested()` or by letting an inner call throw it. Don't return partial or fake results.
5. **Check at sensible points.** Before starting expensive work and between items in a loop, not after every line. Checking is cheap, but scattering it everywhere hurts readability.
6. **Cancellation is a request, not a guarantee.** The work may already be finished, or past a point where it can't stop. Callers must handle both outcomes: a result, or an OCE.
7. **If you create a `CancellationTokenSource`, you own it:** dispose it, and don't expose it to callees.

---

## 7. Cancellation in ASP.NET Core

### 7.1 `RequestAborted`

Every request has a token, **`HttpContext.RequestAborted`**, which is cancelled when the **client disconnects**: the user closes the tab, the HTTP client times out, or a proxy cuts the connection. In minimal APIs and controllers, just declare a `CancellationToken` parameter, and ASP.NET Core binds `RequestAborted` to it:

```csharp
app.MapGet("/report", async (ReportService service, CancellationToken ct) =>
    await service.RunReportAsync(ct));            // ct == HttpContext.RequestAborted
```

The server notices a disconnect when it sees the connection close. For HTTP/1.1 that happens when the socket closes or is reset. For HTTP/2, the client sends a reset for that one stream, so other requests on the same connection carry on.

### 7.2 What happens when the OCE reaches the framework

If your endpoint lets an `OperationCanceledException` escape **after the request was aborted**, ASP.NET Core recognises the situation: the client is gone, so no response can be sent anyway. Recent versions log it quietly at a low level, not as an unhandled error, and the request metrics record it as aborted rather than as a server failure. If an OCE escapes while the client is **still connected** (your own timeout, §5.2), it's treated as a real failure. Map timeouts to a proper response yourself.

### 7.3 Built-in request timeouts (for later)

ASP.NET Core has a request-timeouts middleware (`AddRequestTimeouts()` / `UseRequestTimeouts()`, with `.WithRequestTimeout(...)` per endpoint). It cancels `RequestAborted` after a configured time and returns a 504. It's the framework version of what you build by hand in this task. Following FleetTrack's "build it by hand first" rule, use the linked token here, then compare the two later. Note that the middleware doesn't apply its timeouts while a debugger is attached.

### 7.4 Background services

A `BackgroundService` receives a **`stoppingToken`** in `ExecuteAsync`, which is cancelled when the application shuts down (for example during a deployment). Pass it to everything, so shutdown is quick and clean instead of being killed after the host's shutdown timeout. You'll use this in M06.

---

## 8. Cancellation all the way down to Postgres

Cancelling the C# side isn't enough if the **database** keeps running the query. That's the real point of this task: proving the cancellation reaches Postgres.

### 8.1 How Npgsql cancels a query

```csharp
await using var conn = await dataSource.OpenConnectionAsync(ct);
await using var cmd = new NpgsqlCommand("SELECT pg_sleep(10)", conn);
await cmd.ExecuteNonQueryAsync(ct);
```

When `ct` is cancelled while the query runs:
1. Npgsql sends Postgres a **cancel request** on a **separate, short-lived connection**. (PostgreSQL's protocol can't interrupt a busy connection through itself, so cancellation always arrives "from the side".)
2. Postgres interrupts the running query, which fails with error code **`57014`** (`query_canceled`: "canceling statement due to user request").
3. Npgsql turns that into an **`OperationCanceledException`**, with the `PostgresException` (57014) as its inner exception, so your code sees the standard cancellation exception.
4. The connection stays usable and goes back to the pool.

If the cancel request itself doesn't get an answer within a short time (the connection string's **`Cancellation Timeout`**), Npgsql gives up on the connection and breaks it, so your code isn't stuck waiting forever.

**Don't confuse this with `CommandTimeout`** (default 30 s). That's Npgsql's own per-command timeout. When it fires, you get an `NpgsqlException` wrapping a `TimeoutException`, not an OCE. The two are separate mechanisms: the token is *your* signal, and the command timeout is a safety net.

### 8.2 Proving it with `pg_stat_activity`

**`pg_stat_activity`** is a Postgres system view with one row per server connection, showing what each one is doing right now:

```sql
SELECT pid, state, wait_event, now() - query_start AS running_for, query
FROM pg_stat_activity
WHERE query LIKE '%pg_sleep%' AND pid <> pg_backend_pid();   -- exclude this very query
```

- **While the query runs:** a row with `state = active`, `wait_event = PgSleep` and a growing `running_for`.
- **After a working cancellation:** the row's state becomes `idle` (the connection is back in the pool, waiting), and `query` shows the *last* statement it ran. There's no longer an `active` `pg_sleep`.
- **If cancellation doesn't reach the database:** the row stays `active` for the full 10 seconds after the client has gone. That's exactly the bug this task is about.

Run it in `psql` inside the container: `docker compose -f deploy/compose.yaml exec postgres psql -U <user> -d <db>`.

### 8.3 Proving it with a test

An automated version of the same proof, using what you built in M00-T6 (Testcontainers):
1. Start a Postgres container.
2. Start the `pg_sleep(10)` query with a token, and cancel the source after ~500 ms.
3. Assert that an `OperationCanceledException` arrives **quickly** (well under 10 s, say under 2 s).
4. On a **second** connection, query `pg_stat_activity` and assert there's no `active` row running `pg_sleep`.

Step 4 is what makes it a real proof. Step 3 alone only proves *C#* stopped waiting, and the database could still be working.

To test the *HTTP* path end to end (client disconnects → `RequestAborted` → query cancelled), cancel the `HttpClient` call's token in a `WebApplicationFactory` test, then check `pg_stat_activity` the same way. Whether the in-memory test server passes the client's cancellation through to `RequestAborted` is worth verifying for yourself; a test you've never seen fail proves nothing (M00-T6 §8).

---

## 9. When *not* to cancel

Cancellation is for work whose result **nobody needs any more**. Some work must finish even when the client leaves:

- **After the point of no return.** Once you've charged a credit card, cancelling before saving the payment record leaves the system inconsistent: money taken, no record of it. Do the side-effecting part and its bookkeeping **without** the request token (pass `CancellationToken.None`), or make the whole thing idempotent and retryable (FleetTrack rule 5, the outbox in M06).
- **Database commits.** Cancelling during `SaveChangesAsync` means the transaction may or may not have committed by the time the cancel arrives. The caller can't tell which. Design writes so that either outcome is safe.
- **Cleanup and compensation.** Rolling back, releasing a lock, or writing a "failed" status in a `catch`/`finally` must not use the token that was just cancelled, or the cleanup is cancelled too. Use `CancellationToken.None`, or a fresh short timeout.
- **Cheap work.** A method that returns cached data in microseconds gains nothing from a token. Don't add parameters that never matter.

---

## 10. Pitfalls

- **Accepting a token but not passing it on.** The method *looks* cancellable but isn't. CA2016 catches the obvious cases; review the rest.
- **`Task.Run(work, ct)` doesn't cancel running work.** The token there only prevents the work from **starting** if it's already cancelled. The work itself must observe the token.
- **Catching `Exception` and swallowing OCE**, or logging every cancellation as an error (§4). Cancellations are normal traffic; alerting on them causes alert fatigue.
- **Not disposing `CancellationTokenSource`** (especially with `CancelAfter` or `CreateLinkedTokenSource`): timers and registrations leak.
- **Checking `IsCancellationRequested` and returning normally.** The caller can't tell "finished" from "stopped early". Throw OCE instead.
- **Cancelling cleanup with the same token** (§9).
- **Timeouts that don't nest** (§5.3): an inner timeout longer than the outer one never fires.
- **Forgetting the database.** Cancelling C# code while the SQL keeps running (§8) frees your thread, but not Postgres.

---

## 11. Glossary

| Term | Meaning |
| --- | --- |
| **Cooperative cancellation** | The requester signals; the running code checks and stops itself at a safe point |
| **`CancellationTokenSource` (CTS)** | The object that can request cancellation (`Cancel`, `CancelAfter`); owned and disposed by the requester |
| **`CancellationToken`** | A cheap struct that can only observe cancellation; passed to the workers |
| **`OperationCanceledException` (OCE)** | The exception that signals "stopped because of cancellation"; `TaskCanceledException` derives from it |
| **Linked token source** | A CTS cancelled when any parent token is cancelled, or by its own timer |
| **`RequestAborted`** | ASP.NET Core's per-request token, cancelled when the client disconnects |
| **`stoppingToken`** | A background service's token, cancelled at application shutdown |
| **Deadline** | An absolute time by which the whole operation must finish, passed across layers |
| **Cancel request (Postgres)** | A message sent on a separate connection that asks the server to interrupt a running query |
| **`57014 query_canceled`** | The Postgres error code for a query cancelled at the client's request |
| **`pg_stat_activity`** | A Postgres view showing every connection and what it's doing right now |
| **CA2016** | The analyzer rule that flags a token not being forwarded |
| **Point of no return** | The moment after which stopping would leave data inconsistent |

---

## 12. Summary

1. Cancellation stops abandoned work, so it doesn't waste threads, connections and database time. Under load that's the difference between a slowdown and a spiral.
2. It's cooperative: the source requests, the token observes, and the code being cancelled decides where to stop. The two types separate the right to cancel from the right to observe.
3. Cancelled operations throw `OperationCanceledException`. Catch the base type, use a `when` filter, and never let a general `catch (Exception)` swallow it or log it as an error.
4. A linked token combines "the client went away" with "my own timeout". Tell them apart, because one is normal and the other is a failure.
5. Accept, forward and respect tokens all the way down (CA2016), including to the database. Npgsql turns cancellation into a Postgres cancel request, and `pg_stat_activity` proves it worked.
6. Don't cancel past the point of no return, and don't cancel cleanup with the token that was just cancelled.

### Check yourself

1. Why does .NET use cooperative cancellation instead of killing a thread? (§2)
2. Why are `CancellationTokenSource` and `CancellationToken` separate types? (§2)
3. A method checks `if (ct.IsCancellationRequested) return null;`. What's wrong with that? (§4, §10)
4. Your code catches `OperationCanceledException`. How do you tell "the client disconnected" from "my 2-second timeout fired", and why does it matter? (§5.2)
5. The C# side throws OCE after 500 ms. Does that prove the Postgres query stopped? What does? (§8)
6. What's the difference between cancelling a token and Npgsql's `CommandTimeout`? (§8.1)
7. Give one operation in FleetTrack that should *not* use the request's token, and say what to pass instead. (§9)

**Before you build: predict.** For the endpoint → service → `SELECT pg_sleep(10)` chain, answer these:
- You abort the request with Ctrl+C on `curl` after 2 s. Where in your code does the cancellation surface first, and what exception type does each layer see?
- What will `pg_stat_activity` show 1 second after the abort?
- With the 2-second linked timeout, and a client that **doesn't** disconnect: what does the client receive, and what should it receive?

Post your predictions here before running anything.

### Interview questions this prepares you for

- "How does cancellation work in .NET, and why is it cooperative?"
- "A user closes the browser during a slow report. What happens on your server, all the way to the database?"
- "How do you implement a timeout that also respects client disconnects?"
- "Your logs are full of `TaskCanceledException` errors. Is that a problem? What would you change?"
- "When would you deliberately *not* pass the request's cancellation token?"
- "How do you make sure deadlines are consistent across several services?"

### For FleetTrack

- **The lab** goes in `labs/M01-T3-cancellation`: a minimal API endpoint → a service class → Npgsql running `SELECT pg_sleep(10)` against the Compose Postgres, passing the request's token all the way down. Log at each layer where the OCE surfaces, then add the linked 2-second timeout and map it to a 504.
- **The ✅ check** is either the Testcontainers test from §8.3, or a documented manual check: `curl`, Ctrl+C, then `pg_stat_activity`, written up in a journal note.
- **In FleetTrack itself:** every endpoint, handler and repository method accepts and forwards a `CancellationToken` (CA2016 already fails the build when a token isn't forwarded). Writes and outbox processing (M06) deliberately decide where cancellation stops being allowed. Synchronous calls to the Tracking service (M09) carry a deadline.
