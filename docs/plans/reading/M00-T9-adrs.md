# M00-T9 · Architecture decision records (ADRs)

Task: [M00-T9](../modules/M00-foundations.md)

This file is the complete lesson for the task; you don't need to read anything else. Original sources, if you ever want to go deeper: [Documenting architecture decisions (Michael Nygard, 2011)](https://cognitect.com/blog/2011/11/15/documenting-architecture-decisions) · [ADR GitHub organisation](https://adr.github.io/) (its MADR template is what FleetTrack's is based on)

**Reading time:** about 30 minutes.

## Contents

1. [The problem ADRs solve](#1-the-problem-adrs-solve)
2. [What makes a decision "architectural"](#2-what-makes-a-decision-architectural)
3. [The format: Nygard's original](#3-the-format-nygards-original)
4. [What MADR adds, and FleetTrack's template](#4-what-madr-adds-and-fleettracks-template)
5. [A worked example, good and bad](#5-a-worked-example-good-and-bad)
6. [The lifecycle: proposed, accepted, superseded](#6-the-lifecycle-proposed-accepted-superseded)
7. [Common mistakes](#7-common-mistakes)
8. [ADRs in a team, and the alternatives](#8-adrs-in-a-team-and-the-alternatives)
9. [Glossary](#9-glossary)
10. [Summary, check yourself, interview questions, for FleetTrack](#10-summary)

---

## 1. The problem ADRs solve

Six months after a decision, a new developer (or future you) sees something odd: "why don't we use Aspire?", "why is Tracking a separate service with its own database?", "why are warnings errors?". The code shows **what** was decided, but never **why**. Without the why, there are only two bad options:

- **Blindly accept it.** Nobody dares change it, even after the reason has gone away. The team keeps paying the cost of a decision that no longer earns anything.
- **Blindly change it.** Someone "fixes" it and breaks whatever it was protecting. For example, a developer removes "that annoying separate database" for Tracking, and three months later GPS ingest load takes down billing.

An **architecture decision record (ADR)** is a short document that records **one** significant decision: the situation, the options, what was chosen, **why**, and what it costs. A later reader can then check whether the reasons still hold, and change the decision *deliberately* if they don't.

There's a second, less obvious benefit: **writing the ADR improves the decision.** Being forced to list real alternatives and name the downsides catches weak reasoning before it turns into code.

---

## 2. What makes a decision "architectural"

Nygard's definition: a decision that affects the **structure**, the **non-functional characteristics** (performance, security, reliability, cost), the **dependencies**, the **interfaces** or the **construction techniques** of the system.

**The practical test: is it expensive to reverse?** If yes, write an ADR. If it's cheap to undo, just do it.

| Write an ADR | Don't |
| --- | --- |
| Monolith, modular monolith or services? | What to name a variable |
| Which database, which message broker? | Which order to put methods in a class |
| How tenants are isolated (query filters + row-level security) | A one-off refactoring |
| Synchronous calls vs messaging between services | Choosing between two equivalent helper methods |
| Runtime and build guardrails (LTS, warnings as errors) | Tweaking a log message |

---

## 3. The format: Nygard's original

Michael Nygard proposed ADRs in 2011, deliberately small: **one short markdown file per decision**, numbered and kept **in the repository** next to the code (in FleetTrack, `docs/decisions/`). Keeping them in the repository means they're **reviewed in pull requests** like code, **versioned** with the code, and found by people who read the code.

| Section | What goes in it |
| --- | --- |
| **Title** | A short noun phrase: "ADR-001: Docker Compose for the local environment" |
| **Status** | Proposed → Accepted → later perhaps Deprecated, or **Superseded by ADR-NNN** |
| **Context** | The **forces** at play: constraints, requirements, team skills, budget, deadlines, what's uncertain. Written neutrally, as the situation *before* the decision |
| **Decision** | Active voice, full sentences: "We will…" |
| **Consequences** | **Everything** that follows from the decision: good, bad and neutral |

Nygard's key points:
- **Keep it short.** One or two pages at most. Nobody reads a 30-page design document later.
- **Accepted ADRs are immutable.** When you change your mind, you don't edit the old ADR. You write a **new** one, and mark the old one "Superseded by ADR-NNN". The history of *why things changed* is part of the value (§6).
- **Consequences include the negatives.** A decision with no downsides wasn't a real decision, or you haven't thought hard enough.

### What "forces" means (a weak spot)

**Context is not the question restated.** "We need to choose between LTS and STS" is the *question*. **Context is why the question matters here, and what pushes in each direction**:

> FleetTrack is a learning project built by one developer over ~26 weeks, with a $0 budget. It should finish on a supported runtime without a forced mid-project upgrade. The developer wants to learn current .NET features, but every upgrade costs time that would otherwise go to the plan. Guardrails like warnings-as-errors make SDK changes more disruptive. LTS versions are supported for 3 years; STS for 2.

Each sentence is a **force**: something that pushes the decision one way or the other. A good context lets a reader predict the reasonable options before reading them.

---

## 4. What MADR adds, and FleetTrack's template

**MADR** (Markdown Architectural Decision Records), from adr.github.io, is the most widely used ADR template. FleetTrack's `docs/decisions/adr-template.md` is a slimmed version of it. MADR adds:

- **Decision drivers:** the criteria you'll judge the options by, made explicit *before* comparing them. For example: "$0 budget", "works on Windows", "close to production", "fast onboarding".
- **Considered options:** the **real** alternatives, at least two. This shows the decision was a *choice*, and stops the next person re-proposing an already-rejected option, unless they bring a new argument.
- **Pros and cons per option**, judged against the drivers.
- **Consequences as "Good, because…" / "Bad, because…" bullets:** an explicit place for the negatives.

FleetTrack's template adds one section on top of MADR:

- **Revisit trigger:** an **observable** event that would reopen the decision. "We'll reconsider later" can never be checked. "Ingest exceeds 5,000 messages per second", "a second team starts working in the same module", or ".NET 11 ships feature X we need" can be. When the trigger fires, you write a new ADR (§6).

---

## 5. A worked example, good and bad

The same decision written twice. It isn't one of FleetTrack's own ADRs, so it doesn't spoil anything.

### ❌ A weak ADR

> **ADR-0xx: Use Postgres for GPS positions**
> **Context:** We need to store GPS positions.
> **Decision:** Use Postgres.
> **Consequences:** Good: Postgres is a great database. Bad: none.

What's wrong with it:
- The context restates the question. There's no volume, no query pattern, no budget, no team skills.
- There are no alternatives, so a reader can't tell whether anything else was considered.
- "Great database" isn't tied to any force.
- "Bad: none" means nobody thought about the cost.
- There's no revisit trigger, so nobody will ever know when to reconsider.

### ✅ A useful ADR

> **ADR-0xx: Store GPS positions in Postgres with time-based partitioning**
> **Status:** Accepted (2026-11-02)
>
> **Context:** Devices send a position every 10 s. With 2,000 vehicles that's ~17 million rows a day. Queries are almost always "last position per vehicle" and "track for one vehicle over a day". Data older than 90 days can be deleted. The team knows Postgres well and already runs it; budget is $0, so managed time-series services are out. Ingest must survive a database restart without losing positions.
>
> **Decision drivers:** write throughput, cheap deletion of old data, no new infrastructure to run, team familiarity.
>
> **Considered options:** (1) plain Postgres table; (2) Postgres with native time-based partitioning; (3) TimescaleDB extension; (4) a separate time-series database (InfluxDB).
>
> **Decision:** We will use option 2: one Postgres table partitioned by day, with a separate small table holding each vehicle's latest position.
>
> **Consequences:**
> - Good, because dropping a whole day's partition deletes old data instantly, without a slow `DELETE`.
> - Good, because no new technology to learn, run or back up.
> - Bad, because we must create future partitions ourselves (a scheduled job), and a missed run makes inserts fail.
> - Bad, because analytics over months will be slower than in a purpose-built time-series database.
>
> **Revisit trigger:** sustained ingest above 5,000 positions/second, or a product requirement for long-range analytics.

Notice how every "good" and "bad" bullet points back to a force in the context or a driver.

---

## 6. The lifecycle: proposed, accepted, superseded

```
 Proposed ──(discussed in a PR)──► Accepted ──(trigger fires / new facts)──► Superseded by ADR-NNN
     │                                  │
     └──► Rejected                      └──► Deprecated (no longer relevant, nothing replaces it)
```

- **Proposed** while it's being discussed. The pull request *is* the discussion, and the comments record the objections.
- **Accepted** once agreed and merged.
- **Superseded** when a later ADR replaces it. The old one stays in place with a link to the new one, and the new one links back. Anyone can then follow the chain: "we chose X in 2026 because of A and B; in 2027 B stopped being true, so we moved to Y".
- **Rejected** proposals are worth keeping too. They answer "did anyone consider…?" before the debate starts again.

**Why not just edit the old ADR?** Because editing erases the reasoning that was valid at the time. Then nobody can tell whether the original decision was a mistake (a lesson to learn) or right for its time (circumstances changed). Small corrections like typos or broken links are fine to edit; changing the *decision* never is.

---

## 7. Common mistakes

Reviewers and interviewers notice all of these:

1. **The context restates the question** instead of listing the forces (§3).
2. **Straw-man options:** listing alternatives nobody would really choose, just to make the winner look good. Each option should be one a reasonable engineer could defend.
3. **"Bad: none."** Every real decision costs something: money, time, flexibility, a learning curve, something you can't do yet.
4. **Justification not tied to the forces.** "Because it's modern" or "because it's better" isn't a reason. "Because the $0 budget rules out X, and the team already knows Y" is.
5. **Writing it long after the fact.** You then rationalise the outcome instead of recording what you actually weighed.
6. **ADRs for everything.** If it's cheap to reverse, just do it. With too many ADRs, nobody reads any of them.
7. **A vague revisit trigger.** "When we grow" can't be observed. Use a number, an event or a date.
8. **Deciding before you understand the problem.** For example, drawing module boundaries before you know the domain well enough. A good ADR can say "we deliberately defer this decision until X is known". ADR-003 does exactly that: start layered, split into modules when the features show where the boundaries are.

---

## 8. ADRs in a team, and the alternatives

**How teams use them:**
- An ADR starts as a **pull request** with status *Proposed*. Discussion happens in the PR, and merging means *Accepted*.
- Architects or tech leads don't write all of them. Whoever drives a decision writes the ADR, and the lead reviews it. That spreads architectural thinking across the team.
- New joiners read the ADR log as part of onboarding. It's the fastest way to understand why the system looks the way it does.

**Related documents, and how they differ:**
- **RFC / design document:** a longer proposal written *before* building something big ("how will the billing module work?"). It can produce several ADRs.
- **C4 diagrams** (context, containers, components, code): show *what the system looks like*. ADRs explain *why* it looks that way. They complement each other (diagrams go in `docs/architecture/`).
- **Wiki pages:** easy to write, but they drift from the code, aren't reviewed, and nobody knows whether they're still true. That's why ADRs live in the repository.

**When not to bother:** a 2-person spike or a throwaway prototype. A line in the README is enough there.

---

## 9. Glossary

| Term | Meaning |
| --- | --- |
| **ADR** | Architecture decision record: a short, numbered document recording one significant decision and why |
| **Architecturally significant** | Affects structure, quality attributes, dependencies or interfaces; expensive to reverse |
| **Forces** | The constraints and pressures pushing a decision one way or another (budget, skills, load, deadlines) |
| **Decision drivers** | The explicit criteria the options are judged against |
| **MADR** | Markdown Architectural Decision Records, the most common ADR template |
| **Superseded** | Replaced by a later ADR, which is linked; the old one stays unchanged |
| **Revisit trigger** | An observable event that reopens a decision |
| **Straw man** | A deliberately weak alternative listed only to make the chosen option look good |
| **RFC / design doc** | A longer proposal for a large piece of work, written before building it |

---

## 10. Summary

1. An ADR records **why** a decision was made and what it costs, so later readers can tell whether it still holds.
2. Write one when a decision is expensive to reverse, and keep it short.
3. Context is the **forces**, not the question restated. Options must be real, consequences must include the negatives, and every argument should point back to a force.
4. Accepted ADRs are never rewritten. A new ADR supersedes the old one, and the chain records how thinking evolved.
5. A revisit trigger must be observable: a number, an event or a date.
6. ADRs live in the repository and are reviewed in pull requests, like code.

### Check yourself

1. Rewrite this context so it lists forces: "We need to decide whether to use Aspire." (§3)
2. What's wrong with "Bad: none"? Give a real negative for "warnings as errors". (§7)
3. A 2025 ADR chose a message broker, and you now want to switch. What exactly do you do with the old ADR? (§6)
4. Turn "we'll revisit when the system gets bigger" into an observable trigger for ADR-003. (§4, §7)
5. Why do ADRs live in the repository rather than on a wiki? (§3, §8)
6. Name a FleetTrack decision that doesn't deserve an ADR, and say why. (§2)

### Interview questions this prepares you for

- "How do you make and record architecture decisions in a team?" (ADRs in the repository, proposed as PRs, reviewed like code.)
- "How do you avoid having the same debate every six months?" (Considered options, consequences, revisit triggers.)
- "An old decision is wrong now. What do you do?" (A new ADR that supersedes it; never quietly edit the old one.)
- "Tell me about a decision you made and its trade-offs." (Answer in ADR shape: forces → options → choice → costs → when you'd revisit.)

### For FleetTrack

- `docs/decisions/` holds **ADR-001** (Docker Compose vs native installs vs Aspire, with Aspire deferred to M13), **ADR-002** (LTS and build guardrails; written by Claude on request, so it's worth rereading and challenging) and **ADR-003** (layered monolith, with a feature-coupling revisit trigger and the API → Infrastructure composition-root exception).
- Every module that changes the architecture adds an ADR (FleetTrack rule 7). ADR-003 will be revisited in M05 and M09.
- The ADR register lives in `plan.md`.
