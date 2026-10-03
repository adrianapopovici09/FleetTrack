# Tasks — Read → Build → Done when (all 145 days)

The plan ([`../plan.md`](../plan.md)) says **what** each day is for; these files say **how**: the primary sources to read, the concrete implementation, and the objective check that the day is finished.

| Phase | Weeks | File | Focus |
| --- | --- | --- | --- |
| 0 | W1–W2 | [phase-00-foundations.md](phase-00-foundations.md) | toolchain, guardrails, Aspire, tests + CI day one, agent/MCP dev setup, first slice |
| 1 | W3–W5 | [phase-01-contracts-data.md](phase-01-contracts-data.md) | versioning, keyset paging, ETag/concurrency, idempotency keys, generated clients, EF modelling/perf, documents + pgvector baseline, ACL, webhooks, seeding |
| 2 | W6–W8 | [phase-02-ddd-modules.md](phase-02-ddd-modules.md) | glossary, value objects, aggregate invariants, audit/GDPR, domain events, bounded contexts, modular monolith + fitness tests, freight rules |
| 3 | W9–W10 | [phase-03-cqrs.md](phase-03-cqrs.md) | mediator decision + spike, command/query slices, pipeline behaviours, error taxonomy, read models + rebuild, hybrid keyword+vector search, exports, projection lag |
| 4 | W11–W13 | [phase-04-messaging-sagas.md](phase-04-messaging-sagas.md) | broker topology, framework/licence decision, contracts, outbox, inbox/idempotent consumers, dispatch saga with timeouts/compensation, Polly, backpressure, quotas, chaos drills |
| 5 | W14–W16 | [phase-05-realtime-telemetry.md](phase-05-realtime-telemetry.md) | ingest contract + channels, Timescale hypertables, PostGIS geofences, SSE vs SignalR, hub auth, backplane, coalesced push, ETA, alert rules |
| 6 | W17 | [phase-06-caching-performance.md](phase-06-caching-performance.md) | cache keys/invalidation, output caching, BenchmarkDotNet, k6 + SLOs + capacity maths |
| 7 | W18–W19 | [phase-07-security-tenancy.md](phase-07-security-tenancy.md) | OIDC, policy + resource-based authz, API keys/HMAC, secrets + rotation, OWASP API **+ LLM** threat model, RLS + isolation matrix, tenant lifecycle, quotas |
| 8 | W20–W22 | [phase-08-frontend.md](phase-08-frontend.md) | rendering decision, typed client + error mapping, server-state architecture, ops UI, live map, dispatch board, component/E2E/a11y tests, offline slice |
| 9 | W23 | [phase-09-observability.md](phase-09-observability.md) | structured logs, tracing across the broker, dashboards incl. **AI/cost**, SLI/SLO + alerts, operability tooling |
| 10 | W24 | [phase-10-cloud-iac.md](phase-10-cloud-iac.md) | multi-stage images + graceful shutdown, platform ADR, Bicep/Terraform, CD + expand/contract migrations, PITR restore drill, cost |
| 11 | W25–W26 | [phase-11-design-capstone.md](phase-11-design-capstone.md) | 5 system-design write-ups, capstone acceptance + demo, hardening, C4 + "why not" docs, .NET 11 upgrade day, AI-readiness review |
| 12 | W27–W29 | [phase-12-ai-mcp.md](phase-12-ai-mcp.md) | LLM boundary + provider abstraction, prompt/context engineering, tool calling over use cases, **MCP usage and exposure**, RAG on pgvector with citations, agentic workflows with approval gates, evals in CI, AI ops/cost |

## How to use a day

1. **Read** the listed sources (15–40 min) — skim for concepts, keep notes in `docs/journal/`.
2. **Build** the artifact — small, reviewable diff; tests and CI stay green.
3. **Verify** `Done when` literally; if it fails, the day isn't done.
4. **Self-quiz** the same day in [`agent.md`](../../../agent.md) §7 (answers + one worked example per week).
5. **Tick** the day in [`../plan.md`](../plan.md) and, on D6/D7, write/append the ADR and log the session.

## Resource cheat-sheet (by theme)

