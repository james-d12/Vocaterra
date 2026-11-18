# Test suite performance plan

Written 2026-10-28. It covers both halves of the repository. On 2026-10-29 it was checked against `main` at `604903f` (after R16 moved the
game's tests between projects) and carried out on the branch `feature/test-performance`: T0 to T4 are done (T3b on the owner's word), the owner
declined T5's local fuzz pass, and the items of T6 are open. The results are in the next section; each phase below says what was done and where it differs from the first plan.

## Results

Each run is `dotnet test Vocaterra.sln --no-build -m:2 --logger trx` through `scripts/limited.sh`, on the development machine (16 cores,
11 GiB, the run capped at 10 cores and 6 GB), measured with `scripts/test-timings.py`. The run's wall time is the span from the first test
assembly's start to the last one's finish, so the time a run waits for another heavy command is not counted.

| Assembly | T0, models | T0, no models | After T1 to T3, models | T4, full, models | Branch, full, models | T4, full, no models | T4, fast, models |
|---|---|---|---|---|---|---|---|
| Game.Core.Tests | 146.1 s | 141.2 s | 66.5 s | 40.8 s | 36.8 s | 35.4 s | 26.1 s |
| Game.Tests | 39.3 s | 37.7 s | 18.6 s | 15.1 s | 15.2 s | 15.4 s | 5.8 s |
| Nlp.Tests | 63.1 s | 35.9 s | 59.4 s | 53.5 s | 35.7 s | 34.7 s | 11.3 s |
| Nlp.Fuzzing.Tests | 64.6 s | 63.0 s | 49.5 s | 39.2 s | 40.6 s | 41.0 s | left out |
| Nlp.Golden.Tests | 16.1 s | 13.8 s | 13.2 s | 13.0 s | 13.2 s | 13.2 s | 1.5 s |
| Nlp.Es.Tests | 3.4 s | 3.5 s | 3.9 s | 4.0 s | 4.2 s | 4.0 s | 3.6 s |
| Host.Tests | 1.7 s | 1.4 s | 1.3 s | 1.2 s | 1.3 s | 1.2 s | 1.3 s |
| **The run** | **186.3 s** | **153.2 s** | **130.0 s** | **108.1 s** | **86.5 s** | **84.3 s** | **26.1 s** |
| Passed / skipped | 5,447 / 3 | 5,419 / 31 | 5,449 / 3 | 5,449 / 3 | 5,449 / 3 | 5,421 / 31 | 5,142 / 0 |

