# Phase 9 — Learn & Build Tasks (W23: observability & operations)

> **Read** → **Build** → **Done when**. Questions: `agent.md` §7. Checkbox: [`../plan.md`](../plan.md). Extra reading: [OpenTelemetry .NET](https://opentelemetry.io/docs/languages/net/) · [OpenTelemetry GenAI semantic conventions](https://opentelemetry.io/docs/specs/semconv/gen-ai/) · [Google SRE: monitoring](https://sre.google/sre-book/monitoring-distributed-systems/) · [Structured logging (Microsoft)](https://learn.microsoft.com/dotnet/core/extensions/logging)

## Week 23 — Observability as a design activity

### W23D1 · Structured logging as a design tool
**Read:** [Logging in .NET (high-performance)](https://learn.microsoft.com/dotnet/core/extensions/logging) · [Serilog ASP.NET Core](https://github.com/serilog/serilog-aspnetcore) · [Serilog message templates](https://messagetemplates.org/)
**Build:** Serilog with console + Seq sinks; event-id-based message templates; scopes carrying tenant/user/correlation; PII redaction; log-level policy per environment; wire the collector in the AppHost.
**Done when:** `grep`-free queries in Seq answer "what happened to shipment X" and no PII appears in any log line.

### W23D2 · Distributed tracing end-to-end
**Read:** [OTel .NET instrumentation](https://opentelemetry.io/docs/languages/net/instrumentation/) · [W3C trace context](https://www.w3.org/TR/trace-context/) · [Messaging semantic conventions](https://opentelemetry.io/docs/specs/semconv/messaging/)
**Build:** spans across HTTP → handler → EF → outbox → broker → consumer → SignalR; `traceparent` propagation through messages; trace↔log linking (trace id in logs); one captured trace of a full journey in `docs/ops/`.
**Done when:** a booking request and its downstream consumer/push share one trace id, including across the async gap.

### W23D3 · Metrics & dashboards (incl. AI)
**Read:** [OTel metrics](https://opentelemetry.io/docs/concepts/signals/metrics/) · [Prometheus exposition format](https://prometheus.io/docs/instrumenting/exposition_formats/) · [GenAI semantic conventions](https://opentelemetry.io/docs/specs/semconv/gen-ai/)
**Build:** three dashboards — (1) API RED (rate/errors/duration), (2) domain health (ingest lag, saga duration + stuck count, DLQ depth, projection lag, cache hit rate), (3) AI & cost (tokens in/out, cost per tenant/feature, model + prompt version, tool-call latency/errors, refusal rate).
**Done when:** each dashboard explains a plausible incident, and cardinality is bounded (no per-shipment labels).

### W23D4 · SLIs, SLOs & alerting
**Read:** [SLOs (Google SRE)](https://sre.google/sre-book/service-level-objectives/) · [Alerting on SLOs](https://sre.google/workbook/alerting-on-slos/) · [Prometheus alerting rules](https://prometheus.io/docs/prometheus/latest/configuration/alerting_rules/)
**Build:** `docs/ops/slo.md` (SLIs, SLO targets, error budget, burn-rate windows) + alert rules that are symptom-based, each linking to a runbook section.
**Done when:** a simulated failure raises exactly one actionable alert (no storm) and the runbook step fixes it.

### W23D5 · Operability by design
**Read:** [Health checks](https://learn.microsoft.com/aspnet/core/host-and-deploy/health-checks) · [Aspire dashboard + MCP](https://learn.microsoft.com/dotnet/aspire/fundamentals/dashboard/overview)
**Build:** ops endpoints for saga state, DLQ inspection + replay, pausing a consumer, and rebuilding a projection; tenant-admin view of own usage/quotas; connect the Aspire dashboard MCP so the coding agent can query local telemetry.
**Done when:** every "3 a.m. action" in the runbook is executable through tooling you built (no manual SQL).

### D6 · Integrate
Replay a failure drill and reconstruct the incident purely from traces, metrics and logs; write down what was missing.

### D7 · Review
Cardinality/cost of metrics, alert fatigue, sampling trade-offs; week 23 quiz.

**Phase gate 9:** one journey traceable end to end · SLOs + alerts live · a runbook a colleague could follow.