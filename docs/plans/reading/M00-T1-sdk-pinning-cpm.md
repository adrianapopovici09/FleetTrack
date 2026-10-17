# M00-T1 · SDK pinning and Central Package Management

Task: [M00-T1](../modules/M00-foundations.md)

This file is the complete lesson for the task; you don't need to read anything else. Original sources, if you ever want to go deeper: [.NET support policy](https://dotnet.microsoft.com/platform/support/policy/dotnet-core) · [global.json overview](https://learn.microsoft.com/dotnet/core/tools/global-json) · [Central Package Management](https://learn.microsoft.com/nuget/consume-packages/central-package-management)

**Reading time:** about 25 minutes.

## Contents

1. [How long a .NET version is supported](#1-how-long-a-net-version-is-supported)
2. [SDK vs runtime: two different things](#2-sdk-vs-runtime-two-different-things)
3. [`global.json`: choosing which SDK builds your code](#3-globaljson-choosing-which-sdk-builds-your-code)
4. [How NuGet picks package versions](#4-how-nuget-picks-package-versions)
5. [Central Package Management (CPM)](#5-central-package-management-cpm)
6. [Glossary](#6-glossary)
7. [Summary, check yourself, interview questions, for FleetTrack](#7-summary)

---

## 1. How long a .NET version is supported

Microsoft ships a new major version of .NET **every November**. There are two kinds of release:

| Kind | Versions | Supported for | Example |
| --- | --- | --- | --- |
| **LTS** (Long-Term Support) | even numbers: .NET 8, .NET 10 | **3 years** | .NET 10, released Nov 2025, supported until Nov 2028 |
| **STS** (Standard-Term Support) | odd numbers: .NET 9, .NET 11 | **2 years** (18 months before .NET 9) | .NET 9, released Nov 2024, supported until Nov 2026 |

Because STS was extended to 2 years, **.NET 8 (LTS) and .NET 9 (STS) both reach end of support in November 2026.** A team still on either of them has to move to .NET 10 very soon. That's a typical situation where a senior engineer is expected to plan ahead.

**What "supported" means:** Microsoft ships security and bug fixes, normally on the second Tuesday of each month ("Patch Tuesday"). **Only the latest patch of a version is supported.** If you're on .NET 10 but three patches behind, you're missing security fixes, even though .NET 10 itself is supported.

**Why a senior engineer cares.** Choosing a version is a business decision, not a technical preference:
- Organisations with slow, audited release processes (banks, governments, medical) normally stay on **LTS**, so they do a big upgrade every 2–3 years instead of every year.
- Teams that ship continuously and want new features early sometimes use **STS**. It's still only supported for 2 years, so they must upgrade again quickly.
- Either way, someone must plan the upgrade *before* support ends. Running unsupported software is a common audit and security finding, and it means nobody will fix the next vulnerability for you.

---

## 2. SDK vs runtime: two different things

Picture a factory. The **SDK** is the machinery that *builds* the product. The **runtime** is the power plant that *runs* it once it's built.

- **The runtime** runs your app. It's made up of the **CLR** (Common Language Runtime, the virtual machine that executes compiled .NET code, manages memory and runs the garbage collector) plus the base class libraries (`System.*`). Servers and containers that only *run* apps need just the runtime.
- **The SDK** (Software Development Kit) builds your app. It contains the C# compiler, MSBuild, the `dotnet build`/`test`/`publish` commands, the analyzers, project templates, *and* a runtime. Developer machines and CI need the SDK.

The two are **versioned separately**. An SDK version looks like `10.0.104`:

```
10 . 0 . 1 04
│    │   │  └── patch within the feature band (04 = 4th patch)
│    │   └───── feature band (1 → "100 band"; next bands are 200, 300 …)
│    └───────── minor version of .NET
└────────────── major version of .NET
```

A new **feature band** (100 → 200 → 300, roughly every quarter) can bring new tooling: new analyzers, new warnings, new `dotnet` commands. A new **patch** in a band is meant to contain only fixes.

Which one controls what:
- The project's **`TargetFramework`** (e.g. `net10.0`) decides which **runtime** the app runs on. By default, an app automatically uses the **highest installed patch** of its runtime version. So a `net10.0` app runs on 10.0.5 if that's the newest 10.0 runtime installed.
- **`global.json`** decides which **SDK** builds the code.

This distinction matters for a guardrails-heavy repo like FleetTrack. With `TreatWarningsAsErrors` on (M00-T3), an SDK that adds a new analyzer warning **breaks the build** even though nothing in your code changed.

---

## 3. `global.json`: choosing which SDK builds your code

### 3.1 The problem it solves

If you have several SDKs installed and no `global.json`, `dotnet` uses the **newest** SDK installed. Here's a realistic failure:

> Your colleague installs Visual Studio's latest update, which brings SDK `10.0.300`. You're still on `10.0.100`. The new feature band adds an analyzer warning. With warnings as errors, *their* build fails and *yours* passes, on the same commit. CI, on yet another SDK, fails differently. Everyone wastes an afternoon.

### 3.2 How it works

When you run any `dotnet` command, it searches the **current folder and then each parent folder** for a `global.json`, and uses the first one it finds. That's why it lives at the repository root.

```json
{
  "sdk": {
    "version": "10.0.100",
    "rollForward": "latestFeature",
    "allowPrerelease": false
  }
}
```

- **`version`**: the SDK you want. It's treated as a **minimum**.
- **`rollForward`**: how far `dotnet` may move above that version if it isn't installed exactly.
- **`allowPrerelease`**: whether preview SDKs may be chosen.

If no installed SDK satisfies the policy, the command **fails** with a message telling you which SDK to install. That's good: a loud failure beats a silent mismatch.

### 3.3 The `rollForward` values

Think of it as a dial from strict to loose. All the values below accept only SDKs **at or above** `version`:

| Value | May move to a newer… | Picks |
| --- | --- | --- |
| `disable` | nothing; exact version or fail | exact |
| `patch` | patch, same feature band | the requested version, else the latest patch in that band |
| `latestPatch` *(default when `rollForward` is omitted)* | patch, same feature band | the **highest** patch in that band |
| `feature` | feature band, same major.minor | the latest patch of the *lowest* band that qualifies |
| `latestFeature` | feature band, same major.minor | the **highest** band and patch for that major.minor |
| `minor` / `latestMinor` | minor version, same major | lowest qualifying / highest |
| `major` / `latestMajor` | anything newer, even another major | lowest qualifying / highest |

The rule of thumb: the variants **with** `latest` pick the **highest** matching SDK, and the ones **without** pick the closest one above what you asked for.

**Worked example (quiz Q2).** `global.json` says `10.0.100` with `latestFeature`, and the machine has only `10.0.300` and `11.0.100` installed.
- `latestFeature` allows any feature band of **10.0**, but never another major or minor.
- `11.0.100` is a different major, so it's excluded however new it is.
- `10.0.300` is 10.0 and above `10.0.100`. **It's the one used.**
- If only `11.0.100` were installed, the command would **fail**. It would never silently build with .NET 11.

### 3.4 The trade-off

- **Strict** (`disable`, `patch`): perfectly reproducible builds, but every developer and every CI agent must install exactly that SDK, and you have to bump the file every month to pick up security patches.
- **Loose** (`latestFeature`): convenient, since any recent 10.0 SDK works, but small tooling differences between feature bands can slip in.
- **A common compromise:** `latestFeature` in the file, and CI installs exactly the version from `global.json`. In GitHub Actions, `setup-dotnet` reads it with `global-json-file: global.json`. CI is then the reference, and developers are close enough.

**When not to bother:** for a single-developer throwaway project, pinning adds little. As soon as two machines (a teammate, or CI) build the code, it pays for itself.

---

## 4. How NuGet picks package versions

You need this to understand both CPM and the "transitive version skew" you hit in M00-T6.

A **direct dependency** is a package your project references itself (`<PackageReference Include="Npgsql" />`). A **transitive dependency** is one you get *through* another package. For example, `AspNetCore.HealthChecks.NpgSql` depends on `Npgsql`, so referencing the health-check package pulls `Npgsql` in transitively.

When several packages ask for different versions of the same dependency, NuGet resolves them with a few rules:

1. **Direct dependency wins.** If your project references a package directly, that version is used, even if a deeper package wants a different one. Asking for a version *lower* than one a dependency requires gives a downgrade warning (NU1605), which is an error in FleetTrack.
2. **Lowest applicable version.** A package that says "`Npgsql` ≥ 8.0.3" gets **8.0.3**, the lowest version that satisfies it, *not* the newest one available. NuGet does this deliberately: it's predictable, and it doesn't pull in untested new versions behind your back.
3. **Nearest wins.** When two paths in the dependency graph disagree, the one closer to your project in the graph wins.

**Why this surprises people:** rule 2 means a transitive dependency can stay on an **old** version even though a newer one exists, and even though another project in the same solution uses the newer one. That's exactly the 8.0.3 vs 10.0.3 `Npgsql` skew from M00-T6. Infrastructure got `Npgsql` 8.0.3 transitively through the health-check package, while the tests referenced 10.0.3 directly.

To see why a package is in your graph, use `dotnet nuget why <project> <package>`. To list everything, including transitive packages, use `dotnet list package --include-transitive`.

---

## 5. Central Package Management (CPM)

### 5.1 The problem

In a solution with 20 projects, each `.csproj` normally carries its own versions:

```xml
<!-- FleetTrack.API.csproj -->
<PackageReference Include="Serilog" Version="3.1.0" />
<!-- FleetTrack.Worker.csproj -->
<PackageReference Include="Serilog" Version="4.0.0" />
```

Over time the versions **drift apart**. That causes confusing bugs (behaviour differs between projects), conflict warnings when projects reference each other, and tedious upgrades (edit 20 files, and miss one).

### 5.2 How CPM works

1. **Create `Directory.Packages.props` at the solution root** and switch CPM on:

   ```xml
   <Project>
     <PropertyGroup>
       <ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>
     </PropertyGroup>
     <ItemGroup>
       <PackageVersion Include="Serilog" Version="4.0.0" />
       <PackageVersion Include="Npgsql"  Version="10.0.3" />
     </ItemGroup>
   </Project>
   ```

2. **Reference packages without a version** in each project:

   ```xml
   <PackageReference Include="Serilog" />
   ```

3. **The guardrail:** if a `.csproj` still specifies `Version=`, restore fails with **error NU1008**, so versions can't sneak back into project files. If a project references a package that has no `PackageVersion` entry, it fails with **NU1010**.

`Directory.Packages.props` is found the same way as `global.json`: MSBuild walks up from each project's folder and uses the first one it finds.

> Don't confuse it with **`Directory.Build.props`** (M00-T3, quiz Q1). `Directory.Build.props` applies **MSBuild properties** (like `Nullable`, `TreatWarningsAsErrors`) to every project below it. `Directory.Packages.props` holds **package versions** for CPM. They're different files with different jobs.

### 5.3 The important limit: central versions apply to direct references only

A `<PackageVersion>` entry sets the version **only when some project references that package directly**. It does **not** change transitive dependencies. So listing `Npgsql 10.0.3` in `Directory.Packages.props` doesn't stop a project from getting `Npgsql 8.0.3` transitively (section 4, rule 2). You have two fixes:
- **Reference it directly** in the project that needs it. This is what FleetTrack did in Infrastructure, and it's explicit and easy to understand.
- **Turn on transitive pinning:** `<CentralPackageTransitivePinningEnabled>true</CentralPackageTransitivePinningEnabled>`. Every `PackageVersion` entry then also *raises* any transitive copy of that package to the central version. This is useful for forcing a patched version of a vulnerable transitive package across a whole solution. The trade-off is that you're now running a dependency at a version its parent package was never tested with.

### 5.4 Useful extras

- **`VersionOverride`**: `<PackageReference Include="Serilog" VersionOverride="3.1.0" />` lets one project differ, for emergencies only. Leave a comment saying why and when it can be removed. If overrides pile up, CPM has quietly stopped doing its job.
- **`GlobalPackageReference`**: `<GlobalPackageReference Include="SomeAnalyzers" Version="1.2.3" />` adds a package to **every** project automatically. It's meant for analyzers and build tools that every project should have.
- **Lock files** (`RestorePackagesWithLockFile`): restore writes a `packages.lock.json` with the exact resolved versions of everything, transitive packages included. CI can then restore in locked mode (`dotnet restore --locked-mode`) and fail if anything changed. It's a further step in reproducibility, at the cost of more churn in pull requests.

### 5.5 Trade-offs and "when not to"

- **Pro:** one place to review and upgrade versions, no drift between projects, and Dependabot edits one file instead of twenty.
- **Con:** every project moves to a new version **together**. In a huge monorepo, one team's upgrade can break another team's project, and they all have to be fixed at once. Large organisations sometimes split versions per area (MSBuild allows nested `Directory.Packages.props` files) or accept a few `VersionOverride`s.
- **Con:** the file becomes a merge hotspot when many PRs bump versions at the same time. Grouping related packages in Dependabot (M00-T7) reduces this, and so does never hand-resolving a Dependabot conflict (see the #7 lesson in `plan.md`).
- **When not to:** a solution with one or two projects gets little from CPM. Still, it's cheap to set up from day one and painful to retrofit into 40 projects later, so most teams just turn it on.

---

## 6. Glossary

| Term | Meaning |
| --- | --- |
| **LTS / STS** | Long-Term Support (3 years, even versions) / Standard-Term Support (2 years, odd versions) |
| **Runtime / CLR** | What runs a .NET app; the CLR (Common Language Runtime) is the virtual machine at its core |
| **SDK** | Software Development Kit: compiler, MSBuild, `dotnet` CLI commands, analyzers; builds the app |
| **Feature band** | The hundreds digit of an SDK patch number (`10.0.1xx`, `10.0.2xx`), which can bring new tooling |
| **`global.json`** | A file that selects which installed SDK the `dotnet` command uses in that folder tree |
| **`rollForward`** | The policy for how far above the requested SDK version `dotnet` may go |
| **NuGet** | .NET's package manager and package registry |
| **Direct / transitive dependency** | A package your project references itself / one you get through another package |
| **CPM** | Central Package Management: package versions declared once in `Directory.Packages.props` |
| **Transitive pinning** | A CPM option that also forces transitive copies of a package to the central version |
| **Lock file** | `packages.lock.json`: the exact resolved version of every package, for reproducible restores |

---

## 7. Summary

1. .NET ships every November; even versions are LTS (3 years), odd are STS (2 years), and only the latest patch is supported. .NET 8 and 9 both end in November 2026.
2. The SDK builds and the runtime runs. `global.json` picks the SDK, `TargetFramework` picks the runtime.
3. `rollForward` controls how far above the requested SDK `dotnet` may go. `latestFeature` means "the newest feature band of the **same major.minor**, never another major".
4. NuGet picks the **lowest** version that satisfies a transitive dependency, so transitive versions can lag behind direct ones.
5. CPM puts package versions in one file and fails the restore if a project sneaks a version in, but it only controls **direct** references unless you turn on transitive pinning.

### Check yourself

1. `global.json` says `10.0.100` with `rollForward: latestPatch`. Installed: `10.0.104`, `10.0.200`. Which SDK is used? (§3.3)
2. Same file with `latestFeature`, and installed SDKs `10.0.104`, `10.0.200` and `11.0.100`. Which one is used now? (§3.3)
3. Why can upgrading Visual Studio break a build that has `TreatWarningsAsErrors` but no `global.json`? (§2, §3.1)
4. `Directory.Packages.props` lists `Npgsql 10.0.3`, yet `dotnet list package --include-transitive` shows Infrastructure using 8.0.3. Why, and what are two fixes? (§4, §5.3)
5. What's the difference between `Directory.Build.props` and `Directory.Packages.props`? (§5.2)
6. Give one reason a large organisation might *not* want one central version for every package. (§5.5)

### Interview questions this prepares you for

- "We have 40 services on 5 different .NET versions. How do you get them under control?" (Support policy and an LTS-based upgrade plan, `global.json`, CPM, automated update PRs, and an inventory of what runs where.)
- "What's the difference between the SDK and the runtime, and which one does `global.json` control?"
- "A transitive dependency has a vulnerability, and the package that pulls it in hasn't released a fix. What can you do?" (Transitive pinning or a direct reference to the patched version; check that the parent package still works; remove the pin once the parent ships a fix.)
- "Why would the same commit build on my machine but fail in CI?"

### For FleetTrack

- `global.json` pins `10.0.100` with `rollForward: latestFeature`, and CI installs exactly that version via `setup-dotnet`.
- `Directory.Packages.props` turns on CPM, so no `.csproj` contains a version. `Npgsql` is referenced **directly** in Infrastructure to fix the transitive 8.0.3 skew (§5.3).
- Dependabot edits only `Directory.Packages.props`, and related packages are grouped so they move together.
