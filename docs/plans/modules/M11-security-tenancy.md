# M11 · Security & multi-tenancy

**Time:** ~16 h · **Prereq:** M10 · **Outcome:** real OIDC authentication through the gateway and services, object-level authorization, tenant isolation proven at the application *and* database level, and a threat model.

## Why it matters
Security and tenancy are where architects are held accountable. Broken object-level authorization (BOLA) is the #1 API vulnerability, and a cross-tenant data leak can end a SaaS company. Interviewers expect you to talk tokens, scopes, and defence in depth fluently.

## Concepts
- **OIDC / OAuth 2.0:** authorization code + PKCE (SPAs), client credentials (services), access vs ID vs refresh tokens, JWT validation (issuer, audience, lifetime, signature/JWKS).
- **Authorization:** policies and requirements, resource-based authorization, scopes vs roles vs permissions; the API decides, the token only informs.
- **Service-to-service:** token forwarding vs token exchange vs client credentials; never trust a downstream call just because it's "internal".
- **Multi-tenancy models:** shared schema + `tenant_id` vs schema-per-tenant vs database-per-tenant (isolation, cost, operations, noisy neighbours).
- **Defence in depth:** tenant from the token (never from the request body) → EF global query filters → **Postgres Row-Level Security** → isolation tests.
- **Abuse protection:** rate limiting per tenant/client (`System.Threading.RateLimiting`), request size limits.
- **Secrets:** user-secrets / `.env` locally → Key Vault + managed identity in the cloud (M13); secret scanning in CI.
- **Threat modelling:** STRIDE, the OWASP API Security Top 10.

Read: [OAuth 2.0 simplified](https://www.oauth.com/) · [ASP.NET Core authorization](https://learn.microsoft.com/aspnet/core/security/authorization/introduction) · [Resource-based authorization](https://learn.microsoft.com/aspnet/core/security/authorization/resourcebased) · [Postgres RLS](https://www.postgresql.org/docs/current/ddl-rowsecurity.html) · [OWASP API Top 10](https://owasp.org/API-Security/editions/2023/en/0x11-t10/) · [Multitenancy models](https://learn.microsoft.com/azure/architecture/guide/multitenant/considerations/tenancy-models)

## Labs

- [ ] **L1 · Keycloak + JWT validation.** Add Keycloak to compose with an imported realm (`deploy/keycloak/realm.json`): two tenants, users with roles (dispatcher, customer, admin), a `tenant_id` claim mapper, a SPA client (PKCE) and a service client (client credentials). Validate JWTs at the gateway *and* in each service.
  ✅ Tests: no token → 401; expired / wrong-audience token → 401; correct token → 200. Get a token with `curl` and decode it yourself.
- [ ] **L2 · Policies + resource-based authorization.** A policy catalogue (`CanBookShipments`, `CanDispatch`, `CanViewShipment`) and a resource handler that checks tenant + customer ownership of the *specific* shipment.
  ✅ A BOLA test: customer A requesting customer B's shipment id (same tenant) → 404/403. The behaviour is documented, including the choice of 404 vs 403.
- [ ] **L3 · Service-to-service auth.** The monolith → Tracking gRPC call uses a client-credentials token (cached until near expiry); Tracking validates the audience and scope; user context is propagated explicitly where needed.
  ✅ Calling Tracking directly without a valid service token fails; token acquisition is cached (one token request per lifetime, verified by logs).
- [ ] **L4 · Tenant isolation in code.** `ITenantContext` from the token; EF global (named) query filters on every tenant-owned entity; tenant set automatically on insert; an architecture test forbids `IgnoreQueryFilters()` outside an allow-listed admin path. Also covers caches, message headers and SignalR groups.
  ✅ An **isolation test matrix**: for each endpoint and each consumer, tenant A can't read or modify tenant B's data.
- [ ] **L5 · Tenant isolation in the database (RLS).** Enable RLS on tenant tables with policies on `current_setting('app.tenant_id')`; set it per transaction (`set_config(..., true)`) via an EF interceptor; the app role is not a superuser and doesn't bypass RLS.
  ✅ A raw SQL query without the tenant setting returns 0 rows. A query filter deliberately removed in a test still leaks nothing.
- [ ] **L6 · Rate limits + threat model.** Per-tenant partitioned rate limiter on the gateway (429 + `Retry-After`); request size limits on ingest. Write a STRIDE threat model per entry point (gateway, gRPC ingest, SignalR, broker, admin) mapped to the OWASP API Top 10. Add secret scanning (gitleaks) to CI.
  ✅ `docs/security/threat-model.md`, where each threat has a mitigation and a test or "accepted risk"; k6 shows the 429s; gitleaks runs in CI.

## Break it
1. Take the tenant id from a request header instead of the token, and impersonate another tenant with `curl`.
2. Set `app.tenant_id` with session scope (not transaction-local) and show it leaking across pooled connections.
3. Remove the resource handler and enumerate shipment ids.

## Decide
- **ADR-014** Identity, authorization & service-to-service auth.
- **ADR-015** Tenancy & isolation model (and the path to DB-per-tenant for a big customer).

## Quiz → [answers](../answers/M11.md)
1. Authorization code + PKCE vs client credentials: who uses each, and why does PKCE exist?
2. Which JWT checks must every API do? What does "validate the audience" protect against?
3. What is BOLA, and why doesn't role-based authorization prevent it?
4. 404 vs 403 for "this shipment exists but isn't yours": trade-offs?
5. Shared schema vs schema-per-tenant vs DB-per-tenant: when would you pick each?
6. Why use RLS if you already have EF global query filters?
7. *Code reading:* `var tenantId = Request.Headers["X-Tenant-Id"];` Problems?
8. *Code reading:* `SET app.tenant_id = '42'` runs once when a pooled connection opens. What goes wrong?
9. How should the monolith authenticate to the Tracking service, and why not just forward the user's token everywhere?
10. *Design:* a large customer demands their data in a separate database in their region. How does your architecture accommodate it?

## Design drill (20 min)
"Design authentication and authorization for a B2B SaaS with 2,000 tenants, SSO per tenant, partner API access and an admin back-office."

## Review
M10 Q6 · M09 Q9 · M02 Q5

## Exit check
OIDC end to end (user + service), BOLA and isolation matrix tests green, RLS proven, threat model written, ADR-014/015.
