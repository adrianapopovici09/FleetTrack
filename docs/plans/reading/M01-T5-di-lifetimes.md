# M01-T5 · DI lifetimes and captive dependencies

Task: [M01-T5](../modules/M01-csharp-runtime.md)

This file is the complete lesson for the task; you don't need to read anything else. Original sources, if you ever want to go deeper: [Service lifetimes](https://learn.microsoft.com/dotnet/core/extensions/dependency-injection#service-lifetimes) · [DI guidelines: captive dependencies and scope validation](https://learn.microsoft.com/dotnet/core/extensions/dependency-injection-guidelines) · [Keyed services](https://learn.microsoft.com/dotnet/core/extensions/dependency-injection#keyed-services)

**Reading time:** about 45 minutes for §1–§10. The **appendix** (§A1–§A2) covers two async topics from the M01 quiz (Q8 and Q9) that no other task teaches; add about 20 minutes.

## Contents

1. [What dependency injection is, in one page](#1-what-dependency-injection-is-in-one-page)
2. [Registering and resolving](#2-registering-and-resolving)
3. [The three lifetimes](#3-the-three-lifetimes)
4. [What a scope is in ASP.NET Core](#4-what-a-scope-is-in-aspnet-core)
5. [Captive dependencies](#5-captive-dependencies)
6. [Scope validation: catching it at startup](#6-scope-validation-catching-it-at-startup)
7. [The fix: `IServiceScopeFactory`](#7-the-fix-iservicescopefactory)
8. [Disposal: who disposes what](#8-disposal-who-disposes-what)
9. [More registration tools: factories, multiple implementations, keyed services](#9-more-registration-tools-factories-multiple-implementations-keyed-services)
10. [Choosing a lifetime, and anti-patterns](#10-choosing-a-lifetime-and-anti-patterns)
11. [Glossary](#11-glossary)
12. [Summary, check yourself, interview questions, for FleetTrack](#12-summary)
- [Appendix A1: bounding concurrency (`Task.WhenAll` over 10,000 calls)](#a1-bounding-concurrency-taskwhenall-over-10000-calls)
- [Appendix A2: `IAsyncEnumerable<T>` vs `Task<List<T>>`](#a2-iasyncenumerablet-vs-tasklistt)

---

## 1. What dependency injection is, in one page

A class usually needs other objects to do its work: a repository, a clock, a logger. There are two ways to get them:

```csharp
// Without DI: the class builds its own dependencies
public class DispatchService
{
    private readonly ShipmentRepository _repo = new ShipmentRepository(new NpgsqlDataSource(...));
}

// With DI: the class declares what it needs, and someone else provides it
public class DispatchService(IShipmentRepository repo, TimeProvider clock) { … }
```

With **dependency injection (DI)**, a class asks for its dependencies through its **constructor** (M00-T2's "explicit dependencies" principle), and a **container** creates the objects and passes them in. The class no longer knows *which* implementation it gets or *how* it's built. The benefits:
- **Testability:** a test passes a fake repository instead of a real one.
- **Configuration in one place:** the **composition root** (`Program.cs`) decides which implementation goes with which interface.
- **Lifetime management:** the container decides **how long each object lives and when it's disposed**. That's the topic of this lesson, and the source of some of the nastiest production bugs in .NET.

.NET's built-in container is `Microsoft.Extensions.DependencyInjection`. ASP.NET Core, worker services and console apps using the generic host all use it.

---

## 2. Registering and resolving

**Registration** happens before `builder.Build()` (M00-T5 §5): you describe services, but nothing is created yet.

```csharp
builder.Services.AddSingleton<IClock, SystemClock>();          // interface → implementation
builder.Services.AddScoped<IShipmentRepository, EfShipmentRepository>();
builder.Services.AddTransient<PriceCalculator>();               // a concrete class, no interface
```

**Resolving** happens at run time. The container looks at a class's constructor, resolves each parameter (recursively), and calls the constructor. You rarely call the container yourself: ASP.NET Core resolves your endpoint's parameters, your controllers and your hosted services for you.

The container is an `IServiceProvider`. When you do need it directly, use `GetRequiredService<T>()`, which throws a clear error if `T` isn't registered, rather than `GetService<T>()`, which returns `null` and fails later with a confusing `NullReferenceException`.

---

## 3. The three lifetimes

| Lifetime | One instance per… | Created | Typical use |
| --- | --- | --- | --- |
| **Singleton** | **application** | The first time it's requested; then the same instance forever | Stateless or thread-safe shared services: `TimeProvider`, `NpgsqlDataSource` (owns the connection pool), `IHttpClientFactory`, caches, `IOptions<T>` |
| **Scoped** | **scope** (in ASP.NET Core: per HTTP request) | The first time it's requested within a scope | Anything holding per-request state: EF Core's `DbContext`, a unit of work, "the current user/tenant" |
| **Transient** | **request to the container** | Every time it's injected or resolved | Lightweight, stateless helpers |

A worked example. Inside **one** HTTP request, two classes both take `IClock` (singleton), `AppDbContext` (scoped) and `PriceCalculator` (transient):

```
Request 1:  HandlerA ──► clock#1, db#7,  calc#21
            HandlerB ──► clock#1, db#7,  calc#22     ← same DbContext within the request, new calculator
Request 2:  HandlerA ──► clock#1, db#8,  calc#23     ← same clock forever, new DbContext per request
```

**Why `DbContext` is scoped.** It tracks the entities it has loaded (the "change tracker") and is **not thread-safe**. Per request, all your code should share one context, so that one `SaveChangesAsync` saves everything in one transaction. Across requests, sharing would mix up different users' data and corrupt the change tracker under concurrent access.

**Thread safety follows from lifetime.** A singleton is used by **every request at the same time**, on many threads at once. So a singleton must be **stateless or thread-safe**. A plain `Dictionary` field in a singleton that requests write to is a race condition waiting to happen; use `ConcurrentDictionary` or a proper cache. A scoped service is used by one request at a time, so it normally needs no locking.

---

## 4. What a scope is in ASP.NET Core

A **scope** is a container "session" with its own set of scoped instances, created with `IServiceProvider.CreateScope()` and **disposed when it ends**. ASP.NET Core creates one scope **per HTTP request**, and resolves your endpoint and everything it needs from that scope. When the request finishes, the scope is disposed, and with it every scoped and transient `IDisposable` it created: the `DbContext` and its database connection go back to the pool.

```
App start ──► ROOT provider (holds the singletons)
                │
                ├── Request 1 ──► scope A: DbContext#7 … ──► disposed at the end of request 1
                ├── Request 2 ──► scope B: DbContext#8 … ──► disposed at the end of request 2
                └── BackgroundService (singleton) ──► no request, so NO scope unless it creates one
```

Two places have **no request scope**, and they're where lifetime bugs live:
- **Singletons**, including `BackgroundService`s, which are registered as singletons.
- **Code that resolves from the root provider**, such as `app.Services.GetRequiredService<T>()` in `Program.cs`. (That was the service-locator point in your T3 review.)

---

## 5. Captive dependencies

**A captive dependency** is a service with a **shorter** lifetime held by a service with a **longer** lifetime. The shorter-lived service gets "captured", and lives as long as its holder.

```csharp
builder.Services.AddDbContext<AppDbContext>();          // scoped
builder.Services.AddSingleton<ReportService>();          // singleton…

public class ReportService(AppDbContext db) { … }        // …that captures a scoped DbContext
```

What happens (quiz Q6):
1. The first request that needs `ReportService` creates it, and with it **one** `AppDbContext`.
2. That `ReportService` is a singleton, so **every later request, on every thread, uses that same `DbContext` forever.**

The symptoms in production, all delayed and intermittent, which is what makes this bug nasty:
- **Concurrency exceptions:** `InvalidOperationException: A second operation was started on this context instance before a previous operation completed`, when two requests use it at the same time. `DbContext` isn't thread-safe.
- **Stale data:** the change tracker keeps the entities it loaded long ago, so reads can return old values.
- **A growing memory leak:** the change tracker keeps every entity it has ever tracked.
- **Data from one user leaking into another's request**, if the scoped service holds per-request information such as the current tenant (FleetTrack rule 6). That's a **security** bug.
- **A disposed object:** if the captured service belonged to a scope that ended, it may have been disposed already, giving `ObjectDisposedException`.

The rule is: **a service may only depend on services with the same or a longer lifetime.**

| Holder ↓ / Dependency → | Singleton | Scoped | Transient |
| --- | --- | --- | --- |
| **Singleton** | ✅ | ❌ captive | ⚠️ captured too (lives forever; harmless only if stateless) |
| **Scoped** | ✅ | ✅ | ✅ |
| **Transient** | ✅ | ✅ (lives as long as the scope) | ✅ |

---

## 6. Scope validation: catching it at startup

The container can detect captive dependencies for you. `ServiceProviderOptions` has two switches:

| Option | What it checks |
| --- | --- |
| **`ValidateScopes`** | Throws if a **scoped** service is resolved from the **root** provider, directly or through a singleton that depends on it |
| **`ValidateOnBuild`** | At `Build()`, tries to construct the dependency graph of **every** registration, so missing registrations **and** captive dependencies fail at **startup** instead of on the first request that hits them |

**In ASP.NET Core both are on automatically in the `Development` environment**, and **off in other environments** (`Production` by default), because checking the whole graph has a startup cost. You can turn them on explicitly:

```csharp
builder.Host.UseDefaultServiceProvider(options =>
{
    options.ValidateScopes = true;
    options.ValidateOnBuild = true;
});
```

Or, for a plain container in a test:

```csharp
var services = new ServiceCollection();
// … registrations …
var provider = services.BuildServiceProvider(new ServiceProviderOptions
{
    ValidateScopes = true,
    ValidateOnBuild = true,
});
```

With validation on, the captive `ReportService` above fails with an `InvalidOperationException` whose message says, roughly, *"Cannot consume scoped service 'AppDbContext' from singleton 'ReportService'"*. That's **fail fast** again (M00-T5 §4.2): a bug that would show up as random production errors becomes a clear startup failure.

**The trap:** a developer runs the app locally in Development, so validation is on, and sees nothing wrong. But the production configuration is different (an environment-specific registration, a feature flag), or the code path never ran locally. Validation is off in Production, so the captive dependency ships. **Senior practice:** turn validation on in **all** environments if the startup cost is acceptable (it usually is), and/or have a **test** that builds the real container with validation on. That second option is exactly this task's ✅ check.

---

## 7. The fix: `IServiceScopeFactory`

When a **singleton** legitimately needs a **scoped** service (a `BackgroundService` that processes a queue and needs a `DbContext` for each item is the classic case), it must not hold it. Instead, it **creates a scope for each unit of work**, uses the scoped service inside it, and disposes the scope:

```csharp
public sealed class OutboxProcessor(IServiceScopeFactory scopeFactory) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await using (var scope = scopeFactory.CreateAsyncScope())     // one scope per unit of work
            {
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                // … process one batch with this DbContext …
            }                                                              // scope disposed → DbContext disposed

            await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken);
        }
    }
}
```

- **`IServiceScopeFactory` is itself a singleton**, so injecting it into a singleton is fine.
- **`CreateAsyncScope()`** with `await using` disposes asynchronously, which matters for services like `DbContext` that implement `IAsyncDisposable`.
- **One scope per unit of work** (per message, per batch), not one scope for the whole service's lifetime. A single scope created once and kept forever just recreates the captive dependency.
- This is a deliberate, local use of the container (a small service-locator pattern), which is acceptable at this boundary. Keep it in the infrastructure code, not scattered through business logic.

**Other fixes, depending on the case:**
- **Change the holder's lifetime:** if `ReportService` has no reason to be a singleton, make it scoped. Often the simplest correct fix.
- **Change the dependency's lifetime:** if the dependency is actually stateless and thread-safe, it may be fine as a singleton.
- **Use a factory designed for this:** for EF Core, `IDbContextFactory<T>` (registered with `AddDbContextFactory`) lets a singleton create a short-lived `DbContext` when it needs one. For HTTP, `IHttpClientFactory`.

---

## 8. Disposal: who disposes what

**The rule: the container disposes what the container created.** When a scope ends, it disposes the scoped and transient `IDisposable`/`IAsyncDisposable` objects it created. When the application shuts down, the root provider disposes the singletons.

Consequences:
- **Don't dispose injected services yourself.** If you `Dispose()` an injected `DbContext`, the next class in the same request that uses it gets an `ObjectDisposedException`.
- **Objects you create with `new` are yours to dispose**, even if you register the instance: `AddSingleton(new Foo())` is *not* disposed by the container, because the container didn't create it.
- **A memory leak to know about:** a **transient** `IDisposable` resolved from the **root** provider is kept by the root until application shutdown, so the container can dispose it then. Resolving such a service from the root repeatedly (in a loop, or per message in a singleton) grows memory forever. Resolve from a scope instead.

---

## 9. More registration tools: factories, multiple implementations, keyed services

**Factory registrations**, for when construction needs logic (you used one in M00-T5 for the health check's connection string):

```csharp
builder.Services.AddSingleton(sp =>
{
    var options = sp.GetRequiredService<IOptions<DatabaseConfiguration>>().Value;
    return NpgsqlDataSource.Create(options.FleetTrack);
});
```

**Multiple implementations of one interface.** Register several, and inject `IEnumerable<T>` to get all of them, in registration order:

```csharp
builder.Services.AddSingleton<IShipmentRule, MaxWeightRule>();
builder.Services.AddSingleton<IShipmentRule, HazardousGoodsRule>();

public class ShipmentValidator(IEnumerable<IShipmentRule> rules) { … }   // gets both
```

If you inject a single `IShipmentRule`, you get the **last** one registered. That's why library code uses **`TryAdd…`** (`TryAddSingleton`, `TryAddScoped`): it only registers when nothing is registered yet, so the application's own registration isn't silently replaced.

**Keyed services (.NET 8+)**, for when you need **different implementations of the same interface in different places**, chosen by a key:

```csharp
builder.Services.AddKeyedSingleton<IRouteOptimizer, FastRouteOptimizer>("fast");
builder.Services.AddKeyedSingleton<IRouteOptimizer, CheapestRouteOptimizer>("cheapest");

public class DispatchService([FromKeyedServices("cheapest")] IRouteOptimizer optimizer) { … }
```

You can also resolve by key at run time: `sp.GetRequiredKeyedService<IRouteOptimizer>(key)`. **When not to:** if the choice of implementation depends on runtime data (the customer's plan, a setting), keys spread that decision across constructor attributes. A small factory or strategy class that picks the implementation is often clearer.

**Open generics:** `services.AddScoped(typeof(IRepository<>), typeof(EfRepository<>))` registers every `IRepository<T>` at once.

---

## 10. Choosing a lifetime, and anti-patterns

**A practical way to choose:**
1. Does it hold **per-request state** (a `DbContext`, the current tenant, a unit of work)? → **Scoped**.
2. Is it **stateless or thread-safe**, and expensive or worth sharing (a pool, a cache, a client factory, configuration)? → **Singleton**.
3. Is it cheap, stateless, and none of the above? → **Transient** (or singleton; for stateless classes the difference rarely matters).
4. Then **check its dependencies** against the table in §5.

**Anti-patterns that come up in reviews:**
- **Service locator:** injecting `IServiceProvider` and calling `GetService` everywhere hides dependencies and bypasses validation. It's acceptable only at infrastructure boundaries (§7).
- **`BuildServiceProvider()` during registration:** builds a second container with its own singletons (M00-T5 §5). ASP.NET Core warns about it (ASP0000).
- **Mutable state in singletons** without thread safety (§3).
- **Captive dependencies** hidden two levels deep: a singleton → a transient → a scoped service. The transient in the middle doesn't help; the scoped service is still captured. Validation catches this; reviews usually don't.
- **Async work in constructors:** constructors can't be `async`, which tempts `.Result` (M01-T2). Do async setup in a factory method, or in a hosted service at startup.

---

## 11. Glossary

| Term | Meaning |
| --- | --- |
| **DI (dependency injection)** | Classes declare their dependencies (usually in the constructor); a container creates and supplies them |
| **Container / `IServiceProvider`** | The object that creates services and manages their lifetimes |
| **Composition root** | The startup place (`Program.cs`) where services are registered |
| **Registration / resolution** | Describing a service before `Build()` / creating it at run time |
| **Singleton / scoped / transient** | One instance per app / per scope (per request) / per resolution |
| **Scope** | A container session with its own scoped instances, disposed when it ends; one per HTTP request |
| **Root provider** | The application-level container; holds singletons and has no request scope |
| **Captive dependency** | A shorter-lived service held, and so kept alive, by a longer-lived one |
| **`ValidateScopes` / `ValidateOnBuild`** | Container checks for scoped-from-root resolution / for the whole graph at `Build()`; on by default only in Development |
| **`IServiceScopeFactory`** | A singleton that creates new scopes (`CreateScope`, `CreateAsyncScope`) |
| **`IDbContextFactory<T>`** | Creates short-lived `DbContext` instances on demand, for singletons and background work |
| **Keyed service** | A registration with a key, so several implementations of one interface can be injected by key |
| **`TryAdd…`** | Registers a service only if none is registered yet |
| **Service locator** | Fetching dependencies from the container inside business code (an anti-pattern) |

---

## 12. Summary

1. DI lets classes declare dependencies; the container builds them and **controls how long they live**.
2. Singleton = one per app (must be thread-safe), scoped = one per request (`DbContext`), transient = one per resolution.
3. A service may only depend on services with the same or a **longer** lifetime. A scoped service inside a singleton is a **captive dependency**: shared across requests and threads, with concurrency errors, stale data, leaks and even cross-user data leaks.
4. `ValidateScopes` and `ValidateOnBuild` turn captive dependencies into startup failures, but only in Development by default. Turn them on everywhere, or test the real container with them on.
5. A singleton that needs scoped services creates **one scope per unit of work** with `IServiceScopeFactory` (or uses a factory like `IDbContextFactory`).
6. The container disposes what it created. Don't dispose injected services, and don't resolve disposable transients from the root.

### Check yourself

1. Two classes in the same request both inject `AppDbContext`. Do they get the same instance? And in two different requests? (§3)
2. Why must a singleton be thread-safe, but a scoped service usually not? (§3)
3. Explain quiz Q6 out loud: a singleton `ReportService` takes `AppDbContext`. What goes wrong, and what symptoms appear in production? (§5)
4. Your app works locally but throws "A second operation was started on this context instance" in production. Why might validation not have caught it locally? (§6)
5. A `BackgroundService` needs a `DbContext`. Show the shape of the correct code, and say why one scope created in the constructor is still wrong. (§7)
6. When does the container *not* dispose a singleton for you? (§8)
7. When would you use keyed services, and when would a factory be clearer? (§9)

**Before you build: predict.** For a singleton that takes a scoped service, in a plain `ServiceCollection`:
- With `ValidateScopes = false` and `ValidateOnBuild = false`: what happens when you resolve the singleton, and when you resolve it twice from two different scopes?
- With validation on: at which line does it fail (`BuildServiceProvider` or `GetRequiredService`), and with what exception?

Post your predictions here before running anything.

### Interview questions this prepares you for

- "Explain singleton, scoped and transient. Why is `DbContext` scoped?"
- "What's a captive dependency? How would you find one in a large codebase?"
- "How does a `BackgroundService` use a `DbContext` correctly?"
- "Our app throws random 'second operation started on this context' errors under load. Where do you look?"
- "Why is scope validation on in Development but off in Production by default? Would you change that?"
- "When would you use keyed services?"

### For FleetTrack

- **The lab** goes in `labs/M01-T5-di` (or a test project in it): a scoped service, a singleton that captures it, and NUnit tests that build a `ServiceCollection` with validation off and on, assert the startup failure with validation on, and show the `IServiceScopeFactory` version passing.
- **In FleetTrack itself:** EF Core's `DbContext` arrives in M03 (scoped), the outbox processor in M06 is a `BackgroundService` that needs exactly the §7 pattern, and the "current tenant" in M11 must be scoped (a captive tenant would leak data between customers). A good guardrail to add later: an integration test that builds FleetTrack's real container with `ValidateScopes` and `ValidateOnBuild` on.

---

## Appendix: two async topics from the M01 quiz

These aren't part of T5's build, but the M01 quiz asks about them (Q8 and Q9) and no other task covers them.

### A1. Bounding concurrency (`Task.WhenAll` over 10,000 calls)

**The tempting code:**

```csharp
var tasks = vehicleIds.Select(id => http.GetFromJsonAsync<Position>($"/positions/{id}", ct));
var positions = await Task.WhenAll(tasks);   // 10,000 requests started at once
```

`Select` starts every request immediately, so 10,000 HTTP calls are in flight at the same moment. What goes wrong:
- **You overload the other service.** It sees a sudden burst of 10,000 requests, so it slows down, rate-limits you (HTTP 429) or falls over. You've just created your own denial-of-service attack.
- **Your own resources run out:** thousands of connections and sockets, memory for 10,000 pending requests and responses, and the connection pool queueing behind them.
- **Timeouts cascade:** the calls queue up inside your app, so later ones time out even though the other service would have answered them quickly.
- **All or nothing:** `Task.WhenAll` gives you nothing until **every** call finishes, and if one fails, `await` throws (only the first exception, M01-T1 §9), even though 9,999 succeeded.

**Bound it**, meaning limit how many calls run at once:

```csharp
// Option 1: Parallel.ForEachAsync with a maximum degree of parallelism (.NET 6+)
var results = new ConcurrentBag<Position>();
await Parallel.ForEachAsync(vehicleIds,
    new ParallelOptions { MaxDegreeOfParallelism = 20, CancellationToken = ct },
    async (id, token) =>
    {
        var p = await http.GetFromJsonAsync<Position>($"/positions/{id}", token);
        if (p is not null) results.Add(p);
    });

// Option 2: a SemaphoreSlim as a gate, if you need to keep WhenAll's shape
using var gate = new SemaphoreSlim(20);
var tasks = vehicleIds.Select(async id =>
{
    await gate.WaitAsync(ct);
    try { return await http.GetFromJsonAsync<Position>($"/positions/{id}", ct); }
    finally { gate.Release(); }
});
var positions = await Task.WhenAll(tasks);
```

How to choose the limit: start from what the **other side** can take (its documented rate limit, or a load test), not from what your machine can send. Little's law from M01-T2 helps: if each call takes 100 ms and you can make 200 calls per second, you need about 200 × 0.1 = 20 calls in flight.

**Better still, avoid 10,000 calls:** ask the other service for a **batch endpoint** (`POST /positions/query` with all the ids), or move the work onto a queue (M06) and let consumers process it at a controlled rate. `Channel<T>` with a fixed number of consumers (M08) is the in-process version of the same idea.

### A2. `IAsyncEnumerable<T>` vs `Task<List<T>>`

**`Task<List<T>>`** means "I'll give you **all** the items, **once they're all ready**". The endpoint loads every row into a list in memory, then serializes the whole list.

**`IAsyncEnumerable<T>`** means "I'll give you items **one at a time, as they become available**", and the consumer pulls them with `await foreach`:

```csharp
app.MapGet("/positions/history", (AppDbContext db, CancellationToken ct) =>
    db.Positions.Where(p => p.VehicleId == id).OrderBy(p => p.At).AsAsyncEnumerable());
```

What it gives you, returned from an API endpoint:
- **Streaming:** ASP.NET Core with `System.Text.Json` writes each item to the response **as it's read from the database**, without building the whole list first. The client starts receiving data sooner.
- **Constant memory:** the server holds a few items at a time instead of 100,000. For large exports that's the difference between fine and an out-of-memory crash, or LOH pressure from a huge `List<T>` (M01-T4 §4).
- **Cancellation per item:** if the client disconnects, the loop stops at the next item instead of finishing the whole query (the `RequestAborted` token flows in; in your own async iterators, mark the token parameter with `[EnumeratorCancellation]`).

The trade-offs, and when **not** to use it:
- **Errors after the start:** once the first items are sent, the HTTP status (200) has already gone out. If the database fails halfway, the client gets a **truncated** response, not a clean 500. Clients must cope with that.
- **The database connection stays open** for as long as the client takes to read the response. A slow client holds a pooled connection longer.
- **Small, paged results gain nothing.** For 20 items per page, `Task<List<T>>` is simpler and gives you a clean error status. Use streaming for **large or unbounded** results (exports, history), and **paging** (M02) for normal list endpoints.
