B11d report (agent a2853600511cd98b8, Sonnet 5.5 medium, 2026-11-18 about 02:10 UTC)

Branch `B11d-hillside-mouths`, head 2c32bc0e (feature/biomes merged at 103d4c9b), worktree `/home/james/Dev/Vocaterra/.claude/worktrees/b11d`.
`EntranceSites.FaceMouth` runs first for each trying site: a 6-block grid inside the site, spots where the ground 6 blocks ahead in one of 4
directions climbs `faceRise` (4) or more (plain, dry, above sea); the 10 steepest with 3 draws each, the best-covered passage wins; a face
passage runs level 3 to 6 cells, then falls gently; no face, the old level-ground search. New field `entrances.faceRise` (record, loader,
validator, schema; 0 turns it off). Shares (84 sample passages, seeds 42, 7, 2026): ground rising 4+ within 6 cells 9 of 82 (11%) before,
26 of 84 (31%) after (42: 3/31 to 14/33; 7: 0/24 to 2/24; 2026: 6/27 to 10/27); seed 7 almost flat. Tests: at least 25% open into a slope
(29% under the strict definition: rise 4+ within 6 cells, approach within a block of the mouth's floor, floor falling at most a block over
3 cells); a walk test per seed over the first 4 slope mouths (approach height, 0 or 1 fall a cell over 6 cells, a walk on steps of one to a
cave 20+ down, `Assert.NotEmpty`). Review: dcb33586 had dropped `Assert.Equal(ramp.MouthY, ramp.Nodes[3].Floor)` from its own new test;
the orchestrator sent it back; the cause was an old-style plunging fallback mouth on rising ground (seed 2026, MouthY 112, node 3 at 109)
counted as a face mouth; the strict definition and the walk test replace it; 2c32bc0e corrects the "level at first" claim. Pins:
save-v9 bundle bdf4dc495b31, state digest 1bc346cb440f (underground.json changed); golden seed 42 mountain (-4,-4) 4e29b0894e58. Suite
12,772 passed, 1 failed (the JobPool race, before B30's fix). Pictures not taken (asked).

Pictures (about 03:50 UTC): 8 in artifacts/b-series/b11d/ (hillsides seed 42 (-812,112,-842), (944,153,1358), seed 2026 (-1428,108,-476); cliff seed 42 (-470,233,-1116)), each outside and 4 cells in. The hillside mouths read as a dark notch or trench in a terraced slope with a roof over the inner part, not Minecraft-style arches; the cliff mouth's outside shot shows a snow-walled pocket, not the opening. In the real world (`WorldFactory.Compose` with the valley region) face mouths are about 5%: 29 of 559 mouths in the 24 sites within a 1,500-block square; the land round the valley is low and flat, hillside mouths start about 500 blocks out. The tests' 29% is a property of the regionless test world (as were B11b's). Open: tests on the regioned composition, a site search that finds faces near the start, a clearer arch.
