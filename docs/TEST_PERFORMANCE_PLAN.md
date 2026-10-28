# Test suite performance plan

Written 2026-10-28. It covers both halves of the repository. Nothing in it has been carried out yet.

## Context

`dotnet test Vocaterra.sln` runs seven test projects with about 3,300 test methods. Theory rows add more on top of that. The full set "takes ages" (owner, 2026-10-26), so day-to-day work runs only `Vocaterra.Game.Tests`, and the full set waits for milestones.

The newest timings are from the gate logs of 2026-10-23, from before the projects were merged. In those, Game.Tests alone took 1 m 46 s to 2 m 3 s, the fuzz suite 28–30 s and content 18–31 s. Since then, Game.Core.Tests has grown to about 740 tests built on the same route scene.

Everything runs under `scripts/limited.sh`: one heavy command at a time, 10 of 16 cores, 6 GB of RAM and `-m:2`. Under those limits, wasted CPU and memory turn straight into extra minutes. The goal is to cut that waste without weakening any test or oracle, and then give developers a fast tier to run on every step.

## What makes the suite slow

All of these were found by reading the code. Nothing has been measured yet; phase T0 does that.

1. **The route scene is rebuilt about 380 times.**
   - The scene is 384×128×384, which is 4,608 chunks. One build takes about 0.4 s alone and 3 s or more under parallel load (`SceneTests.cs:28`).
   - These helpers call `SceneBuilder.Build` every time, and re-read `scene.json` each time:
     - `TestGame.Route`, `NewSession` and `NewSessionWithShippedLessons` (`tests/Vocaterra.Game.Core.Tests/TestGame.cs:31-43`)
     - `RouteScene.Build`, `NewSession` and `NewSessionWithShippedLessons` (`tests/Vocaterra.Game.Tests/RouteScene.cs:23-35`)
     - `VoxelTestSupport.BuildRoute` (`tests/Vocaterra.Game.Core.Tests/Voxel/VoxelTestSupport.cs:24`)
     - about 10 direct builds
   - The rebuild exists only because `VoxelSimulation` edits the grid in place (`src/Vocaterra.Game.Core/World/Scene.cs:7-11`: "build a new scene for a fresh run (it is cheap)"). It is no longer cheap.

2. **The content bundle is loaded about 45 times.** Each `GameBundle.Load()` / `SpanishContent.Load` does three expensive things:
   - reads all 481 files, including 33 MB of speech WAVs
   - computes the SHA-256 of every file
   - parses the 4.5 MB lexicon

   Where the loads happen:
   - **Game.Tests:** about 30 classes each hold their own `static Lazy<ContentBundle>`, often with their own `Lazy<NounCatalog>`, for example `LessonFrontendTests.cs:28` and `BookSpeechTests.cs:24-27`. Five more calls have no cache: `BookModelTests.cs:252`, `UiScaleLayoutTests.cs:34,441`, `PlaytestTests.cs:156` and `NpcVoiceTests.cs:20`. Every loaded copy keeps its WAV bytes alive, about 33 MB each, under a 6 GB cap.
   - **Nlp.Tests:** two static loads (`Content/TestSupport.cs:19`, `Pipeline/TestSupport.cs:80`), plus repeat loads in `ContentLoaderTests` and in the in-process CLI tests (`CliTests`, `LintCommandTests`, `ProgramTests`).
   - **Golden.Tests:** three static loads (`EndToEndGoldenTests.cs:39`, `ManifestGoldenTests.cs:22`, `SpanishIdentityTests.cs:46`).
   - **Already fine:** Game.Core.Tests (`TestGame.LoadedBundle`), Host.Tests and the fuzz harness each load once.

3. **Model files are hashed again for every test.**
   - `ModelGate.IsAvailable` (`tests/Vocaterra.Nlp.Tests/Asr/TestSupport.cs:99`) is not cached. It runs `ModelStore.Verify`, which hashes every model file, from inside each `[VoskModelFact]` / `[ParakeetModelFact]` constructor, so the cost lands during test discovery.
   - When `VOCATERRA_MODELS_DIR` is set, as it always is in the gate, that is about 1.9 GB hashed before the first test runs.
   - `Pipeline/VoskPipelineTests.cs:17-19,39-44` does the same.

4. **The serial collection holds tests that don't need it.**
   - `NativeRuntimeCollection` (`tests/Vocaterra.Nlp.Tests/NativeRuntimeCollection.cs`) sets `DisableParallelization = true`, so its tests run alone after the rest of the assembly finishes.
   - About 33 of its plain `[Fact]`s run without any model and never touch a native runtime.

