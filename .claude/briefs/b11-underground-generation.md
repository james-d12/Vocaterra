You are implementing B11, "The underground generation", for the Vocaterra repository (C#, .NET 10). The language core is the engine
Vocaterra.Nlp, the Spanish pack Vocaterra.Nlp.Es and the Vocaterra.Nlp.* test and tool projects; the game is Vocaterra.Game.Core (the
engine-free logic), Vocaterra.Game (MonoGame), Vocaterra.Host and tools/Vocaterra.SpeechRender. This milestone's work is in the game:
Vocaterra.Game.Core (a new World/Underground folder and one wiring point in the procedural pipeline), content/game-v1 (a new
underground.json with its schema and validator), the preview tool, and their tests.
Where to work: the worktree /home/james/Dev/Vocaterra/.claude/worktrees/b11, already on the local branch `B11-underground-generation`
(cut from `feature/biomes` at @@BASE@@, which holds B01 to B06, B04b, B08a, B08b, B09 and B10's nine underground blocks). Run every
command from that directory. Commit after each item that builds and passes its tests, with the prefix `B11: `. Do not push your branch.
Never push or merge feature/biomes or main. When you finish, leave the worktree on your branch, clean, and remove any extra worktree.
Resources (the machine has crashed from overload): run every dotnet restore, build, test, format and run, every scripts/content.sh call
and every run of the preview tool through scripts/limited.sh (for example `scripts/limited.sh dotnet build Vocaterra.sln -warnaserror
-m:4`; tests with `-m:2`). scripts/test.sh calls it itself: run it directly. Never start two heavy commands at once (no `&`, no
background builds, no parallel test runs), and write no shell wait loop on `pgrep -f` (wait for your command's own completion). Up to
three other agents build at the same time; limited.sh queues you all, so expect waits. While iterating run `scripts/test.sh fast` or
`scripts/test.sh project tests/Vocaterra.Game.Core.Tests`; run `scripts/test.sh full` once at the end, after your last change. Exit 137
means the 6 GB cap killed it: lower the parallelism. No game windows: do not run the game, screenshots or scripts/scripted-runs.sh.
Environment: prefix every command inline with PATH="$HOME/.dotnet:$PATH" DOTNET_ROOT="$HOME/.dotnet" DOTNET_NOLOGO=1 (the sandbox may
reject a bare `export`, and it refuses a command line containing the shell builtin words `eval` or `hash`: for `scripts/content.sh hash`
write a two-line wrapper script in /tmp/claude-1000/ and run that). Model-gated tests need
VOCATERRA_MODELS_DIR=$HOME/.local/share/vocaterra/models.

Read first: CLAUDE.md (in particular "Game content bundle"); docs/game/implementation-plan.md sections 1, 3, 4.14 (your row) and 6;
docs/game/biomes-plan.md sections 3.2 (the modules and "How the principles apply"), 3.5 (the underground: strata, ores, caves, entrances,
water never pours in, lava, under the valley) and 7 (risks 3 and 8), and section 4 (your row); D-game-34 items 8, 9 and 13 in
docs/game/decisions.md (nothing gatherable; lava blocks the body; data for the basics, generation behaviour in code). The reports you
build on, in .claude/briefs/: b03-report.md (the pipeline: shapers and decorators in a fixed order, the strategy registry,
chunk-local generation, `GenerateColumn`), b05-report.md (the state digest's header hashes underground.json when it exists), b06-report.md
(`CompositeChunkSource`: under the valley it is solid stone today; the region blend; `Scene.Places`), b10-report.md (the nine blocks and
their ids; bedrock is indestructible; lava `StopsBody` and `Glows`). The code: src/Vocaterra.Game.Core/World/Generation/Procedural/
(`GenerationPipeline`, `StrategyRegistry`, `ProceduralChunkSource`, the passes), World/Regions/CompositeChunkSource.cs, World/WorldGenData.cs
and how world.json and biomes.json are loaded and validated, World/Noise (fixed-point noise; no floating point), tools/Vocaterra.WorldPreview
(the `slice` command). Read the sections named, never whole long files.

Rules: a game milestone edits only the game projects, their tests, content/game-v1, content/schema, assets/ and docs/game/ and takes the
core as it is. A regression check is a C# test. Never create or edit anything under .github/workflows/; do not edit either
implementation-plan.md, either decisions.md or docs/game/biomes-plan.md (the orchestrator does); engine code only in src/Vocaterra.Game
(none needed). No floating point, no clock, no System.Random in generation (the purity tests; extend their folder list to
World/Underground); every number in underground.json with its schema and validator; no block or biome id in src beyond what the existing
tests allow. The valley stays byte-identical (`SceneTests.GoldenDigest` does not move): the valley's own stone is B13's. Keep off the
files other agents are changing now: world.json and biomes.json, World/Climate, the height model and the river and lake code in
World/Generation/Procedural (B02b is retuning the terrain; add your passes through the pipeline's registration and touch the existing
passes only where a pass must hand you something), World/Regions apart from what `CompositeChunkSource` needs for the underground under
regions (B15 is merging there), src/Vocaterra.Game (B07).

Deliverables:
1. content/game-v1/game/underground.json (schema, validator, loaded with `GameData`): the strata (stone down to about y 8, slate below
   y 0 with a dithered change between, granite and limestone blobs), the bedrock floor (ragged, 1 to 4 thick at y -64 to -60), the ore
   rows (block, y range with a peak, veins per chunk, vein size, the rocks it may replace; starting values from section 3.5: copper common
   -16 to 112 peaking at 48; iron -24 to 56 peaking at 16 plus a band 80 to 320 in the mountains; tin -32 to 48 peaking at 8), the caves
   (cheese and spaghetti thresholds, lattice 4 x 8 x 4, how high they reach), the entrance mask, the lava level (-54), gravel on cave
   floors and river beds if you add it. Re-hash, validate, put the new bundle hash into tests/Vocaterra.Game.Core.Tests/Fixtures/save-v9.json
   and re-read its state digest (the header hashes underground.json).
2. World/Underground: `IStrataSelector`, `OreTable`, `ICaveField` (cheese caverns and spaghetti tunnels sampled on the coarse lattice and
   interpolated), the entrance mask, the lava fill, as pure, immutable, thread-safe types; passes in the procedural pipeline in a fixed
   order after the surface. Chunk-local: ore veins never cross their chunk's edge (`WorldRng.For(seed, "ore:<id>", cx, cz)`); ores replace
   rock only, never air, water or another ore.
3. Entrances only on dry procedural land where the mask allows: never inside a region or its margin, on a path, or within reach of
   water. **Water never pours in:** a cave cell stays solid within 3 blocks of any water cell or under a water column's bed.
4. Under regions: below the valley's chunks (under y 0) the composite source serves the procedural underground instead of solid stone,
   with no cave reaching up into the valley's bottom layer and none under its water.
5. Tests: strata bounds and the dithered boundary; the bedrock floor's thickness and range; each ore inside its rows' ranges with the
   shape of its distribution over a sample; veins inside their chunk; ores never replacing air, water or ore; caves on the lattice, with
   cheese mostly deep and spaghetti winding; entrances only where allowed; no cave cell within 3 of water or under a water bed over
   sampled areas for seeds 42, 7 and 2026; lava filling cave air below its level and nowhere above; chunk-local determinism (a chunk
   alone equals the chunk among shuffled neighbours) and 8 threads; the validator; a time-per-chunk test beside `SceneBuildTimeTests`
   (report the cost of a surface chunk and a deep chunk before and after); `SceneTests.GoldenDigest` unchanged.
6. Pictures (D-game-34 item 12) with the preview tool (add an underground mode to `slice` if it has none, drawing each block's colour):
   vertical slices of the underground for seed 42 (through a range, under a lake or river, under the sea, and under the valley's edge)
   and a cave-entrance map, in /home/james/Dev/Vocaterra/artifacts/b-series/b11/ (outside git) with an index.md. The 3D cave pictures
   come later from the game (windows).
7. Documents: the Game.Core README (the underground) and content/README.md (editing underground.json).

Gate (from the worktree root): scripts/limited.sh dotnet build Vocaterra.sln -warnaserror -m:4 (0 warnings); scripts/test.sh full
(@@COUNT@@ passed before your change, plus your new tests) run after your last change; scripts/limited.sh dotnet format Vocaterra.sln
--verify-no-changes clean; scripts/limited.sh scripts/content.sh validate passes; `SceneTests.GoldenDigest` unchanged. Do not commit bin/,
obj/ or artifacts/.
Commit messages end with
Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>
Claude-Session: https://claude.ai/code/session_01F9jKDbkPTqABsu7sLVD9h6

Report, one page: branch, commit SHA, worktree path; what was built (the passes and their order, the data, the entrance and water
rules, the underground under regions); test counts; the pins that moved; measurements; the pictures' folder; bugs found and fixed;
known-not-fixed with reasoning; files touched outside the milestone's area; anything left undone and why. Leave out file dumps and long
logs.
