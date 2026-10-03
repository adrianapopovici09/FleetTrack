# M02 · API design that survives clients

**Time:** ~12 h · **Prereq:** M01 · **Outcome:** a shipment API whose contract is documented, validated, versioned, concurrency-safe and retry-safe, with tests proving each property.

**Why it matters:** "design an API for X" is a top interview and work request. Senior engineers are judged on the parts juniors skip: error contracts, versioning, pagination at volume, lost updates and retries.

Persistence can be a simple EF `DbContext` here; M03 does the data layer properly. Each task: 📖 **Learn** → 🔨 **Build** → ✅ **Done when**.

---

### T1 · Minimal API endpoints + OpenAPI
- 📖 **Learn (45 min):** [Minimal APIs overview](https://learn.microsoft.com/aspnet/core/fundamentals/minimal-apis/overview) · [Route groups](https://learn.microsoft.com/aspnet/core/fundamentals/minimal-apis/route-handlers#route-groups) · [TypedResults](https://learn.microsoft.com/aspnet/core/fundamentals/minimal-apis/responses) · [OpenAPI document generation](https://learn.microsoft.com/aspnet/core/fundamentals/openapi/aspnetcore-openapi) · [Scalar for ASP.NET Core](https://guides.scalar.com/scalar/scalar-api-references/integrations/net-aspnet-core)
- 🔨 **Build:** route group `/api/v1/shipments` with `POST`, `GET {id}`, `GET` (list) and `PATCH {id}/status`; `TypedResults`; `201 Created` + `Location`; built-in OpenAPI + `app.MapScalarApiReference()`.
- ✅ **Done when:** Scalar shows all endpoints with request/response schemas, and an integration test posts a shipment and gets the same one back.

### T2 · Validation & the error contract
- 📖 **Learn (45 min):** [RFC 9457: Problem Details](https://www.rfc-editor.org/rfc/rfc9457) (sections 3–4) · [Handle errors in APIs](https://learn.microsoft.com/aspnet/core/fundamentals/error-handling-api) (`AddProblemDetails`, `IExceptionHandler`) · [Minimal API validation in .NET 10](https://learn.microsoft.com/aspnet/core/fundamentals/minimal-apis#validation-support-in-minimal-apis)
- 🔨 **Build:** `builder.Services.AddValidation()` with DataAnnotations on request records; `AddProblemDetails()`; an `IExceptionHandler` for unexpected errors; a table of stable `type` URIs (`https://fleettrack.dev/errors/validation`, `.../not-found`, `.../conflict`…) in `docs/api/errors.md`.
- ✅ **Done when:** tests show invalid body → 400 with field errors, unknown id → 404, and every error is `application/problem+json` with a documented `type`.

### T3 · Keyset pagination
- 📖 **Learn (30 min):** [We need tool support for keyset pagination (Use The Index, Luke)](https://use-the-index-luke.com/no-offset) · [Pagination in EF Core](https://learn.microsoft.com/ef/core/querying/pagination) (keyset section)
- 🔨 **Build:** `GET /shipments?limit=50&cursor=…&status=…`, sorted by `(created_at, id)`, with an opaque base64 cursor and a `next` link. Seed 5,000 rows.
- ✅ **Done when:** a test inserting rows *between* page requests proves no duplicates or gaps, and a second test shows the offset version producing a duplicate.

### T4 · Optimistic concurrency with ETags
- 📖 **Learn (30 min):** [MDN: ETag](https://developer.mozilla.org/docs/Web/HTTP/Reference/Headers/ETag) and [If-Match](https://developer.mozilla.org/docs/Web/HTTP/Reference/Headers/If-Match) · [Handling concurrency conflicts (EF Core)](https://learn.microsoft.com/ef/core/saving/concurrency) · [Npgsql: concurrency tokens with xmin](https://www.npgsql.org/efcore/modeling/concurrency.html)
- 🔨 **Build:** return an `ETag` (from Postgres `xmin`) on GET; require `If-Match` on PATCH → `412` on mismatch, `428` if missing.
- ✅ **Done when:** a test with two "dispatchers" editing the same shipment gives the second one a 412, and nothing is overwritten.

### T5 · Idempotent POST
- 📖 **Learn (30 min):** [IETF draft: the Idempotency-Key header](https://datatracker.ietf.org/doc/draft-ietf-httpapi-idempotency-key-header/) · [Designing robust APIs with idempotency (Stripe)](https://stripe.com/blog/idempotency) · [Endpoint filters](https://learn.microsoft.com/aspnet/core/fundamentals/minimal-apis/min-api-filters)
- 🔨 **Build:** an endpoint filter that stores `(tenant, key) → request hash + response` in Postgres, with a 24 h expiry and a unique constraint.
- ✅ **Done when:** tests show same key + same body → identical 201 replayed and one row created; same key + different body → 422/409; two concurrent requests with the same key → exactly one row.

### T6 · Contract safety net & versioning
- 📖 **Learn (40 min):** [Asp.Versioning wiki](https://github.com/dotnet/aspnet-api-versioning/wiki) · [Microsoft REST API guidelines: versioning](https://github.com/microsoft/api-guidelines/blob/vNext/azure/Guidelines.md#api-versioning) · [Verify snapshot testing](https://github.com/VerifyTests/Verify) · [oasdiff: breaking-change detection](https://github.com/oasdiff/oasdiff)
- 🔨 **Build:** snapshot the OpenAPI document with Verify in a test; add oasdiff to CI; add `Asp.Versioning.Http` and write the versioning policy.
- ✅ **Done when:** renaming a response property fails the snapshot test, and oasdiff reports it as breaking.

### T7 · Break it
- 📖 **Learn (10 min):** reread "check-then-act" race conditions in the Stripe article (T5).
- 🔨 **Build:** remove the `If-Match` requirement and rerun the two-dispatcher test; remove the unique constraint from T5 and fire 20 parallel POSTs with the same key.
- ✅ **Done when:** you've recorded the lost update and the duplicate count, then restored both protections.

### T8 · Decide: ADR-004 & ADR-005
- 📖 **Learn (20 min):** [Microsoft REST API guidelines: errors](https://github.com/microsoft/api-guidelines/blob/vNext/azure/Guidelines.md#handling-errors)
- 🔨 **Build:** **ADR-004** error contract (ProblemDetails types; exceptions vs results at the boundary, revisited in M04). **ADR-005** versioning & breaking-change policy.
- ✅ **Done when:** both ADRs have ≥2 options, negative consequences and revisit triggers.

---

## Quiz → [answers](../answers/M02.md)
1. Why is keyset pagination more stable *and* faster than offset at volume? When is offset still fine?
2. What problem does `ETag` + `If-Match` solve that a `version` field in the request body also solves? Why prefer the header?
3. 412 vs 409 vs 428: when do you return each?
4. A client times out on `POST /shipments` and retries. Walk through what happens with and without an idempotency key.
5. Why must the idempotency key be scoped to tenant/user, and why must the request body hash be stored?
6. Which changes are breaking for a JSON API: adding a field, adding an enum value, making an optional field required, renaming a field? Explain the enum case.
7. *Code reading:* `if (!await db.Keys.AnyAsync(k => k.Key == key)) { db.Keys.Add(new(key)); await db.SaveChangesAsync(); }` What's the bug?
8. Where should "pickup date must be before delivery date" be validated, and where should "the request body has a pickupDate" be validated? Why the difference?
9. What does a stable `type` URI in ProblemDetails give a client that the `title` doesn't?
10. *Design:* design the public API for partners to create shipments and receive status updates. Cover auth, idempotency, versioning, pagination, webhooks vs polling, and rate limits.

## Design drill (20 min)
"Design a public shipment-tracking API used by 300 partner companies, some with 10-year-old integrations." Focus on contract evolution and support costs.

## Review
M01 Q2 · M01 Q6 · M00 Q6

## Exit check
T3, T4 and T5 tests green, OpenAPI snapshot + oasdiff in CI, ADR-004/005 written.
