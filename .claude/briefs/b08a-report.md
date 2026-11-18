# B08a report (Sonnet 5.5, medium; 2026-11-15)

Branch `B08a-the-pass`, head `6755a6b6` (039a9dec, ddf2afd6, 6755a6b6). Agent's suite at its base (3a56ed18): 11,505 passed, 3 skipped.

- The relief rows 12 to 14 were not edited: the terrain samples the relief at a point moved by noise (up to 86 blocks), so lowering
  those cells opened a second trench at z about 260 to the east edge. The pass is a cutting instead: path `pass` (graded, 7 wide) from
  `paso.foot` (340,212) to `paso.edge` (383,208, y 58); `pass.rise` across the village's blend slope from `paso.gate` (316,217) to
  `paso.rise` (326,217); `pass.floor` to the foot. Sites `paso.gate`, `paso.rise`, `paso.foot`, `paso` (372,208, no label), `paso.edge`.
  Path blocks, so protected as a road.
- Rockfall: stamp `rockfall` (stone, cobble, mossy stone), prop id `rockfall` at (355,200), wall to wall from the road to y 64 to 70.
- Code: optional prop key `floorY` (layers at fixed heights, cells at or under the ground skipped) in SceneBuilder.BuildStamp,
  PropSpec, SceneLayoutParser and scene.schema.json.
- Tests: PassTests (6): the road walkable to the edge; the rockfall stone-only on solid ground; protection; with the rockfall no walk
  within 6 blocks of the pass floor reaches paso or the edge (and without it, it does). SceneTests' trail-and-road walk now takes only
  `trail` and `road` (the new paths broke its chaining).
- Pins: SceneTests.GoldenDigest ab4035358a10... to b189ee0a6783...; AtmosphereSimulationTests re-pinned; save-v8.json bundle hash and
  state digest; bundle hash ca6cbd7e...; trees 1906 to 1851.
- Scripted runs: 18 of 32 moved (scene line, save digests, small patches of the distant east ring, r01 sound rolls).
- Pictures: artifacts/b-series/b08a/ 1-village-east-road, 2-rockfall, 3-top-of-pass-looking-west (taken from the rim).
- Finding: the ring is walkable almost everywhere (a step-up-1 walk from the village reached the east edge near y 79 before the
  change), so the rockfall closes the road, not the ring; physical closure needs B07's edge wall.
