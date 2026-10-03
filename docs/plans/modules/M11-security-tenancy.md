# M11 · Security & multi-tenancy

**Time:** ~17 h · **Prereq:** M10 · **Outcome:** real OIDC authentication through the gateway and services, object-level authorization, tenant isolation proven at the application *and* database level, and a threat model.

**Why it matters:** security and tenancy are where architects are held accountable. Broken object-level authorization (BOLA) is the #1 API vulnerability, and a cross-tenant data leak can end a SaaS company.

Each task: 📖 **Learn** → 🔨 **Build** → ✅ **Done when**.

---

### T1 · OIDC with Keycloak + JWT validation
- 📖 **Learn (90 min):** authorization code + PKCE vs client credentials; access vs ID vs refresh tokens; what JWT validation must check. [OAuth 2.0 simplified](https://www.oauth.com/) (read "Authorization Code", "PKCE" and "Client Credentials") · [JWT introduction](https://jwt.io/introduction) · [Configure JWT bearer authentication](https://learn.microsoft.com/aspnet/core/security/authentication/configure-jwt-bearer-authentication) · [Keycloak: getting started with Docker](https://www.keycloak.org/getting-started/getting-started-docker) · [Keycloak: importing a realm](https://www.keycloak.org/server/importExport)
- 🔨 **Build:** Keycloak in compose with an imported realm (`deploy/keycloak/realm.json`): two tenants, users with roles (dispatcher, customer, admin), a `tenant_id` claim mapper, a SPA client (PKCE) and a service client (client credentials). Validate JWTs at the gateway *and* in each service.
- ✅ **Done when:** tests show no token → 401, expired/wrong-audience token → 401, valid token → 200; you've fetched a token with `curl` and decoded it yourself.

### T2 · Policies + resource-based authorization (BOLA)
- 📖 **Learn (45 min):** [Policy-based authorization](https://learn.microsoft.com/aspnet/core/security/authorization/policies) · [Resource-based authorization](https://learn.microsoft.com/aspnet/core/security/authorization/resourcebased) · [OWASP API1: Broken Object Level Authorization](https://owasp.org/API-Security/editions/2023/en/0xa1-broken-object-level-authorization/)
- 🔨 **Build:** a policy catalogue (`CanBookShipments`, `CanDispatch`, `CanViewShipment`) and a resource handler checking tenant + customer ownership of the *specific* shipment.
- ✅ **Done when:** a test shows customer A requesting customer B's shipment (same tenant) gets 404/403, and the 404-vs-403 choice is documented.

### T3 · Service-to-service authentication
- 📖 **Learn (30 min):** [OAuth client credentials grant](https://www.oauth.com/oauth2-servers/access-tokens/client-credentials/) · [RFC 8693: token exchange](https://www.rfc-editor.org/rfc/rfc8693) (introduction only) · [gRPC authentication in ASP.NET Core](https://learn.microsoft.com/aspnet/core/grpc/authn-and-authz)
- 🔨 **Build:** the monolith → Tracking gRPC call uses a client-credentials token, cached until near expiry; Tracking validates audience and scope; user context is passed explicitly where needed.
- ✅ **Done when:** a direct call to Tracking without a valid service token fails, and logs show one token request per token lifetime.

### T4 · Tenant isolation in code
- 📖 **Learn (45 min):** [Multitenancy models](https://learn.microsoft.com/azure/architecture/guide/multitenant/considerations/tenancy-models) · [EF Core: multi-tenancy](https://learn.microsoft.com/ef/core/miscellaneous/multitenancy) · [Global query filters (including named filters in EF 10)](https://learn.microsoft.com/ef/core/querying/filters)
- 🔨 **Build:** `ITenantContext` from the token; global (named) query filters on every tenant-owned entity; tenant set automatically on insert; an architecture test forbids `IgnoreQueryFilters()` outside an allow-listed admin path. Also cover caches, message headers and SignalR groups.
- ✅ **Done when:** an **isolation test matrix** proves that for every endpoint and consumer, tenant A can't read or modify tenant B's data.

### T5 · Tenant isolation in the database (RLS)
- 📖 **Learn (40 min):** [Postgres row security policies](https://www.postgresql.org/docs/current/ddl-rowsecurity.html) · [`set_config` and transaction-local settings](https://www.postgresql.org/docs/current/functions-admin.html#FUNCTIONS-ADMIN-SET) · [EF Core interceptors](https://learn.microsoft.com/ef/core/logging-events-diagnostics/interceptors)
- 🔨 **Build:** enable RLS on tenant tables with policies on `current_setting('app.tenant_id')`; set it per transaction (`set_config(..., true)`) via an interceptor; the app role isn't a superuser and doesn't bypass RLS.
- ✅ **Done when:** raw SQL without the tenant setting returns 0 rows, and a query filter deliberately removed in a test still leaks nothing.

### T6 · Rate limits, secrets scanning & threat model
- 📖 **Learn (60 min):** [Rate limiting middleware](https://learn.microsoft.com/aspnet/core/performance/rate-limit) · [OWASP API Security Top 10 (2023)](https://owasp.org/API-Security/editions/2023/en/0x11-t10/) · [STRIDE threat modelling](https://learn.microsoft.com/azure/security/develop/threat-modeling-tool-threats) · [gitleaks](https://github.com/gitleaks/gitleaks)
- 🔨 **Build:** a per-tenant partitioned rate limiter at the gateway (429 + `Retry-After`), request size limits on ingest; a STRIDE threat model per entry point (gateway, gRPC ingest, SignalR, broker, admin) mapped to the OWASP API Top 10; gitleaks in CI.
- ✅ **Done when:** `docs/security/threat-model.md` exists with each threat → mitigation → test (or accepted risk); k6 shows the 429s; gitleaks runs in CI.

### T7 · Break it & decide
- 🔨 **Build:** (1) take the tenant id from a header instead of the token and impersonate another tenant with `curl`. (2) Set `app.tenant_id` at session scope and show it leaking across pooled connections. (3) Remove the resource handler and enumerate ids. Then write **ADR-014** (identity, authorization, service-to-service auth) and **ADR-015** (tenancy model, plus the path to DB-per-tenant for a big customer).
- ✅ **Done when:** all three attacks are documented and fixed again, and both ADRs are written.

---

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
OIDC end to end (user + service), BOLA and isolation-matrix tests green, RLS proven, threat model written, ADR-014/015.
