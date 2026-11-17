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

Merge round (about 06:45 UTC 2026-11-17), head `a0a8b4fb` (merge of feature/biomes at 9d2f2139 on d0fc1039): seven conflicts resolved,
both sides kept; B11b had bumped `ProceduralGenerator.Version` to 3, so the merged version is 4 (the far store makes new `-g4-` folders).
Pins re-recorded once: save-v9 bundle `b8af9a19b530`, state digest `ae5f5d0e1040`; atmosphere PinnedFresh `ed6b2ee1981d`, PinnedAfterWalking
`d8b97a7997c6`; 9 of 18 goldens moved (each seed's river and lake columns re-chosen to hold water, 42 mountain, 7 and 2026 coast). The far
sampler (three calls), `EntranceSites` and a WorldPreview test pass `shape.RiverWeight`. Threads: a landmass's first read (0.25 to 0.9 s)
can come from the streaming workers, the far jobs, the entrance probe or the loading thread; `WorldQuery` (the game loop's only reader)
answers dry until warm and queued `Warm` on the .NET pool (sent back: the two-worker rule). Start to first frame +0.4 s (7.5 s); a settled
High far view about 20% slower (203 s against 151 to 168 s, load 8 to 20). B11b check: no entrance passage in a water or carved column (3
seeds, new test). `RockVarietyTests`' face-rock share now skipped, with a note, when its sample holds 100 cliff columns or fewer: the new
ground has almost no non-snow cliffs (the old rivers' valley walls were its cliffs). Suite 12,479 passed, 0 failed. Pictures in
artifacts/b-series/b02c/ ("After the merge"): a lake in the far tiles (seed 2026), cave entrances beside a river from above and in section
(seed 7); no in-game cave-mouth picture (holes between trees).

Round 3 (about 08:35 UTC), head `f4a153b5`: `WorldQuery` no longer queues on the .NET pool; it keeps `IsWarm` and the dry answer until the
streaming workers or the far jobs read the landmass (no case found they never warm: `IsWarm` checks nodes within 128 blocks, all the same
landmass or sea). New test: a fresh seed-7 world's river column is dry, then warm with water after its chunk is generated through the
source. `JobPool` and `StreamedWorldHost` untouched. Merged `ef932990` with no conflicts and no pin moved. Suite 12,508 passed, 1 failed
(`PristineChunksTests...ASnapshotIsTaken`, 21.6 s at load 17, passing alone 3 of 3).

Round 4 (about 11:00 UTC), lake lines: the cause is B02c's. `DrainageNetwork.ReadLandmass` gives a lake node max(its flood level, the
level of the node it was found from), and that node can be a bank node whose level is higher, so one lake (seed 2026, round 5300 to 6900,
600 to 800) became steps of 47, 45, 43 (and 41) with walls of water between: the two dark lines in the far-view picture. Attempts: a body
raised to its highest node broke `HeightModelTests...WaterIsExactlyWhere...` (banks leak); a plain lowest-node flatten broke the
river-never-rises tests; a lake node taking its own flood level, each body flattened to its lowest node and the bank chain below capped
passed the new one-level test and the rivers but failed `TerrainTuningTests...NoPuddleLiesOnTheLand` (seed 7: 9 puddles, limit 8), and a
retry gave the same. Reverted to `f4a153b5` (no commit); the lake levels planned as B02d.
Gate on `f4a153b5` merged into `1afd94e2` (about 11:35): failed on B21's `ResidencyInFlightTests...TheyAllocateNothingOnTheGameThread`
(workers 0 and 2: 64,000 bytes, expected 0): B02c's per-frame `WorldQuery` path allocates. Merge reset; sent back.

Round 5 (about 12:40 UTC), head `a35863e1` (Haiku finisher after the agent stopped twice before its suite ended): `IsRead` and `Warm`
built `new[] { -_look, 0, _look }` arrays per call (160 bytes, 400 calls a test = 64,000); plain loops now; a steady-state allocation test.
Second gate (about 14:20): merged `4445e907`; 12,565 passed, 1 failed (`SceneBuildTimeTests`, 3,026 ms against 3,000 wall clock at load 15;
passed alone): kept.
