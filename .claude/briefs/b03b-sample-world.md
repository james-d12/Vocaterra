You are implementing B03b, "A procedural sample in the game, and the first 3D pictures of the biomes", for the Vocaterra repository
(C#, .NET 10). The language core is the engine Vocaterra.Nlp, the Spanish pack Vocaterra.Nlp.Es and the Vocaterra.Nlp.* test and tool
projects; the game is Vocaterra.Game.Core (the engine-free logic), Vocaterra.Game (MonoGame), Vocaterra.Host and
tools/Vocaterra.SpeechRender. This milestone's work is in the game: a small Core factory and a test aid in src/Vocaterra.Game's startup.
Where to work: the worktree /home/james/Dev/Vocaterra/.claude/worktrees/b03b, already on the local branch `B03b-sample-world` (cut from
`feature/biomes` at @@BASE@@, which holds B01, B04a and B03). Run every command from that directory. Commit after each item that builds and
passes its tests, with the prefix `B03b: `. Do not push your branch: it stays local and the gate merges it from there. Never push or merge
feature/biomes or main. When you finish, leave the worktree on your branch, clean, and remove any extra worktree you made.
Resources (the machine has crashed from overload): run every dotnet restore, build, test, format and run, and every screenshot run through
scripts/limited.sh (for example `scripts/limited.sh dotnet build Vocaterra.sln -warnaserror -m:4`; tests with `-m:2`; the game with
`scripts/limited.sh <the built game> ...`). scripts/test.sh calls it itself: run it directly. Never start two heavy commands at once (no
`&`, no background builds, no parallel test runs), and write no shell wait loop on `pgrep -f`. Other agents build in their own worktrees
at the same time; limited.sh queues you all, so expect waits. While iterating run `scripts/test.sh fast`; run `scripts/test.sh full` (the
whole suite, as the gate) once at the end, after your last change. Exit 137 means the 6 GB cap killed it: lower the parallelism.
Environment: prefix every command inline with PATH="$HOME/.dotnet:$PATH" DOTNET_ROOT="$HOME/.dotnet" DOTNET_NOLOGO=1 (the sandbox may
reject a bare `export`, and it refuses a command line containing the shell builtin words `eval` or `hash`). Runs of the game need
DISPLAY=:0 and VOCATERRA_MODELS_DIR=$HOME/.local/share/vocaterra/models.

Read first: CLAUDE.md; docs/game/implementation-plan.md sections 1, 3, 4.14 (your row) and 6; docs/game/biomes-plan.md sections 3.2, 3.4
and 4 (your row and "Pictures for the owner"); D-game-34 items 12 and 13 in docs/game/decisions.md; docs/game/running.md (`--seed`,
`--teleport`, `--look`, `--screenshot`, `--frames`, `--view-distance`, `--quality`, `--time`, `--weather`, `--needs off`);
docs/process/code-guidelines.md; docs/game/architecture.md. The pieces you join: src/Vocaterra.Game.Core/World/Streaming/
(`IChunkSource`, `ChunkData`), World/Generation/Procedural/ (`ProceduralChunkSource`, `GenerateColumn` for a whole column at once),
World/Biomes/ (`BiomeTable.Classify`), src/Vocaterra.Game.Core/Voxel/ (`VoxelGrid` bounded and streamed, `WorldLimits`, `ChunkContent`,
`InstallChunk`), src/Vocaterra.Game.Core/World/Scene.cs, and where the game builds its world (src/Vocaterra.Game/Program.cs near line 90,
`buildFor`; src/Vocaterra.Game/Startup/LaunchOptions.cs for the options). src/Vocaterra.Game.Core/README.md has B01's and B03's
sections. Read the sections named, never whole long files.

Rules: a game milestone edits only the game projects, their tests, content/game-v1, assets/ and docs/game/ and takes the core as it is.
A regression check is a C# test. Never create or edit anything under .github/workflows/; do not edit either implementation-plan.md,
either decisions.md or docs/game/biomes-plan.md (the orchestrator does); engine code only in src/Vocaterra.Game (D-game-1); Core holds the
logic, Game the drawing; no new content words. Do not overlap the other agents: do not edit src/Vocaterra.Game/Graphics/ (B04b),
src/Vocaterra.Game.Core/Save/ (B05) or content/game-v1/game/scene.json (B08a); keep the default game (the valley) exactly as it is: with
no `--world` option nothing changes. This is a test aid for pictures, not the streamed world (B06, B07): no streaming, no regions, no
blend.

Deliverables:
1. A Core factory that builds a bounded sample `Scene` from the procedural source: a box of about 256 x 320 x 256 blocks above y 0 (chunk
   layers 0 to 19), its x and z origin chosen by the caller, filled with `ProceduralChunkSource.GenerateColumn`, with a spawn on the
   ground near the middle and no sites, NPCs, lessons or layout features. Report its build time and memory.
2. The test aid `--world sample` (with `--seed`, and `--at <x>,<z>` for the box's centre in world coordinates, and `--near-biome <id>`
   to centre it on the nearest column of that biome found by a spiral search of `BiomeTable.Classify` from the origin). It runs the game
   in that box with no guide, NPCs, lessons, orders or needs (whatever the game needs switched off to run without the valley's sites),
   so `--teleport`, `--look` and `--screenshot` work. Document it in docs/game/running.md as a test aid. An unknown biome id is a clear
   error.
3. Tests: the factory's scene for a fixed seed and origin has a pinned digest; its chunks equal `ProceduralChunkSource`'s; the spiral
   search finds each shipped biome within a stated radius for seed 42; the option parsing (`--world sample`, `--at`, `--near-biome`).
4. **The pictures for the owner** (D-game-34 item 12), in /home/james/Dev/Vocaterra/artifacts/b-series/b03b/ (outside git; create the
   folder): for seeds 42, 7 and 2026, at midday in clear weather and at the High quality preset with the longest view distance, one or two
   screenshots of each shipped biome (ocean, beach, river-channel, lake-basin, plains, woodland, birchwood, dense-woodland, taiga,
   meadow, grove, snowy-slopes, stony-peaks, jagged-peaks), plus a wide view across a range from its foot, a river winding through
   lowland, a coast and a lake. Name the files `<seed>-<what>.png`. Write an `index.md` in that folder listing each picture with one line
   on what it shows and what to look at. If a biome cannot be found or a view looks broken (holes, floating trees, cliffs, water that is
   not level, the far plane cutting peaks), say so plainly in the index and in your report; do not tune the generator.

Gate (from the worktree root): scripts/limited.sh dotnet build Vocaterra.sln -warnaserror -m:4 (0 warnings); scripts/test.sh full
(@@COUNT@@ passed before your change, plus your new tests) run after your last change; scripts/limited.sh dotnet format Vocaterra.sln
--verify-no-changes clean; scripts/limited.sh scripts/content.sh validate passes. Do not commit bin/, obj/ or artifacts/.
Commit messages end with
Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>
Claude-Session: https://claude.ai/code/session_01F9jKDbkPTqABsu7sLVD9h6

Report, one page: branch, commit SHA, worktree path; what was built; test counts; the sample's build time and memory; the pictures'
folder and what they show, with anything that looks wrong described plainly; bugs found and fixed; known-not-fixed with reasoning; files
touched outside the milestone's area; anything left undone and why. Leave out file dumps and long logs.
