# ADR-002: .NET 10 LTS with build guardrails enforced from day one

* **Status:** Accepted
* **Date:** 2026-09-30 (build guardrails section added 2026-10-15)
* **Module:** M00

## Context and Problem Statement

FleetTrack is a long-running project (16 modules, many months) that should end up looking like a production .NET codebase. We need to pick a runtime release track, and decide how strictly the build enforces code quality. Forces:

* The project must not have to change runtime mid-way just because support ended.
* $0 budget: only the free SDK and built-in analyzers.
* One developer, no reviewer on every change: the build itself has to catch mistakes.
* Starting strict is cheap on an empty codebase; adding strictness later means fixing hundreds of existing warnings.

## Considered Options

* .NET 10 LTS (3 years of support, to November 2028)
* .NET 11 STS when it ships (November 2026, 2 years of support)
* Build guardrails: strict from day one vs. defaults (warnings only) and tighten later

## Decision Outcome

Chosen option: ".NET 10 LTS", pinned in `global.json` (`10.0.100`, `rollForward: latestFeature`), because its support window covers the whole plan with no forced upgrade, and most companies run LTS in production, so it matches what interviews and real jobs expect.

Chosen option: "strict from day one" for the build guardrails (see below), because on an empty codebase it costs almost nothing, and it is the only time it does.

### Consequences

* Good, because the runtime is supported for the whole plan, and every machine and CI run uses the same SDK feature band.
* Good, because warnings never pile up: the count stays at zero, so a new one is noticed.
* Bad, because new runtime and C# features arrive a year later than on STS, and we skip testing them as they appear.
* Bad, because moving from one LTS to the next is a bigger jump (two releases of changes at once) than following every release.
* Bad, because warnings as errors adds friction when prototyping: a half-written method with an unused variable doesn't build.
* Bad, because `latest-recommended` analysis moves with the SDK: a new SDK can bring new rules and break the build with no code change.

## Build guardrails

Set once for every project in `Directory.Build.props`:

| Setting | What it does | Why |
| --- | --- | --- |
| `Nullable=enable` | The compiler tracks which references can be null | Turns most `NullReferenceException`s into compile-time warnings |
| `TreatWarningsAsErrors=true` | Any warning fails the build | Keeps the warning count at zero, so warnings are read |
| `EnableNETAnalyzers` + `AnalysisLevel=latest-recommended` | Built-in code-quality analyzers (CA rules) at the recommended set | Free, ships with the SDK, catches real bugs (e.g. disposal, culture, performance) |
| `EnforceCodeStyleInBuild=true` | `.editorconfig` style rules (IDE rules) run in the build, not only in the IDE | CI enforces the same style as the editor |

`.editorconfig` holds the rule severities. Exceptions are made per scope, never by turning a guardrail off globally (e.g. `CA1707` is off only for `tests/**`, so test names can use underscores). CI runs `dotnet format --verify-no-changes` in addition to the build.

## Revisit trigger

* **Runtime:** .NET 11 ships a feature FleetTrack needs (and can't wait a year for), or .NET 12 LTS ships (November 2027): plan the upgrade within its first few months.
* **Guardrails:** an SDK update breaks the build through new analyzer rules more than once; then pin `AnalysisLevel` to a fixed version (e.g. `10.0-recommended`) and upgrade it on purpose.
