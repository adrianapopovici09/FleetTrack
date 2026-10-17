# M00-T5 · Configuration, options, secrets and health checks

Task: [M00-T5](../modules/M00-foundations.md)

This file is the complete lesson for the task; you don't need to read anything else. Original sources, if you ever want to go deeper: [Configuration in ASP.NET Core](https://learn.microsoft.com/aspnet/core/fundamentals/configuration/) · [Options pattern](https://learn.microsoft.com/dotnet/core/extensions/options) · [Safe storage of app secrets](https://learn.microsoft.com/aspnet/core/security/app-secrets) · [Health checks in ASP.NET Core](https://learn.microsoft.com/aspnet/core/host-and-deploy/health-checks)

**Reading time:** about 50 minutes.

## Contents

1. [Configuration: where settings come from, and who wins](#1-configuration-where-settings-come-from-and-who-wins)
2. [Keys, sections and binding](#2-keys-sections-and-binding)
3. [The options pattern](#3-the-options-pattern)
4. [Validation and failing fast](#4-validation-and-failing-fast)
5. [Registration time vs run time](#5-registration-time-vs-run-time)
6. [Secrets](#6-secrets)
7. [Health checks: liveness vs readiness](#7-health-checks-liveness-vs-readiness)
8. [Health checks in ASP.NET Core, in practice](#8-health-checks-in-aspnet-core-in-practice)
9. [Glossary](#9-glossary)
10. [Summary, check yourself, interview questions, for FleetTrack](#10-summary)

---

## 1. Configuration: where settings come from, and who wins

### 1.1 Providers and their order

ASP.NET Core reads settings from several **configuration providers** (sources) and merges them into **one flat set of key/value pairs**. With the default `WebApplication.CreateBuilder(args)`, the sources are added in this order, and **a later source overrides an earlier one for the same key**:

```
1. appsettings.json                      committed defaults
2. appsettings.{Environment}.json        e.g. appsettings.Development.json
3. User secrets                          ONLY when the environment is "Development"
4. Environment variables
5. Command-line arguments                ← last added, so it wins
```

A memory aid: **"the closer to the moment of running, the stronger"**. Files are written at development time, environment variables are set by the deployment, and command-line arguments are typed when the process starts.

**Why this order makes sense:** the JSON files hold safe defaults and are committed to git. Each machine or deployment then **overrides** what's specific to it (connection strings, passwords, URLs) through environment variables, without changing a single file. This is one of the **twelve-factor app** principles, a widely used set of guidelines for cloud apps: *store configuration in the environment*. The same build artefact then runs in every environment.

**Worked example (quiz Q5).** `appsettings.json` contains `"ConnectionStrings": { "FleetTrack": "Host=dev-db;..." }`, and the container has an environment variable `ConnectionStrings__FleetTrack=Host=prod-db;...`. **The environment variable wins**, because environment variables are added after the JSON files. The app connects to `prod-db`.

### 1.2 The environment name

`ASPNETCORE_ENVIRONMENT` (or `DOTNET_ENVIRONMENT`) sets the **environment name**. The usual values are `Development`, `Staging` and `Production`, and **`Production` is the default** when nothing is set. It decides:
- which `appsettings.{Environment}.json` is loaded,
- whether user secrets are loaded (only in `Development`),
- what `app.Environment.IsDevelopment()` returns (developer exception page, OpenAPI UI and so on).

You can invent your own names. FleetTrack's integration tests use `Testing`, precisely so that **user secrets aren't loaded** and a developer's local connection string can't leak into a test (M00-T6).

### 1.3 Reloading

The JSON providers are registered with `reloadOnChange: true`, so editing `appsettings.json` while the app runs updates the configuration in memory. Whether your code *sees* the change depends on which options interface it uses (§3.2). Environment variables and command-line arguments are read once, at startup.

---

## 2. Keys, sections and binding

### 2.1 Keys are flat paths

Configuration is hierarchical in JSON, but internally every value has a **flat key** whose levels are joined with `:`:

```json
{
  "ConnectionStrings": { "FleetTrack": "Host=127.0.0.1;..." },
  "Tracking": { "MaxBatchSize": 500, "Regions": [ "eu", "us" ] }
}
```

becomes:

```
ConnectionStrings:FleetTrack = Host=127.0.0.1;...
Tracking:MaxBatchSize        = 500
Tracking:Regions:0           = eu        ← array items get an index
Tracking:Regions:1           = us
```

Keys are **case-insensitive**. A **section** is everything under a prefix: `configuration.GetSection("Tracking")`. `configuration.GetConnectionString("FleetTrack")` is just a shortcut for `configuration["ConnectionStrings:FleetTrack"]`.

### 2.2 The double underscore

Environment variable names can't contain `:` in Linux shells. So the environment-variable provider treats **`__` (double underscore)** as `:`:

```
ConnectionStrings__FleetTrack=Host=...   →  key  ConnectionStrings:FleetTrack
Tracking__Regions__0=eu                  →  key  Tracking:Regions:0
```

### 2.3 What binding does, and what it doesn't

**Binding** copies values from a section into an object's properties. It matches **section path + property name = key**:

```csharp
public sealed class DatabaseConfiguration
{
    public string FleetTrack { get; init; } = string.Empty;
}

// Bind the "ConnectionStrings" section:
//   key "ConnectionStrings:FleetTrack"  →  property FleetTrack
```

Two rules that tripped you up before:
- **It matches names; it never looks inside a value.** The connection string `Host=127.0.0.1;Port=5432;Database=fleettrack` is **one string value**. Binding won't split it into `Host`, `Port` and `Database` properties. If you need its parts, parse it with `NpgsqlConnectionStringBuilder`.
- **Missing keys are silently skipped.** If the key isn't there, the property simply keeps its default value (`string.Empty` above), with no error. That's exactly why validation (§4) is essential.

Binding does convert **types**: `"500"` becomes an `int`, `"true"` a `bool`, `"00:00:05"` a `TimeSpan`. A value that can't be converted throws when the options are first created.

---

## 3. The options pattern

### 3.1 Why

Reading `configuration["Tracking:MaxBatchSize"]` all over the code is fragile. Key strings are scattered, typos fail silently, every caller parses the string itself, and nothing validates the value. The **options pattern** binds a section to a small, typed class once, validates it, and hands it out through dependency injection:

```csharp
// registration
services.AddOptions<TrackingOptions>()
        .BindConfiguration("Tracking")
        .ValidateDataAnnotations()
        .ValidateOnStart();

// usage
public sealed class BatchWriter(IOptions<TrackingOptions> options)
{
    private readonly int _max = options.Value.MaxBatchSize;
}
```

### 3.2 The three interfaces

This difference is a common interview question:

| Interface | Registered as | Sees changes while running? | Use when |
| --- | --- | --- | --- |
| `IOptions<T>` | Singleton; created once, on first use | **No** | The default. Settings that don't change while the app runs |
| `IOptionsSnapshot<T>` | Scoped; recomputed once **per request** | Yes, from the next request | Web code that needs fresh values, consistent within one request |
| `IOptionsMonitor<T>` | Singleton, with `CurrentValue` and an `OnChange` callback | **Yes, immediately** | Singletons and background services that must react to changes |

A trap: **a singleton can't inject `IOptionsSnapshot<T>`**, because it's scoped. That would be a *captive dependency*, which you'll study in M01-T5. Use `IOptionsMonitor<T>` there.

Two more features worth recognising:
- **Named options:** several instances of the same options class, distinguished by name, for example two HTTP clients with different settings: `IOptionsMonitor<ApiClientOptions>.Get("billing")`.
- **`Configure` / `PostConfigure`:** set values in code, before or after binding, for example a default that depends on another setting.

---

## 4. Validation and failing fast

### 4.1 Ways to validate

```csharp
// 1. Data annotations on the class
public sealed class TrackingOptions
{
    [Range(1, 10_000)] public int MaxBatchSize { get; init; }
}
services.AddOptions<TrackingOptions>().BindConfiguration("Tracking").ValidateDataAnnotations();

// 2. A lambda, with a helpful message
.Validate(o => !string.IsNullOrWhiteSpace(o.FleetTrack),
          "ConnectionStrings:FleetTrack is missing. Locally: dotnet user-secrets set ...")

// 3. A class implementing IValidateOptions<T>, for complex rules or rules that need other services
```

Data annotations validation uses reflection. For trimmed or ahead-of-time compiled apps there's a source-generated alternative: a `partial` class marked `[OptionsValidator]`.

### 4.2 `ValidateOnStart()`

**This is the important part.** Without it, validation runs **lazily**, the first time something asks for the options. That might be ten minutes after deployment, on the first real request, which then fails with a confusing error while the deployment looks healthy. With `ValidateOnStart()`, the host validates during startup, and **the app refuses to start** if the configuration is wrong.

This principle is called **fail fast**: a misconfigured deployment should fail **immediately, loudly and with an actionable message** ("ConnectionStrings:FleetTrack is missing; here's how to set it"), not half-work and fail later. In Kubernetes or Azure Container Apps, a pod that crashes on startup is held back from receiving traffic, so a rolling deployment of a bad config stops before it replaces the healthy instances. A late failure would have replaced them.

**When not to:** settings that are genuinely optional, or features that should be able to start degraded. Make those explicitly optional, with defaults, rather than validating them as required.

---

## 5. Registration time vs run time

This was a weak spot, and it explains *why* FleetTrack's health check uses a factory.

An ASP.NET Core app has two phases:

```
var builder = WebApplication.CreateBuilder(args);
builder.Services.Add...(...);        ← REGISTRATION: you describe services. No container exists yet.
var app = builder.Build();           ← the IServiceProvider (the DI container) is created HERE
app.Map...(...); app.Run();          ← RUN TIME: services can be resolved
```

During registration there is **no `IServiceProvider`**, so you can't "get" `IOptions<DatabaseConfiguration>` yet. The tempting fix is wrong:

```csharp
// ❌ builds a SECOND container: duplicate singletons, and options validated outside the host's startup
var sp = builder.Services.BuildServiceProvider();
var cs = sp.GetRequiredService<IOptions<DatabaseConfiguration>>().Value.FleetTrack;
```

ASP.NET Core even has an analyzer warning for this (ASP0000). The right approach is the **factory overload** that many registration APIs offer. You pass a function that receives the real `IServiceProvider` *later*, when the service is first needed:

```csharp
// ✓ deferred: runs at run time, with the real container
services.AddHealthChecks().AddNpgSql(
    connectionStringFactory: sp => sp.GetRequiredService<IOptions<DatabaseConfiguration>>().Value.FleetTrack, ...);
```

---

## 6. Secrets

### 6.1 The rule

**Secrets (passwords, API keys, connection strings containing passwords) never go into git.** Deleting them in a later commit isn't enough: they stay in the **git history** forever, and automated scanners find leaked keys in public repositories within minutes. If a secret is ever committed, treat it as compromised: **rotate it** (change the password or key), then clean up.

### 6.2 User secrets: local development only

```bash
dotnet user-secrets init --project src/FleetTrack.API          # adds <UserSecretsId> to the .csproj
dotnet user-secrets set "ConnectionStrings:FleetTrack" "Host=..." --project src/FleetTrack.API
dotnet user-secrets list --project src/FleetTrack.API
```

- Values are stored in a JSON file **in your user profile**, outside the repository. On Windows that's `%APPDATA%\Microsoft\UserSecrets\<UserSecretsId>\secrets.json`, so they can't be committed by accident.
- They are **not encrypted**. This is a convenience to keep secrets out of git, not a secure vault.
- They're loaded **only in the `Development` environment**.

### 6.3 Production

Use environment variables set by the hosting platform, or better, a **secret store** such as Azure Key Vault, AWS Secrets Manager or HashiCorp Vault. Ideally the app reaches it with a **managed identity**: the cloud platform itself vouches for the app, so no password for the vault is stored anywhere. (M13 covers this with Azure's free tier.)

The trade-off: environment variables are simple and universal, but they're visible to anyone who can inspect the process or the deployment configuration, and rotating them means redeploying. A vault adds a dependency and some setup, and gives you auditing, access control and rotation without redeploying.

---

## 7. Health checks: liveness vs readiness

### 7.1 Who asks, and why

**Orchestrators**, platforms that run and manage containers (Kubernetes, Azure Container Apps), and **load balancers** regularly call your app's health endpoints and *act* on the answer. These calls are called **probes**. There are two different questions, and mixing them up causes outages:

| | **Liveness** | **Readiness** |
| --- | --- | --- |
| Question | "Is this process alive, or stuck beyond repair?" | "Can this instance usefully handle traffic **right now**?" |
| If it fails | The platform **kills and restarts** the instance | The platform **stops sending it traffic**, but leaves it running |
| Checks | Only the process itself (it can answer HTTP at all) | The dependencies it needs: database, essential downstream services |

There's also a **startup probe**, which gives slow-starting apps time to warm up before the liveness checks begin, so they aren't killed while they're still starting.

### 7.2 Why liveness must never check the database

Imagine Postgres is down for one minute, and you have five API instances:

- **If liveness checks the database:** *all five* fail liveness, so the platform restarts *all five*. Restarting doesn't fix the database, so they fail again and get restarted again: a **restart storm**. You also lose in-flight requests, pay cold-start costs, and when Postgres comes back, all five reconnect at once (a **thundering herd**) and may knock it over again.
- **If only readiness checks the database:** all five are taken out of the load balancer and **keep running**. When Postgres recovers, readiness passes and they're put back within seconds. Nothing restarts.

**The rule:** liveness checks only what a **restart would fix** (a deadlocked process, a corrupted in-memory state). A shared dependency on liveness means every instance restarts together. You reproduced this in M00-T8.

### 7.3 A subtler point about readiness

If *every* instance shares the same database, readiness failing means *every* instance leaves the load balancer, and callers see "no healthy upstream". That's still an outage, just a cleaner one, with no restarts. So a senior engineer also asks: **should this dependency be in readiness at all?** If the app can serve *some* requests without it (cached reads, a degraded mode), it may be better to stay ready and fail only the affected requests. Put a dependency in readiness when the instance really can't do anything useful without it, as FleetTrack's API can't without Postgres today.

---

## 8. Health checks in ASP.NET Core, in practice

### 8.1 The pieces

```csharp
// registration
services.AddHealthChecks()
        .AddNpgSql(connectionStringFactory: sp => ..., name: "postgres",
                   tags: ["ready"], timeout: TimeSpan.FromSeconds(5));

// endpoints
app.MapHealthChecks("/health/live",  new HealthCheckOptions { Predicate = _ => false });                      // runs no checks
app.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = c => c.Tags.Contains("ready") });  // runs only "ready" checks
```

- **Tags** label checks. **Predicates** choose which checks an endpoint runs. A liveness predicate of `_ => false` runs **no checks**: if the process can answer HTTP, it's alive.
- **Results:** a check returns `Healthy`, `Degraded` or `Unhealthy`. By default `Healthy` and `Degraded` return **HTTP 200**, and `Unhealthy` returns **HTTP 503 (Service Unavailable)**. The overall status is the worst of the individual checks.
- **Timeouts:** always set one. A check that hangs makes the probe hang, and the orchestrator counts a timeout as a failure anyway, after much longer.
- **A custom check** implements `IHealthCheck`:

  ```csharp
  public sealed class QueueDepthCheck(IQueue queue) : IHealthCheck
  {
      public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext ctx, CancellationToken ct)
      {
          var depth = await queue.GetDepthAsync(ct);
          return depth < 10_000 ? HealthCheckResult.Healthy()
                                : HealthCheckResult.Degraded($"Queue depth {depth}");
      }
  }
  ```

### 8.2 Good practice

- **Keep checks cheap.** Probes run every few seconds on every instance: open a connection and run `SELECT 1`, never a heavy query.
- **Don't expose details publicly.** A detailed JSON response reveals database names, versions and internal topology. Return only the status, or put the endpoints on an internal port or behind authorization.
- **Liveness depends on nothing external, ever** (§7.2).
- The NpgSql check comes from the community package `AspNetCore.HealthChecks.NpgSql` (Apache-2.0 licence, checked as per FleetTrack rule 7).

---

## 9. Glossary

| Term | Meaning |
| --- | --- |
| **Configuration provider** | A source of key/value settings (JSON file, environment variables, user secrets, command line) |
| **Environment name** | `Development`/`Staging`/`Production` (default)/custom; picks the env-specific JSON and whether user secrets load |
| **Section** | All configuration keys under a common prefix, e.g. `Tracking` |
| **Binding** | Copying configuration values into an object's properties by matching key paths to property names |
| **Options pattern** | Typed settings classes bound from configuration and handed out via DI (`IOptions<T>` and friends) |
| **Fail fast** | Detect a broken configuration at startup and refuse to run, with a clear message |
| **`ValidateOnStart`** | Runs options validation during host startup instead of on first use |
| **Registration time / run time** | Before `Build()` (describing services; no container) / after `Build()` (resolving services) |
| **User secrets** | Unencrypted per-user JSON file for local development secrets; loaded only in `Development` |
| **Managed identity** | A cloud-provided identity that lets an app access resources without a stored password |
| **Probe** | A periodic health request from an orchestrator or load balancer |
| **Liveness / readiness / startup probe** | "Restart me if this fails" / "stop sending me traffic if this fails" / "give me time to start" |
| **Restart storm** | Every instance restarting repeatedly because a shared dependency is in their liveness check |
| **Thundering herd** | Many clients hitting a recovering service at the same moment |

---

## 10. Summary

1. Configuration sources are added in order (JSON → env JSON → user secrets → environment variables → command line), and the **last one wins**. `__` stands for `:` in environment variable names.
2. Binding matches *section + property name = key*. It never parses a value's contents and silently skips missing keys, so validate.
3. `IOptions` (read once), `IOptionsSnapshot` (per request, scoped), `IOptionsMonitor` (live, singleton).
4. `ValidateOnStart()` makes a bad configuration stop the app at startup with a clear message: fail fast.
5. No `IServiceProvider` exists before `Build()`. Use factory overloads, never `BuildServiceProvider()`.
6. Secrets never go in git. User secrets are for local development only and aren't encrypted; production uses a vault or the platform's environment, ideally with managed identity.
7. Liveness checks only what a restart fixes; readiness checks the dependencies needed to serve traffic. A database on liveness causes a restart storm.

### Check yourself

1. `appsettings.json`, user secrets and an environment variable all set `ConnectionStrings:FleetTrack`, and the environment is `Development`. Which value wins? Which wins if the environment is `Production`? (§1)
2. Write the environment-variable name that sets `Tracking:Regions:1`. (§2.2)
3. You bind `ConnectionStrings` to a class with a `Host` property, expecting it to receive `127.0.0.1`. It stays empty. Why? (§2.3)
4. A background service (a singleton) needs settings that can change while the app runs. Which interface, and why not `IOptionsSnapshot`? (§3.2)
5. Why is `builder.Services.BuildServiceProvider()` inside registration code a bug, and what do you do instead? (§5)
6. A colleague committed an API key, then deleted it in the next commit. Is the problem solved? What do you do? (§6.1)
7. Postgres is down for a minute. Describe what happens to five instances if it's in liveness, and if it's only in readiness. (§7.2)

### Interview questions this prepares you for

- "An environment variable and `appsettings.json` both set the connection string. Which wins, and why the double underscore?"
- "`IOptions` vs `IOptionsSnapshot` vs `IOptionsMonitor`?"
- "Why use `ValidateOnStart`? What happens in a rolling deployment without it?"
- "Liveness vs readiness, and what happens if liveness checks the database?"
- "How do you manage secrets from a developer laptop all the way to production?"
- "Should every dependency be part of your readiness check?"

### For FleetTrack

- `DatabaseConfiguration` (in Infrastructure) is bound from the `ConnectionStrings` section, validated with a lambda whose message tells you exactly how to set the value, and checked with `ValidateOnStart()`.
- Locally, the connection string lives in **user secrets** (the README's Quick start, step 5). In CI and containers it's the `ConnectionStrings__FleetTrack` environment variable.
- The NpgSql health check is registered in Infrastructure through a **factory** (§5), tagged as ready, with a 5-second timeout. `/health/live` runs no checks, and `/health/ready` runs the tagged ones. Both are mapped in `MapFleetTrackDefaults()`, which you'll compare with Aspire's service defaults in M13.
- Loose end: `HealthCheckTags` lives in a vague `Application.Helpers` namespace, and the validation message would read better as a raw string literal.
