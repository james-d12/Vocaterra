# Speeding up chunk generation (2026-11-18)

*A review by an Opus 5.5 agent at high effort for the owner (D-game-34 item 52: "this is the biggest issue right now and we need to get it right"), read only, with measurements on a scratch copy; saved by the orchestrator. Its milestones are in the handover [2026-11-18-0409-world-next](../handovers/2026-11-18-0409-world-next.md).*

Asked for by the owner ("lets kickoff an opus 5.5 agent high to review how to speed up the chunk generation"). He had seen the far view take minutes to fill on a fresh world, with a sky-coloured ring between the near zone and the distant hills. This was a read-only review by Opus 5.5 at high effort.

## 0. How it was measured

- **The setup.** A throwaway console program, outside the repository and since deleted, referenced a scratch copy of `Vocaterra.Game.Core` (and of `Vocaterra.Game`, for the far tile mesher). It was given the test assembly's name so it could reach the internals. The same code was built in Release and in Debug.
- **The world.** Seed 42 with the shipped `world.json`, `biomes.json` and `underground.json`; 24 land chunk columns; each figure the fastest of 3 passes.
- **The machine.** A Ryzen 7 5700X (8 cores, 16 threads, AVX2) while the owner's game and another agent's full test suite were running. The load average was 9 to 25, so figures are about ±20%, and multi-thread figures are the least reliable.
- **Labels.** "Measured" means this bench. "Estimate" means arithmetic on the measured figures.

## 1. Where the time goes (measured)

| Item | Release | Debug | Debug ÷ Release |
|---|---|---|---|
| One surface chunk, `Generate(coord)` | 1.02 ms | 8.39 ms | 8.2 |
| One deep chunk (layer −4) | 0.88 ms | 8.06 ms | 9.2 |
| One air chunk (top layer): a whole window for nothing | 0.66 ms | 5.12 ms | 7.8 |
| Whole column, 24 layers | 3.68 ms | 21.1 ms | 5.7 |
| Residency column (`SurfaceBand`, then `GenerateLayers` band ±1) | 2.9 ms CPU | 18.6 ms | 6.4 |
| Ring footprint, 32×32 blocks: bands 5.5 + generate 11.3 + ingest 4.5 | 21.3 ms | 121 ms | 5.7 |
| Far model stack, levels 0 / 1 / 2 / 3 | 54–61 / 227 / 243–313 / 373–440 ms | 296 / 1,199 / 1,450 / 2,086 ms | 4.8–5.6 |
| Far model stack, levels 4 to 7 | 190–250 ms | 1,050–1,300 ms | about 5.3 |
| Cold far tile on land (5 model stacks and the mesh), L0 / L1 / L2 / L3 / L4–L7 | 349 / 1,121 / 1,259 / 2,214 / 965–1,192 ms | – | – |
| Cold far tile over open sea, L0 / L1 / L3 / L7 | 115 / 352 / 548 / 1,020 ms | – | – |
| Warm far tile (stacks already in the store, so 5 reads and the mesh), L0 / L1 / L2+ | 18–36 / 7–12 / 1–5 ms | – | – |
| First drainage read of a landmass, under one global lock | 0.12–0.53 s | 0.3–0.7 s (B27) | – |

**Per column, by stage** (Release ms / Debug ms; a column averages 2.1 band layers, 6.0 deep layers and 15.9 air layers):

| Stage | Release | Debug |
|---|---|---|
| Window: climate `Fill` over the 42×42 window | 0.45 | 2.2 |
| Window: height model `Shape`, including drainage | 0.37 | 2.7 |
| Shapers (banks, waterside) | 0.10 | 0.39 |
| Plans (trees, caves, entrances) | 0.07 | 0.36 |
| Band layers, all passes | 0.64 | 3.4 |
| ...of which the strata | 0.41 | 1.9 |
| Deep layers, all passes | 1.79 | 11.6 |
| ...of which the strata | 1.00 | 4.9 |
| ...of which the caves | 0.36 | 2.1 |
| ...of which the linings | 0.15 | 1.6 |
| Trees, undergrowth, block writes, `ToData` | under 0.05 each | – |

