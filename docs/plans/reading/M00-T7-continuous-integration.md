# M00-T7 · Continuous integration

Task: [M00-T7](../modules/M00-foundations.md)

This file is the complete lesson for the task; you don't need to read anything else. Original sources, if you ever want to go deeper: [Building and testing .NET with GitHub Actions](https://docs.github.com/actions/use-cases-and-examples/building-and-testing/building-and-testing-net) · [Dependabot options](https://docs.github.com/code-security/dependabot/working-with-dependabot/dependabot-options-reference) · [Protected branches](https://docs.github.com/repositories/configuring-branches-and-merges-in-your-repository/managing-protected-branches/about-protected-branches) · [`dotnet list package --vulnerable`](https://learn.microsoft.com/dotnet/core/tools/dotnet-list-package)

**Reading time:** about 50 minutes.

## Contents

1. [What continuous integration is, and why teams use it](#1-what-continuous-integration-is-and-why-teams-use-it)
2. [How GitHub Actions works](#2-how-github-actions-works)
3. [FleetTrack's workflow, step by step](#3-fleettracks-workflow-step-by-step)
4. [Making CI fast](#4-making-ci-fast)
5. [Pipeline security](#5-pipeline-security)
6. [Keeping dependencies up to date: Dependabot](#6-keeping-dependencies-up-to-date-dependabot)
7. [Finding vulnerable packages](#7-finding-vulnerable-packages)
8. [Protecting the main branch](#8-protecting-the-main-branch)
9. [CI at organisation scale](#9-ci-at-organisation-scale)
10. [Glossary](#10-glossary)
11. [Summary, check yourself, interview questions, for FleetTrack](#11-summary)

---

## 1. What continuous integration is, and why teams use it

**Continuous integration (CI)** means that every time someone pushes code or opens a pull request (PR), a server automatically **builds** the project and **runs the tests**. The goal is to learn **within minutes**, not weeks later, that a change broke something, while the person who made it still remembers what they did and the change is still small.

Before CI, teams merged everyone's work occasionally ("integration week") and spent days untangling conflicts and breakages. CI makes integration so frequent that each one is small and boring.

Principles that hold whatever tool you use:

- **Keep it fast.** If CI takes an hour, developers stop waiting for it and batch many changes into one push. When it fails, nobody knows which change caused it. Most teams aim for **under about 10 minutes** for the PR check.
- **Cheap checks first.** Checking formatting takes seconds; integration tests that start a database take minutes. If formatting is wrong, you want to know immediately, not after waiting for every test. This is **fail fast** again.
- **Reproducible.** The same commit should produce the same result on your laptop and on CI. That's why you pin the SDK (`global.json`) and package versions (CPM). When something "works on my machine" but fails in CI, it's usually an unpinned version, or something on your machine that CI doesn't have (user secrets, a local database).
- **Never tolerate flaky tests.** A **flaky** test sometimes passes and sometimes fails for no real reason. If people get used to clicking "re-run" until it's green, they'll also re-run past real failures, and CI stops protecting anyone. Fix or quarantine flaky tests immediately.
- **Red main is everyone's problem.** If `main` is broken, the top priority is to fix or revert it, because everyone else is building on top of it.

Two related terms you'll hear:
- **Continuous delivery:** every successful build *could* be released, and a person decides when.
- **Continuous deployment:** every successful build *is* released automatically.

Many companies choose delivery over deployment on purpose: releases may need business sign-off, coordination with marketing, or regulatory approval. Deployment is covered in M13.

---

## 2. How GitHub Actions works

GitHub Actions is GitHub's built-in CI/CD system. Its building blocks:

```
workflow  (.github/workflows/ci.yml)        "when X happens, run these jobs"
 └── job  (runs on one runner)               jobs run in parallel unless one `needs` another
      └── step                               a shell command, or a reusable "action"
```

- **A workflow** is a YAML file in `.github/workflows/`. Its `on:` section says **when** it runs (its **triggers**): `push` to a branch, `pull_request` targeting a branch, `schedule` (a cron expression, e.g. a nightly security scan), or `workflow_dispatch` (a manual "Run" button).
- **A job** is a group of steps that runs on **one machine**. Different jobs run on different machines, in parallel by default. `needs: build` makes one wait for another.
- **A step** is either a shell command (`run: dotnet build`) or an **action**: a reusable, published building block (`uses: actions/checkout@v7` downloads your code, `uses: actions/setup-dotnet@v6` installs the SDK).
- **A runner** is the machine that runs a job. GitHub-hosted runners (`ubuntu-latest`, `windows-latest`) are **fresh virtual machines** that are deleted afterwards, so every run starts clean. Linux runners have Docker installed, which is why Testcontainers works there.

**The job's name matters.** GitHub shows each job's result on the PR as a **check**, and branch protection (§8) refers to that check **by name**. Rename the job later, and the protection rule quietly stops matching: PRs show "expected check, waiting" forever, or worse, the rule is edited and loses its check.

**Exit codes are how steps fail.** Every command returns a number when it finishes: **0 means success**, anything else means failure. A step fails (and by default the job stops) when its command returns non-zero. Remember this for §7.

**Cost:** GitHub-hosted runners are **free and unlimited for public repositories**. Private repositories on the free plan get a monthly allowance of minutes.

---

## 3. FleetTrack's workflow, step by step

```yaml
name: FleetTrack CI
on:
  push:          { branches: [ "main" ] }    # after a merge: proves main itself is green
  pull_request:  { branches: [ "main" ] }    # on every PR into main: the check branch protection requires
permissions:
  contents: read                             # least privilege for the automatic token (§5)
jobs:
  build:                                     # ← this job name is the required check's name (§8)
    runs-on: ubuntu-latest
    steps:
    - uses: actions/checkout@v7              # get the code
    - uses: actions/setup-dotnet@v6
      with: { global-json-file: global.json }   # install EXACTLY the pinned SDK (T1)
    - run: dotnet restore FleetTrack.slnx                         # download packages once
    - run: dotnet format --verify-no-changes --no-restore         # cheapest check first: seconds
    - run: dotnet build FleetTrack.slnx --no-restore -c Release   # 0 warnings, or it fails (T3)
    - run: dotnet test <ArchitectureTests> --no-build -c Release  # fast structural rules
    - run: dotnet test <UnitTests>         --no-build -c Release
    - run: dotnet test <IntegrationTests>  --no-build -c Release  # slowest: starts containers
    - run: |                                                      # vulnerable package scan (§7)
        dotnet list package --vulnerable --include-transitive > VulnerablePackages.txt
        cat VulnerablePackages.txt
        if grep -q "has the following vulnerable packages" VulnerablePackages.txt; then exit 1; fi
```

The design choices:
- **`--no-restore` / `--no-build`** reuse the previous step's output instead of repeating work.
- **Release configuration** builds what will be shipped. Some analyzers and optimisations only apply in Release.
- **Ordered by cost:** format → build → architecture → unit → integration. A formatting mistake fails in seconds.
- **Test projects run separately**, so the log shows which *kind* of test failed.

Possible improvements (from the loose ends): the vulnerability scan could run right after restore, since it needs no build; and grepping English text is fragile (§7.3).

---

## 4. Making CI fast

"Our CI takes 40 minutes. How would you speed it up?" is a classic question. The levers:

- **Measure first.** Look at step timings. Usually one or two steps dominate.
- **Cache package downloads.** `actions/setup-dotnet` can cache the NuGet folder, keyed on a hash of the files that declare packages (`cache: true` plus a lock file, or `actions/cache`). Restore then takes seconds.
- **Parallelise.** Split independent work into separate jobs (unit tests and integration tests on different runners), or split one large test suite into shards.
- **Do less per PR.** Run the slow, rarely failing checks (full E2E, load tests) **nightly** or before release, not on every PR. Run only the affected projects in a monorepo (path filters: `on: pull_request: paths: ['src/Billing/**']`).
- **Reuse containers.** Testcontainers per test *run* rather than per class (T6 §6).
- **Cancel outdated runs.** A `concurrency:` group with `cancel-in-progress: true` stops the run for an old commit when you push a new one to the same PR.
- **Bigger or self-hosted runners** cost money, so they're the last lever, not the first.

---

## 5. Pipeline security

CI pipelines are a favourite target for attackers: they hold **secrets** (cloud keys, package-publishing tokens), and they **produce the code that gets shipped**. Compromising a pipeline is often easier than compromising production directly.

- **Least privilege for the token.** Every run gets an automatic `GITHUB_TOKEN`. In older repositories it can write to the repo by default. `permissions: contents: read` at the top of the workflow limits it to reading, so a compromised step can't push code, create releases or approve PRs. Grant more per job only where it's needed.
- **Pin third-party actions to a commit hash.** `uses: some-author/some-action@v4` refers to a **tag**, and tags can be moved by the author, or by whoever steals the author's account. In March 2025 that's exactly what happened to the popular `tj-actions/changed-files` action: an attacker moved its tags to malicious code that printed secrets from thousands of repositories into their public logs. A **commit hash** (`@a1b2c3…`, a unique fingerprint of one exact version) can't be moved. The cost is updating the hashes, which Dependabot can do for you. Actions published by GitHub itself (`actions/*`) are lower risk, and many teams pin only third-party ones.
- **Pull requests from forks don't get your secrets.** When someone forks a public repository and opens a PR, GitHub runs it **without** your secrets, so they can't write a workflow step that steals them. Don't design a pipeline that needs secrets just to *check* an outside contribution, and be very careful with the `pull_request_target` trigger, which does run with secrets.
- **Secrets are masked in logs, but not perfectly.** If a step transforms a secret (base64-encodes it, prints part of it), the masking can't recognise it. Never echo secrets, even "for debugging".

---

## 6. Keeping dependencies up to date: Dependabot

### 6.1 What it is

**Dependabot** is a free GitHub bot. It checks whether newer versions of your dependencies exist and opens a PR to upgrade them. You configure it in `.github/dependabot.yml`: which **ecosystem** to watch (`nuget`, `github-actions`, `docker`, `docker-compose`), in which **directory**, and how often (`daily`, `weekly`, `monthly`).

### 6.2 Why update regularly

If you only upgrade when forced to, you fall years behind. Then, when a security fix requires a newer version, you have to jump several major versions at once, with many breaking changes, under time pressure. **Small, regular updates** are each easy to review, and your tests tell you whether they're safe. **Good tests are what make frequent updates cheap.** Without them, every update is a gamble, and teams stop updating.

### 6.3 Controlling the noise

Ten packages updating means ten PRs. The tools:
- **Groups** combine related packages into one PR. Group packages that are **released together** (`Microsoft.Extensions.*`, `Microsoft.AspNetCore.*`) or must **stay compatible** (NUnit, its adapter, its analyzers and `Microsoft.NET.Test.Sdk`). The trade-off: if a grouped update breaks something, you have to find which package caused it, and the whole group waits until it's fixed.
- **`ignore`** skips specific packages or update types, for example Postgres **major** versions in `docker-compose`, because a major Postgres upgrade needs a data migration (T4 §6).
- **Schedule** (`monthly` for a learning project) and **`open-pull-requests-limit`**.

### 6.4 The lesson from PR #7: never hand-resolve a Dependabot conflict

Dependabot creates each PR's branch from `main` **as it was at that moment**. If two of its PRs touch neighbouring lines of `Directory.Packages.props`, and one is merged first, the other is now **stale** and conflicts. If you resolve that conflict by hand and keep the PR's side, you **re-apply its old view of the file**, silently undoing the first PR. That's exactly how PR #7 (NUnit 5) downgraded `NUnit.Analyzers` from 4.15.0 back to 4.7.0.

**Green CI didn't catch it, and couldn't have**: a downgrade compiles and passes the tests. Even "require branches to be up to date" (§8) only re-tests the merged result, and that result was valid code. **CI proves the code builds and passes; it does not prove that the PR changes only what its title says.** Only reading the diff catches that.

What to do instead:
- Comment **`@dependabot rebase`** on the PR, and Dependabot regenerates its change on top of the current `main`. **`@dependabot recreate`** rebuilds it from scratch (needed if someone edited its branch). Once you edit a Dependabot branch yourself, it stops maintaining that PR.
- **Read the diff against the title.** A one-package PR that changes two packages is a red flag.
- **Group** packages that move together, so they arrive in one PR and can't conflict with each other.

### 6.5 Version updates vs security updates

- **Version updates:** "a newer version exists". These run on your schedule, controlled by `dependabot.yml`.
- **Security updates:** "a version you use has a known vulnerability". These run as soon as the advisory is published, and you switch them on in the repository's security settings.

**Renovate** is a free, open-source alternative with much richer configuration (auto-merge rules, better monorepo grouping, a dashboard issue). Larger organisations often move to it when Dependabot becomes too noisy.

---

## 7. Finding vulnerable packages

### 7.1 Vocabulary

- **A vulnerability** is a known security bug in a package.
- **A CVE** (Common Vulnerabilities and Exposures) is the public ID given to one, for example `CVE-2021-44228`. **Advisories** (such as the GitHub Advisory Database, which NuGet uses) link CVEs to the affected package versions.
- **Severity** (low, moderate, high, critical) comes from a standard scoring system called CVSS.
- **A transitive dependency** is one you didn't add yourself, but got through a package you did add (T1 §4).

**Transitive dependencies are where most of the risk is.** The famous **Log4Shell** vulnerability (that CVE above, from 2021) was in a Java logging library that many companies didn't know they used, because it came in indirectly through other libraries. "Are we affected?" took some companies weeks to answer, because nobody had an inventory.

### 7.2 Tools in .NET

- **`dotnet list package --vulnerable --include-transitive`** lists every package (direct and transitive) with a known vulnerability, per project.
- **NuGet Audit** checks automatically on every `dotnet restore`, and reports findings as warnings **NU1901–NU1904** (low → critical). Settings: `NuGetAudit` (on/off), `NuGetAuditMode` (`direct` or `all`, i.e. include transitive packages), `NuGetAuditLevel` (minimum severity to report). With `TreatWarningsAsErrors`, these warnings can **already fail the build**, so check whether that makes the separate scan step redundant (a loose end).

### 7.3 The trap: a check that can't fail

`dotnet list package --vulnerable` **reports** problems but **exits with 0**, the "everything went fine" code. Put it in CI on its own, and the pipeline stays green when vulnerabilities are found. FleetTrack's step searches the output for the warning sentence and exits with 1 itself. That works, but grepping English text is fragile: if a future SDK rewords the message, the check silently stops failing. Sturdier: `--format json` and a JSON query (`jq`) for any vulnerable entries.

**The general lesson: always check that a safety check can actually fail.** Many pipelines have security steps that have never blocked anything, and never could. (It's the same lesson as the vacuous architecture tests in T6 §8.)

### 7.4 Deciding what to do about a finding

- **Severity isn't everything.** A "critical" bug in a feature of the library you never call is less urgent than a "moderate" one in code that parses every request. Judge by **how the package is used** and whether the vulnerable code is reachable.
- **Fix:** upgrade the direct package; or, for a transitive one, reference the patched version directly or use CPM transitive pinning (T1 §5.3).
- **No fix yet:** suppress **that specific advisory** (NuGet's `NuGetAuditSuppress` item), with a written reason and a date to check again. Otherwise "temporarily ignored" becomes permanent.

---

## 8. Protecting the main branch

**Branch protection** is a set of rules GitHub enforces on a branch such as `main`:

| Rule | Effect | Trade-off |
| --- | --- | --- |
| **Require a pull request** | Nobody pushes directly to `main` | Small fixes take one extra step |
| **Require status checks** | The PR can't merge until the named checks pass | This is what turns CI from *information* into *enforcement* |
| **Require reviews** | N approvals needed; `CODEOWNERS` can require specific people per path | Solo developers usually skip it |
| **Require branch up to date** | The PR must include the latest `main`, so CI tested the exact merged result | Every merge forces other PRs to update and re-run; large teams use a **merge queue** that tests PRs one after another automatically |
| **Do not allow bypassing** | Even admins must follow the rules | Emergencies need a documented break-glass process |

**Bypassing matters more than it looks.** By default repository admins can ignore the rules. If that's allowed, the rules are a polite suggestion. In regulated companies, auditors check that **nobody**, admins included, can push unreviewed code to production. This principle is called **separation of duties**.

**Rulesets** are GitHub's newer form of branch protection. They can target many branches and, in organisations, many repositories at once, which is how large companies enforce the same policy everywhere.

**Cost:** on GitHub's free plan, branch protection and rulesets are enforced only on **public** repositories.

---

## 9. CI at organisation scale

The M00 drill (a developer platform for 5 teams) showed some gaps here. How CI looks when there are 30 repositories instead of one:

- **Reusable workflows.** One central workflow (in a shared repository) that many repositories call with `uses: org/ci-templates/.github/workflows/dotnet.yml@v3`. A fix to the pipeline is made once, and teams adopt it by bumping a version, instead of copying YAML into 30 repositories that then drift apart.
- **Versioning the shared pieces.** Templates, shared `Directory.Build.props` packages and analyzers are released with **semantic versioning**, and the platform team supports the current and previous version (**N-1**), so teams can upgrade on their own schedule within a window.
- **An inventory.** You can't secure what you can't see: which repositories run which .NET version, which packages, which CI template version. Organisation-wide scheduled scans catch dormant repositories that nobody pushes to (and so never run CI).
- **Paved road vs mandate.** Make the right way the **easiest** way (a template that already has CI, guardrails and health checks), and mandate only the few things that truly must be universal (branch protection, secret scanning). Mandating everything creates resentment and workarounds.

---

## 10. Glossary

| Term | Meaning |
| --- | --- |
| **CI (continuous integration)** | Automatically building and testing every push and PR |
| **Continuous delivery / deployment** | Every green build *can* be released (a person decides) / *is* released automatically |
| **Workflow / job / step / action** | A YAML pipeline / a group of steps on one machine / one command / a reusable published step |
| **Runner** | The machine that runs a job; GitHub-hosted ones are fresh VMs |
| **Check (status check)** | A job's pass/fail result shown on a PR; branch protection refers to it by name |
| **Exit code** | The number a command returns: 0 = success, non-zero = failure |
| **Flaky test** | A test that passes and fails at random, without code changes |
| **`GITHUB_TOKEN`** | The automatic access token every workflow run gets; limit it with `permissions:` |
| **Dependabot / Renovate** | Bots that open PRs to update dependencies |
| **Dependabot group** | Several packages updated together in one PR |
| **CVE / advisory / CVSS** | A vulnerability ID / a record linking it to affected versions / its severity score |
| **NuGet Audit** | Restore-time vulnerability check that reports NU1901–NU1904 warnings |
| **Branch protection / ruleset** | Rules GitHub enforces on branches (PRs, required checks, reviews, no bypass) |
| **Merge queue** | GitHub lines up PRs and tests each against the result of the ones before it |
| **Separation of duties** | No single person can push unreviewed code to production |
| **Reusable workflow** | A workflow in one repository that other repositories call, versioned like a library |

---

## 11. Summary

1. CI exists to give fast, trustworthy feedback: cheap checks first, reproducible environments, zero tolerance for flaky tests.
2. A step fails through a non-zero exit code, so a safety check that always exits with 0 protects nothing.
3. The pipeline is an attack target: least-privilege token, third-party actions pinned to commit hashes, no secrets for untrusted code.
4. Update dependencies little and often, grouped where they release together. Never hand-resolve a Dependabot conflict, and read diffs against titles, because green CI doesn't prove a merge is correct.
5. Most vulnerabilities arrive through transitive dependencies; judge findings by actual use, and suppress only with a reason and a date.
6. Branch protection turns CI into enforcement, but only if nobody can bypass it.
7. At organisation scale: reusable, versioned workflows, an inventory, and a paved road rather than mandates.

### Check yourself

1. Why does FleetTrack run `dotnet format` before the build and the integration tests last? (§3)
2. You rename the CI job from `build` to `ci`. What happens to PRs, and why? (§2, §8)
3. Explain the `tj-actions/changed-files` incident, and what pinning to a commit hash changes. (§5)
4. Explain exactly how PR #7 downgraded `NUnit.Analyzers`, and why neither CI nor "require branch up to date" would catch it. (§6.4)
5. Why did FleetTrack's vulnerability step need its own `grep`, and what's fragile about it? (§7.3)
6. A "critical" vulnerability is reported in a transitive package and no fix exists. Walk through your decision. (§7.4)
7. You're the platform lead for 30 repositories. How do you roll out a CI fix to all of them? (§9)

### Interview questions this prepares you for

- "Your CI takes 40 minutes. How would you speed it up?"
- "A critical vulnerability is reported in a package you use indirectly, and there's no fix yet. What do you do?"
- "Why pin GitHub Actions to a commit hash?"
- "CI was green, but the merge broke something. How is that possible?"
- "Continuous delivery vs continuous deployment: when would you choose delivery?"
- "How do you keep CI consistent across many repositories and teams?"

### For FleetTrack

- `ci.yml` runs on PRs and pushes to `main`, with a read-only token: restore → format check → Release build → architecture/unit/integration tests → vulnerability scan. `setup-dotnet` reads `global.json`.
- `dependabot.yml` checks monthly for `nuget` (groups for `Microsoft.Extensions.*` and the NUnit/test toolchain, now including `Microsoft.NET.Test.Sdk`), `docker-compose` (Postgres majors ignored) and `github-actions`.
- Branch protection on `main` requires the CI check. A deliberate formatting-violation PR was blocked.
- Loose ends: the scan could run right after restore and use `--format json`; check whether NuGet Audit plus warnings-as-errors already covers it; consider a `Microsoft.AspNetCore.*` group.
