# The LOD work on feature/world-next: two reviews (2026-11-19)

*Two read-only reviews of `feature/world-next` at `e3041981` (B35 to B48, PR #72), asked for by the owner after his flights showed sky holes,
and set against his `~/Downloads/Vocaterra_LOD_Transitions_High_Level_Summary.md`. Saved by the orchestrator, who checked findings 1 and 3
against the code (both confirmed). The milestones they lead to are B49 to B53 in the game plan's section 4.14 and the handover
[2026-11-19-2035-lod-lifecycle](../handovers/done/2026-11-19-2035-lod-lifecycle.md).*

## Review 1 (Claude, three reviewers by slice: far generation, far-view drawing, streaming and scheduling)

**Verdict.** The generation work (B36, B37, B39, B40) is sound: the byte-identity claims hold and the digest history is clean. The far-view
lifecycle has two confirmed bugs that break the rule "keep the old LOD until its replacement is on the card"; the first fits the owner's
64-block holes better than B47's explanation. Two scheduling problems undo B38's ordering. The tests miss all of this because they run
without workers and with the ring radius equal to the voxel radius.

### Confirmed in the code

1. **A drawn stand-in is thrown away when its sections change** (a tile-sized hole). `FarView.Invalidate` (about line 601) keeps a changed
   tile only if it is in `_wanted` and needed; `Collect` takes a built stand-in out of `_wanted`. The ring makes footprints real, which writes
   mips at levels 1 and up through `LodStore.Changed`; the level-1 or level-4 tile covering waiting leaves is disposed while drawn, and its
   leaves show sky until each is built at fine priority, behind every coarse job. Matches "64-block square holes, the neighbours' skirts as
   walls". (Orchestrator: confirmed; the else branch disposes any built tile not in `_wanted`.)
2. **A stand-in can stay "being built" forever after a re-selection race.** Re-selection resets `_wanted` without the stand-ins in flight; a
   worker can drop the job as stale before `RequestStandIns` re-adds the key; the key stays in `_building`, the ancestor counts as covered
   and is never asked for again; the leaves under that 512-block tile stay uncovered until each is built (up to 240 s under the ring wait).
   The window is small but opens about 24 times a second at 190 blocks a second.
3. **The drainage warm-up jumps ahead of coarse tiles, stand-ins and the ring.** `FarView.WarmDrainage` submits rows on `JobLane.Ring` at
   priority `|row - eyeZ|` without `FineBias` (about line 566), and B38's `JobPool.TakeLocked` lets a lower-priority ring job go first.
   Contradicts the comment "behind every tile and chunk job"; B40 measured the warm-up before B38's rule. (Orchestrator: confirmed.)
4. **A failed ring footprint is not retried until the eye moves 32 blocks** (`LodRing`); `Awaits` keeps returning true, so the fine tiles over
   it wait 240 s and a scripted `Settle` runs to its limit.
5. **`FarCoarseLevelsTests` "Level 3 ... the same" compares the code with itself** (coarseFrom 4 and 99 take the same path at level 3). The
   real guard is the level-3 digests in `FarSectionDigestTests`, which hold.

### Reported, likely, not re-checked

6. Duplicate drainage reads: B40's lock is per start node, so warm rows starting on different nodes of one continent each read the whole
   continent (up to 8 full reads with 8 workers); `_reading` grows without bound.
7. Shared sea nodes depend on publish order (`DrainageNetwork` publish overwrites built nodes): a rare crack in determinism.
8. Stand-ins with no windows can be evicted by `Trim`; the next leaf to wait under one gets a short hole.
9. A backdrop leaf under the chunks can make a stand-in "unfit", so it draws whole and brings back B42's flicker.
10. `IsMeshedAround` is false when no chunk is near the eye (spawning or flying high), so the gate holds all far work for 20 s.
11. `LodRing.Awaits` measures from `_eye` while the plan uses `_plannedFor`: rim footprints count as awaited but were never planned.
12. Performance: each `FarScope` allocates about 6 MB on the large-object heap at L2/L3 (the corner table about 16 times oversized); draw
    calls per stand-in unbounded since the 128-window cap went; `Assemble` allocates every frame and runs twice on frames with uploads;
    `ColumnBandCache` serialises the valley's group bands.
13. Latent: `ColumnGroup.CopyWindowTo` has no bounds check; coarse levels assume `latticeBlocks` = 4; two far digest places are identical
    (coast far = sea far).

### Why the gates passed

Every `FarView` test uses `JobPool(0)` and puts pending tiles on the card in the same frame, so a hole lasting one build and the race in 2
cannot happen; the flight and ring-wait tests set `RealRadius` equal to the voxel radius (the game: 1,000 against 400 to 576), so 1 is never
exercised; `Uncovered == 0` is asserted only once a fill is complete, never during it.

### Against the owner's LOD summary

| Technique | On the branch | Recommendation |
|---|---|---|
| Overlapping LODs | Stand-ins (B35), old buffers kept until the swap, B42/B47 windows | Fix the lifecycle (1, 2, 8) and make it an invariant with a test: the summary's priority 1 and the real gap |
| Hysteresis | None: `FarTileSelector` depends on position alone and re-selects every 8 blocks | Split at `SplitDistance`, merge beyond about 1.15 times; same for the gate's reopen rule |
| Dithered cross-fade | None: swaps pop | GL path only, an 8-frame complementary Bayer mask at stand-in hand-over and the voxel/far ring; after the lifecycle fix |
| Seam stitching | Skirts (`FarTileMesher`) | Keep; for the zone-edge sky line, overlap the far leaves a chunk under the voxel zone, drawn behind it |
| Screen-space error | Distance-based `SplitDistance` | Not now (equivalent at a fixed field of view) |
| Geomorphing / Transvoxel | None | Do not add (cube faces) |
| Materials, lighting, fog | Planar fog, day-baked far tint | A QA pass at night and in weather comparing far and near colour at the ring |
| Precision | Absolute floats | Not needed (about 0.002 blocks at 16 km) |
| Validation | Stream stats, B43 counters | A test with real workers, the game's ring radius, a flight back and forth, uncovered leaves asserted every frame |

## Review 2 (Codex)

The best first investment is keeping terrain visible while it is replaced, then stitching the coarse-to-fine edge (these address the
videos); hysteresis is the cheapest jitter fix; screen-space error has the most potential to reduce rendering work; cross-fading improves
looks at some GPU cost while it runs.

- **Keep the old LOD until the replacement is ready.** Movement can cancel a needed stand-in, and a data refresh can dispose one on screen
  (selection `FarView.cs:485`, invalidation `:618`). In a stationary probe one refresh took uncovered leaves from 0 to 64; target 0.
- **Stitch LOD seams.** A coarse tile can be cut inside its perimeter for finer terrain; the mesh probe found a five-block opening along
  such an exposed interior edge (`FarTileMesher.cs:730`). Add joining geometry there and where coarse land meets chunks.
- **Hysteresis:** separate enter and exit distances per level (`FarTileSelector.cs:57`); zero switches for reversals inside the band.
- **Dithered cross-fade:** a 0.2 to 0.4 s trial for conspicuous changes, both shader settings, capped simultaneous fades.
- **Screen-space error:** a per-tile geometric error and its projected pixels (`FarViewSettings.cs:23`); the best candidate for a
  throughput gain (one parent instead of four children where the difference is invisible); ridges may need more detail.
- **Geomorphing:** behind the rest (no vertex mapping between block-face levels).
- **Materials, lighting, atmosphere:** compare shoreline, forest and snow edges by day and night; fix surviving water-height or colour
  mismatches.
- **Shared data and edits:** refresh affected tiles while retaining their displayed mesh.
- **Camera-relative rendering:** test far from the origin; add it only if jitter grows with distance.
- **Measurements:** B45's near-first trade (near view 5.2 to 3.3 s, whole view 11.5 to 9.3 s, first far coverage 11.1 to 12.6 s) argues for
  prioritising the cheapest tile that covers exposed sky; **the current flight test still has 316 of 1,200 frames with an uncovered leaf
  with the real ring**. Judge the next changes by frames where visible ground disappears, visible seam pixels and the 99th-percentile frame
  time on the same flight.

## Where they agree

Both put "never retire a drawn LOD" first, then the zone-edge seam (review 1: overlap under the voxel zone; review 2: joining geometry),
then hysteresis, with cross-fade after and geomorphing not at all. Review 2 alone ranks screen-space error as a throughput lever.
