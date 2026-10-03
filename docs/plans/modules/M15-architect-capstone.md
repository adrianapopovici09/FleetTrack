# M15 · Architect's toolkit & capstone

**Time:** ~12 h · **Prereq:** M14 · **Outcome:** a portfolio-ready FleetTrack (docs, diagrams, demo), a legacy-modernisation exercise, a reviewed ADR set, and practice under interview conditions.

## Why it matters
Architects are judged on communication as much as on code: diagrams people understand, decisions with reasons, migration plans that don't stop the business. Modernising legacy .NET is one of the most frequent senior/architect requests in industry.

## Concepts
- **C4 model:** context → container → component; one diagram per audience; diagrams as code (Mermaid/Structurizr).
- **ADR quality:** forces, real options, consequences including negatives, revisit triggers; superseding vs editing.
- **Legacy modernisation:** strangler fig with a reverse proxy (YARP), incremental ASP.NET → ASP.NET Core migration (System.Web adapters, shared session/auth), .NET Upgrade Assistant, branch-by-abstraction, characterisation tests.
- **Upgrading .NET:** breaking changes, analyzers, CI as the safety net.
- **System design interview shape:** requirements → estimates → API → data → high-level design → deep dives → bottlenecks → trade-offs.
- **Communicating up:** RFCs/one-pagers for non-engineers; estimates with ranges and risks.

Read: [C4 model](https://c4model.com/) · [Strangler fig](https://learn.microsoft.com/azure/architecture/patterns/strangler-fig) · [Incremental ASP.NET to ASP.NET Core migration](https://learn.microsoft.com/aspnet/core/migration/fx-to-core/) · [Working Effectively with Legacy Code (summary)](https://understandlegacycode.com/blog/key-points-of-working-effectively-with-legacy-code/) · [System Design Primer](https://github.com/donnemartin/system-design-primer)

## Labs

- [ ] **L1 · Legacy modernisation kata.** Create a small "legacy" app (an ASP.NET Web API style app with a shared static DB helper and no tests; ideally a real .NET Framework 4.8 project if you have Windows tooling). Put it behind **YARP** (reuse M09's gateway), write characterisation tests, then migrate two endpoints into FleetTrack one at a time by flipping routes.
  ✅ Clients see no change while routes move; characterisation tests pass against both implementations. `docs/design/modernisation.md` covers the full plan for a real 200-endpoint app (order, auth/session sharing, data, rollback, when to stop). **ADR-020**.
- [ ] **L2 · C4 + architecture overview.** Context, container (gateway, monolith, Tracking, Postgres ×2, broker, Valkey, Keycloak, LLM provider, Azure services) and component (Shipments module) diagrams as code in `docs/architecture/`; a one-page "architecture overview" with the key decisions and their ADRs.
  ✅ Someone unfamiliar can explain FleetTrack's architecture after 20 minutes of reading.
- [ ] **L3 · ADR review.** Reread all ADRs: which were wrong or superseded? Write supersede notes, add "what we learned" to two of them, and make sure each has a revisit trigger.
  ✅ An ADR index table with statuses in `docs/decisions/README.md`.
- [ ] **L4 · .NET 11 upgrade.** Upgrade the solution (SDK, TFM, packages), fix breaking changes, run everything (unit, integration, architecture, contract, evals, k6 smoke).
  ✅ The upgrade PR is green; a delta note covers what broke and which tests caught it.
- [ ] **L5 · Capstone demo + mock interviews.** A 10-minute scripted demo (book → dispatch saga → live tracking → geofence arrival → assistant question → traces in Grafana) runnable from a clean clone. Then **three timed mock system-design interviews** (45 min each) with Claude as the interviewer, on unseen prompts; self-review each.
  ✅ Demo script in the README; three interview notes with scores and improvements.

## Break it
In the modernisation kata, migrate an endpoint that the legacy app *also* writes to a shared table, without a plan for data ownership. Document the inconsistency you create, and the fix.

## Decide
**ADR-020** Legacy modernisation approach. Final pass on all ADRs (L3).

## Quiz → [answers](../answers/M15.md)
1. What are the four C4 levels, and which audience is each for?
2. What makes an ADR useful two years later?
3. Describe the strangler fig pattern and its biggest risk.
4. How do you migrate a large ASP.NET (Framework) app to ASP.NET Core incrementally? What is shared between the two during migration?
5. What are characterisation tests and when do you write them?
6. Big-bang rewrite vs incremental migration: when (if ever) is a rewrite justified?
7. What's your structure for a 45-minute system-design interview?
8. How do you present an architecture decision to non-technical stakeholders?
9. How do you estimate a migration project with high uncertainty?
10. *Design:* a bank has a 15-year-old .NET Framework monolith with 2M lines and nightly batch jobs. They want cloud-native in 2 years. Outline your first 90 days.

## Design drill
Covered by the three mock interviews in L5.

## Review
Pick any 10 questions you scored lowest on across M00–M14 and redo them.

## Exit check
The modernisation kata and doc, C4 diagrams + overview, ADR index, .NET 11 upgrade, demo script, and three mock-interview reviews. **FleetTrack is portfolio-ready.**
