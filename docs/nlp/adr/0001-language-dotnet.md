# ADR 0001: Implement the system in C# on .NET

> **Note (M19e, 2026-10-21):** game-side matters in this record, such as the engine choice and the game's scenes, are now decided in the Vocaterra game repository, not in this one. The text below is unchanged.

> **Note (M19a, 2026-10-20):** this record names the project `Slg.*` (and the `SLG_*` variables). Those names are now `Vocaterra.Nlp.*` and `VOCATERRA_*` (the old variable names are read for one release); see [ADR 0009](0009-core-and-game-boundary.md). The text below is unchanged.

- Status: accepted
- Date: 2026-10-20
- Deciders: repository owner and the implementation plan (`implementation-plan.md` section 1)

## Context

The high-level design (`docs/archive/nlp/history/high-level-docs.md`, "Technology choices") allows Rust, C# or Go and recommends C#. The constraints
that matter are:

- Everything runs locally and offline, on consumer hardware (the plan assumes 8 GB RAM, 4 cores, no GPU).
- The runtime must be deterministic, so the inference runtimes have to be pinnable and runnable single-threaded on the CPU.
- The engine is not chosen yet. The realistic candidates for a Terraria-like game on .NET are Godot .NET and MonoGame
  (the XNA lineage Terraria itself comes from). On the Rust side the candidate is Bevy.
- The speech stack is Vosk (constrained recognition), sherpa-onnx (Parakeet open recognition, Kokoro speech synthesis)
  and ONNX Runtime (phoneme recognizer, embeddings). C# has first-party or official NuGet packages for all three. Go has the
  thinnest ecosystem of the three languages.

## Decision

- The whole system is written in C# on the current .NET LTS SDK. The SDK is pinned in `global.json`
  (`10.0.402`, `rollForward: latestPatch`) and every project targets `net10.0`.
- `Directory.Build.props` applies one set of rules to every project: `Nullable` enabled, `ImplicitUsings`,
  `TreatWarningsAsErrors`, `Deterministic` builds (and `ContinuousIntegrationBuild` when `CI` is set), `LangVersion latest`, and
  XML documentation for everything under `src/`.
- Package versions live in one place, `Directory.Packages.props` (central package management). Only the packages listed
  there may be used; a contributor who needs another package asks for it instead of adding it. This keeps the license scan
  (`scripts/license-scan.sh`) meaningful.
- Tests use xUnit v2 (2.9.x) with plain `Assert`. FluentAssertions is not used, for license stability.
- Projects are named `Slg.*` ("Slg" for SpanishLearningGame).

## Consequences

- Contributors need the pinned SDK. It does not have to be installed system-wide: a user-local install in `~/.dotnet` with
  `DOTNET_ROOT` and `PATH` pointing at it is enough (see the README).
- The native speech packages (Vosk, sherpa-onnx, ONNX Runtime) are large. A full solution build writes about 3 GB of
  `bin/` and `obj/` output per checkout, mostly native runtime packs for every platform. Trimming the runtime identifiers is a
  known follow-up. Worktrees are cleaned after use (`git clean -fdX -- src tests tools`).
- Switching to Rust later would mean rewriting the libraries, not porting them. The determinism rules (ADR 0002), the
  contracts in `Slg.Core` and the content bundle format are language-neutral on purpose, so a rewrite would keep the same
  tests, replay logs and content.
- The repository has no license file yet. That is a decision for the repository owner, not an engineering decision.
