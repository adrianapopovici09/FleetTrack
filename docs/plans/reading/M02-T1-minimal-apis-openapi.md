# M02-T1 · Minimal API endpoints and OpenAPI

Task: [M02-T1](../modules/M02-api-design.md)

This file is the complete lesson for the task; you don't need to read anything else. Original sources, if you ever want to go deeper: [Minimal APIs overview](https://learn.microsoft.com/aspnet/core/fundamentals/minimal-apis/overview) · [Route groups](https://learn.microsoft.com/aspnet/core/fundamentals/minimal-apis/route-handlers#route-groups) · [TypedResults](https://learn.microsoft.com/aspnet/core/fundamentals/minimal-apis/responses) · [OpenAPI document generation](https://learn.microsoft.com/aspnet/core/fundamentals/openapi/aspnetcore-openapi) · [Scalar for ASP.NET Core](https://guides.scalar.com/scalar/scalar-api-references/integrations/net-aspnet-core)

**Reading time:** about 50 minutes. **Builds on:** [M00-T2](M00-T2-dependency-rule.md) (layers), [M00-T6](M00-T6-test-harness.md) (`WebApplicationFactory`), [M01-T3](M01-T3-cancellation.md) (the request's `CancellationToken`).

## Contents

1. [What an API contract is, and why M02 is about it](#1-what-an-api-contract-is-and-why-m02-is-about-it)
2. [HTTP semantics a senior engineer is expected to know](#2-http-semantics-a-senior-engineer-is-expected-to-know)
3. [Minimal APIs: endpoints as handlers](#3-minimal-apis-endpoints-as-handlers)
4. [Parameter binding: where each argument comes from](#4-parameter-binding-where-each-argument-comes-from)
5. [Route groups and organising endpoints](#5-route-groups-and-organising-endpoints)
6. [Results, `TypedResults` and union return types](#6-results-typedresults-and-union-return-types)
7. [Creating resources: `201 Created` and `Location`](#7-creating-resources-201-created-and-location)
8. [Request and response DTOs: never expose the domain](#8-request-and-response-dtos-never-expose-the-domain)
9. [Modelling state changes: `PATCH` vs action endpoints](#9-modelling-state-changes-patch-vs-action-endpoints)
10. [JSON details that become contract details](#10-json-details-that-become-contract-details)
11. [OpenAPI: describing the contract](#11-openapi-describing-the-contract)
12. [Scalar: a UI for the document](#12-scalar-a-ui-for-the-document)
13. [Testing an endpoint end to end](#13-testing-an-endpoint-end-to-end)
14. [Minimal APIs vs controllers, and other trade-offs](#14-minimal-apis-vs-controllers-and-other-trade-offs)
15. [Glossary](#15-glossary)
16. [Summary, check yourself, interview questions, for FleetTrack](#16-summary)

---

## 1. What an API contract is, and why M02 is about it

An **API contract** is everything a client can rely on: the URLs, the HTTP methods, the request and response shapes, the status codes, the error format, and the behaviour around retries and concurrent updates. Once a mobile app, a partner integration or another team's service depends on it, **changing it breaks someone you can't see**.

That's why "design an API for X" is such a common interview question. Juniors design the happy path: "`POST /shipments` returns the shipment". Seniors are judged on what happens around it:

| Question a client will eventually ask | M02 task |
| --- | --- |
| What exactly do I send, and what do I get back? | **T1** (this task): endpoints, status codes, OpenAPI |
| What does an error look like, and can I handle it in code? | T2: validation and the error contract |
| How do I page through 200,000 shipments without duplicates? | T3: keyset pagination |
| How do I know my update didn't overwrite someone else's? | ETags and optimistic concurrency |
| My request timed out: is it safe to send it again? | Idempotency |
| You changed a field: will my app break? | Versioning |

T1 lays the foundation: correct HTTP semantics, a clean endpoint structure, and a **machine-readable description** of the contract (OpenAPI) that clients and tools can use.

---

## 2. HTTP semantics a senior engineer is expected to know

HTTP methods aren't just names; each one makes **promises** that clients, proxies, caches and retry logic rely on. Two properties matter most:

- **Safe:** the request doesn't change server state. Clients and crawlers may send it freely.
- **Idempotent:** sending it **once or ten times has the same effect** on the server. A client (or a proxy) that didn't get a response can safely retry it.

| Method | Meaning | Safe | Idempotent | Typical success |
| --- | --- | --- | --- | --- |
| `GET` | Read a resource or a collection | ✅ | ✅ | `200 OK` |
| `POST` | Create a resource (or run a command) | ❌ | ❌ | `201 Created` (create), `200`/`202` (command) |
| `PUT` | **Replace** a resource with the body | ❌ | ✅ | `200 OK` or `204 No Content` |
| `PATCH` | **Partially** change a resource | ❌ | not guaranteed | `200 OK` or `204 No Content` |
| `DELETE` | Remove a resource | ❌ | ✅ | `204 No Content` |

Why "idempotent" matters already in T1: a `POST` that times out may or may not have created the shipment. If the client retries, you may get **two** shipments. HTTP doesn't solve that for you. The idempotency task later in M02 does, with an idempotency key. For now, know that `POST` is the dangerous one.

**Status codes you'll use in this module:**

| Code | Meaning | Example |
| --- | --- | --- |
| `200 OK` | Success, body contains the result | `GET /shipments/{id}` |
| `201 Created` | A new resource was created; `Location` header says where | `POST /shipments` |
| `204 No Content` | Success, nothing to return | `DELETE`, or a `PATCH` that returns no body |
| `400 Bad Request` | The request is malformed or invalid | missing required field (T2) |
| `404 Not Found` | The resource doesn't exist (or the caller may not know it exists) | unknown id |
| `409 Conflict` | The request conflicts with the current state | invalid status transition, concurrent update |
| `422 Unprocessable Content` | Syntactically fine, semantically invalid (an alternative to `400` some APIs use) | — |
| `500 Internal Server Error` | Our bug; never deliberately returned | unhandled exception (T2) |

**A common mistake:** returning `200` with `{ "success": false, "error": "..." }`. Every HTTP-aware tool (load balancers, retries, monitoring, client libraries) now thinks the request succeeded. Use the status code for what it's for, and put details in the body.

---

## 3. Minimal APIs: endpoints as handlers

A **minimal API** maps a route and an HTTP method straight to a **handler**: a delegate (a lambda or a method) that receives the request's data as parameters and returns a result. There's no controller class, no base class and no attribute routing:

```csharp
app.MapGet("/api/v1/vehicles/{id:guid}", (Guid id) => …);
app.MapPost("/api/v1/vehicles", (CreateVehicleRequest request) => …);
```

What happens on a request:

```
HTTP request ──▶ routing: which endpoint matches method + path?
                     │
                     ▼
              endpoint filters (optional, like middleware for one endpoint)
                     │
                     ▼
              parameter binding: route values, query, headers, JSON body, DI services
                     │
                     ▼
              your handler runs ──▶ returns an IResult (or an object, serialised as JSON)
                     │
                     ▼
              result executes: status code, headers, body
```

**Lambdas or methods?** For one or two lines, a lambda is fine. For anything real, use a **named static method**:

```csharp
group.MapGet("/{id:guid}", GetVehicle);

static async Task<Results<Ok<VehicleResponse>, NotFound>> GetVehicle(
    Guid id, IVehicleQueries queries, CancellationToken ct)
{
    var vehicle = await queries.FindAsync(id, ct);
    return vehicle is null ? TypedResults.NotFound() : TypedResults.Ok(vehicle);
}
```

Named methods give readable stack traces, can be unit tested directly, keep the `Program.cs`/mapping code to one line per endpoint, and their return type documents the endpoint's responses (§6).

**Route constraints** such as `{id:guid}` or `{id:int}` make the route **not match** when the value has the wrong format, so `/vehicles/abc` returns `404` instead of reaching your handler. Use them for type checks only, never for business validation ("id must exist" is the handler's job).

---

## 4. Parameter binding: where each argument comes from

The framework decides where each handler parameter comes from, by these rules:

| Parameter | Bound from | Example |
| --- | --- | --- |
| Name matches a route segment | **Route** | `{id}` → `Guid id` |
| Simple type (string, number, `Guid`, enum, `DateTimeOffset`…) not in the route | **Query string** | `?status=Delivered` → `ShipmentStatus? status` |
| Complex type in a `POST`/`PUT`/`PATCH` | **JSON body** (only one body parameter per endpoint) | `CreateVehicleRequest request` |
| A type registered in DI | **Services** | `IVehicleQueries queries` |
| Special types | **The request itself** | `CancellationToken` (fires when the client disconnects, M01-T3), `HttpContext`, `ClaimsPrincipal` |

You can be explicit with `[FromRoute]`, `[FromQuery]`, `[FromHeader]`, `[FromBody]` and `[FromServices]`. Do so when the inference isn't obvious to a reader, for example a header like `[FromHeader(Name = "If-Match")] string? ifMatch` (used later for ETags).

**`[AsParameters]`** groups many query parameters into one type, which keeps list endpoints readable:

```csharp
public sealed record VehicleListQuery(int Limit = 50, string? Cursor = null, VehicleType? Type = null);

group.MapGet("/", ListVehicles);
static async Task<Ok<VehicleListResponse>> ListVehicles([AsParameters] VehicleListQuery query, IVehicleQueries q, CancellationToken ct) => …;
```

**Pitfalls:**
- **Forgetting the `CancellationToken`.** Add it to every handler that does I/O and pass it down. CA2016 will catch you only if the methods you call accept one.
- **Binding failures return `400` automatically** (for example `?limit=abc`), with a terse message. In T2 you make all errors consistent.
- **Optional vs required query parameters:** a non-nullable `int limit` without a default is **required**; `int limit = 50` or `int? limit` is optional. This shows up in the OpenAPI document, so it's part of the contract.

---

## 5. Route groups and organising endpoints

A **route group** gives several endpoints a common prefix and common configuration (tags, filters, authorization, rate limiting), written once:

```csharp
var vehicles = app.MapGroup("/api/v1/vehicles")
    .WithTags("Vehicles");              // groups the endpoints in OpenAPI / Scalar

vehicles.MapPost("/", CreateVehicle);
vehicles.MapGet("/{id:guid}", GetVehicle);
vehicles.MapGet("/", ListVehicles);
```

Later modules add `.RequireAuthorization()` (M11) or a rate-limiting policy to the group, and every endpoint in it gets it.

**Organising endpoints in a real codebase.** `Program.cs` should not grow by 30 lines per feature. The usual shape is one static class per resource or feature, with an extension method:

```csharp
// src/FleetTrack.API/Endpoints/VehicleEndpoints.cs
public static class VehicleEndpoints
{
    public static IEndpointRouteBuilder MapVehicleEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/vehicles").WithTags("Vehicles");
        group.MapPost("/", CreateVehicle).WithName("CreateVehicle");
        group.MapGet("/{id:guid}", GetVehicle).WithName("GetVehicle");
        return app;
    }

    private static async Task<…> CreateVehicle(…) { … }
    private static async Task<…> GetVehicle(…) { … }
}

// Program.cs
app.MapVehicleEndpoints();
```

That's the same pattern FleetTrack already uses for `MapFleetTrackDefaults()`. It scales to the **vertical slice** shape (one folder per feature with its endpoint, request, response and handler), which M05's modules will build on.

**Where the logic goes (architecture rule 4).** The handler **orchestrates**: it binds input, calls the Application layer, and translates the outcome to HTTP. Business rules ("a delivered shipment can't go back to in-transit") live in the **Domain**. A handler that contains `if (shipment.Status == Delivered && newStatus == InTransit)` is putting a business rule in the HTTP layer, where no other entry point (a message consumer, a background job) will apply it.

---

## 6. Results, `TypedResults` and union return types

A handler can return:
- **A plain object:** serialised as JSON with `200 OK`. Simple, but you can't return a `404` from the same handler.
- **An `IResult`:** an object that knows how to write the response (status code, headers, body).

There are two factories for `IResult`:

```csharp
Results.Ok(vehicle)        // returns IResult: the compiler (and OpenAPI) only knows "some result"
TypedResults.Ok(vehicle)   // returns Ok<VehicleResponse>: the type says "200 with a VehicleResponse"
```

**Prefer `TypedResults`.** The concrete type carries metadata: the OpenAPI generator reads it and documents the response **automatically**, and unit tests can check the result type without running HTTP.

When a handler can return different results, declare them all with **`Results<...>`**, a union type:

```csharp
static async Task<Results<Ok<VehicleResponse>, NotFound>> GetVehicle(Guid id, IVehicleQueries q, CancellationToken ct)
{
    var vehicle = await q.FindAsync(id, ct);
    return vehicle is null ? TypedResults.NotFound() : TypedResults.Ok(vehicle);
}
```

The return type is now **the documentation**: the OpenAPI document lists `200` (with the `VehicleResponse` schema) and `404`, and the compiler stops you from returning something that isn't declared (say, a `409` you forgot to document).

**When `TypedResults` isn't enough:** results produced *outside* your handler aren't in its return type: binding failures (`400`), validation (T2), authorization (`401`/`403`), unhandled exceptions (`500`). Document those with metadata on the endpoint or group, for example `.ProducesProblem(StatusCodes.Status400BadRequest)` (T2 covers this).

---

## 7. Creating resources: `201 Created` and `Location`

When `POST` creates a resource, the correct response is **`201 Created`** with a **`Location` header** containing the new resource's URL. HTTP (RFC 9110) defines both: `Location` lets the client fetch, link to or cache the resource without guessing how URLs are built.

```csharp
static async Task<Created<VehicleResponse>> CreateVehicle(CreateVehicleRequest request, IVehicleCommands commands, CancellationToken ct)
{
    var vehicle = await commands.CreateAsync(request, ct);
    return TypedResults.Created($"/api/v1/vehicles/{vehicle.Id}", vehicle);
}
```

The response looks like:

```
HTTP/1.1 201 Created
Location: /api/v1/vehicles/0192f3a1-7c2e-7d4b-9a10-4f5e6b7c8d9e
Content-Type: application/json; charset=utf-8

{ "id": "0192f3a1-…", "plate": "TM-01-ABC", … }
```

**Details:**
- **Relative or absolute `Location`?** Both are allowed. Relative URLs are simpler and keep working behind proxies that rewrite the host.
- **Hard-coded path vs a generated link.** `$"/api/v1/vehicles/{id}"` duplicates the route template. `TypedResults.CreatedAtRoute("GetVehicle", new { id }, vehicle)` builds the URL from the named `GET` endpoint (`.WithName("GetVehicle")`), so the two can't drift apart. The trade-off: one more indirection to read.
- **Body or no body?** Returning the created resource saves the client a `GET` and shows server-generated fields (id, timestamps, initial status). Most APIs do.
- **Who generates the id?** The server, usually. For ids, prefer **`Guid.CreateVersion7()`** (.NET 9+) over `Guid.NewGuid()`: version 7 GUIDs start with a timestamp, so new rows are inserted at the end of the index instead of at random places, which keeps B-tree indexes compact. That matters at volume, and it gives a natural sort order for paging (T3). GUIDs also stop clients from guessing other ids by counting (`/vehicles/41`, `/vehicles/42`…).

---

## 8. Request and response DTOs: never expose the domain

A **DTO (data transfer object)** is a type that exists only to carry data across a boundary, here HTTP. Endpoints should accept and return **DTOs**, never domain entities or EF Core entities:

```csharp
public sealed record CreateVehicleRequest(string Plate, VehicleType Type, int CapacityKg);
public sealed record VehicleResponse(Guid Id, string Plate, VehicleType Type, int CapacityKg, DateTimeOffset CreatedAt);
```

Why not just return the entity?
- **The contract would change whenever the domain changes.** Rename a property for a domain reason and every client breaks. With a DTO, the mapping absorbs the change.
- **Over-posting.** If the request type is the entity, a client can send fields it shouldn't control (`"id"`, `"createdAt"`, `"tenantId"`) and the binder happily sets them. A request DTO contains only what the client may set.
- **Leaking data.** Entities contain internal fields (tenant id, internal notes, navigation properties). Serialising them can also follow EF navigations and load half the database, or fail on cycles.
- **Different shapes for different needs.** A list item usually needs fewer fields than the detail view.

Records (M01-T6) are the natural fit: immutable, concise, value equality for tests. The cost is **mapping code**: a few lines per type. Write it by hand (an extension method `vehicle.ToResponse()`); mapping libraries save typing but hide what's exposed, and the contract is exactly the thing you want visible.

---

## 9. Modelling state changes: `PATCH` vs action endpoints

T1's `PATCH {id}/status` raises a design question that interviewers like: how do you expose a **state change**?

**Option A: change the field (resource-oriented).**
```
PATCH /api/v1/shipments/{id}/status
{ "status": "InTransit" }
```
Simple and uniform. The client says *what* the new state is. The server still has to check the transition is allowed (a domain rule), and answer `409 Conflict` if it isn't.

**Option B: an action (command-oriented).**
```
POST /api/v1/shipments/{id}/dispatch
POST /api/v1/shipments/{id}/deliver   { "signedBy": "…" }
```
Each business operation gets its own endpoint, with its own input (deliver needs a signature, dispatch needs a vehicle), its own authorization and its own documentation. It mirrors the domain's language ("dispatch a shipment"), at the cost of more endpoints.

| | Field update (`PATCH …/status`) | Action endpoints |
| --- | --- | --- |
| Number of endpoints | One | One per transition |
| Different input per transition | Awkward (one body for all) | Natural |
| Authorization per operation | Needs logic inside | Per endpoint |
| Fits a CRUD-like resource | ✅ | Overkill |
| Fits a workflow with rules | Rules hidden behind one field | ✅ The workflow is visible in the API |

Neither is "the REST way". A senior answer picks one **consistently** and explains why. The task asks for `PATCH {id}/status`, which is fine while transitions carry no extra data. Note the point where you'd switch.

**About `PATCH` bodies.** There are two standards for partial updates: **JSON Merge Patch** (RFC 7396: send the fields to change; `null` means remove) and **JSON Patch** (RFC 6902: a list of operations like `{ "op": "replace", "path": "/status", "value": "InTransit" }`). For a single-field sub-resource like `/status`, a small DTO is clearer than either.

---

## 10. JSON details that become contract details

ASP.NET Core serialises with `System.Text.Json` using **web defaults**: property names in **camelCase** (`createdAt`), case-insensitive reading. Two defaults surprise people:

- **Enums are serialised as numbers.** `ShipmentStatus.InTransit` goes out as `2`. That's fragile (reordering the enum silently changes the contract) and unreadable. Configure strings once, for the whole API:

  ```csharp
  builder.Services.ConfigureHttpJsonOptions(options =>
      options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));
  ```

  The OpenAPI document then shows the enum as a list of string values. Also decide what happens when a client sends an **unknown** value (a `400`), and remember that **adding** an enum value can break clients that switch over all values.
- **Dates:** use `DateTimeOffset` (ISO 8601 with an offset, `2026-10-03T10:00:00+00:00`), not `DateTime`, whose `Kind` gets lost and causes "off by N hours" bugs. Store and return UTC.

Every one of these choices is part of the contract: once clients parse `"InTransit"`, switching to numbers is a breaking change.

---

## 11. OpenAPI: describing the contract

**OpenAPI** is a standard, machine-readable description of an HTTP API (a JSON or YAML document): every path, method, parameter, request body, response, status code and schema. It's what tools read to generate client code, mock servers, contract tests and documentation UIs.

### 11.1 Built-in generation in .NET 9+

Since .NET 9, ASP.NET Core generates the document itself with the **`Microsoft.AspNetCore.OpenApi`** package. (Before that, most projects used the third-party Swashbuckle, which the templates dropped in .NET 9.) FleetTrack already has the two lines:

```csharp
builder.Services.AddOpenApi();   // registers the generator (document name "v1")
app.MapOpenApi();                // serves it at /openapi/v1.json
```

In .NET 10 the document uses **OpenAPI 3.1** by default.

The generator builds the document from **endpoint metadata**: routes, parameter types, `TypedResults` return types, and anything you add:

```csharp
group.MapGet("/{id:guid}", GetVehicle)
    .WithName("GetVehicle")                         // operationId: client generators use it as the method name
    .WithSummary("Get a vehicle by id")
    .WithDescription("Returns 404 if the vehicle doesn't exist in the caller's fleet.");
```

- **`WithName`** sets the **operationId**. Without it, generated clients get names like `ApiV1VehiclesGet2`. Treat operationIds as part of the contract: renaming one breaks generated client code.
- **XML doc comments:** in .NET 10 the generator can also read `/// <summary>` comments on handler methods and DTO properties, if the project has `<GenerateDocumentationFile>true</GenerateDocumentationFile>`. That keeps descriptions next to the code.
- **Transformers** (`AddOpenApi(o => o.AddDocumentTransformer(...))`) let you edit the document: add a server URL, a security scheme (M11), or a description of the error format (T2).

### 11.2 Code-first vs contract-first

| | **Code-first** (FleetTrack, this task) | **Contract-first** |
| --- | --- | --- |
| How | Write endpoints; generate OpenAPI from the code | Write the OpenAPI document first; generate or check server code against it |
| Pros | One source of truth (the code); fast to start | Clients can start before the server exists; design reviewed before coding; forces deliberate contract decisions |
| Cons | The contract changes whenever the code changes, possibly by accident | Two artefacts to keep in sync; tooling friction |
| Fits | One team owning API and main client | Public APIs, many consumers, cross-team contracts |

**A senior safeguard for code-first:** generate the document **at build time** and commit it (the `Microsoft.Extensions.ApiDescription.Server` package writes it to a file on build). Then every contract change shows up as a **diff in the PR**, and CI can fail on a breaking change. An accidental rename becomes visible in review instead of in a client's crash report.

### 11.3 Should the document be public in production?

FleetTrack maps it only in Development (`if (app.Environment.IsDevelopment())`). That's a reasonable default for an internal API: the document describes every endpoint, which helps attackers map your surface. Public APIs usually publish it on purpose (it's the documentation). Either way, it's a decision, not a default.

---

## 12. Scalar: a UI for the document

The OpenAPI document is JSON; people need a UI to read it and try requests. **Scalar** is an open-source API reference UI (MIT licence) with an ASP.NET Core integration package, **`Scalar.AspNetCore`**:

```csharp
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();               // /openapi/v1.json
    app.MapScalarApiReference();    // UI at /scalar, reading the document above
}
```

Open `/scalar` and you get every endpoint grouped by tag, the request and response schemas, the status codes, and a client to send test requests.

**What it's good for:** a quick check that the document says what you *meant*. If a `404` is missing from Scalar, it's missing from the contract, and generated clients won't handle it. If a field shows as a number instead of a string enum, you've found §10's problem before a client did.

**FleetTrack rules that apply** (CLAUDE.md): a new package needs a **licence check** (Scalar is MIT, fine for the $0 budget), and its version goes in `Directory.Packages.props` (Central Package Management). Swagger UI and Redoc are free alternatives; Scalar is the one the task names.

---

## 13. Testing an endpoint end to end

You already have the harness from M00-T6: **`WebApplicationFactory<Program>`** starts the whole API in memory, and its `HttpClient` sends real HTTP requests through routing, binding, your handler and serialisation, without opening a port.

The shape of an end-to-end test for a create + read endpoint (on vehicles, to keep the shipment one yours):

```csharp
[Test]
public async Task CreatedVehicleCanBeReadBack()
{
    var client = _factory.CreateClient();
    var request = new CreateVehicleRequest("TM-01-ABC", VehicleType.Van, 1200);

    var createResponse = await client.PostAsJsonAsync("/api/v1/vehicles", request);

    Assert.That(createResponse.StatusCode, Is.EqualTo(HttpStatusCode.Created));
    Assert.That(createResponse.Headers.Location, Is.Not.Null);

    var created = await createResponse.Content.ReadFromJsonAsync<VehicleResponse>();
    var fetched = await client.GetFromJsonAsync<VehicleResponse>(createResponse.Headers.Location);

    Assert.That(fetched, Is.EqualTo(created));   // record value equality (M01-T6)
}
```

What it proves, which a unit test of the handler doesn't:
- the **route** and **method** are right
- the **JSON shapes** round-trip (casing, enums as strings, dates)
- the **status code** and the **`Location` header** are correct
- the URL in `Location` **actually works**

**Notes:**
- **Deserialising in tests uses the test's JSON options,** not the server's. If the server writes enums as strings, the test client needs a `JsonStringEnumConverter` too (pass `JsonSerializerOptions` to `ReadFromJsonAsync`), or the read fails. It's a small, real example of a contract detail clients must match.
- **The database:** M00-T6 runs Postgres in Testcontainers. Whatever persistence you choose for M02 (a simple EF Core `DbContext`; M03 does the data layer properly), the test should go through it, not around it.

---

## 14. Minimal APIs vs controllers, and other trade-offs

ASP.NET Core still supports **controllers** (MVC: classes deriving from `ControllerBase`, attribute routing, action filters). Both are first-class:

| | Minimal APIs | Controllers |
| --- | --- | --- |
| Ceremony | Low: a handler per endpoint | A class per resource, attributes, base class |
| Performance / native AOT | Faster startup, AOT-friendly | Heavier, AOT not supported |
| Grouping and conventions | Route groups, extension methods | Built-in conventions, filters, model binding customisation |
| Validation | Built in since .NET 10 (T2) | Built in for years (`[ApiController]`) |
| Ecosystem features | Newer; most features now available | Mature: OData, some libraries still assume MVC |
| Risk | Endpoints scattered in big `Program.cs` files if nobody organises them | Fat controllers with business logic |

New projects at Microsoft and in most teams default to minimal APIs. **When controllers still make sense:** a large existing MVC codebase (consistency beats novelty, M01-T6 §1), or a dependency on MVC-only features. Mixing both in one API is possible but doubles the conventions a reader must know.

**When not to build a REST-style API at all:** high-frequency internal service-to-service calls with strict contracts can use **gRPC** (binary, HTTP/2, generated clients). Clients that need flexible queries over a graph of data sometimes justify **GraphQL**. FleetTrack's public API stays HTTP + JSON; gRPC may come up when Tracking becomes a separate service (M09).

---

## 15. Glossary

| Term | Meaning |
| --- | --- |
| **API contract** | Everything a client relies on: URLs, methods, shapes, status codes, errors, behaviour |
| **Safe / idempotent method** | Doesn't change state / same effect whether sent once or many times |
| **Minimal API** | ASP.NET Core style that maps routes directly to handler delegates |
| **Handler** | The lambda or method that runs for an endpoint |
| **Parameter binding** | Filling handler parameters from route, query, headers, body, DI or the request |
| **Route constraint** | `{id:guid}`: the route only matches values of that format |
| **Route group** | `MapGroup(prefix)`: shared prefix and configuration for several endpoints |
| **`IResult`** | An object that writes the HTTP response (status, headers, body) |
| **`TypedResults`** | Factory returning concrete result types (`Ok<T>`, `NotFound`…) that carry OpenAPI metadata |
| **`Results<T1, T2…>`** | A union return type listing every result a handler can return |
| **`Location` header** | The URL of a newly created resource, sent with `201 Created` |
| **DTO (data transfer object)** | A type carrying data across a boundary, with no behaviour |
| **Over-posting** | A client setting fields it shouldn't, because the bound type has them |
| **JSON Merge Patch / JSON Patch** | Two standard formats for partial updates (RFC 7396 / RFC 6902) |
| **OpenAPI** | A standard machine-readable description of an HTTP API |
| **operationId** | An endpoint's unique name in OpenAPI; generated clients use it as the method name |
| **Code-first / contract-first** | Generate the contract from code / write the contract first |
| **Scalar** | An open-source UI that renders an OpenAPI document and sends test requests |
| **`WebApplicationFactory<T>`** | Test helper that runs the whole API in memory for HTTP-level tests |
| **UUID/GUID version 7** | A GUID that starts with a timestamp; index-friendly and roughly time-ordered |

---

## 16. Summary

1. An API's **contract** is everything clients rely on; changing it breaks people you can't see. M02 is about designing it deliberately.
2. HTTP methods make promises: `GET` is safe, `PUT`/`DELETE` are idempotent, `POST` is neither, which is why retries are dangerous for creates.
3. Minimal APIs map routes to handlers. Use **named static methods**, organised per resource with a **route group** and an extension method, and keep business rules in the domain.
4. Binding takes parameters from route, query, body, DI and the request; always take the **`CancellationToken`**.
5. Return **`TypedResults`** with a **`Results<...>`** union: the return type documents the responses and the compiler enforces them.
6. Creating returns **`201 Created`** with a **`Location`** header (preferably generated from the named `GET` route), usually with the created resource as the body.
7. Endpoints take and return **DTOs**, never entities: stable contract, no over-posting, no leaks.
8. State changes: field update (`PATCH …/status`) vs action endpoints. Pick one consistently and know when you'd switch.
9. JSON choices are contract choices: string enums, `DateTimeOffset` in UTC, camelCase.
10. The built-in generator produces the **OpenAPI** document from endpoint metadata; **Scalar** renders it. Generating it at build time and diffing it in PRs catches accidental contract changes.

### Check yourself

1. Why is retrying a `POST` dangerous but retrying a `PUT` usually fine? (§2)
2. A client sends `GET /api/v1/vehicles/abc` and gets `404`. Your handler never ran. Why? (§3)
3. Where does each parameter come from: `(Guid id, int limit, CreateVehicleRequest body, IClock clock, CancellationToken ct)` on `POST /vehicles/{id}/...`? (§4)
4. What does `Results<Ok<VehicleResponse>, NotFound>` give you that `IResult` doesn't? (§6)
5. Which responses won't appear in a handler's return type, and how do you document them? (§6)
6. What should a `POST` that creates a resource return, and why the `Location` header? (§7)
7. Give three reasons not to return EF Core entities from endpoints. (§8)
8. When would you switch from `PATCH …/status` to action endpoints? (§9)
9. Your API returns `"status": 2`. What's wrong with that as a contract, and how do you fix it? (§10)
10. How would you make sure a refactoring can't silently change your public contract? (§11.2)

**Before you build: decide and note down** (each is a contract decision you'll explain in review):
- How are the shipment endpoints organised (file, class, group)?
- What does each endpoint return in every case (the `Results<...>` per handler)?
- `Location`: hard-coded or generated from a named route?
- Ids: which GUID version, and who generates them?
- Enums: numbers or strings?

### Interview questions this prepares you for

- "Design the HTTP API for a shipment service: endpoints, methods, status codes."
- "What's the difference between `PUT` and `PATCH`? What does idempotent mean, and why does it matter?"
- "What should a create endpoint return?"
- "Why shouldn't you expose your EF Core entities from the API?"
- "How would you model a state transition like 'dispatch a shipment' in a REST API?"
- "Minimal APIs or controllers for a new service? Why?"
- "How do you keep API documentation in sync with the code, and catch breaking changes?"

### For FleetTrack

- **Where things go (rule 1: API → Application → Domain):** the endpoints in `src/FleetTrack.API/Endpoints/ShipmentEndpoints.cs` (mapped from `Program.cs` like `MapFleetTrackDefaults()`); request/response DTOs next to them or in Application; the use cases (create, get, list, change status) in Application; the `Shipment` entity and its status rules in Domain (no framework references, the architecture tests will check).
- **Persistence:** M02 allows a simple EF Core `DbContext` (M03 does the data layer properly). Keep it behind the Application layer so the endpoints don't touch it directly.
- **Packages:** `Scalar.AspNetCore` (MIT) goes into `Directory.Packages.props` with a licence comment, like BenchmarkDotNet. `Microsoft.AspNetCore.OpenApi` is already there.
- **The ✅ check:** `/scalar` shows the four endpoints with their schemas and every status code they return, and an integration test (in `FleetTrack.IntegrationTests`, on the existing Testcontainers setup) posts a shipment and reads the same one back through the `Location` header.
- **Coming next:** T2 turns every error into `application/problem+json`, T3 replaces the list's paging with keyset pagination, and later tasks add ETags, idempotency keys and versioning on top of these same endpoints.
