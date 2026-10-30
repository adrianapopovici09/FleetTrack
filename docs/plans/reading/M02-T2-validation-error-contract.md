# M02-T2 · Validation & the error contract

Task: [M02-T2](../modules/M02-api-design.md)

This file is the complete lesson for the task; you don't need to read anything else. Original sources, if you ever want to go deeper: [RFC 9457: Problem Details](https://www.rfc-editor.org/rfc/rfc9457) (sections 3–4) · [Handle errors in APIs](https://learn.microsoft.com/aspnet/core/fundamentals/error-handling-api) (`AddProblemDetails`, `IExceptionHandler`) · [Minimal API validation in .NET 10](https://learn.microsoft.com/aspnet/core/fundamentals/minimal-apis#validation-support-in-minimal-apis)

**Reading time:** about 60 minutes. **Builds on:** [M02-T1](M02-T1-minimal-apis-openapi.md) (§2 HTTP semantics and status codes, §6 `TypedResults` unions, §13 endpoint tests), [M00-T5](M00-T5-configuration-health-checks.md) (fail fast, options validation), [M01-T1](M01-T1-async-await.md) (exceptions stored in tasks) and [M01-T3](M01-T3-cancellation.md) (`OperationCanceledException` is not an error).

## Contents

1. [Why errors are the hardest part of a contract](#1-why-errors-are-the-hardest-part-of-a-contract)
2. [What a client needs from an error](#2-what-a-client-needs-from-an-error)
3. [Status codes for errors, and retryability](#3-status-codes-for-errors-and-retryability)
4. [Problem Details (RFC 9457)](#4-problem-details-rfc-9457)
5. [The `type` URI: designing an error catalogue](#5-the-type-uri-designing-an-error-catalogue)
6. [Validation: where each kind of rule belongs](#6-validation-where-each-kind-of-rule-belongs)
7. [Built-in validation in .NET 10 Minimal APIs](#7-built-in-validation-in-net-10-minimal-apis)
8. [The error pipeline in ASP.NET Core](#8-the-error-pipeline-in-aspnet-core)
9. [`IExceptionHandler`: the last line of defence](#9-iexceptionhandler-the-last-line-of-defence)
10. [Expected failures: return them, don't throw them](#10-expected-failures-return-them-dont-throw-them)
11. [Errors, logs and traces: one id to connect them](#11-errors-logs-and-traces-one-id-to-connect-them)
12. [Security: what never goes in an error](#12-security-what-never-goes-in-an-error)
13. [Documenting and evolving the error contract](#13-documenting-and-evolving-the-error-contract)
14. [Testing errors](#14-testing-errors)
15. [When *not* to do it this way](#15-when-not-to-do-it-this-way)
16. [Pitfalls](#16-pitfalls)
17. [Glossary](#17-glossary)
18. [Summary, check yourself, interview questions, for FleetTrack](#18-summary)

---

## 1. Why errors are the hardest part of a contract

A warehouse partner integrates with FleetTrack. On the happy path everything works: they post a shipment, they get `201` and the JSON back. Then reality arrives:

- One endpoint answers a bad date with `400` and the plain text `Invalid date`.
- Another answers a missing field with `{ "message": "Origin is required" }`.
- A third, after a database timeout, answers with an HTML page containing a .NET stack trace, the SQL query and the server's file paths.
- Next month a developer fixes a typo in a message (`"Orgin"` → `"Origin"`), and the partner's code, which matched on the text, stops recognising the error.

Each of these is a small decision someone made alone. Together they mean every client must write special-case parsing for every endpoint, can't tell "fix your input" from "try again later", and breaks whenever you change wording. Meanwhile your server leaks its internals to anyone who can trigger an error.

On the happy path every client does the same thing: read the body. **Errors are where integrations break, support tickets come from, and security leaks happen.** A senior engineer designs them **once**, as part of the contract, and makes the framework enforce the design everywhere, including for errors the framework produces before your code runs.

---

## 2. What a client needs from an error

Think about the code the client writes. When a request fails, it must answer four questions, ideally without a human:

| Question | Example | Must be |
| --- | --- | --- |
| **What kind of problem is this?** | validation failed / not found / already dispatched / server overloaded | **machine-readable and stable**, never parsed from English text |
| **Can I retry, and when?** | yes after 30 s / no, fix the request | derivable from the status code (+ `Retry-After`) |
| **What exactly is wrong?** | `pickupAt` is in the past; shipment `0199f…` is already dispatched | specific: field names, ids |
| **How do I get help?** | "quote `traceId` 00-4bf92f… to support" | a correlation id that links to your logs |

The rest of this lesson builds a contract that answers all four in the **same format everywhere**.

---

## 3. Status codes for errors, and retryability

The status code is the coarsest part of the answer, and the one every HTTP library, proxy and retry policy understands. Use the standard meanings, because intermediaries act on them.

### 3.1 The codes you'll actually use

| Code | Name (RFC 9110) | Meaning | Retry? | In FleetTrack |
| --- | --- | --- | --- | --- |
| `400` | Bad Request | The request is malformed: unreadable JSON, wrong types, failed shape validation | **No**, fix it | T2 |
| `401` | Unauthorized | Not authenticated (no/invalid token) | after re-authenticating | M11 |
| `403` | Forbidden | Authenticated, but not allowed | No | M11 |
| `404` | Not Found | No such resource (or one you may not know exists, §12) | No | T2 |
| `405` | Method Not Allowed | Path exists, method doesn't | No | framework |
| `409` | Conflict | Conflicts with the current state (duplicate reference, invalid transition) | No, re-read state first | T2, M04 |
| `412` | Precondition Failed | `If-Match` didn't match: someone changed it since you read it | after re-reading | T4 |
| `415` | Unsupported Media Type | Body isn't JSON | No | framework |
| `422` | Unprocessable Content | Well-formed, but semantically can't be processed | No | T5 |
| `428` | Precondition Required | You must send `If-Match` | add the header | T4 |
| `429` | Too Many Requests | Rate limited | **Yes**, after `Retry-After` | M11 |
| `500` | Internal Server Error | A bug or unexpected failure | maybe, with backoff | §9 |
| `502` / `503` / `504` | Bad Gateway / Service Unavailable / Gateway Timeout | A dependency or the service itself is down or slow | **Yes**, with backoff | M01-T3's 504, M09 |

### 3.2 The retry rule clients actually implement

```
4xx  → the request is wrong. Retrying it unchanged will fail the same way. (Exceptions: 408, 429.)
5xx  → the server failed. The same request may succeed later: retry with exponential backoff + jitter,
       and only if the operation is idempotent (T1 §2) or carries an Idempotency-Key (T5).
```

This is why **getting the class right matters more than getting the exact code right.** Returning `500` for a validation failure makes well-behaved clients retry a request that can never succeed, multiplying load during exactly the moments you're already in trouble. Returning `400` for a database outage makes clients give up on requests that would have worked a second later.

### 3.3 `400` vs `422`, and `409` vs `422`

These are judgement calls, so pick a convention and **write it down** (ADR-004 in T8):
- A common, simple convention, and ASP.NET Core's default: **`400` for anything wrong with the request's shape** (including validation), **`409` for conflicts with the current state** (business rules that depend on stored data), `422` for requests that are well-formed but can't be processed for a reason that isn't a state conflict (T5 uses it for "same idempotency key, different body").
- Some APIs use `422` for all validation. That's equally defensible; mixing both styles in one API is not.

---

## 4. Problem Details (RFC 9457)

### 4.1 History in two lines

Every company used to invent its own error JSON (`{ "error": … }`, `{ "message": … }`, `{ "errors": [...] }`). In 2016 the IETF standardised one: **RFC 7807, Problem Details for HTTP APIs**. In 2023 **RFC 9457** replaced it. The format is the same; 9457 clarifies how to report multiple problems, recommends a registry of common problem types, and tightens the wording on `type` URIs. ASP.NET Core has produced this format since .NET Core 2.1, so you rarely write it by hand.

### 4.2 The format

Problem Details uses its own media type, **`application/problem+json`**, so a client can recognise an error body from the `Content-Type` header alone, before parsing it:

```http
HTTP/1.1 409 Conflict
Content-Type: application/problem+json

{
  "type": "https://fleettrack.dev/errors/shipment-already-dispatched",
  "title": "Shipment already dispatched",
  "status": 409,
  "detail": "Shipment 0199f1c2-… was dispatched at 2026-10-30T09:12:00Z and can no longer be edited.",
  "instance": "/api/v1/shipments/0199f1c2-…",
  "traceId": "00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-01",
  "dispatchedAt": "2026-10-30T09:12:00Z"
}
```

| Member | Meaning | Rules |
| --- | --- | --- |
| `type` | A URI identifying the **kind** of problem | Stable forever; clients switch on it. If absent, it means `about:blank`: "nothing beyond what the status code says" |
| `title` | A short human summary **of the type** | Same text for every occurrence of that type; no ids in it |
| `status` | The HTTP status code, repeated | Must equal the real status (it survives when the body is logged or forwarded alone) |
| `detail` | A human explanation **of this occurrence** | May include ids and values; clients must **never** parse it |
| `instance` | A URI for this occurrence | Often the request path, or a link to an error record |
| *extension members* | Anything else you add | `traceId`, `errors` (field errors), `dispatchedAt`… Clients **must ignore** extensions they don't know, so adding one is not a breaking change |

**Why a standard rather than your own shape?** Client libraries, API gateways, SDK generators and log tools already understand it; ASP.NET Core produces it for its own errors; and nobody spends a design review arguing about field names. "Use the standard unless you have a reason" is itself a senior habit.

---

## 5. The `type` URI: designing an error catalogue

### 5.1 It's an enum that crosses the network

The `type` is what client code branches on:

```csharp
// a partner's client, written against FleetTrack's documented types
switch (problem.Type)
{
    case "https://fleettrack.dev/errors/validation":                  ShowFieldErrors(problem);    break;
    case "https://fleettrack.dev/errors/shipment-already-dispatched": RefreshAndExplain(problem); break;
    case "https://fleettrack.dev/errors/concurrency-conflict":        ReloadAndRetry();           break; // T4
    default:                                                          ShowGeneric(problem.Title); break;
}
```

That gives it the same rules as a public enum:
- **Never rename or reuse a type.** Changing a `type` URI is a **breaking change** (T6), even though the status code and the HTTP shape didn't change.
- **Adding a new type is a soft change:** old clients fall into their `default` branch. That's acceptable only if your documentation tells clients to have one. Say so in `errors.md`.

### 5.2 How many types?

**One type per distinct client reaction**, not per line of code that can fail.

```
Too few:   everything is about:blank + status code  → clients parse detail text (fragile)
Too many:  reference-too-long, reference-too-short,
           reference-has-spaces …                    → nobody handles them; they all mean "fix the field"
Right:     validation (with field errors in "errors"), not-found, shipment-already-dispatched,
           concurrency-conflict, idempotency-key-reused, rate-limited, internal
```

A useful test: if two types would make the client do the same thing, merge them. If one type makes clients read `detail` to decide what to do, split it.

### 5.3 The URI itself

- Use **absolute** URIs (`https://fleettrack.dev/errors/not-found`). A relative one is resolved against the request URL, which means something different behind a gateway.
- The URI **should** lead to a human-readable page describing the error and what to do (RFC 9457 recommends it, but clients must not *require* it to resolve). Your `docs/api/errors.md` is that page until a docs site exists.
- Keep them as constants in one C# class (for example `ErrorTypes`), so code and documentation can be compared in a review and a typo can't create a new "type".

---

## 6. Validation: where each kind of rule belongs

This is quiz Q8 and one of the most common design-review questions.

### 6.1 Defence in depth

Validation isn't one check in one place; it's layers, each catching a different kind of problem, each with a different cost and a different error:

```
  request ──► 1. Parsing / binding        Is it JSON? Can "abc" become an int?          → 400 (framework)
                    │
                    ▼
              2. Shape validation          Required fields, lengths, ranges, formats     → 400 + field errors
                 (API boundary, on the     no database needed                              (this task)
                  request DTO)
                    │
                    ▼
              3. Application checks        Does the referenced vehicle exist?            → 404 / 409
                 (handler / use case)      Is the caller allowed? (M11)
                    │
                    ▼
              4. Domain invariants          A delivered shipment can't be reassigned;     → 409 / 422
                 (aggregates, value         pickup before delivery; weight ≤ capacity       (M04)
                  objects, M04)
                    │
                    ▼
              5. Database constraints       NOT NULL, UNIQUE (reference per tenant),      → 409 (translate!)
                                            foreign keys, CHECK                             (M03, T5)
```

Each layer exists because the layers above it can be **bypassed**:
- **Shape validation** protects the endpoint and gives good field-level messages, but only for requests arriving through *this* endpoint.
- **Domain invariants** hold whatever the entry point: the API, a message consumer (M06), a saga (M07), an admin script. If "pickup before delivery" lives only in an API attribute, the first message consumer that creates shipments bypasses it. **Architecture rule 4: business rules live in the domain.**
- **Database constraints** are the last guarantee against bugs and races. Two concurrent requests can both pass a "reference is unique" check in code and then both insert (check-then-act, quiz Q7 and T7). Only a `UNIQUE` constraint stops the second. Your job at this layer is to **translate** the constraint violation into a proper `409`, not let it surface as a `500`.

### 6.2 Shape rules vs business rules

| | **Shape (input) validation** | **Business-rule (domain) validation** |
| --- | --- | --- |
| Question | Is this request well-formed? | Is this operation allowed now? |
| Examples | `reference` required, 3–32 chars; `weightKg` between 0.1 and 40,000; valid email | can't edit a dispatched shipment; vehicle has capacity; driver isn't double-booked |
| Needs stored data? | no | often yes |
| Lives | on the request DTO, at the API boundary | in the domain model (M04) |
| Changes when | the API contract changes | the business changes |
| Error | `400` with an `errors` map per field | `409`/`422` with a specific `type` |

**The grey zone:** "pickup before delivery" uses only fields of the same request, so it *could* be a shape rule. Check it in both places: a cheap check at the boundary gives a nice field error, and the domain enforces the real guarantee. If someone removes one, it must be the boundary check, never the domain's.

---

## 7. Built-in validation in .NET 10 Minimal APIs

### 7.1 What changed in .NET 10

Controllers have had automatic validation since `[ApiController]` (2018). Minimal APIs had **none** until .NET 10: teams either validated by hand in every handler or added FluentValidation plus a filter. .NET 10 adds built-in validation based on the long-standing **DataAnnotations** attributes (`System.ComponentModel.DataAnnotations`), with a **source generator** that produces the validation code at compile time. The core validation types live in the `Microsoft.Extensions.Validation` package, so the same rules can be reused outside HTTP.

```csharp
builder.Services.AddValidation();       // turn it on for Minimal API endpoints

public sealed record CreateShipmentRequest(
    [property: Required, StringLength(32, MinimumLength = 3)] string Reference,
    [property: Required] string Origin,
    [property: Required] string Destination,
    [property: Range(0.1, 40_000)] decimal WeightKg,
    DateTimeOffset PickupAt);
```

(Whether you need the `property:` target on a positional record's attributes depends on how the generator discovers them; check the current docs and the generated OpenAPI. The point is that the rules sit on the request type, next to the contract.)

### 7.2 How it behaves

- It validates **bound parameters**: body records, `[AsParameters]` types, route and query values, including **nested objects and collections**.
- When validation fails, **your handler is not called.** The response is `400` with a validation problem body (an `errors` dictionary: field → messages), written through the Problem Details service (§8), so your customisation applies.
- **Cross-field rules:** implement `IValidatableObject` on the request type, or write a custom `ValidationAttribute`.
- **Opt out** per endpoint with `.DisableValidation()` (rarely needed).
- Because the code is generated at **compile time**, there's no reflection cost per request, and it works with Native AOT (T1 §14).

```http
HTTP/1.1 400 Bad Request
Content-Type: application/problem+json

{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.1",
  "title": "One or more validation errors occurred.",
  "status": 400,
  "errors": {
    "Reference": [ "The field Reference must be a string with a minimum length of 3 and a maximum length of 32." ],
    "WeightKg":  [ "The field WeightKg must be between 0.1 and 40000." ]
  }
}
```

### 7.3 The parts that are your decision

- **The `type`.** The default points at the HTTP RFC's section for `400`. You want your own stable URI (`https://fleettrack.dev/errors/validation`); set it in one place (§8.2).
- **The keys in `errors`.** Check whether they come out as `Reference` (C# name) or `reference` (JSON name). Clients will map these keys to form fields, so they're part of the contract; prefer the JSON names clients actually send.
- **The messages.** Default messages are English and mention C# names. They're for developers, not end users. If a front end shows them to users, it should translate by key (field + rule), not display them raw. Don't promise message text in the contract.

### 7.4 Alternatives

- **FluentValidation** (Apache-2.0): rules in separate classes, fluent syntax, easy to unit-test, good for complex conditional rules. Fine to discuss; for FleetTrack the built-in option plus domain invariants is enough, and complex rules belong in the domain anyway.
- **Hand-written `if`s in each handler:** inconsistent formats, missed fields, invisible to OpenAPI. This is the option to argue against.

---

## 8. The error pipeline in ASP.NET Core

### 8.1 Where errors come from

Errors reach the client from several places, and **all of them** must produce the same format:

```
                 ┌─────────────────────────────────────────────────────────────────┐
  request ──►    │ UseExceptionHandler  ◄────────── catches exceptions from below   │──► 500 problem
                 │ UseStatusCodePages   ◄────────── adds a body to empty 4xx/5xx    │──► 404/405 problem
                 │   routing: no match → empty 404 / 405                           │
                 │   binding: bad JSON / bad types → 400                           │──► 400 problem
                 │   validation (.NET 10): failed rules → 400 + errors             │──► 400 problem
                 │   YOUR HANDLER: TypedResults.Problem(...) for expected failures │──► 404/409 problem
                 │                 an unexpected exception is thrown ──────────────┼─┐
                 └─────────────────────────────────────────────────────────────────┘ │
                       ▲                                                             │
                       └─────────────────────────────────────────────────────────────┘
                               All of them write through IProblemDetailsService
```

### 8.2 `AddProblemDetails`: one service, one customisation point

```csharp
builder.Services.AddProblemDetails(options =>
{
    options.CustomizeProblemDetails = ctx =>
    {
        ctx.ProblemDetails.Instance = $"{ctx.HttpContext.Request.Method} {ctx.HttpContext.Request.Path}";
        ctx.ProblemDetails.Extensions["traceId"] = Activity.Current?.Id ?? ctx.HttpContext.TraceIdentifier;
        // map framework defaults to your catalogue, e.g. status 400 with an errors map → ErrorTypes.Validation
    };
});
```

`AddProblemDetails()` registers **`IProblemDetailsService`**, which the framework's components (exception handler, status code pages, validation, `TypedResults.Problem`) use to write `application/problem+json`. `CustomizeProblemDetails` runs for **all of them**, so it's the single place for cross-cutting additions: the trace id, the instance, the mapping of default types to yours. One place means it can't be forgotten on one path.

### 8.3 Middleware order

```csharp
var app = builder.Build();

app.UseExceptionHandler();      // FIRST: so it wraps everything that runs after it
app.UseStatusCodePages();       // turns bodiless error responses into problem JSON
app.UseHttpsRedirection();
// … auth (M11) …
app.MapShipmentEndpoints();
```

**Middleware** is the chain of components every request passes through before it reaches an endpoint (exception handling, HTTPS redirection, authentication…). Each one can act before and after the rest of the chain, and they run **in the order you call `app.Use…()`**. An exception handler can only catch exceptions thrown by what runs **after** it, so register it first. `Map…` calls are different: endpoints always run at the end of the chain, wherever the call sits in `Program.cs`. That's why FleetTrack's current `app.UseHttpsRedirection()` *after* the `Map…` calls still works, but reads misleadingly: put `Use…` calls before `Map…` calls so the file reads in pipeline order.

### 8.4 The empty `404` trap

A routing miss (`GET /api/v1/shipmentz`) or a bare `TypedResults.NotFound()` produces a `404` **with no body**. Without `UseStatusCodePages()`, the client receives an empty response, not `application/problem+json`, and your "every error is Problem Details" promise is broken by the framework itself. Two complementary fixes:
- `UseStatusCodePages()` gives every bodiless error a generic problem body.
- For expected cases in your own handlers, return an explicit, informative problem: `TypedResults.Problem(type: ErrorTypes.NotFound, title: "Shipment not found", statusCode: 404, detail: $"No shipment with id {id}.")`.

### 8.5 The developer exception page

In Development, ASP.NET Core shows a detailed exception page (stack trace, request details). It's useful on your machine and dangerous anywhere else. `UseExceptionHandler()` + Problem Details is the production behaviour. Your tests must run with `UseEnvironment("Testing")` (M00-T6) so they check what production does, not what Development does.

---

## 9. `IExceptionHandler`: the last line of defence

### 9.1 What an unhandled exception means

An exception that escapes your handler is **by definition unexpected**: a bug (`NullReferenceException`), an outage (the database is down), a timeout. The goals, in order:
1. **Log it once**, with full detail and the trace id, at `Error` level.
2. **Return a generic `500`** in the standard format, with **no internals**.
3. **Give the client the trace id** so a support request leads straight to the log line.

### 9.2 Writing one

```csharp
internal sealed class UnhandledExceptionHandler(
    IProblemDetailsService problemDetails,
    ILogger<UnhandledExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext http, Exception exception, CancellationToken ct)
    {
        logger.LogError(exception, "Unhandled exception for {Method} {Path}", http.Request.Method, http.Request.Path);

        http.Response.StatusCode = StatusCodes.Status500InternalServerError;
        return await problemDetails.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = http,
            Exception = exception,
            ProblemDetails =
            {
                Type = ErrorTypes.Internal,
                Title = "An unexpected error occurred.",
                Status = StatusCodes.Status500InternalServerError,
            },
        });
    }
}

builder.Services.AddExceptionHandler<UnhandledExceptionHandler>();
```

### 9.3 Chaining handlers

You can register several `IExceptionHandler`s. They run **in registration order** until one returns `true`. That lets you put specific translations before the generic one:

```
AddExceptionHandler<ClientCancelledHandler>()       // OperationCanceledException + RequestAborted → stop quietly
AddExceptionHandler<ConcurrencyExceptionHandler>()  // DbUpdateConcurrencyException → 412 (T4)
AddExceptionHandler<UniqueViolationHandler>()       // Postgres 23505 → 409 (T5, M03)
AddExceptionHandler<UnhandledExceptionHandler>()    // everything else → 500
```

### 9.4 Client cancellation is not a server error

From M01-T3: when the client disconnects, `RequestAborted` fires and an `OperationCanceledException` travels up. Logging it as an `Error` floods your logs and alerts with noise every time a user closes a tab. Check `http.RequestAborted.IsCancellationRequested`: if the client left, log at `Debug`/`Information` (or not at all) and don't write a body nobody will read. A cancellation caused by **your own** timeout (M01-T3's linked token) is different: that's a `504`, and it *is* worth knowing about.

### 9.5 Exceptions and `async`

From M01-T1: an exception in an `async` method is stored in the returned `Task` and rethrown at `await`, so it reaches the exception handler normally as long as everything is awaited. The exceptions that **don't** arrive are the ones in fire-and-forget work (`_ = DoSomethingAsync()`) or `async void`, which bypass the pipeline entirely (and `async void` crashes the process, quiz M01 Q7). Another reason not to write them in request code.

---

## 10. Expected failures: return them, don't throw them

"Shipment not found" and "shipment already dispatched" are not exceptional: they're normal business outcomes that happen every day. There are two ways to model them:

```csharp
// A. Exceptions: throw deep down, translate centrally (an IExceptionHandler maps them to 404/409)
var shipment = await repo.GetAsync(id, ct) ?? throw new ShipmentNotFoundException(id);
shipment.Dispatch(vehicle);   // throws InvalidTransitionException

// B. Results: the outcome is a return value, mapped to HTTP at the edge
var shipment = await repo.FindAsync(id, ct);
if (shipment is null)
    return TypedResults.Problem(type: ErrorTypes.NotFound, title: "Shipment not found",
                                statusCode: StatusCodes.Status404NotFound, detail: $"No shipment with id {id}.");
```

| | Exceptions for expected failures | Returned results |
| --- | --- | --- |
| Visible in the signature? | no: any call might throw anything | yes: the return type lists the outcomes; with `TypedResults`, OpenAPI does too |
| Cost | stack-trace capture; a flood of 404s under load costs real CPU | an ordinary return |
| Control flow | jumps across layers; easy to catch in the wrong place, or swallow | explicit and local |
| Amount of code | small: throw and forget | more plumbing, especially through several layers |
| Risk | a "business" exception you forgot to map becomes a `500` | a result you forgot to check is ignored (mitigated by types) |

The widely used rule: **exceptions for the exceptional** (bugs, broken infrastructure, violated programming assumptions), **results for expected business outcomes**. M04 introduces a `Result` type for the domain, and **ADR-004** (T8) records the decision, including where the line sits. For T2, it's enough to return `TypedResults.Problem(...)` for expected cases in handlers and keep `IExceptionHandler` for the unexpected.

---

## 11. Errors, logs and traces: one id to connect them

A support ticket says: "Creating shipment RO-1042 failed yesterday afternoon." Without a correlation id, someone searches logs by time and guesses. With one, the partner pastes `traceId: 00-4bf92f…`, and you find the exact log lines and, from M12, the full distributed trace across the gateway, the services and the broker.

- Use the **W3C trace context** id (`Activity.Current?.Id`, the same value that travels in the `traceparent` header) rather than inventing your own; M12's OpenTelemetry setup uses it automatically. `HttpContext.TraceIdentifier` is a fallback.
- Put it in **every** error body (§8.2 does this once for all errors) and in every log line (structured logging scopes do this).
- **Log each failure once, at the right level.** `4xx` caused by clients are mostly `Information`/`Warning` (useful in aggregate, not individually alarming). `5xx` are `Error`. Logging the same exception in the repository, the service and the handler triples your log volume and makes counting incidents impossible.

---

## 12. Security: what never goes in an error

**Never in a response:**
- **Stack traces, exception messages, SQL, file paths, server names, library versions.** They map your system for an attacker (OWASP: security misconfiguration). `exception.Message` from Npgsql can contain table names and query fragments; don't put it in `detail`.
- **Secrets**, obviously, but notice how easily they leak: a connection-string parsing error can echo the whole string.
- **Existence information the caller isn't entitled to.** From M11 on, "the shipment exists but belongs to another tenant" must be indistinguishable from "it doesn't exist": same status (`404`, not `403`), same body, ideally similar timing. Otherwise an attacker can enumerate other tenants' ids. Keep this in mind when you write the not-found `detail` now.
- **Internal names** (C# class names, enum member names) in `type` or `title`. They couple clients to your code.

**Always in a response:** the `traceId`, so the safe, generic message is still debuggable from your side.

**Validation errors are a mild exception:** echoing *which field* failed and *why* is necessary and fine. Echoing the full submitted *value* back is usually unnecessary, and with HTML front ends it can become a cross-site scripting vector; prefer describing the rule.

---

## 13. Documenting and evolving the error contract

### 13.1 The catalogue

`docs/api/errors.md` is the human page behind your `type` URIs, one row per type:

| `type` | Status | Title | When | Client should |
| --- | --- | --- | --- | --- |
| `…/errors/validation` | 400 | Validation failed | shape rules failed; details in `errors` | fix the listed fields; don't retry unchanged |
| `…/errors/not-found` | 404 | Resource not found | unknown id (or not visible to you) | stop; check the id |
| `…/errors/shipment-already-dispatched` | 409 | … | … | … |
| `…/errors/internal` | 500 | Unexpected error | bug or outage | retry later with backoff; report `traceId` |

The **"client should"** column is what makes it useful. Writing it is also how you discover design mistakes: two types with the same reaction (merge them), or one type whose reaction depends on `detail` (split it). T4 and T5 will add rows (`412`, `428`, `422`); M11 adds `401`, `403`, `429`.

### 13.2 What counts as a breaking change to errors

| Change | Breaking? | Why |
| --- | --- | --- |
| Add an extension member (`traceId`) | no | clients must ignore unknown members |
| Change `title` or `detail` wording | no, **if** documented as human-only | clients must not parse them; say so in `errors.md` |
| Add a new `type` | soft | old clients hit their `default` branch; document that they need one |
| Rename or remove a `type` | **yes** | client `switch` statements stop matching |
| Change the status code for a type | **yes** | retry and error-handling logic changes meaning |
| Change the `errors` keys (e.g. `Reference` → `reference`) | **yes** | clients map keys to form fields |

That last row is why you decide the key casing **now**, before T6's snapshot freezes it.

---

## 14. Testing errors

Test the **contract**, not the implementation: status code, media type, `type`, and the important extension members.

```csharp
[Test]
public async Task Invalid_body_returns_validation_problem_with_field_errors()
{
    var response = await _client.PostAsJsonAsync("/api/v1/shipments", new { reference = "" });

    Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
    Assert.That(response.Content.Headers.ContentType?.MediaType, Is.EqualTo("application/problem+json"));

    var problem = await response.Content.ReadFromJsonAsync<HttpValidationProblemDetails>();
    Assert.That(problem!.Type, Is.EqualTo(ErrorTypes.Validation));
    Assert.That(problem.Errors, Does.ContainKey("reference"));      // whichever casing you decided
    Assert.That(problem.Extensions, Does.ContainKey("traceId"));
}
```

Ideas for a complete set, one test per catalogue row plus the framework's own paths:
- **Routing miss** (`/api/v1/does-not-exist`): proves even framework errors are problem JSON (§8.4).
- **Malformed JSON** (a body that isn't JSON at all): binding failure, not validation; is it still your format?
- **Unexpected exception**: a test-only endpoint, or a fake service registered in `WebApplicationFactory` that throws. Assert `500`, your `internal` type, a `traceId`, and that the body does **not** contain the exception message. That last assertion is the one that catches a leak.
- A test you've never seen fail proves nothing (from M00-T6): break each one once, for example by removing `UseStatusCodePages()`, and watch it go red.

---

## 15. When *not* to do it this way

- **Don't build a custom error format** when Problem Details fits. The only common reason to deviate is matching an existing, published contract you can't change; then wrap it, document it, and plan the migration.
- **Don't put business rules in DataAnnotations** because it's convenient. Attributes are for the shape of *this* request; the domain is for rules that must hold everywhere.
- **Don't create a `type` per validation rule.** One `validation` type with an `errors` map is what clients can actually handle.
- **Don't translate every exception into a nice `4xx`.** An exception you didn't expect is a bug; turning it into a `400` hides it from your alerts and blames the client. Let it become a `500`, and fix the cause.
- **Don't use exceptions for control flow in hot paths** (for example, "not found" on a high-traffic lookup). The cost is measurable under load.
- **Internal service-to-service calls** (M09) still benefit from Problem Details, but gRPC has its own status model; don't force HTTP error semantics onto it.

---

## 16. Pitfalls

| Pitfall | What goes wrong | Fix |
| --- | --- | --- |
| Clients parse `title`/`detail` | A wording fix breaks integrations | Stable `type` URIs; document text as human-only |
| No `UseStatusCodePages()` | Routing `404`s and bare `NotFound()`s have empty bodies | Add it, and return explicit problems in handlers |
| `UseExceptionHandler()` registered late | Exceptions in earlier middleware escape as raw errors | Register it first |
| `exception.Message` in `detail` | SQL, table names, paths leak | Generic title + `traceId`; details in logs only |
| Logging client cancellations as `Error` | Alert noise every time a user closes a tab | Check `RequestAborted`; log at a low level |
| `500` for validation failures | Clients retry requests that can never succeed | `4xx` for client errors, always |
| Rules only in DataAnnotations | Message consumers and jobs bypass them | Invariants in the domain (M04) |
| Uniqueness checked only in code | Races create duplicates (Q7, T7) | `UNIQUE` constraint + translate to `409` |
| Same exception logged at every layer | Triple log volume, uncountable incidents | Log once, where it's handled |
| `errors` keys in C# casing while JSON is camelCase | Clients can't map errors to fields | Decide the casing; test it |
| Tests run in `Development` | The developer exception page hides real behaviour | `UseEnvironment("Testing")` |

---

## 17. Glossary

| Term | Meaning |
| --- | --- |
| **Error contract** | The documented format, status codes and types of every error an API can return |
| **Problem Details** | RFC 9457's JSON error format (formerly RFC 7807), media type `application/problem+json` |
| **`type` URI** | Stable identifier for a kind of problem; what clients branch on |
| **Extension member** | Any extra field in a problem body (`errors`, `traceId`); clients ignore unknown ones |
| **Error catalogue** | The documented list of `type`s, with when they occur and what the client should do |
| **Retryable error** | One where the same request may succeed later (`429`, `503`, `504`); retry with backoff + jitter |
| **Shape / input validation** | Checking a request is well-formed; at the API boundary |
| **Invariant** | A business rule that must always hold for a domain object; enforced in the domain (M04) |
| **Defence in depth** | Several validation layers, each catching what the others can miss |
| **Check-then-act race** | Two requests both pass a check, then both act; only a database constraint stops it |
| **DataAnnotations** | `System.ComponentModel.DataAnnotations` attributes (`[Required]`, `[Range]`…) used by .NET 10 validation |
| **`IProblemDetailsService`** | The ASP.NET Core service that writes problem bodies; customised once via `CustomizeProblemDetails` |
| **`IExceptionHandler`** | A handler for unhandled exceptions, invoked by `UseExceptionHandler()`; several run in registration order |
| **Status code pages** | Middleware that adds a body to bodiless error responses |
| **Result pattern** | Returning success/failure as a value instead of throwing (M04, ADR-004) |
| **Trace id / correlation id** | An id shared by the error body, the logs and (M12) the distributed trace |

---

## 18. Summary

1. Errors are where integrations break and internals leak. Design them **once**, as part of the contract, and make the framework enforce it on **every** path, including routing, binding and unhandled exceptions.
2. The status-code **class** drives client behaviour: `4xx` means "fix your request", `5xx` means "try again later, if it's idempotent". Getting the class wrong causes useless retries or abandoned good requests.
3. **Problem Details** (`application/problem+json`, RFC 9457) is the standard format; the **`type` URI** is a public enum: one per client reaction, never renamed.
4. Validation is **defence in depth**: binding → shape rules on the DTO (.NET 10 `AddValidation()` → `400` + `errors`) → application checks → **domain invariants** (M04) → **database constraints** translated to `409`.
5. `AddProblemDetails()` + `CustomizeProblemDetails` is the single customisation point; `UseExceptionHandler()` goes first; `UseStatusCodePages()` fills empty error bodies.
6. `IExceptionHandler`s chain in registration order; the last one turns the unexpected into a generic `500`, logged once, without leaking internals. Client cancellations aren't errors.
7. Expected failures are **returned**, unexpected ones **thrown**; ADR-004 records where the line is.
8. Every error carries a **trace id**, and the catalogue in `docs/api/errors.md` says what clients should do.

### Check yourself

1. A client's retry policy retries every `5xx`. You return `500` for "reference already exists". What happens, and what should you return instead? (§3)
2. What does a client get from `type` that it can't safely get from `title`, `detail` or the status code? (§4, §5)
3. Which of these are breaking: adding `traceId`, rewording `title`, adding a new `type`, changing a type's status from `409` to `422`, changing `errors` keys from `Reference` to `reference`? (§13.2)
4. A bare `TypedResults.NotFound()` is returned. What does the client receive with and without `UseStatusCodePages()`? (§8.4)
5. "Pickup before delivery" is enforced with a custom `ValidationAttribute` only. How does M06 bypass it? (§6)
6. Two requests with the same reference arrive at the same moment, and your code checks for duplicates before inserting. What happens, and which layer actually prevents the duplicate? (§6.1)
7. Two `IExceptionHandler`s are registered. Which runs first, and when does the second run? (§9.3)
8. A user closes the browser tab mid-request. What should be logged, and what should be written to the response? (§9.4)
9. Name three things that must never appear in a production error body, and one thing that always should. (§12)

**Before you build: decide and note down** (each is a contract decision you'll explain in review, and most end up in ADR-004):
- Which `type` URIs exist, and what should the client do for each (the rows of `docs/api/errors.md`)?
- `400` for all shape validation, or `422`? And `409` for state conflicts?
- Casing of the `errors` keys: C# names or JSON names?
- Which shipment rules are shape rules (attributes on the DTO) and which are business rules (they wait for the domain in M04)?
- Expected failures in handlers: `TypedResults.Problem(...)` or exceptions translated by a handler?
- What goes in every error body (`traceId`, `instance`), and what must never appear?

### Interview questions this prepares you for

- "How do you design error responses for a public API?"
- "What's RFC 7807/9457, and why use it instead of your own format?"
- "Where do you put validation in a layered / DDD application? What about uniqueness?"
- "Exceptions or result types for business errors? What are the trade-offs?"
- "`400` vs `422` vs `409`: how do you choose?"
- "Which errors should a client retry?"
- "How does a support engineer get from a customer's error report to the failing log line?"
- "What information must you never expose in an error response, and why?"

### For FleetTrack

- **Where things go (rule 1: API → Application → Domain):** `AddProblemDetails()`, `AddValidation()` and the exception handler registration go in `AddFleetTrackDefaults()` next to `AddOpenApi()`; the `IExceptionHandler` class lives in the API project; the `ErrorTypes` constants sit in the API project too (they're HTTP vocabulary), and `docs/api/errors.md` mirrors them with a **"client should"** column.
- **Pipeline order in `Program.cs`:** `UseExceptionHandler()` → `UseStatusCodePages()` → `UseHttpsRedirection()` → `MapShipmentEndpoints()` / `MapFleetTrackDefaults()`.
- **Validation:** DataAnnotations on the T1 request records in `src/FleetTrack.API/Endpoints/` for **shape** rules only; business rules wait for the domain in M04 (rule 4).
- **Handlers:** the `Results<...>` unions from T1 grow to include the problem results, so `/scalar` documents the errors too.
- **Cancellation:** the exception handler treats a client disconnect as a non-error (M01-T3).
- **Tenancy ahead:** the not-found `detail` must reveal nothing that M11's tenant isolation would forbid.
- **The ✅ check:** tests in `FleetTrack.IntegrationTests` show invalid body → `400` with field errors, unknown id → `404`, and every error is `application/problem+json` with a documented `type`.
- **Coming next:** T3 keyset pagination adds an `invalid-cursor` error, T4 adds `412`/`428` for ETags, T5 adds `422` for reused idempotency keys, and ADR-004 in T8 records the choices you made here.
