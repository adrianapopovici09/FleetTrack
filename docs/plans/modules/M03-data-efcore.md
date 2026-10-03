# M03 · Data with EF Core 10 & PostgreSQL

**Time:** ~12 h · **Prereq:** M02 · **Outcome:** a well-modelled schema, measured queries, correct transactions, and a reasoned split between EF Core and raw SQL.

## Why it matters
Most production performance problems are database problems. Senior engineers are expected to read a query plan, kill N+1s, size indexes and know when the ORM is the wrong tool.

## Concepts
- **Modelling:** `IEntityTypeConfiguration<T>`, snake_case naming (`EFCore.NamingConventions`), enums as strings, `timestamptz`, decimal precision, owned/complex types, JSON columns, EF 10 named query filters.
- **Migrations:** reviewable SQL (`dotnet ef migrations script`), idempotent scripts, migration bundles, never `EnsureCreated` in prod.
- **Query performance:** projections to DTOs, `AsNoTracking`, split queries vs cartesian explosion, compiled queries, `ExecuteUpdate`/`ExecuteDelete`, N+1 detection.
- **Postgres:** `EXPLAIN (ANALYZE, BUFFERS)`, B-tree vs GIN vs BRIN, composite index column order, partial indexes, covering indexes (`INCLUDE`).
- **Transactions & concurrency:** unit of work = one `SaveChanges`, explicit transactions, isolation levels, `xmin` concurrency token, retrying execution strategy.
- **EF vs Dapper:** EF for aggregates and writes; Dapper/raw SQL for reporting and hot reads.

Read: [EF Core performance](https://learn.microsoft.com/ef/core/performance/) · [What's new in EF Core 10](https://learn.microsoft.com/ef/core/what-is-new/ef-core-10.0/whatsnew) · [Npgsql EF provider](https://www.npgsql.org/efcore/) · [Use The Index, Luke](https://use-the-index-luke.com/) · [Postgres EXPLAIN](https://www.postgresql.org/docs/current/using-explain.html)

## Labs

- [ ] **L1 · Model the core tables.** `Shipment`, `ShipmentStop`, `Customer`, `Vehicle` with configurations, snake_case, enums as strings, `timestamptz`. Generate the migration and **read the SQL** (`migrations script`).
  ✅ Migration reviewed and committed; an ER diagram (Mermaid) in `docs/architecture/data-model.md`.
- [ ] **L2 · Realistic volume.** A **Bogus** seeder (env-gated, deterministic seed): 50 customers, 500 vehicles, 200k shipments, 600k stops. Use `COPY` (Npgsql binary import) or batched inserts, and time the seeding.
  ✅ Seeding 200k shipments takes < 60 s; the time and approach are in `docs/perf/M03-seeding.md`.
- [ ] **L3 · Hunt an N+1 and a cartesian explosion.** Write a naive "shipment list with stops and customer name" using lazy patterns, and log SQL (`LogTo` + command count). Fix it with a projection; then try `Include` of two collections and compare `AsSplitQuery`.
  ✅ Before/after: SQL command count, rows transferred and duration, in `docs/perf/M03-queries.md`.
- [ ] **L4 · Index design with EXPLAIN.** Three real queries: list by `(tenant_id, status)` ordered by `created_at`, lookup by tracking number, "late shipments" (planned delivery < now and status not delivered). Capture `EXPLAIN (ANALYZE, BUFFERS)`, add indexes (composite, partial, covering), and measure again.
  ✅ For each query: plan before/after, chosen index and why that column order.
- [ ] **L5 · Transactions & bulk ops.** A "cancel all shipments for a customer" use case: compare loading + modifying entities vs `ExecuteUpdateAsync`. Then a use case touching two tables in one explicit transaction with the retrying execution strategy.
  ✅ A test proves an exception mid-use-case leaves no partial writes; timings for both cancel approaches.
- [ ] **L6 · Dapper for the hot read.** Implement the shipment list query with Dapper and compare it with the EF projection using BenchmarkDotNet (against the seeded container).
  ✅ Benchmark table, plus a decision paragraph: is the difference worth a second data-access style?

## Break it
Remove the index from L4 and run the late-shipments query at 200k rows, then at 2M rows. Separately, open a transaction and hold it for 60 s while another session updates the same row. Observe the lock in `pg_locks`.

## Decide
**ADR-006** Persistence: EF Core vs Dapper split, repositories (only for aggregates?), migrations in deployment.

## Quiz → [answers](../answers/M03.md)
1. Why `timestamptz` instead of `timestamp` for freight events?
2. What causes a cartesian explosion with `Include`, and what are the trade-offs of `AsSplitQuery`?
3. Composite index `(tenant_id, status, created_at)`: which of these queries can use it efficiently: filter by `status` only; `tenant_id` + `status` ordered by `created_at`; `tenant_id` ordered by `created_at`?
4. When is a partial index the right tool? Give a FleetTrack example.
5. When would you choose BRIN over B-tree?
6. *Code reading:* `var list = db.Shipments.ToList().Where(s => s.Status == "Late").Take(10);` What's wrong?
7. What does `AsNoTracking` save, and when would it be a bug to use it?
8. `ExecuteUpdateAsync` bypasses the change tracker. Name two consequences you must handle.
9. How does the `xmin` concurrency token work in Postgres, and what does EF do when it doesn't match?
10. *Design:* the shipment list endpoint needs 12 filters, 4 sort orders and must answer in < 100 ms at 50M rows. Outline your approach (indexes, read model, search engine?).

## Design drill (20 min)
"Our main table hit 500M rows and queries are slowing down. What are your options?" (Indexes, partitioning, archiving, read replicas, read models, sharding, and their costs.)

## Review
M02 Q1 · M02 Q7 · M01 Q8

## Exit check
Seeded volume, three documented query plans, the N+1 fix measured, the Dapper vs EF benchmark, and ADR-006.
