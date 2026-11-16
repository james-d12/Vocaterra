You are implementing B07c, "Far-view detail like the Distant Horizons mod", for the Vocaterra repository (C#, .NET 10). The game is
Vocaterra.Game.Core (the engine-free logic), Vocaterra.Game (MonoGame) and Vocaterra.Host; the language core (Vocaterra.Nlp*) is not yours.
This milestone's work is in the game: the far view's drawing in src/Vocaterra.Game/Graphics/Far (FarTileMesher, FarPalette,
FarTerrainRenderer, FarView, FarViewSettings), its sampler in src/Vocaterra.Game.Core/World/Far (FarTerrainSampler, FarSample), a pure
tree-placement query shared with the tree pass (World/Generation/Procedural/TreePass.cs), the quality presets, and their tests.
Where to work: the worktree /home/james/Dev/Vocaterra/.claude/worktrees/b07c, already on the local branch `B07c-far-detail` (cut from
`feature/biomes` at 2114d8f4, which holds the streamed world and far view (B07) and the final terrain (B02b)). Run every command from that
directory. Commit after each item that builds and passes its tests, with the prefix `B07c: `. Do not push. Never push or merge
feature/biomes or main. Leave the worktree clean on its branch.
Resources: every dotnet restore, build, test, format and run, every game run, screenshot and scripted run through scripts/limited.sh
(builds `-m:4`, tests `-m:2`); one heavy command at a time, no `&`, no wait loops on `pgrep -f`. Two other agents (B12, the underground's
light; B07b, teleport fixes) build and run the game at the same time: expect waits. Iterate with `scripts/test.sh fast` or a project;
`scripts/test.sh full` once at the end. Exit 137: lower the parallelism. Environment: prefix every command with
PATH="$HOME/.dotnet:$PATH" DOTNET_ROOT="$HOME/.dotnet" DOTNET_NOLOGO=1; game runs need DISPLAY=:0 and
VOCATERRA_MODELS_DIR=$HOME/.local/share/vocaterra/models; scripted runs use `VOCATERRA_RUNS_WORK=/tmp/claude-1000/b07c-runs`. **Game
windows are allowed; look at every screenshot you take with the Read tool and fix what looks wrong.** Quality before speed: the owner
will judge this by its pictures. Take the final frame-time measurements only when `uptime` shows a 1-minute load under 3 (say the load
beside each figure; if the other agents keep it higher, say so and measure when you can).

The owner's words (2026-11-16, on B07's pictures in /home/james/Dev/Vocaterra/artifacts/b-series/b07/): "how can we make the distances
better quality, but still retaining the performance. So for example, with the Horizons mod in Minecraft, you can see all the trees and you
can still see details even at a distance ... that looks pretty crap, just a big green swath". His choice (D-game-34 item 25 in
docs/game/decisions.md): blocky far land, trees, colour. Today the far view is a smooth height field with vertex colours per biome
(`FarTileMesher`, `VertexPositionColor`, skirts at tile edges; `FarViewSettings` BaseCell 4, TileCells 32, Levels 5), forests only a tint
(`FarSample.Forest`), lit with `ChunkMesher.SunTintFor`, haze from 6% of the range; the voxel zone ends at 224 / 304 / 400 blocks (low,
medium, high) and the far view runs to 4,096 / 8,192 / 16,384.

