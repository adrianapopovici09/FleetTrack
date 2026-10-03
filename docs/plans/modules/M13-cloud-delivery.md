# M13 · Cloud, delivery & Aspire

**Time:** ~16 h · **Prereq:** M12 · Azure **free account** (free-tier SKUs only, $1 budget alert, **tear down after each session**). No free credit? Use the **$0 fallback** below. · **Outcome:** FleetTrack deployed to Azure Container Apps from your own Bicep through a GitHub Actions pipeline, with safe database migrations, then a fair, hands-on verdict on Aspire.

## Why it matters
"How would you deploy this?" follows every design discussion. Senior .NET roles expect Azure fluency, infrastructure as code, zero-downtime deploys and cost awareness. Having built the plumbing by hand, you can now judge what Aspire automates.

## Concepts
- **Containers for .NET:** `dotnet publish /t:PublishContainer` (no Dockerfile needed) vs multi-stage Dockerfile; chiseled/distroless images, non-root, image size.
- **Azure Container Apps:** environments, apps, revisions, ingress, scaling rules (HTTP, queue length via KEDA), Dapr (know it exists), jobs.
- **Bicep:** modules, parameters per environment, outputs, `what-if`; Postgres Flexible Server, Key Vault, Log Analytics. Free-tier facts: Container Apps has a monthly free grant (vCPU-seconds, GiB-seconds, 2M requests); Postgres Flexible Server **B1ms** is free for 12 months on a free account; Log Analytics includes 5 GB/month; Key Vault costs fractions of a cent at this volume. Azure Container Registry (~$5/month) is replaced by the free **GitHub Container Registry**, and Service Bus by RabbitMQ in a container.
- **Identity in the cloud:** managed identity for app → Key Vault/Postgres; GitHub Actions → Azure with **OIDC federated credentials** (no stored secrets).
- **Safe delivery:** build once, promote the same image; expand/contract migrations; EF **migration bundles** as a separate pipeline step; revisions + traffic splitting; rollback.
- **Cost:** what each resource costs idle vs under load; scale-to-zero; budgets and alerts.
- **Aspire (at the end):** AppHost resource model, ServiceDefaults, dashboard, `aspire publish` / `azd` deployment. What it generates vs what you wrote.

Read: [Container Apps overview](https://learn.microsoft.com/azure/container-apps/overview) · [Bicep docs](https://learn.microsoft.com/azure/azure-resource-manager/bicep/) · [Containerize with dotnet publish](https://learn.microsoft.com/dotnet/core/containers/sdk-publish) · [GitHub OIDC with Azure](https://learn.microsoft.com/azure/developer/github/connect-from-azure-openid-connect) · [EF migration bundles](https://learn.microsoft.com/ef/core/managing-schemas/migrations/applying#bundles) · [Aspire overview](https://aspire.dev/)

## Labs

- [ ] **L1 · Production-grade images.** Publish the gateway, monolith and Tracking as containers (SDK container publish, chiseled base, non-root); graceful shutdown (`HostOptions.ShutdownTimeout`, draining).
  ✅ Image sizes recorded; `docker stop` during a k6 run → no failed in-flight requests; a vulnerability scan (Trivy) runs in CI.
- [ ] **L2 · Bicep by hand.** `deploy/bicep/`: modules for Log Analytics, Container Apps environment, Postgres Flexible Server **B1ms** (2 databases), Key Vault, a user-assigned managed identity, RabbitMQ as a container app, and 3 app containers pulled from `ghcr.io`; a `dev` parameters file; min replicas 0 where possible. *(Discuss, don't deploy: what would change with Azure Service Bus instead of RabbitMQ?)*
  ✅ `az deployment group what-if` is clean; `create` builds the environment from nothing; the app works in Azure; secrets come only from Key Vault via managed identity.
- [ ] **L3 · Pipeline with OIDC.** GitHub Actions: CI (from M00) → build images once → push to `ghcr.io` → deploy job (OIDC login, no secrets) → **migration bundle** step → smoke tests → traffic shift.
  ✅ A merge to `main` deploys; a failing smoke test stops the rollout; the repo contains no Azure secrets.
- [ ] **L4 · Zero-downtime schema change.** Rename a column using expand/contract over two deployments (add new column + dual write → backfill → switch reads → drop old) while k6 runs.
  ✅ k6 shows 0 errors across both deployments; the steps are written as a reusable checklist in `docs/ops/`.
- [ ] **L5 · Cost & teardown.** Azure budget with a **$1** alert; scale rules (Tracking on HTTP/gRPC concurrency, consumers on queue length, scale-to-zero where possible); a monthly cost estimate for 10 and 100 tenants; a one-command teardown.
  ✅ `docs/ops/cost.md` with the estimate; the teardown script tested.
- [ ] **L6 · Aspire, now that you know what it does.** On a branch: add an AppHost that models your compose stack (Postgres with your Dockerfile, RabbitMQ, Valkey, Keycloak, LGTM → or the Aspire dashboard) and the 3 projects; replace your `AddFleetTrackDefaults()` with ServiceDefaults in one service; generate the deployment (`aspire publish` / `azd infra gen`) and diff it against your Bicep.
  ✅ A comparison in your journal + **ADR-016 verdict**: what Aspire gave you (onboarding, dashboard, service discovery, less YAML), what it hid or did differently, and whether FleetTrack adopts it (and for local dev only, or deployment too).

## $0 fallback (no Azure credit)
Same lessons, run on your machine. Use **k3d** (k3s in Docker) or **kind** as the "cloud":
- **L2:** describe the environment as code with Kubernetes manifests + **Helm** (or Kustomize) instead of Bicep: Deployments, Services, Ingress, ConfigMaps/Secrets, probes, resource limits. Secrets come from a Kubernetes Secret (stretch: **External Secrets** or **sealed-secrets**) instead of Key Vault.
- **L3:** a GitHub Actions deploy job builds once, pushes to `ghcr.io`, then deploys to the cluster via a self-hosted runner on your machine (or deploy locally with the same script the pipeline runs). The migration bundle runs as a Kubernetes **Job**.
- **L4:** a rolling update with expand/contract under k6 load (0 errors).
- **L5:** replace the cost estimate with resource requests/limits + a written estimate of what this would cost on Azure (use the pricing calculator, no account needed).
- **L6:** Aspire is unchanged (compare `aspire publish` output for Kubernetes/compose with your Helm chart).

Kubernetes skills are equally marketable, so this path loses nothing for learning purposes.

## Break it
1. Deploy a migration that drops a column the *previous* revision still reads, while traffic splitting is 50/50. Watch the old revision fail.
2. Remove the managed identity's Key Vault role and observe the startup failure (and how quickly your health checks / logs reveal it).

## Decide
**ADR-016** Cloud platform & delivery (ACA vs AKS vs App Service, Bicep vs Terraform vs generated) **and the Aspire verdict**. Revisit **ADR-001**.

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
"Design the CI/CD and runtime platform for a company moving 30 .NET services from VMs to Azure." Cover IaC, environments, secrets, observability, cost and migration order.

## Review
M12 Q6 · M11 Q2 · M09 Q6

## Exit check
Deployed from IaC by the OIDC pipeline, a zero-downtime migration proven, the cost doc and teardown done, the Aspire comparison done and ADR-016 written. **Resources torn down.**
