# FleetTrack API errors

Every error response from the FleetTrack API, version 1 (`/api/v1/...`), uses one format: **Problem Details** ([RFC 9457](https://www.rfc-editor.org/rfc/rfc9457)), sent with `Content-Type: application/problem+json`. This page lists every error `type` the API returns, when you get it, and what your client should do.

> **Status:** this is the target contract for M02-T2. The tests in `FleetTrack.IntegrationTests` check it.

## Contents

1. [Response format](#1-response-format)
2. [Error catalogue](#2-error-catalogue)
3. [Error types](#3-error-types)
4. [Errors produced by the framework](#4-errors-produced-by-the-framework)
5. [Rules for adding a new error type](#5-rules-for-adding-a-new-error-type)

---

## 1. Response format

| Member | Always present | Meaning |
| --- | --- | --- |
| `type` | yes | A URI that names the **kind** of problem. It never changes. **Branch your code on this member.** |
| `title` | yes | A short, human-readable summary. It is the same for every response of one `type`. |
| `status` | yes | The HTTP status code, repeated in the body. |
| `detail` | no | A human-readable explanation of **this occurrence**. You can show it to people, but don't parse it: the wording can change. |
| `instance` | no | The request path that failed. |
| `traceId` | yes | The id of this request in FleetTrack's logs. Include it when you report a problem. |

Some types add **extension members**, which are extra fields with data your client can use. Each type lists its extension members below.

## 2. Error catalogue

The base URI is `https://fleettrack.dev/errors/`.

| `type` (slug) | Status | `title` | When it happens | What the client should do |
| --- | --- | --- | --- | --- |
| `validation` | 400 | One or more validation errors occurred. | The request body or parameters break a format rule, for example a required field is missing or a value is too long. | Fix the fields listed in `errors`. **Don't retry the same request**: it will fail again. |
| `not-found` | 404 | The resource was not found. | No shipment with this id exists **that you are allowed to see**. | Stop and check the id. Don't retry. |
| `invalid-status-transition` | 409 | The status transition is not allowed. | The requested status change isn't allowed from the shipment's **current** status, for example `Delivered → InTransit`. | Fetch the shipment again (`GET`), read `currentStatus`, and send only a transition that is allowed from that status. Retrying the same request will always fail. |
| `internal-error` | 500 | An unexpected error occurred. | A bug on FleetTrack's side. | Report it with the `traceId`. Retry **only** safe or idempotent requests (`GET`, or a `PATCH` to the same target status). Retrying a `POST` can create a duplicate. |
| `service-unavailable` | 503 | The service is temporarily unavailable. | A dependency FleetTrack needs is down or overloaded. The failure is temporary. | Retry after the number of seconds in the `Retry-After` header, with backoff. *Not returned yet: it arrives once the API depends on a database (M03).* |

## 3. Error types

### 3.1 `validation` (400)

The extension member `errors` maps each field name, as you sent it, to its messages. All failing fields come back in **one** response.

```json
{
  "type": "https://fleettrack.dev/errors/validation",
  "title": "One or more validation errors occurred.",
  "status": 400,
  "instance": "/api/v1/shipments",
  "traceId": "00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-01",
  "errors": {
    "destination": ["The destination field is required."]
  }
}
```

### 3.2 `not-found` (404)

FleetTrack also returns 404, **never 403**, for a shipment that exists but belongs to another tenant. A 403 would confirm that the id exists, which leaks information about other customers.

```json
{
  "type": "https://fleettrack.dev/errors/not-found",
  "title": "The resource was not found.",
  "status": 404,
  "detail": "Shipment 0193a5b2-7c1e-7d3f-9a40-2f6c1e8b4d21 was not found.",
  "instance": "/api/v1/shipments/0193a5b2-7c1e-7d3f-9a40-2f6c1e8b4d21",
  "traceId": "00-…-01"
}
```

### 3.3 `invalid-status-transition` (409)

This type has two extension members, `currentStatus` and `requestedStatus`, so your client doesn't need to parse `detail`. Status values are `Pending`, `InTransit`, `Delivered` and `Cancelled`.

```json
{
  "type": "https://fleettrack.dev/errors/invalid-status-transition",
  "title": "The status transition is not allowed.",
  "status": 409,
  "detail": "Shipment 0193a5b2-… is Delivered and cannot move to InTransit.",
  "instance": "/api/v1/shipments/0193a5b2-…/status",
  "traceId": "00-…-01",
  "currentStatus": "Delivered",
  "requestedStatus": "InTransit"
}
```

### 3.4 `internal-error` (500)

The body is deliberately generic. Outside the Development environment it **never** contains:

- stack traces (they reveal file paths, library versions and internal structure),
- exception messages (they can contain SQL, table names, or data that belongs to another tenant),
- connection strings, hostnames or internal service URLs.

FleetTrack logs the full exception with the same `traceId` that the body carries, so support can find it from your report.

```json
{
  "type": "https://fleettrack.dev/errors/internal-error",
  "title": "An unexpected error occurred.",
  "status": 500,
  "instance": "/api/v1/shipments",
  "traceId": "00-…-01"
}
```

### 3.5 `service-unavailable` (503)

FleetTrack sends this response together with a `Retry-After` header. It is not returned yet.

## 4. Errors produced by the framework

ASP.NET Core sends some errors before a request reaches FleetTrack's code. They use the same `application/problem+json` format and include a `traceId`, but their `type` is the standard RFC 9110 section link that ASP.NET Core sets, not a `fleettrack.dev` URI.

| Situation | Status |
| --- | --- |
| The body is not valid JSON, or a value has the wrong type (for example an unknown `status` name) | 400 |
| No route matches the URL, for example an id that isn't a GUID (Globally Unique Identifier) | 404 |
| The HTTP method isn't supported on this route | 405 |
| The `Content-Type` isn't `application/json` | 415 |

Branch on `status` for these errors, not on `type`.

## 5. Rules for adding a new error type

1. Add a new `type` only for a situation **a client would handle differently**. If the client would react the same way, reuse an existing type and put the specifics in `detail` and extension members.
2. A `type` URI and its `title` never change once published. Clients branch on them.
3. Put the data a client needs in **extension members**. Never make clients parse `detail`.
4. Every new type gets a row in section 2, a subsection in section 3, and a test that checks its status code and `type`.