**Primitives, per column (µs, Release):**

| Primitive | µs |
|---|---|
| `Climate.At` (4 corners × 6 fields, nothing shared) | 7.95 |
| One lattice point (6 fields) | 2.03 |
| `Climate.Fill`, per column of a window | 0.23 |
| `HeightModel.Shape` | 0.32 |
| `FarTerrainSampler.At` | 50.9 |

Other measured points:
- **Allocation:** about 450 KB per column, 295 KB of it the window. With 8 threads that is 47 gen-0 collections a second.
- **Locks and dictionaries:** no lock or dictionary cost shows in the chunk path. Drainage's river lookup is 0.2 ms per window since B27.
- **Thread scaling:** 8 threads gave 3.4 times one thread. CPU time per column rose only 17 to 25%, so the threads mostly waited for cores on the busy machine, not on locks.
- **The far store:** a level-0 stack read from memory is 0.30 ms. Pack and Unpack of a mixed section are 0.17 and 0.13 ms. The disk path is not a bottleneck.

**A fresh 16 km High view (estimate from the figures above):**
- **Tiles:** 1,348 leaves: L0 296, L1 180, L2 152, L3 148, L4 to L6 156 each, L7 104. 610 of them lie inside the 1,000-block real radius.
  - At about 1.3 new stacks per leaf and 60% land, the tiles cost about 300 worker-seconds.
  - Model stacks are about 90% of that and meshing about 8%.
- **The ring:** about 3,100 footprints at 21 ms each, so 65 to 85 worker-seconds.
- **The near view:** about 10 to 20 worker-seconds.
- **Total:** about 400 worker-seconds. That agrees with B07d's 110 to 180 s at 8 workers on a machine at load 15 to 25.
- **B07c's "33 ms a far tile" is out of date.** It was measured before B11b put cliff bands into the far sampler, which is where the eight neighbour reads come from. That is probably most of the 5 to 10 times rise.

## 2. Duplicated work

1. **The far sampler recomputes everything per sample.**
   - `FarTerrainSampler.At` builds its climate from four lattice corners with nothing reused (`Climate.At` costs 34 times `Fill` per column).
   - `SurfaceRock.Adjust` then reads 8 neighbours. Each read goes through a new `FarGround`, which recomputes climate and shape from nothing, so a dry column costs about 10 climate-plus-shape evaluations.
   - `TryRiver` samples the same centre again, plus 3 more `Climate.At`; `Even` adds 8 more for water above the sea.
   - At levels 1 and 2 the samples are 1 to 2 blocks apart, so each lattice point is evaluated dozens of times.
2. **Fine far tiles are built twice inside the real radius.** L0 to L2 tiles there (610 tiles) are built from the model, at 0.2 to 0.3 s per stack, then built again when the ring makes their footprints real. The model work is thrown away.
3. **A far tile computes four whole neighbour stacks** for a one-voxel border. They are mostly reused by the neighbour's own tile, except at the edges of each level's band.
4. **The window overlaps 6.9 times.** Each column's window is 42×42 columns (margin 13) for its 16×16 own columns, so neighbouring columns compute the same climate and shape about 7 times. The window is half the cost of a residency column.
5. **The window is built twice per column.** `SurfaceBand` builds and shapes a whole window (0.66 ms), then `GenerateLayers` builds the same one again. That is about 23% of residency CPU and 26% of a ring footprint.
6. **`Generate(coord)` builds a whole window per chunk**, even for an air chunk (0.66 ms; 5 ms in Debug).
   - `VoxelGrid.HighestBlock` calls it layer by layer from the top down on the game thread: up to about 15 air chunks per call.
   - At 50 to 110 calls per start or teleport (B21), that is about 30 to 70 ms of game-thread time in Release, and 0.3 to 0.6 s in Debug.
