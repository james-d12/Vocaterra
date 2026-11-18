# B08c report (Sonnet 5.5, medium; 2026-11-16)

Branch `B08c-pass-road`, head `0d345730` (one commit on 4e1691d3). Agent's suite: 0 failed (Game.Core 4,117, up 3); build 0 warnings;
format clean; validate passes.

- Cause of the riser: `ApplySites` blended the village's levelled ground over every cell in its 8-block blend ring, road surfaces
  included, pulling the graded road (from 38) back to the village level (37): a 2-block step at the gate and 2-block steps on the slope.
- Fix: `TerrainGenerator.cs`: a path that neither starts nor ends at a site keeps its own surface across that site's blend (paths ending
  at a site, the west road and the trail, unchanged; a wider first version moved the old fixture's world near the camp). `scene.json`:
  `paso.gate` to (318, 217) y 38 (a 5-wide road from 316 left road blocks inside the village edge under trees, failing
  `TreesNeverStandOnOrOverhangTheRoute`); `paso.rise` (326, 217) to (332, 217) (a block up every second column); `pass.rise` and
  `pass.floor` 5 wide (`halfWidth` 2); `pass.floor`'s via point (336, 215). The cutting past the foot and the rockfall unchanged.
- Walk after: (300, 217) to the gate 6 turns, 293 ticks; gate to `paso.edge` 6 turns, 957 ticks; the shortest step-one walk over road
  cells 67.5 blocks against a 66.9-block centre line.
- Tests: `PassRoadTests` (3: reachability over road cells with no step over one and within 1.3 times the road's length; path blocks at
  every x from the gate to the edge; a scripted walk with the pass open, 600 ticks for the first leg, 2,400 in all). PassTests,
  PassLockTests, ValleyUndergroundTests pass.
- Pins: `SceneTests.GoldenDigest` 83af36d3... to efcfbe02d022...; bundle hash 71de87a0... (in save-v9.json; its state digest unmoved);
  trees 1,846.
- scripts/compare-runs.py: the mask bounded to `\S{0,150}/`; two full captures compare in 4.2 s.
- Pictures: /home/james/Dev/Vocaterra/artifacts/b-series/b08c/ (before-1 to 3, after-1 to 4; index.md).
- Left: the cutting past the foot still steps every 3 to 4 blocks with dirt risers, no retaining walls; the road at the gate climbs in
  1-block steps every second column (voxel stairs).
