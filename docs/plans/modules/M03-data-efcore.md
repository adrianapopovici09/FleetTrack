# M03 · Data with EF Core 10 & PostgreSQL

**Time:** ~13 h · **Prereq:** M02 · **Outcome:** a well-modelled schema, measured queries, correct transactions, and a reasoned split between EF Core and raw SQL.

**Why it matters:** most production performance problems are database problems. Senior engineers are expected to read a query plan, kill N+1s, size indexes and know when the ORM is the wrong tool.

Each task: 📖 **Learn** → 🔨 **Build** → ✅ **Done when**.

---

### T1 · Model the core tables
- 📖 **Learn (60 min):** [Creating and configuring a model](https://learn.microsoft.com/ef/core/modeling/) (read "Grouping configuration" with `IEntityTypeConfiguration`) · [Value conversions](https://learn.microsoft.com/ef/core/modeling/value-conversions) (enums as strings) · [Npgsql: date/time mapping](https://www.npgsql.org/doc/types/datetime.html) (why `timestamptz`) · [EFCore.NamingConventions](https://github.com/efcore/EFCore.NamingConventions) · [What's new in EF Core 10](https://learn.microsoft.com/ef/core/what-is-new/ef-core-10.0/whatsnew) · [Managing migrations](https://learn.microsoft.com/ef/core/managing-schemas/migrations/managing)
- 🔨 **Build:** `Shipment`, `ShipmentStop`, `Customer`, `Vehicle` with configuration classes, snake_case, enums as strings, `timestamptz`. Generate the migration and **read the SQL** (`dotnet ef migrations script`).
- ✅ **Done when:** the migration is reviewed and committed, and there's a Mermaid ER diagram in `docs/architecture/data-model.md`.

### T2 · Realistic data volume
- 📖 **Learn (30 min):** [Bogus](https://github.com/bchavez/Bogus) (read "Determinism") · [Npgsql binary COPY](https://www.npgsql.org/doc/copy.html) · [EF Core: data seeding](https://learn.microsoft.com/ef/core/modeling/data-seeding)
- 🔨 **Build:** an env-gated, deterministic seeder: 50 customers, 500 vehicles, 200k shipments, 600k stops, using binary COPY or batching. Time it.
- ✅ **Done when:** 200k shipments seed in under 60 s, with the time and approach in `docs/perf/M03-seeding.md`.

### T3 · N+1 and cartesian explosion
- 📖 **Learn (45 min):** [Efficient querying](https://learn.microsoft.com/ef/core/performance/efficient-querying) (projections, N+1, tracking) · [Single vs split queries](https://learn.microsoft.com/ef/core/querying/single-split-queries) · [Simple logging](https://learn.microsoft.com/ef/core/logging-events-diagnostics/simple-logging)
- 🔨 **Build:** a naive "shipment list with stops and customer name" that triggers N+1 (log SQL + count commands). Fix it with a projection. Then `Include` two collections and compare with `AsSplitQuery`.
- ✅ **Done when:** `docs/perf/M03-queries.md` shows SQL command count, rows transferred and duration before/after.

### T4 · Index design with EXPLAIN
- 📖 **Learn (60 min):** [Using EXPLAIN](https://www.postgresql.org/docs/current/using-explain.html) · [Multicolumn indexes](https://www.postgresql.org/docs/current/indexes-multicolumn.html) · [Partial indexes](https://www.postgresql.org/docs/current/indexes-partial.html) · [Index-only scans and covering indexes](https://www.postgresql.org/docs/current/indexes-index-only-scans.html) · [Use The Index, Luke: the WHERE clause](https://use-the-index-luke.com/sql/where-clause)
- 🔨 **Build:** three queries: list by `(tenant_id, status)` ordered by `created_at`; lookup by tracking number; "late shipments". Capture `EXPLAIN (ANALYZE, BUFFERS)`, add composite/partial/covering indexes, and measure again.
- ✅ **Done when:** each query has its plan before/after and the reason for the column order you chose.

### T5 · Transactions & bulk operations
- 📖 **Learn (40 min):** [Transactions](https://learn.microsoft.com/ef/core/saving/transactions) · [Connection resiliency & execution strategies](https://learn.microsoft.com/ef/core/miscellaneous/connection-resiliency) · [ExecuteUpdate and ExecuteDelete](https://learn.microsoft.com/ef/core/saving/execute-insert-update-delete) · [Postgres transaction isolation](https://www.postgresql.org/docs/current/transaction-iso.html)
- 🔨 **Build:** "cancel all shipments for a customer" by loading entities vs `ExecuteUpdateAsync`; then a use case touching two tables inside one explicit transaction with the retrying execution strategy.
- ✅ **Done when:** a test proves an exception mid-use-case leaves no partial writes, and both cancel approaches are timed.

### T6 · Dapper for the hot read
- 📖 **Learn (30 min):** [Dapper README](https://github.com/DapperLib/Dapper) · [EF Core: SQL queries](https://learn.microsoft.com/ef/core/querying/sql-queries)
- 🔨 **Build:** the shipment list query in Dapper; benchmark it against the EF projection with BenchmarkDotNet (against the seeded container).
- ✅ **Done when:** a benchmark table plus a decision paragraph: is the difference worth a second data-access style?

### T7 · Break it
- 📖 **Learn (15 min):** [Explicit locking (row-level locks)](https://www.postgresql.org/docs/current/explicit-locking.html)
- 🔨 **Build:** drop the T4 index and run "late shipments" at 200k and then 2M rows. Hold a transaction for 60 s while another session updates the same row, and look at `pg_locks`.
- ✅ **Done when:** you've recorded the timings and the lock you observed.

### T8 · Decide: ADR-006
- 📖 **Learn (20 min):** [Repository pattern: infrastructure persistence layer](https://learn.microsoft.com/dotnet/architecture/microservices/microservice-ddd-cqrs-patterns/infrastructure-persistence-layer-design)
- 🔨 **Build:** **ADR-006** persistence: the EF Core vs Dapper split, repositories (only for aggregates?), and how migrations run in deployment.
- ✅ **Done when:** the ADR has ≥2 options, negative consequences and a revisit trigger.

---

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
10. *Design:* the shipment list endpoint needs 12 filters, 4 sort orders and must answer in under 100 ms at 50M rows. Outline your approach.

## Design drill (20 min)
"Our main table hit 500M rows and queries are slowing down. What are your options?" (Indexes, partitioning, archiving, read replicas, read models, sharding, and their costs.)

## Review
M02 Q1 · M02 Q7 · M01 Q8

## Exit check
Seeded volume, three documented query plans, the measured N+1 fix, the Dapper vs EF benchmark, and ADR-006.
