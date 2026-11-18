B11c report (agent ab42f16f789ba43b0, Sonnet 5.5 medium, 2026-11-17 about 21:15 UTC)

Branch `B11c-caves-surface`, head `c4d782f6` (one commit on f9093bb4, a test file only), worktree `/home/james/Dev/Vocaterra/.claude/worktrees/b11c`.
Cause: the owner's pits at (857, 71, 227) were B11's one-block entrance shafts on its mask lattice (a slice of 23c5795f at z=227 shows a
shaft from the grass to a cave at x about 855; 35 entrance columns in 32x32 round the spot). He played at about 18:00 UTC on 2026-11-16;
B11b, which replaced the shafts with winding passages, merged at 21:39. On the current code the spot is an unbroken meadow (ground 71, no
entrance within 64 blocks). The cover rule is sound (`CavePlan` ceiling = min(height - 6, cave top, water limit)). Scans: 0 pits in
320,000 columns a seed (42, 7, 2026); 0 air between the top solid block and the ground in 172,544 columns a seed. No fix needed.
Test: `CaveCoverTests` (Slow, 6 cases, seeds 42, 7, 2026): the free world (about 148,000 columns a seed) and the streamed world round the
spot (about 57,000 a seed), columns within an entrance's passage excluded. Suite 12,576 passed, 0 failed; build, format, validate clean.
Pictures in artifacts/b-series/b11c/ (the slices before and after are the evidence; the owner's exact camera not matched).
