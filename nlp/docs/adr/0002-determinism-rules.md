# ADR 0002: Determinism rules for the runtime

> **Note (M19a, 2026-10-20):** this record names the project `Slg.*` (and the `SLG_*` variables). Those names are now `Vocaterra.Nlp.*` and `VOCATERRA_*` (the old variable names are read for one release); see [ADR 0009](0009-core-and-game-boundary.md). The text below is unchanged.

- Status: accepted
- Date: 2026-10-20
- Deciders: repository owner and the implementation plan (`IMPLEMENTATION_PLAN.md` section 3)

## Context

The core promise of the system is that the same audio clip, game state and content version always produce the same verdict,
the same feedback and the same game outcome. `HIGH_LEVEL_DOCS.md` ("Determinism model") sorts every component into one of
three classes:

- **D0, algorithmic:** plain code with fixed-order math. Identical output on every machine.
- **D1, frozen inference:** a neural model with frozen weights, greedy decoding, a pinned runtime, one thread, CPU execution.
  Identical on the same platform build; decisions use margins so floating-point drift cannot flip an outcome.
- **N, non-deterministic:** sampled generation or anything not reproducible run to run. Not allowed in the core.

Determinism is only useful if it can be checked, so the rules also have to make every graded interaction replayable
(ADR 0004 covers the model side; `docs/REPLAY.md` covers the log format).

## Decision

The runtime (`src/`) follows these rules. They are the checklist in `CONTRIBUTING.md` and apply to every change.

| # | Rule | Where it lives |
| --- | --- | --- |
| 1 | No `System.Random`. All randomness comes from `SeededRng`, seeded with `SeededRng.ForInteraction(worldSeed, npcId, interactionCounter)`. | `Slg.Core.Determinism.SeededRng` (SplitMix64 seeding, xoshiro256** generator) |
| 2 | No wall clock in evaluation paths. `DateTime.Now`, `DateTime.UtcNow`, `Stopwatch` and `Environment.TickCount` are banned in `src/`. Time arrives as a `StoredTimestamp` (Unix seconds from the save). Latency is measured only in `tools/Slg.Bench`. | `Slg.Core.Determinism.StoredTimestamp`, `GameContext.Now` |
| 3 | Stable hashing only: SHA-256 over canonical bytes. Never `string.GetHashCode()` or `HashCode` for anything persisted or logged. | `DeterministicHash` |
| 4 | Ordered collections. Enumeration order of a `Dictionary` or `HashSet` never affects output. Sort with `StringComparer.Ordinal`; serialize maps with sorted keys. | `SlotMap`, `CanonicalJson` |
| 5 | Invariant culture, ordinal comparison, Unicode NFC everywhere. `InvariantGlobalization` stays off (ADR 0006). | `TextNormalizer`, every `string.Compare*` call |
| 6 | A margin on every threshold: accept at `t + e` or above, reject at `t - e` or below, otherwise take the defined "uncertain" branch. Fixed summation order; no parallel reductions in grading paths. | `Margin.Evaluate` |
| 7 | Models sit behind `Slg.Core` interfaces. Tests use deterministic fakes; real-model tests are skipped unless `SLG_MODELS_DIR` is set and the files verify. One thread, CPU provider, greedy decoding. | `IWordRecognizer`, `IPhonemeRecognizer`, `IEmbeddingModel`, `ISpeechSynthesizer` |
| 8 | No N-class code in `src/`: no LLM packages. Build-time LLM work (the Freeze pattern) lives in `tools/Slg.ContentBuilder` only, and is not part of this plan. | project references, `Directory.Packages.props` |
| 9 | Pinned versions. Every model file has a SHA-256 in `models/manifest.json`; every `VersionStamp` carries the content hash and the sorted model hashes. | `VersionStamp`, `ModelStore`, `ContentManifest` |
| 10 | Build hygiene: nullable enabled, warnings as errors, `dotnet format` clean, a one-line `///` summary on every public type. | `Directory.Build.props`, `.editorconfig` |

Verification is part of the decision, not an afterthought:

- Every graded interaction can be written to a replay log and re-evaluated without the models. A replay that differs from
  the recording fails with the JSON paths that changed (`docs/REPLAY.md`).
- `tests/Slg.Golden.Tests` pins the worked example end to end (a committed golden JSON file), the G2P output for 1,788
  golden lines (Latin American and Castilian), and the golden manifest layout. The content bundle hash is pinned in
  `tests/Slg.Content.Tests/ManifestTests.cs`.
- The pure stages and the pipeline are tested by running them twice and comparing the canonical JSON byte for byte.

## Consequences

- Contributors cannot use convenient APIs: no `Random.Shared`, no `DateTime.UtcNow` for a timeout, no `Parallel.For` with a
  floating-point reduction, no default `Dictionary` enumeration in output code. The price is a few more lines per feature.
- The rules are enforced by review and by the PR checklist, plus the tests above. There is no analyzer that bans the APIs in
  rules 1 to 3 yet. Adding one (a banned-API analyzer needs a new package, so the owner has to approve it; a test that scans
  `src/` for the banned identifiers needs none) would turn those rules from review items into build failures.
- D1 models can still drift between CPUs or ONNX Runtime builds. Margins contain this, and the cross-platform CI matrix
  (parked in `.github/workflows-pending/ci.yml`) is meant to detect it. Until that workflow is active, drift across platforms
  is unmeasured.
- Because the verdict, decision and response are pure functions of their inputs, a bug report only needs a replay log line
  (no audio, no models) to be reproduced.
- Anything that needs real nondeterminism (free conversation with a runtime LLM, Phase 3 in the roadmap) has to go through
  the Cache or Advisory pattern from the high-level design and may never change a verdict, the learner model or game state.
