# M02 · API design that survives clients

**Time:** ~12 h · **Prereq:** M01 · **Outcome:** a shipment API whose contract is documented, validated, versioned, concurrency-safe and retry-safe, with tests proving each property.

## Why it matters
"Design an API for X" is a top interview and work request. Senior engineers are judged on the parts juniors skip: error contracts, versioning, pagination at volume, lost updates and retries.

## Concepts
- **Minimal APIs at scale:** route groups per feature, `TypedResults`, endpoint filters, built-in OpenAPI document + Scalar UI.
- **Validation:** .NET 10 built-in minimal-API validation (`AddValidation()` + DataAnnotations) vs FluentValidation; shape validation at the edge, business rules in the domain.
- **Errors:** RFC 9457 `ProblemDetails`, stable `type` URIs, `IExceptionHandler`, mapping table.
- **Versioning:** URL vs header vs media type; additive change; deprecation (`Sunset`/`Deprecation` headers).
- **Pagination:** offset vs keyset (cursor); stable sort keys; opaque cursors.
- **Concurrency:** `ETag` + `If-Match` → `412 Precondition Failed`; lost updates.
- **Idempotency:** `Idempotency-Key` for POST; replay vs conflict; storage and expiry.

Read: [Minimal APIs overview](https://learn.microsoft.com/aspnet/core/fundamentals/minimal-apis/overview) · [OpenAPI in ASP.NET Core](https://learn.microsoft.com/aspnet/core/fundamentals/openapi/overview) · [RFC 9457](https://www.rfc-editor.org/rfc/rfc9457) · [Idempotency-Key draft](https://datatracker.ietf.org/doc/draft-ietf-httpapi-idempotency-key-header/) · [Microsoft REST API guidelines](https://github.com/microsoft/api-guidelines)

## Labs
Persistence can be a simple EF `DbContext` here; M03 does the data layer properly.

- [ ] **L1 · Shipment endpoints + OpenAPI.** Route group `/api/v1/shipments` with `POST`, `GET {id}`, `GET` (list), `PATCH {id}/status`; `TypedResults`; `201 Created` + `Location`; built-in OpenAPI + `app.MapScalarApiReference()`.
  ✅ Scalar shows all endpoints with request/response schemas; an integration test posts and gets back the same shipment.
- [ ] **L2 · Validation + error contract.** `builder.Services.AddValidation()`; `AddProblemDetails()`; an `IExceptionHandler` for unexpected errors; a table of error `type`s (`https://fleettrack.dev/errors/validation`, `.../not-found`, `.../conflict`…) in `docs/api/errors.md`.
  ✅ Tests: invalid body → 400 with field errors; unknown id → 404; every error response is `application/problem+json` with a documented `type`.
- [ ] **L3 · Keyset pagination.** `GET /shipments?limit=50&cursor=…&status=…`, sorted by `(created_at, id)`, opaque base64 cursor, `next` link in the response. Seed 5,000 rows.
  ✅ A test inserts rows *between* page requests and proves no duplicates or gaps; a second test shows the offset version producing a duplicate.
- [ ] **L4 · Optimistic concurrency with ETags.** Return `ETag` (based on Postgres `xmin`) on GET; require `If-Match` on PATCH → `412` on mismatch, `428` if missing.
  ✅ A test with two "dispatchers" editing the same shipment: the second gets 412 and nothing is overwritten.
- [ ] **L5 · Idempotent POST.** Middleware/endpoint filter storing `(tenant, key) → request hash + response` in Postgres with a 24 h expiry.
  ✅ Tests: same key + same body → identical 201 replayed and only one row created; same key + different body → 422/409; two concurrent requests with the same key → exactly one row (unique constraint, not a check-then-insert).
- [ ] **L6 · Contract safety net.** Snapshot the OpenAPI document with **Verify** in a test; add **oasdiff** to CI to flag breaking changes; add `Asp.Versioning.Http` and write the versioning policy.
  ✅ Renaming a response property fails the snapshot test and oasdiff reports it as breaking.

## Break it
Remove the `If-Match` requirement and run the two-dispatcher test. Then remove the unique constraint in L5 and fire 20 parallel POSTs with the same key. Count the duplicates.

## Decide
- **ADR-004** Error contract (ProblemDetails types, exceptions vs results at the boundary; revisit in M04).
- **ADR-005** Versioning & breaking-change policy.

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
L3, L4 and L5 tests are green, OpenAPI snapshot + oasdiff in CI, ADR-004/005 written.
