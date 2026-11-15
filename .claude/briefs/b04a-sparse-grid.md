You are implementing B04a, "The sparse VoxelGrid in both modes", for the Vocaterra repository (C#, .NET 10). The language core is the
engine Vocaterra.Nlp, the Spanish pack Vocaterra.Nlp.Es and the Vocaterra.Nlp.* test and tool projects; the game is Vocaterra.Game.Core
(the engine-free logic), Vocaterra.Game (MonoGame), Vocaterra.Host and tools/Vocaterra.SpeechRender. This milestone's work is in the
game, in Vocaterra.Game.Core and its tests.
Where to work: the worktree /home/james/Dev/Vocaterra/.claude/worktrees/b04a, already on the local branch `B04a-sparse-grid` (cut from
`feature/biomes` at 55e26c41). Run every command from that directory. Commit after each item that builds and passes its tests, with the
prefix `B04a: `. Do not push your branch: it stays local and the gate merges it from there (owner, 2026-11-05). Never push or merge
feature/biomes or main. When you finish, leave the worktree on your branch, clean.
Resources (the machine has crashed from overload): run every dotnet restore, build, test, format and run, every scripts/content.sh call,
speech prerender and screenshot run through scripts/limited.sh (for example `scripts/limited.sh dotnet build Vocaterra.sln -warnaserror
-m:4`; tests with `-m:2`). scripts/test.sh, scripts/content-loop.sh and scripts/scripted-runs.sh call it themselves: run them directly.
Never start two heavy commands at once (no `&`, no background builds, no parallel test runs), and write no shell wait loop on `pgrep -f`
(it matches its own command line and never ends). Another agent (B01) builds in its own worktree at the same time; limited.sh queues you
both. While iterating run `scripts/test.sh fast` or `scripts/test.sh project tests/Vocaterra.Game.Core.Tests`; run `scripts/test.sh full`
(the whole suite, as the gate) once before reporting. Exit 137 means the 6 GB cap killed it: lower the parallelism, do not work around the
cap.
Environment: prefix every command inline with PATH="$HOME/.dotnet:$PATH" DOTNET_ROOT="$HOME/.dotnet" DOTNET_NOLOGO=1 (the sandbox may
reject a bare `export`, and it refuses a command line containing the shell builtin words `eval` or `hash`). Model-gated tests and the
scripted runs need VOCATERRA_MODELS_DIR=$HOME/.local/share/vocaterra/models; the scripted runs also need DISPLAY=:0.

Read first: CLAUDE.md; docs/game/implementation-plan.md sections 1, 3, 4.14 (your row) and 6; docs/game/biomes-plan.md sections 2 ("What
exists today": the storage, the edge, the dense index, the whole-world scans), 3.2 (the `Voxel/` row and the seams), 3.7 (storage,
generate on read, residency: your design) and 7 (risks 6 and 11); docs/process/code-guidelines.md; docs/game/architecture.md;
docs/game/scripted-runs.md ("How to run them" and "Comparing"). The code: src/Vocaterra.Game.Core/Voxel/ (VoxelGrid.cs, VoxelChunk.cs,
VoxelChunkCoord.cs, BlockRay.cs) and the users of the dense members (`Chunks`, `ChunksX`/`ChunksY`/`ChunksZ`, `BlocksOfKind`,
`ComputeDigest`): in Core, Activities/ProcedureRunner.cs, Content/GameDataValidator.cs, Land/LandRules.cs, Needs/FireRings.cs,
Npc/WorldFacts.cs, Save/*.cs, Session/VoxelSession.cs, Simulation/VoxelSimulation.cs; in Game, Graphics/Weather/ColumnCover.cs,
Graphics/World/ChunkVersionTracker.cs, ChunkViewSet.cs, VoxelRenderer.cs and VoxelGame.cs. Grep for the members that assume y >= 0
(`GroundFinder`, `HighestBlock`, `BlockRay`, `TeleportTo`). Read the sections named, never whole long files.

Rules: a game milestone edits only the game projects, their tests, content/game-v1, assets/ and docs/game/ and takes the core as it is
(a missing core call stops that part: report it, do not copy core source or edit a Vocaterra.Nlp project or docs/nlp/). A regression
check is a C# test, never a script with text baselines. Never create or edit anything under .github/workflows/; do not edit either
implementation-plan.md, either decisions.md or docs/game/biomes-plan.md (the orchestrator does); engine code only in src/Vocaterra.Game
(D-game-1); no System.Random and no clock in the simulation. Tests are Given_When_Then in folders that mirror the source.
Do not overlap B01, which runs at the same time: do not edit content/, src/Vocaterra.Game.Core/Content/ (GameData, GameDataValidator:
allow-list their dense uses instead), src/Vocaterra.Game.Core/World/ or the save fixtures. Do not change the save format or the state
digest (B05), the Game project's consumers (B04b: touch src/Vocaterra.Game only where an API change forces a compile fix, and keep its
pictures identical), or add chunk generation, `IChunkSource` or streaming policy (B03, B06).

The outcome is **byte-identical**: every golden, state digest, save and scripted run stays exactly as it is.

Deliverables:
1. `VoxelGrid` keeps its name and API but stores chunks sparsely, keyed by the existing `VoxelChunkCoord` (use it; do not add a second
   coordinate type such as `ChunkCoord3`), with a cache of the last chunk used on the hot read path, and all-air chunks never allocated.
   **Bounded mode** is today's constructor (SceneBuilder and the tests keep it). **Streamed mode** takes the world's limits as a value (for
   example a `WorldLimits` record: x and z plus and minus 30,000, y -64 to 320); the values come from world.json later (B06), so take them
   as a parameter, never from content in this milestone.
2. Negative y: every member that assumed y >= 0 (`GroundFinder`, `HighestBlock`, `BlockRay`, `TeleportTo`, and Core code that loops from
   0) reads the grid's minimum y instead. Outside the limits reads stay solid, as today's edge wall.
3. Residency: an install API (installing a chunk is not an edit: it does not bump `EditCount` and does not wake `FlowSimulation`), a
   separate `ResidencyVersion`, an `IChunkResidencyListener` for installs and evictions, eviction of unchanged chunks only, and a hook
   for generate-on-read that B06 will fill (a delegate or a small interface owned by `Voxel/`, so `Voxel/` never depends on `World/`).
   With no hook a missing chunk in streamed mode reads as air.
4. `PristineDigest`: each chunk records its digest lazily, just before its first write, so a chunk whose edits are undone counts as
   unchanged (B05 builds the changed-chunk digest and saves on it).
5. `IVoxelReader` (the read members: blocks, water, solidity, `HighestBlock`, the limits) and an internal `IVoxelWriter`; Core consumers
   that only read take `IVoxelReader`. Keep the change proportionate: convert the obvious readers, list the rest in the report.
6. Core consumers keyed by coordinate: `WorldFacts` (the copied dense index near line 131) and any other Core user that can move cheaply.
   The dense members stay valid only on bounded grids. An architecture test allow-lists every remaining use of them in `src/`, each with
   a one-line reason naming the milestone that removes it (the save files B05, `FireRings` B06, the Game consumers B04b).
7. Tests: sparse and bounded grids agree block for block on the valley; installs do not move `EditCount`; only unchanged chunks are
   evicted; an undone edit counts as unchanged; negative y and coordinates near plus and minus 29,000 in x and z and at y -60 and 310
   read and write correctly; the allow-list test.

Checks beyond the suite: capture the scripted runs before your first change (`scripts/scripted-runs.sh <out-before>`) and after
(`<out-after>`), with the same work folder, and `python3 -I scripts/compare-runs.py <out-before> <out-after>` must report no difference.
Report the scene build time (`SceneBuildTimeTests`) and the grid's memory for the valley before and after.

Gate (from the worktree root): scripts/limited.sh dotnet build Vocaterra.sln -warnaserror -m:4 (0 warnings); scripts/test.sh full
(11,498 passed before your change, plus your new tests); scripts/limited.sh dotnet format Vocaterra.sln --verify-no-changes clean;
scripts/limited.sh scripts/content.sh validate passes; `SceneTests.GoldenDigest` and every golden unchanged. Do not commit bin/, obj/ or
artifacts/ (keep the scripted-run captures outside the repository, for example in /tmp).
Commit messages end with
Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>
Claude-Session: https://claude.ai/code/session_01F9jKDbkPTqABsu7sLVD9h6

Report, one page: branch, commit SHA, worktree path; what was built (the storage, the modes, the residency API, the generate-on-read
hook's shape, `IVoxelReader`); test counts; the scripted-run comparison's result; the build time and memory before and after; the
allow-list's entries; bugs found and fixed (where, symptom, fix, test); known-not-fixed with reasoning; files touched outside the
milestone's area; anything left undone and why. Leave out file dumps and long logs.
