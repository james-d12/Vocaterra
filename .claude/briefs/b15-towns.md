You are implementing B15, "Towns: a region on procedural ground", for the Vocaterra repository (C#, .NET 10). The language core is the
engine Vocaterra.Nlp, the Spanish pack Vocaterra.Nlp.Es and the Vocaterra.Nlp.* test and tool projects; the game is Vocaterra.Game.Core
(the engine-free logic), Vocaterra.Game (MonoGame), Vocaterra.Host and tools/Vocaterra.SpeechRender. This milestone's work is in the game:
Vocaterra.Game.Core (World/Generation, World/Regions), its tests and the preview tool.
Where to work: the worktree /home/james/Dev/Vocaterra/.claude/worktrees/b15, already on the local branch `B15-towns` (cut from
`feature/biomes` at 06e018a5, which holds B01 to B06, B04b and B08a). Run every command from that directory. Commit after each item that
builds and passes its tests, with the prefix `B15: `. Do not push your branch. Never push or merge feature/biomes or main. When you
finish, leave the worktree on your branch, clean, and remove any extra worktree you made.
Resources (the machine has crashed from overload): run every dotnet restore, build, test, format and run and every run of the preview tool
through scripts/limited.sh (for example `scripts/limited.sh dotnet build Vocaterra.sln -warnaserror -m:4`; tests with `-m:2`).
scripts/test.sh calls it itself: run it directly. Never start two heavy commands at once (no `&`, no background builds, no parallel test
runs), and write no shell wait loop on `pgrep -f`. Up to three other agents build at the same time; limited.sh queues you all, so expect
waits. While iterating run `scripts/test.sh fast` or `scripts/test.sh project tests/Vocaterra.Game.Core.Tests`; run `scripts/test.sh full`
once at the end, after your last change. Exit 137 means the 6 GB cap killed it: lower the parallelism. No game windows: do not run the
game, screenshots or scripts/scripted-runs.sh (the owner may be using the screen); the preview tool's PNGs are fine.
Environment: prefix every command inline with PATH="$HOME/.dotnet:$PATH" DOTNET_ROOT="$HOME/.dotnet" DOTNET_NOLOGO=1 (the sandbox may
reject a bare `export` and refuses a command line containing the words `eval` or `hash`). Model-gated tests need
VOCATERRA_MODELS_DIR=$HOME/.local/share/vocaterra/models.

Read first: CLAUDE.md; docs/game/implementation-plan.md sections 1, 3, 4.14 (your row) and 6; docs/game/biomes-plan.md sections 3.1,
3.2 (the modules table, `ProceduralReliefRegion`, "How the principles apply"), 3.6 ("Towns") and 7; D-game-34 items 13 and 14 in
docs/game/decisions.md (generation behaviour in code, data for the basics; B16 later declares regions in world.json, so a region's
size, place and layout come from data, never code constants); docs/process/code-guidelines.md; .claude/briefs/b06-report.md (`IRegion`,
`RegionCatalog` and its `Validate`, `AuthoredReliefRegion`, `RegionBlend`, `CompositeChunkSource`, `WorldFactory`, `Scene.Places`, the
test region of another size and place) and b03-report.md (the procedural pipeline). The code: src/Vocaterra.Game.Core/World/Regions/,
World/Generation/TerrainGenerator.cs, Terrain.cs, SceneBuilder.cs (how a layout's sites, props and paths are placed), World/Layout/
(the layout's types and parser), World/WorldFactory.cs, tools/Vocaterra.WorldPreview (its `--world valley` mode). Read the sections
named, never whole long files.

Rules: a game milestone edits only the game projects, their tests, content/game-v1, content/schema, assets/ and docs/game/ and takes the
core as it is. A regression check is a C# test. Never create or edit anything under .github/workflows/; do not edit either
implementation-plan.md, either decisions.md or docs/game/biomes-plan.md (the orchestrator does); engine code only in src/Vocaterra.Game
(none needed here). The seams' implementations are pure, immutable and safe from any thread (the purity tests). The valley stays
byte-identical (`SceneTests.GoldenDigest` does not move, and the bounded and streamed valley stay equal). No content words: the sample
town has no phrases, names or NPCs, and lives in test data (or in content/game-v1 only if it is not loaded by the game). Keep off the
files other agents are changing now: world.json and biomes.json, World/Climate, World/Generation/Procedural (B02b), and in World/Regions
the blend (`RegionBlend`, `BlendedHeightModel`, `BlendedClimateSampler`, which B02b is changing for rivers near a region: build on them
as they are, through `IRegion`); content/game-v1/game/scene.json and Menu/ (B08b); src/Vocaterra.Game.Core/Voxel/ and
src/Vocaterra.Game/Graphics/ (B09).

Deliverables:
1. `IGroundShape`: a strategy inside `TerrainGenerator` that chooses the ground's height, either the hand-drawn relief (the valley, as
   today, byte-identical) or the procedural height at the region's world position.
2. `ProceduralReliefRegion : IRegion`: a layout's sites, props and paths placed at an offset on procedural ground, its bounds and
   position from its layout and placement (no constants), with points that know their origin (a site's local and world coordinates);
   ground levelled only where its sites and paths need it, so its border meets the procedural world with no step beyond the B01/B03
   bound; its edge height and template digest exported like the valley's, so `RegionCatalog`, `RegionBlend` and `CompositeChunkSource`
   take it with no change of theirs.
3. One tiny sample town in test data (a few buildings or props and a path, no words), placed by a test away from the valley.
4. Tests: the valley unchanged (GoldenDigest, bounded equals streamed); the town at two offsets is the same town relative to its origin;
   its border has no step beyond the bound; the catalog validates the valley and the town together (and refuses overlapping regions);
   the composite source serves both; `Scene.Places` reports the town's region; the town's sites carry world coordinates.
5. Pictures (D-game-34 item 12) with the preview tool (add an option to place a region from a layout file at a position if it has none):
   heights and biome maps of about 512 blocks round the sample town on seed 42 and one slice through it, in
   /home/james/Dev/Vocaterra/artifacts/b-series/b15/ (outside git) with an index.md saying what each shows.
6. Documents: the Game.Core README (regions: the two kinds and how a town is placed).

Gate (from the worktree root): scripts/limited.sh dotnet build Vocaterra.sln -warnaserror -m:4 (0 warnings); scripts/test.sh full
(11,879 passed before your change, plus your new tests) run after your last change; scripts/limited.sh dotnet format Vocaterra.sln
--verify-no-changes clean; scripts/limited.sh scripts/content.sh validate passes; `SceneTests.GoldenDigest` unchanged. Do not commit bin/,
obj/ or artifacts/.
Commit messages end with
Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>
Claude-Session: https://claude.ai/code/session_01F9jKDbkPTqABsu7sLVD9h6

Report, one page: branch, commit SHA, worktree path; what was built (the strategy, the region, how a town is placed, what B16 will
need); test counts; measurements (a town region's build time); the pictures' folder; bugs found and fixed; known-not-fixed with
reasoning; files touched outside the milestone's area; anything left undone and why. Leave out file dumps and long logs.
