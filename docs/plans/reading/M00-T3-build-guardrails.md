# M00-T3 reading notes: build guardrails

Task: [M00-T3](../modules/M00-foundations.md) · Sources: [Customize your build by folder](https://learn.microsoft.com/visualstudio/msbuild/customize-by-directory) · [Code analysis overview](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/overview) · [Configuration files for code analysis](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/configuration-files) · [Nullable reference types](https://learn.microsoft.com/dotnet/csharp/nullable-references)

**Big idea:** a **guardrail** is a rule the build enforces automatically, so quality doesn't depend on people remembering things. A rule only in a wiki gets forgotten; a rule that fails the build doesn't.

---

## 1. `Directory.Build.props`: settings for every project at once

**MSBuild** is the engine behind `dotnet build`. Before it builds a project, it searches the project's folder and each parent folder for a file called `Directory.Build.props`, and imports the first one it finds. Anything you put in it applies to every project underneath, so you set things like "nullable on" and "warnings are errors" once instead of in 20 `.csproj` files.

Details worth knowing:

- **`.props` vs `.targets`:**
  - `Directory.Build.props` is imported **early**, before the project's own settings, so a project can still override a value.
  - `Directory.Build.targets` is imported **late**, after the project's settings, so it's the place for build steps or for values that must win.
- **Only the nearest file is imported.** If `tests/Directory.Build.props` exists, the root one is *not* imported automatically for the test projects. You have to import it explicitly from the nested file. This catches people out regularly.
- **Don't confuse it with `Directory.Packages.props`**, which manages package *versions* ([T1](M00-T1-sdk-pinning-cpm.md)). `Directory.Build.props` manages *build settings*.

---

## 2. Code analyzers

An **analyzer** is a plug-in to the C# compiler that inspects your code while it compiles and reports problems. .NET ships with two families:

- **CA rules ("code analysis")** are about *quality and correctness*, for example `CA2007` (async usage), `CA1062` (validate arguments) and security rules.
- **IDE rules** are about *code style*, for example `IDE0055` (formatting) and `IDE0005` (unnecessary `using`).

Settings that control them:

| Setting | What it does |
| --- | --- |
| `EnableNETAnalyzers` | Turns the built-in analyzers on (already on by default for .NET 5+). |
| `AnalysisLevel` | Which *set* of rules is active, e.g. `latest-recommended`. `latest` means the newest rules shipped with your SDK; `recommended` is a stricter group than the default; you can also pin a number like `10.0`. |
| `EnforceCodeStyleInBuild` | IDE (style) rules normally only show in the editor. This makes them run in `dotnet build` too, so CI can enforce them. |
| `TreatWarningsAsErrors` | Any warning fails the build. |

### The `latest` trade-off
`AnalysisLevel=latest` means a newer SDK can bring **new rules**. Combined with `TreatWarningsAsErrors`, upgrading the SDK can suddenly break your build. That's one reason to pin the SDK ([T1](M00-T1-sdk-pinning-cpm.md)). Pinning `AnalysisLevel` to a number gives stability, but you miss new rules until you bump it on purpose.

### Why warnings as errors?
Warnings that don't fail the build pile up. Once there are 300, nobody reads them, and the one important new warning gets lost. Making them errors keeps the count at zero. The cost is a bit of friction when you're prototyping.

---

## 3. `.editorconfig`: configuring rules and severities

`.editorconfig` is a text file (supported by most editors and by the compiler) where you set code style preferences and how serious each analyzer rule is. Each rule gets a **severity**:

| Severity | Effect |
| --- | --- |
| `none` | Rule is off. |
| `silent` | Rule runs but nothing is shown (a refactoring may still be offered). |
| `suggestion` | Shown as a hint in the editor; never breaks the build. |
| `warning` | Shown as a warning; breaks the build if warnings are errors. |
| `error` | Always breaks the build. |

Syntax: `dotnet_diagnostic.CA1062.severity = warning`.

Two things to know:
- **`.editorconfig` files can be nested.** A file in a subfolder can override rules just for that folder, for example relaxing a rule in tests. Set `root = true` in the top-level file so the search stops there.
- **Style preferences only fail the build if they have a severity of `warning` or `error`.** Writing `csharp_style_var_elsewhere = true` alone doesn't enforce anything.

---

## 4. Nullable reference types

### The problem
`NullReferenceException` is the most common bug in .NET code. Historically, any reference-type variable (a `string`, a `Vehicle`) could be `null`, and nothing warned you.

### The feature
With `<Nullable>enable</Nullable>`:
- `string name` means "should never be null".
- `string? name` means "may be null".

The compiler tracks this through your code and **warns** when you might use something that could be null, or assign null to something that shouldn't be.

### What it is *not*
It's **compile-time only**. At runtime nothing changes: a caller using reflection, deserialized JSON or a library without annotations can still hand you `null`. So at **public boundaries** (API inputs, public library methods) you still validate, for example with `ArgumentNullException.ThrowIfNull(x)`.

### Useful syntax
- `x!`, the **null-forgiving operator**, tells the compiler "trust me, this isn't null". Every `!` is a small lie the compiler can't check, so treat it as a code-review red flag.
- Attributes like `[NotNullWhen(true)]` describe more complex cases, as on `TryGetValue`-style methods.

### Adopting it in an existing codebase
Turning it on for a big old project produces thousands of warnings. Teams enable it **file by file** with `#nullable enable` at the top of each file, then flip the project setting once they're done.

---

## Interview questions this prepares you for

- "How do you enforce code quality across 30 projects and 15 developers?" (`Directory.Build.props`, analyzers, `.editorconfig`, warnings as errors, CI.)
- "What's the risk of `AnalysisLevel=latest` with `TreatWarningsAsErrors`?"
- "Do nullable reference types prevent null at runtime?" (No. Explain why, and where you still validate.)

## For FleetTrack

The root [Directory.Build.props](../../../Directory.Build.props) turns on nullable, warnings-as-errors, code style in build and `latest-recommended`. `dotnet format --verify-no-changes` must also pass.
