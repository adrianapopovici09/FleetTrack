# M16 · Frontend slice *(optional)*

**Time:** ~12 h · **Prereq:** M11 (OIDC) · **Outcome:** a small but well-architected ops console (shipment list, details, live map) proving you can design the client side of a system and test a full user journey.

## Why it matters
Backend-focused senior roles still expect you to design APIs *for* clients, understand auth flows in the browser, and reason about client state. Skip this module if your target roles are purely backend; do it if you want a demoable UI or full-stack roles.

## Concepts
- **Rendering model:** SPA vs SSR vs hybrid; why an internal ops console is a SPA.
- **BFF pattern:** keeping tokens out of the browser (YARP as BFF) vs PKCE in the SPA.
- **Server state vs UI state:** TanStack Query (cache, invalidation, retries, optimistic updates) vs local component state; no global store for server data.
- **Typed contracts:** generate the TS client from OpenAPI; one ProblemDetails → UI error mapper.
- **Real-time UX:** reconnect, resubscribe, backfill, showing freshness.
- **Testing:** component tests vs Playwright E2E against the real stack.

Read: [React docs](https://react.dev/learn) · [TanStack Query](https://tanstack.com/query/latest/docs/framework/react/overview) · [BFF pattern](https://learn.microsoft.com/azure/architecture/patterns/backends-for-frontends) · [Playwright](https://playwright.dev/docs/intro) · [SignalR JS client](https://learn.microsoft.com/aspnet/core/signalr/javascript-client)

## Labs

- [ ] **L1 · Scaffold + auth.** `web/` with Vite + React + TS; login via Keycloak (PKCE with `oidc-client-ts`, *or* the gateway as BFF with cookies; pick one and justify it); a typed API client generated from OpenAPI (`openapi-typescript` + `openapi-fetch`, or Kiota).
  ✅ Log in, call an authenticated endpoint; a 401 triggers re-login; ProblemDetails errors show as readable messages.
- [ ] **L2 · Shipment list & details.** Keyset paging + filters reflected in the URL; TanStack Query with sensible cache keys; booking a shipment invalidates the list; optimistic status change with rollback on 409/412.
  ✅ Reloading the page restores the view from the URL; a concurrent edit shows the 412 message and refreshes.
- [ ] **L3 · Live map.** MapLibre + the SignalR client through the gateway: vehicles move, the trail is drawn, geofences are shown; reconnect + backfill the last positions after a drop; a "last updated N s ago" indicator.
  ✅ Kill and restart the Tracking service → the map recovers without a page reload.
- [ ] **L4 · E2E.** Playwright against the compose stack in CI: log in → book shipment → see it in the list → start the simulator → see it move on the map.
  ✅ A green E2E job in CI with a trace artifact on failure.

## Break it
Store the access token in `localStorage` and demonstrate how one XSS-injected script could read it. Then explain what the BFF approach changes.

## Decide
A short ADR (append to the register as ADR-021): SPA + PKCE vs BFF, and the React vs Blazor choice.

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
