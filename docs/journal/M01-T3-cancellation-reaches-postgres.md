# M01-T3: cancellation reaches Postgres

* **Date:** 2026-10-21
* **Task:** M01-T3 (Cancellation end to end)
* **Note:** drafted by Claude from my runs and answers in the session. Reread it and rewrite anything I couldn't explain myself.

## What I did

I built a lab ([labs/M01-T3-cancellation](../../labs/M01-T3-cancellation/)) with three layers, passing the request's `CancellationToken` all the way down:

- **Endpoint** (`GET /report`): takes the request token (`RequestAborted`), calls the service, and catches `OperationCanceledException`. If the request token is cancelled, the client is gone, so it rethrows. Otherwise it was our own timeout, so it logs an error and returns a **504** Problem response.
- **Service:** creates a linked token from the request token plus `CancelAfter(2 s)` (disposed with `using`) and passes the **linked** token down.
- **Repository:** opens a connection from `NpgsqlDataSource`, runs `SELECT pg_sleep(10)` with the token, logs the exception and its inner exception, and rethrows.

Then I tried three cases against the Compose Postgres, watching `pg_stat_activity` from `psql` ([check.sql](../../labs/M01-T3-cancellation/check.sql)):

1. **Client abort:** `curl.exe -i http://localhost:5001/report`, then Ctrl+C after about a second.
2. **Timeout:** the same request without aborting.
3. **Probe:** the token removed from `ExecuteReaderAsync`, then an abort again.

## What I observed

**The client abort.** The cancellation surfaced first in the repository, then in the service and the endpoint, as the same exception:

```
Repository: cancellation surfaced as OperationCanceledException (inner: PostgresException)
Service:    cancellation surfaced as System.OperationCanceledException (inner: Npgsql.PostgresException)
Program:    cancellation surfaced as OperationCanceledException (inner: PostgresException)
```

The endpoint rethrew, so no error was logged and no response was attempted. In Postgres, the connection went back to `idle` long before the 10 seconds were up:

```
 pid  | state | wait_event |   running_for   |        query
------+-------+------------+-----------------+---------------------
 2534 | idle  | ClientRead | 00:00:02.633594 | SELECT pg_sleep(10)
```

`idle` means the query is no longer running; `query` only shows the last statement that connection ran.

**The timeout.** After 2 s the same chain happened, but the endpoint saw that the request token was *not* cancelled and returned a 504:

```
fail: Program[0]
      Operation timed out: Query was cancelled
      System.OperationCanceledException: Query was cancelled
       ---> Npgsql.PostgresException (0x80004005): 57014: canceling statement due to user request
```

**Both cases failed in Postgres with the same error, `57014: canceling statement due to user request`.**

**The probe** (no token on the query): one snapshot showed the query `active` (`PgSleep`) after 2.9 s. That doesn't prove much on its own, since a normal running request looks the same. **Still to confirm:** a snapshot taken after the abort, showing it still `active` at 6–9 s.

**A bug in my logging, now fixed:** the "client aborted" flag was first printed as `!cancellationToken.IsCancellationRequested`, so it said `False` for the real abort and `True` for the timeout. The behaviour was right; only the label was inverted. Now the endpoint's abort branch logs `yes`, the service checks the **request** token, and the repository logs `unknown`, because it only sees the linked token and can't tell the two causes apart. A rerun showed the right labels:

```
abort:   Repository … client aborted: unknown   Service … yes   Program … yes            (no error, no response)
timeout: Repository … client aborted: unknown   Service … no    fail: Operation timed out → 504 in 2.1 s
```

## What would happen in production

Without the token reaching Npgsql, every request the client abandons keeps running in Postgres: it holds a pooled connection and uses database CPU and I/O for a result nobody will read. Under load, impatient users retry, so each retry adds another query that runs to the end. The connection pool (100 by default) fills up with abandoned work, and new requests have to wait for a connection, much like the blocked threads in T2.

The timeout has the same weakness. It's just another token feeding the same linked token, so if the token isn't passed to the query, neither the client abort nor the timeout can stop it.

## Why

- Cancellation in .NET is **cooperative**: the token only *requests* a stop, and each layer has to pass it on. Npgsql reacts by sending Postgres a **cancel request on a separate connection**. Postgres interrupts the query with error 57014, and Npgsql turns that into an `OperationCanceledException`, so the connection stays usable and goes back to the pool.
- Postgres reports a client abort and a timeout **with the same error**, because both arrive as the same cancel request. Only the **endpoint** can tell them apart, by checking whether the request token is cancelled. That's why the decision is made at the edge, and why the lower layers just log and rethrow instead of swallowing the exception.
- A client abort isn't an error (nobody is waiting), so it's rethrown quietly. A timeout means the client is still waiting and we gave up, so it's a real failure: it's logged and returned as a 504, not a 500.

## The fix / the rule I'll follow

Every async method that touches I/O accepts a `CancellationToken` and passes it all the way down to the database call, and I check in `pg_stat_activity` that the query actually stops. Cancellation exceptions travel up untouched; only the edge decides between "the client left" (rethrow quietly) and "we timed out" (log it and return 504).