The two extra passes from T1 on are the two clone tests it adds; no test was removed, skipped or weakened, and no golden changed. "T4" is T1
to T4 with the warmed scene of T1; "branch" adds T3b and the timing collection (T1), and is the state of the branch (three runs: 86.5, 87.9
and 86.7 s). The no-models and the fast columns were measured at T4; T3b and the timing collection change little there (without models the
collection's model tests skip, and the fast tier leaves them and the timing tests out). Runs of the same code differ by 10 s or more an
assembly with what runs beside them and the machine's other load: the fuzz suite, whose tests no phase changed, took 64.6, 49.5 and 39.2 s
in three runs with models, so the step from 66.5 to 40.8 s for Game.Core.Tests is partly the warmed scene and partly that variance (the two
were not measured apart). The fast tier runs 94 percent of the tests in under a third of the full run's time (26 s against 87 s). With
`-m:2` two test assemblies run at once, so the run's wall time is set by the longer of the two lanes. At T0 that was Game.Core.Tests (146 s)
beside everything else (188 s). On the branch the lanes are Host, Game, Fuzzing and Es (ending after 62 s) and Game.Core, Golden and
Nlp.Tests (86 s); the assemblies hold about 148 s of work, so an even split would end near 75 s, and the order in which `dotnet test` starts
them is the remaining lever on the run as a whole (T6).

**T7 (2026-11-06), the memory of the game tests.** Peak of each test command's cgroup (`limited.sh`), models on. Before (565554f3 and later): Game.Core.Tests 5.0
GB alone (Failed 2 in that run, see T7), Game.Tests 1.6, Host.Tests 0.3, Nlp.Tests 2.8, Es.Tests 1.3, Golden.Tests 0.4, Fuzzing.Tests 2.5; `scripts/test.sh full`
was killed by the 6 GB cap (exit 143) at 87 s. After: Game.Core.Tests 2.4 to 2.5 GB alone (two runs, 73 to 81 s, 2384 passed), and `scripts/test.sh full`
passed three times in a row with peaks of 4.6, 4.7 and 4.8 GB and wall times (build included) of 274, 255 and 238 s; the counts are the same as before (9586
passed, 3 skipped). Without models (CI's case) the full run passed once, 5.2 GB, 234 s, 28 + 17 model tests skipped; the full run overlaps Nlp.Tests, the fuzz suite and Game.Core.Tests, so its peak moves between 4.6 and 5.2 GB.

## Context

`dotnet test Vocaterra.sln` runs seven test projects with about 3,360 test methods, 5,450 tests with the theory rows. R16 (2026-10-28) moved
most of the game's session tests from Game.Tests to Game.Core.Tests, which now has 1,210 tests (983 methods) on the same route scene;
Game.Tests has 529. The full set "takes ages" (owner, 2026-10-26), so day-to-day work ran only `Vocaterra.Game.Tests` and the full set waited
for milestones.

Everything runs under `scripts/limited.sh`: at most three heavy commands at once (two from 2026-11-05, three since 2026-11-16; one until 2026-11-05), each at most 10 of 16 cores and 6 GB of RAM (10 GB since 2026-11-16), all together at most 14 cores and 8 GB (16 GB since 2026-11-16), and `-m:2`. Under those limits, wasted
CPU and memory turn straight into extra minutes. The goal was to cut that waste without weakening any test or oracle, and then give
developers a fast tier to run on every step.

## What made the suite slow

The first plan found these by reading the code; T0 measured them. Each item says what the measurement showed.

1. **The route scene was rebuilt for almost every game test.** The scene is 384×128×384 (4,608 chunks, 38 MB of blocks); one build takes
   about 0.6 s alone and 0.8 to 2.5 s beside the other tests (`SceneTests` prints it). `TestGame.Route` and `NewSession`
   (`tests/Vocaterra.Game.Core.Tests/TestSupport/TestGame.cs`), `RouteScene.Build`, `Clearing` and `NewSession` (`TestSupport/RouteScene.cs`,
   linked into Game.Tests since R16), `VoxelTestSupport.BuildRoute` and seven direct `SceneBuilder.Build` calls built and re-read
   `scene.json` every time. The rebuild existed only because `VoxelSimulation` edits the grid in place (`Scene`'s summary called a build
   cheap). Confirmed: it was the largest cost. With T2, T1 took Game.Core.Tests from 146 s to 41 s and Game.Tests from 39 s to 15 s.
2. **The content bundle was loaded about 45 times.** Each `GameBundle.Load()` / `SpanishContent.Load` reads all 481 files with their 33 MB of
   speech WAVs, computes the SHA-256 of every file, parses the 4.5 MB lexicon and keeps the WAV bytes alive. After R16 the copies were:
   20 classes of Game.Core.Tests and 14 of Game.Tests with their own `Lazy<ContentBundle>` (most with their own `NounCatalog`), five
   uncached calls (`NpcVoiceTests`, `BookModelTests`, `PlaytestTests`, `UiScaleLayoutTests` twice), two loads in Nlp.Tests
   (`Content/TestSupport`, `Pipeline/TestSupport`) and three in Golden.Tests. Host.Tests, Es.Tests and the fuzz harness load once. The first
   plan's "already fine: Game.Core.Tests" stopped being true when R16 moved the classes there.
3. **Model files were hashed again for every model attribute.** `ModelGate.IsAvailable` ran `ModelStore.Verify` from each
   `[VoskModelFact]` / `[ParakeetModelFact]` constructor, during discovery; `Pipeline/VoskPipelineTests.cs` had a second copy of the gate.
   Measured: the gates verify Vosk (58 MB) and Parakeet (641 MB) only, not 1.9 GB (Kokoro, 387 MB, has no hashing gate), and SHA-256 runs at
   about 1 GB/s here, so this cost seconds, not minutes. It is fixed anyway (T3).
4. **The serial collection.** `NativeRuntimeCollection` sets `DisableParallelization = true`, so its tests run alone after the rest of
   Nlp.Tests. Measured: its 33 plain facts (no model) take 0.2 s in all, so moving them out gains nothing; the collection's cost is its
   model tests (23.7 s with models, run one at a time at the end of the assembly). That is what the gap between Nlp.Tests with models
   (63 s) and without (36 s) is. See T3b.
5. **Smaller waste on every session:** `RouteScene.NoLessons` loaded `PhraseSheet` twice and enumerated the speech lines on every
   `NewSession`; `TestGame.Speech`, `Lessons` and the shipped lesson book were recomputed on every access. Fixed in T1. The polling loops
   that sleep 1 to 5 ms while waiting for the turn worker (`LearnerRecordTests`, `SessionLogTests`, ...) do not show among the slow tests;
   left as they are (T6).
6. **No fast/slow split existed.** There were no `[Trait]`s, no `xunit.runner.json` and no `.runsettings`. Fixed in T4.

Not a cost: coverlet is referenced but never collects, because nothing passes `--collect`. CsCheck depth is fixed by `FuzzOptions` and is a
deliberate decision (T5).

A finding the first plan did not have: **inside one test class xUnit runs the tests one after another**, and a test assembly's
classes run in parallel, so an assembly cannot finish before its largest class. Alone, Game.Core.Tests now uses about 2.4 of its 10 cores on
average; its wall time is set by its biggest classes (`VillageTests`, `SceneTests`, `SaveMigrationTests`, `GuideTurnTests`), not by the CPU.

## Phases

The work splits by half: T1 and the game's part of T2 are game work, the core's part of T2, T3 and T5 are core work. They landed as one
commit each on `feature/test-performance` (one worktree, at the owner's request) instead of one branch each. No phase removed an assertion,
lowered an oracle's depth or changed a golden.

### T0: Baseline measurement (tooling only), done
- `scripts/test-timings.py RUN [BASE] [--top N]` reads a run's TRX files and prints each assembly's wall time and counts, the run's wall time
  (first start to last finish), the N slowest tests and the N classes with the most test time; with `BASE` it prints both runs side by side.
- The baseline was recorded twice, with `VOCATERRA_MODELS_DIR` set (the gate's case) and without (the CI case), into
  `.claude/test-timings/` (git-ignored). The table is in Results.

### T1: Build the route scene once and give each test a copy (game), done
- **Production code:** `internal VoxelChunk Clone()`, `VoxelGrid Clone()` and `Scene Clone()`. A clone copies the blocks (38 MB for the
  route) and keeps the version, the edit count, the non-air count, the kind mask and the cached digests (an edit replaces a cached digest and
  never writes into it, so the arrays are shared); the spawn, the NPCs and the points are read-only and shared. The `Scene` summary no
  longer calls a build cheap. `Vocaterra.Game.Core` already had `InternalsVisibleTo` for both test projects.
- **Test helpers:** `RouteScene` keeps one built scene per layout and seed in a `ConcurrentDictionary<(layout, seed), Lazy<Scene>>`, with
  each layout parsed once, and hands out clones; the built scene computes its digests and kind masks once, so the clones carry them.
  `TestGame.Route` and `VoxelTestSupport.BuildRoute` (and its `RouteDraft`) go through it; so do `RouteScene.Clearing` and the direct
  builds in `FootstepTests`, `ChunkMesherTests`, `GreyscaleTests`, `FirstPersonCameraTests`, `BeaconArrowTests`, `CampObjectiveTests` and
  `VoxelSessionTests`.
- **Still built for real:** `Seed42SceneMatchesTheGoldenAndLoadsInUnderThreeSeconds` and `SceneTests.TheSceneBuildsIdenticallyTwice` (two
  builds, no longer two copies of one), and the small layouts of `TerrainTests`. The other generation tests only read a scene of a given
  seed, and a clone is that scene, so they share the copies (the first plan kept all of `World/Generation` on real builds).
- **New tests:** `VoxelGridTests.ACloneReadsLikeItsOriginalAndEditsApartFromIt` and
  `SceneTests.TheTestsCopyOfTheRouteIsAFreshBuildAndItsEditsStayInIt` (the copy has the golden digest, the fresh build's counts, versions,
  NPCs and points, and an edit to it reaches no other copy).
- The per-session extras are made once: `TestGame.Phrases`, `Speech`, `NoLessons` and the shipped lesson book as `Lazy`, and
  `RouteScene.NoLessons` per bundle (a `ConditionalWeakTable`). All of them are read-only.
- The built scenes stay in memory for the test process: one per seed the tests use (ten in Game.Core.Tests, about 38 MB each). Even so the
  peak memory fell with T1 and T2 together: Game.Core.Tests alone peaked at 3.61 GB on `main` and 2.80 GB on the branch (the test host's
  peak resident set, sampled), since each test process now holds one bundle instead of up to 21.
- **The timing tests run on their own.** With the route built once per seed, a run's first seconds build it for every seed the tests use,
  many at once, and the seed-42 timing test (three timed builds, the fastest under 3 s) measured that crowd: it failed in two of the fifteen runs
  of Game.Core.Tests that included it (3.5 to 4.0 s at the fastest attempt; about 0.6 s alone), and very likely in a third whose log was
  not kept. It moved, unchanged, from `SceneTests` to its own
  class `SceneBuildTimeTests` in a new serial collection, `TimingCollection` (Game.Core.Tests' `TestSupport/`), which runs after the rest of
  the assembly. `ChunkIndexTests` (six small tests, one with a 100 ms budget) is in it too. The budgets are unchanged. In three full runs since, the timed build took 0.66 to 0.75 s and every run passed.

### T2: Load each bundle once per test assembly (game and core test projects), done
- **Game:** the 34 per-class loads and the five uncached calls use `TestGame.Bundle`, `TestGame.Nouns` and `TestGame.Phrases`. `TestGame`
  is linked into Game.Tests as `RouteScene` is, instead of a new `TestBundle` class: since R16 both projects need it.
- **Core:** `GameBundleLocator` (tests/Shared, linked into the core's test projects) has a `Bundle`, loaded on first use with every hash
  verified. Nlp.Tests' two loads and Golden.Tests' three use it.
- The CLI tests (`CliTests`, `LintCommandTests`, `ProgramTests`) still load the bundle through `--content`: the CLI takes no `LexiconCache`.
  Recorded for T6; T4 leaves them out of the fast tier instead.
- Every load still verifies the hashes, so what the tests check is the same.

### T3: Model gates and the serial collection (core test projects), done
- `ModelGate.IsAvailable` keeps its verdict per model id (`ConcurrentDictionary<string, Lazy<bool>>`). `VoskPipelineTests`' copy of the
  gate is removed; it uses the ASR tests' gate, so the cache is shared. The model tests run, not skip, with models present (3 skipped, as at
  T0: the wav2vec2 and e5 models are not in the shared folder).
- **Not done: moving the collection's plain facts out.** T0 measured them at 0.2 s in all (item 4), so the move would gain nothing for the
  risk the collection's comment describes.
- **T3b, done (owner, 2026-10-29): the collection runs beside the assembly's other tests.** `NativeRuntimeCollection` no longer sets
  `DisableParallelization = true`: its own tests still run one at a time (an xUnit collection always runs in order), but no longer wait
  until the rest of Nlp.Tests has finished. Measured with models, Nlp.Tests alone: 53.4 and 53.7 s with the flag, 34.3, 34.3 and 34.5 s
  without it, all green with the same counts (2,452 passed, 3 skipped). It reverses M52's choice that these tests never run beside another
  test of the assembly; no reason beyond the shared native state was recorded, and every test that loads a native runtime is in the
  collection (its comment now says that a new one belongs there). In the full run with models, Nlp.Tests took 34.9 to 36.3 s in five runs (53.5 s at T4).

### T4: A fast tier for daily work (both halves), done
- `[Trait(TestSpeed.Trait, TestSpeed.Slow)]` (the constant in `tests/Shared/TestSpeed.cs` for the core and
  `tests/Vocaterra.Game.Core.Tests/TestSupport/TestSpeed.cs` for the game) marks:
  - the fuzz suite, as an assembly trait (`FuzzOptions.cs`);
  - the 31 model-gated facts (14 of the game's, 17 of the core's);
  - the world-generation classes `SceneTests`, `SceneBuildTimeTests`, `VillageTests` and `UndergrowthTests` (real builds, the timing test, and scenes of seeds the
    session tests do not use), and `PlayerBodyTests.TheBodyNeverEndsATickInsideASolidOnTheRealRouteEither` (six seeds, 3,000 ticks each);
  - `PlaytestReportTests`, which starts processes;
  - the content tool's full-bundle classes `CliTests`, `ProgramTests`, `LintTests` and `LintCommandTests`;
  - `SpanishIdentityTests` (the frozen identity hashes, 12 to 15 s). The other golden tests stay in the fast tier: they take about 1.5 s in
    all, and the first plan's "golden end-to-end tests" are among them;
  - the other single tests over about 4 s in the full run: the whole-route meshing and greyscale tests, two scripted-run tests, a lesson
    replay, `TextGraderTests` and `TwoPhaseTests` over every example, and `GeneratedLexiconTests.LoadingTheBundleIsFastEnough` (a timing
    budget, better run on a quiet machine).
- `scripts/test.sh fast|full|project <name>` builds with `-m:4` and tests with `-m:2` through `limited.sh`, sets `VOCATERRA_MODELS_DIR`
  to the shared folder when it exists (as the gate does), writes the TRX files to `.claude/test-timings/<mode>-<stamp>/`, names the tests
  the fast tier left out and ends with each assembly's wall time. `fast` is `--filter "Speed!=Slow"`; a `--filter` given to it is combined.
- The tiers are documented in `docs/nlp/building-and-testing.md`, `docs/game/building-and-testing.md`, `scripts/README.md` and the orchestrator's brief template
  and resource rules: `fast` per step, `full` at a milestone and in the gate.
- The gate (`scripts/merge-gate.sh`) and CI (`.github/workflows/ci.yml`, which exists since 2026-10-29 and runs the full suite without
  models) still run the full suite.

### T5: Fuzz depth for local runs, declined (owner, 2026-10-29)
- `FuzzOptions` keeps its one full depth, and T4 leaves the whole fuzz suite out of `fast`. The owner chose that over a cheap local pass
  (`scripts/test.sh fuzz-quick` at a tenth of the default depths), so the fuzz suite runs in full in `full`, the gate and CI, and nowhere
  shallower.
- O15 (`ContentSchemaFuzzTests.MutatedBundles_...`) is the suite's longest test (52 to 55 s of its 63 s at T0). It already shares the
  parsed lexicon (`LexiconCache`), hard-links the speech clips and runs four threads; what is left is the oracle itself: every one of its 666
  iterations writes, hashes and fully loads a bundle copy. A cheaper equivalent with the same oracle needs the loader to read from an
  in-memory file source, which it cannot today (`ContentLoader.Load` takes a directory). That is a core API change: its own milestone (T6).

### T6: Follow-ups, judged on the numbers above, open
- **The order of the assemblies.** `dotnet test Vocaterra.sln -m:2` starts them in its own order; on the branch one lane ends after 62 s and
  the other after 86 s, of about 148 s of work. Starting Nlp.Tests before Golden.Tests and the fuzz suite, for example by testing two
  ordered lists of projects in the gate, would bring the run near 75 s. A gate-script change; measure it before adopting it.
- **An in-memory file source for `ContentLoader`** (core milestone): O15, `TempBundle` users and the CLI tests could then load damaged or
  edited copies without writing them to disk. O15 is most of the fuzz suite's 40 s, which sits on the shorter lane now.
- **A `LexiconCache` for the CLI entry points**, or the CLI tests on the light `TempBundle`: `CliTests`, `LintCommandTests` and
  `ProgramTests` parse the full lexicon on each call (about 52 s of test time at T0). A core change to the tools.
- **Don't keep speech WAV bytes in memory.** The core loader would read `speech/*.wav` lazily, or hash it as a stream without holding it.
  A core API change with its own milestone and ADR note. Less urgent now that each test process holds one bundle instead of up to 21.
- **The game's largest classes** bound Game.Core.Tests (see the end of "What made the suite slow"). Splitting `VillageTests`, `SceneTests`,
  `SaveMigrationTests` or `GuideTurnTests` into classes by topic would let them run in parallel; it is a readability call for the game.
- **The `Thread.Sleep` polling** in the turn-worker tests: not among the slow tests; leave unless a later measurement says otherwise.
- **CI:** a fast job beside the full one, or the test projects as a matrix. Both are edits of `.github/workflows/ci.yml`; the token has
  the workflow scope (2026-10-31).
- **xUnit v3 / Microsoft.Testing.Platform:** test-host start-up is about a second per assembly here, so a move is not justified by speed.

### T7: The game tests' memory, done (2026-11-06)
Game.Core.Tests peaked at 5.0 GB alone (2.8 GB at T1), so beside a second heavy assembly in `dotnet test -m:2` the 6 GB cap killed the run.
- **What held the memory.** Not a cache and not the number of seeds (the tests use one seed: 42): the sessions that start a turn worker. `StartWorker`
  runs a thread that holds the session, its scene (a 38 MB clone) and a language engine until the session is disposed. `ConversationSurfaceTests`
  built ten such sessions and never disposed them, and four more tests did the same (`SecondTryTests`, `CheckProgressTests`, Game.Tests'
  `SpokenFrontendTests` and `SessionLogTests`). A probe that forced a collection every 0.6 s and logged the running tests showed the live heap going from 250
  MB to 1.9 GB within two seconds, in steps of about 450 MB, one per `ConversationSurfaceTests` case, and staying there to the end of the run; with the
  sessions disposed the live heap never passed 0.8 GB. Garbage did the rest: the 2384 tests allocate 79 GB between them (about 38 MB each, the scene
  clone), and the collector lets the heap grow to 75 percent of the cgroup's cap (4.5 GB) before it collects hard, so the peak followed the leak.
- **What changed.** The five test classes dispose their sessions (`using var`; `ConversationSurfaceTests` is `IDisposable` and disposes the sessions its
  `Screen()` made), and `tests/Vocaterra.Game.Core.Tests/xunit.runner.json` caps the assembly at three test threads (copied to the output by the
  project): with the leak gone and ten threads the peak was still 4.0 to 4.5 GB, with three it is 2.4 to 2.5 GB and the wall time is the same (the long
  classes bound it). No test, assertion or budget changed; no production code.
- **Tried and left.** A GC heap hard limit (`DOTNET_GCHeapHardLimit`) lowers the peak but turns a live set above the limit into `OutOfMemoryException`s
  (3 GB failed three tests at four threads); `GCConserveMemory`, `GCgen0MaxBudget` and non-concurrent GC did not help. The change to `scripts/test.sh`
  (Game.Core.Tests alone, then the rest) was not needed.
- **Left.** Nlp.Tests (2.8 GB) and the fuzz suite (2.5 GB), the core's, are now the next largest; the full run peaks near 4.7 GB when they overlap
  Game.Core.Tests. They were not touched (core).

### T8: The nondeterministic `r30-no-classes` capture, done (2026-11-06, gated at `937f4a44`)

W05 and W07 found that two captures of `r30-no-classes` from one build differ in `declined.png`'s bytes, so `compare-runs.py` reports a
difference no change caused. T8 finds the source (a frame-timing race, an unseeded draw, the clock, the turn worker's timing, an
asynchronous voice or thinking indicator) and fixes it at the source, so that three captures of one build are identical; widening the
comparison's tolerance or excluding the picture is not a fix. A C# test pins the cause if it is in code. Haiku 5.5, on
`feature/bread-pilot`; game plan section 5 has the finding.

**Done:** the cause was the turn latency on the `--dev` note; a scripted run now gives the turn worker no clock
(`LaunchOptions.TurnClock`), so three captures are identical. Details in game plan section 5.

### T9: The jittering `r27-bread` capture, done (2026-11-08, gated at `ed1aa937`)

W02 and W03 found that `r27-bread`'s first capture is taken at tick 2460 on some builds and 2461 on others (W04 and V31 saw no
jitter). T9 finds the source and fixes it there, as T8 did, so that three captures of one build, and of two builds of the same
sources, are identical; widening the comparison is not a fix. A C# test pins the cause if it is in code. Haiku 5.5, on
`feature/bread-pilot`.

**Done:** the turn worker's outcome reached the next frame's collect or the one after at random; a scripted run now waits for its
turns at the end of each frame (`TurnWorker.WaitForOutcomes`), so twenty runs gave tick 2460. Details in game plan section 5.

### T10: Game.Core's managed heap since the water merge, done (2026-11-08, gated at `386815a6`)

Water's H08a (`e71af2a2`) capped the Game.Core test host's managed heap at 3 GB (`System.GC.HeapHardLimit` in
`tests/Vocaterra.Game.Core.Tests/runtimeconfig.template.json`), sized for water's tests. With the bread pilot's tests merged in
(`bc881c58`), a full run throws `OutOfMemoryException` now and then (`GuideTurnTests.TheCursorTileIsNamedWhenTheGuideIsAskedWhatThisIs`
in T9's own run and in M84's gate, 2 of 3 runs). T10 finds what holds the memory and cuts it at the source, as H08a's lazy scans did; the
cap stays (the owner, D-game-23 item 6) and the xUnit threads stay at 3. Sonnet 5.5 medium, on `feature/bread-pilot`.

**Done:** two test leaks held whole worlds: `TestSupport/RouteScene` cached a built scene (about 110 MB) per seed for the run (about
1.1 GB for ten seeds; now seed 42 and the last two others), and `TeacherLoopTests`' rig never stopped its turn worker (about 30 MB a test;
now disposed). Live heap peak 563 MB (was 1.67 GB), unforced 1.07 GB of the 3 GB cap (was 2.96 GB), the test host 2.1 GB (was 3.9 GB);
the gate's suite peak 4.5 GB (was 6.0). `RouteSceneCacheTests` pins the cache. Report `.claude/briefs/t10-report.md`.

### T11: B28, the long classes split and the threads raised, done (2026-11-17)

The full suite had grown from 7 to 42 minutes in two days, with the machine's load average at 12 to 13 from the other agents. xUnit runs the tests of one class one
after another, so the slow classes were split by theme (each keeps every test and assertion; the shared fields and helpers moved to an abstract `<Name>Base` class,
members `internal`, and the long tests moved to classes of their own so they run beside the others): `StreamedWorldHostTests` (13 tests, 38 min) into nine classes
(`...Settle`, `...Ring`, `...SaveEdit`, `...OtherSave`, `...Quit`, `...Dispose`, `...FarBuild` and the small ones; the tests that use a folder make their own GUID folder, so none
shares one), `FarViewTests` into six, `LodStoreTests`, `TerrainTuningTests`, `HeadwaterTests`, `ProceduralChunkSourceTests`, `DrainageNetworkTests`, `UndergroundWorldTests` and
Game.Tests' `ChunkResidencyTests` into two to four each. `Game.Core.Tests` runs on 6 threads (was 3; Game.Tests has no limit, the cores' count). `NamePlatesTests`
spent its 6 to 9 minutes in two game processes run one after the other (each starts the game, builds the world and waits 5 s); they now run at the same time (one async test).
About 35 classes of more than a minute are marked `[Trait(TestSpeed.Trait, TestSpeed.Slow)]` and so left out of `scripts/test.sh fast`.

| Run (load 8 to 13, other agents building) | Game.Core.Tests | Game.Tests | Nlp.Tests | Fuzzing | The run |
|---|---|---|---|---|---|
| Base, full | 34.3 m | 38.6 m | 286 s | 170 s | 42.3 m |
| After, full | 31.5 m | 19.5 m | 285 s | 250 s | 31.5 m |
| After, fast | 187 s | 32 s | 19 s | left out | 187 s (3.1 m) |

Counts: 12,568 before; after 12,567 passed and one failed, `GeneratedLexiconTests.LoadingTheBundleIsFastEnough` (a timing test of Nlp.Tests under load; it passed alone). Peak memory of the full run 5.5 GB.
The classes' tests are CPU-bound (the test projects build in Debug), so the gain is bounded by the cores the other agents leave: each long test now takes up to 18 minutes
of wall time because 16 test threads share the machine. The next levers are the Debug build of the world generation under test and the single tests over 5 minutes
(`ProceduralChunkSourceOrderTests`, `LodStoreRingEditRaceTests`, the host tests that settle a world), which only a cheaper test or a smaller sample can shorten.

### T12: B29a, Release tests, the gate's one slot, failed tests re-run alone, the timing tests apart (done, 2026-11-18)

The throughput review (`docs/research/2026-11-18-throughput-review.md`, items 1, 2, 3, 5 and 7a; the owner's decisions D-game-34 items 57 to 59) changed how the suite is built and gated. Nothing in the game or the core changed.

**Release against Debug** (the full suite with the fuzz suite, 12,788 tests, the shared models; one run each on 2026-11-18 at 12:3x to 12:5x UTC, load 0.6 to 9 from the preceding build; peak memory 5.6 GB in Release, 5.7 GB in Debug):

| Assembly | Debug | Release | Faster by |
|---|---|---|---|
| Game.Core.Tests | 26.4 m | 332 s (5.5 m) | 4.8 |
| Game.Tests | 17.6 m | 202 s | 5.2 |
| Nlp.Tests | 241 s | 199 s | 1.2 |
| Nlp.Fuzzing.Tests | 127 s | 119 s | 1.1 |
| WorldPreview.Tests | 53 s | 25 s | 2.1 |
| Nlp.Golden.Tests | 19.6 s | 11.3 s | 1.7 |
| Es.Tests, Host.Tests | 5 s, 3 s | 7 s, 3 s | 1 |
| **The run** | **26.4 m** | **532 s (8.9 m)** | **3.0** |

All the goldens held in Release: the 18 procedural goldens, `SceneTests.GoldenDigest`, the end-to-end goldens of `Nlp.Golden.Tests` (17 passed) and the save fixture; the passed and skipped counts are the same in both (12,788 and 9). The slowest classes in Release: `StreamedWorldHostFarBuildTests` 167 s, `OpenFallbackMeasurementTests` 167 s, `StreamedWorldHostSaveEditTests` 158 s, `StreamedWorldHostOtherSaveTests` 155 s, `LodStoreRingEditRaceTests` 139 s, `TerrainTuningCoastPoolsTests` 138 s, `TerrainTuningRiverLinesTests` 131 s, `LodRingTests` 123 s, `ProceduralChunkSourceOrderTests` 115 s, `FarViewChangedSectionTests` 114 s (no class is over 5 minutes; in Debug eleven are over 5). The core's `Nlp.Tests` gain least: its time is the speech models and the open fallback measurement, not C# in the project. No `#if DEBUG`, `Debug.Assert` or `[Conditional]` exists under `src/`, `tests/` or `tools/`, so no test needs a Debug build; the gate builds Release only (the analysers and `-warnaserror` are the same), and `scripts/content.sh` still builds the content tools it runs with `dotnet run`, in Debug, on demand. `VOCATERRA_CONFIG=Debug` makes `scripts/test.sh` and the gate use Debug.

**Used from now on:** `scripts/test.sh` (every mode), `scripts/merge-gate.sh` and CI's `build-and-test` build with `-c Release -warnaserror` and test with `-c Release --no-build`. CI keeps `CI=true`, which multiplies the CPU budgets by 4. **The CPU budgets were re-set to twice their Release cost** (CPU time, the fastest of three, the timing step's own conditions): `ProceduralChunkTimeTests` surface chunk 14 to 2.2 ms (cost 1.10), all-stone 13 to 2 (0.98), deep 13 to 2 (0.97), column of 24 41 to 6.2 (3.1); `StreamedWorldTimeTests` band 13 to 2.6 (1.25), made 23 to 3.7 (1.81), per chunk 6 to 0.9 (0.43), plan 2 to 0.8 (0.4); `PristineSnapshotTimeTests` 5,900 to 910 ms (451); `SceneBuildTimeTests` 7,300 to 1,450 (720); `ValleyUndergroundTimeTests` 5,000 to 1,060 (526); `GeneratedLexiconLoadTimeTests` 1,082 to 1,280 (592 to 639 alone: its load is cold in the timing step, 253 inside the full suite). The wall-clock asserts of `ProceduralTimingTests` (3,000 and 100 ms), `RegionTimeTests` (3,000 ms) and `ChunkIndexTests` (100 ms) are not CPU budgets and keep their values; B29b moves them to CPU time.

**The test modes** (counts on this tree, Release, models present): `fast` 12,001 passed and 6 skipped in 93 s (`Speed!=Slow`, no timing, no fuzz; 187 s before); `full` 12,631 passed and 9 skipped in 421 s (no timing, no fuzz); `timing` 16 tests in about 45 s (`Category=Timing`: the eight classes of Game.Core.Tests's `TimingCollection` and `GeneratedLexiconLoadTimeTests`); `ci` everything, 12,788 (full plus the 16 timing tests plus the 141 fuzz tests, plus 1 more test seen only in a whole run); `project <name>` one project without timing and fuzz (`--filter Category=Fuzz` or `Timing` runs a project's own on purpose). The traits are `Category=Timing` on the nine timing classes and `Category=Fuzz` on the fuzz assembly (`TestSpeed.Category`, `TestSpeed.Timing`, `TestSpeed.Fuzz`); `Speed=Slow` is unchanged.

**The gate's new shape** (`scripts/merge-gate.sh`): it re-runs itself once under `scripts/limited.sh`, so the whole gate is one command, one slot and one scope's caps (peak memory of a whole gate 6.5 GB, inside 10 GB); the inner `limited.sh` calls run directly and it never queues after it starts. Steps: restore and Release build with `-warnaserror`; the suite without fuzz and timing (`test-<stamp>.log`); the timing tests alone, after the suite (`timing-<stamp>.log`); format; the content gate; the grammar replay; the licence scans. A failed test is found from the TRX files (`.claude/gate-logs/trx-<stamp>/`, `scripts/gate-tests.py failed`), re-run alone in its project (`FullyQualifiedName=Class.Method`, a theory by method) and printed `FLAKY <name>` (passed alone) or `FAILED <name>`; the step passes when all are FLAKY, more than 20 failures fail it without a re-run, and the FLAKY names go to the summary and to `.claude/gate-logs/flaky-<stamp>.txt`. Tested on a throwaway branch with a test that failed once and a test and a theory that always failed (FLAKY, FAILED and FAILED; with only the flaky one the step passed). After the steps the gate prints each assembly's time and its slowest class's time next to the last gate's (`timing-<stamp>.json`, written each run), with a warning, never a failure, for 10 percent growth or a class over 5 minutes. The first gate after this change still runs the old script, because the gate runs the copy on the target branch before the merge; merge the branch first (or run the gate twice) to use the new one.

**The gate on the same tree** (a copy of `feature/meta_improvements` plus B29a, a quiet machine, load 7 from the previous gate's tail, 2026-11-18): the old script (Debug, one slot a step, the whole suite with timing tests and the fuzz suite) took 1,705 s (28.4 m) and failed on six timing tests whose budgets are now Release budgets, the old script run on the new tests in Debug (not a finding: that mixture is not used). The new gate took 779 s (13.0 m): build 41 s, the suite 10.0 m, the timing step 51 s, format, the content gate, grammar and licence scans about 1.7 m; 12,631 and 16 tests passed, no flaky test. The review expected a test phase of 6 to 10 minutes; here it is 10.9 m with the timing step (Game.Core.Tests 420 s, Nlp.Tests 343 s, Game.Tests 233 s: the Nlp.Tests assembly is now the second lane, and its OpenFallbackMeasurementTests take 167 to 279 s).

## Files touched
- **New:** `scripts/test-timings.py`, `scripts/test.sh`, `tests/Shared/TestSpeed.cs`, `tests/Vocaterra.Game.Core.Tests/TestSupport/TestSpeed.cs`
- **Production (game):** `src/Vocaterra.Game.Core/Voxel/VoxelGrid.cs`, `VoxelChunk.cs` and `World/Scene.cs` (`Clone`)
- **Test helpers:** `TestSupport/TestGame.cs` and `RouteScene.cs`, `Voxel/VoxelTestSupport.cs` (Game.Core.Tests; `TestGame.cs` and
  `TestSpeed.cs` newly linked into Game.Tests), `tests/Shared/GameBundleLocator.cs`, `tests/Vocaterra.Nlp.Tests/Asr/TestSupport.cs`,
  `Content/TestSupport.cs`, `Pipeline/TestSupport.cs`, the Golden.Tests loaders
- **Call sites:** the 34 game test classes and five calls that dropped their own load, the seven direct scene builds, and the classes and
  methods marked slow
- **Docs:** this plan, `docs/nlp/building-and-testing.md`, `docs/game/building-and-testing.md`, `scripts/README.md`, `.claude/agents/orchestrator.md`, and the
  findings logs of both plans (`docs/nlp/findings.md` and `docs/game/findings.md`), each for its half

## Verification
Every phase:
1. `scripts/limited.sh dotnet build Vocaterra.sln -warnaserror -m:4`
2. `scripts/limited.sh dotnet test Vocaterra.sln --no-build -m:2 --logger trx --results-directory <dir>`, then
   `python3 -I scripts/test-timings.py <dir> <baseline>`: the passed and skipped counts must match the baseline, or rise only by the tests
   the phase adds, and the wall time must go down.
3. `dotnet format Vocaterra.sln --verify-no-changes`, then the merge gate (`GATE_TARGET=<branch> scripts/merge-gate.sh ...`), with
   `VOCATERRA_MODELS_DIR` set so the model tests run.

Phase-specific checks, as run:
- **T1:** the two clone tests pass and the seed-42 golden (`8048644059674b5335ef2c32e230defbbe8402a32f72f7ee10324a36fc80cb47`) is unchanged.
- **T3:** with models present the model-gated tests ran (3 skipped, the same as at T0).
- **T4:** `scripts/test.sh fast` ran in 26 s against 87 s for the full suite (46 s before the warmed scene), and named every test it left out.
