# Phase 12 — Learn & Build Tasks (W27–W29: AI, LLM & MCP architecture)

> **Read** → **Build** → **Done when**. Questions: `agent.md` §7. Checkbox: [`../plan.md`](../plan.md).
> **Extra reading:** [`Microsoft.Extensions.AI`](https://learn.microsoft.com/dotnet/ai/microsoft-extensions-ai) · [Azure OpenAI / Foundry](https://learn.microsoft.com/azure/ai-services/openai/) · [Model Context Protocol spec](https://modelcontextprotocol.io/) · [MCP C# SDK](https://github.com/modelcontextprotocol/csharp-sdk) · [OWASP LLM Top 10](https://genai.owasp.org/llm-top-10/) · [OpenTelemetry GenAI conventions](https://opentelemetry.io/docs/specs/semconv/gen-ai/) · [Ollama](https://ollama.com/)

## Week 27 — LLM integration foundations

### W27D1 · Where AI belongs in the architecture (**ADR-017**)
**Read:** [`Microsoft.Extensions.AI` overview](https://learn.microsoft.com/dotnet/ai/microsoft-extensions-ai) · [OWASP LLM Top 10 (excessive agency)](https://genai.owasp.org/llm-top-10/) · [AI in layered architectures](https://learn.microsoft.com/dotnet/architecture/)
**Build:** a candidate-use-case worksheet rating each AI idea on value, risk, latency and cost, plus the rule that the assistant is an **adapter** over existing use cases with deterministic core logic and a kill switch.
**Done when:** ADR-017 lists ≥3 use cases you rejected and the deterministic owner of every rule the model must not decide (pricing, legality, authorisation).

### W27D2 · Provider abstraction (**ADR-018**)
**Read:** [`IChatClient` / `IEmbeddingGenerator`](https://learn.microsoft.com/dotnet/ai/microsoft-extensions-ai) · [Azure OpenAI .NET quickstart](https://learn.microsoft.com/azure/ai-services/openai/quickstart) · [Ollama + .NET](https://learn.microsoft.com/dotnet/ai/quickstarts/quickstart-local-ai)
**Build:** register `IChatClient` + `IEmbeddingGenerator` behind your own interfaces; wire Azure OpenAI/Foundry for cloud and **Ollama** for local/CI; add streaming, cancellation, timeouts, retry and a small-model/large-model routing policy; keys via Key Vault/user-secrets.
**Done when:** switching providers or models is a configuration change (prove it: run the same use case against Ollama locally and Azure in staging), and prompt/model versions are recorded per call.

### W27D3 · Prompt & context engineering
**Read:** [Prompt engineering (OpenAI)](https://platform.openai.com/docs/guides/prompt-engineering) · [Prompt engineering (Azure OpenAI)](https://learn.microsoft.com/azure/ai-services/openai/concepts/prompt-engineering) · [Token counting](https://learn.microsoft.com/dotnet/ai/)
**Build:** prompts as versioned files (`prompts/shipment-assistant.system.md`) with variables; context assembled from **read models** only; few-shot examples for tool selection; an explicit token budget with truncation/summarisation; temperature 0 where determinism matters.
**Done when:** no prompt string is inlined in code, every prompt has a version + owner, and an over-budget context degrades gracefully (test).

### W27D4 · Tool calling over your own use cases
**Read:** [Function calling (Azure OpenAI)](https://learn.microsoft.com/azure/ai-services/openai/how-to/function-calling) · [AI function calling in .NET (`AIFunction`)](https://learn.microsoft.com/dotnet/ai/microsoft-extensions-ai) · [OWASP LLM Top 10 (excessive agency)](https://genai.owasp.org/llm-top-10/)
**Build:** a tool catalogue over existing use cases — `get_shipment`, `list_delayed_shipments`, `get_last_position`, `assign_vehicle` (write) — with strict JSON schemas (`additionalProperties: false`), tenant/role authorisation **inside** the tool, idempotency keys, an `ApprovalRequest` for writes, and a `ToolInvocation` audit row for every call (including denials).
**Done when:** the assistant can answer "where is SHP-2026-000123?" and "assign vehicle X" only ends in an approval request, with the audit trail proving both.

### W27D5 · Structured outputs & validation
**Read:** [Structured outputs](https://platform.openai.com/docs/guides/structured-outputs) · [JSON Schema](https://json-schema.org/) · [Guardrails/validation patterns](https://learn.microsoft.com/azure/architecture/patterns/)
**Build:** schema-constrained responses with validation, a bounded repair retry, an explicit refusal path, and a deterministic fallback when the model is unavailable.
**Done when:** invalid model output never reaches the domain (test with a mocked bad response), and "I can't answer that" works.

### D6 · Integrate
Assistant host behind a feature flag + kill switch; every model call traced (model, prompt version, tokens in/out, latency, cost); per-request token/timeout budgets enforced.

### D7 · Review
When NOT to use an LLM (pricing, tax, compliance, routing maths, anything a `SELECT` answers); week 27 quiz.

## Week 28 — MCP: using it, and exposing FleetTrack through it

### W28D1 · MCP fundamentals (**ADR-019**)
**Read:** [Model Context Protocol — introduction](https://modelcontextprotocol.io/docs/getting-started/intro) · [MCP architecture & concepts](https://modelcontextprotocol.io/docs/learn/architecture) · [MCP servers list](https://github.com/modelcontextprotocol/servers)
**Build:** a written comparison — MCP vs bespoke plugin API vs plain HTTP integration — with tools/resources/prompts mapping, stdio vs HTTP/SSE transports, capability negotiation and versioning; plus a spike server exposing one read tool.
**Done when:** you can explain when MCP wins (many clients, discoverable tools) and when a normal API is the right answer; ADR-019 records both.

### W28D2 · Using MCP in the development workflow
**Read:** [MCP in VS Code](https://code.visualstudio.com/docs/copilot/chat/mcp-servers) · [GitHub MCP server](https://github.com/github/github-mcp-server) · [Postgres MCP server](https://github.com/modelcontextprotocol/servers/tree/main/src/postgres)
**Build:** `.mcp.json` with **read-only** servers (filesystem scoped to the repo, git/GitHub, read-only Postgres, OpenAPI, Aspire dashboard); `AGENTS.md` with the dependency rule, ADR rule, test expectations and an explicit denial list (no prod data, no write tokens, no cloud credentials).
**Done when:** the coding agent can inspect schema/OpenAPI/telemetry but cannot write to the database, and the config is committed and reviewable.

### W28D3 · Exposing FleetTrack as an MCP server (**ADR-020**)
**Read:** [MCP C# SDK](https://github.com/modelcontextprotocol/csharp-sdk) · [MCP tools spec](https://modelcontextprotocol.io/specification/2025-06-18/server/tools) · [MCP resources spec](https://modelcontextprotocol.io/specification/2025-06-18/server/resources)
**Build:** `FleetTrack.Mcp` server: read tools (`get_shipment`, `list_delayed_shipments`, `get_last_position`, `search_documents`), resource templates for projections (`shipment://{id}/timeline`), one write tool that only drafts + raises `ApprovalRequest`; authentication reusing the API's claims and tenant scoping; per-tenant call/token caps.
**Done when:** an MCP client completes a real journey (look up → search documents → create draft → approve), and cross-tenant calls are refused and audited.

### W28D4 · MCP security & operations
**Read:** [OWASP LLM Top 10](https://genai.owasp.org/llm-top-10/) · [MCP security best practices](https://modelcontextprotocol.io/specification/2025-06-18/basic/security_best_practices) · [Prompt injection (OWASP)](https://genai.owasp.org/llmrisk/llm01-prompt-injection/)
**Build:** a threat model for the MCP surface — tool poisoning/masquerading, prompt injection arriving via tool output or documents, confused deputy via over-broad scopes, data exfiltration, unbounded consumption, tool-contract drift — with controls (allow-lists, least privilege, approval gates, caps, versioned tool contracts, full invocation audit).
**Done when:** ≥1 red-team test proves an injection attempt inside a retrieved document cannot trigger a write.

### W28D5 · Tool/prompt evals & release gates
**Read:** [Evaluating AI (Azure AI Foundry)](https://learn.microsoft.com/azure/ai-foundry/concepts/evaluation-approach-gen-ai) · [OpenAI evals guide](https://platform.openai.com/docs/guides/evals)
**Build:** a golden task suite (tool selection, argument correctness, refusals, tenant boundaries) wired to CI as a gate, plus a prompt/tool version registry with rollback.
**Done when:** changing a tool schema or prompt without updating evals fails CI, and a deliberate regression is caught before merge.

### D6 · Integrate
End-to-end demo: MCP client → FleetTrack MCP server → authorised use case → approval gate → audited action, all in one trace.

### D7 · Review
MCP design trade-offs (one server vs many, scopes, when to use MCP vs HTTP); week 28 quiz.

## Week 29 — Retrieval, agents & AI operations

### W29D1 · RAG architecture on Postgres (**ADR-021**)
**Read:** [pgvector](https://github.com/pgvector/pgvector) · [Chunking strategies (Azure AI Search)](https://learn.microsoft.com/azure/search/vector-search-how-to-chunk-documents) · [Hybrid search](https://learn.microsoft.com/azure/search/hybrid-search-ranking)
**Build:** ingestion pipeline for shipment documents → chunking per document type (BOL, POD, notes; different sizes/overlap) → embeddings via `IEmbeddingGenerator` into `document_chunk` → retrieval with **metadata filters** (tenant, shipment, date) + **hybrid search (FTS + vector)** + rerank; idempotent re-embedding when the model changes; chunk deletion with the document (GDPR).
**Done when:** retrieval on a 500-document corpus returns the right chunks for 20 hand-written questions (measured recall@5), and a re-embed run is idempotent.

### W29D2 · Grounding, citations & freshness
**Read:** [RAG guidance (Azure)](https://learn.microsoft.com/azure/ai-services/openai/concepts/use-your-data) · [Responsible AI: transparency notes](https://learn.microsoft.com/azure/ai-services/openai/concepts/use-your-data)
**Build:** a grounded-answer contract — answers only from retrieved context, each claim citing document + section (link), freshness shown next to time-sensitive data, explicit refusal when evidence is weak, and a feedback control that stores thumbs + comment.
**Done when:** a test proves an unanswerable question yields a refusal (not a guess), and every answered claim carries a resolvable citation.

### W29D3 · Agentic workflows with human gates (**ADR-023**)
**Read:** [AI agent design patterns](https://learn.microsoft.com/azure/architecture/ai-ml/guide/ai-agents) · [Human-in-the-loop patterns](https://learn.microsoft.com/azure/architecture/ai-ml/guide/ai-agents#human-in-the-loop) · [OWASP LLM Top 10 (excessive agency)](https://genai.owasp.org/llm-top-10/)
**Build:** one multi-step workflow (e.g. "find late shipments, draft re-plan proposals, ask for approval, apply on approval") with approval gates for consequential writes, step/timeout/token ceilings, deterministic authorisation, idempotent effects and stuck-state detection.
**Done when:** the workflow cannot exceed its budgets, cannot write without approval, and re-running it does not double-apply actions.

### W29D4 · Evals & quality gates (**ADR-022**)
**Read:** [Evaluation approach for GenAI](https://learn.microsoft.com/azure/ai-foundry/concepts/evaluation-approach-gen-ai) · [LLM-as-a-judge](https://learn.microsoft.com/azure/ai-foundry/concepts/evaluation-model-as-a-judge)
**Build:** a 60-case golden suite (tool selection, grounded answers, refusals, tenant boundaries, red-team prompts) + deterministic checks and a judged quality score with thresholds; run it in CI (`--fail-under`), store `EvalRun` rows (pass rate, groundedness, p95 latency, cost) and plot them over prompt/model versions.
**Done when:** CI blocks a merge when quality drops below threshold or cost per case exceeds budget, and the dashboard plots quality + cost history.

### W29D5 · AI operations & cost
**Read:** [GenAI semantic conventions](https://opentelemetry.io/docs/specs/semconv/gen-ai/) · [Manage costs for Azure OpenAI](https://learn.microsoft.com/azure/ai-services/openai/how-to/manage-costs) · [Prompt caching](https://platform.openai.com/docs/guides/prompt-caching)
**Build:** per-tenant token/cost caps with graceful degradation, cost + latency dashboards per feature, PII redaction on prompts/logs, content-safety filtering, a model/prompt rollback runbook, an incident playbook for bad answers, and a written "what the AI may and may not do" statement.
**Done when:** a simulated cost spike alerts before the cap, a rollback is executed once end to end, and the playbook has been walked through with someone else.

### D6 · Integrate
Wire the eval gate into CI and the AI dashboards into the observability stack; finalise the AI runbook in `docs/ops/ai-runbook.md`.

### D7 · Review — phase gate 12
Assistant + MCP server in the repo, ≥1 eval suite gating CI, per-tenant cost caps and dashboards, AI threat model + runbook, ADR-017…023 accepted; week 29 quiz.