# M12 · Observability & operations

**Time:** ~8 h · **Prereq:** M11 · **Outcome:** one request traceable across the gateway → monolith → broker → Tracking → SignalR, domain metrics on dashboards, SLO-based alerts, and a practised incident.

## Why it matters
Once you have more than one process, you can't debug with logs on one machine. "How would you find out why 2% of bookings fail?" is a senior/ops question, and in real jobs observability is often the architect's responsibility.

## Concepts
- **Three signals, one correlation:** traces (spans + context), metrics (aggregates), logs (events), joined by `trace_id`.
- **OpenTelemetry in .NET:** `ActivitySource` (traces), `Meter` (metrics), `ILogger` (logs) → OTel SDK → OTLP exporter. Automatic instrumentation for ASP.NET Core, HttpClient, gRPC, Npgsql, EF Core; manual spans for domain work.
- **Context propagation:** W3C `traceparent` across HTTP/gRPC automatically, across the broker via message headers.
- **Structured logging:** source-generated `[LoggerMessage]`, scopes, no string interpolation, PII rules, log levels with intent.
- **Metrics design:** RED (rate, errors, duration) for requests, USE for resources, plus domain metrics (ingest lag, saga duration, DLQ depth); cardinality budgets.
- **SLOs & alerting:** SLI → SLO → error budget → burn-rate alerts; alert on symptoms, not causes; every alert has a runbook.
- **Sampling:** head vs tail sampling and what each loses.

Read: [OpenTelemetry .NET](https://opentelemetry.io/docs/languages/dotnet/) · [.NET observability with OTel](https://learn.microsoft.com/dotnet/core/diagnostics/observability-with-otel) · [High-performance logging](https://learn.microsoft.com/dotnet/core/extensions/high-performance-logging) · [Google SRE: Monitoring](https://sre.google/sre-book/monitoring-distributed-systems/) · [Grafana otel-lgtm](https://github.com/grafana/docker-otel-lgtm)

## Labs

- [ ] **L1 · OTel by hand in `AddFleetTrackDefaults()`.** Add the OpenTelemetry SDK with resource attributes (service name, version, environment), instrumentation (ASP.NET Core, HttpClient, gRPC, Npgsql, EF Core, Wolverine) and OTLP export. Add `grafana/otel-lgtm` to compose.
  ✅ Each service appears in Grafana/Tempo with its own name; one HTTP request shows the gateway → monolith → Postgres spans.
- [ ] **L2 · Trace across the broker and into the push.** Make sure `traceparent` is carried in message headers (verify what Wolverine does; add it if needed). Add manual spans for "geofence evaluation" and "push to hub".
  ✅ One trace shows: booking request → outbox → RabbitMQ → Dispatch saga, and device batch → geofence → event → Shipments → SignalR. Screenshot in `docs/architecture/traces.md`.
- [ ] **L3 · Structured logs, correlated.** Replace string-interpolated logs with `[LoggerMessage]`; scopes with tenant id and shipment id; OTel log export to Loki; a PII rule (no names/addresses in logs) with a test or analyzer.
  ✅ From a trace in Grafana you can jump to its logs and back.
- [ ] **L4 · Domain metrics + dashboard.** `Meter` instruments: `fleettrack.ingest.positions` (counter), `fleettrack.ingest.channel_depth` (gauge), `fleettrack.saga.duration` (histogram), `fleettrack.bookings.failed` (counter by reason), DLQ depth. Build a Grafana dashboard (RED per service + domain panel), exported as JSON into the repo.
  ✅ The dashboard JSON is committed; no metric label has unbounded cardinality (no shipment id or user id labels).
- [ ] **L5 · SLO alerts + runbooks + game day.** Two SLOs (booking success 99.5%, ingest lag < 10 s) with burn-rate alerts in Grafana; a runbook per alert in `docs/ops/runbooks/`. Then a game day: someone (or a script) breaks something (kill RabbitMQ, slow Postgres, wrong config in Tracking); you diagnose it using only dashboards, traces and logs.
  ✅ A game-day report: time to detect, time to diagnose, what was missing, and the improvements you made.

## Break it
Add a metric labelled with `shipment_id` and generate 100k shipments, then watch Prometheus series and memory explode. Then turn on 100% trace sampling under k6 load and measure the overhead.

## Decide
No new ADR. Add an "Observability conventions" doc (`docs/architecture/observability.md`): naming, required attributes, cardinality rules, sampling.

## Quiz → [answers](../answers/M12.md)
1. Traces vs metrics vs logs: what question does each answer best?
2. How does trace context cross a message broker?
3. Why are source-generated `[LoggerMessage]` methods better than `logger.LogInformation($"...")`?
4. What's metric cardinality and why does it matter for cost and stability?
5. RED vs USE: which applies to which component?
6. What's an error budget and a burn-rate alert? Why not alert on "CPU > 80%"?
7. Head sampling vs tail sampling: what does each lose?
8. *Code reading:* `_logger.LogInformation($"Shipment {shipment} booked for {customer.Email}");` Name three problems.
9. What must every alert have?
10. *Design:* design the observability for FleetTrack in production on Azure: what you collect, where it goes, retention, cost controls, and who gets paged.

## Design drill (20 min)
"Users report the checkout is 'sometimes slow' in a 15-service system. Walk me through how you find the cause."

## Review
M09 Q5 · M07 Q8 · M10 Q1

## Exit check
A cross-process trace through the broker and push, correlated logs, a domain dashboard committed, 2 SLO alerts with runbooks, and the game-day report.
