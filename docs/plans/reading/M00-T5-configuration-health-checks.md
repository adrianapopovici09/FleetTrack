# M00-T5 reading notes: configuration, options, secrets and health checks

Task: [M00-T5](../modules/M00-foundations.md) · Sources: [Configuration in ASP.NET Core](https://learn.microsoft.com/aspnet/core/fundamentals/configuration/) · [Options pattern](https://learn.microsoft.com/dotnet/core/extensions/options) · [Safe storage of app secrets](https://learn.microsoft.com/aspnet/core/security/app-secrets) · [Health checks in ASP.NET Core](https://learn.microsoft.com/aspnet/core/host-and-deploy/health-checks)

---

## 1. Where configuration comes from, and who wins

ASP.NET Core reads settings from several **configuration providers** and merges them into one big set of key/value pairs. With the default setup, they're read in this order, and **a later source overrides an earlier one**:

1. `appsettings.json`
2. `appsettings.{Environment}.json`, e.g. `appsettings.Development.json`
3. **User secrets**, only when the environment is `Development`
4. **Environment variables**
5. **Command-line arguments**

**Why this order makes sense:** the JSON files hold defaults that are committed to git. Each machine or deployment then overrides what it needs (connection strings, passwords) through environment variables, without changing the code. This is one of the "12-factor app" principles, a widely used set of guidelines for cloud apps: *store config in the environment*.

### Keys and the double underscore
Configuration is hierarchical. In JSON you nest objects; in code you join levels with a colon: `ConnectionStrings:FleetTrack`.

Environment variable names can't contain `:` on every operating system (Linux shells don't allow it). So .NET accepts **`__` (double underscore)** as a stand-in for `:`. The environment variable `ConnectionStrings__FleetTrack` sets the key `ConnectionStrings:FleetTrack`.

---

## 2. The options pattern

Reading `configuration["Some:Key"]` all over the code is fragile: typos, strings everywhere, no validation. The **options pattern** binds a configuration section to a small class:

```csharp
services.AddOptions<DatabaseOptions>()
        .BindConfiguration("Database")
        .ValidateDataAnnotations()
        .ValidateOnStart();
```

Then classes ask for it through dependency injection. There are three interfaces, and the difference is a common interview question:

| Interface | Lifetime | Sees config changes? | Use when |
| --- | --- | --- | --- |
| `IOptions<T>` | Singleton, read once | No | Settings that never change while running; this is the default choice |
| `IOptionsSnapshot<T>` | Scoped, re-read per request | Yes, on the next request | Web apps that need fresh values per request |
| `IOptionsMonitor<T>` | Singleton with change notifications | Yes, immediately | Long-lived services (background workers) that must react to changes |

### Validation and failing fast
You can validate options with data annotations (`[Required]`, `[Range]`), with a lambda (`.Validate(o => ...)`), or with a class implementing `IValidateOptions<T>`.

**`ValidateOnStart()` is the important part.** Without it, validation only happens the first time something reads the options, which might be ten minutes after deployment, on the first real request. With it, the app **refuses to start** if configuration is wrong. This principle is called **fail fast**: a deployment with a missing connection string should fail immediately and visibly, not half-work and fail later.

---

## 3. Secrets

**The rule: secrets (passwords, API keys, connection strings with passwords) never go into git.** Removing them in a later commit isn't enough: they stay in the git history forever, and attackers scan public repositories for leaked keys within minutes.

**User secrets** (`dotnet user-secrets set "Key" "value"`) are for **local development only**:
- Values are stored in a JSON file in your user profile, outside the project folder, so they can't be committed by accident.
- They are **not encrypted**. It's a convenience to keep secrets out of git, not a secure vault.
- They're only loaded when the environment is `Development`.

In **production**, use environment variables set by the hosting platform, or better, a dedicated secret store such as Azure Key Vault, AWS Secrets Manager or HashiCorp Vault, ideally accessed with a **managed identity** (the app proves who it is to the cloud without storing any password at all).

---

## 4. Health checks: liveness vs readiness

Platforms like Kubernetes regularly call your app's health endpoints and act on the answer. There are two different questions, and mixing them up causes outages.

| | **Liveness** | **Readiness** |
| --- | --- | --- |
| Question | "Is this process alive, or stuck?" | "Can this instance handle traffic right now?" |
| If it fails | The platform **restarts** the instance | The platform **stops sending it traffic** but leaves it running |
| Should it check the database? | **No** | **Yes** |

(Kubernetes also has a **startup probe**, which gives slow-starting apps time before liveness checks begin.)

### Why liveness must never check the database
Imagine the database goes down for one minute:
- If **liveness** checks the database, *every* instance fails liveness, and the platform restarts *all* of them, over and over. Restarting doesn't fix the database. Now you also have cold starts, lost in-flight requests and a "restart storm", and when the database comes back, all instances reconnect at once and may overload it.
- If only **readiness** checks the database, instances are just taken out of the load balancer until the database recovers, then put back in. Nothing restarts.

**Rule:** liveness checks only the process itself; readiness checks the dependencies the instance needs to do useful work.

### Practical details
- Responses: `Healthy` and `Degraded` return HTTP 200 by default; `Unhealthy` returns **503 (Service Unavailable)**.
- Use **tags** on checks and a **predicate** on each endpoint to choose which checks it runs. Liveness runs none, so it just answers "I'm alive".
- **Don't expose detailed health output publicly.** It reveals your internal architecture (database names, versions). Restrict it, or return only the status.
- Health checks run often, so keep them cheap. A `SELECT 1`, not a heavy query.

---

## Interview questions this prepares you for

- "An environment variable and `appsettings.json` both set the connection string. Which wins, and why is there a double underscore?"
- "`IOptions` vs `IOptionsSnapshot` vs `IOptionsMonitor`?"
- "Why use `ValidateOnStart`?"
- "Liveness vs readiness, and what happens if liveness checks the database?"
- "How do you manage secrets from a developer laptop all the way to production?"

## For FleetTrack

`DatabaseOptions` is validated on start. `/health/live` checks nothing; `/health/ready` checks Postgres. Both are wired up in `AddFleetTrackDefaults()` / `MapFleetTrackDefaults()`, which you'll compare with Aspire's version in M13.
