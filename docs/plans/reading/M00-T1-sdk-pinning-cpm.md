# M00-T1 reading notes: SDK pinning and Central Package Management

Task: [M00-T1](../modules/M00-foundations.md) · Sources: [.NET support policy](https://dotnet.microsoft.com/platform/support/policy/dotnet-core) · [global.json overview](https://learn.microsoft.com/dotnet/core/tools/global-json) · [Central Package Management](https://learn.microsoft.com/nuget/consume-packages/central-package-management)

---

## 1. How long a .NET version is supported

Microsoft ships a new major version of .NET every November. There are two kinds of release:

- **LTS (Long-Term Support)**: the even-numbered versions (.NET 8, .NET 10). They get fixes for **3 years**.
- **STS (Standard-Term Support)**: the odd-numbered versions (.NET 9, .NET 11). They get fixes for **2 years**. Before .NET 9 this was 18 months.

"Supported" means Microsoft releases security and bug fixes, usually on the second Tuesday of each month ("Patch Tuesday"). **Only the latest patch of a version is supported.** If you're on .NET 10 but a few patches behind, you're missing security fixes, even though .NET 10 itself is still supported.

**Why a senior engineer cares:** choosing a version is a business decision. Companies with slow release processes (banks, governments) usually stay on LTS, so they upgrade every two or three years instead of every year. Teams that want the newest features and can upgrade easily sometimes use STS. Either way, someone has to plan upgrades *before* support ends. Running unsupported software is a common audit finding.

---

## 2. SDK vs runtime: two different things

- The **runtime** is what *runs* your app: the CLR (the virtual machine that runs compiled .NET code) plus the base libraries.
- The **SDK** (Software Development Kit) is what *builds* your app: the compiler, `dotnet build`, `dotnet test`, the analyzers and so on.

They're versioned separately. The SDK version looks like `10.0.100`:

- `10.0` is the .NET version it targets.
- The hundreds digit of the last number (`1` in `100`) is the **feature band**. A new band (`200`, `300`) can bring new tooling features, for example new analyzers or new warnings.
- The last two digits (`00`) are the **patch** within that band. `10.0.105` is band 1, patch 5.

Your project's `TargetFramework` (e.g. `net10.0`) decides which **runtime** the app runs on. `global.json` decides which **SDK** builds it.

---

## 3. `global.json`: pinning the SDK

If several SDKs are installed, `dotnet` normally uses the newest one. That causes "works on my machine" problems: a newer SDK may add new warnings, and with warnings treated as errors your build suddenly fails.

`global.json` fixes this. When you run any `dotnet` command, it looks in the current folder and then each parent folder for a `global.json`. If it finds one, it picks an SDK according to two settings:

- **`version`**: the SDK version you want, as a minimum.
- **`rollForward`**: how far it may move away from that version if the exact one isn't installed. Common values:
  - `patch` / `latestPatch`: same feature band, newer patch only. `latestPatch` is the default when you give a version without `rollForward`.
  - `feature` / `latestFeature`: newer feature bands are allowed too, but the same major version.
  - `minor` / `major` / `latestMajor`: progressively looser.
  - `disable`: exactly this version or fail.

  The `latest…` variants pick the **highest** matching installed SDK. The variants without `latest` pick the **lowest** one that's still at least the requested version.
- **`allowPrerelease`**: whether preview SDKs may be chosen.

If no installed SDK matches the policy, the command fails with an error that tells you which SDK to install.

**The trade-off:** strict pinning (`disable`, `patch`) gives perfectly reproducible builds, but every developer and every CI machine must have exactly that SDK installed. Looser pinning (`latestFeature`) is more convenient but lets small tooling differences slip in. Many teams use `latestFeature` locally and let CI install exactly the pinned version.

---

## 4. Central Package Management (CPM)

### The problem

In a solution with 20 projects, each `.csproj` normally says which version of each NuGet package it uses. Over time those drift apart: project A uses `Serilog 3.1`, project B uses `Serilog 4.0`. That causes confusing bugs and makes upgrades tedious, because you have to edit 20 files.

### How CPM works

1. Create `Directory.Packages.props` at the solution root and set `<ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>`.
2. List every package and its version once: `<PackageVersion Include="Serilog" Version="4.0.0" />`.
3. In each `.csproj`, reference the package **without** a version: `<PackageReference Include="Serilog" />`.

If a `.csproj` still specifies a version, the build fails with error `NU1008`. That's the guardrail that keeps versions in one place.

### Useful extras

- **`VersionOverride`**: lets one project use a different version in an emergency. Use it rarely, and leave a comment explaining why.
- **`GlobalPackageReference`**: adds a package to *every* project automatically. It's typically used for analyzers or build tools that every project should have.
- **Transitive pinning** (`CentralPackageTransitivePinningEnabled`): a **transitive dependency** is a package you don't reference directly but get through another package. If one has a security problem, transitive pinning lets you force a safe version from `Directory.Packages.props` without adding a direct reference everywhere.

### Trade-offs

- **Pro:** one place to review and upgrade versions; no drift; Dependabot updates a single file.
- **Con:** every project must move to a new version together. In a huge monorepo, one team's upgrade can break another team's project. Large organisations sometimes split the props file per area for that reason.

---

## Interview questions this prepares you for

- "We have 40 services on 5 different .NET versions. How do you get them under control?" (Support policy, LTS-based upgrade plan, `global.json`, CPM, automated update PRs.)
- "What's the difference between the SDK and the runtime, and which one does `global.json` control?"
- "A transitive dependency has a vulnerability and the package that pulls it in hasn't released a fix. What can you do?" (Transitive pinning, or a direct reference to the patched version.)

## For FleetTrack

FleetTrack pins `10.0.100` with `rollForward: latestFeature` and uses CPM, so no `.csproj` contains a package version.
