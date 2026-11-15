You are implementing B04b, "The game's consumers keyed by coordinate", for the Vocaterra repository (C#, .NET 10). The language core is
the engine Vocaterra.Nlp, the Spanish pack Vocaterra.Nlp.Es and the Vocaterra.Nlp.* test and tool projects; the game is
Vocaterra.Game.Core (the engine-free logic), Vocaterra.Game (MonoGame), Vocaterra.Host and tools/Vocaterra.SpeechRender. This milestone's
work is in the game, in src/Vocaterra.Game and its tests.
Where to work: the worktree /home/james/Dev/Vocaterra/.claude/worktrees/b04b, already on the local branch `B04b-game-consumers` (cut from
`feature/biomes` at @@BASE@@, which holds B01 and B04a). Run every command from that directory. Commit after each item that builds and
passes its tests, with the prefix `B04b: `. Do not push your branch: it stays local and the gate merges it from there. Never push or merge
feature/biomes or main. When you finish, leave the worktree on your branch, clean, and remove any extra worktree you made (B04a made one
under /tmp for its baseline; if you do the same, remove it with `git worktree remove --force` before reporting).
Resources (the machine has crashed from overload): run every dotnet restore, build, test, format and run, and every scripted run through
scripts/limited.sh (for example `scripts/limited.sh dotnet build Vocaterra.sln -warnaserror -m:4`; tests with `-m:2`).
scripts/test.sh and scripts/scripted-runs.sh call it themselves: run them directly. Never start two heavy commands at once (no `&`, no
background builds, no parallel test runs), and write no shell wait loop on `pgrep -f`. Other agents build in their own worktrees at the
same time; limited.sh queues you all, so expect waits. While iterating run `scripts/test.sh fast` or
`scripts/test.sh project tests/Vocaterra.Game.Tests`; run `scripts/test.sh full` (the whole suite, as the gate) once at the end, after
your last change. Exit 137 means the 6 GB cap killed it: lower the parallelism, do not work around the cap.
Environment: prefix every command inline with PATH="$HOME/.dotnet:$PATH" DOTNET_ROOT="$HOME/.dotnet" DOTNET_NOLOGO=1 (the sandbox may
reject a bare `export`, and it refuses a command line containing the shell builtin words `eval` or `hash`). Model-gated tests and the
scripted runs need VOCATERRA_MODELS_DIR=$HOME/.local/share/vocaterra/models; the scripted runs also need DISPLAY=:0.

Read first: CLAUDE.md; docs/game/implementation-plan.md sections 1, 3, 4.14 (your row) and 6; docs/game/biomes-plan.md sections 2, 3.2
(the `Voxel/` row and the seams) and 3.7 ("The renderer", residency); docs/process/code-guidelines.md; docs/game/architecture.md (B04a's
note); docs/game/scripted-runs.md ("How to run them" and "Comparing"). B04a's work, which you build on: src/Vocaterra.Game.Core/Voxel/
(`VoxelGrid` sparse in both modes, `WorldLimits`, `IVoxelReader`, `IChunkResidencyListener`, `ResidencyVersion`, `TryGetChunk`,
`IsResident`, `PeekBlock`, `ResidentCoordinates`) and its allow-list test,
tests/Vocaterra.Game.Core.Tests/Architecture/DenseGridMembersAllowListTests.cs. The files to change (the allow-list's B04b entries):
src/Vocaterra.Game/Graphics/Weather/ColumnCover.cs, Graphics/World/ChunkVersionTracker.cs, ChunkViewSet.cs, VoxelRenderer.cs (including
`FindLights`, near line 195), and the mesher's assumptions in Graphics/World/ChunkMesher.cs (grep for "below the world" or y < 0).
Read the sections named, never whole long files.

Rules: a game milestone edits only the game projects, their tests, content/game-v1, assets/ and docs/game/ and takes the core as it is.
A regression check is a C# test, never a script with text baselines. Never create or edit anything under .github/workflows/; do not edit
either implementation-plan.md, either decisions.md or docs/game/biomes-plan.md (the orchestrator does); engine code only in
src/Vocaterra.Game (D-game-1); Core holds logic, Game the drawing. Tests are Given_When_Then in folders that mirror the source.
Do not overlap the other agents: do not edit src/Vocaterra.Game.Core/Save/ or the state digest (B05 owns it, including
`VoxelGame.cs`'s use of `ComputeDigestHex`: leave that entry on the allow-list, its reason changed to B05),
src/Vocaterra.Game.Core/World/ (B03) or content/game-v1/game/scene.json (B08a). Not in scope: streaming, budgeted generation, the
26-neighbour mesh gating, eviction policy, far plane and fog (B07); widening the kind masks (B09).

The outcome is **identical pictures**: every scripted run compares exactly as before (the one known exception below).

Deliverables:
1. The renderer, the chunk version tracker and the view set keyed by `VoxelChunkCoord` instead of the dense index, reading through the
   coordinate APIs, so they work on a streamed grid as well as a bounded one. They listen to `IChunkResidencyListener` for installs and
   evictions (a newly installed chunk is meshed; an evicted one drops its mesh) and to the existing block-change events for edits. The
   renderer peeks without generating (`TryGetChunk`, `PeekBlock`), never through a read that would generate a chunk.
2. The mesher's "below the world is stone" reads the grid's minimum y (`Limits.MinY`), not 0.
3. A per-column light index replaces the one-off global `FindLights`: lanterns and torches indexed by chunk column, built as chunks are
   installed and kept current when a light block is placed or removed, so a placed torch lights its surroundings at once. The glow
   itself (its reach and falloff) stays as it is.
4. The rain cover (`ColumnCover`) sparse: keyed by column, built for resident chunks, no array over the whole world.
5. Each B04b entry leaves the allow-list as its file stops using the dense members (the test fails if a listed use disappears, so update
   it with each change).
6. Tests: on the valley, the keyed tracker, view set, light index and rain cover agree with the old dense results (identical meshes per
   chunk, identical lights, identical cover); on a small streamed grid with a test `IChunkGenerator`, installs and evictions mesh and
   unmesh; a torch placed and removed changes the light index.

Checks beyond the suite: capture the scripted runs before your first change (`scripts/scripted-runs.sh <out-before>`) and after
(`<out-after>`), with the same work folder, and `python3 -I scripts/compare-runs.py <out-before> <out-after>` must report no difference.
Known exception (B04a's finding): r01-route's `sounds.log` voice-end tick varies between captures of the same code under load; if r01
differs only there, capture r01 twice on the same code to show it, and report it. Report the frame time of one view (before and after)
if the game prints it.

Gate (from the worktree root): scripts/limited.sh dotnet build Vocaterra.sln -warnaserror -m:4 (0 warnings); scripts/test.sh full
(@@COUNT@@ passed before your change, plus your new tests) run after your last change; scripts/limited.sh dotnet format Vocaterra.sln
--verify-no-changes clean; scripts/limited.sh scripts/content.sh validate passes. Do not commit bin/, obj/ or artifacts/ (keep the
captures outside the repository, for example in /tmp).
Commit messages end with
Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>
Claude-Session: https://claude.ai/code/session_01F9jKDbkPTqABsu7sLVD9h6

Report, one page: branch, commit SHA, worktree path; what was built; test counts; the scripted-run comparison's result; frame time if
measured; the allow-list entries left; bugs found and fixed (where, symptom, fix, test); known-not-fixed with reasoning; files touched
outside the milestone's area; anything left undone and why. Leave out file dumps and long logs.