7. **The ring regenerates the voxel zone's resident chunks.** The zone is 400 of the 1,000 blocks at High, so about 16% of footprints.
8. **Buried chunks are already well skipped.** The band skips them, the near zone holds 4 chunks, and the ring starts 2 layers under the band. The ring's 67 air layers per footprint cost almost nothing. No large win is left here.
9. **The far store reads some things twice.** `ReadFromDisk` unpacks a section to validate it and `Get` unpacks it again. `IsWhollyReal` reads whole blobs to test one byte. Both are small.

## 3. Ranked recommendations

The fill-time effects are estimates unless marked measured. Near is the near view after a teleport; far is the 16 km fill.

| # | Change | Near | Far (16 km) | Cost | Risk to pins, determinism, purity | Owner? |
|---|---|---|---|---|---|---|
| 1 | **Far sampler shares its work.** Per stack (or per tile), take climate from a lattice table (`Fill`) and compute each column's climate and shape once, reused by the cliff test, `TryRiver` and `Even`, in per-call arrays (`World/Far` is a pure folder). | Indirect: workers freed sooner | L0–L2 stacks 8–14×, L3 3.3× (**measured, sections byte-identical**). Fill −30%; inner ring 3–4× sooner | M | None: same integer arithmetic. Add a test comparing old and new packed sections | No |
| 2 | **A stand-in for the inner ring.** For each leaf with no built ancestor within `FallbackSteps`, request its level-4 ancestor at coarse priority. | – | The sky gap is gone within about 3 s (about 40 L4 tiles, about 9 worker-seconds today) | S | None (rendering only) | No |
| 3 | **Cheap coarse levels (L3 to L7).** These are about 75% of far CPU once #1 is done; at levels 4 to 7 the samples are 32 to 128 blocks apart, so a lattice cache gives nothing (measured 0.9–1.4×). Take climate at the nearest lattice point, the cliff test from the child grid instead of ±1-block neighbours, and the river pooling from the grid, or set the existing `MergeLevels` to 3 (one sample per cell above level 3). | – | L3–L7 4–10×. With #1, far fill about 2–2.5× | M | The far picture changes slightly; no world pins | Visual sign-off |
| 4 | **No model-built fine tiles inside the real radius.** Show the stand-in until the ring's data lands, and give ring footprints the priority of the tiles that wait for them (the ring gets 1 job in 5 while tiles queue). | – | Removes about 610 double builds; about 10–15 worker-seconds after #1, and the detail arrives in order | S–M | None | No |
| 5 | **One shaped window per column, shared by groups of columns.** A 4×4-column window for residency; one window per ring footprint over the 4×4 columns its bands read; band and layers from the same context. | Residency CPU −35–45% | Ring −25% | M–L | Byte-identical by the invariant that a wider window changes no chunk (the `extraMargin` tests); proved by the goldens | No |
| 6 | **Optimise `StrataPass`** (40% of decoration, 0.17 ms per deep chunk): skip rows whose height band misses, use the lattice minimum and maximum before any per-block work, no `Array.IndexOf` per block. | −10–15% | Ring −8% | S–M | Must stay byte-identical (the goldens check it) | No |
| 7 | **The ring ingests resident chunks** instead of making them again. | – | Ring −16% at High | S | None | No |
| 8 | **GC.** Server GC (+28% at 8 threads in one noisy run) or a 64 MB gen-0 budget (4 times fewer gen-0 collections, about 0–15%). Pool the window's arrays through caller-owned scratch: the purity regex allows it, while a `[ThreadStatic]` cache would need an `ArchitectureRulesTests` allow-list line, as `DrainageNetwork` has. | 5–25% | 5–25% | S | None | Server GC raises memory (a machine limit) |
| 9 | **Jobs and priority.** A cold L1–L3 tile is a 1–2 s job today, which holds residency jobs back after a teleport; #1 and #3 shorten them. `ThreadPriority.BelowNormal` most likely has no effect on Linux (not checked here: the game had exited before its thread nice values could be read); set nice 5–10 on the worker threads through `setpriority` (libc P/Invoke, no package). | Smoother, sooner | Smoother | S | None | No |
| 10 | **Drainage warmed ahead.** Warm the landmasses within the far range at start and on a teleport, and give each landmass its own lock. First reads are 0.12–0.53 s each (measured), now serialised under one global lock. The network is the same whoever reads it first. | Up to 1 s | A few seconds at start | S–M | The lock is in the allowed file; the test's lock allowance must cover the new locks | No |
| 11 | **`HighestBlock`:** use the band, or one `GenerateLayers`, instead of one `Generate` per layer. | Game-thread hitch −30–70 ms per teleport (more in Debug) | – | S | None | No |
| 12 | **Release builds.** Measured 5–8× for generation. The owner's game is already Release. Tests and scripted runs are Debug: see `docs/research/2026-11-17-ai-development-throughput.md`, item 2; the CPU budgets need re-setting. | – | – | S | None (no `#if DEBUG` in `src`) | Yes (already raised) |
| 13 | **Runtime settings.** Keep tiered PGO: turning it off was 5–35% slower (measured). ReadyToRun speeds up start-up JIT only, with no gain on fill. | – | – | S | – | No |
| 14 | **SIMD on the noise.** AVX2 on this CPU has no 64-bit multiply, and the noise hash is made of 64-bit multiplies; a batched kernel might give 1.3–2× on noise. Worth doing only after #1 to #6. | small | small | L | Determinism keeps (integers); purity keeps | No |
| 15 | **A coarser lattice for the continentalness.** It has 5 chained warps and a 6-octave fractal per 4-block lattice point, about half of each point's 2 µs; a 16-block lattice cuts climate cost about 40% in both paths. | −10% | −10–20% | S | **Changes the world:** re-record every pin | Yes (world change); not now |

