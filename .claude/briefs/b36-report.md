# B36 report (the far sampler shares its work; the generation bench)

Branch `B36-far-sampler-sharing`, head `866192db` (merge of feature/world-next). Commits: `0431aee6` the bench, `b3fd231d` the digests
recorded on the unshared code, `b0a39127` the sharing.

Agent's gate: build -warnaserror 0 warnings; `scripts/test.sh full` 12,644 passed, 0 failed, 9 skipped (base plus the bench smoke test and
12 digest cases); timing 21 and 2 passed; format clean; content validate passes. The optional game run with `--stream-stats` was not done
(machine busy).

Bench: `scripts/limited.sh dotnet run -c Release --project tools/Vocaterra.WorldPreview -- bench [--seed 42] [--passes 3] [--columns 24]
[--json file] [--quick]`. Seed 42, 24 land columns, fastest of 3, one thread; text plus JSON; `--quick` about 12 s; no timing assert, a
smoke test. Game.Core gives `InternalsVisibleTo` to Vocaterra.WorldPreview. The ring row is bands plus generate for 2x2 chunk columns (no
ingest, which lives in Game).

Digest test: `tests/Vocaterra.Game.Core.Tests/World/Far/FarSectionDigestTests.cs` (slow trait): packed sections of model stacks at
levels 0 to 7 plus `Fill` and `FillCells` grids, seeds 42 and 2026 (river, coast, mountain, forest, open sea) and seed 42 in the
valley-blended world; recorded before the change, unchanged after.

Design: `World/Far/FarScope.cs`, a per-call object (one per `LodModelSource.Fill`, `FarTerrainSampler.Fill` or `FillCells`) with
fixed-size arrays and no statics: climate from memoised lattice corners (`AtLatticePoint`, the same `IntMath.Lerp`), `RegionBlend.Bias`
applied after as `BlendedClimateSampler.At` does; each column's climate and `ColumnShape` memoised by (x, z); the cliff test, `TryRiver`
and `Even` read the scope; past half full a column is computed unshared. Public `At`, `AtCell`, `TryRiver` unchanged. Additive:
`BlendedClimateSampler.Inner`, internal overloads.

Bench, seed 42, loaded machine, fastest of 3 (land / sea, ms):

| Row | Before | After |
|---|---|---|
| `Fill` 32x32 step 1, per column | 54 µs | 3.3 µs |
| L0 | 52.3 / 20.6 | 8.5 / 2.7 |
| L1 | 150 / 47 | 24.7 / 5.8 |
| L2 | 204 / 68 | 68.7 / 10.4 |
| L3 | 517 / 83 | 277 / 35 |
| L4 | 224 / 86 | 41 / 29 |
| L5 | 150 / 109 | 61 / 54 |
| L6 | 82 / 72 | 45 / 38 |
| L7 | 94 / 62 | 53 / 34 |

Land: L0 and L1 about 6 times, L2 about 3, L3 about 1.9, L4 to L7 1.8 to 5.5 (below the review's 8 to 14 on L0 to L2). Chunk rows
unchanged (surface 3.1 ms, deep 2.6, air 0.94, column 5.6, residency 3.35 = band 1.05 + layers 2.30, ring footprint 13.1 = 4.3 + 8.8).
Far CPU by the review's tile counts: about 231 worker-seconds before, 87 after; L3 to L7 now about 80% of it, L3 alone 47%.

Left over: L3 land still 277 ms, likely the tree raster and placement at L3 (`Trees()` below `TreeLevels` 4; `PlantTrees` uses an unshared
`FarGround`) and 4-block samples: a lead for B37. Fixed-size tables allocate about 1 to 2 MB per stack (LOH).

Text for docs/game/running.md: "Generation bench (B36): measures chunk generation and the far model the same way each time, so a speed
milestone can be compared with the one before. `scripts/limited.sh dotnet run -c Release --project tools/Vocaterra.WorldPreview -- bench
[--seed 42] [--passes 3] [--columns 24] [--json bench-seed42.json] [--quick]`. Seed 42 over the shipped world files, one thread, each row
the fastest of --passes. It prints the surface, deep and air chunk; the whole column; the residency column (band, then layers); the ring
footprint (2x2 chunk columns, bands plus generate); the far model stack per level 0 to 7 (land and sea); FarTerrainSampler.At and Fill per
column; and the per-stage split of a column. It also writes the rows as JSON. --quick (about 12 s) uses 6 columns, 2 passes and one land
position. It is a tool, not a test, and asserts nothing. Run Release builds only."
