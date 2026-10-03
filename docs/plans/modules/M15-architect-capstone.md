# M15 · Architect's toolkit & capstone

**Time:** ~13 h · **Prereq:** M14 · **Outcome:** a portfolio-ready FleetTrack (docs, diagrams, demo), a legacy-modernisation exercise, a reviewed ADR set, and practice under interview conditions.

**Why it matters:** architects are judged on communication as much as on code: diagrams people understand, decisions with reasons, migration plans that don't stop the business. Modernising legacy .NET is one of the most frequent senior/architect requests.

Each task: 📖 **Learn** → 🔨 **Build** → ✅ **Done when**.

---

### T1 · Legacy modernisation kata
- 📖 **Learn (90 min):** [Strangler fig pattern](https://learn.microsoft.com/azure/architecture/patterns/strangler-fig) · [Incremental ASP.NET to ASP.NET Core migration](https://learn.microsoft.com/aspnet/core/migration/fx-to-core/) · [System.Web adapters](https://github.com/dotnet/systemweb-adapters) · [.NET Upgrade Assistant](https://learn.microsoft.com/dotnet/core/porting/upgrade-assistant-overview) · [Key points of "Working Effectively with Legacy Code"](https://understandlegacycode.com/blog/key-points-of-working-effectively-with-legacy-code/) (characterisation tests)
- 🔨 **Build:** a small "legacy" app (static DB helper, no tests; a real .NET Framework 4.8 project if you have Windows tooling) behind **YARP** (reusing M09's gateway). Write characterisation tests (Verify snapshots), then migrate two endpoints into FleetTrack one at a time by flipping routes. Write `docs/design/modernisation.md`: the plan for a real 200-endpoint app (order, auth/session sharing, data, rollback, when to stop).
- ✅ **Done when:** clients see no change while routes move, characterisation tests pass against both implementations, and **ADR-020** is written.

### T2 · C4 diagrams & architecture overview
- 📖 **Learn (45 min):** [The C4 model](https://c4model.com/) · [C4 diagrams in Mermaid](https://mermaid.js.org/syntax/c4.html) · [Structurizr DSL](https://docs.structurizr.com/dsl) (optional)
- 🔨 **Build:** context, container (gateway, monolith, Tracking, Postgres ×2, broker, Valkey, Keycloak, LLM, cloud services) and component (Shipments module) diagrams as code in `docs/architecture/`, plus a one-page overview with the key decisions and their ADRs.
- ✅ **Done when:** someone unfamiliar can explain FleetTrack's architecture after 20 minutes of reading.

### T3 · ADR review
- 📖 **Learn (20 min):** [ADR: superseding decisions](https://adr.github.io/) · [Lightweight ADRs (Thoughtworks Radar)](https://www.thoughtworks.com/radar/techniques/lightweight-architecture-decision-records)
- 🔨 **Build:** reread every ADR. Mark superseded ones, add "what we learned" to two, and make sure each has a revisit trigger.
- ✅ **Done when:** `docs/decisions/README.md` has an index table with statuses.

### T4 · .NET 11 upgrade
- 📖 **Learn (30 min):** [What's new in .NET 11](https://learn.microsoft.com/dotnet/core/whats-new/) · [Breaking changes](https://learn.microsoft.com/dotnet/core/compatibility/breaking-changes)
- 🔨 **Build:** upgrade the SDK, TFM and packages, fix breaking changes, and run everything (unit, integration, architecture, contract, evals, k6 smoke).
- ✅ **Done when:** the upgrade PR is green and a delta note lists what broke and which tests caught it.

### T5 · System-design interview practice
- 📖 **Learn (60 min):** [System Design Primer: how to approach a system design interview](https://github.com/donnemartin/system-design-primer#how-to-approach-a-system-design-interview-question) · [Back-of-the-envelope numbers](https://github.com/donnemartin/system-design-primer#appendix)
- 🔨 **Build:** three timed mock interviews (45 min each) with Claude as the interviewer (`mock interview`), on unseen prompts; self-review each against the structure.
- ✅ **Done when:** three interview notes have scores and 3 improvements each.

### T6 · Capstone demo
- 🔨 **Build:** a 10-minute scripted demo (book → dispatch saga → live tracking → geofence arrival → assistant question → traces in Grafana) runnable from a clean clone; README updated; portfolio summary written. Rerun any 10 quiz questions you scored lowest on across M00–M14.
- ✅ **Done when:** the demo script works from a clean clone, unaided. **FleetTrack is portfolio-ready.**

---

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

## Exit check
The modernisation kata and doc, C4 diagrams + overview, ADR index, .NET 11 upgrade, three mock-interview reviews, and the demo script.