5. **Smaller waste on every session:**
   - `RouteScene.NoLessons` loads `PhraseSheet.Load` twice and enumerates the speech lines on every `NewSession`.
   - `TestGame.Speech` and `TestGame.Lessons` are recomputed on every access.
   - Many polling loops sleep 1–5 ms while waiting for the turn worker, for example `LearnerRecordTests.cs:26-36` and `SessionLogTests.cs:40-50`.

6. **No fast/slow split exists.** There are no `[Trait]`s, no `xunit.runner.json` and no `.runsettings`. Developers can only run whole projects.

Not a cost today: coverlet is referenced but never collects, because nothing passes `--collect`. CsCheck depth is fixed by `FuzzOptions` and is a deliberate decision (see T5).

## Phases

Each phase is one milestone on its own branch, through the usual merge gate. The work splits by half:
- T2 changes core test projects, and T3 needs an owner decision on the fuzz suite. Both count as core milestones.
- The scene and bundle helpers in T1 are game work.

No phase removes an assertion, lowers an oracle's depth in the gate, or changes a golden.

### T0: Baseline measurement (tooling only)
- Add `scripts/test-timings.py`, which parses TRX files and prints three things:
  - the 30 slowest tests
  - total time per class
  - wall time per assembly
- Record a baseline with `scripts/limited.sh dotnet test Vocaterra.sln --no-build -m:2 --logger trx --results-directory .claude/test-timings/<stamp>`. Run it twice: once with `VOCATERRA_MODELS_DIR` set (the gate's case) and once without (the CI case).
- Put the baseline table in this document. Every later phase reports against it. The table also checks the estimates in the section above.

### T1: Build the route scene once and give each test a copy (game; biggest win)
- **Production code:**
  - Add `internal VoxelGrid Clone()` to `src/Vocaterra.Game.Core/Voxel/VoxelGrid.cs`. It copies each chunk's `BlockId[]` (ushort, about 38 MB for the route) and clears the digest caches.
  - Add `internal Scene Clone()` to `World/Scene.cs`. The NPC list and the points are read-only, so they can be shared.
  - `Vocaterra.Game.Core` already has `InternalsVisibleTo` set for both test projects.
  - Update the "it is cheap" remark in the `Scene` summary.
- **Test helpers:**
  - Add a process-wide `ConcurrentDictionary<ulong, Lazy<Scene>>` template cache, with the parsed `SceneLayout` cached alongside.
  - `TestGame.Route`, `RouteScene.Build` and `VoxelTestSupport.BuildRoute` return `template.Clone()`. The same applies to `RouteScene.Clearing`.
  - Point the direct `SceneBuilder.Build` call sites at the helpers. These are `FootstepTests`, `ChunkMesherTests`, `GreyscaleTests`, `FirstPersonCameraTests`, `BeaconArrowTests`, `CampObjectiveTests` and `VoxelSessionTests`.
- **Tests that must keep building for real** (they test the builder itself):
  - `SceneTests.Seed42SceneMatchesTheGoldenAndLoadsInUnderThreeSeconds`
  - `SceneTests.TheSceneBuildsIdenticallyTwice`
  - the generation tests under `World/Generation`
- **New tests:**
  - A clone has the same world digest as a fresh build.
  - An edit to a clone leaves the template unchanged.
- Cache the per-session extras: `NoLessons` and the phrase sheet per bundle, and `TestGame.Speech` / `Lessons` as `Lazy`.

### T2: Load each bundle once per test assembly (game and core test projects)
- **Game.Tests:** add one `TestBundle` (bundle and `NounCatalog`, static `Lazy`), like Game.Core's `TestGame`. Replace the roughly 30 per-class `Lazy` loads and the 5 uncached calls with it.
- **Nlp.Tests:**
  - Combine `Content/TestSupport` and `Pipeline/TestSupport` into one shared load.
  - Have the CLI tests that only read `--content BundleDir` go through a process-wide `LexiconCache`, where the CLI entry point accepts one. If it does not, leave them as they are and record the case for T6.
- **Golden.Tests:** one shared load instead of three.
- Every load still verifies hashes, so what the tests check stays the same.

### T3: Model gates and the serial collection (core test projects)
- Cache `ModelGate.IsAvailable` per model ID with a static `ConcurrentDictionary<string, Lazy<bool>>`. Do the same for the gate in `VoskPipelineTests`. Each model is then hashed once per process instead of once per attribute.
- Move the facts in `NativeRuntimeCollection` that never load a native runtime into ordinary, parallel classes. Check each one against the collection's stated reason (Vosk's process-wide log level, native libraries, gate caches); when in doubt, keep it serial.

