# Where the development time goes (2026-11-17)

*A read-only study by an Opus 5.5 agent for the owner ("a meta-analysis of how we can make developing the game with AI quicker"), saved by the orchestrator. Its analysis scripts are outside git in `artifacts/tmp/meta/scripts/`. The October study it compares with is [2026-10-31-development-bottlenecks.md](2026-10-31-development-bottlenecks.md).*

The owner asked what is slowing down development of the game with AI agents, and how to make it faster. This is a read-only study: nothing was built or run. It covers the B series on `feature/biomes` from 2026-11-15 14:14 to 2026-11-17 16:45 UTC, which is 39 merged milestones, 57 agents and 51 gate runs. All times are UTC. A number marked "(est.)" is an estimate. Every other number was measured, and the method section at the end says how.

## 1. Summary

- **The tests are the bottleneck.** The full suite took 7 minutes on 2026-11-15 and 50 to 53 minutes in the last three gates. On 2026-11-17 alone, B07f and B02c each added about 15 minutes to every full run, and nothing in the process noticed. Each merged milestone pays for about four full runs: about 2.8 started by its agent and 1.3 by the gate.
- **Agents mostly wait.** Only 16% of an agent's active time is the model thinking and writing (it was 58% in October's study). Tests take at least 33%, game runs and measurement batches at least 26%, builds about 5%, and waits whose kind could not be identified about 20%.
- **The slots fill up while the machine is half idle.** A full run holds a limited.sh slot for 50 minutes but uses only 5 to 6 cores and about 5 GB. Its length is set by two things: one test class whose 13 tests run one after another (45 minutes), and Game.Core.Tests' limit of three threads. On 2026-11-17, commands queued for a slot for at least 8.5 hours within 16.7 hours. At 16:43 a build had waited 14 minutes for a slot while the load was 10 and 19 GB of memory was free.
- **Rework.** 20% of gate time went into runs that were thrown away. 6 of the 13 gates on 2026-11-17 failed, five of them on wall-clock timing tests that passed when run alone. 16 of the 48 building agents met merge conflicts, mostly in the same dozen files.
- **Lead time quadrupled.** The median time from launch to merge was about 1 hour on 2026-11-15 and 4.2 hours on 2026-11-17. Merges held at 12 to 14 a day (a pace of 19 a day on 2026-11-17 with five agents). The gate checkout is now busy about 60% of the time, which caps throughput at about 20 merges a day.

**Do these first** (section 5): make the suite fast; run one full suite per milestone; give the gate its own slot and size the slots by need; set rules for agents (no polling, short game runs); remove the files that conflict most. Expected result (est.): 13 to 19 merges a day becomes 20 to 25, and the median launch-to-merge time falls from about 4 hours to about 2.

## 2. Where the time goes

### 2.1 A typical milestone on 2026-11-17

| Phase | Typical | Range and examples |
| --- | --- | --- |
| The agent thinking and writing (model time) | 0.8 h | 0.1 to 2.0 h |
| The agent waiting on tests (filtered runs, one project, full runs) | 1.5 to 2 h | B24: 1.8 h of full runs and 1.6 h of filtered runs; B21: two full runs because it merged `feature/biomes` twice |
| The agent waiting on game runs, pictures and measurement batches | 0 to 2 h | B17b 1.9 h, B07d 2.2 to 3.0 h, B25 2.3 h |
| The agent waiting on builds | 0.3 h | 0.1 to 1.5 h |
| ... of the waiting above, queued for a slot | about 0.5 h | at least 18% of all heavy-command time on 2026-11-17 |
| Waiting for the orchestrator (merge requests, the gate queue, the owner) | 0.5 h | up to 2.7 h (B24 queued behind the B17b, B02c and M99 gates) |
| The gate | 0.6 to 0.9 h | the B22 gate took 94 min, 70 of them waiting for slots |
| Rework after a failed gate or a merge conflict | about 0.5 h on average | 0 to 6 h |
| **Launch to merge (median of the 13 merges on 2026-11-17)** | **4.2 h** | 1.1 h (M99) to 22 h (B02c, five rounds) |

The owner's two examples:

- **B07d.** Launched at 10:19 and still open at 16:55. Model time was 0.9 h. The rest was waiting: measurement batches 3.0 h, filtered tests 2.5 h and one full run 0.7 h. It was then sent back to merge B24 (conflicts in `JobPool.cs`, `QualityPreset.cs` and `architecture.md`) and to run the full suite again.
- **B02c.** It took 22 hours: model time 2.0 h, 12 h of polling while it waited, 4.8 h sitting finished between rounds, and two gates.