## 4. Do these first

| Step | Fill after a teleport (start) | What changes |
|---|---|---|
| Today (B07d: High, 16 km, 8 workers, load 15–25) | 110 s (180 s) | The inner ring (400–1,800 blocks) shows sky for most of the fill |
| 1. Stand-ins (#2) | Unchanged | The gap is covered within about 3 s of arrival |
| 2. Far sampler sharing, identical output (#1) | about 80–130 s | The inner ring's detail comes 3–4× sooner |
| 3. Cheap coarse levels (#3, after a visual sign-off) | about 45–75 s | The horizon is no longer the cost; the ring is |
| 4. Fine tiles wait for the ring; the ring ingests resident chunks (#4, #7) | about 40–65 s | No tile is built twice |
| 5. Shared windows and the strata pass (#5, #6) | about 30–50 s | Near view after a jump about 2× faster; 16 km `--pregen` from about 48 to about 30 minutes |

- **On a quiet machine** expect roughly half to two-thirds of these times.
- **The sky gap.** It comes from three things together:
  - Coarse-first ordering divides the distance of level 4 and up by 16, so the horizon is built first.
  - L0 to L3, from about 384 to 1,800 blocks, come last, at 1 to 2.2 s per cold tile.
  - B07e's stand-ins draw only ancestors that are already built, and on a fresh world nothing has built the ancestors of those inner leaves.
- **The fix.** Recommendation 2 alone puts coarse land there within seconds, as B07e intended. Recommendation 1 then makes the real detail arrive 3 to 4 times faster.

## 5. Caveats

- The machine was loaded, so treat every figure as ±20%. The thread-scaling and GC figures come from single runs.
- The tile figures are for single positions (4 on land, 1 at sea), and the 60% land share is assumed.
- The prototype for recommendation 1 used dictionaries cleared before each stack. A real version with arrays would be at least as fast; it is not a design.
- The far-view budget assumes about 1.3 new stacks per leaf.
- The near-view effects are estimates: B21's 3.1 s near view was measured with 2 workers, and the 8-worker figure was not measured.
- Nothing was changed in the repository. The scratch folder `artifacts/tmp/chunkgen-review` has been removed, and nothing is left running.
