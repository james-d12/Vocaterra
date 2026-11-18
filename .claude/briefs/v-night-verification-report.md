# Night verification report (Sonnet 5.5, medium; 2026-11-16; no repository changes)

Builds: after `4fc0a00f` (worktree verify), before `bb145f22` (verify-base); both clean.

- Scripted runs: all 32 exit 0 on both. `scripts/compare-runs.py` hangs on the saves' single 70 KB lines (a quadratic mask); a copy with
  that regex bounded to 150 characters compared them. 17 runs "order only" (r02-06, r10, r11, r16-19, r22-24); 15 differ, every
  difference expected: the scene line (trees 1,906 to 1,848, digest, chunk counts: B08a's cutting and rockfall; `SceneTests.GoldenDigest`
  held from B08a's gate to B11, so the 1,848 is B08a's final commit, not 1,851 as its report said); the world and state digest lines,
  autosave lines and replay hashes (B05, B06, B11, the bundle hash); pictures pixel-diffed: only the far east ring (the pass notch, fewer
  trees) and the dev overlay's digests; peaks identical. Nothing from B04b, B09, B10, B10b, M96 or B15 in the valley's pictures. r01's
  sounds.log differs between captures of one build too (34 and 8 lines): the known timing noise; r01's pictures identical.
- Pictures: /home/james/Dev/Vocaterra/artifacts/b-series/verify/ (index.md): the rockfall closed (a stone and cobble wall fills the
  cutting) and open (gone, a stepped dirt road); the road at (380, 208) open (rock walls, empty sky at the edge); the road from the
  village at (318, 217) facing a 2 to 3 block dirt riser; the cutting from the rim (mossy stone steps); valley views before and after
  identical but for the notch and a few trees.
- Play-check: with the pass open a walk reaches (383, 208) and stops at the edge (383.7, 59.4, 211.7); closed, the walk gives up at
  (355.7, 211.6), 17 blocks short; a save with the pass open writes `"passOpen":true` and a load keeps the rockfall gone; the cheat row
  not testable by script (only `--pass open`); default start unchanged; the guide's first exchange and the core demo as before.
- Findings: (1) the road from the village to the gate: a 41-turn, 3,023-tick walk from (300, 222) to (316, 217) (later legs 150 to 470)
  and a dirt riser at (318, 217); (2) compare-runs.py hangs; (3) the rockfall shot's start (337, 208) stands in trees, (341, 212) used.