Read first: CLAUDE.md; docs/game/implementation-plan.md sections 1, 3, 4.14 (your row, B07c) and 6; docs/game/biomes-plan.md section 4
(your row) and its rendering notes; D-game-34 items 21 and 25; .claude/briefs/b07-phase-b2a-report.md and b07-phase-b2b-report.md (the far
view's light, haze, sampler, budgets and the Release measurements you compare against); the code named above, the chunk mesher's light
(`ChunkMesher.SunTintFor`) and the block atlas (Graphics/Blocks). Read the sections named, never whole long files.

Rules: a game milestone edits only the game projects, their tests, content/game-v1, content/schema, assets/ and docs/game/ and takes the
core as it is. A regression check is a C# test. Never edit .github/workflows/, either implementation-plan.md, either decisions.md or
docs/game/biomes-plan.md. Engine code only in src/Vocaterra.Game; Core stays pure (no floating point, clock or System.Random in anything
generation reads). **The generated world must not change:** sharing the tree pass's placement must leave every generated tree where it
is (the 18 `ProceduralGoldenTests`, `SceneTests.GoldenDigest` and the fixture's state digest do not move). Keep off what the other agents
change now: B12 (Graphics/World's mesher light and `ChunkLightIndex`, the residency's underground radius) and B07b (VoxelGame.cs's
scripted steps and teleport, the streamed world's first install in Streaming/). The `--world valley` scripted runs stay identical.

Deliverables:
1. **Blocky far land.** Each far cell is a box column: a top at the cell's height in whole blocks and sides down to each lower neighbour,
   so the far land steps like the voxel land and keeps its silhouette to the horizon. Merge runs of equal tops (greedy) so the triangle
   count stays near today's; no cracks or gaps at a tile's edge or where two detail levels meet (keep skirts where they are needed).
2. **Trees in the far view.** Extract the tree pass's placement into a pure query (the same hash, density, species and height per biome
   row) that both the tree pass and the far sampler call, so the far view knows every tree without generating chunks. In the nearest far
   rings (to about 1 to 2 km at High, less at Medium and Low) draw each tree as a canopy box in its species' leaf colour, sized from its
   real crown, on a trunk; beyond that a forest cell becomes a canopy layer raised by the trees' height, in leaf colour with a bumpy top,
   so forests read as forests. Snowy conifers white-topped where the generator puts snow on them. Test: the query's trees equal the
   trees the generator places in a sample of chunks over seeds 42, 7 and 2026 (positions, heights, species).
3. **Colour and light.** Each surface's colour from its block's texture (the average of its atlas tile, read once at load, with the
   biome tint where the block is tinted), with a small per-cell variation from a hash; rock where the generator puts stone on steep
   ground, sand and dirt on banks and shores, snow from the sample; tops lit full and sides shaded by their facing as the chunk mesher
   does; a little darkening at the foot of a step and under canopies. Water in its own colour with depth darkening; rivers at least one
   cell wide so they show beyond the voxel zone. Match the voxel land's colour at the seam (dawn, midday, dusk, greyscale).
4. **Finer cells near.** The first far level at 2 blocks a cell (or the finest the measurements allow), coarser levels beyond, coarse
   beyond about 8 km; built tiles kept while they are in range.
5. **Measurements** (Release, the same walk as B07's part 2b: seed 42, `--pass open`, `--no-settle`, a 250-block walk along the pass
   road, `--stream-stats`) at Low, Medium and High: frame p50, p95, p99, frames over 16.7 ms, far tiles and triangles, tile build time,
   memory; against B07's table. Target at High with 16 km: p95 about 12 ms or less and p99 under 16.7 ms. If the target is not met, say
   what costs most and cut the far trees' ring or the cell size per preset until it is.
6. **Tests:** the mesher's boxes (closed, no cracks at tile edges and level seams, merged faces); the tree query against the generator;
   the block colours from the atlas; the presets' rings; the generated world unchanged (the goldens above).
7. **Pictures for the owner** (an owner-review stop), before (feature/biomes at the base) and after from the same viewpoints, in
   /home/james/Dev/Vocaterra/artifacts/b-series/b07c/ with an index.md and the frame times: B07's set (river-valley at midday, summit
   over the sea and lakes, rim looking at a chain's spine) plus a forest seen from 500 m to 2 km, a view at dawn and at dusk, and one at
   Low. Pick viewpoints on open ground with nothing in front of the camera.
8. **Documents:** the Game README (the far view), docs/game/running.md (the presets' far settings).

Gate: build with -warnaserror (0 warnings); scripts/test.sh full (12,267 passed before, plus your new tests); format clean; content
validate. Commit messages end with
Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>
Claude-Session: https://claude.ai/code/session_01F9jKDbkPTqABsu7sLVD9h6

Report, one page: branch, head commit, worktree; what was built (the boxes, the tree query and far trees, colour and light, cell sizes);
test counts; the measurements against B07's; the scripted-run comparison; the pictures and what they show, plainly; bugs found and fixed;
known-not-fixed; files touched outside the milestone's area; anything left undone and why. No file dumps or long logs.

---
Addendum sent to the agent at about 09:00 UTC (D-game-34 item 26; replaces items 1 to 4 where they differ): the far view works like
Distant Horizons. (1) LOD data as columns of vertical segments (block, sky and block light), one per cell of 2^L blocks, coarser levels
merging 2 x 2; a segment cap per column (vertical quality, 4 to 8). (2) Built from the real generator: terrain and water, surface strata,
trees from the shared placement query, the regions' own blocks; on workers with a budget. (3) Rendered as greedy axis-aligned boxes,
never a smooth face (a test: every face axis-aligned, every vertex on whole-block coordinates times the cell); per-face shading, corner
darkening, block-texture colour with the biome tint and per-cell noise, translucent water. (4) Stored light: night darkening, glowing
blocks glow far off. (5) Detail falloff: 1-block columns past the voxel zone, each level doubling over a distance band to 16 km at High;
no overdraw under the voxel zone. (6) The brief's target, pictures and rules stand. B07d (later): LOD from loaded chunks as they unload
(edits), the store on disk per world, detail settings; B07c's store designed for it.
Addendum 2 (about 09:10 UTC): the owner relaxed the target: quality first, do not cut detail to reach 12 ms; measure and name the costs; keep High well inside 60 fps and no frame over 100 ms outside a start or jump.
