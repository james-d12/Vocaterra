# Water at rest on the shipped scene (H07)

**Area:** game. **Question:** if every water cell of the shipped scene (`content/game-v1`, seed 42) is woken, does anything move?
**Outcome:** yes, 1,201 cells, in three clusters. The invariant test is written and skipped until the owner decides each cluster
(water-plan section 6, decision 2). Nothing in the layout, the content or the flow rules was changed.

## Method

`WaterAtRestTests` builds the scene, queues every water cell and every neighbour of water, steps `FlowSimulation` (every 15 ticks) to standstill
and records the net change of each cell. Result: settled in 23 steps, 32,052 updates, 1,201 cells changed (1,193 air and 8 reeds became water).
Cause in every cluster: section 4.5 says a source that rests on solid or on source water spreads sideways. The scene's sources stand on source
water (or the pool) with air beside them at their own level, so each such source spills a thin sheet (flowing 1 to 7) into that air.

Timing: worst step 2,048 updates (exactly the budget, never over), 20.8 ms in a debug build. The test asserts
the update count only and prints the time.

## Clusters

| # | Place | Bounding box (x, y, z) | Cells | Was | Fed by | Why | Proposed fix |
| --- | --- | --- | --- | --- | --- | --- | --- |
| 1 | Waterfall sheet and its landing | 252..264, 43..100, 112..120 | 357 | air | the sheet: 3 x 62 sources at x 257..259, z 112, y 38..100; the pool's surface sources | every sheet source rests on source water (the one below) with air on its sides (x 256, 260) and in front (z 113), so the sheet fattens to 5 wide and 2 thick (118 cells at z 112, 180 at z 113); the lowest sheet cell (y 43) sits over the pool surface (y 42) and spreads 7 cells over the pool (z 113..120) | Build it as one source at the top (y 100) over falling water (`WaterLevel` falling) down to **y 42, the pool's surface layer**, so the bottom falling cell has pool sources beside it. Tested, see below |
| 2 | River staircase below the pool | 250..264, 35..42, 130..163 | 225 (223 air, 2 reeds) | air, 2 reeds | river sources at 8 steps: surface 42 at z 129..130, 41 at z 130..134, 40 at 135..139, 39 at 138..144, 38 at 143..148, 37 at 147..153, 36 at 151..153, 35 at 152..157, 34 at 155..163 | the river's surface drops one block at a time (generator: "lowest ground so far less 2", `TerrainGenerator.CarveRiver`); the last source of each upper reach rests on source water and has air at its level over the lower reach, which it floods up to 6 cells (about 30 cells a step) | Either keep as built, baking the spill in (see options), or give each drop a one-row sill: a solid block at the upper level on the first cell of the lower reach, across the river. Cheapest single change: make the river surface fall in fewer, larger drops with falling water, as for the waterfall |
| 3 | River mouth over the lake | 219..258, 33..34, 272..307 | 619 (613 air, 6 reeds) | air, 6 reeds | river sources at y 33 and 34, x 244..250, z 272..299 (about 130) | the river's surface there is 34, the lake's 32 (`WaterLevel`): the river is a ridge of water two blocks above the lake, resting on lake water with air beside it, so it spills 7 cells each side over the whole mouth and over the lake | Layout fix: inside the lake's footprint the river surface must equal the lake's (clamp `_riverSurface` to `WaterLevel` there, or end the river path at the shore). Then the river and the lake are one level and nothing spills. This is also the visible bug in `lake-ridge-asbuilt.png` |

The 8 reeds (cluster 2: 2, cluster 3: 6) are the only decor that changed; all of them are among the five sown plants.

## Decision 5 (water replaces only the five sown plants)

Not a factor for these clusters. Only air and reeds changed, and reeds stay replaceable under decision 5. No cluster spreads through stall goods,
wheat or other decor. Under the new rule the 8 reed cells still turn to water; if the owner wants them kept, the fix is a bank block or moving
the water, as for the rest.

## Is a waterfall of one source over falling water at rest?

Yes, if its bottom cell is in the pool's surface layer. `WaterfallAtRestTests` builds a 3-wide sheet against a cliff, one source on top, falling
cells below, a basin of sources, wakes everything and runs to standstill:

- bottom falling cell at the pool's surface layer (pool sources beside it): 0 cells change, at rest;
- bottom falling cell one layer above the pool's surface (as the shipped scene would have it, y 43 over y 42): 237 cells change (a sheet over the pool).

Why: a falling cell has water below or goes down, so its neighbours skip it; the top source rests on falling water, which counts as "can go down",
so it never spreads; the bottom cell has only sources or solid beside it. What it needs from the builder: it must write `WaterLevel.Flowing(1,
falling: true)` for the column (the scene build writes sources only today; `SetWater` and the save's `WaterRun` already hold levels), and the
sheet's side cells (x 256, 260 at z 112, and z 113) must be solid or the sheet exactly the width built. Falling cells with air beside them are
fine. Not tested in the scene itself (it was not to be changed); the pool's cells at x 255, 261 beside the bottom cell need to be pool water or
solid, which the layout must be checked for.

## Options for the owner

- **Layout fix, cell by cell** (as above): waterfall and river mouth are clean fixes; the river staircase needs sills or falling steps.
- **Keep as built by baking:** run the flow to standstill once at build time and store the result as the scene. Wakes then change nothing, but the
  map gains the spills (a 5-wide, 2-thick sheet, a 7-cell fringe either side of the river mouth, 8 shallow fans down the river), which look
  worse than the layout fixes. No special cases in the rules either way.

## Screenshots (not in the repository)

In `/tmp/claude-1000/-home-james-Dev-Vocaterra/3e0b1995-31d4-4b1e-a9e6-09ca2fa69d1d/scratchpad/h07-shots/`. There is no "wake all" launch step, so
the "after" shots use `--pour` of one source in air near the cluster, which wakes the box round it (5 sideways, 2 up and down) and adds its own
falling column. They show the as-built look but only hint at the spill.

- `waterfall-asbuilt.png`, `waterfall-after.png`: the sheet from the pool's east bank; after, a poured column left of it.
- `river-steps-asbuilt.png`, `river-steps-after.png`: the river staircase seen through the trees (weak: the valley is wooded).
- `lake-ridge-asbuilt.png`, `lake-ridge-after.png`: the river mouth over the lake from the east shore; the raised, textured ridge is the river; after, a poured column on it.
