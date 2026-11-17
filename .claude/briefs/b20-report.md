B20 report (agent a55b67b92cf085747, 2026-11-17 about 03:15 UTC)

Branch `B20-valley-without-snow`, head 43834fdd (one commit on b4343875), worktree clean, not pushed.
Changed: the snow rule removed from `SceneBuilder`; the `snowLine` field removed (TerrainSpec, SceneLayoutParser, scene.json,
scene.schema.json, the SceneLayout comment, the Game.Core README, the sample-town fixture). Former snow tops take the existing rule:
stone above rockLine or on steep ground, mossy stone low, grass otherwise. `SceneTests.TheMountainsHaveTheHeightsOfTheBrief` now requires
bare stone above 86 (it counted snow); new `SceneTests.TheValleysBuildLaysNoSnowBlockAnywhere`.
Pins: `SceneTests.GoldenDigest` 6f6570562b9e to 6ff0d1c82d70; bundle hash (manifest, save-v9) e15607e887a9 to 32c4c1b56638; the
fixture's stateDigest 357dfed55046 unchanged; atmosphere pins and end-to-end goldens unchanged. Village, Pass, PassLock and
ValleyUnderground tests unchanged.
Gate: build 0 warnings; full suite 12,399 passed + 1 new, 3 skipped, 1 failed: the valley underground timing test (18 s under load;
passes alone); format and content validate clean.
Pictures (artifacts/b-series/b20/, pairs 1 to 8, index.md): 3 and 4 (from the village, from the pass looking back) white caps become
bare stone; 2 slight; 1 identical; 7 and 8 on the ring tops: a snow plateau before, bare stone after; 5 and 6 (outside views from the
streamed world) not useful: both in forest, identical; an owner-chosen viewpoint is needed.
Scripted runs: six run before and after (r01, r02, r19, r22, r31, r42); changed pictures differ only in the skyline's top rows (r01
shot, r02 shot, r19 arrow and shot, r22 bought and sold, r31 land-north and shot); the other 27 runs unchecked.
