# M02-T3 · Keyset pagination

Task: [M02-T3](../modules/M02-api-design.md)

This file is the complete lesson for the task; you don't need to read anything else. Original sources, if you ever want to go deeper: [We need tool support for keyset pagination (Use The Index, Luke)](https://use-the-index-luke.com/no-offset) · [Pagination in EF Core](https://learn.microsoft.com/ef/core/querying/pagination) (keyset section)

**Reading time:** about 50 minutes. **Builds on:** [M02-T1](M02-T1-minimal-apis-openapi.md) (the list endpoint returns an object so a `next` link can be added; B-trees and UUIDv7), [M02-T2](M02-T2-validation-error-contract.md) (validation, Problem Details, the error catalogue) and [M01-T2](M01-T2-threadpool-starvation.md) (why a slow query per request turns into a capacity problem).

## Contents

1. [Why a list endpoint needs pagination](#1-why-a-list-endpoint-needs-pagination)
2. [The three ways to paginate](#2-the-three-ways-to-paginate)
3. [Offset pagination and its cost](#3-offset-pagination-and-its-cost)
4. [Offset pagination and moving data: duplicates and gaps](#4-offset-pagination-and-moving-data-duplicates-and-gaps)
5. [Keyset pagination: "continue after this row"](#5-keyset-pagination-continue-after-this-row)
6. [The tie-breaker: why one sort column is never enough](#6-the-tie-breaker-why-one-sort-column-is-never-enough)
7. [Indexes: what makes keyset fast](#7-indexes-what-makes-keyset-fast)
8. [The cursor: an opaque bookmark](#8-the-cursor-an-opaque-bookmark)
9. [The page size (`limit`)](#9-the-page-size-limit)
10. [The response shape: `next` links, no totals](#10-the-response-shape-next-links-no-totals)
11. [Filters and sort options](#11-filters-and-sort-options)
12. [Implementing it in .NET](#12-implementing-it-in-net)
13. [What keyset guarantees, and what it doesn't](#13-what-keyset-guarantees-and-what-it-doesnt)
14. [Testing pagination](#14-testing-pagination)
15. [When *not* to use keyset pagination](#15-when-not-to-use-keyset-pagination)
16. [Pitfalls](#16-pitfalls)
17. [Glossary](#17-glossary)
18. [Summary, check yourself, interview questions, for FleetTrack](#18-summary)

---

## 1. Why a list endpoint needs pagination

Right now `GET /api/v1/shipments` returns **every** shipment. With 20 rows nobody notices. Now imagine a year of production: 2 million shipments.

- **The database** reads and sorts 2 million rows for each request.
- **The API** loads them all into memory, maybe 500 MB of objects, and serialises a response of several hundred megabytes. The garbage collector works hard, because big arrays go on the Large Object Heap (M01-T4).
- **The network and the client** move and parse that response, and the client's UI freezes.
- **Under load**, ten of these requests at once can take the API down. One slow request type becomes a capacity problem, just like in M01-T2.

An endpoint that returns "everything" has no upper bound on cost, and the cost grows every day without anyone changing the code. **Every list endpoint needs a maximum page size from day one.** That's also why T1 returned `{ "items": [...] }` instead of a bare array: you can add `next` later without breaking clients, whereas changing a bare array into an object *is* a breaking change.

The question isn't *whether* to paginate, but *how*. The choice decides three things: what the database has to do for page 1,000, what clients see when data changes while they page, and what you can promise in the contract.

---

## 2. The three ways to paginate

| Style | Request looks like | How the server finds the page | Used by |
| --- | --- | --- | --- |
| **Offset / limit** | `?offset=100&limit=50` | "Skip 100 rows, take 50" | Most tutorials, admin UIs |
| **Page number** | `?page=3&pageSize=50` | The same thing: `offset = (page - 1) × pageSize` | Websites with page links ("1 2 3 … 40") |
| **Keyset (seek)**, usually sent as a **cursor** | `?cursor=eyJjIjoi…&limit=50` | "Rows that come *after* this one in the sort order, take 50" | Stripe, Slack, GitHub GraphQL, Twitter/X, Facebook |

Page number is just offset with nicer numbers, so it has the same properties. The real choice is between **offset** ("skip N") and **keyset** ("continue after X"). A **cursor** is how keyset is usually exposed in an API: the "X" is packed into an opaque token, so the client doesn't need to know what it contains (§8).

---

## 3. Offset pagination and its cost

Offset in SQL:

```sql
SELECT id, destination, status, created_at
FROM shipments
ORDER BY created_at DESC, id DESC
OFFSET 50000 LIMIT 50;   -- page 1,001 with 50 rows per page
```

It reads naturally, but **the database can't jump to row 50,001.** No index stores "row number 50,001". The database has to walk 50,050 rows in sort order, throw away the first 50,000, and return the last 50.

```
OFFSET 0      ███                              read 50,     return 50
OFFSET 5000   ███████████████████████          read 5,050,  return 50
OFFSET 50000  ████████████████████████████████ read 50,050, return 50   ← every deeper page costs more
```

So **the cost of a page grows with its depth**, which makes deep paging slow. Who goes deep?
- **Machines.** A partner's nightly sync job pages through *all* shipments. Its last pages each scan almost the whole table, and the total work grows with the *square* of the table size.
- **Crawlers and scripts.** A bot that follows "next page" links forever.
- **Attackers.** `?offset=10000000` is a cheap way to make your database do a lot of work.

There's a second hidden cost. UIs with page numbers want "page 3 of 40", which needs `SELECT COUNT(*)`. In PostgreSQL that's a **full scan of the table or an index** on every request, because of how PostgreSQL handles concurrent transactions (MVCC, M03): each row's visibility has to be checked, so there's no stored row count to read. On a large table the count can cost more than the page itself.

---

## 4. Offset pagination and moving data: duplicates and gaps

Cost is the reason people usually give against offset. The more important reason is **correctness**. An offset counts positions, and **positions move whenever rows are inserted or deleted** in front of where the client is.

Take a list sorted **newest first**, 3 rows per page. The letters are shipments; F is the newest.

```
Data:        F E D C B A

Request 1:   OFFSET 0 LIMIT 3   →  F E D        client has seen F E D

             (a new shipment G is created)

Data:        G F E D C B A
Request 2:   OFFSET 3 LIMIT 3   →  D C B        D again: a DUPLICATE
```

And the opposite with a delete:

```
Data:        F E D C B A
Request 1:   OFFSET 0 LIMIT 3   →  F E D

             (shipment E is deleted)

Data:        F D C B A
Request 2:   OFFSET 3 LIMIT 3   →  B A          C was never returned: a GAP
```

The client did nothing wrong, and neither did the server. Offset is just "position N", and position N now holds a different row.

**Why this is worse than it looks:**
- A sync job that receives a **duplicate** may process it twice: two invoices, or two notifications to a driver.
- A sync job that hits a **gap** silently never sees a shipment. Nobody notices until a customer calls.
- These bugs only appear **under write traffic**, so they don't show in tests or quiet staging environments. They show up in production during peak hours.

**The sort direction decides where the problem appears.** In a **newest-first** list, new rows land at the *front*, in front of every client, so every insert shifts every reader. In an **oldest-first** list, new rows land at the *end*, behind readers, so inserts don't shift anyone, but deletes and updates to the sort column still do. Remember this when you design a test that has to *show* the problem (§14).

---

## 5. Keyset pagination: "continue after this row"

Keyset pagination doesn't remember a *position*. It remembers the **sort key of the last row the client saw**, and asks for rows that come after that key:

```sql
-- Page 1
SELECT id, destination, status, created_at
FROM shipments
ORDER BY created_at DESC, id DESC
LIMIT 50;

-- Page 2: the last row of page 1 had created_at = 2026-11-15 09:30:00.123456+00, id = 0193a5b2-…
SELECT id, destination, status, created_at
FROM shipments
WHERE (created_at, id) < ('2026-11-15 09:30:00.123456+00', '0193a5b2-…')
ORDER BY created_at DESC, id DESC
LIMIT 50;
```

Run the two scenarios from §4 again:

```
Data:        F E D C B A
Request 1:   first 3            →  F E D        last key seen: D

             (G is created, or E is deleted)

Request 2:   key < D, first 3   →  C B A        no duplicate, no gap
```

"Rows after D" means the same thing no matter what happened in front of D. That's the whole idea.

**The cost no longer depends on depth.** With the right index (§7), the database goes straight to the key, as it would for a single-row lookup, and reads 50 rows from there. Page 1 and page 40,000 cost the same.

### 5.1 Row-value comparison

`(created_at, id) < (@c, @id)` is a **row-value comparison**. It compares the pair the way a dictionary orders words: first by `created_at`, and only when those are equal, by `id`. PostgreSQL supports it directly and can use an index for it. Written out by hand, it's equivalent to:

```sql
WHERE created_at < @c
   OR (created_at = @c AND id < @id)
```

Both versions are correct. PostgreSQL optimises the row-value version better: it's a single range condition on the index. Some databases, and some ORM (object-relational mapper) query translations, only support the longer version.

### 5.2 Direction

For a newest-first list (`ORDER BY … DESC`), "after" means **smaller**, so you use `<`. For oldest-first (`ASC`) you use `>`. Getting this wrong returns page 1 again forever, or an empty page 2. Your tests will catch it if they walk more than one page.

---

## 6. The tie-breaker: why one sort column is never enough

Why not sort by `created_at` alone? Because **`created_at` isn't unique.** Two shipments created in the same microsecond, or the same millisecond on a coarse clock, or a batch import that stamps 500 rows with one timestamp, all share a value.

Sort by `created_at` only, with page size 3:

```
Rows:   F(10:00:05)  E(10:00:04)  D(10:00:03)  C(10:00:03)  B(10:00:03)  A(10:00:01)
Page 1: F E D                       last key: 10:00:03
Page 2: WHERE created_at < 10:00:03  →  A          C and B are skipped: a gap
```

If you use `<=` instead of `<`, you get D again: a duplicate. **No comparison is correct if the sort key isn't unique.** The fix is to always add a **unique column as the last sort key**, the **tie-breaker**. For FleetTrack that's `id`, so the key becomes `(created_at, id)`, and the pair is unique because `id` is.

You saw the same thing in T1: UUIDv7 ids generated in the same millisecond aren't ordered among themselves. So `id` alone isn't a reliable *timeline*, but it is a perfect *tie-breaker*, because it's unique and has a total order.

**The rule:** the sort order must be a **total order**, meaning no two rows compare as equal. Add a unique column to every sort you expose. If the sort and the cursor use different columns, you get gaps.

**A cross-database detail:** the tie-breaker's order must be the same in the database and in .NET, if you ever compare or sort in memory. PostgreSQL compares `uuid` values byte by byte, which matches their text form. .NET's `Guid.CompareTo` also follows the text form. SQL Server's `uniqueidentifier` **doesn't**: it compares the *last* group of bytes first. Code that sorts GUIDs in C# and then pages in SQL Server can skip or repeat rows.

---

## 7. Indexes: what makes keyset fast

Keyset is only fast if an index matches the sort. A **B-tree** index (T1) keeps entries sorted by its columns, so the database can:
1. **Seek**: descend the tree to the first entry after `(@c, @id)`. That takes a few page reads, even with millions of rows.
2. **Scan**: read the next 50 entries in order.
3. **Stop**: `LIMIT` means it never touches the rest.

```sql
CREATE INDEX ix_shipments_created_at_id ON shipments (created_at, id);
```

A PostgreSQL B-tree can be read **backwards**, so this one index serves both `ORDER BY created_at, id` and `ORDER BY created_at DESC, id DESC`. It **can't** serve a mixed order like `created_at DESC, id ASC`. Keep both columns in the same direction; there's no reason to mix them.

**With a filter.** `?status=Pending` adds `WHERE status = 'Pending'`. Put **equality columns first**, then the sort columns:

```sql
CREATE INDEX ix_shipments_status_created_at_id ON shipments (status, created_at, id);
```

Now the database seeks to "Pending, after (@c, @id)" and reads 50 rows. If the index were `(created_at, id, status)`, it would walk the sort order and skip every non-Pending row on the way, and for a rare status that could mean reading most of the table.

**Prove it with `EXPLAIN`.** Don't assume the index is used: check the plan.

```sql
EXPLAIN (ANALYZE, BUFFERS)
SELECT … WHERE (created_at, id) < (…) ORDER BY created_at DESC, id DESC LIMIT 51;
```

- **Good:** `Limit → Index Scan Backward using ix_shipments_created_at_id`, a few buffers read, the same timing for page 1 and page 1,000.
- **Bad:** `Sort` above a `Seq Scan`. The database read the whole table and sorted it to return 50 rows, so the index doesn't match the query.

M03 covers `EXPLAIN` properly. For now, remember that **an index is part of the pagination design**, not an optimisation you add later.

---

## 8. The cursor: an opaque bookmark

The client needs to send "the key of the last row" back to you. You *could* use `?afterCreatedAt=…&afterId=…`, but the standard practice is a single **opaque cursor**:

```
GET /api/v1/shipments?limit=50&cursor=eyJ2IjoxLCJjIjoiMjAyNi0xMS0xNVQwOTozMDowMC4xMjM0NTZaIiwiaSI6IjAxOTNhNWIyLS4uLiJ9
```

### 8.1 Why opaque

"Opaque" means the client treats it as a meaningless string: receive it, send it back, never build or edit it. That gives **you** the freedom to change what's inside:
- add a column to the sort (for example a new tie-breaker),
- change the format or the encoding,
- add a version number, the filter it belongs to, or an expiry.

None of that is a breaking change, because no client depends on the contents. With `?afterCreatedAt=…` parameters, the sort key becomes part of the public contract, and every change to it is breaking.

### 8.2 What goes inside

The minimum is the sort key of the last returned row. Adding a **version** costs nothing and saves you later:

```json
{ "V": 1, "C": "2026-11-15T09:30:00.123456+00:00", "I": "0193a5b2-7c1e-7d3f-9a40-2f6c1e8b4d21" }
```

Short property names keep the token short, because the cursor travels in every `next` URL.

The encoding is: **JSON → UTF-8 bytes → Base64Url**. Base64Url is base64 with `-` and `_` instead of `+` and `/`, and no `=` padding, so the token is safe in a URL without escaping. In .NET 9+ that's `System.Buffers.Text.Base64Url`:

```csharp
// A sketch of the encoding only. Where this lives and how errors map to HTTP is your design.
internal sealed record ShipmentCursor(int V, DateTimeOffset C, Guid I);

static string Encode(ShipmentCursor cursor) =>
    Base64Url.EncodeToString(JsonSerializer.SerializeToUtf8Bytes(cursor));

static bool TryDecode(string token, out ShipmentCursor? cursor)
{
    cursor = null;
    try
    {
        cursor = JsonSerializer.Deserialize<ShipmentCursor>(Base64Url.DecodeFromChars(token));
        return cursor is { V: 1 };
    }
    catch (FormatException) { return false; }   // not Base64Url
    catch (JsonException) { return false; }     // Base64Url, but not our JSON
}
```

### 8.3 Opaque is not secret, and not trusted

Base64 is an **encoding**, not encryption. Anyone can decode the cursor above in one line. So:
- **Don't put anything secret in it.** A timestamp and an id the client has already seen in the response are fine. A tenant id or internal flags are not.
- **Treat it as untrusted input.** A client can send garbage, an old version, or a hand-edited value. Decoding must never throw a 500. Garbage is the client's mistake: a **`400`** with its own `type`, for example `https://fleettrack.dev/errors/invalid-cursor`. That's a new row in `docs/api/errors.md`.
- **A hand-edited cursor must not expose more than a normal request would.** The cursor only says *where to start*. All the normal filters (status, and in M11 the tenant) still apply in the `WHERE` clause. If editing the cursor could reveal another tenant's rows, the bug is in the query, not in the cursor.
- **Signing** the cursor with an HMAC (hash-based message authentication code) makes tampering detectable. Some APIs do it, but it's only necessary when the cursor carries something the client must not change. FleetTrack's doesn't, so validation is enough.

### 8.4 Precision: the timestamp must round-trip exactly

The cursor's `created_at` has to compare **exactly equal** to the stored value. Otherwise the `=` branch of the tie-breaker never matches, and rows are skipped or repeated at page boundaries. Two places lose precision without warning:
- **.NET vs PostgreSQL.** `DateTimeOffset` stores 100-nanosecond ticks (7 decimal places). PostgreSQL's `timestamptz` stores microseconds (6). A value created in C# and saved to the database loses its last digit. If a cursor is built from the C# value instead of the stored one, it no longer matches the row. **Build cursors from values as they were read from the store**, or truncate timestamps to microseconds when you create them.
- **Formatting.** If you write a timestamp with a format that drops digits (`"yyyy-MM-ddTHH:mm:ss"`), you've changed the value. Let `System.Text.Json` serialise the `DateTimeOffset`, because its round-trip format keeps every digit.

---

## 9. The page size (`limit`)

| Decision | Common answer | Why |
| --- | --- | --- |
| Default when absent | 20–50 | Small enough to be cheap, big enough for a screen |
| Maximum | 100–200 | The upper bound on cost per request, the whole point of §1 |
| Above the maximum | **`400` validation error** or silently clamp to the max | Rejecting is explicit, and the client learns the rule. Clamping is friendlier but hides bugs ("why do I only get 100?"). Pick one and document it. |
| `0` or negative | `400` | Not a meaningful page size |

The maximum belongs in the **contract**: in OpenAPI (validation attributes on the query parameter show up as `minimum` / `maximum` in the schema, as `maxLength` did in T2) and in your docs.

**How do you know there's a next page?** Not with `COUNT(*)`. Ask the database for **`limit + 1`** rows:
- `limit + 1` rows come back → there's at least one more. Return the first `limit`, and build the cursor from the **last returned** row (not the extra one).
- `limit` or fewer come back → this is the last page. There's no `next`.

One extra row costs almost nothing, and it avoids the classic bug where the client gets an empty last page and has to make an extra request to find out it's done.

---

## 10. The response shape: `next` links, no totals

```json
{
  "items": [ { "id": "0193…", "destination": "Cluj", "status": "Pending", "createdAt": "…" }, … ],
  "next": "/api/v1/shipments?limit=50&status=Pending&cursor=eyJ2IjoxLC…"
}
```

**Return a `next` link, not just the cursor.** The link includes every query parameter the client used (`limit`, `status`) plus the new cursor, so the client just follows it. That's the "hypermedia" idea in its simplest, most useful form: the client doesn't rebuild URLs, and you can change parameter names later without breaking anyone who follows links.

- **The last page:** leave `next` out or set it to `null`. Decide which and document it. Never send a `next` link that leads to an empty page.
- **Relative or absolute URL?** An absolute URL (`https://api.fleettrack.dev/…`) needs the API to know its public host. Behind a reverse proxy or load balancer it sees an internal one, like `http://10.0.3.7:8080`, unless forwarded headers are configured (M13). A path relative to the host (`/api/v1/shipments?…`) avoids that problem completely.
- **An alternative: the `Link` header** (RFC 8288: `Link: <…>; rel="next"`), which GitHub's REST API uses. It keeps the body a plain list, but many clients never look at headers. A field in the body is easier to discover.

**Why no `total`?** Because it costs a `COUNT(*)` on every page (§3), and it's out of date as soon as it's returned. If a product really needs one, offer it separately: a `/count` endpoint, a cached value, or an estimate (PostgreSQL's planner statistics give a rough row count at no cost). Don't charge every page request for it.

**Why no `previous`?** It's possible: reverse the comparison and the order, then reverse the results. But it doubles the logic and the tests, and most clients either scroll forward or sync forward. Add it when a real client needs it (YAGNI, "you aren't gonna need it").

---

## 11. Filters and sort options

**Filters** (`?status=Pending`) go in the `WHERE` clause next to the keyset condition, and they need an index that starts with the filter column (§7). What if the client changes the filter but sends the *old* cursor (`?status=Delivered&cursor=<a cursor from a Pending page>`)? Two defensible answers:
1. **Allow it.** A cursor is just a position in `(created_at, id)` order, and that position means something for any filter. The client gets "Delivered shipments older than that point". Stripe works roughly like this.
2. **Reject it.** Store the filter inside the cursor, and return `400 invalid-cursor` if the request's filter differs. Stricter, and it catches client bugs.

Neither is wrong, but you have to choose one and document it.

**Sort options** (`?sort=destination`) multiply everything. Each sort needs its own unique tie-breaker, its own index, its own cursor format and its own tests, and some (sorting by a column that can be `NULL`, or by a text column with a collation) are hard to get right. Offer **only the sorts a real client needs**. One sort, newest first, covers most API consumers. The T3 task asks for exactly one: `(created_at, id)`.

---

## 12. Implementing it in .NET

### 12.1 The layers

Following FleetTrack's rules (rule 1: API → Application → Domain; endpoints orchestrate, logic lives below):

| Layer | Responsibility |
| --- | --- |
| **API** | Reads `limit`, `cursor`, `status` from the query. Validates them (an invalid cursor → `400`). Encodes and decodes the cursor, because the cursor is an HTTP concern. Builds the `next` link. |
| **Application** | A query method like `ListAsync(ShipmentListQuery query)`, where the query carries an optional "after" key `(CreatedAt, Id)`, the filter and the limit. It returns the items and whether there are more. It knows nothing about base64 or URLs. |
| **Infrastructure** | Runs the actual keyset query against the store. |

Keeping base64 out of the Application layer is the same idea as keeping URIs out of the Domain in T2: each layer speaks its own vocabulary.

### 12.2 LINQ over an in-memory collection

FleetTrack's repository is still the in-memory `ConcurrentDictionary`. In LINQ to Objects, keyset is a `Where` and an ordering:

```csharp
// Sketch: "after" is the decoded key, or null for the first page.
var page = shipments
    .Where(s => after is null
        || s.CreatedAt < after.Value.CreatedAt
        || (s.CreatedAt == after.Value.CreatedAt && s.Id.CompareTo(after.Value.Id) < 0))
    .OrderByDescending(s => s.CreatedAt)
    .ThenByDescending(s => s.Id)
    .Take(limit + 1)
    .ToList();
```

This is correct, but it isn't *fast*: it scans and sorts the whole collection on every call, because a dictionary has no sorted index. That's fine for a learning repository with 5,000 rows. What matters in T3 is **the contract and the correctness**. Speed comes from the database index in M03, and the API contract won't change when the storage does. That's the benefit of putting the cursor behind an interface.

### 12.3 EF Core and PostgreSQL

With EF Core the same LINQ is translated to SQL, with one snag: **C# has no `<` operator for `Guid`**, and whether `CompareTo` translates depends on the database provider. Npgsql's EF Core provider has row-value comparison functions for exactly this case (`EF.Functions.LessThan` over two tuples), which produce the `(created_at, id) < (…)` SQL from §5.1. Check its documentation for your version, and always check the SQL EF generates (`ToQueryString()`) instead of assuming.

The EF Core documentation's own warning applies: `Skip()`/`Take()` is offset pagination, with all the problems from §3–4. `Take()` is fine; `Skip(n)` with a large `n` is the trap.

### 12.4 Seeding 5,000 rows

You need enough rows to see paging behave: many pages, and some identical timestamps. Two practical points:
- Inject a **fake clock** (`TimeProvider`, which `ShipmentService` already uses). That lets the seed give many shipments the **same** `CreatedAt` on purpose, which is the only reliable way to test the tie-breaker (§6). With the real clock, ties happen rarely and randomly, so the test is flaky. `FakeTimeProvider` from `Microsoft.Extensions.TimeProvider.Testing` (MIT licence) is the standard one; a small hand-written subclass of `TimeProvider` works just as well.
- Seed through the **repository or service**, not through HTTP. 5,000 HTTP calls make tests slow, and seeding isn't what the test is about.

---

## 13. What keyset guarantees, and what it doesn't

Keyset is often described as "consistent". Be precise about what it promises, because interviewers check:

| Situation during a walk through all pages | Keyset | Offset |
| --- | --- | --- |
| Row existed before the walk started and wasn't changed | Returned **exactly once** ✅ | May be duplicated or skipped |
| Row **inserted** in front of the client's position (newest-first: a new shipment) | **Not returned** in this walk. The client starts a new walk to see it. | Shifts everything: duplicates |
| Row inserted *behind* the client's position | Returned when the client gets there | Returned, but may shift others |
| Row **deleted** during the walk | Not returned if not reached yet; nothing else moves | Shifts everything: gaps |
| Row's **sort key updated** during the walk | Can move to a page already read (missed) or one not yet read (seen twice) | The same, plus shifts |

So keyset guarantees **no duplicates and no gaps for rows that stay put during the walk**. It is **not** a snapshot. That's why the sort key should be a column that **never changes** after creation. `created_at` and `id` are both immutable, which is a big part of why they're the default choice. Sorting by `status` or `updated_at` would break the guarantee.

For a sync client that needs to catch *everything*, including rows created during its walk, the usual pattern is to **remember the newest key from the start of the walk**, and next time ask for rows newer than it. In M08 you'll see the more robust tool for this: an event stream or change feed instead of polling a list.

---

## 14. Testing pagination

Pagination bugs hide at page boundaries and under concurrent writes, so the tests have to go there on purpose:

- **Walk all the pages.** Follow `next` until it's absent, collect every id, and assert: the count equals the number of seeded rows, there are no duplicates (`Is.Unique`), and the order is right. A test that only fetches page 1 proves almost nothing.
- **Ties at a page boundary.** Seed many rows with the same `CreatedAt` (§12.4) so that a group of equal timestamps **spans** the boundary between two pages. Without this, a tie-breaker bug never shows.
- **Exact multiples.** With `limit = 50` and exactly 100 rows, the second page must have no `next`. This catches the empty-last-page bug (§9).
- **Edge cases:** an empty collection (items `[]`, no `next`), `limit` at its minimum and maximum, `limit` out of range → `400`, a garbage cursor → `400` with your `invalid-cursor` type.
- **Writes during the walk.** This is the ✅ check for T3. Think about which writes, where in the sort order, and which sort direction will make offset pagination **visibly fail** while keyset passes (§4 has everything you need). Then make the offset test fail **for the right reason**: a duplicate id, not an off-by-one in your test.

A practical note: the in-memory repository is a **singleton**, so rows from one test stay for the next one. Pagination tests that count rows need either their own `WebApplicationFactory` (a fresh repository) or a filter that only matches their own rows.

---

## 15. When *not* to use keyset pagination

| Situation | Better choice | Why |
| --- | --- | --- |
| The list is small and bounded (countries, a tenant's 12 depots, config values) | Return everything | No real cost, and paging adds complexity for nothing. Still enforce a sane maximum. |
| A UI needs "jump to page 37" and "page 3 of 40" | Offset, with a **maximum depth** (for example 100 pages) and a cached or estimated total | Keyset can't jump to an arbitrary page; it only moves forward from a known row. Most people never go past page 3, so cap the depth to limit the cost. |
| Exporting everything (reports, a data dump) | A streaming endpoint (`IAsyncEnumerable`, M01) or an async export job that writes a file | Pagination is for interactive reading. A 2-million-row export over 40,000 requests is slow and fragile. |
| Full-text search ranked by relevance | The search engine's own paging, usually a cursor tied to a snapshot | Relevance scores aren't a stable, unique sort key |
| The data never changes while being read (a finished monthly report) | Offset is fine for small depths | The correctness problem (§4) needs concurrent writes |

The honest summary: **keyset for APIs that machines and infinite-scroll UIs consume; offset with a depth cap for "page N of M" UIs; no pagination for small fixed lists.**

---

## 16. Pitfalls

| Pitfall | What happens | Fix |
| --- | --- | --- |
| Sort key not unique | Gaps or duplicates at page boundaries | Always end the sort with a unique column (`id`) |
| `<` vs `<=`, or the wrong direction | Repeats the last row, or returns page 1 forever | `<` for DESC, `>` for ASC; tests that walk several pages |
| The cursor is built from the extra (`limit + 1`) row | The first row of the next page is skipped | Build it from the **last returned** row |
| Mixed sort directions (`created_at DESC, id ASC`) | A single row-value comparison is wrong, and the index can't be used | Same direction for every column |
| Timestamp precision lost in the cursor | The `=` tie-breaker branch never matches | Round-trip format; build from stored values (§8.4) |
| Decoding throws on garbage | Any client can trigger a `500` | `TryDecode` → `400 invalid-cursor` |
| No maximum `limit` | `?limit=1000000` brings back the problem pagination solves | Validate a maximum |
| `COUNT(*)` on every page | The count costs more than the page | No total, or a separate cached/estimated one |
| Sorting by a mutable column | Rows jump pages during a walk | Sort by immutable columns |
| Index doesn't match the query (or filter column not first) | `Seq Scan` + `Sort`: fast in tests, slow in production | `EXPLAIN` with realistic data |
| `next` built as an absolute URL behind a proxy | Links to the internal host | Relative links, or forwarded headers |
| Tests share a singleton repository | Row counts depend on test order | A fresh factory, or filter to the test's own rows |

---

## 17. Glossary

| Term | Meaning |
| --- | --- |
| **Pagination** | Returning a large list in bounded pieces (pages) |
| **Offset pagination** | "Skip N rows, take M" (`OFFSET`/`LIMIT`, `Skip`/`Take`) |
| **Keyset (seek) pagination** | "Rows after this sort key, take M" (`WHERE key < @last … LIMIT`) |
| **Cursor** | An opaque token that encodes where the next page starts; usually the last row's sort key |
| **Opaque** | Clients must not parse, build or change it; the server can change its format freely |
| **Sort key** | The columns a list is ordered by, e.g. `(created_at, id)` |
| **Tie-breaker** | A unique column added last to the sort key so that no two rows compare as equal |
| **Total order** | An ordering where every two different rows compare as strictly less or greater |
| **Row-value comparison** | Comparing tuples: `(a, b) < (x, y)`, ordered like words in a dictionary |
| **Composite index** | An index on several columns; column order matters (equality filters first, then sort columns) |
| **Seek** | Jumping straight to a key in a B-tree index, instead of scanning from the start |
| **Base64Url** | Base64 with URL-safe characters (`-`, `_`) and no padding |
| **`limit + 1`** | Fetching one extra row to learn whether a next page exists without counting |
| **Deep paging** | Requesting pages far from the start; expensive with offset |
| **Snapshot** | A frozen view of the data at one moment; keyset is *not* one |

---

## 18. Summary

1. Every list endpoint needs a **maximum page size**. An unbounded list is a cost that grows every day without a code change.
2. **Offset** counts positions. Its cost grows with depth, and positions shift under inserts and deletes, which causes **duplicates and gaps**.
3. **Keyset** remembers the last row's sort key and continues after it. The cost is constant at any depth, and nothing in front of the client affects it.
4. The sort key must be a **total order**: end it with a unique **tie-breaker** (`id`), and use **immutable** columns.
5. An **index matching the sort** (equality filters first) is part of the design. Prove it with `EXPLAIN`.
6. Expose the key as an **opaque, versioned cursor**. It's not secret and not trusted: invalid cursors are a `400` with their own `type`.
7. Fetch **`limit + 1`** to know whether there's a next page. Return a **`next` link** with all the query parameters, and no `total`.
8. Keyset guarantees "exactly once for rows that stay put", **not** a snapshot.
9. Don't use it for small fixed lists, "page N of M" UIs or bulk exports.

### Check yourself

1. Page 10,000 of a list takes 3 seconds with offset and 4 ms with keyset. Explain the difference in terms of what the database reads. (§3, §5, §7)
2. A newest-first list is paged with offset while new shipments are being created. What does the client see, and why doesn't the same happen with keyset? (§4, §5)
3. Why does sorting by `created_at` alone break keyset pagination, even with a perfect index? Describe the exact rows that go missing. (§6)
4. Your index is `(created_at, id)`. Will it help `WHERE status = 'Pending' ORDER BY created_at DESC, id DESC LIMIT 51`? What index would you add, and why that column order? (§7)
5. A client base64-decodes your cursor, changes the timestamp, and sends it back. What can go wrong, and what must the server do? (§8.3)
6. Why `limit + 1` instead of `COUNT(*)`? What's the bug if you build the cursor from the 51st row? (§9, §16)
7. A shipment's `created_at` is corrected by an admin during a client's walk. What can the client see? (§13)
8. A product manager wants "page 3 of 40" in the dispatcher UI. What do you propose, and what limit do you put on it? (§15)
9. Your pagination test seeds 5,000 rows with the real clock and walks the pages. Why might it pass even if the tie-breaker is broken? (§12.4, §14)

**Before you build: decide and note down** (in `docs/design/`, like the T1 contract decisions; several end up in ADR-004):
- Sort direction: newest first or oldest first? Which do API clients of a shipment list expect?
- Default and maximum `limit`; above the maximum: `400` or clamp?
- What's inside the cursor, and how is it versioned? Where are encoding and decoding done?
- The `invalid-cursor` error: its `type`, status and client action (a new row in `docs/api/errors.md`).
- A cursor with a different `status` filter: allow or reject?
- `next`: relative or absolute? `null` or absent on the last page?
- Stay in memory for T3, or bring in PostgreSQL now? (The module allows "a simple EF `DbContext`"; M03 does the data layer properly. Consider what each choice lets your test prove.)

### Interview questions this prepares you for

- "Design the list endpoint for an API with 100 million rows."
- "Offset vs cursor pagination: what are the trade-offs?"
- "Why do clients see duplicates when paging through a feed, and how do you fix it?"
- "How would you index a query that filters by status and sorts by date?"
- "What goes in a pagination cursor? Should it be encrypted or signed?"
- "Why don't large APIs return a total count?"
- "How does a sync client make sure it doesn't miss records?"

### For FleetTrack

- **Endpoint:** `GET /api/v1/shipments?limit=50&cursor=…&status=…`, sorted by `(created_at, id)`, returning the existing `ShipmentListResponse` with a new `next` field. Adding a field is not a breaking change, which is why T1 chose an object over an array.
- **Where things go (rule 1):** query parameters, validation, cursor encoding and the `next` link in the API layer; a query object and "items + has more" in Application; the keyset query in Infrastructure.
- **Validation:** `limit` limits as attributes, so they appear in `/scalar` (T2). A bad cursor is a new `invalid-cursor` row in `docs/api/errors.md`, with a test.
- **Seeding:** 5,000 rows through the repository, with a fake `TimeProvider` so some timestamps collide on purpose.
- **The ✅ check:** a test that inserts rows *between* page requests proves no duplicates and no gaps, and a second test shows the offset version producing a duplicate. You'll need an offset version to compare against. Keep it test-only or behind a clearly internal path, so it never becomes part of the public contract.
- **Coming next:** T4 adds ETags and `If-Match` (concurrency on a single shipment), M03 moves this query to PostgreSQL with a real composite index and `EXPLAIN`, and M11 adds the tenant to every `WHERE` clause, cursor queries included.
