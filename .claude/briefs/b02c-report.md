B02c report (agent abe4172fa8b7bb6ce, 2026-11-17 about 03:20 UTC), first round

Branch `B02c-river-drainage`, head d0fc1039 on 41fd2825 (before B07c, B11b, B18, B07e, B20), worktree clean, not pushed.
Design: `World/Drainage/DrainageNetwork.cs`: nodes every 32 blocks (`height.drainage` data, schema and validator); routing over each node's
water level with a smooth relief noise, dented by the old lake field; a priority flood from the sea fills basins to their outlet; flow
area counted; a river past `sourceArea` 400, width and depth by sqrt(area); a node's level max(own, parent's), so a river never rises. A
landmass is read whole on first touch (BFS over land to the sea) into 128x128-node tiles shared by threads; priorities by place, so the
result is pure and order-independent (tested). First read of a continent 245 to 860 ms, then about +4 us a column. Outside the purity
test's folders (a lock and a thread-static tile cache). `ProceduralGenerator.Version` 3.
Measures (seeds 42/7/2026): river humps 0 / 0.57 / 0 % (round 3: 9.8 / 6.5 / 6.5; seed 7's one is a hairpin the tool's heuristic flags,
no rise in the network); river water reaching sea or lake 99.9 / 99.8 / 99.9 % (94.4 / 91.1 / 87.9); 158 / 142 / 176 sources per 16k
square; lakes 3.4 / 1.9 / 2.8 %; sea, coasts, snow, land steps kept.
Pins moved: the 18 goldens, save-v9 bundle hash and state digest, atmosphere pins, GeneratorVersion 3.
Suite: 12,325 passed, 1 failed (`GeneratedLexiconTests.LoadingTheBundleIsFastEnough`, timing, passes alone).
Pictures: artifacts/b-series/b02c/ (maps beside round 3's, drainage, basins, river sections, four in-game shots of seed 7).
Left: hairpin heuristic; a continent's first read blocks its first chunk up to about 1 s; a sea-less world is cut at maxNodes;
`climate.river` kept but unused for water.
Sent back (about 03:40) to merge feature/biomes at 9d2f2139 (seven conflicts), pass `RiverWeight` in the far sampler, check which threads
can trigger a first read, check B11b's entrances against rivers, re-record pins once, far-view pictures.