### 2.2 The agents' hours

The 57 agents were active for 142.9 hours, not counting time spent finished and waiting for the orchestrator:

| Activity | Hours | Share |
| --- | --- | --- |
| Model (thinking, writing, tool calls) | 23.1 | 16% |
| Waiting on tests (polling for them, or in the foreground) | 47 or more | 33% or more |
| Waiting on game runs, pictures and measurement batches | 37 or more | 26% or more |
| Builds, content and other heavy commands in the foreground | 7 | 5% |
| Unattributed waits (background commands a poll did not name) and light commands | 29 | 20% |

- **Polling costs tokens.** 2,453 of 17,757 agent turns (14%) did nothing but `sleep` and check on a file. They used 1.05 billion of the 6.3 billion context tokens (17%).
- **Most polls end up orphaned.** The Claude Code harness moves a foreground command into the background after 120 to 180 seconds. It did that 831 times, and 622 of those commands were polls or wait loops. That is where the roughly 40 orphaned `until grep ...; do sleep` loops came from.
- **Some runs are invisible to the harness.** Some agents start runs with `nohup ... &`, so the harness cannot tell them when the run ends.
- **The orchestrator is not the constraint.** Its model time was 3.7 hours over the 50 hours, and it usually started a gate within minutes of an agent's report.

### 2.3 The gate

**Phases of the B24 gate (15:30 to 16:22):** build 1.2 min, tests 50.1, format 0.8, content 0.3, grammar 0.05, licence 0.1, so 52.4 minutes in all. Tests are 96% of the gate.

| Day | Gates | Median gate | Median test phase | Game.Tests | Game.Core.Tests |
| --- | --- | --- | --- | --- | --- |
| 2026-11-15 | 17 | 9 min | 7 min | 0.2 to 0.6 min | 2 to 3 min |
| 2026-11-16 | 20 | 20 min | 16 min | 0.4 to 23 min | 3 to 23 min |
| 2026-11-17 | 14 | 38 min | 31 min | 15 to 49 min | 17 to 45 min |

- **Each assembly in the B24 gate:** Game.Core.Tests 43.1 min, Game.Tests 41.7, Nlp.Tests 4.8, Nlp.Fuzzing.Tests 2.1, WorldPreview 1.2, Golden 0.35, Host 0.1, Es 0.1. With `-m:2` they run in two lanes of about 47 minutes each.
- **B07f (merged 09:43):** took Game.Tests from 15 to 32 minutes. It added five `StreamedWorldHostTests`, and four of them take 7 to 12 minutes each.
- **B02c (merged 13:28):** made chunk generation 2.5 to 3 times dearer (B24 measured this on CPU time). Game.Core.Tests went from 18 to 26 minutes up to about 43, and Game.Tests from 30 to 46. Heavier load accounts for part of the rise.
- **All gate runs:** 51 runs gave 39 merges (1.3 runs a merge) and took 18.7 hours in all.
  - 3.7 hours (20%) went into runs that failed and were run again, or were killed.
  - 2.8 hours went into waiting for a slot outside the test phase. The B22 gate's build waited 32 minutes and its content step 38; the second B07e gate's format step waited 35.

### 2.4 The ten slowest test classes

The figures come from the TRX files of B14's full run at 14:38, which ran on what was then the `feature/biomes` code, at load 15 to 25.

- **Summed time** is the sum of a class's test times. Because xUnit 2 runs one class's tests one after another, it is also the class's wall time.
- **Before** is the same class in B14's 08:47 run, before B07f and B02c had merged.

| Class (assembly) | Tests | Summed time | Before | Slowest test | Marked Slow? |
| --- | --- | --- | --- | --- | --- |
| StreamedWorldHostTests (Game) | 13 | 45.1 min | 4.9 min (8 tests) | 721 s | no |
| FarViewTests (Game) | 21 | 33.0 min | 15.2 min | 324 s | no |
| TerrainTuningTests (Core) | 30 | 23.3 min | 12.3 min | 164 s | yes |
| LodStoreTests (Core) | 23 | 11.9 min | under 1 min | 423 s | no |
| NamePlatesTests (Game; starts a game process) | 9 | 9.0 min | 8.1 min | 543 s | no |
| ProceduralChunkSourceTests (Core) | 12 | 7.5 min | 1.1 min | 123 s | no |
| UndergroundWorldTests (Core) | 25 | 7.2 min | 2.4 min | 170 s | no |
| ProceduralWaterTests (Core) | 28 | 6.8 min | 1.1 min | 202 s | no |
| HeadwaterTests (Core) | 7 | 6.5 min | 3.5 min | 93 s | no |
| ChunkResidencyTests (Game) | 14 | 5.7 min | 2.1 min | 126 s | no |

