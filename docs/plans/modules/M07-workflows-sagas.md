# M07 · Workflows, sagas & background work

**Time:** ~8 h · **Prereq:** M06 · **Outcome:** a long-running dispatch process that survives restarts, handles timeouts and compensates, plus correct background processing.

## Why it matters
Real business processes wait on humans and third parties: driver acceptance, appointment confirmations, payments. "How do you handle a distributed transaction?" is a standard interview question, and the senior answer is a saga, not 2PC.

## Concepts
- **Saga:** a sequence of local transactions coordinated by messages, with **compensating actions** instead of rollback.
- **Orchestration vs choreography:** a central saga/process manager vs modules reacting to each other's events. Visibility vs coupling.
- **Saga state:** persisted, versioned (optimistic concurrency), correlated by id; idempotent transitions.
- **Timeouts as events:** scheduled/delayed messages; a timeout is a first-class branch.
- **Compensation ≠ undo:** it's a new business fact ("assignment released"), and must itself be idempotent.
- **Background work in .NET:** `BackgroundService`, scoped services inside it, graceful shutdown, `PeriodicTimer`; Wolverine scheduled messages vs Quartz/Hangfire.

Read: [Saga pattern](https://learn.microsoft.com/azure/architecture/patterns/saga) · [Compensating transaction](https://learn.microsoft.com/azure/architecture/patterns/compensating-transaction) · [Wolverine sagas](https://wolverinefx.net/guide/durability/sagas.html) · [Background tasks with hosted services](https://learn.microsoft.com/aspnet/core/fundamentals/host/hosted-services)

## Labs

- [ ] **L1 · Design the process first.** Sequence diagram: `ShipmentBooked` → Dispatch proposes vehicle + driver → driver accepts *or* 15-minute timeout → escalate to a dispatcher → reassign; after 3 failed attempts → shipment `OnHold` + customer notified. List each step's compensation.
  ✅ `docs/design/dispatch-saga.md` with the diagram, state table and compensation table.
- [ ] **L2 · Implement the saga (Wolverine).** Persisted saga state with correlation by shipment id; handlers for `AssignmentProposed`, `DriverAccepted`, `DriverRejected`.
  ✅ A test: restart the app mid-flow and the saga continues from its persisted state.
- [ ] **L3 · Timeouts.** Schedule `AcceptanceTimedOut` when proposing; ignore it if acceptance already happened (idempotent).
  ✅ Tests with a fake clock/scheduler: the timeout path escalates; a late acceptance after the timeout is handled deterministically (decide and document which wins).
- [ ] **L4 · Compensation.** If the shipment is cancelled mid-flow: release the vehicle reservation and notify the driver. Compensations are idempotent.
  ✅ A test cancels at each state and checks that no reservation is left behind; duplicate cancellation is harmless.
- [ ] **L5 · Background job done right.** A nightly "stale draft cleanup" as a `BackgroundService` with `PeriodicTimer`, a scope per run, cancellation on shutdown, and a single-runner guarantee across instances (Postgres advisory lock).
  ✅ With two app instances running, the job executes once; stopping the app during a run exits cleanly within the shutdown timeout.

## Break it
Remove optimistic concurrency from saga state and deliver `DriverAccepted` and `AcceptanceTimedOut` at the same moment (parallel test). Observe the inconsistent state, then restore concurrency and show the retry resolving it.

## Decide
Add a section to ADR-009 (or a new short ADR): orchestration vs choreography for dispatch, and why.

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
