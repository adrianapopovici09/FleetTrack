# Phase 8 — Learn & Build Tasks (W20–W22: frontend architecture)

> **Read** → **Build** → **Done when**. Questions: `agent.md` §7. Checkbox: [`../plan.md`](../plan.md). Default stack: **React 19 + TypeScript + Vite** (ADR-015).
> Extra reading: [React docs — learn](https://react.dev/learn) · [TanStack Query](https://tanstack.com/query/latest/docs/framework/react/overview) · [Testing Library](https://testing-library.com/docs/react-testing-library/intro/) · [Playwright](https://playwright.dev/docs/intro) · [web.dev Learn Performance](https://web.dev/learn/performance) · [MDN accessibility](https://developer.mozilla.org/docs/Web/Accessibility)

## Week 20 — Client foundations

### W20D1 · Frontend architecture decision (**ADR-015**)
**Read:** [Rendering on the web (web.dev)](https://web.dev/articles/rendering-on-the-web) · [React: start a new project](https://react.dev/learn/start-a-new-react-project) · [Blazor render modes](https://learn.microsoft.com/aspnet/core/blazor/components/render-modes)
**Build:** ADR-015 (SPA vs SSR/hybrid for the ops console; BFF or straight API; React vs Blazor vs Angular) + `web/` scaffold with TypeScript strict, ESLint/Prettier, Vite (`npm create vite@latest`; Blazor equivalent `dotnet new blazor`).
**Done when:** the app builds and runs, and ADR-015 states *why* the rejected rendering model was rejected.

### W20D2 · Type-safe API layer
**Read:** [Kiota-generated clients](https://learn.microsoft.com/openapi/kiota/) · [`AbortController` (MDN)](https://developer.mozilla.org/docs/Web/API/AbortController)
**Build:** generated client from the W3D5 spec + one wrapper handling auth (PKCE + silent renew), timeout/cancellation, correlation-id propagation, and `ProblemDetails` → typed error mapping.
**Done when:** no component calls `fetch` directly, and a 400 with `errors` is mapped to fields in exactly one place.

### W20D3 · State architecture
**Read:** [TanStack Query overview](https://tanstack.com/query/latest/docs/framework/react/overview) · [Thinking in React](https://react.dev/learn/thinking-in-react)
**Build:** server state in the query cache (keys, staleTime, invalidation on mutation); local UI state in components only; one optimistic action with rollback on 409.
**Done when:** no server data lives in a global store, and the optimistic path visibly rolls back with a conflict message.

### W20D4 · Routing, layout & design system
**Read:** [React Router](https://reactrouter.com/) · [WAI-ARIA Authoring Practices](https://www.w3.org/WAI/ARIA/apg/) · [Design tokens format](https://tr.designtokens.org/format/)
**Build:** route structure (list/detail/dispatch/tracking/settings), app shell with nav, design tokens (colour/spacing/typography), standard loading/empty/error components.
**Done when:** every route has loading, empty and error states (no blank screens) and tokens are the only source of colours/spacing.

### W20D5 · Forms & validation architecture
**Read:** [React Hook Form](https://react-hook-form.com/) · [Zod](https://zod.dev/) · [Form validation (MDN)](https://developer.mozilla.org/docs/Learn_web_development/Extensions/Forms/Form_validation)
**Build:** a multi-step create-shipment wizard (stops + items), schema validation for fast feedback, server `ProblemDetails` mapped onto fields, unsaved-changes guard.
**Done when:** the wizard completes against the real API and a server rejection lands on the correct field + step.

### D6 · Integrate
Frontend CI: `tsc --noEmit`, lint, unit tests, production build, preview deployment per PR.

### D7 · Review
SSR/CSR/SSG trade-offs, hydration cost, request waterfalls; week 20 quiz.

## Week 21 — Shipment operations UI

### W21D1 · Shipment list & operations dashboard
**Read:** [TanStack Query pagination](https://tanstack.com/query/latest/docs/framework/react/guides/pagination) · [TanStack Virtual](https://tanstack.com/virtual/latest) · [URLSearchParams (MDN)](https://developer.mozilla.org/docs/Web/API/URLSearchParams)
**Build:** paged/filtered/sorted list driven by the URL (shareable), saved views, bulk selection with a bulk action, keyboard navigation, virtualised rows above 500 items.
**Done when:** a filtered, paged view is shareable by URL and scrolling 2 000 rows stays smooth (measured).

### W21D2 · Shipment detail & timeline
**Read:** [React: rendering lists](https://react.dev/learn/rendering-lists) · [`<Suspense>`](https://react.dev/reference/react/Suspense)
**Build:** detail page with status timeline (source, reason, actor, timestamp), documents, charges summary, assignment panel, `allowedActions`-driven buttons, deep links.
**Done when:** the timeline matches the API's status events exactly (test) and illegal actions are never offered.

### W21D3 · Document handling
**Read:** [File API (MDN)](https://developer.mozilla.org/docs/Web/API/File_API) · [Pre-signed URL pattern](https://learn.microsoft.com/azure/storage/blobs/storage-blob-user-delegation-sas-create-dotnet)
**Build:** direct-to-storage upload with progress + retry, confirm call, preview (image/PDF), download with audit, clear failure messaging.
**Done when:** a 50 MB upload succeeds with progress and retry, and bytes never pass through the API.

### W21D4 · Live tracking map
**Read:** [MapLibre GL JS](https://maplibre.org/maplibre-gl-js/docs/) · [SignalR JavaScript client](https://learn.microsoft.com/aspnet/core/signalr/javascript-client) · [Leaflet marker clustering](https://github.com/Leaflet/Leaflet.markercluster)
**Build:** map with markers + trails, SignalR subscription scoped to the visible viewport/shipments, coalesced updates, geofence overlays, clustering, "updated N s ago" indicator.
**Done when:** 500 vehicles update smoothly, reconnect + snapshot backfill works after a network drop, and full tracks are never pushed per tick.

### W21D5 · Dispatch board
**Read:** [`useOptimistic`](https://react.dev/reference/react/useOptimistic) · [Drag and drop API (MDN)](https://developer.mozilla.org/docs/Web/API/HTML_Drag_and_Drop_API)
**Build:** drag/drop assignment with an optimistic move, 409 conflict handling that reverts + explains, and a queue of unassigned shipments.
**Done when:** two dispatchers assigning one vehicle end with one clear success and one explicit conflict in the UI.

### D6 · Integrate
Performance pass: route-level code splitting, bundle budget, map memory profiling, Lighthouse CI thresholds.

### D7 · Review
Rendering cost, list virtualisation, offline/reconnect expectations; week 21 quiz.

## Week 22 — Client quality & testing

### W22D1 · Component & behaviour tests
**Read:** [Testing Library principles](https://testing-library.com/docs/guiding-principles) · [MSW (API mocking)](https://mswjs.io/docs/)
**Build:** component tests centred on user behaviour (accessible queries), MSW handlers generated from the OpenAPI examples so mocks match the real contract, and a test for the error-mapping path.
**Done when:** tests fail if the contract shape changes, and none query by CSS class or internal state.

### W22D2 · End-to-end tests
**Read:** [Playwright intro](https://playwright.dev/docs/intro) · [Playwright best practices](https://playwright.dev/docs/best-practices)
**Build:** Playwright against the containerised stack (or Aspire): sign in → create shipment → assign → simulate position → assert live update → upload document → assert timeline; traces/screenshots uploaded on failure.
**Done when:** the journey is green in CI and a deliberate break in any step fails the suite.

### W22D3 · Accessibility & localisation
**Read:** [WCAG quick reference](https://www.w3.org/WAI/WCAG22/quickref/) · [axe-core](https://github.com/dequelabs/axe-core) · [`Intl` (MDN)](https://developer.mozilla.org/docs/Web/JavaScript/Reference/Global_Objects/Intl)
**Build:** keyboard/ARIA audit fixed, automated a11y checks in CI, i18n scaffolding with date/number/currency/weight formatting per locale.
**Done when:** a full keyboard journey works (including the map + dialogs) and CI runs a11y checks with a small allow-list.

### W22D4 · Client observability
**Read:** [OpenTelemetry JS](https://opentelemetry.io/docs/languages/js/) · [Web Vitals](https://web.dev/articles/vitals)
**Build:** error boundaries + reporting, web OTel traces propagated into API calls (same trace), and Web Vitals reporting with a budget.
**Done when:** one trace shows the browser span linked to the API span for the same request.

### W22D5 · Offline/PWA slice
**Read:** [Service workers (MDN)](https://developer.mozilla.org/docs/Web/API/Service_Worker_API) · [Workbox](https://developer.chrome.com/docs/workbox) · [Background sync](https://developer.chrome.com/docs/workbox/modules/workbox-background-sync)
**Build:** a focused offline slice — capture a status update/POD while offline, queue it, sync on reconnect with conflict handling.
**Done when:** an offline action survives a browser restart, syncs once (no duplicates), and conflicts surface to the user.

### D6 · Integrate
Full-stack regression: E2E + API + realtime + a11y in one pipeline run.

### D7 · Review — phase gate 8
Ops console usable end to end (create → assign → track → documents), E2E green in CI, budgets enforced; week 22 quiz.