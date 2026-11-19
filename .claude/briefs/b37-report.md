# B37 report (cheap coarse levels L3 to L7; resumed after the pause)

Branch `B37-cheap-coarse-levels`, head `b832ab1d` (feature/world-next at c8715d4e merged). B37 commits 4530c969, 1ae3443b, 8cfc3cbc,
cfeb68ca and ed896341 (replacing the WIP). Agent's gate: build 0 warnings; full with 0 failures (Game.Core 4,476, Game 1,495, Host 189, core
4,176, Es 2,317, Golden 17, WorldPreview 32); timing passes; format clean; validate; goldens, save fixture, atmosphere and tour pins and
chunk tests unchanged.

L3 (seed 42, one stack): 230 ms to about 49 (20 ms column samples, 21 ms tree plan, 8 ms crowns); 4530c969 (trees through the scope) did most
of it and left L3 byte-identical (asserted). L4 to L7: the cliff test's neighbours take the nearest lattice point's climate; a column on a
lattice line reads one corner; river pooling off at L4 and up (it fired in 4 of 10,240 cells and never found a river); the four-column
merge kept; L4 to L7 digests re-recorded (cfeb68ca).

Bench (ms per land stack, base to after, load about 20, ±30%): seed 42 L3 230 to 49, L4 34 to 15, L5 52 to 14, L6 39 to 10, L7 47 to 18 (L3 to
L7 402 to 106, 3.8 times); seed 2026 477 to 120 (4.0 times). Sea stacks 2 to 4 times.

Not shipped: `MergeLevels` 3 (one column per cell at L4 and up) would take L3 to L7 to about 65 ms (6 times) but keeps cliff rock in 4 of 51
rock-topped cells and loses 12% of wet cells at L7; the default stays (`LodModelSource.DefaultMergeLevels` = 7); `Lod(treeLevels, 3)` in
`FarWorld` is the switch, with the L4 to L7 digests re-recorded. The shipped shortcuts keep water in 6,639 of 6,640 wet cells, the top
within 8 blocks in 99.9%, rock in 35 of 51.

Far store: only real and mipped-real sections go to disk (model sections are never persisted), so no version bump. Tests:
`FarCoarseLevelsTests` (4; no holes at L3 to L7; L3 identical; against exact columns at L4 to L7: 99.3% identical, top exact 99.6%, within
8 blocks 99.9%, water kept 99.98%, none gained, rock kept 35 of 51).

Pictures: artifacts/b-series/b37/ (index.md; seed 42 from a hill, seed 42 over the western sea to the coast, seed 2026 from a snow peak;
before, after and a diff each). Seed 2026 pixel-identical; seed 42 differs in 0.01 to 0.02% of pixels (0.03 to 0.1% in the far strip): a few
grey cliff-rock blocks on a distant crest and a forest edge turn green. The pillar: not reproduced (no water-topped cell 24 above its
neighbours at L4 to L7 in either mode, seeds 42, 2026 and the valley world); if it came from river pooling at L4 and up it is gone.
Left: L3 is now half tree plan (a faster `TreePlacement` or a lower `TreeLevels`, which changes the picture).
