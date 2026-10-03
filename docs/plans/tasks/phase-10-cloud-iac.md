# Phase 10 — Learn & Build Tasks (W24: cloud architecture, IaC & delivery)

> **Read** → **Build** → **Done when**. Questions: `agent.md` §7. Checkbox: [`../plan.md`](../plan.md). Extra reading: [.NET container images](https://learn.microsoft.com/dotnet/core/docker/build-container) · [Azure Container Apps](https://learn.microsoft.com/azure/container-apps/overview) · [Bicep docs](https://learn.microsoft.com/azure/azure-resource-manager/bicep/overview) · [Expand/contract migrations](https://martinfowler.com/articles/evodb.html) · [Twelve-factor](https://12factor.net/)

## Week 24 — Cloud platform, IaC and pipelines

### W24D1 · Containerisation & runtime behaviour
**Read:** [Multi-stage Dockerfile for .NET](https://learn.microsoft.com/dotnet/core/docker/build-container) · [App health/liveness probes](https://learn.microsoft.com/azure/container-apps/health-probes) · [Graceful shutdown](https://learn.microsoft.com/dotnet/core/extensions/generic-host#host-shutdown)
**Build:** multi-stage Dockerfiles for API, worker and web (non-root user, `chiseled`/slim runtime, no SDK in final image); health vs liveness endpoints; bounded shutdown that drains requests and message handlers; resource limits declared.
**Done when:** the image is <200 MB, runs as non-root, and a rolling restart loses zero in-flight requests or messages (prove with a load test during deploy).

### W24D2 · Platform decision (**ADR-016**)
**Read:** [Container Apps vs AKS vs App Service](https://learn.microsoft.com/azure/architecture/guide/technology-choices/compute-decision-tree) · [Container Apps scaling](https://learn.microsoft.com/azure/container-apps/scale-app)
**Build:** ADR-016 with the scaling model, networking, secrets, ingress, cost and ops-capacity comparison; a cost estimate per environment and per tenant.
**Done when:** the ADR names the autoscaling rules (telemetry is steady, billing is bursty) and the reason the alternatives were rejected.

### W24D3 · Infrastructure as code
**Read:** [Bicep overview](https://learn.microsoft.com/azure/azure-resource-manager/bicep/overview) · [Terraform on Azure](https://learn.microsoft.com/azure/developer/terraform/overview) · [Terraform remote state](https://developer.hashicorp.com/terraform/language/state/remote)
**Build:** `infra/` IaC for dev + staging: container registry, Postgres (with extensions), Redis, Service Bus/queue (or RabbitMQ container), Key Vault, log analytics; parameterised by environment; remote state.
**Done when:** a new environment is created from code only (no portal clicks) and `what-if`/`plan` is part of the PR review.

### W24D4 · Delivery pipeline & zero-downtime change
**Read:** [GitHub Actions for Azure](https://learn.microsoft.com/azure/developer/github/github-actions) · [Blue/green deployment](https://learn.microsoft.com/azure/architecture/guide/blue-green-deployment) · [Expand/contract migrations](https://martinfowler.com/articles/evodb.html)
**Build:** CD workflow: build → test → scan (Trivy/SBOM) → push to registry → deploy to staging → smoke tests → manual approval → prod with revisions/traffic shifting; migrations as a separate expand/contract job; documented rollback.
**Done when:** a change deploys to staging automatically, smoke tests gate promotion, and a "rename a column" change ships without downtime (prove it).

### W24D5 · Production data operations
**Read:** [Azure Postgres backup/PITR](https://learn.microsoft.com/azure/postgresql/flexible-server/concepts-backup-restore) · [Secret rotation](https://learn.microsoft.com/entra/identity/managed-identities-azure-resources/overview) · [Cost management basics](https://learn.microsoft.com/azure/cost-management-billing/costs/overview-cost-management)
**Build:** PITR backups + a **tested** restore into staging with measured RTO/RPO; retention/archival job (telemetry > N days to cheaper storage); secret rotation runbook; cost review per environment and cost-per-tenant estimate.
**Done when:** `docs/ops/dr-drill.md` records a real restore with timings and the data loss window; rotation has been exercised once.

### D6 · Integrate
Deploy the whole stack to staging; verify telemetry flows from the deployed environment (traces in the collector, dashboards populated) and smoke tests cover the core journey.

### D7 · Review
Blast radius, progressive delivery, migration safety, cost per tenant; week 24 quiz.

**Phase gate 10:** one command deploys to staging from IaC · smoke tests gate the deploy · restore drill documented with timings.