- **Next slowest:** RegionBlendTests 4.2 min, DrainageNetworkTests 4.1, OpenFallbackMeasurementTests 3.8 (core, marked Slow), ProceduralWaterSeedsTests 3.4 and FarTileMesherTests 3.2.
- **A few tests hold most of the time.** Of 12,650 tests, the slowest 50 hold 64% of the test time and the slowest 100 hold 79%.
- **The "fast" tier is not fast.** Only one of the ten classes is marked Slow, so `scripts/test.sh fast` still includes 98% of Game.Tests' time and 70% of Game.Core.Tests'. It would take about 45 minutes, and agents used it 10 times in two days.

### 2.5 The machine: slots, cores and memory

- **Memory is not the constraint.**
  - Across 1,028 limited.sh reports, the peak memory of a heavy command was 0.5 GB at the median, 2.4 GB at the 90th percentile and 7.8 GB at most.
  - Full suites peaked at 4.1 to 6.0 GB (one at 7.8 GB), and builds at 1.3 to 1.5 GB.
  - In the 132 preflight waits found in the kept logs, free memory averaged 20.5 GB (the lowest was 9.8 GB) while the load averaged 19.
- **Full runs leave cores unused.**
  - During a full run, the Game.Core.Tests host used 2.8 cores and the Game.Tests host 1.9.
  - Game.Core.Tests was limited to three threads in T7 (2026-11-06), when the cap for one command was 6 GB. The cap is 10 GB now.
- **The slots are busy most of the day.**
  - On 2026-11-17 an average of 2.8 heavy commands were in flight, the three slots were busy 77% of the time, and more than three commands wanted a slot 35% of the time.
  - That adds up to at least 8.5 command-hours queued for a slot (about 12 hours a day). This is a lower bound, because runs started with `nohup` are invisible in the transcripts.
- **Spot check at 16:43.**
  - The load was 10.1, CPU pressure was about zero and 19 GB was free.
  - All three slots were held: the B26 gate's two test hosts, one game run and one filtered test.
  - A Release build had been queued for 14 minutes.

Most of the time, then, the number of slots is the limit, not CPU or memory. CPU only runs short (load 17 to 27) when several CPU-heavy commands happen to run at once.

### 2.6 Rework

**Why gates failed.** Of the 51 gate runs, 17 failed or were stopped; a run can have more than one cause:

| Cause | Runs |
| --- | --- |
| Wall-clock timing, or a slow test under load (passes alone) | 7 |
| A fuzz seed finding an older core bug | 4 |
| Killed because the owner held the work or paused it | 4 |
| A merge conflict | 3 |
| A real clash between parallel milestones (B11b against B12's cave tour; B02c against B21's zero-allocation test) | 2 |
| Infrastructure (a test host killed; a dirty checkout) | 2 |

