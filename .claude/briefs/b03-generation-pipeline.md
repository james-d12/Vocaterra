You are implementing B03, "The generation pipeline and the procedural chunk source", for the Vocaterra repository (C#, .NET 10). The
language core is the engine Vocaterra.Nlp, the Spanish pack Vocaterra.Nlp.Es and the Vocaterra.Nlp.* test and tool projects; the game is
Vocaterra.Game.Core (the engine-free logic), Vocaterra.Game (MonoGame), Vocaterra.Host and tools/Vocaterra.SpeechRender. This milestone's
work is in the game, in Vocaterra.Game.Core and its tests.
Where to work: the worktree /home/james/Dev/Vocaterra/.claude/worktrees/b03, already on the local branch `B03-generation-pipeline` (cut
from `feature/biomes` at 94bea060, which holds B01). Run every command from that directory. Commit after each item that builds and passes
its tests, with the prefix `B03: `. Do not push your branch: it stays local and the gate merges it from there. Never push or merge
feature/biomes or main. When you finish, leave the worktree on your branch, clean, and remove any extra worktree you made.
Resources (the machine has crashed from overload): run every dotnet restore, build, test, format and run, every scripts/content.sh call
and screenshot run through scripts/limited.sh (for example `scripts/limited.sh dotnet build Vocaterra.sln -warnaserror -m:4`; tests with
`-m:2`). scripts/test.sh and scripts/content-loop.sh call it themselves: run them directly. Never start two heavy commands at once (no
`&`, no background builds, no parallel test runs), and write no shell wait loop on `pgrep -f`. Two other agents build in their own
worktrees at the same time; limited.sh queues you all, so expect waits. While iterating run `scripts/test.sh fast` or
`scripts/test.sh project tests/Vocaterra.Game.Core.Tests`; run `scripts/test.sh full` (the whole suite, as the gate) once at the end,
after your last change. Exit 137 means the 6 GB cap killed it: lower the parallelism, do not work around the cap.
Environment: prefix every command inline with PATH="$HOME/.dotnet:$PATH" DOTNET_ROOT="$HOME/.dotnet" DOTNET_NOLOGO=1 (the sandbox may
reject a bare `export`, and it refuses a command line containing the shell builtin words `eval` or `hash`). Model-gated tests need
VOCATERRA_MODELS_DIR=$HOME/.local/share/vocaterra/models.

Read first: CLAUDE.md; docs/game/implementation-plan.md sections 1, 3, 4.14 (your row) and 6; docs/game/biomes-plan.md sections 3.1, 3.2
(the table, the seams, "How the principles apply", including the owner's rule that generation behaviour stays in code and biome rows name
code strategies), 3.4 ("Water", "Biomes", "Decoration"), 3.7 (determinism) and 4 (your row); D-game-34 items 12 and 13 in
docs/game/decisions.md; docs/process/code-guidelines.md. B01's work, which you build on: src/Vocaterra.Game.Core/World/Noise/,
World/Climate/ (`IClimateSampler.Fill` for areas and chunks: a single `At` per column is slow, about 12 µs), World/Biomes/
(`BiomeTable.Classify`, `Surface(row)`, `TreeStrategies`), World/Generation/Procedural/ (`HeightModel`, `ColumnShape` with `Height`,
`NaturalHeight`, `Water`, `WaterLevel`, `RiverWeight`, `LakeWeight`), World/WorldGenData.cs, content/game-v1/game/world.json and
biomes.json, and src/Vocaterra.Game.Core/README.md (B01's section). The valley's code to learn from and port, not to change:
World/Generation/TerrainGenerator.cs (`BankHeight`, `BankBesideWater`, `Finish`; grep, it is long), World/Generation/WaterAtRest.cs and
the tree and undergrowth code in SceneBuilder.cs (grep `TreePassName`, `undergrowth`). Read the sections named, never whole long files.

Rules: a game milestone edits only the game projects, their tests, content/game-v1, content/schema, assets/ and docs/game/ and takes the
core as it is. A regression check is a C# test, never a script with text baselines. Never create or edit anything under
.github/workflows/; do not edit either implementation-plan.md, either decisions.md or docs/game/biomes-plan.md (the orchestrator does);
engine code only in src/Vocaterra.Game; no System.Random, no clock, no floating point and no threads in generation (the purity tests of
B01 cover the new folders; extend their folder list to yours). Tests are Given_When_Then in folders that mirror the source.
Do not overlap the other agents: do not edit src/Vocaterra.Game.Core/Voxel/ or Save/ (B04a and B05), anything in src/Vocaterra.Game
(B04b), or content/game-v1/game/scene.json (B08a). The valley must stay byte-identical (`SceneTests.GoldenDigest` does not move): port
helpers by copying their logic into the procedural folders or by moving them unchanged into a shared helper, as B01 did with `Terracing`.
Not in scope: installing chunks into a `VoxelGrid`, regions, blending, streaming (B06); the underground beyond solid stone (B11); any
game wiring or `--world` option (B03b, B06, B07); the preview tool (B02).

Deliverables (in Vocaterra.Game.Core):
1. `World/Streaming/`: `IChunkSource` (`Generate(VoxelChunkCoord)`; use the existing `Voxel/VoxelChunkCoord`, never a second coordinate
   type) and `ChunkData`, an immutable chunk (16 x 16 x 16 block ids and water levels) owned by this folder, so this milestone needs no
   change to `VoxelChunk`. B06 turns a `ChunkData` into an installed chunk.
2. `World/Generation/Procedural/`: `GenerationPipeline` with the seams `IColumnShaper` and `IChunkDecorator` (internal), each pass with
   a unique `PassName` that is also its `WorldRng` and noise key; a test checks the names are unique and differ from the valley's
   (`terrain`, `forest`, `trees`, `treeshape`, `undergrowth`). Passes, in a fixed order registered in one place: the surface (the biome's
   surface and under blocks, snow above the snow line), river banks (`BankHeight`'s profile), the `BankBesideWater` port (every water
   edge exitable, D-game-26), solid stone down to y -64 (the floor and strata come in B11), trees, undergrowth.
3. **The strategy registry** (the owner's rule, D-game-34 item 13): a biome row may name code strategies for its own surface shaping and
   decoration, as it names tree species. Build the registry with the default strategies (at least the plain surface and the existing
   tree species), resolved by name at load, validated (an unknown name is a validator problem), and a rule that a strategy fades out at
   its biome's border so a border never makes a cliff. A strategy has its own name, never a biome id.
4. Water: every procedural water block is a source at the sea level (32), so all of it is at rest from the start (H07); every column
   below 32 fills to 32; rivers and lakes from `ColumnShape`.
5. Trees: a jittered grid of 4 x 4 cells per chunk, each cell inside one chunk, seeded by `WorldRng.For(seed, pass, cx, cz)`; spacing
   settled by hash priority among neighbouring cells; crowns from the candidates of the neighbouring chunks, writing only the blocks
   inside the chunk being generated; a trunk needs solid ground under it; the tree line from B01's thresholds. Undergrowth driven by
   noise.
6. `ProceduralChunkSource : IChunkSource`, pure and immutable (safe from any thread), filling the climate for the chunk column with
   `Fill`, then the pipeline.
7. Tests: a chunk generated alone equals the same chunk generated among its neighbours, in any order (chunk-locality); water at rest
   (no water block would flow); every water edge exitable; no cliff at biome borders beyond B01's bound; a biome and a strategy defined
   only in test data generate with no code change; procedural goldens (digests of a fixed set of chunks for 3 seeds, including a
   mountain, a coast, a river and a forest); time per chunk (median and worst over a sample, a `[Trait(TestSpeed.Trait, TestSpeed.Slow)]`
   test beside `SceneBuildTimeTests` with a wide margin, since CI is about four times slower than this machine).

Measure and report: time per chunk (surface chunks and all-stone chunks), memory per `ChunkData`, and how many chunks of a column are
non-empty in plains and in a range.

Gate (from the worktree root): scripts/limited.sh dotnet build Vocaterra.sln -warnaserror -m:4 (0 warnings); scripts/test.sh full
(11,597 passed before your change, plus your new tests) run after your last change; scripts/limited.sh dotnet format Vocaterra.sln
--verify-no-changes clean; scripts/limited.sh scripts/content.sh validate passes; `SceneTests.GoldenDigest` unchanged. If you edit
content (for example a strategy name in biomes.json), re-hash it as CLAUDE.md says and put the new hash into
tests/Vocaterra.Game.Core.Tests/Fixtures/save-v8.json. Do not commit bin/, obj/ or artifacts/.
Commit messages end with
Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>
Claude-Session: https://claude.ai/code/session_01F9jKDbkPTqABsu7sLVD9h6

Report, one page: branch, commit SHA, worktree path; what was built (the types, the passes in order, the strategies); test counts; the
measurements; bugs found and fixed (where, symptom, fix, test); design choices the plan left open; known-not-fixed with reasoning; files
touched outside the milestone's area; anything left undone and why. Leave out file dumps and long logs.
