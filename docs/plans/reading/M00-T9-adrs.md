# M00-T9 reading notes: architecture decision records (ADRs)

Task: [M00-T9](../modules/M00-foundations.md) · Sources: [Documenting architecture decisions (Michael Nygard, 2011)](https://cognitect.com/blog/2011/11/15/documenting-architecture-decisions) · [ADR GitHub organisation](https://adr.github.io/) (look at the MADR template, which ours is based on)

---

## 1. The problem ADRs solve

Six months after a decision, a new developer (or you) sees something odd, like "why is Aspire not used here?" There are then only two bad options:

- **Blindly accept it:** nobody dares change it, even after the reason for it has gone away.
- **Blindly change it:** someone "fixes" it and breaks the thing it was protecting.

An ADR records **why** a decision was made, so a later reader can tell whether the reason still holds. The code already shows *what* was decided. The ADR's job is the *why* and the *what else we considered*.

---

## 2. What makes a decision "architectural"

Nygard's definition: a decision that affects the **structure**, **non-functional characteristics** (performance, security, reliability), **dependencies**, **interfaces** or **construction techniques** of the system. A practical test: **is it expensive to reverse?** If yes, write an ADR. "Which test framework?", "monolith or services?" and "which database?" qualify. "What do I name this variable?" doesn't.

---

## 3. Nygard's format: small and plain

One short markdown file per decision, numbered and kept in the repo next to the code (so it's reviewed in PRs and versioned with the code). Sections:

| Section | What goes in it |
| --- | --- |
| **Title** | A short noun phrase: "ADR-001: Docker Compose for the local environment" |
| **Status** | Proposed → Accepted → (later) Deprecated or **Superseded by ADR-NNN** |
| **Context** | The **forces** at play: constraints, team, budget, deadlines, what's uncertain. Written neutrally, *before* the decision. |
| **Decision** | Active voice, full sentences: "We will…" |
| **Consequences** | **Everything** that follows from it: good, bad and neutral. |

Nygard's key points:
- **Keep it short.** One or two pages at most. Nobody reads a long design document later.
- **ADRs are immutable.** You don't edit an accepted ADR when you change your mind. You write a **new** ADR and mark the old one "Superseded by ADR-NNN". The history of *why things changed* is part of the value.
- **Consequences include the negatives.** A decision with no downsides wasn't a real decision, or you haven't thought hard enough.

---

## 4. What adr.github.io and MADR add

[MADR](https://adr.github.io/madr/) ("Markdown Architectural Decision Records") is the most common template, and `docs/decisions/adr-template.md` is a slimmed version of it. It adds:
- **Considered options:** list the real alternatives, at least two. It shows the decision was a *choice*, and it stops the next person re-proposing an option that was already rejected, unless they bring a new argument.
- **Decision drivers:** the criteria you judged options by. Example: $0 budget, works on Windows, matches production.
- **Good/Bad bullets per consequence:** an explicit place for the negatives.

FleetTrack's template adds one more section on top of MADR:
- **Revisit trigger:** an *observable* event that would reopen the decision. Examples: "a second team joins", "ingest exceeds 5k msg/s", ".NET 11 ships a feature we need". It turns "we'll reconsider later" into something you can actually check.

---

## 5. Common mistakes (all of which reviewers and interviewers notice)

1. **The context is the decision restated.** "Choose between LTS and STS" isn't context. Context is *why it matters here*: what constraints, and what happens if you get it wrong.
2. **Straw-man options.** Listing options nobody would really choose, just to make the winner look good.
3. **"Bad: none".** Every real decision costs something: money, time, flexibility, a learning curve, a thing you can't do yet.
4. **Justification not tied to the forces.** "Because it's better" isn't a reason. "Because the $0 budget rules out X, and the team already knows Y" is.
5. **Writing it long after the fact.** You then rationalise instead of recording what you actually weighed.
6. **Using ADRs for everything.** If it's cheap to reverse, just do it. Too many ADRs and nobody reads any.

---

## 6. Interview angle

- "How do you make architecture decisions in a team?" → ADRs in the repo, reviewed in PRs like code, with a Proposed status while the discussion happens.
- "How do you avoid the same debate every 6 months?" → considered options, consequences and revisit triggers.
- "An old decision is wrong now. What do you do?" → a new ADR that supersedes it. Never quietly edit the old one.
- When *not* to: a 2-person spike or throwaway prototype. A README line is enough there.

---

## 7. What this means for your three ADRs (questions, not answers)

- **ADR-001 (new): compose vs native installs vs Aspire.** What forces matter for FleetTrack? Think about the $0 budget, Windows, matching production, onboarding a new developer, and the plan's "build it by hand first" rule. What do you give up by deferring Aspire? What would make you adopt it *before* M13?
- **ADR-002 (LTS):** the context is currently one line. What's the bad consequence? Think about features you have to wait for, and how big each upgrade jump is. Also fill the "Build guardrails" section from T3: warnings as errors, analyzers, code style in the build. Then write the revisit trigger.
- **ADR-003 (layered monolith):** it needs a revisit trigger. The plan already hints at one (M05). What *observable* signal, not just "the plan says so", would tell a real team that the layers are no longer enough?
