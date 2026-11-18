B14 report (agent ae23166d1cf0dc16c, Sonnet 5.5 medium, 2026-11-17 about 13:30 UTC)

Branch `B14-integration`, head `ca5cee33`, worktree `.claude/worktrees/biomes/.claude/worktrees/b14` (nested), clean. Merged feature/biomes
at 21d80c3a (B21, M98, B22, B24a); not B07f, B17b or B02c (it conflicts with B02c in biomes.json, manifest.json, scripted-runs.md and
save-v9.json: merge after B02c, re-hash).
1. Ambient by place: each biome row has an `ambient` field (bed ids with weights in thousandths: wind, birds, river, insects, surf;
   schema, validator); `PlaceAmbience` (Game.Core/Sound) reads `IPlaceInfo` at five columns six blocks out (a border is a 12-block slope)
   and the depth: surface beds fade 3 to 14 blocks down and a quiet `cave` bed takes over; regions keep their beds; 1.5 s cross-fade; the
   sound log names the biome or `underground`. `surf` and `cave` synthesised in code (`AmbientLoops`): no file or licence.
   `WorldStateHeader` hashes biomes.json without `ambient`, so the state digest did not move.
2. `ProceduralWaterSeedsTests` (slow): seeds 42, 7, 2026, seven windows each (the pass's east end, two rivers, two lakes, two coasts):
   all water at rest, every edge exitable; 21 of 21 passed (before B02c's rivers).
3. Signed `TryParseColumn` and `TryParseBlock` (`--walk-to`); tests. 4. `cloudAltitude` in weather.json (80 to 800, default 168) through
   `SkyLook` to `CloudField`; `CloudAltitudeTests`; bundle re-hashed, fixture updated.
5. `r51-biome-tour` (21 stops: the valley, the pass, all 14 biomes, a cave's ground, mouth, passage, landing and cavern, the far land from
   the stony peaks), Release, High, midday, 571 s; pictures in artifacts/b-series/b14/; 20 and 21 have no torch (the stall below).
   `BiomeTourTests` check a stop per biome, in or beside it, ground at each teleport, an open torch cell. Seen in the pictures: thin seams
   between spruce leaf blocks (05, 06), a one-block recess in the snow (08), birches and oaks in the taiga (13), streaks on distant water
   (14, 16); the entrance's open pit hears `cave=0.9` while open to the sky. r50's cave-tour torch cell fixed (it was in a floor block).
6. Docs: architecture.md, saves.md, running.md, sound-and-display.md, scripted-runs.md ("Known stall"), content/README "Adding a biome"
   and "Adding an ore".
Stall: `--teleport-to 468,62,153 --give antorcha --place antorcha@467,62,153 --wait 3 --shot x.png` (seed 42, `--pass open`) stalls 10
minutes on B14 and on feature/biomes (at B17b's launch) alike: the simulation holds, workers idle, main thread alive (B25). The first
teleport of a cold run waited 5 to 13 minutes on a loaded machine while the far land was made.
Suite 12,585 passed, 1 failed (`NamePlatesTests...`, game process exit 139 under load; 9 of 9 alone); build, format, validate, lint
clean; the last two commits (scripts, tests, docs) checked alone, not by a full run. The orchestrator killed about 20 wait loops it left.

Round 2 (about 18:30 UTC), head 0e277f7e: merged feature/biomes 08bbfeb0 (B02c, B07f, B17b); conflicts in biomes.json (B02c's river
range kept with `ambient`), manifest.json and save-v9.json (re-hashed: bundle 8818e16a..., state digest B02c's ae5f5d0e...),
scripted-runs.md. ProceduralWaterSeedsTests 21 of 21 on B02c's rivers. Tour stops re-picked for B02c's river (1404,-72), lake
(4528,1880), dense woodland (466,-1688). Torch pictures 20 and 21 retaken after B07f's fix (torch and warm light shown). Not retaken: 07,
12, 14 (a full tour re-run stalled at the beach stop, 30-minute bound). Suite 12,638 passed, 4 failed (BiomeTourTests fixed; Pristine and
an M98 fuzz test passing alone). Gate (about 19:25): `50d919a1`, 12,714 passed, 1 failed (the core lexicon wall-clock test, passing alone).
