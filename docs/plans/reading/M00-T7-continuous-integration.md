# M00-T7 reading notes: continuous integration

Task: [M00-T7](../modules/M00-foundations.md) · Sources: [Building and testing .NET with GitHub Actions](https://docs.github.com/actions/use-cases-and-examples/building-and-testing/building-and-testing-net) · [Dependabot options](https://docs.github.com/code-security/dependabot/working-with-dependabot/dependabot-options-reference) · [Protected branches](https://docs.github.com/repositories/configuring-branches-and-merges-in-your-repository/managing-protected-branches/about-protected-branches) · [`dotnet list package --vulnerable`](https://learn.microsoft.com/dotnet/core/tools/dotnet-list-package)

---

## 1. What continuous integration is, and why teams use it

**Continuous integration (CI)** means that every time someone pushes code or opens a pull request, a server automatically builds the project and runs the tests. The goal is to find out within minutes, not weeks later, that a change broke something, while the person who made it still remembers what they did.

A few principles hold whatever tool you use:

- **Keep it fast.** If CI takes an hour, developers stop waiting for it and start batching many changes into one push. Then when it fails, nobody knows which change caused it. Most teams aim for under about 10 minutes.
- **Put cheap checks first.** Checking formatting takes seconds; integration tests that start a database take minutes. If the formatting is wrong, you want to know immediately rather than after waiting for every test.
- **Make the build reproducible.** The same commit should produce the same result on your laptop and on the CI server. That's why you pin exact versions of the SDK (with `global.json`) and of packages. When something "works on my machine" but fails in CI, it's usually because some version isn't pinned.
- **Never ignore unreliable ("flaky") tests.** A flaky test sometimes passes and sometimes fails for no real reason. If people get used to clicking "re-run" until it's green, they'll also re-run past real failures, and CI stops protecting anyone.

You'll also hear two related terms:
- **Continuous delivery:** every successful build *could* be released, but a person decides when.
- **Continuous deployment:** every successful build *is* released automatically.

Many companies choose delivery over deployment on purpose, for example when releases need sign-off from the business.

---

## 2. How GitHub Actions works

GitHub Actions is GitHub's built-in CI system, and it uses four basic building blocks:

- **A workflow** is a YAML file in `.github/workflows/`. It says *when* to run, for example "on every pull request to `main`".
- **A job** is a group of steps that runs on one machine. Different jobs run on different machines, in parallel unless you say one depends on another.
- **A step** is a single command (`dotnet build`) or a ready-made, reusable piece someone published, called an **action**. For example, `actions/checkout` downloads your code, and `actions/setup-dotnet` installs the .NET SDK.
- **A runner** is the machine that runs a job. GitHub provides fresh virtual machines (`ubuntu-latest`, for example) and deletes them afterwards, so every run starts clean.

**The job's name matters.** GitHub shows each job's result on the pull request as a "check", and branch protection (section 5) refers to that check by name. If you rename the job later, the protection rule quietly stops matching.

### Security points every senior developer should know

CI pipelines have become a favourite target for attackers, because they hold secrets (passwords, cloud keys) and they produce the code that gets shipped.

- **Give the pipeline only the permissions it needs.** Every run gets an automatic access token. In older repositories that token can write to the repo by default. Adding `permissions: contents: read` to the workflow limits it to reading, so a compromised step can't push code.
- **Pin third-party actions to an exact version.** When you write `uses: some-author/some-action@v4`, `v4` is just a label, and the author (or someone who hacks their account) can move it to different code. In March 2025 that's exactly what happened to a popular action called `tj-actions/changed-files`: the attacker moved its labels to malicious code that printed secrets from thousands of repositories into their logs. Referring to the action by its *commit hash* (a long unique ID for one exact version of the code) prevents this, because a hash can't be moved. The cost is that you have to update the hashes yourself, though Dependabot can do it for you.
- **Pull requests from strangers don't get your secrets.** If someone forks your public repo and opens a PR, GitHub deliberately runs it without your secrets, so they can't write a workflow that steals them. Don't design a pipeline that needs secrets just to check an outside contribution.

---

## 3. Keeping dependencies up to date: Dependabot

**Dependabot** is a free GitHub bot. It checks whether newer versions of your packages exist and opens a pull request to upgrade each one. You configure it in `.github/dependabot.yml` by saying which kind of package to watch (NuGet packages, GitHub Actions, Docker images) and how often (daily, weekly).

### Why bother updating regularly?
If you only upgrade when forced to, you fall years behind. Then, when a security issue requires a newer version, you have to jump several major versions at once, with lots of breaking changes, under time pressure. Small regular updates are each easy to review, and your tests tell you whether they're safe. **Good tests are what make frequent updates cheap.**

### The downside: noise
Ten packages updating means ten pull requests. Dependabot lets you **group** related packages, for example "all `Microsoft.Extensions.*` packages in one PR", because they're usually released together. The trade-off: if a grouped update breaks something, it takes more work to find which package caused it.

### Two features with similar names
- **Version updates:** "a newer version exists". These run on your schedule, and they're what the config file controls.
- **Security updates:** "the version you use has a known vulnerability". These run as soon as the problem is published, and you switch them on in the repository settings.

**Renovate** is a free, open-source alternative to Dependabot with more configuration options. Larger organisations often move to it when Dependabot gets too noisy.

---

## 4. Finding vulnerable packages

### Some vocabulary first
- **A vulnerability** is a known security bug in a package.
- **A CVE** ("Common Vulnerabilities and Exposures") is the public ID number given to one, for example `CVE-2021-44228`.
- **A transitive dependency** is a package you didn't add yourself; you got it because a package you *did* add depends on it.

Transitive dependencies are where most of the risk is. The famous **Log4Shell** vulnerability in 2021 was in a Java logging library that many companies didn't know they used, because it came in indirectly through other libraries.

### Tools in .NET
- **`dotnet list package --vulnerable --include-transitive`** lists every package in your project, including indirect ones, that has a known vulnerability.
- **NuGet Audit** does a similar check automatically every time you run `dotnet restore`, and reports problems as warnings.

**An important trap:** `dotnet list package --vulnerable` reports problems but still *succeeds*. In technical terms, it exits with code 0, the "everything went fine" signal. So if you just add it to CI, the pipeline stays green even when vulnerabilities are found. You have to read its output and fail the build yourself. The general lesson for senior engineers: **always check that a safety check can actually fail.** Many pipelines have security steps that never block anything.

### How to think about severity
Each vulnerability gets a severity score: low, medium, high or critical. But a "critical" bug in a feature of the library you never use is less urgent than a "medium" bug in code that handles every request. Senior engineers decide based on *how the package is used*, not just the score.

### When no fix exists yet
You can tell the tool to ignore that specific vulnerability. Always write down why and when you'll check again. Otherwise "temporarily ignored" becomes permanent.

---

## 5. Protecting the main branch

**Branch protection** is a set of rules GitHub enforces on a branch like `main`. The common ones are:

- **Require a pull request.** Nobody can push directly to `main`; every change goes through a PR.
- **Require status checks to pass.** The PR can't be merged until CI is green. This rule is what makes CI actually *block* bad code instead of just reporting on it.
- **Require reviews.** One or more other people must approve. Solo developers usually skip this one.
- **Require the branch to be up to date.** Your PR must include the latest `main` before merging, so CI has tested the exact combination that will exist after the merge. The cost is that every time someone merges, everyone else's open PRs have to update and re-run CI. Large teams solve this with a **merge queue**, where GitHub lines up PRs and tests them one after another automatically.
- **Do not allow bypassing.** By default, repository admins can ignore all of these rules. If that's allowed, the rules are a polite suggestion rather than real protection. In regulated companies, auditors check that *nobody*, including admins, can push unreviewed code to production. This idea is called **separation of duties**.

**Rulesets** are GitHub's newer version of branch protection. They can be applied across many repositories at once, which is how large organisations enforce the same policy everywhere.

**Cost:** with a free GitHub account, branch protection only works on **public** repositories.

---

## 6. The formatting check

`dotnet format --verify-no-changes` checks whether `dotnet format` *would* change any file. If it would, the command fails, and that's what blocks a badly formatted PR. It follows the rules in `.editorconfig` ([T3](M00-T3-build-guardrails.md)).

---

## What to remember

1. CI exists to give fast, trustworthy feedback. Speed and reliability matter more than the number of checks.
2. The pipeline is an attack target: limit its permissions, pin third-party actions, and keep secrets away from untrusted code.
3. Update dependencies little and often; tests are what make that safe.
4. Most vulnerabilities come in through indirect dependencies, and a security check that can't fail the build is useless.
5. Branch protection is what turns CI from "information" into "enforcement", but only if nobody can bypass it.

## Interview questions this prepares you for

- "Your CI takes 40 minutes. How would you speed it up?"
- "A critical vulnerability is reported in a package you use indirectly, and there's no fix yet. What do you do?"
- "Why pin GitHub Actions to a commit hash?"
- "Continuous delivery vs continuous deployment: when would you choose delivery?"

## For FleetTrack

- `setup-dotnet` can read the SDK version from `global.json` (`global-json-file: global.json`).
- FleetTrack already turns warnings into errors (`TreatWarningsAsErrors`), so NuGet Audit's warnings may already fail the build when a vulnerable package appears. Decide whether you still need the separate scan step.
- `EnforceCodeStyleInBuild` already fails the build on some style rules. Work out what `dotnet format` catches that the build doesn't.
- If the GitHub repo is private, branch protection won't be enforced on the free plan.
- There's a compose file but no Dockerfile, so check which Dependabot ecosystem covers `deploy/compose.yaml`.