### T4: A fast tier for daily work (both halves)
- Add `[Trait("Speed", "Slow")]`, with the constant in each project's test support, to these:
  - the scene-builder timing and generation tests
  - the fuzz suite (at assembly level)
  - the golden end-to-end tests
  - `PlaytestReportTests`, which spawns processes
  - the full-bundle CLI tests
  - `LintTests`, which copies the full bundle 26 times
  - model-gated facts
- Add `scripts/test.sh` with three modes, all going through `limited.sh` with `-m:2`:
  - `fast`: `--filter "Speed!=Slow"`
  - `full`: the whole suite, the same as the gate
  - `project <name>`
- Document the tiers in `docs/nlp/CONTRIBUTING.md` and `docs/game/README.md`. Update the orchestrator's verification guidance: `fast` per step, `full` at milestones and in the gate.
- The gate and CI keep running the full suite.

### T5: Fuzz depth for local runs (owner decision first)
- `FuzzOptions` deliberately has "no separate fast tier". T4 already leaves the fuzz suite out of `fast`. That is enough unless the owner also wants a cheap local fuzz pass.
- If they do, `scripts/test.sh fuzz-quick` sets the existing `VOCATERRA_FUZZ_*` variables to a tenth of the defaults for local use only. The gate's depth never changes.
- Separately, O15 (`ContentSchemaFuzzTests`) writes, hashes and loads a full bundle copy on every iteration, 666 times per run. Look for a cheaper equivalent that keeps the oracle unchanged, such as an in-memory file source, if the loader can take one. If that needs a loader change, it is a core milestone.

### T6: Optional follow-ups, judged on T0–T4 numbers
- **Don't keep speech WAV bytes in memory.** The core loader would read `speech/*.wav` lazily, or hash it as a stream without holding it. This is a core API change and needs its own milestone and ADR note.
- **Replace the `Thread.Sleep` polling** in the turn-worker tests with a completion signal, if T0 shows those waits matter.
- **Possible CI changes:**
  - Split CI into a fast job and a full job.
  - Run the test projects as a matrix.
  - Both need the workflow-scope token (CLAUDE.md), so nothing under `.github/workflows/` is written until the owner says so.
- **Possibly move to xUnit v3 / Microsoft.Testing.Platform** for faster startup and better parallelism. This is a large change, so only if T0 shows test-host startup is significant.

## Files touched (all phases)
- **New:** `scripts/test-timings.py` and `scripts/test.sh`
- **Production (game):** `src/Vocaterra.Game.Core/Voxel/VoxelGrid.cs` and `VoxelChunk.cs`, which get `Clone`, and `src/Vocaterra.Game.Core/World/Scene.cs`
- **Test helpers:**
  - `tests/Vocaterra.Game.Core.Tests/TestGame.cs` and `Voxel/VoxelTestSupport.cs`
  - `tests/Vocaterra.Game.Tests/RouteScene.cs`, plus a new `TestBundle.cs`
  - `tests/Vocaterra.Nlp.Tests/Asr/TestSupport.cs`, `Content/TestSupport.cs`, `Pipeline/TestSupport.cs` and `NativeRuntimeCollection.cs`
  - the Golden.Tests loaders
- **Call sites:** about 30 Game.Tests classes that drop their own `Lazy<ContentBundle>`, and the direct scene builds listed in T1
- **Docs:**
  - `docs/nlp/CONTRIBUTING.md`, `docs/game/README.md`, the orchestrator's verification notes, and this plan's results table
  - the core plan's findings log (`docs/nlp/IMPLEMENTATION_PLAN.md` 5b) and the game plan's findings log (`docs/game/IMPLEMENTATION_PLAN.md` section 5), each recording its own half

## Verification
Every phase:
1. `scripts/limited.sh dotnet build Vocaterra.sln -warnaserror -m:4`
2. `scripts/limited.sh dotnet test Vocaterra.sln --no-build -m:2 --logger trx`, then compare with the T0 baseline using `scripts/test-timings.py`:
   - The pass count and the skip count must be the same as the baseline, or higher only by the tests the phase adds.
   - The wall time must go down.
3. `dotnet format Vocaterra.sln --verify-no-changes`, then the merge gate (`GATE_TARGET=<branch> scripts/merge-gate.sh ...`), with `VOCATERRA_MODELS_DIR` set so the model tests run.

Phase-specific checks:
- **T1:** the new clone tests and the unchanged Seed-42 golden.
- **T3:** the model-gated tests run, not skip, with models present.
- **T4:** `scripts/test.sh fast` runs in a fraction of the time of `full`, and the slow tests it leaves out appear in its summary.
