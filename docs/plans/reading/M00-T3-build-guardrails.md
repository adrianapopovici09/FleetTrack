# M00-T3 · Build guardrails

Task: [M00-T3](../modules/M00-foundations.md)

This file is the complete lesson for the task; you don't need to read anything else. Original sources, if you ever want to go deeper: [Customize your build by folder](https://learn.microsoft.com/visualstudio/msbuild/customize-by-directory) · [Code analysis overview](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/overview) · [Configuration files for code analysis](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/configuration-files) · [Nullable reference types](https://learn.microsoft.com/dotnet/csharp/nullable-references)

**Reading time:** about 40 minutes.

**Big idea:** a **guardrail** is a rule the build enforces automatically, so quality doesn't depend on people remembering things. A rule in a wiki gets forgotten. A rule that fails the build can't be.

## Contents

1. [`Directory.Build.props`: settings for every project at once](#1-directorybuildprops-settings-for-every-project-at-once)
2. [Code analyzers: what they are and how to switch them on](#2-code-analyzers-what-they-are-and-how-to-switch-them-on)
3. [Warnings as errors, and the ways to silence a rule](#3-warnings-as-errors-and-the-ways-to-silence-a-rule)
4. [`.editorconfig`: rule severities and code style](#4-editorconfig-rule-severities-and-code-style)
5. [`dotnet format`](#5-dotnet-format)
6. [Nullable reference types](#6-nullable-reference-types)
7. [Implicit usings](#7-implicit-usings)
8. [Rolling guardrails out on an existing codebase](#8-rolling-guardrails-out-on-an-existing-codebase)
9. [Glossary](#9-glossary)
10. [Summary, check yourself, interview questions, for FleetTrack](#10-summary)

---

## 1. `Directory.Build.props`: settings for every project at once

### 1.1 What it is

**MSBuild** is the build engine behind `dotnet build`. A `.csproj` file is an MSBuild file: it sets **properties** (like `<Nullable>enable</Nullable>`) and **items** (like `<PackageReference>`), and imports **targets** (steps such as "compile", "copy output").

Before building a project, MSBuild searches the project's folder and each parent folder for a file called `Directory.Build.props`, and **imports the first one it finds**. Everything in it applies to every project beneath that folder. So instead of copying `<Nullable>enable</Nullable>` into 20 `.csproj` files, and forgetting the 21st, you write it once at the root:

```xml
<!-- Directory.Build.props at the repository root -->
<Project>
  <PropertyGroup>
    <Nullable>enable</Nullable>
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
  </PropertyGroup>
</Project>
```

### 1.2 `.props` vs `.targets`: when each is read

MSBuild reads a project from top to bottom, and **a later value for the same property overwrites an earlier one**. The order is:

```
1. Directory.Build.props     ← imported EARLY (inside the SDK's own props)
2. your .csproj contents     ← can override anything set in step 1
3. the SDK's targets
4. Directory.Build.targets   ← imported LATE; wins over the .csproj
```

- Put **defaults** in `Directory.Build.props`, so an individual project can still override them (for example, a test project turning a rule off).
- Put things that **must win**, or that need values the project has already set, in `Directory.Build.targets`. For example, "if this is a test project, also add X" needs the project's own properties to exist first.

### 1.3 Nesting: only the nearest file is imported

If `tests/Directory.Build.props` exists, the projects under `tests/` use **only that file**, and the root file is **not** imported automatically. This catches people out all the time: they add a nested file to relax one rule for tests, and accidentally lose *all* the root settings for tests. The fix is to import the parent explicitly at the top of the nested file:

```xml
<!-- tests/Directory.Build.props -->
<Project>
  <Import Project="$([MSBuild]::GetPathOfFileAbove('Directory.Build.props', '$(MSBuildThisFileDirectory)../'))" />
  <PropertyGroup>
    <IsPackable>false</IsPackable>
  </PropertyGroup>
</Project>
```

> Don't confuse it with **`Directory.Packages.props`** ([T1](M00-T1-sdk-pinning-cpm.md)). `Directory.Build.props` holds **build settings**, and `Directory.Packages.props` holds **package versions** for Central Package Management.

---

## 2. Code analyzers: what they are and how to switch them on

### 2.1 What an analyzer is

An **analyzer** is a plug-in to the C# compiler (Roslyn). It inspects your code *while it compiles* and reports **diagnostics**: messages with an id, a severity and a location, like `CA2016: Forward the CancellationToken parameter to methods that take one`. Some analyzers also come with a **code fix**, the light bulb in the editor that rewrites the code for you.

.NET ships two families out of the box:

| Family | Ids | About | Examples |
| --- | --- | --- | --- |
| **Code quality** | `CAxxxx` | Correctness, reliability, performance, security, design | `CA2016` forward the `CancellationToken`; `CA1849` don't call a blocking method in an async method; `CA2100` SQL injection risk; `CA1822` member can be `static` |
| **Code style** | `IDExxxx` | How the code looks | `IDE0005` unnecessary `using`; `IDE0055` formatting; `IDE0090` simplify `new` expression |

You can also add analyzer packages (for example `NUnit.Analyzers` in FleetTrack's tests, which flags misused NUnit assertions). Every analyzer package needs a licence check, as with any other package.

### 2.2 The settings that control them

| Setting | What it does |
| --- | --- |
| `EnableNETAnalyzers` | Turns the built-in CA analyzers on. Already on by default for projects targeting .NET 5 or later. |
| `AnalysisLevel` | **Which version** of the rules and **how many** are active. The version is a number or `latest`; the mode is `none`, `default`, `minimum`, `recommended` or `all`. They combine as `latest-recommended`, `10.0-all`, and so on. |
| `AnalysisMode<Category>` | Overrides the mode for one category only, e.g. `<AnalysisModeSecurity>All</AnalysisModeSecurity>`. |
| `EnforceCodeStyleInBuild` | IDE (style) rules normally only appear in the editor. This makes them run during `dotnet build` too, so CI can enforce them. |
| `TreatWarningsAsErrors` | Any warning fails the build (section 3). |

The modes, from loosest to strictest:
- **`default`**: a small set of rules as warnings, most of the rest as suggestions.
- **`minimum`**: a few more rules escalated to warnings.
- **`recommended`**: a larger, sensible set as warnings. This is FleetTrack's choice.
- **`all`**: every rule as a warning. It's very noisy; people use it to *explore* rules, then disable the ones they don't want.

### 2.3 The `latest` trade-off

`AnalysisLevel=latest-...` means "the newest rule set that ships with the SDK you're building with". When a new SDK adds a rule, and warnings are errors, **an SDK upgrade can break a build whose code didn't change**. That's one reason FleetTrack pins the SDK with `global.json` ([T1](M00-T1-sdk-pinning-cpm.md)): new rules arrive only when *you* choose to move to a newer SDK.

The alternative is pinning a number (`10.0-recommended`). It's more stable, but you miss new rules until you bump it on purpose. Teams that pin usually bump the number on the same day they upgrade the SDK, and fix the new warnings in that one PR.

### 2.4 A caveat about `IDE0005` (unnecessary usings)

`IDE0005` only runs during the build if the project generates XML documentation (`<GenerateDocumentationFile>true</GenerateDocumentationFile>`). That's a quirk of how the compiler tracks which `using`s are used. If you set the rule to `warning` and nothing happens in the build, this is why. Turning on documentation generation then raises missing-XML-comment warnings (`CS1591`) for public members, which most applications silence.

---

## 3. Warnings as errors, and the ways to silence a rule

### 3.1 Why treat warnings as errors?

Warnings that don't fail the build **pile up**. At 300 warnings nobody reads them, and the one important new warning (a forgotten `CancellationToken`, a possible null dereference) disappears into the noise. With `TreatWarningsAsErrors=true` the count stays at **zero**, so any new warning is impossible to miss. The cost is some friction while prototyping, for example an unused variable fails the build while you're halfway through writing a method.

There are finer-grained versions of the same switch:
- **`<WarningsAsErrors>CA2016;CS8602</WarningsAsErrors>`** turns only the listed warnings into errors. This is the gentle way to introduce the idea on an existing codebase (section 8).
- **`<WarningsNotAsErrors>CA1822</WarningsNotAsErrors>`** keeps everything as errors *except* these, which stay as plain warnings.

### 3.2 Ways to silence a rule, from narrowest to widest

Sometimes a rule is wrong for one piece of code. Silence it as **narrowly** as possible, and **say why**:

| Scope | How | When |
| --- | --- | --- |
| One line or block | `#pragma warning disable CA1822` … `#pragma warning restore CA1822` | A genuine one-off; add a comment saying why |
| One member or type | `[SuppressMessage("Performance", "CA1822", Justification = "Called by reflection")]` | Same, but the reason is recorded in the attribute |
| One folder | A nested `.editorconfig` with `dotnet_diagnostic.CA1707.severity = none` | For example, underscores in test method names |
| One project | `<NoWarn>$(NoWarn);CA1707</NoWarn>` in the `.csproj` | A whole project type where the rule doesn't fit |
| Everything | `.editorconfig` at the root | The team has decided the rule doesn't suit the codebase |

**Code-review red flag:** a `#pragma warning disable` with no matching `restore` and no comment. It silences the rule for the rest of the file, and nobody will remember why.

---

## 4. `.editorconfig`: rule severities and code style

### 4.1 What it is

`.editorconfig` is a plain-text settings file understood by most editors (VS, VS Code, Rider) **and** by the C# compiler. It's organised in **sections** keyed by file patterns:

```ini
root = true                       # stop searching parent folders here

[*]                               # every file
indent_style = space
insert_final_newline = true

[*.cs]                            # only C# files
dotnet_diagnostic.CA2016.severity = error
csharp_style_namespace_declarations = file_scoped
dotnet_diagnostic.IDE0161.severity = warning   # ...and enforce it in the build
```

### 4.2 Severities

| Severity | Editor | Build |
| --- | --- | --- |
| `none` | Rule is off | — |
| `silent` | Not shown; a refactoring may still be offered | — |
| `suggestion` | Faint dots under the code | Doesn't affect the build |
| `warning` | Green squiggle | Warning, so an **error** with `TreatWarningsAsErrors` |
| `error` | Red squiggle | Always fails the build |

### 4.3 The two ways to set a style rule, and the reliable one

A **style option** says *what you prefer*. A **diagnostic severity** says *how hard to enforce it*. They're separate:

```ini
csharp_style_var_elsewhere = true                  # preference only, enforces nothing on its own
csharp_style_var_elsewhere = true:warning          # preference + severity in one line (older shorthand)
dotnet_diagnostic.IDE0008.severity = warning       # severity on the rule id: the reliable form for build enforcement
```

The `:warning` shorthand is honoured by the editor, but **build-time** enforcement (`EnforceCodeStyleInBuild`) is only reliable when the rule's id has a `dotnet_diagnostic.<id>.severity` entry. If a style rule shows in the editor but doesn't fail `dotnet build`, check this first. (FleetTrack's `.editorconfig` uses the shorthand in a couple of places, such as `dotnet_style_readonly_field = true:warning`. Test whether it fails the build: add a field that could be `readonly`, then build.)

### 4.4 Nesting

`.editorconfig` files nest by folder, and a closer file **overrides** a farther one for the same setting. Unlike `Directory.Build.props`, they **merge**, so a nested file only needs the settings it changes. `root = true` in the top file stops the search going further up, so a stray `.editorconfig` in your user folder can't leak in.

---

## 5. `dotnet format`

`dotnet format` applies the `.editorconfig` rules to your files. It can fix whitespace, style and analyzer issues automatically.

- `dotnet format` rewrites the files in place. Run it before committing.
- `dotnet format --verify-no-changes` changes **nothing**. It just exits with a **non-zero code** if anything *would* change. That's what CI runs (M00-T7), so unformatted code can't be merged.
- Subcommands narrow it down: `dotnet format whitespace`, `dotnet format style`, `dotnet format analyzers`.

**Why run both the build and `dotnet format` in CI?** `EnforceCodeStyleInBuild` only fails on rules set to `warning`/`error`, and pure whitespace layout isn't fully covered by the build. `dotnet format --verify-no-changes` catches formatting the build lets through. It's a cheap check, so CI runs it first.

---

## 6. Nullable reference types

### 6.1 The problem

`NullReferenceException` is the most common bug in .NET code. Historically, *any* reference-type variable (a `string`, a `Vehicle`) could hold `null`, and nothing warned you until it crashed in production.

### 6.2 The feature

With `<Nullable>enable</Nullable>`, the type tells the compiler what's allowed:

```csharp
string plate = "B-123-ABC";   // "never null"
string? nickname = null;      // "may be null"

Console.WriteLine(nickname.Length);    // ⚠ CS8602: possible null dereference
if (nickname is not null)
    Console.WriteLine(nickname.Length); // ✓ the compiler knows it's not null here

plate = null;                           // ⚠ CS8600/CS8625: assigning null to a non-nullable
```

The compiler performs **flow analysis**: it follows `if` checks, `is not null` patterns and early returns, and knows where a variable can or can't be null.

### 6.3 What it is *not*

It's **compile-time only**. At runtime, `string` and `string?` are the same type, and nothing checks anything. `null` can still arrive:
- from deserialized JSON (a client simply omits a field),
- from reflection, or from a library without nullable annotations,
- from code that uses `!` (below).

So at **public boundaries** (API inputs, public library methods) you still validate, e.g. `ArgumentNullException.ThrowIfNull(vehicle);`. For API request bodies, validation happens at the endpoint (M02).

### 6.4 Useful syntax

- **`x!`, the null-forgiving operator,** means "trust me, this isn't null". It silences the warning, and checks nothing. Every `!` is a small claim the compiler can't verify, so treat it as a code-review red flag and ask why it's safe.
- **`required` members** (C# 11) make the compiler insist that object initialisers set them: `public required string Plate { get; init; }`. This avoids the old `= null!;` trick on properties.
- **Attributes** describe cases the compiler can't infer, such as `[NotNullWhen(true)]` on a `TryGet(out Vehicle? v)` method ("when this returns `true`, `v` is not null") and `[MaybeNull]`.

### 6.5 Adopting it on an existing codebase

Turning it on in a large old project produces thousands of warnings at once. Teams adopt it **gradually**: `#nullable enable` at the top of each file as it's touched, or `<Nullable>warnings</Nullable>` first (warnings without the new annotations), then flip the project to `enable` once the count is zero.

---

## 7. Implicit usings

`<ImplicitUsings>enable</ImplicitUsings>` makes the SDK generate a hidden file of `global using` directives for the namespaces almost every file needs: `System`, `System.Collections.Generic`, `System.Linq`, `System.Threading.Tasks`, plus extras for web projects such as `Microsoft.AspNetCore.Builder`. It removes boilerplate at the top of every file.

**The trade-off:** a file's dependencies are less visible. For an architecture-sensitive project like Domain, implicit usings for a *web* SDK would quietly make ASP.NET namespaces available. FleetTrack's Domain uses the plain SDK, so it only gets the `System.*` set. And the architecture tests check what Domain actually *uses*, which is what matters.

---

## 8. Rolling guardrails out on an existing codebase

Quiz Q8 and Q10 showed this is a weak spot, and it's a classic senior interview question: *"You join a 6-year-old solution with 40 projects, 2,000 warnings and no rules. What do you do?"*

Flipping `TreatWarningsAsErrors` on day one turns 2,000 warnings into 2,000 errors, blocks every team, and gets reverted by Friday. A rollout that works:

1. **Visibility before enforcement.** Add CI first, so every PR is built and tested the same way. Report the warning count, without failing the build yet.
2. **Stop the bleeding.** Fail the build if the warning count goes **up**, a "no new warnings" **ratchet**. Every PR can only keep the count level or lower it.
3. **Curate.** Use `WarningsAsErrors` with a short list of rules that catch **real bugs**: possible null dereferences, a missing `CancellationToken`, SQL injection, async misuse. Leave style rules for later.
4. **Fix by area, opportunistically.** Teams fix warnings in the code they're already changing (the "boy scout rule"), and project by project the full set gets turned on.
5. **Then flip the default.** When a project reaches zero, give it `TreatWarningsAsErrors=true`, so it can never slide back.

Write it up as an ADR, so the reason for each step survives people moving teams. The general principle: **guardrails need buy-in**, and a gradual ratchet earns it, where a big-bang switch creates resentment and workarounds (`<NoWarn>` everywhere).

---

## 9. Glossary

| Term | Meaning |
| --- | --- |
| **Guardrail** | A rule enforced automatically by the build or CI rather than by memory |
| **MSBuild** | The build engine behind `dotnet build`; `.csproj`, `.props` and `.targets` are MSBuild files |
| **Property / item / target** | MSBuild building blocks: a named value / a list entry (like a package reference) / a build step |
| **`Directory.Build.props` / `.targets`** | Files imported automatically for every project below them, early / late |
| **Analyzer** | A compiler plug-in that reports diagnostics (CA = quality, IDE = style) while compiling |
| **Diagnostic** | A message from the compiler or an analyzer, with an id, a severity and a location |
| **`AnalysisLevel`** | Which version and which mode (`default`, `recommended`, `all`…) of the built-in rules are active |
| **Severity** | How hard a rule is enforced: `none`, `silent`, `suggestion`, `warning`, `error` |
| **Ratchet** | A rule that lets a metric (e.g. warning count) only stay level or improve |
| **Nullable reference types** | Compile-time annotations (`string` vs `string?`) plus flow analysis that warn about possible nulls |
| **Null-forgiving operator (`!`)** | Tells the compiler to treat a value as non-null, without any runtime check |
| **Implicit usings** | SDK-generated `global using` directives for common namespaces |

---

## 10. Summary

1. `Directory.Build.props` applies build settings to every project below it. Only the nearest one is imported, so a nested file must import its parent explicitly.
2. Analyzers report CA (quality) and IDE (style) diagnostics. `AnalysisLevel` chooses the rule set, and `EnforceCodeStyleInBuild` makes style rules run in the build.
3. Warnings as errors keeps the count at zero. With `AnalysisLevel=latest`, it's another reason to pin the SDK.
4. Silence rules as narrowly as possible, with a written reason.
5. `.editorconfig` sets preferences *and* severities. Only `warning`/`error` affect the build, and `dotnet_diagnostic.<id>.severity` is the reliable way to enforce a style rule.
6. Nullable reference types are compile-time only; still validate at public boundaries, and treat `!` as a red flag.
7. On legacy code: visibility, then a ratchet, then a curated list, then the full switch, never a big bang.

### Check yourself

1. A project in `tests/` sets `<Nullable>disable</Nullable>` in its `.csproj`. Does it win over the root `Directory.Build.props`? What if the root setting were in `Directory.Build.targets` instead? (§1.2)
2. You add `tests/Directory.Build.props` to turn off one rule, and suddenly nullable warnings vanish from the tests. Why? (§1.3)
3. Why can upgrading the SDK fail a build whose code didn't change? Name two ways to prevent it. (§2.3)
4. `csharp_style_var_elsewhere = true` is in `.editorconfig`, but `dotnet build` never complains about `string x = ...`. Why not? (§4.3)
5. A method parameter is `Vehicle vehicle` (non-nullable), and a JSON client sends no vehicle. What happens at runtime? (§6.3)
6. Describe your first three guardrails, in order, for a 40-project legacy solution. (§8)

### Interview questions this prepares you for

- "How do you enforce code quality across 30 projects and 15 developers?" (`Directory.Build.props`, analyzers, `.editorconfig`, warnings as errors, CI, and an ADR explaining it.)
- "What's the risk of `AnalysisLevel=latest` combined with `TreatWarningsAsErrors`?"
- "Do nullable reference types prevent null at runtime?" (No; why, and where you still validate.)
- "How would you introduce `TreatWarningsAsErrors` on a legacy codebase with 2,000 warnings?"
- "When is it OK to suppress an analyzer warning, and how?"

### For FleetTrack

- The root [Directory.Build.props](../../../Directory.Build.props) sets `Nullable`, `ImplicitUsings`, `TreatWarningsAsErrors`, `EnforceCodeStyleInBuild`, `EnableNETAnalyzers` and `AnalysisLevel=latest-recommended`. The reasoning is in **ADR-002**.
- `CA1707` (underscores in names) is turned off for `tests/**`, because NUnit test names like `Domain_ShouldNotDependOn_API` use underscores on purpose.
- CI runs `dotnet format --verify-no-changes` before the build.
- From the loose ends: most naming rules in `.editorconfig` are at `suggestion`, so the build doesn't enforce them. Raise the ones you care about to `warning` with `dotnet_diagnostic` entries as the code grows (§4.3).