| Theme | Primary sources |
| --- | --- |
| .NET fundamentals & tooling | [.NET docs](https://learn.microsoft.com/dotnet/) · [support policy / LTS](https://dotnet.microsoft.com/platform/support/policy/dotnet-core) · [Central Package Management](https://learn.microsoft.com/nuget/consume-packages/central-package-management) · [code analysis](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/overview) |
| ASP.NET Core / APIs | [ASP.NET Core docs](https://learn.microsoft.com/aspnet/core/) · [minimal APIs](https://learn.microsoft.com/aspnet/core/fundamentals/minimal-apis) · [Problem Details / RFC 9457](https://www.rfc-editor.org/rfc/rfc9457.html) · [API versioning](https://github.com/dotnet/aspnet-api-versioning/wiki) · [Kiota](https://learn.microsoft.com/openapi/kiota/) |
| Data | [EF Core](https://learn.microsoft.com/ef/core/) · [efficient querying](https://learn.microsoft.com/ef/core/performance/efficient-querying) · [Use The Index, Luke](https://use-the-index-luke.com/) · [Dapper](https://github.com/DapperLib/Dapper) · [pgvector](https://github.com/pgvector/pgvector) · [TimescaleDB](https://docs.tigerdata.com/) · [PostGIS](https://postgis.net/docs/) |
| Architecture & DDD | [Azure Architecture Center](https://learn.microsoft.com/azure/architecture/) · [.NET microservices/DDD guide](https://learn.microsoft.com/dotnet/architecture/microservices/) · [Effective Aggregate Design](https://www.dddcommunity.org/library/vernon_2011/) · [C4 model](https://c4model.com/) · [ADRs](https://adr.github.io/) · [Modular monolith primer](https://www.kamilgrzybek.com/blog/posts/modular-monolith-primer) |
| Messaging & resilience | [Cloud design patterns](https://learn.microsoft.com/azure/architecture/patterns/) · [RabbitMQ tutorials](https://www.rabbitmq.com/tutorials) · [Wolverine](https://wolverinefx.net/) · [MassTransit](https://masstransit.io/) · [Polly](https://www.pollydocs.org/) · [Saga reference](https://learn.microsoft.com/azure/architecture/reference-architectures/saga/saga) |
| Real-time & streams | [SignalR](https://learn.microsoft.com/aspnet/core/signalr/introduction) · [Channels](https://learn.microsoft.com/dotnet/core/extensions/channels) · [MapLibre GL JS](https://maplibre.org/maplibre-gl-js/docs/) · [SSE (MDN)](https://developer.mozilla.org/docs/Web/API/Server-sent_events/Using_server-sent_events) |
| Security & tenancy | [OWASP API Security Top 10](https://owasp.org/API-Security/editions/2023/en/0x11-t10/) · [OWASP ASVS](https://owasp.org/www-project-application-security-verification-standard/) · [Entra identity platform](https://learn.microsoft.com/entra/identity-platform/v2-protocols) · [Keycloak](https://www.keycloak.org/getting-started/getting-started-docker) · [Postgres RLS](https://www.postgresql.org/docs/current/ddl-rowsecurity.html) · [Multitenant guidance](https://learn.microsoft.com/azure/architecture/guide/multitenant/overview) |
| Frontend | [React](https://react.dev/learn) · [TanStack Query](https://tanstack.com/query/latest/docs/framework/react/overview) · [React Hook Form](https://react-hook-form.com/) · [Zod](https://zod.dev/) · [Playwright](https://playwright.dev/docs/intro) · [Testing Library](https://testing-library.com/docs/guiding-principles) · [web.dev performance](https://web.dev/learn/performance) · [WCAG quickref](https://www.w3.org/WAI/WCAG22/quickref/) |
| Observability & ops | [OpenTelemetry .NET](https://opentelemetry.io/docs/languages/net/) · [OTel GenAI conventions](https://opentelemetry.io/docs/specs/semconv/gen-ai/) · [Google SRE book](https://sre.google/sre-book/table-of-contents/) · [Serilog](https://github.com/serilog/serilog-aspnetcore) · [Grafana k6](https://grafana.com/docs/k6/latest/) |
| Cloud & delivery | [Azure Container Apps](https://learn.microsoft.com/azure/container-apps/overview) · [Bicep](https://learn.microsoft.com/azure/azure-resource-manager/bicep/overview) · [Terraform on Azure](https://learn.microsoft.com/azure/developer/terraform/overview) · [Docker for .NET](https://learn.microsoft.com/dotnet/core/docker/build-container) · [Twelve-factor](https://12factor.net/) · [Evolvable DB / expand-contract](https://martinfowler.com/articles/evodb.html) |
| **AI / LLM / MCP** | [`Microsoft.Extensions.AI`](https://learn.microsoft.com/dotnet/ai/microsoft-extensions-ai) · [Azure OpenAI](https://learn.microsoft.com/azure/ai-services/openai/) · [Ollama](https://ollama.com/) · [Model Context Protocol](https://modelcontextprotocol.io/) · [MCP C# SDK](https://github.com/modelcontextprotocol/csharp-sdk) · [OWASP LLM Top 10](https://genai.owasp.org/llm-top-10/) · [GenAI evals](https://learn.microsoft.com/azure/ai-foundry/concepts/evaluation-approach-gen-ai) · [Prompt engineering](https://platform.openai.com/docs/guides/prompt-engineering) |

> **Optional guided tutorials** (if you prefer a course alongside the plan): [.NET + AI tutorial](https://learn.microsoft.com/dotnet/ai/quickstarts/) · [MCP for beginners](https://github.com/microsoft/mcp-for-beginners) · [Generative AI for beginners (.NET)](https://github.com/microsoft/generative-ai-for-beginners) · [.NET Aspire workshop](https://github.com/dotnet/aspire-samples).