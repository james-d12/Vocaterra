You are implementing round 3 of B02b, "Terrain tuning: rivers with water all the way down, thinner chains, smoother coasts", for the
Vocaterra repository (C#, .NET 10). The language core is the engine Vocaterra.Nlp, the Spanish pack Vocaterra.Nlp.Es and the
Vocaterra.Nlp.* test and tool projects; the game is Vocaterra.Game.Core (the engine-free logic), Vocaterra.Game (MonoGame),
Vocaterra.Host and tools/Vocaterra.SpeechRender. This milestone's work is in the game: content/game-v1/game/world.json and biomes.json,
World/Climate, World/Generation/Procedural (the height model, the water and river passes), the blend's headwater part in World/Regions,
the water model where generated water meets the flow simulation, the preview tool, and their tests.

Where to work: the worktree /home/james/Dev/Vocaterra/.claude/worktrees/b02b, on the local branch `B02b-terrain-tuning` (head 03ee06fa:
rounds 1 and 2, built by an earlier agent; its reports are .claude/briefs/b02b-report.md (round 1) and the round-2 summary below). Run
every command from that directory. Commit after each step that builds and passes its tests, with the prefix `B02b: `. Do not push.
Never push or merge feature/biomes or main. When you finish, leave the worktree on the branch, clean.

Resources (the machine has crashed from overload): every dotnet restore, build, test, format and run, every scripts/content.sh call and
every run of the preview tool through scripts/limited.sh (builds `-m:4`, tests `-m:2`; `scripts/limited.sh dotnet run -c Release
--project tools/Vocaterra.WorldPreview -- ...`). scripts/test.sh calls it itself. Never start two heavy commands at once (no `&`, no
background builds), and write no shell wait loop on `pgrep -f` (wait for your command's own completion). Other agents build at the same
time; expect waits. Iterate with `scripts/test.sh project tests/Vocaterra.Game.Core.Tests` and the preview tool; run `scripts/test.sh
full` once at the end. Exit 137 means the 6 GB cap killed it: lower the parallelism, or rerun when the other slot is free. No game
windows: do not run the game or scripts/scripted-runs.sh.
Environment: prefix every command inline with PATH="$HOME/.dotnet:$PATH" DOTNET_ROOT="$HOME/.dotnet" DOTNET_NOLOGO=1 (the sandbox refuses
the words `eval` and `hash` in a command line: for `scripts/content.sh hash` write a two-line wrapper script in /tmp/claude-1000/).

**Step 0: merge first.** Since round 2's base (20e75aa5), feature/biomes has gained B08b (a `pass` section in world.json), B09, B10 and
B10b (blocks, lava), B11 (the underground: underground.json, five passes after `gen/stone`, `ProceduralGenerator.Version` 2, the 18
`ProceduralGoldenTests` re-recorded, a water rule that keeps caves 3 blocks from water and 16 under a water bed), B15 (towns:
`ProceduralReliefRegion`, `IGroundShape`, the preview tool's `--region`), B13 maybe (the valley's underground) and core M96. Run `git merge
feature/biomes` (the local branch). Expected conflicts: world.json (keep B08b's `pass` section and your tuning), manifest.json (re-hash),
tests/Vocaterra.Game.Core.Tests/Fixtures/save-v9.json (bundle hash and state digest re-read on the merged tree), ProceduralGoldenTests.cs
(re-record on the merged tree, searching columns again for a river, lake, coast, forest, mountain and plain), tools/Vocaterra.WorldPreview
Cli.cs and README (keep every side's commands and options). Build and run the Game.Core and preview tests; commit the merge before
round 3's changes. Report every conflict and how it went.

Read first: CLAUDE.md ("Game content bundle"); docs/game/implementation-plan.md sections 1, 3, 4.14 and 6; docs/game/biomes-plan.md
sections 3.4 and 3.6; D-game-34 items 13, 16, 18, 20 and 24 in docs/game/decisions.md; .claude/briefs/b02b-terrain-tuning.md (the
original brief: its rules hold), b02b-redo.md (round 2's targets), b02b-report.md, b11-report.md (the underground and its water rule),
b06-report.md (the blend); /home/james/Dev/Vocaterra/artifacts/b-series/b02b/index.md (round 2's maps and figures; round 1's in
`round1/`). The code: World/Climate (`ContinentField` from round 2), World/Generation/Procedural (`HeightModel`, `ColumnShape`,
`RiverBankPass`, the water pass, `LakeWeight`), Voxel/WaterLevel.cs, Water/Flow/FlowSimulation.cs and Simulation/VoxelSimulation.Water.cs
(how still and flowing water are stored and when the flow simulation wakes), the biome table's water test. Read the sections named,
never whole long files.

**Round 2, in short** (the earlier agent's report): sea 38 to 42% in one ocean, continents 8,000 to 12,000 blocks (`ContinentField`:
jittered Voronoi cells with sea channels along their borders, warped), ranges with summits and saddles (one-octave rounded ridges,
erosion cell 12,288, `peakVariation`), the land round the valley rising inland (`landBias` 450 to 150 over 3,000 blocks, a calm ring to
800, a rugged belt from 1,800), clean beach strips and no puddles (`coast.flatFrom`/`flatTo`), river valleys cut from sources at 190 to
250 down to the plains (`rivers.valley`). Ring walk 191, 189, 190 of 192. What it left wrong, which round 3 fixes:

**The owner's decision (2026-11-16, D-game-34 item 24): "Another round first."** Fix these, then the whole tuning is gated once:
1. **Rivers hold water all the way down.** Today the generator has one water level, the sea's, so a river valley holds water only where
   its floor dips below the sea: most rivers are dry valleys. Give generated water a level per column, so a river carries water from its
   source in the high ground to a lake or the sea: a water surface that steps down the valley in flat reaches joined by short rapids or
   falls, lakes at their own heights. Design it so that a freshly generated world is at rest: loading or walking beside a river does not
   wake the flow simulation into moving water (state how: for example every generated water block a source, each reach held by its
   banks and a lip, falls built as a stable shape). Keep the column-local, pure, deterministic generation (the purity and chunk-locality
   tests). Update every reader of "water is at sea level or none": the water and bank passes, the biome table's water test, the edge
   pass, B11's cave water rule (3 blocks from any water, 16 under a water bed: it must use the per-column water), B06's headwaters (near
   a region a river still starts in the hills, with no water in the margin), and the save's and the flow simulation's invariants. Lakes
   should be more common than round 2's 0.03 to 0.6% (inland lakes where valleys meet or the ground hollows, at their own level).
2. **Thinner mountain chains.** Today ranges are broad snowy massifs and Y-shapes. Wanted: chains a few hundred blocks wide (about 300 to
   800) and thousands long (about 2,000 to 8,000), several times longer than wide, with a spine of peaks and saddles, foothills either
   side; massifs only rarely. State a measure (for example the width and length of connected land above 180, or an aspect ratio) and test
   it over the three seeds.
3. **Smoother coasts.** Today the Voronoi polygon structure still shows: straight coast runs and long straight sea channels. Wanted:
   coasts that wander at every scale, bays and headlands, channels that curve. State a measure (for example the longest near-straight run
   of coastline, or the share of coastline within a few blocks of a straight segment over 200 blocks) and test it.
Keep everything rounds 1 and 2 got right: the sea share 35 to 45% in one connected ocean; continents 5,000 to 15,000 or more; snow only
above y 120 on mountains; the land rising inland round the valley with no sea within 1,000 blocks; clean beaches and no puddles; steps no
worse than 9 and rare walls; the headwaters near a region; the ring walk at 97% or more for seeds 42, 7 and 2026.

Rules: a game milestone edits only the game projects, their tests, content/game-v1, content/schema, assets/ and docs/game/ and takes the
core as it is. A regression check is a C# test. Never create or edit anything under .github/workflows/; do not edit either
implementation-plan.md, either decisions.md or docs/game/biomes-plan.md (the orchestrator does). No floating point, clock or System.Random
in generation. Every new parameter in world.json with its schema and validator; no biome id in src. The valley stays byte-identical
(`SceneTests.GoldenDigest` does not move: B13 may have moved it on feature/biomes; keep whatever value the merged tree has). Keep off
src/Vocaterra.Game (B07: its far sampler reads water heights; say in the report what it must change to read the per-column water), the
rest of World/Regions beyond the blend's headwater and land-bias parts (B15's), World/Underground beyond what the per-column water rule
needs (B11's), and content/game-v1/game/scene.json.

Pictures: draw the same set again into /home/james/Dev/Vocaterra/artifacts/b-series/b02b/ (move round 2's files into a subfolder
`round2/` first), and update index.md to set each map beside rounds 1 and 2, with a table of before, round 1, round 2 and round 3
figures (add: the share of river length that holds water, lake share, chain width and length, the coast-straightness measure). Add
river slices along a valley from source to sea that show the water stepping down.

Gate: build with -warnaserror (0 warnings); scripts/test.sh full (the merged tree's count before your round-3 changes, plus your new
tests) after your last change; format clean; content validate; re-hash and the fixture's bundle hash and state digest re-read; the
atmosphere pins and end-to-end goldens re-pinned only if they move (say why). Commit messages end with
Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>
Claude-Session: https://claude.ai/code/session_01F9jKDbkPTqABsu7sLVD9h6

Report, one page: branch, head commit, worktree; the merge (conflicts and resolutions); the river water design (how a river's water is
stored, how it stays at rest, what the flow simulation and the save see) and the readers changed; the chain and coast changes and their
measures; the figures table; test counts; pins that moved; what B07's far sampler must change; what still looks wrong; files touched
outside the milestone's area; anything left undone and why. No file dumps or long logs.
