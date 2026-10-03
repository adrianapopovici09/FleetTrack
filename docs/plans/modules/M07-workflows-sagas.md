# M07 · Workflows, sagas & background work

**Time:** ~9 h · **Prereq:** M06 · **Outcome:** a long-running dispatch process that survives restarts, handles timeouts and compensates, plus correct background processing.

**Why it matters:** real business processes wait on humans and third parties. "How do you handle a distributed transaction?" is a standard interview question, and the senior answer is a saga, not 2PC.

Each task: 📖 **Learn** → 🔨 **Build** → ✅ **Done when**.

---

### T1 · Design the process first
- 📖 **Learn (45 min):** sagas as sequences of local transactions with compensations; orchestration vs choreography. [Saga pattern (Azure Architecture Center)](https://learn.microsoft.com/azure/architecture/patterns/saga) · [Saga pattern (microservices.io)](https://microservices.io/patterns/data/saga.html) · [Compensating transaction pattern](https://learn.microsoft.com/azure/architecture/patterns/compensating-transaction)
- 🔨 **Build:** a sequence diagram: `ShipmentBooked` → Dispatch proposes a vehicle + driver → driver accepts *or* a 15-min timeout fires → escalate to a dispatcher → reassign; after 3 failures, the shipment goes `OnHold` and the customer is notified. List each step's compensation.
- ✅ **Done when:** `docs/design/dispatch-saga.md` has the diagram, the state table and the compensation table.

### T2 · Implement the saga
- 📖 **Learn (40 min):** [Wolverine sagas](https://wolverinefx.net/guide/durability/sagas.html) · [Optimistic concurrency for saga state](https://wolverinefx.net/guide/durability/sagas.html#concurrency)
- 🔨 **Build:** persisted saga state correlated by shipment id, with handlers for `AssignmentProposed`, `DriverAccepted` and `DriverRejected`.
- ✅ **Done when:** a test restarts the app mid-flow and the saga continues from its persisted state.

### T3 · Timeouts
- 📖 **Learn (20 min):** [Wolverine: scheduled messages & saga timeouts](https://wolverinefx.net/guide/messaging/message-bus.html#scheduling-message-delivery-or-execution)
- 🔨 **Build:** schedule `AcceptanceTimedOut` when proposing, and ignore it if acceptance already happened.
- ✅ **Done when:** tests with a fake clock/scheduler show the timeout path escalating, and a late acceptance is handled deterministically (decide and document which wins).

### T4 · Compensation
- 📖 **Learn (15 min):** reread the compensating transaction pattern from T1, focusing on idempotency.
- 🔨 **Build:** a cancellation mid-flow releases the vehicle reservation and notifies the driver; compensations are idempotent.
- ✅ **Done when:** a test cancels at each state and finds no reservation left behind; a duplicate cancellation is harmless.

### T5 · A background job done right
- 📖 **Learn (40 min):** [Background tasks with hosted services](https://learn.microsoft.com/aspnet/core/fundamentals/host/hosted-services) (scoped services inside them, shutdown) · [PeriodicTimer](https://learn.microsoft.com/dotnet/api/system.threading.periodictimer) · [Postgres advisory locks](https://www.postgresql.org/docs/current/explicit-locking.html#ADVISORY-LOCKS)
- 🔨 **Build:** a nightly "stale draft cleanup" `BackgroundService` with `PeriodicTimer`, a scope per run, cancellation on shutdown, and a single runner across instances via `pg_try_advisory_lock`.
- ✅ **Done when:** with two app instances running, the job executes once; stopping the app mid-run exits cleanly within the shutdown timeout.

### T6 · Break it & record the decision
- 🔨 **Build:** remove optimistic concurrency from the saga state and deliver `DriverAccepted` and `AcceptanceTimedOut` at the same moment (a parallel test); observe the inconsistent state, then restore concurrency. Add a section to ADR-009 (or a short new ADR): orchestration vs choreography for dispatch, and why.
- ✅ **Done when:** the race is documented and the decision is recorded.

---

## Quiz → [answers](../answers/M07.md)
1. Why not use a distributed transaction (2PC) across modules/services?
2. Orchestration vs choreography: give one situation that clearly favours each.
3. Why is compensation not the same as rollback? Give a FleetTrack example.
4. What makes a saga transition idempotent, and why is that necessary?
5. How do you implement a timeout in a message-driven saga, and what happens if the "real" reply arrives after it?
6. *Code reading:* a `BackgroundService` injects a `DbContext` in its constructor. What's wrong?
7. How do you make sure a recurring job runs once when the app is scaled to 3 instances?
8. What operational tooling does a saga need in production?
9. When is a saga overkill?
10. *Design:* design hotel + flight + car booking with partial failures and a 10-minute hold on each.

## Design drill (20 min)
"Design the payout process for a ride-sharing platform: driver earnings accumulate, payouts run weekly, the bank API is unreliable, and no driver may be paid twice."

## Review
M06 Q1 · M06 Q4 · M04 Q4

## Exit check
The saga survives restarts, times out, compensates and tolerates concurrency (tested); the background job is single-runner; the design doc is committed.
