# Phase 7 — Learn & Build Tasks (W18–W19: identity, authorisation & multi-tenancy)

> **Read** → **Build** → **Done when**. Questions: `agent.md` §7. Checkbox: [`../plan.md`](../plan.md). Extra reading: [OWASP API Security Top 10](https://owasp.org/API-Security/editions/2023/en/0x11-t10/) · [OWASP Top 10 for LLM Applications](https://genai.owasp.org/llm-top-10/) · [Claims-based authorisation](https://learn.microsoft.com/aspnet/core/security/authorization/claims)

## Week 18 — Identity & authorization

### W18D1 · OIDC architecture (**ADR-013**)
**Read:** [OAuth 2.0 / OIDC on the Microsoft identity platform](https://learn.microsoft.com/entra/identity-platform/v2-protocols) · [Authorization Code flow with PKCE](https://auth0.com/docs/get-started/authentication-and-authorization-flow/authorization-code-flow-with-pkce) · [Keycloak getting started](https://www.keycloak.org/getting-started/getting-started-docker)
**Build:** Keycloak (or Entra ID) in the AppHost; JWT bearer validation on the API (issuer/audience/lifetime, JWKS caching, clock skew); a client-credentials machine client; ADR-013 records the provider choice.
**Done when:** a valid token works, an expired/wrong-issuer token is rejected, and JWKS are cached (no per-request fetch).

### W18D2 · Authorization model
**Read:** [Policy-based authorization](https://learn.microsoft.com/aspnet/core/security/authorization/policies) · [Resource-based authorization](https://learn.microsoft.com/aspnet/core/security/authorization/resourcebased) · [OWASP API1 BOLA](https://owasp.org/API-Security/editions/2023/en/0xa1-broken-object-level-authorization/)
**Build:** a policy catalogue (roles → policies) + resource-based handlers for by-id operations; an authorisation matrix test (same tenant allowed/forbidden, other tenant → 404) applied to every endpoint.
**Done when:** the matrix covers every route and removing a check fails the suite.

### W18D3 · Machine clients & partner APIs
**Read:** [API key best practices](https://cloud.google.com/docs/authentication/api-keys) · [OWASP authentication cheat sheet](https://cheatsheetseries.owasp.org/cheatsheets/Authentication_Cheat_Sheet.html)
**Build:** API keys stored hashed with scopes + rotation; device/partner requests signed with HMAC (timestamp + nonce) and replay rejection; keys never logged.
**Done when:** a replayed signed request is rejected, a revoked key fails immediately, and no key material appears in logs.

### W18D4 · Secrets & key management
**Read:** [Safe storage of app secrets](https://learn.microsoft.com/aspnet/core/security/app-secrets) · [Managed identities](https://learn.microsoft.com/entra/identity/managed-identities-azure-resources/overview) · [Data Protection key management](https://learn.microsoft.com/aspnet/core/security/data-protection/configuration/overview)
**Build:** user-secrets in dev, Key Vault + managed identity in prod; persist the Data Protection key ring; write a rotation runbook (keys, tokens, webhook secrets).
**Done when:** a secret scan (`gitleaks`) is clean, dev/prod paths are documented, and the rotation runbook exists.

### W18D5 · Threat model: OWASP API + OWASP LLM Top 10
**Read:** [OWASP API Security Top 10](https://owasp.org/API-Security/editions/2023/en/0x11-t10/) · [OWASP LLM Top 10](https://genai.owasp.org/llm-top-10/) · [STRIDE](https://learn.microsoft.com/azure/security/develop/threat-modeling-tool-threats)
**Build:** `docs/security/threat-model.md` — assets, actors, trust boundaries, abuse cases per endpoint group, plus AI abuse cases (prompt injection via ingested BOL/POD content, insecure output handling, excessive agency of tools, exfiltration through tool calls, unbounded consumption); wire CodeQL + dependency scanning.
**Done when:** every AI feature (current or planned) has ≥2 named abuse cases with a planned control.

### D6 · Integrate
Security regression tests in CI: authz matrix, tenant boundary, header policy (HSTS/CSP/CORS/CSRF for cookie flows).

### D7 · Review
Token pitfalls, confused deputy, why client-side checks are UX only; week 18 quiz.

## Week 19 — Multi-tenancy & data isolation

### W19D1 · Tenancy model decision (**ADR-014**)
**Read:** [Multitenant architecture approaches](https://learn.microsoft.com/azure/architecture/guide/multitenant/approaches/overview) · [Tenancy models](https://learn.microsoft.com/azure/architecture/guide/multitenant/considerations/tenancy-models)
**Build:** ADR-014 comparing shared schema + `TenantId` + RLS, schema-per-tenant and DB-per-tenant on isolation, cost, migration pain and compliance; write the migration path if requirements change.
**Done when:** the ADR names the requirement (isolation/compliance/cost) that drove the choice and the trigger to revisit.

### W19D2 · Enforce isolation in code
**Read:** [Global query filters](https://learn.microsoft.com/ef/core/querying/filters) · [NetArchTest](https://github.com/BenMorris/NetArchTest)
**Build:** tenant context resolved from the token (never the request body); EF global query filters on every tenant-scoped entity; write guards stamping `TenantId`; an architecture test forbidding `IgnoreQueryFilters()` outside Infrastructure.
**Done when:** a query with no tenant predicate still returns only the current tenant's rows, and the arch test blocks filter bypasses.

### W19D3 · Enforce isolation in the database (RLS)
**Read:** [Postgres row security policies](https://www.postgresql.org/docs/current/ddl-rowsecurity.html) · [`SET` session variables](https://www.postgresql.org/docs/current/sql-set.html)
**Build:** RLS policies on all tenant tables using `current_setting('app.tenant_id')`, applied per connection/transaction by an interceptor; isolation tests running as the application role (not superuser).
**Done when:** raw SQL without a tenant predicate returns zero rows, and cross-tenant access fails even with the app filter removed.

### W19D4 · Tenant lifecycle
**Read:** [Multitenant lifecycle](https://learn.microsoft.com/azure/architecture/guide/multitenant/considerations/lifecycle) · [Right to erasure (ICO)](https://ico.org.uk/for-organisations/uk-gdpr-guidance-and-resources/individual-rights/individual-rights/right-to-erasure/)
**Build:** onboarding/provisioning (create tenant, seed reference data, per-tenant config + feature flags), offboarding (export + delete with an auditable record), retention policy per data class.
**Done when:** a new tenant is provisioned with one command, and a deleted tenant leaves no residual PII across DB, blobs, cache and projections.

### W19D5 · Noisy neighbour & fair usage
**Read:** [Multitenant overview](https://learn.microsoft.com/azure/architecture/guide/multitenant/approaches/overview) · [Rate limiting middleware](https://learn.microsoft.com/aspnet/core/performance/rate-limit)
**Build:** per-tenant quotas (requests, telemetry points, AI tokens), statement timeouts, heavy-query protection, per-tenant metrics + a noisy-tenant alert.
**Done when:** a simulated abusive tenant is throttled and individually visible in metrics while others keep their SLOs.

### D6 · Integrate
Run the isolation test matrix across API, database, cache, broker and projections; fix the first weak link found.

### D7 · Review — phase gate 7
OIDC end-to-end, threat model (API + LLM), RLS + filters + isolation matrix green, ADR-013/014 accepted; week 19 quiz.