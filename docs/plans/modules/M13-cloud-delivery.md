# M13 · Cloud, delivery & Aspire

**Time:** ~17 h · **Prereq:** M12 · **Outcome:** FleetTrack deployed from your own infrastructure-as-code through a GitHub Actions pipeline, with safe database migrations, then a fair, hands-on verdict on Aspire.

**Cost:** $0. Use an Azure **free account** with free-tier SKUs only, a **$1 budget alert**, and **tear down after every session**. No free credit, or you don't want to give a card? Follow the **🆓 local path** in each task (k3d/kind): same lessons, nothing to pay.

**Why it matters:** "how would you deploy this?" follows every design discussion. Senior .NET roles expect cloud fluency, infrastructure as code, zero-downtime deploys and cost awareness. Having built the plumbing by hand, you can now judge what Aspire automates.

Each task: 📖 **Learn** → 🔨 **Build** → ✅ **Done when**.

---

### T1 · Production-grade container images
- 📖 **Learn (45 min):** [Containerize an app with dotnet publish](https://learn.microsoft.com/dotnet/core/containers/sdk-publish) · [Chiseled Ubuntu containers for .NET](https://devblogs.microsoft.com/dotnet/announcing-dotnet-chiseled-containers/) · [Generic host shutdown](https://learn.microsoft.com/dotnet/core/extensions/generic-host#host-shutdown) · [Trivy](https://trivy.dev/latest/getting-started/)
- 🔨 **Build:** publish the gateway, monolith and Tracking as containers (SDK container publish, chiseled base, non-root) with graceful shutdown (`HostOptions.ShutdownTimeout`, draining).
- ✅ **Done when:** image sizes are recorded, `docker stop` during a k6 run causes no failed in-flight requests, and a Trivy scan runs in CI.

### T2 · Infrastructure as code, by hand
- 📖 **Learn (90 min):**
  - Azure path: [Container Apps overview](https://learn.microsoft.com/azure/container-apps/overview) · [Container Apps billing & free grant](https://learn.microsoft.com/azure/container-apps/billing) · [Bicep fundamentals (Learn path)](https://learn.microsoft.com/training/paths/fundamentals-bicep/) · [Postgres Flexible Server free tier](https://learn.microsoft.com/azure/postgresql/flexible-server/how-to-deploy-on-azure-free-account) · [Managed identities](https://learn.microsoft.com/entra/identity/managed-identities-azure-resources/overview)
  - 🆓 Local path: [k3d quick start](https://k3d.io/stable/) · [Kubernetes basics tutorial](https://kubernetes.io/docs/tutorials/kubernetes-basics/) · [Helm: getting started](https://helm.sh/docs/chart_template_guide/getting_started/) · [Liveness, readiness & startup probes](https://kubernetes.io/docs/tasks/configure-pod-container/configure-liveness-readiness-startup-probes/)
- 🔨 **Build:**
  - Azure: `deploy/bicep/` modules for Log Analytics, the Container Apps environment, Postgres Flexible Server **B1ms** (2 databases), Key Vault, a user-assigned managed identity, RabbitMQ as a container app, and the 3 apps pulled from **`ghcr.io`** (free, instead of ACR); min replicas 0 where possible.
  - 🆓 Local: a Helm chart (Deployments, Services, Ingress, ConfigMaps/Secrets, probes, resource limits) on a k3d cluster.
- ✅ **Done when:** the environment builds from nothing (`az deployment group what-if` is clean / `helm install` succeeds), the app works, and no secret lives in the repo or in plain config.

### T3 · Pipeline with OIDC
- 📖 **Learn (45 min):** [GitHub Actions: OIDC with Azure](https://learn.microsoft.com/azure/developer/github/connect-from-azure-openid-connect) · [Publishing to GitHub Container Registry](https://docs.github.com/packages/working-with-a-github-packages-registry/working-with-the-container-registry) · [EF Core migration bundles](https://learn.microsoft.com/ef/core/managing-schemas/migrations/applying#bundles) · 🆓 [Self-hosted runners](https://docs.github.com/actions/hosting-your-own-runners/managing-self-hosted-runners/about-self-hosted-runners)
- 🔨 **Build:** CI (from M00) → build images once → push to `ghcr.io` → deploy (Azure: OIDC login, no stored secrets; 🆓 local: a self-hosted runner, or the same script run locally) → **migration bundle** step (🆓 as a Kubernetes Job) → smoke tests → traffic shift.
- ✅ **Done when:** a merge to `main` deploys, a failing smoke test stops the rollout, and the repo contains no cloud secrets.

### T4 · Zero-downtime schema change
- 📖 **Learn (30 min):** [Evolutionary database design (expand/contract)](https://martinfowler.com/articles/evodb.html) · [Container Apps revisions & traffic splitting](https://learn.microsoft.com/azure/container-apps/revisions) · 🆓 [Kubernetes rolling updates](https://kubernetes.io/docs/tutorials/kubernetes-basics/update/update-intro/)
- 🔨 **Build:** rename a column with expand/contract over two deployments (add new column + dual write → backfill → switch reads → drop old) while k6 runs.
- ✅ **Done when:** k6 shows 0 errors across both deployments, and the steps are written as a reusable checklist in `docs/ops/`.

### T5 · Cost & teardown
- 📖 **Learn (30 min):** [Azure cost management budgets](https://learn.microsoft.com/azure/cost-management-billing/costs/tutorial-acm-create-budgets) · [Azure pricing calculator](https://azure.microsoft.com/pricing/calculator/) (no account needed) · [Container Apps scaling rules](https://learn.microsoft.com/azure/container-apps/scale-app)
- 🔨 **Build:** a $1 budget alert; scale rules (Tracking on concurrency, consumers on queue length, scale-to-zero where possible); a monthly cost estimate for 10 and 100 tenants using the calculator; a one-command teardown. (🆓 local: requests/limits per pod + the same calculator estimate.)
- ✅ **Done when:** `docs/ops/cost.md` has the estimate and the teardown script is tested.

### T6 · Aspire, now that you know what it does
- 📖 **Learn (60 min):** [Aspire overview](https://aspire.dev/get-started/what-is-aspire/) · [AppHost & resources](https://learn.microsoft.com/dotnet/aspire/fundamentals/app-host-overview) · [Service defaults](https://learn.microsoft.com/dotnet/aspire/fundamentals/service-defaults) · [Aspire deployment overview](https://learn.microsoft.com/dotnet/aspire/deployment/overview)
- 🔨 **Build:** on a branch, add an AppHost modelling your compose stack (Postgres with your Dockerfile, RabbitMQ, Valkey, Keycloak; LGTM or the Aspire dashboard) and the 3 projects; replace your `AddFleetTrackDefaults()` with ServiceDefaults in one service; generate deployment artifacts (`aspire publish` / `azd infra gen`) and diff them against your Bicep/Helm.
- ✅ **Done when:** your journal has the comparison, and **ADR-016** gives the verdict: what Aspire gave you, what it hid or did differently, and whether FleetTrack adopts it (local dev only, or deployment too). Revisit ADR-001.

### T7 · Break it
- 🔨 **Build:** (1) deploy a migration that drops a column the *previous* revision still reads, during a 50/50 traffic split or a rolling update. (2) Remove the app identity's access to secrets (Key Vault role / Kubernetes Secret) and see how fast health checks and logs reveal it. **Tear everything down afterwards.**
- ✅ **Done when:** both failures are recorded and resources are deleted.

---

## Quiz → [answers](../answers/M13.md)
1. Container Apps vs AKS vs App Service: when does each win?
2. Why build the image once and promote it, instead of rebuilding per environment?
3. How do GitHub Actions authenticate to Azure without a stored secret?
4. Why must database migrations be backward compatible with the previous app version? Describe expand/contract.
5. Why run migrations as a separate pipeline step instead of `Database.Migrate()` at app startup?
6. Managed identity: what problem does it remove, and how does an app use it to reach Key Vault or Postgres?
7. What's a revision in Container Apps, and how do you use it for canary/rollback?
8. *Code reading:* `app.Services.GetRequiredService<AppDb>().Database.Migrate();` in `Program.cs`, with 3 replicas. What can go wrong?
9. Name three cost levers for this deployment.
10. *Design:* design environments (dev/test/prod), promotion and rollback for a 4-team platform with 20 services.

## Design drill (20 min)
"Design the CI/CD and runtime platform for a company moving 30 .NET services from VMs to the cloud." Cover IaC, environments, secrets, observability, cost and migration order.

## Review
M12 Q6 · M11 Q2 · M09 Q6

## Exit check
Deployed from IaC by the pipeline, a zero-downtime migration proven, the cost doc and teardown done, the Aspire comparison and ADR-016 written. **Cloud resources torn down.**
