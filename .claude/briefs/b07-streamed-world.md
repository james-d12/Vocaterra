You are implementing B07, "The game streams the tall world, with a far view", for the Vocaterra repository (C#, .NET 10). The language
core is the engine Vocaterra.Nlp, the Spanish pack Vocaterra.Nlp.Es and the Vocaterra.Nlp.* test and tool projects; the game is
Vocaterra.Game.Core (the engine-free logic), Vocaterra.Game (MonoGame), Vocaterra.Host and tools/Vocaterra.SpeechRender. This milestone's
work is in the game: Vocaterra.Game (start-up, the renderer, the sky, the far view, threads) and Vocaterra.Game.Core (residency policy,
the edge wall, pure samplers), and their tests.
Where to work: the worktree /home/james/Dev/Vocaterra/.claude/worktrees/b07, already on the local branch `B07-streamed-world` (cut from
`feature/biomes` at @@BASE@@, which holds B01 to B06, B04b, B08a, B08b (the pass's lock) and B09). Run every command from that directory.
Commit after each item that builds and passes its tests, with the prefix `B07: `. Do not push your branch. Never push or merge
feature/biomes or main. When you finish, leave the worktree on your branch, clean, and remove any extra worktree you made.
Resources (the machine has crashed from overload): run every dotnet restore, build, test, format and run, and every game run, through
scripts/limited.sh (for example `scripts/limited.sh dotnet build Vocaterra.sln -warnaserror -m:4`; tests with `-m:2`). scripts/test.sh and
scripts/scripted-runs.sh call it themselves: run them directly. Never start two heavy commands at once (no `&`, no background builds, no
parallel test runs), and write no shell wait loop on `pgrep -f` (wait for your command's own completion). Up to three other agents build
at the same time; limited.sh queues you all, so expect waits. While iterating run `scripts/test.sh fast` or `scripts/test.sh project
<project>`; run `scripts/test.sh full` once at the end, after your last change. Exit 137 means the 6 GB cap killed it: lower the
parallelism. Threads only in src/Vocaterra.Game; Core stays pure.
Environment: prefix every command inline with PATH="$HOME/.dotnet:$PATH" DOTNET_ROOT="$HOME/.dotnet" DOTNET_NOLOGO=1 (the sandbox may
reject a bare `export` and refuses a command line containing the words `eval` or `hash`). Model-gated tests and game runs need
VOCATERRA_MODELS_DIR=$HOME/.local/share/vocaterra/models; game runs also need DISPLAY=:0.

**Two phases, because of game windows.** The owner has asked for no game windows while he uses the screen. Phase A (now): build and test
everything without opening a window (no game runs, screenshots or scripted runs). End phase A with your report, listing exactly what
phase B will run. Phase B (only when the orchestrator tells you, by message, that windows are allowed): the scripted runs, the
performance measurements and the pictures, then the full suite again and a short addendum to the report. Also before phase B's pictures
and measurements, merge the local `feature/biomes` into your branch when the orchestrator says B02b's second round of terrain tuning has
landed, so they show the final terrain.

Read first: CLAUDE.md; docs/game/implementation-plan.md sections 1, 3, 4.14 (your row) and 6; docs/game/biomes-plan.md sections 3.2,
3.6 (the pass and the edge wall), 3.7 (storage, streaming, residency, the renderer, scripted runs) and 7 (risks 1, 2, 6, 7), and section 4
(your row, "Pictures for the owner", "Measurement"); D-game-34 items 6, 15, 19 and 21 in docs/game/decisions.md (the edge wall stands for
the body while the pass is locked and lifts all the way round when it opens; the far view: level-of-detail terrain to 16 km at 60 fps on
the High preset). The reports you build on, in .claude/briefs/: b04a-report.md (the sparse grid, residency, `IChunkGenerator`),
b04b-report.md (the renderer, tracker, view set, rain cover and light index keyed by coordinate; no caps yet; a non-resident neighbour
meshes as air), b06-report.md (`WorldFactory` bounded and streamed, `CompositeChunkSource`, `InstallColumn`, `ChunkStreamer` and
`StreamPolicy`, `Scene.Places`, the `WorldFacts` window; a guide snapshot outside the valley generates its window's chunks on the game
thread), b08b-report.md (`session.Pass.IsOpen`, `Pass.Opened`, `session.Data.Pass.Edge`) and b09-report.md. The code: src/Vocaterra.Game/
Program.cs (the `WorldFactory.Create(..., Bounded, layout)` call), VoxelGame.cs (the far plane, near line 769), Graphics/QualityPreset.cs
(view distances 224, 304, 400; a slider to 576), Graphics/World/ (VoxelRenderer, ChunkViewSet, ChunkVersionTracker, ChunkMesher's
neighbour reads), Graphics/Camera/FirstPersonCamera.cs, Graphics/Sky/SkyRenderer.cs (sky far plane 2400), Graphics/Weather (the rain
cover), src/Vocaterra.Game.Core/World/Streaming/, World/WorldFactory.cs, World/WorldQuery.cs, Startup/LaunchOptions.cs, and the body's
collision with the world's edge. Read the sections named, never whole long files.

Rules: a game milestone edits only the game projects, their tests, content/game-v1, content/schema, assets/ and docs/game/ and takes the
core as it is. A regression check is a C# test. Never create or edit anything under .github/workflows/; do not edit either
implementation-plan.md, either decisions.md or docs/game/biomes-plan.md (the orchestrator does); engine code only in src/Vocaterra.Game.
With `--world valley` the game is exactly today's: `SceneTests.GoldenDigest` does not move, and the scripted runs under `--world valley`
compare exactly. Residency never changes results (state digests and saves). Keep off the files other agents are changing now:
World/Climate, World/Generation/Procedural, world.json's existing sections and biomes.json (B02b); World/Regions and
World/Generation/TerrainGenerator.cs (B15; read them, do not change them); Voxel/BlockId.cs, Voxel/BlockDefinition.cs and
Graphics/Blocks/ (B10, which adds nine blocks and lava's glow). B03b's unmerged branch also adds `--world sample` to LaunchOptions: make
`--world` a named choice (`valley`, the default streamed world) that a `sample` value can join.

Deliverables (phase A unless marked B):
1. The streamed world by default: the game starts in the streamed world (the composite source round the valley, the player in the
   valley as today); `--world valley` keeps the bounded valley. The pass's state and saves work in both.
2. Residency driven by `ChunkStreamer`: at a distance only the surface band (the chunks meeting the surface heights of the column and its
   neighbours, from the pure height model, generating nothing to decide); chunks wholly underground only within a near radius (about 4
   chunks) or when the player is underground; generation budgeted per frame on the game loop; an optional `ChunkPrefetcher` with at
   most 2 workers calling the pure source (workers never touch `VoxelGrid`; the loop installs, dropping duplicates); eviction of unchanged
   chunks beyond the keep distance plus one chunk of slack; changed chunks stay. The guide's snapshot outside the valley must not stall a
   frame for seconds (B06's finding): bound it.
3. The renderer: a chunk meshed only when its 26 neighbours are resident; peeks never generate; caps on chunks, meshes and tiles.
4. The edge wall: the valley's outer edge stops the body while the pass is locked; it lifts all round when the pass opens (and stays
   lifted after a load); B06's exit corridor stays walkable.
5. Far plane and fog for height: a peak at 300 is not cut off while its foot is in view.
6. **The far view** (D-game-34 item 21, like the Distant Horizons mod): beyond the voxel chunks, out to 16 km, level-of-detail terrain
   tiles: a pure Core sampler (heights, top surface, water, snow, a forest tint, from the height model, the biome table and the regions'
   templates, generating no chunk; deterministic and tested) and Game-side tile meshes in rings that get coarser with distance (for
   example 4, 8, 16, 32 and 64 blocks per vertex), built on worker threads (shared with or beside the prefetcher, at most 2 in all),
   cached, rebuilt only at ring edges as the player moves; skirts against cracks; no gap or overlap with the voxel zone; haze that blends
   the far land into the sky; depth precision held (a far pass with its own depth range, or reversed depth). Player edits do not show at
   a distance. Memory, build time and triangles counted.
7. Scripted runs: a settle step before each shot (everything in view installed and meshed); residency counts kept out of compared output.
8. Presets: the streamed voxel distances and the far view's rings set by measurement (B); the voxel radius may shrink if the far view
   carries the distance.
9. Tests: `--world valley` unchanged; the same scripted edits under two residency policies and with and without the prefetcher give the
   same state digests and saves; eviction never drops a changed chunk; no chunk meshed with a missing neighbour; the surface band picks
   the right chunks for sample columns; the far sampler is pure, deterministic and agrees with the generated surface at sample columns;
   tile seams have no cracks; the edge wall closed and open (and after a load); the far plane covers a 300-block peak; a time-per-chunk
   test beside `SceneBuildTimeTests`.
10. (B) Measurements on this machine (an NVIDIA RTX 3070 since the owner's upgrade; the plan's "software-GL VM" is out of date) at each
    preset: frame medians, meshing and generation time per chunk, far-tile build time, resident chunks, memory; the target is 60 fps at
    High with the far view at 16 km. Report what it takes if it is not met.
11. (B) Scripted runs: capture before (on your base) and after, compare; `--world valley` runs compare exactly; the streamed runs are
    re-baselined (list what moved and why).
12. (B) Pictures for the owner (D-game-34 item 12): the streamed world from the pass looking out, across the presets, and the far view
    from a height (a mountain top and the valley's rim) and at sea level, with the frame times, in
    /home/james/Dev/Vocaterra/artifacts/b-series/b07/ (outside git) with an index.md.
13. Documents: docs/game/running.md (`--world`, the far view's settings), the Game.Core and Game READMEs, docs/game/architecture.md if
    the threads or the render passes change its picture.

Gate (from the worktree root): scripts/limited.sh dotnet build Vocaterra.sln -warnaserror -m:4 (0 warnings); scripts/test.sh full
(@@COUNT@@ passed before your change, plus your new tests) run after your last change; scripts/limited.sh dotnet format Vocaterra.sln
--verify-no-changes clean; scripts/limited.sh scripts/content.sh validate passes; `SceneTests.GoldenDigest` unchanged. Do not commit bin/,
obj/ or artifacts/.
Commit messages end with
Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>
Claude-Session: https://claude.ai/code/session_01F9jKDbkPTqABsu7sLVD9h6

Report, one page per phase: branch, commit SHA, worktree path; what was built (residency, the prefetcher, the renderer's gating and caps,
the edge wall, the far view's design: sampler, rings, threads, passes); test counts; measurements (B); the scripted-run comparison (B);
the pictures' folder (B); bugs found and fixed; known-not-fixed with reasoning; files touched outside the milestone's area; anything left
undone and why. Leave out file dumps and long logs.
