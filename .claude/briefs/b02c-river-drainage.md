You are implementing B02c, "River drainage: rivers that only fall", for the Vocaterra repository (C#, .NET 10). The game is
Vocaterra.Game.Core (the engine-free logic), Vocaterra.Game (MonoGame) and Vocaterra.Host; the language core (Vocaterra.Nlp*) is not yours.
This milestone's work is in the game: the procedural world's rivers and lakes (World/Generation/Procedural: the height model, the river
and lake fields, the water and bank passes, the edge pass), the blend's headwater part in World/Regions, content/game-v1/game/world.json
(and biomes.json if the river row needs it) with schema and validator, the preview tool's river and stats commands, and their tests.
Where to work: the worktree /home/james/Dev/Vocaterra/.claude/worktrees/b02c, already on the local branch `B02c-river-drainage` (cut from
`feature/biomes` at @@BASE@@, which holds B02b's terrain (a water level per column, rivers as noise lines), the streamed world (B07) and the
underground (B11, B13, B12)). Run every command from that directory. Commit after each step that builds and passes its tests, with the
prefix `B02c: `. Do not push. Never push or merge feature/biomes or main. Leave the worktree clean on its branch.
Resources: every dotnet restore, build, test and format, every scripts/content.sh call, every preview-tool run, game run and screenshot
through scripts/limited.sh (builds `-m:4`, tests `-m:2`); one heavy command at a time, no `&`, no wait loops on `pgrep -f`. Three other
agents build at the same time: expect waits. Iterate with `scripts/test.sh project tests/Vocaterra.Game.Core.Tests` and the preview tool;
`scripts/test.sh full` once at the end. Caps: 10 GB a command, 16 GB in all (the script's defaults). /tmp is a 16 GB RAM-backed tmpfs
shared by every agent: keep scratch builds on disk under /home/james/Dev/Vocaterra/artifacts/tmp/b02c/ and remove them at the end.
Environment: prefix every command with PATH="$HOME/.dotnet:$PATH" DOTNET_ROOT="$HOME/.dotnet" DOTNET_NOLOGO=1 (the sandbox refuses the words
`eval` and `hash` in a command line: for `scripts/content.sh hash` use a two-line wrapper script); game runs need DISPLAY=:0 and
VOCATERRA_MODELS_DIR=$HOME/.local/share/vocaterra/models. **Game windows are allowed; look at every picture you take with the Read tool.**
Quality before speed.

Why (D-game-34 item 33; B02b's finding): B02b round 3 gave every column its own water level (the smooth ground less the hills and a
clearance, in steps of 3), but its rivers are lines of a noise field with no direction, so the water rises and falls with the land along a
river: 6.5 to 9.8% of river water stands in "humps" higher than both neighbouring reaches, the largest rising 48 to 84 blocks (seed 7's
river climbs to y 98 over hills between ground at 42 and 47; a seed-2026 river joins two seas over a rise), and 6 to 12% of river water
ends in a closed basin or a ring. The owner asked for rivers that only flow downhill.

Read first: CLAUDE.md ("Game content bundle"); docs/game/implementation-plan.md sections 1, 3, 4.14 (your row, B02c) and 6;
docs/game/biomes-plan.md sections 3.4 and 3.6; D-game-34 items 13, 16, 18, 20, 24 and 33 in docs/game/decisions.md; .claude/briefs/
b02b-round3.md and b02b-round3-report.md (the per-column water, how it stays at rest, the readers of the water, the hump measure in the
preview tool's `stats`), b02b-redo.md (round 2's targets), b06-report.md (the blend and the headwaters near a region); the code:
World/Generation/Procedural (`HeightModel`, `ColumnShape`, the river and lake fields, `WaterPass`, `WaterEdgePass`, `RiverBankPass`,
`ColumnContext`), World/Regions (the blend's `Own`/`Toward`), Voxel/WaterLevel.cs and the flow simulation's rest rules. Read the sections
named, never whole long files.

Rules: a game milestone edits only the game projects, their tests, content/game-v1, content/schema, assets/ and docs/game/ and takes the
core as it is. A regression check is a C# test. Never edit .github/workflows/, either implementation-plan.md, either decisions.md or
docs/game/biomes-plan.md. Generation stays pure and deterministic (no floating point, clock or System.Random), and the blocks of any chunk
are the same whatever order chunks are generated in and on whatever thread (B11's purity and locality tests). A drainage network is
non-local by nature: compute it as a pure function of the seed over large cells with margins (for example a coarse grid per cell, flow
directions and accumulation by a priority flood, channels where accumulation passes a threshold, water levels made monotone downstream),
memoised in a thread-safe cache, continuous across cell borders (a river crossing a border continues; prove it in a test). Every new
parameter in world.json with its schema and validator. **The valley stays byte-identical** (`SceneTests.GoldenDigest` does not move; the
valley's own river and lake are authored). Keep the `ColumnShape` water API (`Water`, `WaterLevel`, `Surface`) that the far view reads.
Keep off what the other agents change now: B07c (src/Vocaterra.Game/Graphics/Far, World/Far and World/Lod: the far view), B11b (the
entrance, lining and surface-rock passes, underground.json); name in your report anything they must adapt to (B11b lays gravel on river
beds and banks from the river's carve).

Deliverables:
1. **A drainage network:** rivers start in the high ground (sources in hills and below the ranges' saddles, never on a crest; near a
   region still in the hills, with no water in the region's margin: D-game-34 item 18) and flow only downhill to the sea or a lake;
   tributaries join larger rivers (wider and deeper downstream); a river never crosses a crest or climbs a rise.
2. **Water that only falls:** along every river's flow the water surface never rises; flat reaches joined by short rapids or falls (built
   as B02b builds them, at rest); lakes fill basins to the level of their outlet and drain on from there (or stay closed in a dry basin
   only where the data allows); the sea stays at the sea level. A freshly generated world stays at rest (B02b's wake test).
3. **Readers updated:** the water, bank and edge passes, the biome table's water test, B11's cave water rule (it reads the per-column
   water), the blend's headwaters, the preview tool (`river`, `stats`).
4. **Measures** (the preview tool's `stats`, tests over seeds 42, 7 and 2026 in 16k squares): river humps 0% (no reach higher than its
   downstream neighbour; state the rule); river water reaching the sea or a lake 98% or more; rivers per 16k square and the share of land
   within 500 blocks of a river (state them, round 3's for comparison); lakes 1.5 to 3.5%; and everything B02b got right kept: the sea 35 to
   45% in one ocean, continents 5,000 to 15,000 blocks, chains as round 3, coasts as round 3, snow only above y 120, the land rising round
   the valley with no sea within 1,000 blocks, clean beaches, steps no worse than 9, the ring walk 97% or more.
5. **Pins:** the 18 procedural goldens, save-v9.json's bundle hash and state digest, the atmosphere pins if they move, `GeneratorVersion`
   up by one; re-hash the bundle.
6. **Pictures** in /home/james/Dev/Vocaterra/artifacts/b-series/b02c/ with an index.md: the heights and biome maps of the three seeds
   beside round 3's (artifacts/b-series/b02b/), river profiles from source to sea (the water stepping down, never up), a map of one
   drainage basin with its tributaries, and in the game: a river stepping down a valley from above, a confluence, a lake with its outlet.
7. **Documents:** content/README.md (the new world.json parameters), the Game.Core README (rivers), the preview tool's README.

Gate: build with -warnaserror (0 warnings); scripts/test.sh full (@@COUNT@@ passed before, plus your new tests); format clean; content
validate. Commit messages end with
Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>
Claude-Session: https://claude.ai/code/session_01F9jKDbkPTqABsu7sLVD9h6

Report, one page: branch, head commit, worktree; the network's design (cells, margins, the cache, continuity, cost per column before and
after); how water stays monotone and at rest; the readers changed; the measures before and after; pins that moved; the pictures and what
they show, plainly; what B07c's far view and B11b must adapt; known-not-fixed; files touched outside the milestone's area. No file dumps or
long logs.
