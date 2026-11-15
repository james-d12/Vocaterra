B02b redo (the owner, 2026-11-15, about 22:20 UTC, D-game-34 item 20): B02b is held, not merged. Its gate passed the build and
started the suite, but the owner, shown the maps (seed 42: the 16k heights before and after, the highest range at 4k, the valley at 2k),
chose to have it redone before it is gated. Work on in the same worktree and branch (/home/james/Dev/Vocaterra/.claude/worktrees/b02b,
`B02b-terrain-tuning`, now at 3ccc69c6); the brief .claude/briefs/b02b-terrain-tuning.md and its rules still hold, with these changes:

- Resources: heavy commands go through scripts/limited.sh at its default two slots (no VOCATERRA_HEAVY_SLOTS=1). Three other agents
  build at the same time (B08b, B09, B15); still no game windows.
- Code changes are expected now, in the same files as before (World/Climate, World/Generation/Procedural, the height model, the blend's
  headwater and land-bias parts in World/Regions), with every parameter in world.json, its schema and validator. Keep off the rest of
  World/Regions (B15 is adding a region kind there) and off world.json's other agents' sections (B08b adds `pass`).

What the owner wants, on top of what B02b already got right (connected seas, the coastal shelf, snow only above y 120 on mountains,
inland lakes, steps no worse than 9, rare walls, the headwaters near a region; keep all of it):
1. **Sea about 35 to 45% of the map** (the owner's choice), with big continents 5,000 to 15,000 blocks or more across and large
   connected oceans between them (the largest sea still well over half of all ocean columns on seeds 42, 7 and 2026).
2. **Mountain ranges with peaks, not plateaus.** Today the highest land is a flat snow tableland 2,000 blocks across at each island's
   core. Wanted (as in Minecraft 1.18): long chains hundreds of blocks wide and thousands long, a spine of peaks from about 200 to 300
   that rises and falls along the ridge, with saddles between peaks, steep upper slopes, and foothills and high meadows below; snow on the
   peaks and high slopes above the snow line. State a test that the high land is not flat (for example, over columns above 220, the
   spread of height within 32 blocks is at least some stated amount, and no 64 x 64 block area above 220 is level within a few blocks).
   Fix the ridge creases on foothills if you can.
3. **The land round the valley rises inland.** Today the land bias builds a flat coastal dome at y 32 to 38 round the valley, ringed by
   beach and puddles. The plan wanted the opposite: the valley deep inland, and beyond the blend's margin foothills rising into mountains
   two or three times the height of its ring (the ring's peaks are about 110; so ranges of 220 to 330 within a few thousand blocks). Make
   the bias pull the valley inland (no sea within at least 1,000 blocks) without flattening it into a dome, and let the land beyond the
   margin rise. Keep the blend walkable: report the ring walk for the three seeds (B06's test); the margin stays 64 (the owner's call).
4. **No speckled beach and puddle flats.** Land just above sea level must not flicker between beach, grass and one-block pools; coastal
   land rises a few blocks above the sea within the shelf, the beach is a clean strip, and no pools of a few columns sit on beach or
   lowland (state a measure and test it).
5. **Rivers run down from the mountains.** Rivers start in high ground (springs in the ranges and foothills) and run down valleys to a
   lake or the sea; keep rivers ending in water at 93% or more of river columns; near a region they still start in the hills (D-game-34
   item 18). If true downhill routing needs a different river model than the noise band, design it so it stays pure, chunk-local or
   column-local with a bounded window, and deterministic (the purity tests), and say what it costs per column.

Pictures: draw the same set again into /home/james/Dev/Vocaterra/artifacts/b-series/b02b/ (replace the first round's files, keeping a
copy of the first round in a subfolder `round1/`), and update index.md to set each map beside B02's and round 1's, with the before,
round-1 and after numbers (add: sea share, continent sizes, flatness of high land, pool count, rivers from high ground).

Gate as before: build with -warnaserror (0 warnings); scripts/test.sh full (11,879 before B02b, plus B02b's and your new tests) after your
last change; format clean; content validate; `SceneTests.GoldenDigest` unchanged; re-hash and the fixture's bundle hash and state digest
re-read. Commit with the prefix `B02b: `; never push. Report as before (one page), with the before, round-1 and after figures.
