# M16 · Frontend slice *(optional)*

**Time:** ~13 h · **Prereq:** M11 (OIDC) · **Outcome:** a small but well-architected ops console (shipment list, details, live map) proving you can design the client side of a system and test a full user journey.

**Why it matters:** backend-focused senior roles still expect you to design APIs *for* clients, understand browser auth flows and reason about client state. Skip this module for purely backend roles; do it for a demoable UI or full-stack roles.

Each task: 📖 **Learn** → 🔨 **Build** → ✅ **Done when**.

---

### T1 · Scaffold, auth & a typed client
- 📖 **Learn (90 min):** [React: Quick start](https://react.dev/learn) · [Vite: getting started](https://vite.dev/guide/) · [OAuth 2.0 for browser-based apps (IETF draft)](https://datatracker.ietf.org/doc/draft-ietf-oauth-browser-based-apps/) (read the BFF vs token-in-browser sections) · [Backends for Frontends pattern](https://learn.microsoft.com/azure/architecture/patterns/backends-for-frontends) · [oidc-client-ts](https://github.com/authts/oidc-client-ts) · [openapi-typescript + openapi-fetch](https://openapi-ts.dev/)
- 🔨 **Build:** `web/` with Vite + React + TS; login via Keycloak (PKCE in the SPA, *or* the gateway as BFF with cookies: pick one and justify it); a typed client generated from OpenAPI.
- ✅ **Done when:** you can log in and call an authenticated endpoint, a 401 triggers re-login, and ProblemDetails errors show as readable messages.

### T2 · Shipment list & details with server state
- 📖 **Learn (60 min):** server state vs UI state. [TanStack Query overview](https://tanstack.com/query/latest/docs/framework/react/overview) · [Query invalidation](https://tanstack.com/query/latest/docs/framework/react/guides/query-invalidation) · [Optimistic updates](https://tanstack.com/query/latest/docs/framework/react/guides/optimistic-updates) · [React Router](https://reactrouter.com/start/framework/installation)
- 🔨 **Build:** keyset paging + filters reflected in the URL; sensible cache keys; booking invalidates the list; optimistic status change with rollback on 409/412.
- ✅ **Done when:** reloading restores the view from the URL, and a concurrent edit shows the 412 message and refreshes.

### T3 · Live map
- 📖 **Learn (45 min):** [MapLibre GL JS](https://maplibre.org/maplibre-gl-js/docs/) · [OpenFreeMap](https://openfreemap.org/) (free tiles, no key) · [SignalR JS client: reconnect](https://learn.microsoft.com/aspnet/core/signalr/javascript-client#reconnect-clients)
- 🔨 **Build:** MapLibre + the SignalR client through the gateway: moving vehicles, trails and geofences; reconnect + backfill of the last positions after a drop; a "last updated N s ago" indicator.
- ✅ **Done when:** killing and restarting Tracking lets the map recover without a page reload.

### T4 · End-to-end tests
- 📖 **Learn (45 min):** [Playwright: getting started](https://playwright.dev/docs/intro) · [Playwright: authentication](https://playwright.dev/docs/auth) · [Playwright in CI](https://playwright.dev/docs/ci-intro)
- 🔨 **Build:** Playwright against the compose stack in CI: log in → book a shipment → see it in the list → start the simulator → see it move on the map.
- ✅ **Done when:** the E2E job is green in CI, with a trace artifact on failure.

### T5 · Break it & decide
- 🔨 **Build:** store the access token in `localStorage` and show how one injected script could read it, then explain what the BFF approach changes. Write a short ADR (ADR-021): SPA + PKCE vs BFF, React vs Blazor.
- ✅ **Done when:** the demo note and the ADR are written.

---

## Quiz → [answers](../answers/M16.md)
1. Why shouldn't server data live in a global client store like Redux?
2. PKCE in the SPA vs BFF with cookies: security and complexity trade-offs?
3. How should the client handle 412 and 409 from the API?
4. Why reflect list filters and the cursor in the URL?
5. What does a real-time client need to do after a reconnect?
6. *Code reading:* `useEffect(() => { fetch('/api/shipments').then(r => r.json()).then(setData) }, [])`. What's missing?
7. Why generate the TS client instead of handwriting fetch calls?
8. Component tests vs E2E tests: what does each prove?
9. React vs Blazor for a .NET shop: arguments each way?
10. *Design:* design the client architecture for a dispatcher console used 8 h/day with live data on 3 monitors.

## Review
M02 Q2 · M08 Q7 · M11 Q1

## Exit check
Login, list and details, the live map with reconnect, and the E2E job green in CI.
