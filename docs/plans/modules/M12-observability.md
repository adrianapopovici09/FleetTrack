# M12 · Observability & operations

**Time:** ~9 h · **Prereq:** M11 · **Outcome:** one request traceable across the gateway → monolith → broker → Tracking → SignalR, domain metrics on dashboards, SLO-based alerts, and a practised incident.

**Why it matters:** once you have more than one process, you can't debug with logs on one machine. "How would you find out why 2% of bookings fail?" is a senior/ops question, and observability is often the architect's responsibility.

Each task: 📖 **Learn** → 🔨 **Build** → ✅ **Done when**.

---

### T1 · OpenTelemetry by hand
- 📖 **Learn (60 min):** traces, metrics and logs joined by `trace_id`; `ActivitySource`, `Meter`, OTLP. [OpenTelemetry concepts: signals](https://opentelemetry.io/docs/concepts/signals/) · [.NET observability with OpenTelemetry](https://learn.microsoft.com/dotnet/core/diagnostics/observability-with-otel) · [OpenTelemetry .NET getting started](https://opentelemetry.io/docs/languages/dotnet/getting-started/) · [grafana/otel-lgtm](https://github.com/grafana/docker-otel-lgtm)
- 🔨 **Build:** add the OTel SDK to `AddFleetTrackDefaults()` with resource attributes (service name, version, environment), instrumentation (ASP.NET Core, HttpClient, gRPC, Npgsql, EF Core, Wolverine) and OTLP export; add `grafana/otel-lgtm` to compose.
- ✅ **Done when:** each service appears in Grafana Tempo under its own name, and one HTTP request shows gateway → monolith → Postgres spans.

### T2 · Trace across the broker and into the push
- 📖 **Learn (30 min):** [W3C Trace Context](https://www.w3.org/TR/trace-context/) (sections 1–3) · [OTel context propagation](https://opentelemetry.io/docs/concepts/context-propagation/) · [Messaging spans semantic conventions](https://opentelemetry.io/docs/specs/semconv/messaging/messaging-spans/)
- 🔨 **Build:** make sure `traceparent` travels in message headers (check what Wolverine does; add it if needed); add manual spans for "geofence evaluation" and "push to hub".
- ✅ **Done when:** one trace shows booking → outbox → RabbitMQ → Dispatch saga, and device batch → geofence → event → Shipments → SignalR (screenshots in `docs/architecture/traces.md`).

### T3 · Structured, correlated logs
- 📖 **Learn (30 min):** [Compile-time logging source generation](https://learn.microsoft.com/dotnet/core/extensions/logger-message-generator) · [Logging in .NET: scopes](https://learn.microsoft.com/dotnet/core/extensions/logging#log-scopes) · [Redaction in .NET](https://learn.microsoft.com/dotnet/core/extensions/data-redaction)
- 🔨 **Build:** replace interpolated logs with `[LoggerMessage]`; add scopes with tenant id and shipment id; export logs to Loki via OTel; add a PII rule (no names/addresses in logs) with a test, an analyzer or redaction.
- ✅ **Done when:** in Grafana you can jump from a trace to its logs and back.

### T4 · Domain metrics + dashboard
- 📖 **Learn (40 min):** [Creating metrics in .NET](https://learn.microsoft.com/dotnet/core/diagnostics/metrics-instrumentation) · [The RED method (Grafana)](https://grafana.com/blog/2018/08/02/the-red-method-how-to-instrument-your-services/) · [Prometheus: naming & label cardinality](https://prometheus.io/docs/practices/naming/)
- 🔨 **Build:** `Meter` instruments: `fleettrack.ingest.positions` (counter), `fleettrack.ingest.channel_depth` (gauge), `fleettrack.saga.duration` (histogram), `fleettrack.bookings.failed` (counter by reason), DLQ depth. Build a Grafana dashboard (RED per service + a domain panel) and export the JSON into the repo.
- ✅ **Done when:** the dashboard JSON is committed and no label has unbounded cardinality.

### T5 · SLO alerts, runbooks & a game day
- 📖 **Learn (45 min):** [Google SRE workbook: alerting on SLOs](https://sre.google/workbook/alerting-on-slos/) (burn rates) · [Grafana alerting](https://grafana.com/docs/grafana/latest/alerting/) · [Google SRE: postmortem culture](https://sre.google/sre-book/postmortem-culture/)
- 🔨 **Build:** two SLOs (booking success 99.5%, ingest lag < 10 s) with burn-rate alerts and a runbook per alert in `docs/ops/runbooks/`. Then a game day: a script (or someone else) breaks something (kill RabbitMQ, slow Postgres, wrong config in Tracking), and you diagnose it using only dashboards, traces and logs.
- ✅ **Done when:** a game-day report records time to detect, time to diagnose, what was missing, and the improvements you made.

### T6 · Break it & write conventions
- 🔨 **Build:** add a metric labelled with `shipment_id`, generate 100k shipments, and watch the series count explode; turn on 100% trace sampling under k6 and measure the overhead. Then write `docs/architecture/observability.md`: naming, required attributes, cardinality rules, sampling.
- ✅ **Done when:** both measurements are recorded and the conventions doc exists.

---

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
10. *Design:* design the observability for FleetTrack in production: what you collect, where it goes, retention, cost controls, and who gets paged.

## Design drill (20 min)
"Users report the checkout is 'sometimes slow' in a 15-service system. Walk me through how you find the cause."

## Review
M09 Q5 · M07 Q8 · M10 Q1

## Exit check
A cross-process trace through the broker and push, correlated logs, a committed domain dashboard, 2 SLO alerts with runbooks, and the game-day report.
