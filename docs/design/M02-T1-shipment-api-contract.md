# Shipment API contract (M02-T1)

Task: [M02-T1 · Minimal API endpoints + OpenAPI](../plans/modules/M02-api-design.md) · Notes: [reading summary](../plans/reading/M02-T1-minimal-apis-openapi.md)

The contract decisions for `/api/v1/shipments`, made before building. Each one is part of the public contract: changing it later is a breaking change for clients.

## 1. Organisation

- One route group, `/api/v1/shipments`, created in a `MapShipmentEndpoints()` extension on `IEndpointRouteBuilder` and called from `Program.cs` (like `MapFleetTrackDefaults()`).
- The group owns what all four endpoints share: the prefix, `.WithTags("Shipments")`, and later auth, filters and versioning.
- One `ShipmentEndpoints` class holding the group and the four handlers as private static methods, matching the single `ShipmentService`. **Why:** four small handlers read best in one place, next to the group setup they share. **Revisit when** handlers grow on their own (validation, ETags, idempotency in T2–T5) or the file passes a few hundred lines: then split into one file per endpoint (vertical-slice style), which keeps diffs and merge conflicts small. (First choice was one file per endpoint; changed while building, since it was premature for four handlers.)

## 2. Responses per handler

| Endpoint | Return type | Cases |
| --- | --- | --- |
| `POST /` | `CreatedAtRoute<ShipmentResponse>` | 201 with the created shipment and a `Location` header |
| `GET /{id:guid}` | `Results<Ok<ShipmentResponse>, NotFound>` | 200, or 404 when no such shipment exists |
| `GET /` | `Ok<ShipmentListResponse>` | Always 200; no matches = empty `items` |
| `PATCH /{id:guid}/status` | `Results<Ok<ShipmentResponse>, NotFound, Conflict>` | 200 with the updated shipment; 404 unknown id; 409 illegal transition |

Why each choice:

- **Empty list is 200, not 404.** The collection `/shipments` exists; it just has no items matching the filter. A 404 would tell clients the URL is wrong.
- **The list returns an object, not a bare array** (`{ "items": [...] }`). T3 adds a `next` cursor link; adding a field to an object is non-breaking, while changing an array to an object breaks every client.
- **PATCH returns 200 with the shipment, not 204.** The client sees the new state (and later the new `updatedAt`/ETag) without an extra `GET`.
- **Illegal transition = 409 Conflict.** The request is well-formed, but it conflicts with the resource's current state (Delivered → Pending). The rule itself lives in the Domain; the handler only maps the outcome to 409. T2 gives the 409 a problem-details body.
- **Setting the same status again is 200, not 409.** `PATCH` to `InTransit` when already `InTransit` is a no-op, so a client retrying after a timeout gets success instead of a confusing error (retried writes must be safe, architecture rule 5).
- **`{id:guid}` route constraint.** A non-GUID id (`/shipments/abc`) doesn't match the route and returns 404 without reaching the handler.
- **Not in the unions yet:** validation errors (400 `ValidationProblem`, added in T2); malformed JSON (400 from the framework's binding); 500s.
- **Later, with tenancy (M11):** a shipment that belongs to another tenant returns **404, not 403**, so the API doesn't confirm that the id exists.

## 3. `Location` header

Generated from a named route: `GET /{id}` gets `.WithName(...)`, and `POST` returns `TypedResults.CreatedAtRoute(...)` with that name. **Why:** the link can't drift from the actual route when the prefix or version changes. **Watch out:** behind a reverse proxy, absolute links need forwarded headers configured, or they point at the internal host.

## 4. Ids

UUID version 7 (`Guid.CreateVersion7()`), generated in the Domain when the `Shipment` is created, not by the database and not by the client.

- **Why v7:** it starts with a millisecond timestamp, so new ids sort after old ones and inserts append to the right edge of the primary-key B-tree. Random v4 ids land on random index pages, causing page splits, a bloated index and extra I/O once it no longer fits in memory.
- **Why in the Domain:** the id exists before the save, so the handler can build `Location` without a round-trip, and later the outbox (M06) can reference it in the same transaction.
- **Trade-off:** a v7 id reveals its creation time to the millisecond. Fine for shipments; use v4 (or a separate internal id) where creation time is sensitive.

## 5. Enums

Strings on the wire (`"InTransit"`), configured once with `JsonStringEnumConverter` via `ConfigureHttpJsonOptions`, so the JSON and the OpenAPI schema agree.

- **Why:** readable in logs and Scalar, and reordering the C# enum can't silently change the contract (with numbers, `2` would suddenly mean something else).
- **Evolution:** the set of statuses is documented as **open**: new values may be added, and clients must tolerate values they don't know. Adding a status is then non-breaking; renaming or removing one is breaking.
- How the status is stored in the database is a separate decision (M03).

## 6. Status transitions and how failures are reported

```
Pending ──► InTransit ──► Delivered
   │
   └──────► Cancelled

Delivered, Cancelled = terminal
X → X (same status)   = no-op, success
anything else         = illegal → 409
```

- **Left out on purpose:** `InTransit → Cancelled` (once the truck has left, getting the goods back is a return, a separate flow); a failed-delivery status (can be added later, the status set is open).
- **Illegal transitions are returned, not thrown.** The status-change method returns a result the caller must inspect; the handler maps it explicitly to `Ok` or `Conflict`. **Why:** an illegal transition is an expected outcome of user input, not a bug; a return value makes the failure visible in the signature, keeps the handler's `Results<...>` union truthful (and therefore the OpenAPI document), and avoids the cost of throwing on a path clients can trigger at will. **Exceptions are kept for bugs and broken invariants**, which T2's global exception handler turns into a 500.
- **Rejected:** one custom exception per action, mapped to status codes by a global handler. It hides the failure from the signature, makes the handler's union lie, and couples the API to every domain exception type.