**Conflicts.** 16 of the 48 building agents met conflicts in their 34 merges of `feature/biomes`. Each conflict costs a merge, a re-hash when content is involved, and another full run (about an hour at today's suite). The same few files keep being changed:

| File | Changed by how many of the 39 merges |
| --- | --- |
| `src/Vocaterra.Game.Core/README.md` | 18 |
| `VoxelRenderer.cs` | 12 |
| `docs/game/running.md` | 12 |
| `save-v9.json` | 11 |
| `manifest.json` | 11 |
| `SceneDrawer.cs` | 10 |
| `VoxelGrid.cs` | 10 |
| `NoLiteralTextTests.cs` | 9 |
| `src/Vocaterra.Game/README.md` | 9 |
| `architecture.md` | 8 |
| `VoxelGame.cs` | 8 |
| `LaunchOptions.cs` | 7 |
| `QualityPreset.cs` | 6 |

## 3. Bottlenecks ranked by hours lost a day

The figures are at the 2026-11-17 rate: five agents, about 90 agent-hours a day, and the gate checkout busy about 14 hours a day.

| # | Bottleneck | Hours lost a day | Evidence |
| --- | --- | --- | --- |
| 1 | Long full suite, run about four times per milestone | About 30 to 35 agent-hours waiting on tests, 20 to 25 of them avoidable; about 9 hours of serial gate time beyond a 15-minute suite | The suite went from 7 to 50 min; about 4 full runs per merge; at least 33% of agent time |
| 2 | Slots held by long test runs that use few cores | At least 12 command-hours queued (about 10 agent-hours), plus 1.3 hours of the gate's time | Demand model; spot check; the gate's waits |
| 3 | Game runs and measurement batches | About 23 agent-hours, of which 8 to 10 avoidable (est.) | A cold far view takes 5 to 13 min to fill after the first teleport (B14); 10-min stalls (B25); 571-second tours; `gl-compare` allows 40 min |
| 4 | Rework from failed gates and conflicts | About 2 gate-hours and 8 to 10 hours of lead time | 20% of gate time thrown away; 6 of 13 gates failed on 2026-11-17; 16 agents met conflicts |
| 5 | Polling and orphaned waits | About 1.5 model-hours and 17% of agent tokens | 2,453 polling turns; 622 orphaned loops |
| 6 | Owner holds and review rounds | Not machine time, but long tails | B11b 11 h, B02b 9 h, B07c 8 h |

## 4. Recommendations ranked by hours saved a day

The savings are estimates against today. Items 1 to 3 overlap, so their savings do not simply add up.

| # | Change | Saves a day (est.) | Cost | Risk | Owner? |
| --- | --- | --- | --- | --- | --- |
| 1 | **A faster suite.** Split StreamedWorldHostTests, FarViewTests, LodStoreTests and ChunkResidencyTests into several classes each, so that xUnit runs them in parallel. Raise Game.Core.Tests from 3 to 6 threads after measuring peak memory. Launch B27 (generation's 2.5 to 3 times cost) first. Find out why NamePlatesTests takes 9 minutes. The suite would drop from 50 to about 20 to 25 minutes. | 12 to 18 agent-hours and 4 to 5 gate-hours | S to M: one Sonnet milestone at medium effort, plus B27 | Low. No test is removed. More parallelism can expose races between tests, which is worth finding. A run's peak memory rises, so keep it under 10 GB. | No |
| 2 | **Release test runs** (`-c Release` in the gate, `test.sh` and CI). Expected 1.5 to 3 times faster on the generation, meshing and far-view tests. Measure it once first. | A further 8 to 12 agent-hours and 3 to 4 gate-hours | S | Low. `src` has no `Debug.Assert` or `#if DEBUG`, and the generator bans floats, so the goldens should not move; check them. The CPU budgets of the timing tests must be re-set from Release times, or they lose their power. Each worktree needs one Release build. CI would gain too: its runner is about four times slower than this machine. | Yes (changes how the gate builds) |
| 3 | **One full run per milestone.** Mark the 20 or so slowest classes Slow, so that `test.sh fast` takes a few minutes. Agents iterate with the fast tier plus the classes they touch. They run one full suite after their last merge of `feature/biomes`, and report only once it has finished. | 10 to 15 slot-hours now, and 1 to 1.5 hours less lead time per milestone | S (traits and the brief template) | Medium. More failures reach the gate, and each costs another gate run. The agent's one full run on the merged code keeps that rare. | No |
| 4 | **A new slot policy.** Reserve one slot for the gate. Count a full suite as two of five tokens and every other command as one. Base the preflight wait on CPU pressure (`/proc/pressure/cpu`) and free memory instead of a load over 16. Keep the slice's limit of 14 cores; raise its memory limit to 20 GB only if three suites can coincide. | 8 to 10 agent-hours and 1.3 gate-hours | S (one script) | The slice's unchanged limits keep the desktop safe, because a kill happens inside the slice. Badly sized tokens could get a run killed for memory. | Yes (machine limits) |
| 5 | **Cheaper game runs.** Use a pre-generated far store for each seed (B07d's `--pregen`), copied for each scripted run. Pass `--far-view off` unless the milestone is about the far view. Always use Release builds. Put `timeout 600` on each run. Fix the B25 stall first. Use one batch script per milestone. | 6 to 10 agent-hours | S to M | A warm store can hide bugs in how the far view fills, so far-view milestones keep one cold run. | No |
| 6 | **Remove the hot spots.** The orchestrator writes the README, `running.md` and `architecture.md` notes when it records a milestone, so agents stop editing them. The save fixture checks the bundle hash against `manifest.json` instead of pinning it. A re-hash script replaces hand-merging `manifest.json`. Milestones that touch `QualityPreset`, `LaunchOptions` or the settings page, `JobPool` or `VoxelRenderer` run one after another, or the settings page gets a registry of rows. | 4 to 6 agent-hours, and fewer gates lost to conflicts | S to M | Low | No |
| 7 | **A different gate.** (a) Gate two ready milestones in one run: `merge-gate.sh` already takes several branches, so split them only on a failure. (b) When a branch already contains the target's head, merging changes nothing. If the agent's full run left a stamp for exactly that tree, the gate runs only build, format, content and licence (about 3 minutes). | 3 to 7 gate-hours; lifts the ceiling of about 20 merges a day | S | (a) No loss of protection. (b) Relies on a run made in the agent's worktree. Protection stays the same if `test.sh` stamps the tree hash and the test counts and the gate checks them. | (b) yes |
| 8 | **Agent rules.** Run long commands with `run_in_background: true`, end the turn and wait for the notification. No `sleep` or `until` polls and no `nohup ... &`. One heavy command at a time. | About 1.5 model-hours, 17% of agent tokens, and no orphaned loops | S | None | No |
| 9 | **Timing tests.** Finish moving them to CPU-time budgets (M100's lexicon test, `PristineChunksTests`, NamePlates' exit 139 under load) and keep them in the serial timing collection. | 1 to 2 hours | S (partly planned) | None, as long as each budget stays at twice the measured cost | No |
| 10 | **A test-time report in the gate.** Print each assembly's time and the largest class's time against the base. Warn when the suite grows by more than 10% or a class passes 5 minutes. | Stops the next +15 minutes from going unnoticed | S | None (it only adds a check) | Yes (adds to the gate) |

**Considered, and not recommended now:**

- **Gate only the affected assemblies, with a full run every night.** Every game milestone touches Game.Core, whose tests are the slow half, so little would be saved. Content edits also move core tests (R02).
- **Gates in parallel worktrees.** Two gates on the same base can each pass and still break together. Batching (7a) gets most of the gain safely.
- **A build cache.** A gate build takes about a minute, and builds are about 5% of agent time.
- **Splitting Game.Tests into two assemblies.** Classes already run in parallel, so it is the class split in item 1 that shortens the run.
- **More agents.** While the slots and the gate are saturated, a sixth agent adds waiting, not merges. Keep five until items 1 to 4 have landed.

## 5. Do these first

1. **A test-speed milestone** (Sonnet, medium effort): item 1, the Slow marks from item 3, and the report from item 10. Launch B27 beside it.
2. **Measure Release once.** If it is at least 1.5 times faster and the goldens are identical, ask the owner to switch the gate, `test.sh` and CI to Release (item 2).
3. **Rules in the brief template and `orchestrator.md`** (items 3, 5 and 8): one full run after the last merge; run in the background and wait for the notification; game runs kept short, with the far view off or a pre-generated store, in Release.
4. **limited.sh** (item 4): a slot for the gate and tokens sized to each command. This needs the owner.
5. **Hot spots** (item 6): the orchestrator writes the shared docs when it records; the fixture's hash is no longer pinned.

| | Now (2026-11-17) | After items 1 to 5 (est.) |
| --- | --- | --- |
| Full suite | 50 min | 15 to 20 min |
| Gate | 52 to 55 min | 18 to 23 min |
| Full runs per merged milestone | about 4 | about 2 |
| Share of agent time on tests | 33 to 40% | about 15% |
| Time queued for a slot | at least 12 command-hours a day | 2 to 3 |
| Median launch to merge | 4.2 h | 2 to 2.5 h |
| Merges a day (five agents) | 13 to 19 | 20 to 25 |

**Why the "after" column is plausible:** on 2026-11-15, with a 7-minute suite, the same process merged 14 milestones in 9.5 hours (though they were smaller milestones).

## Method and caveats

**Sources:**

- **Gate logs.** The 51 gate runs in `.claude/gate-logs/`. Phase times come from each log's creation and last-write times, which btrfs records; assembly times come from the `dotnet test` summaries.
- **Per-test durations.** Three TRX sets from 2026-11-17: B14's runs at 08:47 and 14:38, and B07d's at 15:44.
- **Transcripts.** The 57 subagent transcripts and the orchestrator's, read for tool-call times, background-task notifications, polls and token use.
- **Timeline.** `git log` of `feature/biomes`, for launches, merges and records.
- **limited.sh output.** Its peak-memory and preflight lines in the kept logs.
- **Machine state.** A check at 16:43.

**Caveats:**

- **Load inflates the class times.** The 14:38 run ran at load 15 to 25, so its class times are higher than on a quiet machine (the 08:47 run gives the comparison).
- **Detached runs are hidden.** Runs started with `nohup` do not appear in the transcripts. So the slot-queue hours are a lower bound, and part of the 20% "unattributed" share is really tests and game runs.
- **The Release speed-up is an estimate.** No Release test run exists yet, so it needs the one measurement in step 2.

**Scripts:** the analysis scripts are in `artifacts/tmp/meta/scripts/`: `gates.py`, `trx.py`, `trx2.py`, `agents.py`, `partition.py`, `pollcat.py`, `polls.py`, `demand.py`, `orch.py` and `bgdur.py`.
